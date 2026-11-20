#!/usr/bin/env python3
"""Safely plan/apply the bundled core component to a Unity project."""
from pathlib import Path
import argparse, hashlib, json, re, shutil

ASSETS = Path(__file__).resolve().parents[1] / "assets"
DEFAULT_FONT = "AlibabaPuHuiTi-3-105-Heavy.otf"
DEFAULT_FONT_BYTES = 2_151_384

def trees():
    return [
        (ASSETS / "core/platformTools/src", Path("Assets/Resources/platformTools/src")),
        (ASSETS / "core/Plugins", Path("Assets/Plugins")),
        (ASSETS / "core/platformTools/fonts", Path("Assets/Resources/platformTools/fonts")),
    ]

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def collect(project):
    pairs = []
    for source, target in trees():
        if not source.is_dir():
            raise ValueError(f"missing bundled assets: {source}")
        for src in sorted(source.rglob("*")):
            if src.is_file():
                pairs.append(("core", src, project / target / src.relative_to(source)))
    font = ASSETS / "core/platformTools/fonts" / DEFAULT_FONT
    if not font.is_file() or font.stat().st_size != DEFAULT_FONT_BYTES:
        raise ValueError(f"invalid default font asset: {font}")
    unique = {}
    for item in pairs:
        key = item[2]
        if key in unique and unique[key][1].read_bytes() != item[1].read_bytes():
            raise ValueError(f"component sources disagree for {key}")
        unique[key] = item
    return list(unique.values())

def plan(project):
    pairs = collect(project)
    destinations = {dst for _, _, dst in pairs}
    classes = {}
    for src in (project / "Assets").rglob("*.cs"):
        if src in destinations or src.is_symlink():
            continue
        text = src.read_text(encoding="utf-8-sig", errors="replace")
        for name in re.findall(r"\b(?:class|struct|enum|interface)\s+(\w+)", text):
            classes.setdefault(name, []).append(src)
    guids = {}
    for meta in (project / "Assets").rglob("*.meta"):
        if meta in destinations or meta.is_symlink():
            continue
        match = re.search(r"^guid: ([a-f0-9]{32})", meta.read_text(encoding="utf-8-sig", errors="replace"), re.M)
        if match:
            guids.setdefault(match.group(1), []).append(meta)
    report = []
    for component, source, destination in pairs:
        if not destination.resolve().is_relative_to(project):
            raise ValueError(f"target escapes project: {destination}")
        state = "same" if destination.is_file() and source.read_bytes() == destination.read_bytes() else "conflict" if destination.exists() else "add"
        collisions = []
        if source.suffix == ".cs":
            text = source.read_text(encoding="utf-8-sig", errors="replace")
            for name in re.findall(r"\b(?:class|struct|enum|interface)\s+(\w+)", text):
                collisions.extend(str(p.relative_to(project)) for p in classes.get(name, []))
        if source.suffix == ".meta":
            match = re.search(r"^guid: ([a-f0-9]{32})", source.read_text(encoding="utf-8-sig", errors="replace"), re.M)
            if match:
                collisions.extend(str(p.relative_to(project)) for p in guids.get(match.group(1), []))
        if collisions:
            state = "conflict"
        report.append({"component": component, "asset": str(source.relative_to(ASSETS)), "target": str(destination.relative_to(project)), "bytes": source.stat().st_size, "sha256": sha(source), "state": state, "collisions": sorted(set(collisions))})
    return pairs, report

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("project", type=Path)
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    project = args.project.resolve()
    if not all((project / p).exists() for p in ("Assets", "Packages", "ProjectSettings/ProjectVersion.txt")):
        parser.error("not a Unity project")
    pairs, report = plan(project)
    blocked = any(row["state"] == "conflict" for row in report)
    result = {"mode": "apply" if args.apply else "dry-run", "components": ["core"], "blocked": blocked, "files": report}
    print(json.dumps(result, ensure_ascii=False, indent=2))
    if blocked:
        return 2
    if args.apply:
        for (_, source, destination), row in zip(pairs, report):
            if row["state"] == "add":
                destination.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(source, destination)
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
