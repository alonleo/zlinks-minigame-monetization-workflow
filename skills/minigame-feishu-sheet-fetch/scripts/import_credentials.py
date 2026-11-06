#!/usr/bin/env python3
"""Import Feishu credentials into a project config as Windows DPAPI ciphertext."""

from __future__ import annotations

import argparse
import json
import os
import re
import tempfile
from pathlib import Path
from typing import Any

from fetch_sheet import InputError, protect_config_value


def load_source(path: Path) -> tuple[str, str]:
    try:
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as error:
        raise InputError(f"Cannot read source credential JSON: {type(error).__name__}.") from error
    if not isinstance(payload, dict):
        raise InputError("Source credential JSON must be an object.")
    node: Any = payload.get("feishu", payload)
    if not isinstance(node, dict):
        raise InputError("Source credential JSON has no Feishu object.")
    app_id = node.get("app_id")
    app_secret = node.get("app_secret")
    if not isinstance(app_id, str) or not re.fullmatch(r"cli_[A-Za-z0-9]+", app_id):
        raise InputError("Source Feishu App ID has an invalid format.")
    if (
        not isinstance(app_secret, str)
        or not 16 <= len(app_secret) <= 128
        or any(ord(character) < 33 for character in app_secret)
    ):
        raise InputError("Source Feishu App Secret has an invalid format.")
    return app_id, app_secret


def update_target(source: Path, target: Path) -> None:
    app_id, app_secret = load_source(source)
    try:
        config = json.loads(target.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as error:
        raise InputError(f"Cannot read target config JSON: {type(error).__name__}.") from error
    if not isinstance(config, dict):
        raise InputError("Target config JSON must be an object.")
    config["credentials"] = {
        "provider": "windows-dpapi-current-user",
        "source": str(source),
        "app_id_dpapi": protect_config_value(app_id),
        "app_secret_dpapi": protect_config_value(app_secret),
    }
    auth = config.setdefault("auth", {})
    if not isinstance(auth, dict):
        raise InputError("Target auth config must be an object.")
    auth["mode"] = "legacy"
    rendered = json.dumps(config, ensure_ascii=False, indent=2) + "\n"
    target.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary = tempfile.mkstemp(dir=target.parent, suffix=".tmp")
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8") as output:
            output.write(rendered)
        os.replace(temporary, target)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", required=True, help="Existing JSON; secrets are never printed")
    parser.add_argument("--target", required=True, help="Project Feishu config JSON")
    args = parser.parse_args()
    try:
        source = Path(args.source).expanduser().resolve()
        target = Path(args.target).expanduser().resolve()
        update_target(source, target)
        print(
            json.dumps(
                {
                    "saved": True,
                    "storage": "Windows DPAPI current user",
                    "source": str(source),
                    "target": str(target),
                },
                ensure_ascii=False,
            )
        )
        return 0
    except Exception as error:
        print(
            json.dumps(
                {
                    "saved": False,
                    "error_type": type(error).__name__,
                    "hint": "Check JSON structure and run as the Windows user that will read the config.",
                },
                ensure_ascii=False,
            )
        )
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
