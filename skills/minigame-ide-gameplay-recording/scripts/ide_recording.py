#!/usr/bin/env python3
"""Record WeChat or Douyin IDE gameplay on Windows with FFmpeg."""

from __future__ import annotations

import argparse
import ctypes
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import time
import uuid
from ctypes import wintypes
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


PLATFORM_PATTERNS = {
    "wechat": r"微信开发者工具|WeChat\s*(DevTools|Developer Tools)",
    "douyin": (
        r"抖音.*开发者工具|字节跳动.*开发者工具|"
        r"Douyin.*(IDE|DevTools|Developer Tools)|"
        r"ByteDance.*(IDE|DevTools|Developer Tools)"
    ),
}
FINAL_STATUSES = {"completed", "failed"}


def now_iso() -> str:
    return datetime.now().astimezone().isoformat()


def print_json(value: Any) -> None:
    print(json.dumps(value, ensure_ascii=False, indent=2))


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(f"{path.name}.{uuid.uuid4().hex}.tmp")
    temporary.write_text(
        json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    os.replace(temporary, path)


def read_json(path: Path) -> dict[str, Any]:
    if not path.is_file():
        raise RuntimeError(f"Session file was not found: {path}")
    return json.loads(path.read_text(encoding="utf-8-sig"))


def update_session(path: Path, **fields: Any) -> dict[str, Any]:
    session = read_json(path)
    session.update(fields)
    write_json(path, session)
    return session


def resolve_executable(explicit_path: str | None, command_name: str) -> str:
    if explicit_path:
        resolved = Path(explicit_path).expanduser().resolve()
        if not resolved.is_file():
            raise RuntimeError(f"{command_name} executable was not found: {resolved}")
        return str(resolved)
    resolved = shutil.which(command_name)
    if not resolved:
        option = "--ffmpeg-path" if command_name == "ffmpeg" else "--ffprobe-path"
        raise RuntimeError(
            f"{command_name} is not available on PATH. Pass {option} explicitly."
        )
    return str(Path(resolved).resolve())


def require_windows() -> None:
    if os.name != "nt":
        raise RuntimeError("IDE window recording is supported only on Windows.")


def process_alive(pid: int | None) -> bool:
    if not pid or os.name != "nt":
        return False
    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    process = kernel32.OpenProcess(0x1000, False, int(pid))
    if not process:
        return False
    try:
        exit_code = wintypes.DWORD()
        if not kernel32.GetExitCodeProcess(process, ctypes.byref(exit_code)):
            return False
        return exit_code.value == 259
    finally:
        kernel32.CloseHandle(process)


def process_name(pid: int) -> str | None:
    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    process = kernel32.OpenProcess(0x1000, False, pid)
    if not process:
        return None
    try:
        size = wintypes.DWORD(32768)
        buffer = ctypes.create_unicode_buffer(size.value)
        if kernel32.QueryFullProcessImageNameW(
            process, 0, buffer, ctypes.byref(size)
        ):
            return Path(buffer.value).stem
        return None
    finally:
        kernel32.CloseHandle(process)


def get_ide_windows(platform: str, title_pattern: str | None) -> list[dict[str, Any]]:
    require_windows()
    pattern_text = title_pattern or PLATFORM_PATTERNS[platform]
    try:
        pattern = re.compile(pattern_text, re.IGNORECASE)
    except re.error as exc:
        raise RuntimeError(
            f"Window title pattern is not a valid regular expression: {pattern_text}"
        ) from exc

    user32 = ctypes.WinDLL("user32", use_last_error=True)
    windows: list[dict[str, Any]] = []
    enum_proc_type = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)

    @enum_proc_type
    def enum_window(hwnd: int, _lparam: int) -> bool:
        length = user32.GetWindowTextLengthW(hwnd)
        if length <= 0:
            return True
        buffer = ctypes.create_unicode_buffer(length + 1)
        user32.GetWindowTextW(hwnd, buffer, length + 1)
        title = buffer.value
        if not title or not pattern.search(title):
            return True
        pid = wintypes.DWORD()
        user32.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
        windows.append(
            {
                "Id": int(pid.value),
                "ProcessName": process_name(int(pid.value)),
                "MainWindowTitle": title,
            }
        )
        return True

    if not user32.EnumWindows(enum_window, 0):
        error = ctypes.get_last_error()
        if error:
            raise ctypes.WinError(error)
    return windows


