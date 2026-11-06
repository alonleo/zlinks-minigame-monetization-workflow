"""Windows DPAPI credential storage; never accepts secrets as CLI arguments."""
import argparse
import ctypes
from ctypes import wintypes
import getpass
import json
import os
from pathlib import Path
import re
import sys
import tempfile


def state_dir():
    return Path(os.environ.get("LOCALAPPDATA", Path.home() / ".local" / "share")) / "Codex" / "minigame-kodo-upload"


def legacy_credential_path():
    return Path(os.environ.get("LOCALAPPDATA", Path.home() / ".local" / "share")) / "Codex" / "wechat-kodo-upload" / "credentials.dpapi"


def credential_path():
    return state_dir() / "credentials.dpapi"


def validate(data):
    result = {}
    for name in ("access_key", "secret_key"):
        value = data.get(name, "")
        if not isinstance(value, str) or not re.fullmatch(r"[A-Za-z0-9_-]{16,128}", value):
            raise ValueError("Invalid credential format; supply the key value without labels or trailing backslashes.")
        result[name] = value
    return result


def dpapi(data, decrypt=False):
    if os.name != "nt":
        raise RuntimeError("DPAPI requires Windows; use process environment credentials on other systems.")

    class Blob(ctypes.Structure):
        _fields_ = [("size", wintypes.DWORD), ("data", ctypes.POINTER(ctypes.c_ubyte))]

    buffer = ctypes.create_string_buffer(data)
    source = Blob(len(data), ctypes.cast(buffer, ctypes.POINTER(ctypes.c_ubyte)))
    target = Blob()
    crypt = ctypes.WinDLL("crypt32", use_last_error=True)
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel.LocalFree.argtypes = [ctypes.c_void_p]
    kernel.LocalFree.restype = ctypes.c_void_p
    if decrypt:
        call = crypt.CryptUnprotectData
        second_type = ctypes.POINTER(wintypes.LPWSTR)
    else:
        call = crypt.CryptProtectData
        second_type = wintypes.LPCWSTR
    call.argtypes = [ctypes.POINTER(Blob), second_type, ctypes.POINTER(Blob), ctypes.c_void_p,
                     ctypes.c_void_p, wintypes.DWORD, ctypes.POINTER(Blob)]
    call.restype = wintypes.BOOL
    # CRYPTPROTECT_UI_FORBIDDEN; user scope, never machine scope.
    if not call(ctypes.byref(source), None, None, None, None, 1, ctypes.byref(target)):
        raise RuntimeError("Windows credential encryption/decryption failed.")
    try:
        return ctypes.string_at(target.data, target.size)
    finally:
        kernel.LocalFree(target.data)


def save(data):
    payload = json.dumps(validate(data)).encode("utf-8")
    encrypted = dpapi(payload)
    destination = credential_path()
    destination.parent.mkdir(parents=True, exist_ok=True)
    fd, temporary = tempfile.mkstemp(dir=destination.parent, suffix=".dpapi.tmp")
    try:
        with os.fdopen(fd, "wb") as output:
            output.write(encrypted)
        os.replace(temporary, destination)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def load():
    access = os.environ.get("QINIU_ACCESS_KEY")
    secret = os.environ.get("QINIU_SECRET_KEY")
    if access is not None or secret is not None:
        return validate({"access_key": access, "secret_key": secret})
    destination = credential_path()
    if not destination.is_file() and legacy_credential_path().is_file():
        destination = legacy_credential_path()
    if not destination.is_file():
        raise RuntimeError("No saved credential. Run credentials.py store or provide process environment credentials.")
    return validate(json.loads(dpapi(destination.read_bytes(), decrypt=True)))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=["store", "status"])
    parser.add_argument("--stdin", action="store_true", help="Read a credential JSON object from stdin without echoing it")
    args = parser.parse_args()
    try:
        if args.action == "store":
            data = json.load(sys.stdin) if args.stdin else {
                "access_key": getpass.getpass("Qiniu Access Key: "),
                "secret_key": getpass.getpass("Qiniu Secret Key: "),
            }
            save(data)
            print(json.dumps({"saved": True, "storage": "Windows DPAPI current user", "path": str(credential_path())}))
        else:
            load()
            print(json.dumps({"available": True, "note": "Local decryption only; remote authorization has not been checked."}))
        return 0
    except Exception as error:
        # Do not print raw parser/SDK errors which could contain secret input.
        print(json.dumps({"ok": False, "error_type": type(error).__name__, "hint": "Check the credential input and Windows user context."}))
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
