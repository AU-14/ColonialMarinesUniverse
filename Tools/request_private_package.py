#!/usr/bin/env python3
# CMU14: Retrieve verified server packages without copying private source or logs into public CI.
"""Request a server package from private CI; failures never fall back to public builds."""

import argparse
import hashlib
import json
import os
import re
import sys
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
import zipfile
from pathlib import Path

REPOSITORY = "AU-14/CMU-ServerBuild"
WORKFLOW = "package-server.yml"
API = f"https://api.github.com/repos/{REPOSITORY}"
MAX_PACKAGE_SIZE = 4 * 1024**3


class PackageError(Exception):
    pass


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def api(path, token, body=None):
    request = urllib.request.Request(
        API + path,
        data=json.dumps(body).encode() if body is not None else None,
        headers={"Authorization": f"Bearer {token}", "Accept": "application/vnd.github+json",
                 "X-GitHub-Api-Version": "2022-11-28", "User-Agent": "CMU-package-request",
                 "Content-Type": "application/json"},
    )
    try:
        with urllib.request.build_opener(NoRedirect).open(request, timeout=60) as response:
            data = response.read()
            return json.loads(data) if data else None
    except urllib.error.HTTPError as error:
        if error.code == 302:
            return error.headers["Location"]
        # Private API bodies and logs must not be echoed into public workflow logs.
        raise PackageError(f"Private build API returned HTTP {error.code}") from None


def receive_package(artifact_path, output, expected):
    """Validate every receipt field and the package digest before exposing a ZIP to publishing."""
    name = f"SS14.Server_{expected['platform']}.zip"
    with zipfile.ZipFile(artifact_path) as archive:
        entries = archive.infolist()
        if len(entries) != 2 or {e.filename for e in entries} != {name, "manifest.json"}:
            raise PackageError("Unexpected private artifact contents")
        if archive.getinfo("manifest.json").file_size > 16384:
            raise PackageError("Invalid package receipt size")
        receipt = json.loads(archive.read("manifest.json"))
        if any(receipt.get(key) != value for key, value in expected.items()):
            raise PackageError("Package receipt does not match the requested build")
        info = archive.getinfo(name)
        if (receipt.get("package") != name or receipt.get("size") != info.file_size
                or not 0 < info.file_size <= MAX_PACKAGE_SIZE
                or not re.fullmatch(r"[0-9a-f]{64}", receipt.get("sha256", ""))):
            raise PackageError("Invalid package receipt")
        output.mkdir(parents=True, exist_ok=True)
        destination = output / name
        if destination.exists():
            raise PackageError("Refusing to replace an existing package")
        with tempfile.TemporaryDirectory(dir=output) as temporary:
            staged = Path(temporary) / name
            digest = hashlib.sha256()
            with archive.open(name) as source, staged.open("wb") as target:
                while chunk := source.read(1024 * 1024):
                    digest.update(chunk)
                    target.write(chunk)
            if digest.hexdigest() != receipt["sha256"] or not zipfile.is_zipfile(staged):
                raise PackageError("Package integrity check failed")
            staged.replace(destination)
    return destination


def download_artifact(url, destination):
    parsed = urllib.parse.urlparse(url)
    allowed = (".blob.core.windows.net", ".actions.githubusercontent.com", ".githubusercontent.com")
    if parsed.scheme != "https" or not parsed.hostname or not parsed.hostname.endswith(allowed):
        raise PackageError("Unexpected GitHub artifact download host")
    # Signed artifact URLs receive no repository credential and cannot redirect it elsewhere.
    with urllib.request.build_opener(NoRedirect).open(url, timeout=120) as response:
        total = 0
        with destination.open("wb") as target:
            while chunk := response.read(1024 * 1024):
                total += len(chunk)
                if total > MAX_PACKAGE_SIZE + 1024**2:
                    raise PackageError("Private artifact exceeds the size limit")
                target.write(chunk)