def absolute_session_path(
    requested_path: str | None, output_directory: str | None
) -> Path:
    if requested_path:
        return Path(requested_path).expanduser().resolve()
    if not output_directory:
        raise RuntimeError(
            "Pass --output-directory for Start or --session-file for Stop/Status."
        )
    return Path(output_directory).expanduser().resolve() / "recording-session.json"


def run_captured(executable: str, arguments: list[str]) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [executable, *arguments],
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
    )


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def doctor(args: argparse.Namespace) -> None:
    if not args.platform:
        raise RuntimeError("Doctor requires --platform wechat or douyin.")
    errors: list[str] = []
    ffmpeg = None
    ffprobe = None
    windows: list[dict[str, Any]] = []
    try:
        ffmpeg = resolve_executable(args.ffmpeg_path, "ffmpeg")
    except RuntimeError as exc:
        errors.append(str(exc))
    try:
        ffprobe = resolve_executable(args.ffprobe_path, "ffprobe")
    except RuntimeError as exc:
        errors.append(str(exc))
    try:
        windows = get_ide_windows(args.platform, args.window_title_pattern)
    except (RuntimeError, OSError) as exc:
        errors.append(str(exc))

    guidance = None
    if not windows:
        guidance = "Open the target IDE and project, then run Doctor again."
    elif len(windows) > 1:
        guidance = (
            "Pass --window-title-pattern with a regular expression that matches "
            "exactly one IDE window."
        )
    print_json(
        {
            "action": "doctor",
            "platform": args.platform,
            "ready": not errors and len(windows) == 1,
            "ffmpeg": ffmpeg,
            "ffprobe": ffprobe,
            "matching_windows": windows,
            "errors": errors,
            "guidance": guidance,
        }
    )


def start_recording(args: argparse.Namespace) -> None:
    require_windows()
    if not args.platform:
        raise RuntimeError("Start requires --platform wechat or douyin.")
    if not args.output_directory:
        raise RuntimeError("Start requires --output-directory.")
    if args.capture_mode == "region" and (
        args.capture_width <= 0 or args.capture_height <= 0
    ):
        raise RuntimeError(
            "Region capture requires positive --capture-width and --capture-height."
        )

    ffmpeg = resolve_executable(args.ffmpeg_path, "ffmpeg")
    ffprobe = resolve_executable(args.ffprobe_path, "ffprobe")
    output = Path(args.output_directory).expanduser().resolve()
    output.mkdir(parents=True, exist_ok=True)
    session_path = absolute_session_path(args.session_file, str(output))

    if session_path.is_file():
        previous = read_json(session_path)
        if (
            previous.get("status") in {"starting", "recording", "stopping"}
            and process_alive(previous.get("worker_pid"))
        ):
            raise RuntimeError(
                f"An active recording already uses this session file: {session_path}"
            )

    window = None
    if args.capture_mode == "window":
        windows = get_ide_windows(args.platform, args.window_title_pattern)
        if not windows:
            raise RuntimeError(
                f"No {args.platform} IDE window matched. Open the IDE or pass "
                "--window-title-pattern."
            )
        if len(windows) > 1:
            titles = "; ".join(item["MainWindowTitle"] for item in windows)
            raise RuntimeError(
                "Multiple IDE windows matched. Pass a unique --window-title-pattern. "
                f"Matches: {titles}"
            )
        window = windows[0]

    stamp = datetime.now().strftime("%Y%m%d-%H%M%S")
    base_name = f"{args.platform}-{stamp}"
    stop_signal_path = output / f"{base_name}.stop"
    stop_signal_path.unlink(missing_ok=True)
    session: dict[str, Any] = {
        "schema_version": 1,
        "session_id": str(uuid.uuid4()),
        "platform": args.platform,
        "status": "starting",
        "started_at": now_iso(),
        "completed_at": None,
        "capture_mode": args.capture_mode,
        "frame_rate": args.frame_rate,
        "max_duration_seconds": args.max_duration_seconds,
        "window_process_id": window["Id"] if window else None,
        "window_process_name": window["ProcessName"] if window else None,
        "window_title": window["MainWindowTitle"] if window else None,
        "region": (
            {
                "x": args.capture_x,
                "y": args.capture_y,
                "width": args.capture_width,
                "height": args.capture_height,
            }
            if args.capture_mode == "region"
            else None
        ),
        "audio_device_name": args.audio_device_name,
        "output_directory": str(output),
        "mkv_path": str(output / f"{base_name}.mkv"),
        "mp4_path": str(output / f"{base_name}.mp4"),
        "log_path": str(output / f"{base_name}.ffmpeg.log"),
        "receipt_path": str(output / f"{base_name}.receipt.json"),
        "stop_signal_path": str(stop_signal_path),
        "worker_pid": None,
        "ffmpeg_pid": None,
        "failure_reason": None,
    }
    write_json(session_path, session)

    command = [
        sys.executable,
        str(Path(__file__).resolve()),
        "--action",
        "worker",
        "--session-file",
        str(session_path),
        "--ffmpeg-path",
        ffmpeg,
        "--ffprobe-path",
        ffprobe,
    ]
    flags = getattr(subprocess, "CREATE_NO_WINDOW", 0) | getattr(
        subprocess, "DETACHED_PROCESS", 0
    )
    worker = subprocess.Popen(
        command,
        stdin=subprocess.DEVNULL,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        close_fds=True,
        creationflags=flags,
    )
    session = update_session(session_path, worker_pid=worker.pid)
    print_json(
        {
            "action": "start",
            "status": session["status"],
            "session_file": str(session_path),
            "worker_pid": worker.pid,
            "expected_mp4": session["mp4_path"],
            "max_duration_seconds": session["max_duration_seconds"],
        }
    )


