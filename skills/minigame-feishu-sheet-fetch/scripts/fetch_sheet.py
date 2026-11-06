#!/usr/bin/env python3
"""Read one game row from a Feishu spreadsheet without writing back."""

from __future__ import annotations

import argparse
import base64
import ctypes
import json
import os
import re
import sys
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path
from ctypes import wintypes
from typing import Any


API_BASE = "https://open.feishu.cn/open-apis"
OAUTH_URL = "https://accounts.feishu.cn/oauth/v3/token"
LEGACY_AUTH_URL = f"{API_BASE}/auth/v3/tenant_access_token/internal"

DEFAULT_ALIASES: dict[str, list[str]] = {
    "game": [
        "游戏标识", "游戏ID", "游戏编号", "游戏名称", "游戏名", "游戏", "项目名称", "项目"
    ],
    "commercialization_document": [
        "商业化方案", "商业化文档", "变现方案", "商业化需求"
    ],
    "cdn_prefix": ["CDN路径", "CDN地址", "资源路径", "远程资源路径"],
    "target_platform": ["目标平台", "平台", "小游戏平台", "发布平台"],
    "project_path": ["工程路径", "项目路径", "工程", "仓库", "仓库地址", "Git仓库"],
}

REQUIRED_FIELDS = tuple(DEFAULT_ALIASES)

DEFAULT_OPTIONS: dict[str, Any] = {
    "spreadsheet": None,
    "sheet": None,
    "target_game": None,
    "header_row": None,
    "cell_range": None,
    "max_rows": 5000,
    "max_columns": 100,
    "aliases": None,
    "strict": False,
    "list_sheets": False,
    "output": None,
    "auth_mode": "oauth",
    "app_id_env": "FEISHU_APP_ID",
    "app_secret_env": "FEISHU_APP_SECRET",
    "credential_provider": None,
    "app_id_dpapi": None,
    "app_secret_dpapi": None,
    "timeout": 30.0,
    "retries": 2,
}


class InputError(RuntimeError):
    """The source data cannot be mapped safely."""


class ApiError(RuntimeError):
    """Authentication, transport, or Feishu API failure."""


def dpapi(data: bytes, decrypt: bool = False) -> bytes:
    if os.name != "nt":
        raise InputError("DPAPI credentials require Windows and the importing user account.")

    class Blob(ctypes.Structure):
        _fields_ = [("size", wintypes.DWORD), ("data", ctypes.POINTER(ctypes.c_ubyte))]

    buffer = ctypes.create_string_buffer(data)
    source = Blob(len(data), ctypes.cast(buffer, ctypes.POINTER(ctypes.c_ubyte)))
    target = Blob()
    crypt = ctypes.WinDLL("crypt32", use_last_error=True)
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel.LocalFree.argtypes = [ctypes.c_void_p]
    kernel.LocalFree.restype = ctypes.c_void_p
    call = crypt.CryptUnprotectData if decrypt else crypt.CryptProtectData
    second_type = ctypes.POINTER(wintypes.LPWSTR) if decrypt else wintypes.LPCWSTR
    call.argtypes = [
        ctypes.POINTER(Blob), second_type, ctypes.POINTER(Blob), ctypes.c_void_p,
        ctypes.c_void_p, wintypes.DWORD, ctypes.POINTER(Blob),
    ]
    call.restype = wintypes.BOOL
    if not call(ctypes.byref(source), None, None, None, None, 1, ctypes.byref(target)):
        raise InputError("Windows DPAPI credential operation failed.")
    try:
        return ctypes.string_at(target.data, target.size)
    finally:
        kernel.LocalFree(target.data)


def protect_config_value(value: str) -> str:
    return base64.b64encode(dpapi(value.encode("utf-8"))).decode("ascii")


def unprotect_config_value(value: str) -> str:
    try:
        encrypted = base64.b64decode(value, validate=True)
        return dpapi(encrypted, decrypt=True).decode("utf-8")
    except (ValueError, UnicodeDecodeError) as error:
        raise InputError("Configured DPAPI credential is invalid.") from error


