"""Inventory Bosenmori exact-port placeholders and their map usage."""

from __future__ import annotations

from collections import Counter
from pathlib import Path
import json
import re


ROOT = Path(__file__).resolve().parents[1]
EXACT = ROOT / "Content.CMU/Resources/Prototypes/CMU14/Maps/Prototypes/Bosenmori/exact_port.yml"
MAPS = ROOT / "Content.CMU/Resources/Maps/CMU14/BosenmoriBashoMultiZ"


def load() -> tuple[dict[str, dict[str, str | int]], Counter[str]]:
    prototypes: dict[str, dict[str, str | int]] = {}
    for block in re.split(r"(?=^- type: (?:entity|tile)$)", EXACT.read_text(encoding="utf-8"), flags=re.M):
        identifier = re.search(r"^  id: (CMUBosenmoriExact[0-9a-f]{16})$", block, re.M)
        if not identifier:
            continue
        field = lambda pattern: (match.group(1) if (match := re.search(pattern, block, re.M)) else "")
        prototypes[identifier.group(1)] = {
            "source": field(r"^  # CMSS13 source: (.+)$"),
            "name": field(r'^  name: "([^"]+)"$'),
            "parent": field(r"^  parent: (.+)$"),
            "sprite": field(r"^    sprite: (.+)$"),
            "offset": field(r"^    offset: (.+)$"),
            "kind": field(r"^- type: (\w+)$"),
        }

    counts: Counter[str] = Counter()
    for path in MAPS.glob("*.yml"):
        counts.update(re.findall(r"^- proto: (CMUBosenmoriExact[0-9a-f]{16})$", path.read_text(encoding="utf-8"), re.M))
    return prototypes, counts


if __name__ == "__main__":
    prototypes, counts = load()
    used = [(identifier, count, prototypes[identifier]) for identifier, count in counts.most_common()]
    print(f"{len(prototypes)} placeholders, {len(counts)} used IDs, {sum(counts.values())} placed entities")
    for identifier, count, proto in used[:80]:
        print(f"{count:5d}  {identifier}  {proto['source']}")
    groups = Counter(str(proto["source"]).split("/")[2] if len(str(proto["source"]).split("/")) > 2 else "" for _, _, proto in used)
    print("Group counts:", json.dumps(groups, sort_keys=True))
