#!/usr/bin/env python3
"""Inventory Garrison's saved entities and sprite resources without loading huge map YAML.

Requires PyYAML (``python -m pip install PyYAML==6.0.3``). This is a static art
planning tool, not a replacement for Robust's prototype loader. Component fields
use child/first-parent precedence; component-specific custom inheritance and live
visualizers are deliberately outside its coverage. Run from any directory.
"""

from __future__ import annotations

import argparse
from collections import Counter, defaultdict
from copy import deepcopy
import json
from pathlib import Path
import re
from typing import Any

import yaml


class PrototypeLoader(getattr(yaml, "CSafeLoader", yaml.SafeLoader)):
    """Read Robust's tagged values as inert YAML, without invoking constructors."""


# YAML 1.1's yes/no/on/off coercions corrupt unquoted sprite state names. Robust
# reads those fields as strings; only true/false are booleans in this inventory.
PrototypeLoader.yaml_implicit_resolvers = {
    key: [(tag, pattern) for tag, pattern in resolvers if tag != "tag:yaml.org,2002:bool"]
    for key, resolvers in PrototypeLoader.yaml_implicit_resolvers.items()
}
PrototypeLoader.add_implicit_resolver("tag:yaml.org,2002:bool", re.compile(r"^(?:true|True|TRUE|false|False|FALSE)$"),
                                    list("tTfF"))


def _tagged_value(loader: PrototypeLoader, suffix: str, node: yaml.Node) -> Any:
    if isinstance(node, yaml.MappingNode):
        return {"__type_tag__": suffix, **loader.construct_mapping(node)}
    if isinstance(node, yaml.SequenceNode):
        return loader.construct_sequence(node)
    return loader.construct_scalar(node)


PrototypeLoader.add_multi_constructor("!type:", _tagged_value)


def load_yaml(text: str) -> Any:
    return yaml.load(text, Loader=PrototypeLoader)


def parent_ids(prototype: dict) -> list[str]:
    value = prototype.get("parent", [])
    return [value] if isinstance(value, str) else list(value or [])


def component_map(prototype: dict) -> dict[str, dict]:
    return {entry["type"]: entry for entry in prototype.get("components", []) or []
            if isinstance(entry, dict) and "type" in entry}


class ResolutionError(ValueError):
    pass


class Resolver:
    """Resolve default data-field inheritance and component-registry composition.

    The engine pushes parents in declaration order, filling absent child fields.
    Ordinary component fields (including Sprite.layers) replace, rather than
    concatenate or recursively merge, their corresponding parent fields.
    See SerializationManager.Composition.cs and ComponentRegistrySerializer.cs.
    """

    def __init__(self, prototypes: dict[str, dict]):
        self.prototypes = prototypes
        self.cache: dict[str, dict] = {}

    def resolve(self, prototype_id: str, stack: tuple[str, ...] = ()) -> dict:
        if prototype_id in self.cache:
            return self.cache[prototype_id]
        if prototype_id in stack:
            raise ResolutionError("Inheritance cycle: " + " -> ".join((*stack, prototype_id)))
        if prototype_id not in self.prototypes:
            raise ResolutionError("Missing prototype: " + " -> ".join((*stack, prototype_id)))
        source = self.prototypes[prototype_id]
        result = deepcopy(source)
        components = component_map(result)
        ancestors: list[str] = []
        for parent_id in parent_ids(source):
            parent = self.resolve(parent_id, (*stack, prototype_id))
            for key, value in parent.items():
                if key not in {"id", "type", "abstract", "parent", "components", "_ancestors", "_source"}:
                    result.setdefault(key, deepcopy(value))
            for name, inherited_component in component_map(parent).items():
                component = components.setdefault(name, {})
                for key, value in inherited_component.items():
                    component.setdefault(key, deepcopy(value))
            for ancestor in [parent_id, *parent["_ancestors"]]:
                if ancestor not in ancestors:
                    ancestors.append(ancestor)
        result["components"] = list(components.values())
        result["_ancestors"] = ancestors
        # Abstract is explicitly marked NeverPushInheritance by EntityPrototype.
        result["abstract"] = bool(source.get("abstract", False))
        self.cache[prototype_id] = result
        return result