def normalize_key(value: Any) -> str:
    text = cell_text(value).strip().casefold()
    return re.sub(r"[\s_\-—–:：/\\]+", "", text)


def cell_text(value: Any) -> str:
    if value is None:
        return ""
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, (str, int, float)):
        return str(value)
    if isinstance(value, list):
        return "".join(cell_text(item) for item in value)
    if isinstance(value, dict):
        for key in ("text", "value", "name", "link", "url"):
            if key in value and value[key] not in (None, ""):
                return cell_text(value[key])
        return json.dumps(value, ensure_ascii=False, sort_keys=True)
    return str(value)


def cell_links(value: Any) -> list[str]:
    found: list[str] = []

    def visit(item: Any) -> None:
        if isinstance(item, str):
            if re.match(r"^https?://", item.strip(), re.IGNORECASE):
                found.append(item.strip())
        elif isinstance(item, list):
            for child in item:
                visit(child)
        elif isinstance(item, dict):
            for key, child in item.items():
                if key.casefold() in {"link", "url", "href"}:
                    visit(child)
                elif isinstance(child, (dict, list)):
                    visit(child)

    visit(value)
    return list(dict.fromkeys(found))


def parse_source_reference(value: str) -> tuple[str, str, str | None]:
    source = value.strip()
    if not source:
        raise InputError("Spreadsheet URL or token is required.")
    if "://" not in source:
        if not re.fullmatch(r"[A-Za-z0-9_-]+", source):
            raise InputError("Spreadsheet token contains unsupported characters.")
        return "sheet", source, None
    parsed = urllib.parse.urlparse(source)
    sheet_match = re.search(r"/sheets/([^/?#]+)", parsed.path)
    wiki_match = re.search(r"/wiki/([^/?#]+)", parsed.path)
    match = sheet_match or wiki_match
    if not match:
        raise InputError(
            "The URL must contain /sheets/<spreadsheet_token> or /wiki/<node_token>."
        )
    token = urllib.parse.unquote(match.group(1)).strip()
    if not re.fullmatch(r"[A-Za-z0-9_-]+", token):
        raise InputError("Document token contains unsupported characters.")
    sheet = urllib.parse.parse_qs(parsed.query).get("sheet", [None])[0]
    return ("sheet" if sheet_match else "wiki"), token, sheet


def parse_spreadsheet_reference(value: str) -> tuple[str, str | None]:
    kind, token, sheet = parse_source_reference(value)
    if kind != "sheet":
        raise InputError("The reference is a Wiki node, not a direct spreadsheet token.")
    return token, sheet


