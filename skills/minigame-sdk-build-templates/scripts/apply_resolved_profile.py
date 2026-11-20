#!/usr/bin/env python3
"""Apply non-sensitive resolved profile identity to a platform project config."""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path


class ApplyError(ValueError):
    pass


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def profile_sha(profile: dict) -> str:
    unsigned = dict(profile)
    unsigned.pop("profile_sha256", None)
    data = json.dumps(unsigned, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return sha(data)


def apply_profile(profile: object, config: object, platform: str) -> dict:
    if not isinstance(profile, dict) or not isinstance(config, dict):
        raise ApplyError("profile and project config must be JSON objects")
    if profile.get("platform") != platform:
        raise ApplyError("resolved profile platform does not match requested platform")
    app_id = profile.get("app_id")
    display_name = profile.get("display_name")
    profile_hash = profile.get("profile_sha256")
    if not all(isinstance(value, str) and value.strip() for value in (app_id, display_name, profile_hash)):
        raise ApplyError("resolved profile identity is incomplete")
    if profile_sha(profile) != profile_hash:
        raise ApplyError("resolved profile SHA-256 does not match its content")
    result = dict(config)
    result["appid"] = app_id.strip()
    result["projectname"] = display_name.strip()
    return result


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--profile", type=Path, required=True)
    parser.add_argument("--platform", choices=["wechat", "douyin"], required=True)
    parser.add_argument("--project-config", type=Path, required=True)
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    try:
        before = args.project_config.read_bytes()
        profile = json.loads(args.profile.read_text(encoding="utf-8"))
        config = json.loads(before.decode("utf-8-sig"))
        updated = apply_profile(profile, config, args.platform)
        output = (json.dumps(updated, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
        if args.apply:
            args.project_config.write_bytes(output)
        result = {
            "result": "passed",
            "mode": "apply" if args.apply else "dry-run",
            "platform": args.platform,
            "variant_id": profile.get("variant_id"),
            "profile_sha256": profile.get("profile_sha256"),
            "config_before_sha256": sha(before),
            "config_after_sha256": sha(output),
            "project_config": str(args.project_config.resolve()),
        }
    except (OSError, UnicodeError, json.JSONDecodeError, ApplyError) as error:
        print(json.dumps({"result": "failed", "error": str(error)}, ensure_ascii=False), file=sys.stderr)
        return 2
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
