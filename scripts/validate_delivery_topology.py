#!/usr/bin/env python3
"""Validate and normalize workflow delivery-topology selection."""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path


SAFE_ID = re.compile(r"^[a-z0-9][a-z0-9._-]*$")
PLATFORMS = {"wechat", "douyin"}
MODES = {"isolated_branches", "unified_matrix"}


class ValidationError(ValueError):
    pass


def require_text(value: object, field: str) -> str:
    if not isinstance(value, str) or not value.strip():
        raise ValidationError(f"{field} must be a non-empty string")
    return value.strip()


def require_platform(value: object, field: str) -> str:
    platform = require_text(value, field)
    if platform not in PLATFORMS:
        raise ValidationError(f"{field} must be one of {sorted(PLATFORMS)}")
    return platform


def normalize_isolated(data: dict) -> dict:
    if data.get("variant_catalog") or data.get("selected_targets"):
        raise ValidationError("isolated_branches cannot include unified_matrix targets")
    targets = data.get("branch_targets")
    if not isinstance(targets, list) or not targets:
        raise ValidationError("branch_targets must be a non-empty list")
    normalized = []
    seen = set()
    for index, target in enumerate(targets):
        if not isinstance(target, dict):
            raise ValidationError(f"branch_targets[{index}] must be an object")
        platform = require_platform(target.get("platform"), f"branch_targets[{index}].platform")
        name = require_text(target.get("platform_game_name"), f"branch_targets[{index}].platform_game_name")
        branch = require_text(target.get("branch"), f"branch_targets[{index}].branch")
        expected = "miniGame-wx-" if platform == "wechat" else "miniGame-dy-"
        if not branch.startswith(expected) or len(branch) == len(expected):
            raise ValidationError(f"branch_targets[{index}].branch must start with {expected} and have a suffix")
        key = (platform, name)
        if key in seen:
            raise ValidationError(f"duplicate isolated target: {platform}/{name}")
        seen.add(key)
        normalized.append({"platform": platform, "platform_game_name": name, "branch": branch})
    return {"mode": "isolated_branches", "targets": normalized}


def normalize_unified(data: dict, topology: dict) -> dict:
    if data.get("branch_targets"):
        raise ValidationError("unified_matrix cannot include branch_targets")
    branch = require_text(topology.get("unified_branch"), "delivery_topology.unified_branch")
    if branch.startswith("miniGame-wx-") or branch.startswith("miniGame-dy-"):
        raise ValidationError("unified_branch cannot be a platform-named branch")
    catalog = data.get("variant_catalog")
    if not isinstance(catalog, list) or not catalog:
        raise ValidationError("variant_catalog must be a non-empty list")
    variants = {}
    for index, variant in enumerate(catalog):
        if not isinstance(variant, dict):
            raise ValidationError(f"variant_catalog[{index}] must be an object")
        variant_id = require_text(variant.get("variant_id"), f"variant_catalog[{index}].variant_id")
        if not SAFE_ID.fullmatch(variant_id):
            raise ValidationError(f"invalid variant_id: {variant_id}")
        if variant_id in variants:
            raise ValidationError(f"duplicate variant_id: {variant_id}")
        names = variant.get("names")
        if not isinstance(names, dict):
            raise ValidationError(f"variant_catalog[{index}].names must be an object")
        variants[variant_id] = {key: require_text(value, f"{variant_id}.names.{key}") for key, value in names.items() if key in PLATFORMS}

    selected = data.get("selected_targets")
    if not isinstance(selected, list) or not selected:
        raise ValidationError("selected_targets must be a non-empty list")
    targets = []
    seen = set()
    for index, target in enumerate(selected):
        if not isinstance(target, dict):
            raise ValidationError(f"selected_targets[{index}] must be an object")
        variant_id = require_text(target.get("variant_id"), f"selected_targets[{index}].variant_id")
        platform = require_platform(target.get("platform"), f"selected_targets[{index}].platform")
        if variant_id not in variants:
            raise ValidationError(f"selected target references unknown variant_id: {variant_id}")
        if platform not in variants[variant_id]:
            raise ValidationError(f"variant {variant_id} has no official name for {platform}")
        key = (variant_id, platform)
        if key in seen:
            raise ValidationError(f"duplicate unified target: {variant_id}/{platform}")
        seen.add(key)
        targets.append({
            "variant_id": variant_id,
            "platform": platform,
            "platform_game_name": variants[variant_id][platform],
        })
    return {"mode": "unified_matrix", "unified_branch": branch, "targets": targets}


def normalize(data: object) -> dict:
    if not isinstance(data, dict):
        raise ValidationError("workflow input must be a JSON object")
    topology = data.get("delivery_topology")
    if not isinstance(topology, dict):
        raise ValidationError("delivery_topology must be an object")
    mode = require_text(topology.get("mode"), "delivery_topology.mode")
    if mode not in MODES:
        raise ValidationError(f"delivery_topology.mode must be one of {sorted(MODES)}")
    return normalize_isolated(data) if mode == "isolated_branches" else normalize_unified(data, topology)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("input", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    try:
        result = normalize(json.loads(args.input.read_text(encoding="utf-8")))
    except (OSError, json.JSONDecodeError, ValidationError) as error:
        print(json.dumps({"result": "failed", "error": str(error)}, ensure_ascii=False), file=sys.stderr)
        return 2
    text = json.dumps({"result": "passed", **result}, ensure_ascii=False, indent=2) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(text, encoding="utf-8")
    print(text, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
