"""Rank exact-art functional counterparts for manual Bosenmori map review."""

from __future__ import annotations

from difflib import SequenceMatcher
from pathlib import Path
import json
import re

from bosenmori_prototype_audit import ROOT, load


STOP = {"obj", "item", "structure", "machinery", "device", "prop", "clothing", "storage", "reagent", "container", "food", "type", "base", "cm", "rmc", "cmu", "au14"}


def tokens(value: str) -> list[str]:
    value = re.sub(r"([a-z0-9])([A-Z])", r"\1 \2", value)
    value = re.sub(r"([A-Z])([A-Z][a-z])", r"\1 \2", value)
    return [part.lower() for part in re.findall(r"[A-Za-z]+|\d+", value) if part.lower() not in STOP]


def score(source: dict[str, str | int], candidate: dict[str, object]) -> float:
    identifier = str(candidate["id"])
    name = str(candidate["name"])
    original_name = str(source["name"])
    source_path = str(source["source"])
    if any(word in identifier for word in ["SpawnPoint", "VendorBundle", "HunterShipPlaced", "Guidebook"]):
        return -1
    if "CMUHunterShip" in identifier and "/huntership" not in source_path:
        return -1
    target_tokens = set(tokens(original_name))
    leaf_tokens = set(tokens(source_path.split("/")[-1]))
    path_tokens = set(tokens("/".join(source_path.split("/")[-3:])))
    candidate_tokens = set(tokens(identifier + " " + name))
    if not candidate_tokens:
        return 0
    overlap_name = len(target_tokens & candidate_tokens) / len(target_tokens) if target_tokens else 0
    overlap_leaf = len(leaf_tokens & candidate_tokens) / len(leaf_tokens) if leaf_tokens else 0
    overlap_path = len(path_tokens & candidate_tokens) / len(path_tokens) if path_tokens else 0
    ratio_name = SequenceMatcher(None, "".join(tokens(original_name)), "".join(tokens(name or identifier))).ratio()
    exact_name = name.casefold() == original_name.casefold() and bool(name)
    result = max(overlap_name * 0.75 + overlap_leaf * 0.2 + overlap_path * 0.05,
                 overlap_leaf * 0.65 + overlap_name * 0.25 + overlap_path * 0.1,
                 ratio_name * 0.6)
    if exact_name:
        result += 0.2
    if "Broken" in identifier and "broken" not in source_path.lower() and "broken" not in original_name.lower():
        result -= 0.25
    if "Ash" in identifier and "ash" not in source_path.lower():
        result -= 0.25
    return round(result, 3)


def run() -> None:
    placeholders, counts = load()
    report = json.loads((ROOT / ".codex-build/bosenmori_candidate_report.json").read_text(encoding="utf-8"))
    result = []
    for identifier, candidates in report.items():
        ranked = sorted(candidates, key=lambda candidate: score(placeholders[identifier], candidate), reverse=True)
        # Duplicate exact matches can occur when a prototype is defined more than once.
        unique = list({str(item["id"]): item for item in reversed(ranked)}.values())
        unique.reverse()
        result.append({"id": identifier, "source": placeholders[identifier]["source"],
                       "name": placeholders[identifier]["name"], "count": counts[identifier],
                       "candidates": [{"id": item["id"], "name": item["name"],
                                       "score": score(placeholders[identifier], item)} for item in unique[:5]]})
    result.sort(key=lambda item: (item["candidates"][0]["score"] if item["candidates"] else 0), reverse=True)
    out = ROOT / ".codex-build/bosenmori_ranked_candidates.json"
    out.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    for item in result:
        ranked = item["candidates"]
        print(f"{ranked[0]['score']:.3f} {item['id']} {item['source']} [{item['name']}]: " + ", ".join(f"{candidate['id']}({candidate['score']})" for candidate in ranked[:3]))


if __name__ == "__main__":
    run()
