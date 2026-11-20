#!/usr/bin/env python3
"""Plan/apply the bundled WeChat build hook and dnSDK template."""
from pathlib import Path
import argparse
import hashlib
import json
import shutil


ASSETS = Path(__file__).resolve().parents[1] / "assets"


def mappings():
    return [
        (ASSETS / "wechat/Editor", Path("Assets/Editor/MiniGameKit")),
        (
            ASSETS / "wechat/CustomizeTemplate",
            Path("Assets/MiniGameKit/SDK/WeChatTemplate"),
        ),
    ]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("project", type=Path)
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    project = args.project.resolve()
    if not all(
        (project / path).exists()
        for path in ("Assets", "Packages", "ProjectSettings/ProjectVersion.txt")
    ):
        parser.error("not a Unity project")
    payloads = []
    rows = []
    for asset_root, relative_target in mappings():
        if not asset_root.is_dir():
            parser.error(f"bundled template is missing: {asset_root}")
        for source in sorted(asset_root.rglob("*")):
            if not source.is_file():
                continue
            target = project / relative_target / source.relative_to(asset_root)
            if not target.resolve().is_relative_to(project):
                parser.error("target escapes project")
            data = source.read_bytes()
            state = (
                "same"
                if target.is_file() and target.read_bytes() == data
                else "conflict"
                if target.exists()
                else "add"
            )
            rows.append(
                {
                    "asset": str(source.relative_to(ASSETS)),
                    "target": str(target.relative_to(project)),
                    "bytes": len(data),
                    "sha256": hashlib.sha256(data).hexdigest(),
                    "state": state,
                }
            )
            payloads.append((source, target, state))
    blocked = any(row["state"] == "conflict" for row in rows)
    print(
        json.dumps(
            {
                "mode": "apply" if args.apply else "dry-run",
                "blocked": blocked,
                "files": rows,
            },
            ensure_ascii=False,
            indent=2,
        )
    )
    if blocked:
        return 2
    if args.apply:
        for source, target, state in payloads:
            if state == "add":
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(source, target)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
