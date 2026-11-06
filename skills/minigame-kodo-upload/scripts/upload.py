"""Plan, check, upload and verify a resource directory in Qiniu Kodo."""
import argparse
from collections import Counter
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
import fnmatch
import hashlib
import json
import logging
import mimetypes
import os
from pathlib import Path
import re
import stat
import sys
import threading
from urllib.parse import quote, unquote, urlsplit
import uuid

from credentials import load, state_dir
from qiniu import Auth, BucketManager, etag
import qiniu.config
from qiniu.services.storage.uploaders import FormUploader, ResumeUploaderV2

# SDK exceptions may carry request headers. Only controlled errors reach reports.
logging.disable(logging.CRITICAL)
EXCLUDED_DIRS = {".git", ".svn", "node_modules", "library", "temp", "logs", "obj", "usersettings", ".codex", ".venv"}
EXCLUDED_FILES = {"project.private.config.json", ".ds_store", "thumbs.db"}
SECRET_EXTENSIONS = {".pem", ".key", ".p12", ".pfx", ".dpapi"}


class UploadError(Exception):
    pass


def normalize_prefix(value):
    value = value.replace("\\", "/").strip("/")
    if ":" in value or any(ord(c) < 32 for c in value):
        raise UploadError("Prefix must be an object path, not a URL or drive path.")
    if any(part in ("", ".", "..") for part in value.split("/")) and value:
        raise UploadError("Prefix contains an empty or traversal component.")
    return value + "/" if value else ""


def domain_origin(value):
    if not value:
        return ""
    parsed = urlsplit(value)
    if parsed.scheme != "https" or not parsed.hostname or parsed.username or parsed.password or parsed.query or parsed.fragment or parsed.path not in ("", "/"):
        raise UploadError("Domain must be an HTTPS origin, without path or credentials.")
    return value.rstrip("/")


def linked(path):
    return path.is_symlink() or bool(getattr(path.lstat(), "st_file_attributes", 0) & stat.FILE_ATTRIBUTE_REPARSE_POINT)


def excluded(path, patterns):
    return (any(p.lower() in EXCLUDED_DIRS for p in path.parts)
            or path.name.lower() in EXCLUDED_FILES
            or path.name.lower() == ".env" or path.name.lower().startswith(".env.")
            or path.suffix.lower() in SECRET_EXTENSIONS
            or any(fnmatch.fnmatchcase(path.as_posix(), pattern) for pattern in patterns))


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def collect(source, prefix, patterns=(), manifest=None, domain=""):
    source = Path(source).absolute()
    if not source.exists() or linked(source):
        raise UploadError("Source is missing or is a link/junction.")
    if source.is_dir() and (source / "Assets").is_dir() and (source / "ProjectSettings").is_dir():
        raise UploadError("Source is a Unity project root. Select its exported CDN resource directory.")
    root = source.resolve() if source.is_dir() else source.resolve().parent
    paths, skipped = [], []
    if source.is_file():
        paths = [source]
    else:
        for current, directories, files in os.walk(source, followlinks=False):
            for name in list(directories):
                path = Path(current) / name
                relative = path.relative_to(source)
                if linked(path) or excluded(relative, patterns):
                    directories.remove(name)
                    skipped.append(relative.as_posix() + "/")
            paths.extend(Path(current) / name for name in files)
    records = []
    for path in sorted(paths):
        relative = path.relative_to(source) if source.is_dir() else Path(path.name)
        if linked(path) or excluded(relative, patterns):
            skipped.append(relative.as_posix())
            continue
        if not path.resolve().is_relative_to(root):
            raise UploadError("A source file resolves outside the selected directory.")
        before = path.stat()
        item = {"path": str(path), "relative": relative.as_posix(), "key": prefix + relative.as_posix(),
                "bytes": before.st_size, "mtime_ns": before.st_mtime_ns, "etag": etag(str(path)), "sha256": sha256(path)}
        after = path.stat()
        if (before.st_size, before.st_mtime_ns) != (after.st_size, after.st_mtime_ns):
            raise UploadError("A source file changed while building the upload plan.")
        if len(item["key"].encode("utf-8")) > 750 or any(ord(c) < 32 for c in item["key"]):
            raise UploadError("Invalid or overly long object key.")
        if domain:
            item["url"] = domain + "/" + quote(item["key"], safe="/")
        records.append(item)
    if not records:
        raise UploadError("No eligible files found.")
    if manifest:
        data = json.loads(Path(manifest).read_text(encoding="utf-8-sig"))
        expected = {item["path"]: item for item in data["files"]}
        if len(expected) != len(data["files"]) or set(expected) != {item["relative"] for item in records}:
            raise UploadError("The resource directory and CDN manifest file list differ.")
        if "cdn_prefix" in data:
            parsed = urlsplit(data["cdn_prefix"])
            if normalize_prefix(unquote(parsed.path)) != prefix or (domain and domain_origin(parsed.scheme + "://" + parsed.netloc) != domain):
                raise UploadError("CDN manifest does not match the requested domain/prefix.")
        for item in records:
            target = expected[item["relative"]]
            if target.get("bytes") != item["bytes"] or target.get("sha256", "").lower() != item["sha256"]:
                raise UploadError("Resource bytes/hash do not match the CDN manifest.")
    return records, skipped