def load_prototypes(root: Path) -> tuple[dict[str, dict[str, dict]], list[dict], int]:
    kinds: dict[str, dict[str, dict]] = defaultdict(dict)
    issues: list[dict] = []
    file_count = 0
    # Authoring tools need both optional libraries even though gameplay loads them on demand.
    for prototype_root in (root / "Resources/Prototypes", root / "Content.CMU/Resources/Prototypes",
                           root / "Content.CMU/Resources/ThreeD/Prototypes"):
        for path in sorted(prototype_root.rglob("*.yml")):
            file_count += 1
            relative = path.relative_to(root).as_posix()
            try:
                documents = load_yaml(path.read_text(encoding="utf-8-sig"))
            except (yaml.YAMLError, UnicodeError) as error:
                issues.append({"kind": "yaml_error", "source": relative, "message": str(error)})
                continue
            if not isinstance(documents, list):
                continue
            for item in documents:
                if not isinstance(item, dict) or not isinstance(item.get("id"), str):
                    continue
                kind, prototype_id = item.get("type"), item["id"]
                if not isinstance(kind, str):
                    continue
                if prototype_id in kinds[kind]:
                    issues.append({"kind": "duplicate_prototype", "type": kind, "id": prototype_id,
                                   "sources": [kinds[kind][prototype_id]["_source"], relative]})
                item["_source"] = relative
                kinds[kind][prototype_id] = item
    return dict(kinds), issues, file_count


def resource_path(root: Path, reference: str) -> Path | None:
    relative = reference.replace("\\", "/").lstrip("/")
    if ".." in Path(relative).parts:
        return None
    for resource_root in (root / "Content.CMU/Resources", root / "Resources"):
        path = resource_root / relative
        if path.exists():
            return path
    return None


def texture_reference(reference: str) -> str:
    normalized = reference.replace("\\", "/").lstrip("/")
    return "/" + (normalized if normalized.startswith("Textures/") else "Textures/" + normalized)


def scan_map(path: Path) -> dict:
    """Read only format-7 grouping, entity IDs, override counts and tile palette.

    Avoid parsing grid chunks and every entity's components. Report differences
    from meta.entityCount: edited maps can have a stale metadata count.
    """
    counts: Counter[str] = Counter()
    tiles: set[str] = set()
    sprite_overrides: Counter[str] = Counter()
    section = ""
    current_proto: str | None = None
    expected = None
    map_format = None
    with path.open(encoding="utf-8-sig") as stream:
        for line in stream:
            if line.startswith("  format:") and section == "meta":
                map_format = int(line.split(":", 1)[1].strip())
            elif line.startswith("  entityCount:") and section == "meta":
                expected = int(line.split(":", 1)[1].strip())
            if line and not line[0].isspace() and not line.startswith(("-", "#")):
                section = line.split(":", 1)[0].strip()
            if section == "tilemap" and re.match(r"^  \d+: ", line):
                tiles.add(str(load_yaml(line.split(":", 1)[1])))
            elif section == "entities":
                if line.startswith("- proto:"):
                    current_proto = str(load_yaml(line.split(":", 1)[1]) or "")
                elif line.startswith("  - uid:"):
                    if current_proto is None:
                        raise ValueError(f"Ungrouped entity in {path}")
                    counts[current_proto] += 1
                elif re.match(r"^\s*- uid:", line):
                    raise ValueError(f"Unsupported entity indentation in {path}")
                elif line.rstrip() == "    - type: Sprite":
                    sprite_overrides[current_proto or ""] += 1
    if map_format != 7:
        raise ValueError(f"Unsupported map format {map_format}: {path}")
    if expected is None:
        raise ValueError(f"Map is missing entityCount: {path}")
    return {"entityCount": sum(counts.values()), "metadataEntityCount": expected,
            "metadataCountMatches": sum(counts.values()) == expected, "prototypeCounts": dict(sorted(counts.items())),
            "tilePalette": sorted(tiles), "spriteOverrideCounts": dict(sorted(sprite_overrides.items()))}


def sprite_resources(sprite: dict | None) -> list[str]:
    references: set[str] = set()
    if not sprite:
        return []
    for source in [sprite, *(sprite.get("layers", []) or [])]:
        if not isinstance(source, dict):
            continue
        for key in ("sprite", "rsi", "texture"):
            if isinstance(source.get(key), str) and source[key]:
                references.add(texture_reference(source[key]))
    return sorted(references)


