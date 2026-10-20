"""Replace reviewed Bosenmori placeholders while retaining their sprite offsets."""

from __future__ import annotations

from pathlib import Path
import json
import re

from bosenmori_prototype_audit import ROOT


MAPS = ROOT / "Content.CMU/Resources/Maps/CMU14/BosenmoriBashoMultiZ"
PROTOTYPES = ROOT / "Content.CMU/Resources/Prototypes/CMU14/Maps/Prototypes/Bosenmori/functional_matches.yml"
MANIFEST = ROOT / "Tools/bosenmori_functional_matches.json"


def run() -> None:
    matches = json.loads(MANIFEST.read_text(encoding="utf-8"))
    replacement = {}
    adapter_blocks = [
        "# Functional Bosenmori replacements whose original map art had a pixel offset.\n"
        "# Each parent supplies the item's real interactions and visual states; the\n"
        "# Sprite override retains the original CMSS13 placement offset.\n"
    ]
    for match in matches:
        old = match["source_id"]
        target = match["target"]
        offset = match["offset"]
        visual_state = match.get("visual_state", "")
        if offset or visual_state:
            adapter = old.replace("CMUBosenmoriExact", "CMUBosenmoriFunctional")
            replacement[old] = adapter
            adapter_blocks.append(
                f"\n- type: entity\n"
                f"  parent: {target}\n"
                f"  id: {adapter}\n"
                f"  # CMSS13 source: {match['source']}\n"
                f"  components:\n"
                f"  - type: Sprite\n"
                + (f"    offset: {offset}\n" if offset else "")
                + (f"    state: {visual_state}\n" if visual_state else "")
            )
        else:
            replacement[old] = target

    changed = 0
    updates: dict[Path, str] = {}
    pattern = re.compile(r"(?ms)^- proto: (CMUBosenmoriExact[0-9a-f]{16})\n(.*?)(?=^- proto: |\Z)")
    empty_fixtures = re.compile(r"(?m)^    - type: Fixtures\n      fixtures: \{\}\n")
    for path in sorted(MAPS.glob("*.yml")):
        original = path.read_text(encoding="utf-8")
        def replace(match: re.Match[str]) -> str:
            nonlocal changed
            old = match.group(1)
            if old in replacement:
                changed += 1
                # Placeholder fixtures are empty; keeping their map override
                # would suppress the functional parent's actual collision.
                body = empty_fixtures.sub("", match.group(2))
                return f"- proto: {replacement[old]}\n{body}"
            return match.group(0)
        updated = pattern.sub(replace, original)
        if updated != original:
            updates[path] = updated
    PROTOTYPES.write_text("".join(adapter_blocks), encoding="utf-8", newline="\n")
    for path, updated in updates.items():
        path.write_text(updated, encoding="utf-8", newline="\n")
    print(f"Replaced {changed} map groups in {len(updates)} maps, with {len(adapter_blocks) - 1} offset-preserving adapters")


if __name__ == "__main__":
    run()