class Remote:
    def __init__(self, bucket):
        credentials = load()
        self.auth = Auth(credentials["access_key"], credentials["secret_key"])
        self.bucket = bucket
        qiniu.config.set_default(connection_timeout=30, connection_retries=1,
                                 default_rs_host="https://rs.qiniu.com", default_api_host="https://api.qiniuapi.com")
        self.manager = BucketManager(self.auth, preferred_scheme="https")

    def stat(self, key):
        try:
            result, info = self.manager.stat(self.bucket, key)
        except Exception:
            raise UploadError("Remote stat failed; authorization/network must be checked.") from None
        code = getattr(info, "status_code", None)
        if code == 612:
            return None
        if code != 200 or not isinstance(result, dict):
            raise UploadError("Remote stat failed (status %s); no assumption of absence is allowed." % code)
        return result

    def put(self, item, overwrite):
        policy = {"insertOnly": 0 if overwrite else 1, "fsizeLimit": item["bytes"]}
        token = self.auth.upload_token(self.bucket, item["key"], 3600, policy)
        # Qiniu form upload is limited to 4 MiB. Use resumable V2 above that
        # boundary; otherwise 4-8 MiB Unity bundles may return an uncertain
        # receipt and remain absent when reconciled with stat.
        uploader_type = ResumeUploaderV2 if item["bytes"] > 4 * 1024 * 1024 else FormUploader
        uploader = uploader_type(self.bucket, auth=self.auth, preferred_scheme="https")
        # Precompressed Unity .bin.br files remain opaque; no Content-Encoding is set.
        mime = "application/octet-stream" if item["relative"].endswith((".br", ".gz")) else (mimetypes.guess_type(item["relative"])[0] or "application/octet-stream")
        try:
            result, info = uploader.upload(item["key"], item["path"], up_token=token, mime_type=mime)
            return isinstance(result, dict) and result.get("key") == item["key"] and result.get("hash") == item["etag"]
        except Exception:
            return False  # Reconcile with remote stat before reporting an unknown outcome.


def matches(remote, item):
    return remote is not None and remote.get("hash") == item["etag"] and remote.get("fsize") == item["bytes"]


def classify(remote, item, overwrite):
    result = remote.stat(item["key"])
    if matches(result, item):
        item["status"] = "unchanged"
    elif result is None:
        item["status"] = "new"
    else:
        item["status"] = "replace" if overwrite else "conflict"
    return item


