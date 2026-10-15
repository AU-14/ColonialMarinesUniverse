"""Read-only terrain representation experiment; never rewrites a game map.

Only uncustomized, unreferenced CMWallRock records with eight rock neighbors
are eligible. This proves a lossless record encoding, not collision/visibility
equivalence. Vertical exposure must be checked before any runtime migration.
"""
from __future__ import annotations
import argparse
from collections import Counter
from copy import deepcopy
import hashlib
import json
import math
from pathlib import Path
import time
import zlib
import yaml

SIZE = 32

def compact_json(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode()

def scalar_references(value):
    if isinstance(value, dict):
        for child in value.values():
            yield from scalar_references(child)
    elif isinstance(value, list):
        for child in value:
            yield from scalar_references(child)
    elif isinstance(value, str):
        yield value

def locate(record, canonical=True):
    transform = next((c for c in record.get("components", []) if c.get("type") == "Transform"), None)
    if transform is None or "parent" not in transform or "pos" not in transform:
        return None
    try:
        x, y = map(float, transform["pos"].split(","))
    except (ValueError, TypeError):
        return None
    if not math.isfinite(x) or not math.isfinite(y):
        return None
    ix, iy = math.floor(x), math.floor(y)
    if canonical and transform["pos"] != f"{ix + .5:g},{iy + .5:g}":
        return None
    return transform["parent"], ix, iy

def select(document, prototype):
    rocks = {}
    occupied = set()
    references = set()
    rejected = Counter()
    for group in document.get("entities", []):
        for record in group.get("entities", []):
            position = locate(record)
            for component in record.get("components", []):
                if component.get("type") == "Transform":
                    if "parent" in component:
                        references.add(component["parent"])
                else:
                    references.update(scalar_references(component))
            if group.get("proto") != prototype:
                occupied_position = locate(record, canonical=False)
                if occupied_position is not None:
                    occupied.add(occupied_position)
                continue
            if position is None:
                rejected["noncanonical_transform"] += 1
                continue
            if position in rocks:
                occupied.add(position)
                rejected["overlapping_rock"] += 1
            rocks[position] = record

    selected = {}
    for position, record in rocks.items():
        parent, x, y = position
        if record["uid"] in references:
            rejected["referenced"] += 1
        elif position in occupied:
            rejected["occupied"] += 1
        elif (set(record) != {"uid", "components"} or len(record["components"]) != 1
              or record["components"][0].get("type") != "Transform"
              or set(record["components"][0]) - {"type", "parent", "pos", "rot", "anchored"}
              or record["components"][0].get("anchored", "true").lower() != "true"):
            rejected["customized"] += 1
        elif any((parent, x + dx, y + dy) not in rocks
                 for dx in (-1, 0, 1) for dy in (-1, 0, 1) if dx or dy):
            rejected["boundary"] += 1
        else:
            selected[position] = record
    return selected, len(rocks), dict(rejected)

def pack(records, prototype):
    palettes = []
    palette_ids = {}
    chunks = {}
    for (parent, x, y), record in sorted(records.items()):
        template = deepcopy(record)
        del template["uid"]
        transform = template["components"][0]
        del transform["pos"]
        del transform["parent"]
        signature = compact_json(template)
        if signature not in palette_ids:
            palette_ids[signature] = len(palettes)
            palettes.append(template)
        key = (parent, x // SIZE, y // SIZE, palette_ids[signature])
        bit = (y % SIZE) * SIZE + x % SIZE
        chunks.setdefault(key, {})[bit] = record["uid"]

    output = []
    for (parent, x, y, template), cells in sorted(chunks.items()):
        mask = sum(1 << bit for bit in cells)
        output.append({"parent": parent, "x": x, "y": y, "template": template,
                       "mask": mask.to_bytes(SIZE * SIZE // 8, "little").hex(),
                       "uids": [cells[bit] for bit in sorted(cells)]})
    return {"format": 1, "prototype": prototype, "chunk_size": SIZE, "templates": palettes, "chunks": output}

def unpack(packed):
    records = {}
    for chunk in packed["chunks"]:
        bits = int.from_bytes(bytes.fromhex(chunk["mask"]), "little")
        uids = iter(chunk["uids"])
        for bit in range(SIZE * SIZE):
            if not bits & (1 << bit):
                continue
            x = chunk["x"] * SIZE + bit % SIZE
            y = chunk["y"] * SIZE + bit // SIZE
            record = deepcopy(packed["templates"][chunk["template"]])
            record["uid"] = next(uids)
            transform = record["components"][0]
            transform["parent"] = chunk["parent"]
            transform["pos"] = f"{x + .5:g},{y + .5:g}"
            records[(chunk["parent"], x, y)] = record
        if next(uids, None) is not None:
            raise ValueError("Extra entity IDs in packed chunk")
    return records

def audit(path, output, prototype):
    source = path.read_bytes()
    started = time.perf_counter()
    # BaseLoader avoids interpreting arbitrary map tags or executing constructors.
    document = yaml.load(source.decode("utf-8-sig"), Loader=yaml.CBaseLoader)
    selected, rocks, rejected = select(document, prototype)
    packed = pack(selected, prototype)
    payload = compact_json(packed)
    compressed = zlib.compress(payload, level=9)
    decoded = unpack(json.loads(zlib.decompress(compressed)))
    if decoded != selected:
        raise AssertionError("Record round trip differs from source")
    original = compact_json(list(selected.values()))
    output.mkdir(parents=True, exist_ok=True)
    target = output / (path.stem + ".terrain.json.zlib")
    target.write_bytes(compressed)
    result = {"map": str(path.resolve()), "source_sha256": hashlib.sha256(source).hexdigest(),
              "prototype": prototype, "map_entities": int(document["meta"]["entityCount"]),
              "rocks_at_canonical_positions": rocks, "interior_candidate_records": len(selected),
              "rejected": rejected, "chunks": len(packed["chunks"]),
              "original_record_json_bytes": len(original), "packed_json_bytes": len(payload),
              "original_record_zlib_bytes": len(zlib.compress(original, 9)), "packed_zlib_bytes": len(compressed),
              "round_trip_verified": True, "elapsed_seconds": round(time.perf_counter() - started, 3),
              "vertical_exposure_validated": False, "production_map_modified": False,
              "packed_file": str(target.resolve())}
    (output / (path.stem + ".terrain-report.json")).write_text(json.dumps(result, indent=2), encoding="utf-8")
    return result

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("maps", nargs="+", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--prototype", default="CMWallRock")
    args = parser.parse_args()
    for path in args.maps:
        print(json.dumps(audit(path, args.output, args.prototype)), flush=True)

if __name__ == "__main__":
    main()
