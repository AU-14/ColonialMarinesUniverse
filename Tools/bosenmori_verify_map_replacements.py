"""Check selected Bosenmori map groups against HEAD and clear stale fixtures."""

from __future__ import annotations

from pathlib import Path
import json
import re
import subprocess

from bosenmori_prototype_audit import ROOT


MAPS = ROOT / "Content.CMU/Resources/Maps/CMU14/BosenmoriBashoMultiZ"
MANIFEST = ROOT / "Tools/bosenmori_functional_matches.json"
GROUP = re.compile(r"(?ms)^- proto: (\S+)\n(.*?)(?=^- proto: |\Z)")
UID = re.compile(r"(?m)^  - uid: (\d+)$")
EMPTY_FIXTURES = re.compile(r"(?m)^    - type: Fixtures\n      fixtures: \{\}\n")


def run() -> None:
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    replacement = {
        item["source_id"]: (
            item["source_id"].replace("Exact", "Functional")
            if item["offset"] or item.get("visual_state") else item["target"]
        )
        for item in manifest
    }
    repaired_groups = 0
    removed_fixtures = 0
    seen: set[str] = set()
    for path in sorted(MAPS.glob("*.yml")):
        relative = path.relative_to(ROOT).as_posix()
        old = subprocess.run(["git", "show", f"HEAD:{relative}"], cwd=ROOT,
                             capture_output=True, check=True).stdout.decode("utf-8")
        original_by_uid = {
            uid: proto
            for match in GROUP.finditer(old)
            if (proto := match.group(1)) in replacement
            for uid in UID.findall(match.group(2))
        }
        content = path.read_text(encoding="utf-8")

        def repair(match: re.Match[str]) -> str:
            nonlocal repaired_groups, removed_fixtures
            proto, body = match.groups()
            source_ids = {original_by_uid[uid] for uid in UID.findall(body) if uid in original_by_uid}
            if not source_ids:
                return match.group(0)
            if len(source_ids) != 1:
                raise RuntimeError(f"Mixed converted sources in {relative}: {source_ids}")
            source_id = source_ids.pop()
            seen.add(source_id)
            expected = replacement[source_id]
            if proto != expected:
                repaired_groups += 1
            body, count = EMPTY_FIXTURES.subn("", body)
            removed_fixtures += count
            return f"- proto: {expected}\n{body}"

        updated = GROUP.sub(repair, content)
        if updated != content:
            path.write_text(updated, encoding="utf-8", newline="\n")
    missing = set(replacement) - seen
    if missing:
        raise RuntimeError(f"Missing selected source groups in HEAD maps: {sorted(missing)}")
    print(f"Verified {len(seen)} source IDs; corrected {repaired_groups} prototype groups and removed {removed_fixtures} empty Fixture overrides")


if __name__ == "__main__":
    run()
