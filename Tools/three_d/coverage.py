#!/usr/bin/env python3
"""Compare authored 3D models with Garrison's static art inventory.

Direct references establish a draft or reviewed asset mapping. Inherited matches
are only candidates: a base model can have the wrong size, material or silhouette
for a descendant. No category here proves live renderer or animation coverage.
"""

from __future__ import annotations

import argparse
from collections import Counter, defaultdict
import json
from pathlib import Path

import yaml

ROOT = Path(__file__).resolve().parents[2]
CATEGORIES = ("exact_reviewed", "exact_draft", "inherited_reviewed_candidate", "inherited_draft_candidate", "unmapped")


def presentation_target(prototype):
    components = set(prototype.get('components', []))
    if 'Action' in components:
        return 'existing-ui'
    if 'MobState' in components:
        return 'sprite-character'
    if prototype['id'] in ('CMUFogWallLamentWaterfallMist', 'CMUFogWall_distress_inverted30min'):
        return 'sprite-atmosphere'
    return 'geometry-review'


def read_models(source: Path) -> list[dict]:
    models = []
    seen = set()
    for path in ([source] if source.is_file() else sorted(source.rglob("*.yml"))):
        for model in yaml.load(path.read_text(encoding="utf-8"), Loader=getattr(yaml, "CSafeLoader", yaml.SafeLoader)) or []:
            if model.get("type") != "cmu3DModel":
                continue
            if model["id"] in seen:
                raise ValueError("Duplicate model ID: " + model["id"])
            seen.add(model["id"])
            status = model.get("status", "draft")
            if status not in {"draft", "reviewed"}:
                raise ValueError(f"Invalid review status on {model['id']}: {status}")
            models.append({"id": model["id"], "status": status,
                           "sourcePrototypes": model.get("sourcePrototypes", []),
                           **{key: model[key] for key in ('randomSpritePrototypes', 'randomSpriteLayer', 'referenceState', 'referenceDirection', 'directionalModels') if key in model}})
    return sorted(models, key=lambda model: model["id"])


