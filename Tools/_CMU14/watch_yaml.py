#!/usr/bin/env python3
"""Stop a test runner when this attempt's YAML job fails."""

import json
import os
import re
import signal
import sys
import time
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen

JOB = "YAML Linter"
# The Actions token allows 1,000 requests per hour per repository, shared with the
# label and retry workflows: at this interval fifteen runners polling for a whole
# 36-minute lane stay inside it. A green YAML job ends polling after a few minutes.
POLL = 60
BACKOFF_MAX = 300
NEXT = re.compile(r'<([^>]+)>;\s*rel="next"')


def pages(url, headers):
    while url:
        with urlopen(Request(url, headers=headers), timeout=10) as response:
            yield from json.load(response)["jobs"]
            match = NEXT.search(response.headers.get("Link") or "")
            url = match and match.group(1)


def verdict(url, headers):
    """The YAML job's result, or None with the kind of uncertainty."""
    try:
        matches = [job for job in pages(url, headers) if job["name"] == JOB]
    except (HTTPError, URLError, TimeoutError, OSError, ValueError, KeyError) as error:
        return None, "unavailable", str(error)
    if len(matches) != 1:
        return None, "ambiguous", f"{len(matches)} matching jobs"
    if matches[0]["status"] != "completed":
        return None, "", ""
    # Required Checks wants success; a timed-out or cancelled job fails the run too.
    return ("clear" if matches[0]["conclusion"] == "success" else "failed"), "", ""


def warn(kind, detail):
    print(f"::warning::YAML job {kind} ({detail}); tests continue.", flush=True)


def main():
    headers = {"Authorization": f"Bearer {os.environ['CI_YAML_TOKEN']}", "Accept": "application/vnd.github+json"}
    url = (f"{os.environ['GITHUB_API_URL']}/repos/{os.environ['GITHUB_REPOSITORY']}/actions/runs/"
           f"{os.environ['GITHUB_RUN_ID']}/attempts/{os.environ['GITHUB_RUN_ATTEMPT']}/jobs?per_page=100")
    result, kind, detail = verdict(url, headers)
    if sys.argv[1:] == ["--check"]:
        if result == "failed":
            print(f"::error::{JOB} failed; skipping the tests.", flush=True)
            return 1
        if kind:
            warn(kind, detail)
        return 0
    pid = int(sys.argv[1])
    delay, warned = POLL, set()
    while True:
        if result:
            if result == "failed":
                print(f"::error::{JOB} failed; stopping the test runner.", flush=True)
                try:
                    os.kill(pid, signal.SIGTERM)
                except ProcessLookupError:
                    pass
            return
        if kind:
            if kind not in warned:
                warn(kind, detail)
                warned.add(kind)
            delay = min(delay * 2, BACKOFF_MAX) if kind == "unavailable" else delay
        time.sleep(delay)
        result, kind, detail = verdict(url, headers)
        if not kind:
            delay = POLL


if __name__ == "__main__":
    sys.exit(main())
