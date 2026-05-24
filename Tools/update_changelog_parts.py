#!/usr/bin/env python3
"""
Extracts changelog .yml parts from the latest PRs, via the :cl: entries.
Each part includes: author (required), changes (required), time, url, category
usage: update_changelog_parts.py <parts-dir> --category "Main"
"""

import os
import re
import argparse
import yaml
import requests

CATEGORY_MAIN = "Main"
GITHUB_API_URL = "https://api.github.com"

COMMENT_RE = re.compile(r"<!--.*?-->", re.DOTALL)
HEADER_RE = re.compile(
    r"^\s*(?::cl:|🆑)\s*([a-z0-9_\- ,]+)?\s*$", re.IGNORECASE | re.MULTILINE
)
ENTRY_RE = re.compile(
    r"^ *[*-]? *(add|remove|tweak|fix|map|code|admin): *([^\n\r]+)\r?$",
    re.IGNORECASE | re.MULTILINE,
)

TYPE_MAP = {
    "add": "Add",
    "remove": "Remove",
    "tweak": "Tweak",
    "fix": "Fix",
    "map": "Map",
    "code": "Code",
    "admin": "Admin",
}
DEFAULT_MESSAGES = {
    "Added fun!",
    "Removed fun!",
    "Changed fun!",
    "Fixed fun!",
    "Mapped fun!",
    "Admin related change!",
    "Code related change for contributors!",
}


def make_session(token: str) -> requests.Session:
    sess = requests.Session()
    sess.headers.update(
        {
            "Authorization": f"Bearer {token}",
            "X-GitHub-Api-Version": "2022-11-28",
            "Accept": "application/vnd.github+json",
        }
    )
    return sess


def get_last_run_time(sess: requests.Session, repo: str, run_id: str) -> str:
    current = sess.get(f"{GITHUB_API_URL}/repos/{repo}/actions/runs/{run_id}")
    current.raise_for_status()
    current_run = current.json()

    params = {"status": "success", "created": f"<={current_run['created_at']}"}
    past = sess.get(f"{current_run['workflow_url']}/runs", params=params)
    past.raise_for_status()

    for run in past.json()["workflow_runs"]:
        if run["id"] == current_run["id"]:
            continue
        print(f"Last successful run was {run['id']} at {run['created_at']}")
        return run["created_at"]

    print("No previous successful run found, using fallback date (jun. '25)")
    return "2025-06-01T00:00:00Z"


def get_merged_prs(sess: requests.Session, repo: str, since: str) -> list[dict]:
    prs = []
    page = 1
    while True:
        q = f"repo:{repo} is:pr is:merged merged:>={since}"
        resp = sess.get(
            f"{GITHUB_API_URL}/search/issues",
            params={
                "q": q,
                "sort": "created",
                "order": "asc",
                "per_page": 100,
                "page": page,
            },
        )
        resp.raise_for_status()
        items = resp.json()["items"]
        prs.extend(items)
        if len(items) < 100:
            break
        page += 1
        if page > 10:
            print("Warning: hit GitHub search cap of 1000 results.")
            break
    return prs


def fetch_pr(sess: requests.Session, repo: str, number: int) -> dict:
    resp = sess.get(f"{GITHUB_API_URL}/repos/{repo}/pulls/{number}")
    resp.raise_for_status()
    return resp.json()


def parse_cl_block(body: str, pr_author: str) -> tuple[str, list[dict]] | None:
    body = COMMENT_RE.sub("", body)
    match = HEADER_RE.search(body)
    if not match:
        return None
    author = (match.group(1) or "").strip() or pr_author
    changes = [
        {"type": TYPE_MAP[m.group(1).lower()], "message": m.group(2).strip()}
        for m in ENTRY_RE.finditer(body[match.end() :])
        if m.group(2).strip() not in DEFAULT_MESSAGES
    ]
    return (author, changes) if changes else None


def write_part(
    parts_dir: str,
    pr_number: int,
    author: str,
    changes: list,
    time: str,
    url: str,
    category: str,
) -> None:
    part = {
        "author": author,
        "changes": changes,
        "time": time,
        "url": url,
        "category": category,
    }
    path = os.path.join(parts_dir, f"pr-{pr_number}.yml")
    if os.path.exists(path):
        print(f"Part for PR #{pr_number} already exists, skipping.")
        return
    with open(path, "w", encoding="utf-8") as f:
        yaml.safe_dump(part, f, allow_unicode=True)
    print(f"Wrote part for PR #{pr_number} by {author}")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("parts_dir")
    parser.add_argument("--category", default=CATEGORY_MAIN)
    args = parser.parse_args()

    token = os.environ["GITHUB_TOKEN"]
    repo = os.environ["GITHUB_REPOSITORY"]
    run_id = os.environ["GITHUB_RUN_ID"]
    start = os.environ.get("START_DATE")

    sess = make_session(token)
    with open("Resources/Changelog/CMU.yml", "r") as f:
        current = yaml.safe_load(f)
    entries = (current or {}).get("Entries", [])
    since = (
        "2025-06-01T00:00:00Z"  # start date when changelog is empty
        if not entries
        else (start if start else get_last_run_time(sess, repo, run_id))
    )

    print(f"Fetching PRs merged since {since}")

    prs = get_merged_prs(sess, repo, since)
    print(f"Found {len(prs)} merged PRs")

    written = 0
    for item in prs:
        pr = fetch_pr(sess, repo, item["number"])
        body = pr.get("body") or ""
        result = parse_cl_block(body, pr["user"]["login"])
        if result is None:
            print(f"PR #{pr['number']}: no :cl: block, skipping")
            continue
        author, changes = result
        time = pr["merged_at"].replace("Z", ".0000000+00:00")
        write_part(
            args.parts_dir,
            pr["number"],
            author,
            changes,
            time,
            pr["html_url"],
            args.category,
        )
        written += 1

    print(f"Done. Wrote {written} parts from {len(prs)} PRs.")


if __name__ == "__main__":
    main()
