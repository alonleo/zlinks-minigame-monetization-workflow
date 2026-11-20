#!/usr/bin/env python3
"""Plan/apply the bundled Douyin CustomizeTemplate."""
from pathlib import Path
import argparse, hashlib, json

ASSETS = Path(__file__).resolve().parents[1] / "assets"

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("project", type=Path)
    parser.add_argument("--destination", type=Path, required=True)
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    project, destination = args.project.resolve(), args.destination.resolve()
    if not all((project / p).exists() for p in ("Assets", "Packages", "ProjectSettings/ProjectVersion.txt")):
        parser.error("not a Unity project")
    if not destination.is_relative_to(project):
        parser.error("destination must stay inside the Unity project")
    asset_root = ASSETS / "douyin/CustomizeTemplate"
    if not asset_root.is_dir():
        parser.error("bundled template is missing")
    rows, payloads = [], []
    for source in sorted(asset_root.rglob("*")):
        if not source.is_file():
            continue
        target = destination / source.relative_to(asset_root)
        data = source.read_bytes()
        state = "same" if target.is_file() and target.read_bytes() == data else "conflict" if target.exists() else "add"
        rows.append({"asset": str(source.relative_to(ASSETS)), "target": str(target.relative_to(project)), "bytes": len(data), "sha256": hashlib.sha256(data).hexdigest(), "state": state})
        payloads.append((target, data, state))
    blocked = any(row["state"] == "conflict" for row in rows)
    print(json.dumps({"mode": "apply" if args.apply else "dry-run", "blocked": blocked, "wasm_function_split": "disabled", "active_split_artifacts": [], "files": rows}, ensure_ascii=False, indent=2))
    if blocked:
        return 2
    if args.apply:
        for target, data, state in payloads:
            if state == "add":
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(data)
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