def ffmpeg_recording_arguments(session: dict[str, Any]) -> list[str]:
    arguments = [
        "-hide_banner",
        "-loglevel",
        "warning",
        "-y",
        "-f",
        "gdigrab",
        "-framerate",
        str(session["frame_rate"]),
        "-draw_mouse",
        "1",
    ]
    if session["capture_mode"] == "region":
        region = session["region"]
        arguments.extend(
            [
                "-offset_x",
                str(region["x"]),
                "-offset_y",
                str(region["y"]),
                "-video_size",
                f'{region["width"]}x{region["height"]}',
                "-i",
                "desktop",
            ]
        )
    else:
        arguments.extend(["-i", f'title={session["window_title"]}'])

    if session.get("audio_device_name"):
        arguments.extend(
            [
                "-f",
                "dshow",
                "-i",
                f'audio={session["audio_device_name"]}',
                "-map",
                "0:v:0",
                "-map",
                "1:a:0",
            ]
        )
    else:
        arguments.extend(["-map", "0:v:0"])
    arguments.extend(
        ["-c:v", "libx264", "-preset", "veryfast", "-crf", "20", "-pix_fmt", "yuv420p"]
    )
    if session.get("audio_device_name"):
        arguments.extend(["-c:a", "aac", "-b:a", "160k"])
    arguments.extend(["-f", "matroska", session["mkv_path"]])
    return arguments


def finalize_failure(session_path: Path, failure: str) -> None:
    try:
        session = update_session(
            session_path,
            status="failed",
            completed_at=now_iso(),
            failure_reason=failure,
            ffmpeg_pid=None,
        )
        Path(session["log_path"]).write_text(failure + "\n", encoding="utf-8")
        write_json(
            Path(session["receipt_path"]),
            {
                "schema_version": 1,
                "session_id": session["session_id"],
                "platform": session["platform"],
                "status": "failed",
                "started_at": session["started_at"],
                "completed_at": session["completed_at"],
                "mp4_path": session["mp4_path"],
                "log_path": session["log_path"],
                "failure_reason": failure,
            },
        )
    except Exception:
        pass