def request_package(args):
    token = os.environ.get("CMU_PRIVATE_BUILD_TOKEN", "")
    if not token:
        raise PackageError("CMU_PRIVATE_BUILD_TOKEN is not configured")
    if not re.fullmatch(r"[0-9a-f]{40}", args.source_sha):
        raise PackageError("A full public source commit SHA is required")
    pulls = json.loads(os.environ.get("CMU_TEST_MERGE_PRS", "[]"))
    if not isinstance(pulls, list) or len(pulls) > 20:
        raise PackageError("Invalid test-merge request")
    heads = []
    for pull in pulls:
        if (type(pull.get("number")) is not int or pull["number"] < 1
                or not re.fullmatch(r"[0-9a-f]{40}", pull.get("head_sha", ""))):
            raise PackageError("Invalid test-merge PR reference")
        heads.append({"number": pull["number"], "head_sha": pull["head_sha"]})
    if bool(heads) != bool(args.patch):
        raise PackageError("Test-merge PR references and patch must be provided together")
    patch_digest = hashlib.sha256(args.patch.read_bytes()).hexdigest() if args.patch else ""
    request_id = uuid.uuid4().hex
    build_sha = api("/git/ref/heads/main", token)["object"]["sha"]
    expected = {"schema": 1, "request_id": request_id, "source_sha": args.source_sha,
                "build_sha": build_sha, "platform": args.platform, "patch_sha256": patch_digest,
                "audio": args.require_audio}
    api(f"/actions/workflows/{WORKFLOW}/dispatches", token, {
        "ref": "main",
        "inputs": {"source_sha": args.source_sha, "platform": args.platform,
                   "request_id": request_id, "require_audio": str(args.require_audio).lower(),
                   "pr_heads": json.dumps(heads), "patch_sha256": patch_digest},
    })
    print("Private server package requested.", flush=True)
    deadline = time.monotonic() + args.timeout_minutes * 60
    run_id = None
    previous_status = None
    while time.monotonic() < deadline:
        if run_id is None:
            runs = api(f"/actions/workflows/{WORKFLOW}/runs?event=workflow_dispatch&per_page=100", token)
            matches = [r for r in runs["workflow_runs"] if r["display_title"] == f"package-{request_id}"]
            if len(matches) > 1:
                raise PackageError("Ambiguous private build request")
            if not matches:
                time.sleep(10)
                continue
            run = matches[0]
            run_id = run["id"]
        else:
            run = api(f"/actions/runs/{run_id}", token)
        if (run["head_sha"] != build_sha or run["event"] != "workflow_dispatch"
                or run["path"] != f".github/workflows/{WORKFLOW}"):
            raise PackageError("Private build revision changed; request the package again")
        status = run["status"]
        if status != previous_status:
            print(f"Private server build: {status}.", flush=True)
            previous_status = status
        if status == "completed":
            if run["conclusion"] != "success":
                raise PackageError("Private server build failed; maintainers can inspect its private logs")
            break
        time.sleep(10)
    else:
        raise PackageError("Timed out waiting for the private server build")
    artifacts = api(f"/actions/runs/{run_id}/artifacts?per_page=100", token)["artifacts"]
    matches = [a for a in artifacts if a["name"] == f"server-{request_id}" and not a["expired"]]
    if len(matches) != 1:
        raise PackageError("Missing or ambiguous private server artifact")
    with tempfile.TemporaryDirectory() as temporary:
        archive = Path(temporary) / "artifact.zip"
        location = api(f"/actions/artifacts/{matches[0]['id']}/zip", token)
        download_artifact(location, archive)
        package = receive_package(archive, args.output, expected)
    print(f"Verified server package: {package.name}", flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-sha", required=True)
    parser.add_argument("--platform", required=True, choices=["linux-x64", "win-x64"])
    parser.add_argument("--patch", type=Path)
    parser.add_argument("--require-audio", action="store_true")
    parser.add_argument("--output", type=Path, default=Path("release"))
    parser.add_argument("--timeout-minutes", type=int, default=50)
    args = parser.parse_args()
    try:
        request_package(args)
    except PackageError as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 1
    except (OSError, ValueError, KeyError, TypeError, zipfile.BadZipFile):
        print("ERROR: Private package request could not be completed or verified.", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