def inspect_resource(root: Path, reference: str) -> dict:
    path = resource_path(root, reference)
    result: dict[str, Any] = {"exists": path is not None}
    if path is None:
        return result
    result["source"] = path.relative_to(root).as_posix()
    if path.suffix.lower() != ".rsi":
        return result
    metadata_path = path / "meta.json"
    try:
        metadata = json.loads(metadata_path.read_text(encoding="utf-8-sig"))
    except (OSError, ValueError) as error:
        result["error"] = str(error)
        return result
    result.update({"size": metadata.get("size"), "license": metadata.get("license"),
                   "copyright": metadata.get("copyright"),
                   "states": [{"name": state["name"], "directions": state.get("directions", 1),
                               "animated": any(len(row) > 1 for row in state.get("delays", [])),
                               "imageExists": (path / (state["name"] + ".png")).is_file()}
                              for state in metadata.get("states", [])]})
    return result


def classification(components: dict[str, dict]) -> str:
    if "Marker" in components or "Area" in components:
        return "helper"
    sprite = components.get("Sprite")
    if sprite is None:
        return "no_sprite"
    if sprite.get("visible") is False:
        return "hidden"
    return "visual"


def build_inventory(root: Path) -> dict:
    kinds, issues, file_count = load_prototypes(root)
    entities = Resolver(kinds.get("entity", {}))
    totals: Counter[str] = Counter()
    variants: dict[str, Counter] = {}
    maps: list[dict] = []
    all_tiles: set[str] = set()
    for variant, prototype_id in (("classic", "stablegarrison"), ("redux", "StableGarrisonRedux")):
        definition = kinds["gameMap"][prototype_id]
        variant_counts: Counter[str] = Counter()
        paths = [definition["mapPath"], *definition.get("mapsBelow", []), *definition.get("mapsAbove", [])]
        for reference in paths:
            path = resource_path(root, reference)
            if path is None:
                raise FileNotFoundError(reference)
            scanned = scan_map(path)
            scanned.update({"variant": variant, "path": path.relative_to(root).as_posix()})
            variant_counts.update(scanned["prototypeCounts"])
            all_tiles.update(scanned["tilePalette"])
            maps.append(scanned)
        variants[variant] = variant_counts
        totals.update(variant_counts)
    prototypes: list[dict] = []
    resources: dict[str, dict] = {}
    families: dict[str, dict] = {}
    for prototype_id, count in sorted(totals.items()):
        if not prototype_id:
            continue
        entry = {"id": prototype_id, "instanceCount": count,
                 "instanceCounts": {variant: counts.get(prototype_id, 0) for variant, counts in variants.items()},
                 "issues": []}
        try:
            effective = entities.resolve(prototype_id)
        except ResolutionError as error:
            entry.update({"classification": "unresolved", "sprite": None, "icon": None,
                          "parents": [], "ancestors": [], "resources": [], "issues": [str(error)]})
            prototypes.append(entry)
            continue
        components = component_map(effective)
        sprite = deepcopy(components.get("Sprite"))
        icon = deepcopy(components.get("Icon"))
        if sprite:
            sprite.pop("type", None)
        if icon:
            icon.pop("type", None)
        for appearance in (sprite, icon):
            if not appearance:
                continue
            for layer in [appearance, *(appearance.get("layers", []) or [])]:
                if isinstance(layer, dict) and layer.get("state") is not None:
                    layer["state"] = str(layer["state"])
        references = sprite_resources(sprite)
        entry.update({"source": effective["_source"], "parents": parent_ids(effective),
                      "ancestors": effective["_ancestors"], "classification": classification(components),
                      "sprite": sprite, "icon": icon, "resources": references,
                      "components": sorted(components)})
        for reference in sorted(set(references + sprite_resources(icon))):
            if reference not in resources:
                resources[reference] = inspect_resource(root, reference)
            if not resources[reference]["exists"]:
                entry["issues"].append("Missing resource: " + reference)
        if sprite:
            # Prototype defaults only; state names selected by systems are not inferred.
            for layer in [sprite, *(sprite.get("layers", []) or [])]:
                if not isinstance(layer, dict) or not isinstance(layer.get("state"), str):
                    continue
                reference = layer.get("rsi", layer.get("sprite", sprite.get("sprite")))
                if not isinstance(reference, str):
                    continue
                metadata = resources.get(texture_reference(reference), {})
                if "states" in metadata and layer["state"] not in {state["name"] for state in metadata["states"]}:
                    entry["issues"].append(f"Default state absent from RSI: {reference}::{layer['state']}")
        if entry["classification"] == "visual":
            # Family is a resource grouping, NOT a claim that these entities share one model.
            key = "|".join(references) if references else "dynamic:" + prototype_id
            family = families.setdefault(key, {"id": key, "resources": references, "prototypeIds": [],
                                              "instanceCount": 0, "instanceCounts": {"classic": 0, "redux": 0}})
            family["prototypeIds"].append(prototype_id)
            family["instanceCount"] += count
            for variant in variants:
                family["instanceCounts"][variant] += entry["instanceCounts"][variant]
        prototypes.append(entry)
    tiles: list[dict] = []
    tile_resolver = Resolver(kinds.get("tile", {}))
    for tile_id in sorted(all_tiles):
        try:
            tile = tile_resolver.resolve(tile_id)
            entry = {"id": tile_id, "source": tile["_source"], "sprite": tile.get("sprite"),
                     "edgeSprites": tile.get("edgeSprites", {}), "variants": tile.get("variants", 1),
                     "allowRotationMirror": tile.get("allowRotationMirror", False),
                     "isSubfloor": tile.get("isSubfloor", False)}
            references = [entry["sprite"], *entry["edgeSprites"].values()]
            for reference in references:
                if isinstance(reference, str):
                    normalized = texture_reference(reference)
                    if normalized not in resources:
                        resources[normalized] = inspect_resource(root, normalized)
        except ResolutionError as error:
            entry = {"id": tile_id, "error": str(error)}
        tiles.append(entry)
    ordered_families = sorted(families.values(), key=lambda family: (-family["instanceCount"], family["id"]))
    category_counts = Counter(entry["classification"] for entry in prototypes)
    category_instances = Counter()
    for entry in prototypes:
        category_instances[entry["classification"]] += entry["instanceCount"]
    return {"schemaVersion": 1,
            "scope": {"runtimeSpawnClosureCovered": False, "mapInstanceSpriteOverridesApplied": False,
                      "entityInheritance": "child fields, then first-parent fields; components merged by type",
                      "limitations": [
                          "Saved maps only: excludes runtime vendors, loadouts, threats, construction and spawn closure.",
                          "Saved instance Sprite overrides are counted but not applied to prototype art references.",
                          "Component-specific custom inheritance outside Sprite is not simulated.",
                          "Live visualizers, autotiling, humanoid layers, worn and in-hand art are not resolved.",
                          "Sprite families group resource references, not unique meshes or completed assets.",
                          "Tile palettes can contain unused entries; tile occurrence counts are not measured.",
                          "Anonymous saved entities are counted but their custom sprites are not inventoried."]},
            "summary": {"prototypeFilesRead": file_count, "entityPrototypesIndexed": len(entities.prototypes),
                        "mapFilesRead": len(maps), "namedPrototypeCount": len(prototypes),
                        "savedEntityCount": sum(totals.values()), "anonymousEntityCount": totals.get("", 0),
                        "classificationPrototypeCounts": dict(sorted(category_counts.items())),
                        "classificationInstanceCounts": dict(sorted(category_instances.items())),
                        "spriteFamilyCount": len(families), "tilePaletteCount": len(tiles),
                        "resourceCount": len(resources),
                        "missingResourceCount": sum(not value["exists"] for value in resources.values()),
                        "prototypeLoadIssueCount": len(issues),
                        "mapMetadataCountMismatches": sum(not item["metadataCountMatches"] for item in maps),
                        "variants": {variant: {"namedPrototypeCount": len([key for key in counts if key]),
                                               "savedEntityCount": sum(counts.values())}
                                     for variant, counts in variants.items()}},
            "maps": maps, "prototypes": prototypes, "knownEntityPrototypeIds": sorted(entities.prototypes),
            "families": ordered_families, "tiles": tiles,
            "resources": dict(sorted(resources.items())), "loadIssues": issues}