def worker(args: argparse.Namespace) -> None:
    require_windows()
    if not args.session_file:
        raise RuntimeError("Worker requires --session-file.")
    session_path = Path(args.session_file).expanduser().resolve()
    ffmpeg = resolve_executable(args.ffmpeg_path, "ffmpeg")
    ffprobe = resolve_executable(args.ffprobe_path, "ffprobe")
    process: subprocess.Popen[bytes] | None = None

    try:
        deadline = time.monotonic() + 5
        session = read_json(session_path)
        while not session.get("worker_pid") and time.monotonic() < deadline:
            time.sleep(0.1)
            session = read_json(session_path)

        log_path = Path(session["log_path"])
        log_path.parent.mkdir(parents=True, exist_ok=True)
        with log_path.open("wb") as log_stream:
            process = subprocess.Popen(
                [ffmpeg, *ffmpeg_recording_arguments(session)],
                stdin=subprocess.PIPE,
                stdout=subprocess.DEVNULL,
                stderr=log_stream,
                creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
            )
            session = update_session(
                session_path, status="recording", ffmpeg_pid=process.pid
            )
            recording_deadline = time.monotonic() + int(
                session["max_duration_seconds"]
            )
            stop_signal = Path(session["stop_signal_path"])
            while (
                process.poll() is None
                and not stop_signal.exists()
                and time.monotonic() < recording_deadline
            ):
                time.sleep(0.2)

            if process.poll() is None and process.stdin:
                try:
                    process.stdin.write(b"q\n")
                    process.stdin.flush()
                except (BrokenPipeError, OSError):
                    pass
                try:
                    process.wait(timeout=15)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait()
            recording_exit_code = process.returncode

        session = update_session(session_path, status="finalizing")
        mkv_path = Path(session["mkv_path"])
        mp4_path = Path(session["mp4_path"])
        remux = None
        if mkv_path.is_file():
            remux = run_captured(
                ffmpeg,
                [
                    "-hide_banner",
                    "-loglevel",
                    "error",
                    "-y",
                    "-i",
                    str(mkv_path),
                    "-c",
                    "copy",
                    "-movflags",
                    "+faststart",
                    str(mp4_path),
                ],
            )

        with log_path.open("a", encoding="utf-8", errors="replace") as log_stream:
            log_stream.write(f"\nrecording_exit_code={recording_exit_code}\n")
            if remux:
                log_stream.write(f"remux_exit_code={remux.returncode}\n")
                log_stream.write(remux.stderr or "")
            else:
                log_stream.write("remux_not_started=mkv_missing\n")

        probe = None
        probe_data = None
        if mp4_path.is_file():
            probe = run_captured(
                ffprobe,
                [
                    "-v",
                    "error",
                    "-show_entries",
                    "format=duration,size:stream=index,codec_name,width,height,r_frame_rate",
                    "-of",
                    "json",
                    str(mp4_path),
                ],
            )
            if probe.returncode == 0 and probe.stdout:
                probe_data = json.loads(probe.stdout)

        completed = bool(
            recording_exit_code == 0
            and remux
            and remux.returncode == 0
            and probe
            and probe.returncode == 0
            and mp4_path.is_file()
            and mp4_path.stat().st_size > 0
        )
        if completed:
            failure = None
        elif not mkv_path.is_file():
            failure = (
                "FFmpeg did not produce the Matroska recording. Inspect the log "
                "and window/audio selection."
            )
        elif not remux or remux.returncode != 0:
            failure = "The recording exists, but MP4 remuxing failed. Inspect the FFmpeg log."
        elif not probe or probe.returncode != 0:
            failure = "The MP4 was created, but FFprobe could not validate it."
        else:
            failure = f"Recording failed with FFmpeg exit code {recording_exit_code}."

        completed_at = now_iso()
        receipt = {
            "schema_version": 1,
            "session_id": session["session_id"],
            "platform": session["platform"],
            "status": "completed" if completed else "failed",
            "started_at": session["started_at"],
            "completed_at": completed_at,
            "capture_mode": session["capture_mode"],
            "window_title": session["window_title"],
            "region": session["region"],
            "frame_rate_requested": session["frame_rate"],
            "audio_requested": bool(session.get("audio_device_name")),
            "audio_device_name": session.get("audio_device_name"),
            "mp4_path": str(mp4_path),
            "mp4_size_bytes": mp4_path.stat().st_size if mp4_path.is_file() else 0,
            "mp4_sha256": sha256_file(mp4_path) if mp4_path.is_file() else None,
            "mkv_path": str(mkv_path),
            "log_path": str(log_path),
            "media": probe_data,
            "failure_reason": failure,
        }
        write_json(Path(session["receipt_path"]), receipt)
        update_session(
            session_path,
            status=receipt["status"],
            completed_at=completed_at,
            failure_reason=failure,
            ffmpeg_pid=None,
        )
    except Exception as exc:
        if process and process.poll() is None:
            process.kill()
        finalize_failure(session_path, str(exc))
        raise


