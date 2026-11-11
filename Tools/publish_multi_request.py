#!/usr/bin/env python3

import argparse
import concurrent.futures
# CMU14: bounded upload recovery with response-aware streaming.
import json
import os
import subprocess
import tempfile
import time
import zipfile
from pathlib import Path
from typing import Iterable

import requests

PUBLISH_TOKEN = os.environ.get("PUBLISH_TOKEN", "")  # CMU14: read-only checks need no secret.
VERSION = os.environ.get("PUBLISH_VERSION") or os.environ["GITHUB_SHA"]

RELEASE_DIR = "release"
# CMU14: avoid competing uploads into the same CDN publish transaction.
DEFAULT_UPLOAD_WORKERS = 1
REQUEST_TIMEOUT = (30, 120)
REQUIRED_PACKAGES = {"SS14.Client.zip", "SS14.Server_linux-x64.zip", "SS14.Server_win-x64.zip"}
TRANSIENT_HTTP = {408, 429, 500, 502, 503, 504}
TRANSIENT_CURL = {5, 6, 7, 18, 28, 35, 52, 55, 56, 92}


# CMU14: distinguish recoverable transport failures from permission/configuration failures.
class UploadError(RuntimeError):
    def __init__(self, message: str, retryable: bool):
        super().__init__(message)
        self.retryable = retryable


#
# CONFIGURATION PARAMETERS
# Forks should change these to publish to their own infrastructure.
#
ROBUST_CDN_URL = "https://cmu-cdn.cm-ss13.com/"
FORK_ID = "cmu"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--fork-id", default=FORK_ID)
    # CMU14: retry the complete upload transaction using the existing packages.
    parser.add_argument("--attempts", type=int, choices=range(1, 6), default=3)
    parser.add_argument(
        "--upload-workers",
        type=int,
        default=int(os.environ.get("PUBLISH_UPLOAD_WORKERS", DEFAULT_UPLOAD_WORKERS)),
        help="Maximum number of release files to upload concurrently.",
    )
    subparsers = parser.add_subparsers(dest="command")
    subparsers.add_parser("check", help="Check publication without starting an upload transaction.")  # CMU14
    subparsers.add_parser("verify", help="Verify the complete release without publishing.")  # CMU14
    subparsers.add_parser("start", help="Start a publish operation.")
    upload_parser = subparsers.add_parser(
        "upload", help="Upload files to an existing publish operation."
    )
    upload_parser.add_argument("files", nargs="+")
    subparsers.add_parser("finish", help="Finish a publish operation.")

    args = parser.parse_args()
    fork_id = args.fork_id
    upload_workers = max(1, args.upload_workers)

    # CMU14: CI prepares metadata before building; CDN mutation waits for all packages.
    if args.command == "check":
        published = check_published(fork_id)
        set_github_output("already-published", str(published).lower())
        if not published:
            set_github_output("engine-version", get_engine_version())
        return

    if args.command == "start":
        started = start_publish(fork_id)
        set_github_output("already-published", str(not started).lower())
        return

    if args.command == "upload":
        publish_files(args.files, fork_id, upload_workers)
        return

    if args.command == "finish":
        finish_publish(fork_id)
        return

    # CMU14: validate the complete release before resetting any incomplete CDN upload.
    files = list(get_files_to_publish())
    if {os.path.basename(file) for file in files} != REQUIRED_PACKAGES:
        raise ValueError("Publishing requires exactly the client, Linux, and Windows server ZIPs")
    if any(not zipfile.is_zipfile(file) for file in files):
        raise ValueError("A release package is not a valid ZIP")
    if args.command == "verify":
        print("Verified client, Linux, and Windows release ZIPs.")
        return

    publish_release(files, fork_id, upload_workers, args.attempts)


# CMU14: checking an existing release must not leave an in-progress upload behind.
def check_published(fork_id: str) -> bool:
    response = requests.get(f"{ROBUST_CDN_URL}fork/{fork_id}/manifest", timeout=REQUEST_TIMEOUT)
    response.raise_for_status()
    return VERSION in response.json()["builds"]


# CMU14: interrupted uploads leave partial files that reject per-file retries.
def publish_release(files: list[str], fork_id: str, upload_workers: int, attempts: int):
    for attempt in range(1, attempts + 1):
        try:
            if not start_publish(fork_id):
                return
            publish_files(files, fork_id, upload_workers)
            finish_publish(fork_id)
            return
        except (UploadError, requests.RequestException) as error:
            if isinstance(error, UploadError):
                retryable = error.retryable
            elif isinstance(error, requests.HTTPError):
                retryable = error.response is not None and error.response.status_code in TRANSIENT_HTTP
            else:
                retryable = isinstance(error, (requests.ConnectionError, requests.Timeout))
            if not retryable or attempt == attempts:
                raise
            print(f"Publish attempt {attempt} failed: {error}. Restarting upload using the same packages.", flush=True)
            time.sleep(attempt)