def markdown_summary(inventory: dict) -> str:
    summary = inventory["summary"]
    lines = ["# Garrison static 3D asset inventory", "",
             "Generated by `python Tools/three_d/inventory.py`; do not edit by hand.", "",
             f"Scanned {summary['mapFilesRead']} currently configured map files and "
             f"{summary['prototypeFilesRead']} prototype files. "
             f"{summary['savedEntityCount']:,} saved entities reference "
             f"{summary['namedPrototypeCount']:,} distinct named entity prototypes.", "",
             "| Variant | Saved entities | Distinct named prototypes |", "| --- | ---: | ---: |"]
    for variant, data in summary["variants"].items():
        lines.append(f"| {variant} | {data['savedEntityCount']:,} | {data['namedPrototypeCount']:,} |")
    lines.extend(["", "| Classification | Prototypes | Instances |", "| --- | ---: | ---: |"])
    for category, count in summary["classificationPrototypeCounts"].items():
        lines.append(f"| {category} | {count:,} | {summary['classificationInstanceCounts'][category]:,} |")
    lines.extend(["", f"Visual prototypes group into **{summary['spriteFamilyCount']:,} sprite resource families**. "
                  "These are modeling work groups, not a measured model count. "
                  f"There are {summary['tilePaletteCount']:,} tile palette entries, "
                  f"{summary['missingResourceCount']} missing resource references and "
                  f"{summary['prototypeLoadIssueCount']} prototype load issues.", "",
                  "## Coverage boundaries", ""])
    lines.extend("- " + limitation for limitation in inventory["scope"]["limitations"])
    overrides = sum(sum(item["spriteOverrideCounts"].values()) for item in inventory["maps"])
    lines.extend([f"- Saved maps contain {overrides:,} explicit Sprite component overrides and "
                  f"{summary['anonymousEntityCount']:,} anonymous entities requiring separate review.", "",
                  "## First 40 art work groups by saved instance frequency", "",
                  "Frequency helps prioritize environment coverage; it does not estimate modeling effort. "
                  "Every group starts unreviewed. Hidden/helper prototypes are excluded from this queue.", "",
                  "| Rank | Sprite resource(s) | Prototypes | Instances |", "| ---: | --- | ---: | ---: |"])
    for index, family in enumerate(inventory["families"][:40], 1):
        references = ", ".join(f"`{resource}`" for resource in family["resources"]) or family["id"]
        lines.append(f"| {index} | {references} | {len(family['prototypeIds'])} | {family['instanceCount']:,} |")
    mismatches = [item for item in inventory["maps"] if not item["metadataCountMatches"]]
    if mismatches:
        lines.extend(["", "## Map metadata discrepancies", "",
                      "Counts above use saved entity records, not stale metadata headers.", ""])
        lines.extend(f"- `{item['path']}`: {item['entityCount']:,} records; header says "
                     f"{item['metadataEntityCount']:,}." for item in mismatches)
    unresolved = [entry for entry in inventory["prototypes"] if entry["classification"] == "unresolved"]
    if unresolved:
        lines.extend(["", "## Unresolved prototypes", ""])
        lines.extend(f"- `{entry['id']}`: {'; '.join(entry['issues'])}" for entry in unresolved)
    if inventory["loadIssues"]:
        lines.extend(["", "## Prototype loading issues", ""])
        lines.extend("- `" + json.dumps(issue, ensure_ascii=False) + "`" for issue in inventory["loadIssues"])
    return "\n".join(lines) + "\n"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--output", type=Path, default=Path(__file__).resolve().parent / "generated")
    args = parser.parse_args()
    inventory = build_inventory(args.root.resolve())
    args.output.mkdir(parents=True, exist_ok=True)
    (args.output / "inventory.json").write_text(json.dumps(inventory, indent=2, ensure_ascii=False) + "\n",
                                               encoding="utf-8")
    (args.output / "inventory.md").write_text(markdown_summary(inventory), encoding="utf-8")
    print(json.dumps(inventory["summary"], indent=2))


if __name__ == "__main__":
    main()
