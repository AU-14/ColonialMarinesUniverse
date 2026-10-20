"""Relate pixel-matched Bosenmori art to functional entity prototypes."""

from __future__ import annotations

from collections import Counter, defaultdict
from pathlib import Path
import json
import re

from bosenmori_prototype_audit import ROOT, load
from bosenmori_sprite_match import REPORT


def entity_prototypes() -> list[dict[str, object]]:
    result = []
    roots = [ROOT / "Resources/Prototypes", ROOT / "Content.CMU/Resources/Prototypes"]
    for root in roots:
        for path in root.rglob("*.yml"):
            if path.name in {"exact_port.yml", "functional_matches.yml"}:
                continue
            content = path.read_text(encoding="utf-8-sig")
            for block in re.split(r"(?=^- type: entity\s*$)", content, flags=re.M):
                if not block.startswith("- type: entity"):
                    continue
                identifier = re.search(r"^  id: (\S+)", block, re.M)
                if not identifier:
                    continue
                sprite_component = re.search(r"^  - type: Sprite\s*$([\s\S]*?)(?=^  - type: |\Z)", block, re.M)
                sprite = ""
                states = []
                if sprite_component:
                    sprite_match = re.search(r"^    sprite: ([^\s#]+)", sprite_component.group(1), re.M)
                    if sprite_match:
                        sprite = sprite_match.group(1)
                    states = re.findall(r"^\s+(?:- )?state:\s+[\"']?([^\s\"',\]]+)", sprite_component.group(1), re.M)
                name = re.search(r"^  name: [\"']?([^\r\n\"']+)", block, re.M)
                parent = re.search(r"^  parent: [\"']?([^\r\n\"']+)", block, re.M)
                abstract = bool(re.search(r"^  abstract: true\s*$", block, re.M))
                components = re.findall(r"^  - type: (\S+)", block, re.M)
                result.append({
                    "id": identifier.group(1), "name": name.group(1) if name else "",
                    "parent": parent.group(1) if parent else "", "abstract": abstract,
                    "sprite": sprite, "states": states, "components": components,
                    "file": str(path.relative_to(ROOT)).replace("\\", "/"),
                })
    # Child prototypes frequently override only the state while inheriting an
    # RSI path, or inherit the entire Sprite component from a base item.
    by_id = {str(proto["id"]): proto for proto in result}
    resolved: dict[str, tuple[str, list[str]]] = {}

    def inherited(identifier: str, visiting: set[str]) -> tuple[str, list[str]]:
        if identifier in resolved:
            return resolved[identifier]
        proto = by_id.get(identifier)
        if proto is None or identifier in visiting:
            return "", []
        visiting = visiting | {identifier}
        parent_sprite = ""
        parent_states: list[str] = []
        for parent in re.findall(r"[A-Za-z][A-Za-z0-9]+", str(proto["parent"])):
            sprite, states = inherited(parent, visiting)
            if sprite:
                parent_sprite, parent_states = sprite, states
                break
        sprite = str(proto["sprite"]) or parent_sprite
        states = list(proto["states"]) or parent_states
        resolved[identifier] = sprite, states
        return sprite, states

    for proto in result:
        sprite, states = inherited(str(proto["id"]), set())
        proto["sprite"] = sprite
        proto["states"] = states
    return result


def run() -> None:
    placeholders, counts = load()
    sprite_matches = json.loads(REPORT.read_text(encoding="utf-8"))["matches"]
    prototypes = entity_prototypes()
    by_sprite: dict[str, list[dict[str, object]]] = defaultdict(list)
    for proto in prototypes:
        sprite = str(proto["sprite"])
        if sprite:
            by_sprite[sprite].append(proto)
    candidate_report = {}
    for identifier in counts:
        candidate_sets = []
        for match in sprite_matches.get(identifier, []):
            sprite = match["sprite"]
            state = match["state"]
            for proto in by_sprite.get(sprite, []):
                if proto["abstract"]:
                    continue
                states = proto["states"]
                if states and state not in states:
                    continue
                candidate_sets.append({"id": proto["id"], "name": proto["name"], "sprite": sprite,
                                       "state": state, "file": proto["file"], "components": proto["components"]})
        if candidate_sets:
            candidate_report[identifier] = candidate_sets
    out = ROOT / ".codex-build/bosenmori_candidate_report.json"
    out.write_text(json.dumps(candidate_report, indent=2) + "\n", encoding="utf-8")
    print(f"{len(prototypes)} entity prototypes; {len(candidate_report)}/{len(counts)} placed Bosen IDs have direct sprite/state prototype candidates")
    print(f"One candidate: {sum(len(items) == 1 for items in candidate_report.values())}")
    for identifier, items in candidate_report.items():
        source = placeholders[identifier]["source"]
        print(f"{identifier} {source}: " + ", ".join(str(item["id"]) for item in items[:8]))
    print(f"Saved {out}")


if __name__ == "__main__":
    run()