def calculate_coverage(inventory: dict, models: list[dict]) -> dict:
    if "knownEntityPrototypeIds" not in inventory:
        raise ValueError("Regenerate inventory.json: knownEntityPrototypeIds registry is required")
    known = set(inventory["knownEntityPrototypeIds"])
    on_map = {entry["id"] for entry in inventory["prototypes"]}
    ancestors = {ancestor for entry in inventory["prototypes"] for ancestor in entry.get("ancestors", [])}
    references = []
    random_references = []
    by_reference: dict[str, list[dict]] = defaultdict(list)
    for model in models:
        status = model.get("status", "draft")
        if status not in {"draft", "reviewed"}:
            raise ValueError(f"Invalid review status on {model['id']}: {status}")
        for reference in sorted(set(model.get('randomSpritePrototypes', []))):
            random_references.append({'modelId': model['id'], 'sourcePrototype': reference,
                                      'layer': model.get('randomSpriteLayer'), 'state': model.get('referenceState'),
                                      'status': status, 'location': 'unresolved' if reference not in known else
                                      'direct_on_map' if reference in on_map else 'existing_off_map'})
        for reference in sorted(set(model.get("sourcePrototypes", []))):
            location = ("unresolved" if reference not in known else "direct_on_map" if reference in on_map
                        else "ancestor_on_map" if reference in ancestors else "existing_off_map")
            references.append({"modelId": model["id"], "sourcePrototype": reference,
                               "status": status, "location": location})
            if reference in known:
                by_reference[reference].append({"modelId": model["id"], "sourcePrototype": reference, "status": status})
    prototype_coverage = []
    for prototype in inventory["prototypes"]:
        if prototype["classification"] != "visual":
            continue
        matches = [{**match, "kind": "exact"} for match in by_reference.get(prototype["id"], [])]
        for ancestor in prototype.get("ancestors", []):
            matches.extend({**match, "kind": "inherited_candidate"} for match in by_reference.get(ancestor, []))
        category = "unmapped"
        for candidate in CATEGORIES[:-1]:
            kind = "exact" if candidate.startswith("exact_") else "inherited_candidate"
            status = "reviewed" if "reviewed" in candidate else "draft"
            if any(match["kind"] == kind and match["status"] == status for match in matches):
                category = candidate
                break
        prototype_coverage.append({"id": prototype["id"], "category": category,
                                   "presentationTarget": presentation_target(prototype),
                                   "instanceCount": prototype["instanceCount"],
                                   "instanceCounts": prototype["instanceCounts"], "matches": matches})
    variants = ["combined", *inventory["summary"]["variants"]]
    summary = {}
    for variant in variants:
        counts = {category: {"prototypes": 0, "instances": 0} for category in CATEGORIES}
        for entry in prototype_coverage:
            instances = entry["instanceCount"] if variant == "combined" else entry["instanceCounts"].get(variant, 0)
            if not instances:
                continue
            counts[entry["category"]]["prototypes"] += 1
            counts[entry["category"]]["instances"] += instances
        summary[variant] = {"visualPrototypes": sum(count["prototypes"] for count in counts.values()),
                            "visualInstances": sum(count["instances"] for count in counts.values()),
                            "categories": counts}
    categories = {entry["id"]: entry for entry in prototype_coverage}
    queues = {}
    for queue, excluded in (("unreviewedFamilies", {"exact_reviewed"}),
                            ("unmappedFamilies", set(CATEGORIES) - {"unmapped"})):
        groups = []
        for family in inventory["families"]:
            pending = [categories[prototype_id] for prototype_id in family["prototypeIds"]
                       if prototype_id in categories and categories[prototype_id]["category"] not in excluded]
            if not pending:
                continue
            groups.append({"id": family["id"], "resources": family["resources"],
                           "prototypeIds": [entry["id"] for entry in pending],
                           "instanceCount": sum(entry["instanceCount"] for entry in pending),
                           "instanceCounts": {variant: sum(entry["instanceCounts"].get(variant, 0) for entry in pending)
                                              for variant in variants if variant != "combined"}})
        queues[queue] = sorted(groups, key=lambda entry: (-entry["instanceCount"], entry["id"]))
    # Preserve the complete inventory/mapping totals above. This separate work
    # queue follows the requested presentation: existing UI and sprite actors
    # need integration review, not fabricated solid models of their icons.
    physical = []
    for family in queues['unmappedFamilies']:
        pending = [categories[uid] for uid in family['prototypeIds']
                   if categories[uid]['presentationTarget'] == 'geometry-review']
        if pending:
            physical.append({**family, 'prototypeIds': [p['id'] for p in pending],
                'instanceCount': sum(p['instanceCount'] for p in pending),
                'instanceCounts': {v: sum(p['instanceCounts'].get(v, 0) for p in pending)
                                   for v in variants if v != 'combined'}})
    queues['physicalModelingFamilies'] = sorted(physical,
        key=lambda family: (-family['instanceCounts'].get('redux', 0), -family['instanceCount'], family['id']))
    queues['retainedSpriteOrUiPrototypes'] = [p for p in prototype_coverage if p['presentationTarget'] != 'geometry-review']
    return {"schemaVersion": 1, "scope": "Saved-map visual entity prototypes only; no live renderer coverage claim.",
            "acceptance": "Only an explicit sourcePrototypes reference on a reviewed model counts as reviewed art. "
                          "Inherited mappings remain unapproved candidates even when the parent model is reviewed.",
            "modelCount": len(models), "modelStatusCounts": dict(sorted(Counter(model.get("status", "draft")
                                                                                   for model in models).items())),
            "summary": summary, "referenceMappings": references,
            "randomSpriteMappings": random_references,
            "directionalModelGroups": [{'sourcePrototypes': model['sourcePrototypes'], 'baseModelId': model['id'],
                                        'modelsInRsiOrder': model['directionalModels']}
                                       for model in models if model.get('directionalModels') and model['sourcePrototypes']],
            "unresolvedSourceReferences": sorted({entry["sourcePrototype"] for entry in references + random_references
                                                  if entry["location"] == "unresolved"}),
            "prototypes": prototype_coverage, **queues}


