import importlib.util
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch


MODULE_PATH = Path(__file__).with_name("fetch_sheet.py")
SPEC = importlib.util.spec_from_file_location("fetch_sheet", MODULE_PATH)
fetch_sheet = importlib.util.module_from_spec(SPEC)
assert SPEC and SPEC.loader
SPEC.loader.exec_module(fetch_sheet)


class FetchSheetTests(unittest.TestCase):
    def test_parse_spreadsheet_url_and_sheet(self):
        token, sheet = fetch_sheet.parse_spreadsheet_reference(
            "https://acme.feishu.cn/sheets/sht123?sheet=abc789"
        )
        self.assertEqual(token, "sht123")
        self.assertEqual(sheet, "abc789")

    def test_parse_wiki_url(self):
        kind, token, sheet = fetch_sheet.parse_source_reference(
            "https://acme.feishu.cn/wiki/wik123"
        )
        self.assertEqual((kind, token, sheet), ("wiki", "wik123", None))

    def test_column_names(self):
        self.assertEqual(fetch_sheet.column_name(0), "A")
        self.assertEqual(fetch_sheet.column_name(25), "Z")
        self.assertEqual(fetch_sheet.column_name(26), "AA")
        self.assertEqual(fetch_sheet.column_name(99), "CV")

    def test_normalizes_unique_game_row_and_link(self):
        rows = [
            ["说明"],
            ["游戏 ID", "商业化方案", "CDN 路径", "目标平台", "工程路径"],
            [
                "GAME-001",
                {"text": "方案文档", "link": "https://acme.feishu.cn/docx/doc123"},
                "game/wx/one/",
                "微信",
                "D:/Games/One",
            ],
        ]
        result = fetch_sheet.normalize_table(
            rows, " game-001 ", fetch_sheet.load_aliases(None), strict=True
        )
        self.assertEqual(result["header_row"], 2)
        self.assertEqual(result["data_row"], 3)
        self.assertEqual(result["fields"]["game"], "GAME-001")
        self.assertEqual(
            result["links"]["commercialization_document"],
            ["https://acme.feishu.cn/docx/doc123"],
        )

    def test_rejects_duplicate_game_rows(self):
        rows = [
            ["游戏", "商业化方案"],
            ["Same", "a"],
            [" same ", "b"],
        ]
        with self.assertRaisesRegex(fetch_sheet.InputError, "multiple rows"):
            fetch_sheet.normalize_table(rows, "SAME", fetch_sheet.load_aliases(None))

    def test_strict_mode_rejects_missing_columns(self):
        rows = [["游戏", "商业化方案"], ["One", "doc"]]
        with self.assertRaisesRegex(fetch_sheet.InputError, "Strict mode"):
            fetch_sheet.normalize_table(
                rows, "One", fetch_sheet.load_aliases(None), strict=True
            )

    def test_custom_aliases_are_appended(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "aliases.json"
            path.write_text(
                json.dumps({"game": ["内部代号"]}, ensure_ascii=False), encoding="utf-8"
            )
            aliases = fetch_sheet.load_aliases(str(path))
        result = fetch_sheet.normalize_table(
            [["内部代号", "商业化方案"], ["X-1", "doc"]], "X-1", aliases
        )
        self.assertEqual(result["fields"]["game"], "X-1")

    def test_resolve_sheet_requires_unique_selection(self):
        sheets = [
            {"sheet_id": "a", "title": "A", "hidden": False},
            {"sheet_id": "b", "title": "B", "hidden": False},
        ]
        with self.assertRaisesRegex(fetch_sheet.InputError, "ambiguous"):
            fetch_sheet.resolve_sheet(sheets, None)
        self.assertEqual(fetch_sheet.resolve_sheet(sheets, "B")["sheet_id"], "b")

    def test_oauth_and_legacy_token_fields_are_distinct(self):
        class FakeClient(fetch_sheet.FeishuClient):
            def __init__(self):
                super().__init__()
                self.calls = []

            def _request(self, method, url, headers=None, body=None):
                self.calls.append((method, url, headers, body))
                if url == fetch_sheet.OAUTH_URL:
                    return {"code": 0, "access_token": "oauth-token"}
                return {"code": 0, "tenant_access_token": "legacy-token"}

        client = FakeClient()
        self.assertEqual(client.authenticate("id", "secret", "oauth"), "oauth-token")
        self.assertEqual(client.calls[0][3]["grant_type"], "client_credentials")
        self.assertEqual(client.authenticate("id", "secret", "legacy"), "legacy-token")

    def test_sheet_and_range_api_paths(self):
        class FakeClient(fetch_sheet.FeishuClient):
            def __init__(self):
                super().__init__()
                self.urls = []

            def _request(self, method, url, headers=None, body=None):
                self.urls.append(url)
                if url.endswith("/sheets/query"):
                    return {"code": 0, "data": {"sheets": [{"sheet_id": "tab"}]}}
                return {
                    "code": 0,
                    "data": {"valueRange": {"values": [["游戏", "商业化方案"]]}},
                }

        client = FakeClient()
        self.assertEqual(client.list_sheets("sht-token", "token")[0]["sheet_id"], "tab")
        self.assertEqual(
            client.read_range("sht-token", "tab!A1:B2", "token"),
            [["游戏", "商业化方案"]],
        )
        self.assertIn("/sheets/v3/spreadsheets/sht-token/sheets/query", client.urls[0])
        self.assertIn("/values/tab!A1:B2", client.urls[1])

    def test_wiki_node_api_path(self):
        class FakeClient(fetch_sheet.FeishuClient):
            def __init__(self):
                super().__init__()
                self.url = ""

            def _request(self, method, url, headers=None, body=None):
                self.url = url
                return {
                    "code": 0,
                    "data": {"node": {"obj_type": "sheet", "obj_token": "sht123"}},
                }

        client = FakeClient()
        node = client.resolve_wiki_node("wik123", "token")
        self.assertEqual(node["obj_token"], "sht123")
        self.assertTrue(client.url.endswith("/wiki/v2/spaces/get_node?token=wik123"))

    def test_config_merge_and_cli_override(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "config.json"
            path.write_text(
                json.dumps(
                    {
                        "source": {
                            "url": "https://acme.feishu.cn/wiki/wik123",
                            "target_game": "FROM-CONFIG",
                        },
                        "auth": {
                            "mode": "oauth",
                            "app_id_env": "FEISHU_APP_ID",
                            "app_secret_env": "FEISHU_APP_SECRET",
                        },
                        "limits": {"max_rows": 123},
                        "strict": True,
                    }
                ),
                encoding="utf-8",
            )
            parsed = fetch_sheet.build_parser().parse_args(
                ["--config", str(path), "--target-game", "CLI-GAME"]
            )
            merged = fetch_sheet.merge_options(parsed)
        self.assertEqual(merged.spreadsheet, "https://acme.feishu.cn/wiki/wik123")
        self.assertEqual(merged.target_game, "CLI-GAME")
        self.assertEqual(merged.max_rows, 123)
        self.assertTrue(merged.strict)

    def test_config_rejects_inline_secret(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "config.json"
            path.write_text(
                json.dumps({"source": {}, "auth": {"app_secret": "do-not-store"}}),
                encoding="utf-8",
            )
            with self.assertRaisesRegex(fetch_sheet.InputError, "must not contain"):
                fetch_sheet.load_config(str(path))

    def test_dpapi_config_round_trip(self):
        if fetch_sheet.os.name != "nt":
            self.skipTest("Windows DPAPI test")
        protected = fetch_sheet.protect_config_value("cli_test_value")
        self.assertNotIn("cli_test_value", protected)
        self.assertEqual(fetch_sheet.unprotect_config_value(protected), "cli_test_value")

    def test_run_resolves_wiki_before_reading_sheet(self):
        class FakeClient:
            def __init__(self, timeout, retries):
                self.calls = []

            def authenticate(self, app_id, app_secret, mode):
                return "token"

            def resolve_wiki_node(self, node_token, access_token):
                self.calls.append(("wiki", node_token))
                return {
                    "obj_type": "sheet",
                    "obj_token": "sht-real",
                    "space_id": "space-1",
                    "title": "Info",
                }

            def list_sheets(self, spreadsheet_token, access_token):
                self.calls.append(("sheets", spreadsheet_token))
                return [
                    {
                        "sheet_id": "tab1",
                        "title": "Games",
                        "hidden": False,
                        "grid_properties": {"row_count": 10, "column_count": 5},
                    }
                ]

            def read_range(self, spreadsheet_token, range_text, access_token):
                self.calls.append(("range", spreadsheet_token, range_text))
                return [
                    ["游戏", "商业化方案", "CDN路径", "目标平台", "工程路径"],
                    ["G-1", "doc", "", "微信", "D:/Game"],
                ]

        options = fetch_sheet.argparse.Namespace(
            **{
                **fetch_sheet.DEFAULT_OPTIONS,
                "spreadsheet": "https://acme.feishu.cn/wiki/wik123",
                "target_game": "G-1",
                "strict": True,
            }
        )
        with patch.object(fetch_sheet, "FeishuClient", FakeClient), patch.dict(
            fetch_sheet.os.environ,
            {"FEISHU_APP_ID": "id", "FEISHU_APP_SECRET": "secret"},
        ):
            result = fetch_sheet.run(options)
        self.assertEqual(result["source"]["spreadsheet_token"], "sht-real")
        self.assertEqual(result["source"]["reference_type"], "wiki")
        self.assertEqual(result["fields"]["game"], "G-1")


if __name__ == "__main__":
    unittest.main()