def load_config(path: str | None) -> dict[str, Any]:
    if not path:
        return {}
    config_path = Path(path).expanduser().resolve()
    try:
        payload = json.loads(config_path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as error:
        raise InputError(f"Cannot read config JSON: {error}") from error
    if not isinstance(payload, dict):
        raise InputError("Config JSON must be an object.")
    forbidden = {"app_id", "app_secret", "access_token", "tenant_access_token"}
    serialized_keys: set[str] = set()

    def collect_keys(value: Any) -> None:
        if isinstance(value, dict):
            for key, child in value.items():
                serialized_keys.add(str(key).casefold())
                collect_keys(child)
        elif isinstance(value, list):
            for child in value:
                collect_keys(child)

    collect_keys(payload)
    exposed = forbidden.intersection(serialized_keys)
    if exposed:
        raise InputError(
            "Config must not contain credentials or tokens: " + ", ".join(sorted(exposed))
        )
    allowed_top = {"source", "auth", "credentials", "limits", "aliases", "strict", "output"}
    unknown_top = set(payload).difference(allowed_top)
    if unknown_top:
        raise InputError("Unknown config keys: " + ", ".join(sorted(unknown_top)))
    source = payload.get("source", {})
    auth = payload.get("auth", {})
    credentials = payload.get("credentials", {})
    limits = payload.get("limits", {})
    if not all(isinstance(item, dict) for item in (source, auth, credentials, limits)):
        raise InputError("Config source, auth, credentials, and limits must be objects.")
    allowed_source = {"url", "sheet", "target_game", "header_row", "range"}
    allowed_auth = {"mode", "app_id_env", "app_secret_env"}
    allowed_credentials = {"provider", "app_id_dpapi", "app_secret_dpapi", "source"}
    allowed_limits = {"max_rows", "max_columns", "timeout", "retries"}
    for label, value, allowed in (
        ("source", source, allowed_source),
        ("auth", auth, allowed_auth),
        ("credentials", credentials, allowed_credentials),
        ("limits", limits, allowed_limits),
    ):
        unknown = set(value).difference(allowed)
        if unknown:
            raise InputError(f"Unknown {label} config keys: " + ", ".join(sorted(unknown)))
    result: dict[str, Any] = {
        "spreadsheet": source.get("url"),
        "sheet": source.get("sheet"),
        "target_game": source.get("target_game"),
        "header_row": source.get("header_row"),
        "cell_range": source.get("range"),
        "auth_mode": auth.get("mode"),
        "app_id_env": auth.get("app_id_env"),
        "app_secret_env": auth.get("app_secret_env"),
        "credential_provider": credentials.get("provider"),
        "app_id_dpapi": credentials.get("app_id_dpapi"),
        "app_secret_dpapi": credentials.get("app_secret_dpapi"),
        "max_rows": limits.get("max_rows"),
        "max_columns": limits.get("max_columns"),
        "timeout": limits.get("timeout"),
        "retries": limits.get("retries"),
        "strict": payload.get("strict"),
        "output": payload.get("output"),
        "aliases": payload.get("aliases"),
    }
    for key in ("aliases", "output"):
        value = result.get(key)
        if isinstance(value, str) and value and not Path(value).is_absolute():
            result[key] = str((config_path.parent / value).resolve())
    return {key: value for key, value in result.items() if value is not None}


def merge_options(parsed: argparse.Namespace) -> argparse.Namespace:
    values = dict(DEFAULT_OPTIONS)
    values.update(load_config(getattr(parsed, "config", None)))
    for key in DEFAULT_OPTIONS:
        value = getattr(parsed, key, None)
        if value is not None:
            values[key] = value
    values["config"] = getattr(parsed, "config", None)
    return argparse.Namespace(**values)


def column_name(index: int) -> str:
    if index < 0:
        raise ValueError("Column index must be non-negative.")
    result = ""
    current = index + 1
    while current:
        current, remainder = divmod(current - 1, 26)
        result = chr(65 + remainder) + result
    return result


def load_aliases(path: str | None) -> dict[str, list[str]]:
    aliases = {key: list(values) for key, values in DEFAULT_ALIASES.items()}
    if not path:
        return aliases
    try:
        custom = json.loads(Path(path).read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as error:
        raise InputError(f"Cannot read aliases JSON: {error}") from error
    if not isinstance(custom, dict):
        raise InputError("Aliases JSON must be an object keyed by canonical field.")
    for key, values in custom.items():
        if key not in aliases or not isinstance(values, list) or not all(
            isinstance(item, str) and item.strip() for item in values
        ):
            raise InputError(f"Invalid aliases entry for {key!r}.")
        aliases[key].extend(values)
    return aliases


def alias_lookup(aliases: dict[str, list[str]]) -> dict[str, str]:
    lookup: dict[str, str] = {}
    for canonical, values in aliases.items():
        for value in values:
            normalized = normalize_key(value)
            existing = lookup.get(normalized)
            if existing and existing != canonical:
                raise InputError(
                    f"Header alias {value!r} maps to both {existing!r} and {canonical!r}."
                )
            lookup[normalized] = canonical
    return lookup


def choose_header(
    rows: list[list[Any]], aliases: dict[str, list[str]], explicit_row: int | None
) -> tuple[int, dict[str, int]]:
    lookup = alias_lookup(aliases)
    candidates: list[tuple[int, int, dict[str, int]]] = []
    indexes = [explicit_row - 1] if explicit_row is not None else range(min(20, len(rows)))
    for row_index in indexes:
        if row_index < 0 or row_index >= len(rows):
            raise InputError("The explicit header row is outside the returned range.")
        mapping: dict[str, int] = {}
        for column_index, cell in enumerate(rows[row_index]):
            canonical = lookup.get(normalize_key(cell))
            if canonical is not None:
                if canonical in mapping:
                    raise InputError(
                        f"Header row {row_index + 1} contains duplicate columns for {canonical}."
                    )
                mapping[canonical] = column_index
        if "game" in mapping and "commercialization_document" in mapping:
            candidates.append((len(mapping), row_index, mapping))
    if not candidates:
        raise InputError(
            "No header row contains both a game column and a commercialization document column."
        )
    candidates.sort(key=lambda item: (-item[0], item[1]))
    _, row_index, mapping = candidates[0]
    return row_index, mapping


def normalize_table(
    rows: list[list[Any]],
    target_game: str,
    aliases: dict[str, list[str]],
    explicit_header_row: int | None = None,
    strict: bool = False,
) -> dict[str, Any]:
    if not rows:
        raise InputError("The selected spreadsheet range returned no rows.")
    header_index, mapping = choose_header(rows, aliases, explicit_header_row)
    target = normalize_key(target_game)
    if not target:
        raise InputError("Target game must not be empty.")
    matches: list[tuple[int, list[Any]]] = []
    game_column = mapping["game"]
    for row_index in range(header_index + 1, len(rows)):
        row = rows[row_index]
        game_value = row[game_column] if game_column < len(row) else None
        if normalize_key(game_value) == target:
            matches.append((row_index, row))
    if not matches:
        raise InputError(f"No row exactly matches target game {target_game!r}.")
    if len(matches) > 1:
        locations = ", ".join(str(index + 1) for index, _ in matches)
        raise InputError(
            f"Target game {target_game!r} matches multiple rows: {locations}."
        )
    row_index, row = matches[0]
    fields: dict[str, str | None] = {}
    links: dict[str, list[str]] = {}
    missing_columns: list[str] = []
    empty_fields: list[str] = []
    for canonical in REQUIRED_FIELDS:
        column = mapping.get(canonical)
        if column is None:
            fields[canonical] = None
            missing_columns.append(canonical)
            continue
        value = row[column] if column < len(row) else None
        text_value = cell_text(value).strip()
        fields[canonical] = text_value
        found_links = cell_links(value)
        if found_links:
            links[canonical] = found_links
        if not text_value and canonical != "cdn_prefix":
            empty_fields.append(canonical)
    if strict and missing_columns:
        raise InputError(
            "Strict mode requires columns: " + ", ".join(missing_columns)
        )
    headers = [cell_text(item).strip() for item in rows[header_index]]
    return {
        "header_row": header_index + 1,
        "data_row": row_index + 1,
        "fields": fields,
        "links": links,
        "missing_columns": missing_columns,
        "empty_fields": empty_fields,
        "headers": headers,
        "raw_row": row,
    }


class FeishuClient:
    def __init__(self, timeout: float = 30.0, retries: int = 2) -> None:
        self.timeout = timeout
        self.retries = retries

    def _request(
        self,
        method: str,
        url: str,
        headers: dict[str, str] | None = None,
        body: dict[str, Any] | None = None,
    ) -> dict[str, Any]:
        encoded = None
        request_headers = {"Accept": "application/json"}
        if headers:
            request_headers.update(headers)
        if body is not None:
            encoded = json.dumps(body, ensure_ascii=False).encode("utf-8")
            request_headers["Content-Type"] = "application/json; charset=utf-8"
        for attempt in range(self.retries + 1):
            request = urllib.request.Request(
                url=url, data=encoded, headers=request_headers, method=method
            )
            try:
                with urllib.request.urlopen(request, timeout=self.timeout) as response:
                    payload = json.loads(response.read().decode("utf-8"))
                if not isinstance(payload, dict):
                    raise ApiError("Feishu returned a non-object JSON response.")
                return payload
            except urllib.error.HTTPError as error:
                retryable = error.code == 429 or 500 <= error.code < 600
                if retryable and attempt < self.retries:
                    time.sleep(2**attempt)
                    continue
                message = ""
                try:
                    failure = json.loads(error.read(4096).decode("utf-8", "replace"))
                    message = str(
                        failure.get("error_description")
                        or failure.get("msg")
                        or failure.get("error")
                        or ""
                    )
                except (json.JSONDecodeError, AttributeError):
                    pass
                raise ApiError(
                    f"Feishu HTTP {error.code}" + (f": {message}" if message else ".")
                ) from error
            except (urllib.error.URLError, TimeoutError, json.JSONDecodeError) as error:
                if attempt < self.retries:
                    time.sleep(2**attempt)
                    continue
                raise ApiError(f"Feishu request failed: {type(error).__name__}.") from error
        raise ApiError("Feishu request failed after bounded retries.")

    @staticmethod
    def _require_success(payload: dict[str, Any], operation: str) -> dict[str, Any]:
        code = payload.get("code", 0)
        if code not in (0, None):
            message = payload.get("msg") or payload.get("error_description") or "unknown error"
            raise ApiError(f"{operation} failed with Feishu code {code}: {message}")
        return payload

    def authenticate(self, app_id: str, app_secret: str, mode: str) -> str:
        if mode == "oauth":
            payload = self._request(
                "POST",
                OAUTH_URL,
                body={
                    "grant_type": "client_credentials",
                    "client_id": app_id,
                    "client_secret": app_secret,
                },
            )
            self._require_success(payload, "OAuth token request")
            token = payload.get("access_token")
        else:
            payload = self._request(
                "POST",
                LEGACY_AUTH_URL,
                body={"app_id": app_id, "app_secret": app_secret},
            )
            self._require_success(payload, "Legacy token request")
            token = payload.get("tenant_access_token")
        if not isinstance(token, str) or not token:
            raise ApiError("Token response did not contain the expected access token.")
        return token

    def list_sheets(self, spreadsheet_token: str, access_token: str) -> list[dict[str, Any]]:
        token = urllib.parse.quote(spreadsheet_token, safe="")
        payload = self._request(
            "GET",
            f"{API_BASE}/sheets/v3/spreadsheets/{token}/sheets/query",
            headers={"Authorization": f"Bearer {access_token}"},
        )
        self._require_success(payload, "List sheets")
        sheets = payload.get("data", {}).get("sheets")
        if not isinstance(sheets, list):
            raise ApiError("List sheets response did not contain data.sheets.")
        return [item for item in sheets if isinstance(item, dict)]

    def resolve_wiki_node(self, node_token: str, access_token: str) -> dict[str, Any]:
        query = urllib.parse.urlencode({"token": node_token})
        payload = self._request(
            "GET",
            f"{API_BASE}/wiki/v2/spaces/get_node?{query}",
            headers={"Authorization": f"Bearer {access_token}"},
        )
        self._require_success(payload, "Resolve Wiki node")
        node = payload.get("data", {}).get("node")
        if not isinstance(node, dict):
            raise ApiError("Wiki node response did not contain data.node.")
        return node

    def read_range(
        self, spreadsheet_token: str, range_text: str, access_token: str
    ) -> list[list[Any]]:
        token = urllib.parse.quote(spreadsheet_token, safe="")
        encoded_range = urllib.parse.quote(range_text, safe="!:")
        payload = self._request(
            "GET",
            f"{API_BASE}/sheets/v2/spreadsheets/{token}/values/{encoded_range}",
            headers={"Authorization": f"Bearer {access_token}"},
        )
        self._require_success(payload, "Read spreadsheet range")
        values = payload.get("data", {}).get("valueRange", {}).get("values")
        if values is None:
            return []
        if not isinstance(values, list):
            raise ApiError("Read range response did not contain a row matrix.")
        return [row if isinstance(row, list) else [row] for row in values]


def resolve_sheet(sheets: list[dict[str, Any]], selector: str | None) -> dict[str, Any]:
    if selector:
        matches = [
            sheet
            for sheet in sheets
            if str(sheet.get("sheet_id", "")) == selector
            or str(sheet.get("title", "")) == selector
        ]
        if len(matches) != 1:
            raise InputError(
                f"Sheet selector {selector!r} matched {len(matches)} sheets; expected one."
            )
        return matches[0]
    visible = [sheet for sheet in sheets if not sheet.get("hidden", False)]
    if len(visible) != 1:
        choices = ", ".join(
            f"{item.get('title')} ({item.get('sheet_id')})" for item in visible
        )
        raise InputError(
            "Sheet is ambiguous; pass --sheet with an ID or exact title. "
            f"Visible sheets: {choices or 'none'}."
        )
    return visible[0]


def write_output(value: dict[str, Any], output: str | None) -> None:
    rendered = json.dumps(value, ensure_ascii=False, indent=2) + "\n"
    if not output:
        sys.stdout.write(rendered)
        return
    target = Path(output).expanduser().resolve()
    target.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(
        "w", encoding="utf-8", dir=target.parent, delete=False, suffix=".tmp"
    ) as temporary:
        temporary.write(rendered)
        temporary_path = Path(temporary.name)
    os.replace(temporary_path, target)
    print(json.dumps({"status": "written", "output": str(target)}, ensure_ascii=False))


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", help="UTF-8 JSON configuration file")
    parser.add_argument("--spreadsheet", default=None, help="Override Sheets/Wiki URL or token")
    parser.add_argument("--sheet", default=None, help="Override sheet ID or exact title")
    parser.add_argument("--target-game", default=None, help="Override exact game ID or name")
    parser.add_argument("--header-row", type=int, default=None, help="One-based header row")
    parser.add_argument("--range", dest="cell_range", default=None, help="A1 range without sheet ID")
    parser.add_argument("--max-rows", type=int, default=None)
    parser.add_argument("--max-columns", type=int, default=None)
    parser.add_argument("--aliases", default=None, help="UTF-8 JSON file with additional aliases")
    parser.add_argument("--strict", action="store_true", default=None, help="Require all canonical columns")
    parser.add_argument("--list-sheets", action="store_true", default=None)
    parser.add_argument("--output", default=None, help="Write normalized JSON atomically")
    parser.add_argument("--auth-mode", choices=("oauth", "legacy"), default=None)
    parser.add_argument("--app-id-env", default=None)
    parser.add_argument("--app-secret-env", default=None)
    parser.add_argument("--timeout", type=float, default=None)
    parser.add_argument("--retries", type=int, default=None)
    return parser


def run(args: argparse.Namespace) -> dict[str, Any]:
    if args.max_rows < 1 or args.max_columns < 1 or args.max_columns > 1000:
        raise InputError("max-rows must be positive and max-columns must be 1..1000.")
    if args.retries < 0 or args.retries > 5:
        raise InputError("retries must be between 0 and 5.")
    if not args.spreadsheet:
        raise InputError("Set source.url in config or pass --spreadsheet.")
    source_kind, source_token, url_sheet = parse_source_reference(args.spreadsheet)
    if not args.list_sheets and not args.target_game:
        raise InputError(
            "Set source.target_game in config or pass --target-game; use --list-sheets only to inspect tabs."
        )
    encrypted_id = args.app_id_dpapi
    encrypted_secret = args.app_secret_dpapi
    if encrypted_id is not None or encrypted_secret is not None:
        if args.credential_provider != "windows-dpapi-current-user":
            raise InputError("Unsupported encrypted credential provider in config.")
        if not encrypted_id or not encrypted_secret:
            raise InputError("Both app_id_dpapi and app_secret_dpapi are required.")
        app_id = unprotect_config_value(encrypted_id)
        app_secret = unprotect_config_value(encrypted_secret)
    else:
        app_id = os.environ.get(args.app_id_env, "")
        app_secret = os.environ.get(args.app_secret_env, "")
        if not app_id or not app_secret:
            raise InputError(
                "Config has no DPAPI credentials; set "
                f"{args.app_id_env} and {args.app_secret_env} in the process environment."
            )
    client = FeishuClient(timeout=args.timeout, retries=args.retries)
    access_token = client.authenticate(app_id, app_secret, args.auth_mode)
    wiki_node: dict[str, Any] | None = None
    if source_kind == "wiki":
        wiki_node = client.resolve_wiki_node(source_token, access_token)
        obj_type = str(wiki_node.get("obj_type", "")).casefold()
        if obj_type not in {"sheet", "spreadsheet"}:
            raise InputError(
                f"Wiki node resolves to obj_type {obj_type or 'unknown'!r}, not a spreadsheet."
            )
        spreadsheet_token = str(wiki_node.get("obj_token", ""))
        if not spreadsheet_token:
            raise ApiError("Wiki node response did not contain obj_token.")
    else:
        spreadsheet_token = source_token
    sheets = client.list_sheets(spreadsheet_token, access_token)
    if args.list_sheets:
        return {
            "spreadsheet_token": spreadsheet_token,
            "wiki_node_token": source_token if source_kind == "wiki" else None,
            "sheets": [
                {
                    "sheet_id": item.get("sheet_id"),
                    "title": item.get("title"),
                    "index": item.get("index"),
                    "hidden": bool(item.get("hidden", False)),
                }
                for item in sheets
            ],
        }
    selected = resolve_sheet(sheets, args.sheet or url_sheet)
    sheet_id = str(selected.get("sheet_id", ""))
    if not sheet_id:
        raise ApiError("Selected sheet has no sheet_id.")
    grid = selected.get("grid_properties") or {}
    rows = min(max(int(grid.get("row_count") or args.max_rows), 1), args.max_rows)
    columns = min(max(int(grid.get("column_count") or args.max_columns), 1), args.max_columns)
    cell_range = args.cell_range or f"A1:{column_name(columns - 1)}{rows}"
    range_text = f"{sheet_id}!{cell_range}"
    values = client.read_range(spreadsheet_token, range_text, access_token)
    normalized = normalize_table(
        values,
        args.target_game,
        load_aliases(args.aliases),
        explicit_header_row=args.header_row,
        strict=args.strict,
    )
    return {
        "schema_version": 1,
        "source": {
            "spreadsheet_token": spreadsheet_token,
            "spreadsheet_reference": args.spreadsheet,
            "reference_type": source_kind,
            "wiki_node_token": source_token if source_kind == "wiki" else None,
            "wiki_space_id": wiki_node.get("space_id") if wiki_node else None,
            "wiki_title": wiki_node.get("title") if wiki_node else None,
            "sheet_id": sheet_id,
            "sheet_title": selected.get("title"),
            "range": range_text,
            "header_row": normalized.pop("header_row"),
            "data_row": normalized.pop("data_row"),
        },
        **normalized,
    }


def main() -> int:
    parser = build_parser()
    args = merge_options(parser.parse_args())
    try:
        result = run(args)
        write_output(result, args.output)
        return 0
    except InputError as error:
        print(json.dumps({"status": "input_error", "error": str(error)}, ensure_ascii=False), file=sys.stderr)
        return 2
    except ApiError as error:
        print(json.dumps({"status": "api_error", "error": str(error)}, ensure_ascii=False), file=sys.stderr)
        return 3


if __name__ == "__main__":
    raise SystemExit(main())