def markdown_summary(coverage: dict) -> str:
    lines = ["# Garrison 3D asset mapping coverage", "",
             "Generated by `python Tools/three_d/coverage.py`; do not edit by hand.", "",
             f"{coverage['modelCount']} authored models: " + ", ".join(
                 f"{count} {status}" for status, count in coverage["modelStatusCounts"].items()) + ".", "",
             coverage["scope"], "", coverage["acceptance"], "",
             "An asset marked draft is unfinished. Sprite comparison sheets support review; "
             "they do not certify silhouette, materials, animation, or engine integration.", "",
             "| Variant | Category | Visual prototypes | Saved instances |", "| --- | --- | ---: | ---: |"]
    for variant, data in coverage["summary"].items():
        for category, counts in data["categories"].items():
            lines.append(f"| {variant} | {category} | {counts['prototypes']:,} | {counts['instances']:,} |")
    locations = Counter(entry["location"] for entry in coverage["referenceMappings"])
    if coverage['randomSpriteMappings']:
        lines.extend(['', f"{len(coverage['randomSpriteMappings'])} conditional RandomSprite state mappings are listed separately in coverage.json. "
                      'They require a known selected state; they do not promote an unknown saved appearance into exact coverage.'])
    if coverage.get('directionalModelGroups'):
        lines.extend(['', f"{len(coverage['directionalModelGroups'])} direction-specific model families are listed separately in coverage.json. "
                      'Alternate directional assemblies do not add duplicate prototype mappings.'])
    retained = coverage.get('retainedSpriteOrUiPrototypes', [])
    if retained:
        lines.extend(['', 'The physical work queue separately retains action icons in the existing UI, characters as sprites, '
            'and the two audited fog sources as atmosphere. These entries remain in the full inventory totals; '
            'their presentation is not marked complete. `physicalModelingFamilies` ranks the remaining geometry work by Redux placements. '
            'Terrain, physical foam walls and loose apparel remain in that queue.', '',
            '| Retained presentation | Types | Redux saved records |', '| --- | ---: | ---: |'])
        for target in sorted({p['presentationTarget'] for p in retained}):
            group = [p for p in retained if p['presentationTarget'] == target]
            lines.append(f"| {target} | {len(group)} | {sum(p['instanceCounts'].get('redux', 0) for p in group)} |")
    lines.extend(["", "## Source reference checks", ""])
    lines.extend(f"- {location}: {count}" for location, count in sorted(locations.items()))
    if coverage["unresolvedSourceReferences"]:
        lines.extend(["", "Unresolved IDs: " + ", ".join(f"`{reference}`" for reference in coverage["unresolvedSourceReferences"])])
    lines.extend(["", "Existing off-map IDs are valid references for assets not placed in these saved maps. "
                  "Their runtime spawn coverage is not measured.", "",
                  "## First 30 unmapped families by frequency", "",
                  "Unmapped means no exact or inherited model candidate. A candidate moves a family into "
                  "review work, not completion. `coverage.json` also retains the full unreviewed queue.", "",
                  "| Rank | Resource(s) | Unmapped prototypes | Instances |", "| ---: | --- | ---: | ---: |"])
    for index, family in enumerate(coverage["unmappedFamilies"][:30], 1):
        references = ", ".join(f"`{reference}`" for reference in family["resources"]) or family["id"]
        lines.append(f"| {index} | {references} | {len(family['prototypeIds'])} | {family['instanceCount']:,} |")
    return "\n".join(lines) + "\n"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inventory", type=Path, default=ROOT / "Tools/three_d/generated/inventory.json")
    parser.add_argument("--models", type=Path, default=ROOT / "Content.CMU/Resources/ThreeD/Prototypes")
    parser.add_argument("--output", type=Path, default=ROOT / "Tools/three_d/generated")
    args = parser.parse_args()
    inventory = json.loads(args.inventory.read_text(encoding="utf-8"))
    coverage = calculate_coverage(inventory, read_models(args.models))
    args.output.mkdir(parents=True, exist_ok=True)
    (args.output / "coverage.json").write_text(json.dumps(coverage, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    (args.output / "coverage.md").write_text(markdown_summary(coverage), encoding="utf-8")
    print(json.dumps({"modelCount": coverage["modelCount"], "summary": coverage["summary"],
                      "unresolvedSourceReferences": coverage["unresolvedSourceReferences"]}, indent=2))


if __name__ == "__main__":
    main()
