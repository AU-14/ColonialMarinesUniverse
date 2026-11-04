#!/usr/bin/env python3

import argparse
import concurrent.futures
import os
import subprocess
from typing import Iterable

import requests

PUBLISH_TOKEN = os.environ["PUBLISH_TOKEN"]
VERSION = os.environ.get("PUBLISH_VERSION") or os.environ["GITHUB_SHA"]

RELEASE_DIR = "release"
DEFAULT_UPLOAD_WORKERS = 4

#
# CONFIGURATION PARAMETERS
# Forks should change these to publish to their own infrastructure.
#
ROBUST_CDN_URL = "https://cmu-cdn.cm-ss13.com/"
FORK_ID = "cmu"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--fork-id", default=FORK_ID)
    parser.add_argument(
        "--upload-workers",
        type=int,
        default=int(os.environ.get("PUBLISH_UPLOAD_WORKERS", DEFAULT_UPLOAD_WORKERS)),
        help="Maximum number of release files to upload concurrently.",
    )
    subparsers = parser.add_subparsers(dest="command")
    subparsers.add_parser("start", help="Start a publish operation.")
    upload_parser = subparsers.add_parser(
        "upload", help="Upload files to an existing publish operation."
    )
    upload_parser.add_argument("files", nargs="+")
    subparsers.add_parser("finish", help="Finish a publish operation.")

    args = parser.parse_args()
    fork_id = args.fork_id
    upload_workers = max(1, args.upload_workers)

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

    if not start_publish(fork_id):
        return

    files = list(get_files_to_publish())
    if not files:
        raise RuntimeError(f"No release files found in {RELEASE_DIR}")

    publish_files(files, fork_id, upload_workers)
    finish_publish(fork_id)


def start_publish(fork_id: str) -> bool:
    session = create_session()
    print(f"Starting publish on Robust.Cdn for version {VERSION}")

    data = {
        "version": VERSION,
        "engineVersion": get_engine_version(),
    }
    headers = {"Content-Type": "application/json"}

    resp = session.post(
        f"{ROBUST_CDN_URL}fork/{fork_id}/publish/start", json=data, headers=headers
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
        f"{ROBUST_CDN_URL}fork/{fork_id}/publish/finish", json=data, headers=headers
    )
    resp.raise_for_status()
    print("SUCCESS!")


def create_session() -> requests.Session:
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


def publish_file(file: str, fork_id: str):
    print(f"Publishing {file}")
    with requests.Session() as session:
        session.headers.update(
            {
                "Authorization": f"Bearer {PUBLISH_TOKEN}",
            }
        )
        with open(file, "rb") as f:
            headers = {
                "Content-Type": "application/octet-stream",
                "Robust-Cdn-Publish-File": os.path.basename(file),
                "Robust-Cdn-Publish-Version": VERSION,
            }
            resp = session.post(
                f"{ROBUST_CDN_URL}fork/{fork_id}/publish/file", data=f, headers=headers
            )
        resp.raise_for_status()


def get_engine_version() -> str:
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