def transfer(remote, item, overwrite, stop):
    if item["status"] == "unchanged":
        return item
    if stop.is_set():
        item["status"] = "not_started"
        return item
    try:
        path = Path(item["path"])
        if path.stat().st_size != item["bytes"] or sha256(path) != item["sha256"]:
            raise UploadError("Local file changed after preflight.")
        receipt_ok = remote.put(item, overwrite)
        if not matches(remote.stat(item["key"]), item):
            raise UploadError("Remote verification failed; reconcile before any retry.")
        if sha256(path) != item["sha256"]:
            raise UploadError("Local file changed during upload; remote matches the earlier snapshot.")
        item["status"] = "uploaded" if receipt_ok else "verified_after_uncertain_receipt"
    except Exception as error:
        stop.set()
        item["status"] = "failed"
        item["error"] = str(error) if isinstance(error, UploadError) else "Upload failed; raw SDK error suppressed."
    return item


def write_report(report):
    directory = state_dir() / "reports"
    directory.mkdir(parents=True, exist_ok=True)
    path = directory / (datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ") + "-" + uuid.uuid4().hex[:8] + ".json")
    path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    return path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source")
    parser.add_argument("--bucket")
    parser.add_argument("--prefix", help="Object directory prefix; use --prefix= for bucket root")
    parser.add_argument("--domain", default="")
    parser.add_argument("--manifest", help="Optional cdn-manifest.json with exact relative paths, bytes, sha256")
    parser.add_argument("--exclude", action="append", default=[])
    parser.add_argument("--workers", type=int, choices=range(1, 9), default=4)
    parser.add_argument("--overwrite", action="store_true", help="Allow replacement of different objects only when authorized")
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--check", action="store_true", help="Compare remote metadata without uploading")
    mode.add_argument("--upload", action="store_true", help="Perform the authorized upload")
    mode.add_argument("--list-buckets", action="store_true", help="Read-only credential check and bucket listing")
    args = parser.parse_args()
    report = {"ok": False, "mode": "upload" if args.upload else "check" if args.check else "plan", "files": []}
    try:
        if args.list_buckets:
            remote = Remote(None)
            buckets, info = remote.manager.buckets()
            if getattr(info, "status_code", None) != 200 or not isinstance(buckets, list):
                raise UploadError("Bucket listing failed; check credentials or list permission.")
            print(json.dumps({"ok": True, "buckets": buckets}, ensure_ascii=False))
            return 0
        if not args.source or not args.bucket or args.prefix is None:
            raise UploadError("--source, --bucket and --prefix are required.")
        if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_-]{1,62}", args.bucket):
            raise UploadError("Invalid bucket name.")
        prefix, domain = normalize_prefix(args.prefix), domain_origin(args.domain)
        records, skipped = collect(args.source, prefix, args.exclude, args.manifest, domain)
        report.update(source=str(Path(args.source).absolute()), bucket=args.bucket, prefix=prefix, domain=domain,
                      overwrite=args.overwrite, total_bytes=sum(f["bytes"] for f in records), files=records, excluded=skipped)
        if args.check or args.upload:
            remote = Remote(args.bucket)
            # Complete every read-only preflight before starting the first write.
            for item in records:
                classify(remote, item, args.overwrite)
            if any(f["status"] == "conflict" for f in records):
                raise UploadError("Different objects already exist; no files uploaded. Review conflicts before --overwrite.")
            if args.upload:
                stop = threading.Event()
                with ThreadPoolExecutor(max_workers=args.workers) as pool:
                    list(pool.map(lambda item: transfer(remote, item, args.overwrite, stop), records))
                if any(f["status"] in ("failed", "not_started") for f in records):
                    raise UploadError("Upload incomplete; see per-file report. No automatic whole-job retry.")
        else:
            for item in records:
                item["status"] = "planned"
        report["ok"] = True
    except Exception as error:
        report["error"] = str(error) if isinstance(error, UploadError) else "Operation failed (%s); raw details suppressed to protect credentials." % type(error).__name__
    report["counts"] = dict(Counter(f.get("status", "not_checked") for f in report["files"]))
    path = write_report(report)
    summary = {k: v for k, v in report.items() if k not in ("files", "excluded")}
    summary.update(report=str(path), file_count=len(report["files"]), excluded_count=len(report.get("excluded", [])),
                   sample_keys=[f["key"] for f in report["files"][:8]])
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    return 0 if report["ok"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