def stop_recording(args: argparse.Namespace) -> None:
    session_path = absolute_session_path(args.session_file, args.output_directory)
    session = read_json(session_path)
    if session["status"] in FINAL_STATUSES:
        print_json(session)
        return

    session = update_session(session_path, status="stopping")
    stop_signal = Path(session["stop_signal_path"])
    stop_signal.parent.mkdir(parents=True, exist_ok=True)
    stop_signal.write_text("stop\n", encoding="utf-8")

    deadline = time.monotonic() + args.wait_seconds
    while time.monotonic() < deadline:
        time.sleep(0.25)
        session = read_json(session_path)
        if session["status"] in FINAL_STATUSES:
            break
        if not process_alive(session.get("worker_pid")):
            break
    receipt_path = Path(session["receipt_path"])
    receipt = read_json(receipt_path) if receipt_path.is_file() else None
    print_json(
        {
            "action": "stop",
            "status": session["status"],
            "session_file": str(session_path),
            "receipt": receipt,
        }
    )


def status(args: argparse.Namespace) -> None:
    session_path = absolute_session_path(args.session_file, args.output_directory)
    session = read_json(session_path)
    receipt_path = Path(session["receipt_path"])
    receipt = read_json(receipt_path) if receipt_path.is_file() else None
    print_json(
        {
            "action": "status",
            "session_file": str(session_path),
            "worker_alive": process_alive(session.get("worker_pid")),
            "session": session,
            "receipt": receipt,
        }
    )


def ranged_integer(minimum: int, maximum: int):
    def parse(value: str) -> int:
        number = int(value)
        if not minimum <= number <= maximum:
            raise argparse.ArgumentTypeError(
                f"value must be between {minimum} and {maximum}"
            )
        return number

    return parse


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--action", "-Action", required=True, type=str.lower,
                        choices=["doctor", "start", "stop", "status", "worker"])
    parser.add_argument("--platform", "-Platform", type=str.lower,
                        choices=["wechat", "douyin"])
    parser.add_argument("--output-directory", "-OutputDirectory")
    parser.add_argument("--session-file", "-SessionFile")
    parser.add_argument("--window-title-pattern", "-WindowTitlePattern")
    parser.add_argument("--capture-mode", "-CaptureMode", default="window",
                        type=str.lower, choices=["window", "region"])
    parser.add_argument("--frame-rate", "-FrameRate", type=ranged_integer(1, 120), default=30)
    parser.add_argument("--max-duration-seconds", "-MaxDurationSeconds",
                        type=ranged_integer(10, 86400), default=300)
    parser.add_argument("--capture-x", "-CaptureX", type=int, default=0)
    parser.add_argument("--capture-y", "-CaptureY", type=int, default=0)
    parser.add_argument("--capture-width", "-CaptureWidth", type=int, default=0)
    parser.add_argument("--capture-height", "-CaptureHeight", type=int, default=0)
    parser.add_argument("--audio-device-name", "-AudioDeviceName")
    parser.add_argument("--ffmpeg-path", "-FfmpegPath")
    parser.add_argument("--ffprobe-path", "-FfprobePath")
    parser.add_argument("--wait-seconds", "-WaitSeconds",
                        type=ranged_integer(1, 300), default=45)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    actions = {
        "doctor": doctor,
        "start": start_recording,
        "stop": stop_recording,
        "status": status,
        "worker": worker,
    }
    try:
        actions[args.action](args)
        return 0
    except Exception as exc:
        print_json({"action": args.action, "status": "error", "error": str(exc)})
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
