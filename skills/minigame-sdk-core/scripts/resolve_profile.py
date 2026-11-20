#!/usr/bin/env python3
"""Resolve one immutable mini-game variant/platform profile from a JSON catalog."""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import re
import sys
from pathlib import Path


PLATFORMS = {"wechat", "douyin"}
SAFE_ID = re.compile(r"^[a-z0-9][a-z0-9._-]*$")
SENSITIVE = re.compile(r"(^|_)(secret|token|password|private_key|access_key|credential)s?($|_)", re.I)


class ProfileError(ValueError):
    pass


def merge(base: dict, override: dict) -> dict:
    result = copy.deepcopy(base)
    for key, value in override.items():
        if isinstance(value, dict) and isinstance(result.get(key), dict):
            result[key] = merge(result[key], value)
        else:
            result[key] = copy.deepcopy(value)
    return result


def reject_sensitive(value: object, path: str = "catalog") -> None:
    if isinstance(value, dict):
        for key, child in value.items():
            if SENSITIVE.search(str(key)):
                raise ProfileError(f"sensitive field is forbidden in profile catalog: {path}.{key}")
            reject_sensitive(child, f"{path}.{key}")
    elif isinstance(value, list):
        for index, child in enumerate(value):
            reject_sensitive(child, f"{path}[{index}]")


def require_text(value: object, field: str) -> str:
    if not isinstance(value, str) or not value.strip():
        raise ProfileError(f"{field} must be a non-empty string")
    return value.strip()


def canonical_bytes(value: object) -> bytes:
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")


def resolve(catalog: object, variant_id: str, platform: str) -> dict:
    if not isinstance(catalog, dict):
        raise ProfileError("catalog must be a JSON object")
    reject_sensitive(catalog)
    if platform not in PLATFORMS:
        raise ProfileError(f"platform must be one of {sorted(PLATFORMS)}")
    if not SAFE_ID.fullmatch(variant_id):
        raise ProfileError("variant_id is not Git/path safe")
    defaults = catalog.get("defaults", {})
    platform_defaults = catalog.get("platforms", {}).get(platform, {}) if isinstance(catalog.get("platforms", {}), dict) else {}
    variants = catalog.get("variants")
    if not isinstance(variants, list):
        raise ProfileError("catalog.variants must be a list")
    matches = [item for item in variants if isinstance(item, dict) and item.get("variant_id") == variant_id]
    if len(matches) != 1:
        raise ProfileError(f"variant_id must match exactly one catalog entry: {variant_id}")
    variant = copy.deepcopy(matches[0])
    variant_platforms = variant.pop("platforms", {})
    variant.pop("variant_id", None)
    if not all(isinstance(item, dict) for item in (defaults, platform_defaults, variant, variant_platforms)):
        raise ProfileError("defaults, platforms, variant and variant.platforms must be objects")
    specific = variant_platforms.get(platform, {})
    if not isinstance(specific, dict):
        raise ProfileError(f"variant.platforms.{platform} must be an object")
    resolved = merge(merge(merge(defaults, variant), platform_defaults), specific)
    resolved["schema_version"] = int(catalog.get("schema_version", 1))
    resolved["variant_id"] = variant_id
    resolved["platform"] = platform
    resolved["display_name"] = require_text(resolved.get("display_name"), "display_name")
    resolved["app_id"] = require_text(resolved.get("app_id"), "app_id")
    digest = hashlib.sha256(canonical_bytes(resolved)).hexdigest()
    resolved["profile_sha256"] = digest
    return resolved


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--catalog", type=Path, required=True)
    parser.add_argument("--variant", required=True)
    parser.add_argument("--platform", choices=sorted(PLATFORMS), required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    try:
        catalog = json.loads(args.catalog.read_text(encoding="utf-8"))
        result = resolve(catalog, args.variant, args.platform)
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(result, ensure_ascii=False, sort_keys=True, indent=2) + "\n", encoding="utf-8")
    except (OSError, json.JSONDecodeError, ProfileError, ValueError) as error:
        print(json.dumps({"result": "failed", "error": str(error)}, ensure_ascii=False), file=sys.stderr)
        return 2
    print(json.dumps({
        "result": "passed",
        "variant_id": result["variant_id"],
        "platform": result["platform"],
        "profile_sha256": result["profile_sha256"],
        "output": str(args.output.resolve()),
    }, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