def start_publish(fork_id: str) -> bool:
    session = create_session()
    print(f"Starting publish on Robust.Cdn for version {VERSION}")

    data = {
        "version": VERSION,
        "engineVersion": get_engine_version(),
    }
    headers = {"Content-Type": "application/json"}

    resp = session.post(
        f"{ROBUST_CDN_URL}fork/{fork_id}/publish/start", json=data, headers=headers, timeout=REQUEST_TIMEOUT  # CMU14
    )
    if resp.status_code == 409:
        try:
            msg = resp.json()
        except Exception:
            msg = resp.text
        print(f"Version {VERSION} already published (CDN: {msg}), skipping...")
        return False
    resp.raise_for_status()
    print("Publish successfully started.")
    return True


def publish_files(files: Iterable[str], fork_id: str, upload_workers: int):
    files = list(files)
    if not files:
        raise RuntimeError("No files specified for upload")

    for file in files:
        if not os.path.isfile(file):
            raise FileNotFoundError(file)

    with concurrent.futures.ThreadPoolExecutor(
        max_workers=min(upload_workers, len(files))
    ) as executor:
        futures = {
            executor.submit(publish_file, file, fork_id): file
            for file in files
        }
        for future in concurrent.futures.as_completed(futures):
            future.result()

    print("Successfully pushed files.")


def finish_publish(fork_id: str):
    session = create_session()
    print(f"Finishing publish on Robust.Cdn for version {VERSION}")
    data = {"version": VERSION}
    headers = {"Content-Type": "application/json"}
    resp = session.post(
        f"{ROBUST_CDN_URL}fork/{fork_id}/publish/finish", json=data, headers=headers, timeout=REQUEST_TIMEOUT  # CMU14
    )
    resp.raise_for_status()
    print("SUCCESS!")


def create_session() -> requests.Session:
    # CMU14: fail before making a mutation request when CI credentials are absent.
    if not PUBLISH_TOKEN:
        raise RuntimeError("PUBLISH_TOKEN is not configured")
    session = requests.Session()
    session.headers.update({"Authorization": f"Bearer {PUBLISH_TOKEN}"})
    return session


def set_github_output(name: str, value: str):
    output_path = os.environ.get("GITHUB_OUTPUT")
    if output_path is None:
        return

    with open(output_path, "a", encoding="UTF-8") as output:
        output.write(f"{name}={value}\n")


def get_files_to_publish() -> Iterable[str]:
    for file in sorted(os.listdir(RELEASE_DIR)):  # Consistent ordering
        path = os.path.join(RELEASE_DIR, file)
        if os.path.isfile(path):
            yield path


# CMU14: Expect/Continue exposes early HTTP rejection instead of a misleading TLS EOF.
def publish_file(file: str, fork_id: str):
    if not PUBLISH_TOKEN:
        raise RuntimeError("PUBLISH_TOKEN is not configured")
    print(f"Publishing {file}", flush=True)
    with tempfile.TemporaryDirectory() as temporary:
        response_path = Path(temporary) / "response.txt"
        # Keep credentials out of process arguments and curl diagnostics.
        config = "header = " + json.dumps(f"Authorization: Bearer {PUBLISH_TOKEN}") + "\n"
        result = subprocess.run(
            ["curl", "--config", "-", "--silent", "--show-error", "--fail-with-body",
             "--http1.1", "--expect100-timeout", "10", "--connect-timeout", "30", "--max-time", "600",
             "--request", "POST", "--header", "Expect: 100-continue",
             "--header", "Content-Type: application/octet-stream",
             "--header", f"Robust-Cdn-Publish-File: {os.path.basename(file)}",
             "--header", f"Robust-Cdn-Publish-Version: {VERSION}",
             "--data-binary", f"@{file}", "--output", str(response_path), "--write-out", "%{http_code}",
             f"{ROBUST_CDN_URL}fork/{fork_id}/publish/file"],
            input=config, text=True, capture_output=True, check=False,
        )
        status = int(result.stdout.strip() or "0")
        if result.returncode == 0 and status == 204:
            return
        body = response_path.read_text(encoding="utf-8", errors="replace")[:500] if response_path.exists() else ""
        retryable = (status in TRANSIENT_HTTP or result.returncode in TRANSIENT_CURL
                     or (status == 409 and "File already published" in body))
        raise UploadError(
            f"Upload {os.path.basename(file)} failed: HTTP {status}, curl {result.returncode}. "
            f"{body} {result.stderr.strip()[:500]}", retryable,
        )


def get_engine_version() -> str:
    # CMU14: the metadata job resolves this once, so upload retries need no engine checkout.
    if version := os.environ.get("PUBLISH_ENGINE_VERSION"):
        return version
    proc = subprocess.run(
        ["git", "describe", "--tags", "--abbrev=0"],
        stdout=subprocess.PIPE,
        cwd="RobustToolbox",
        check=True,
        encoding="UTF-8",
    )
    tag = proc.stdout.strip()
    assert tag.startswith("v")
    return tag[1:]  # Cut off v prefix.


if __name__ == "__main__":
    main()
