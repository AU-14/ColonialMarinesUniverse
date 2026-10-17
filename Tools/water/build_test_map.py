"""Rebuild the self-contained water playground. Requires PyYAML; no imported map assets."""

import base64
import struct
from pathlib import Path

import yaml


class MapDumper(yaml.SafeDumper):
    def ignore_aliases(self, data):
        return True

ROOT = Path(__file__).resolve().parents[2]
DEST = ROOT / "Content.CMU/Resources/Maps/CMU14/Test/water.yml"
groups = {}
tiles = {(x, y): 1 for x in range(-26, 27) for y in range(-22, 18)}
tilemap = {0: "Space", 1: "FloorSteel", 2: "FloorAsteroidSand", 3: "RMCPlanetDesertWaterShore0"}
next_uid = 3


def comp(kind, **fields):
    return {"type": kind, **fields}


def spawn(proto, x, y, *components):
    global next_uid
    transform = comp("Transform", parent=2, pos=f"{x + .5},{y + .5}")
    extra = []
    for component in components:
        if component["type"] == "Transform":
            transform.update(component)
        else:
            extra.append(component)
    groups.setdefault(proto, []).append({
        "uid": next_uid,
        "components": [transform, *extra],
    })
    next_uid += 1


def label(text, x, y, description):
    spawn("CMWetSign", x, y, comp("MetaData", name=text, desc=description))


def pool(proto, x, y, width, height, *components):
    for dx in range(width):
        for dy in range(height):
            tiles[x + dx, y + dy] = 2
            if proto:
                spawn(proto, x + dx, y + dy, *components)


def subject(proto, x, y, name, resting=False, dead=False):
    spawn(proto, x, y, comp("MetaData", name=name),
          comp("WaterTestSubject", resting=resting, dead=dead))


# Five equal pools make depth transitions, all wake sizes and footstep sounds easy to compare.
for x, depth in zip((-16, -9, -2, 5, 12), (2, 4, 8, 12, 18)):
    proto = "CMFloorDeepWaterEntity" if depth == 18 else "CMFloorShallowWaterEntity"
    pool(proto, x, 2, 5, 12, comp("RMCWater", depth=depth))
    label(f"DEPTH {depth} - WALK / RUN / REST", x + 2, 0,
          "Walk to suppress footsteps; run for wading sounds and moving wakes. Rest to compare submersion.")
    subject("CMUWaterTestHuman", x + 1, 4, f"Marine - depth {depth}")
    subject("CMXenoDrone", x + 1, 7, f"Drone - depth {depth}")
    subject("CMXenoQueen", x + 2, 11, f"Queen - depth {depth}")

# Bodies available for admin possession, walking, pouncing, resting and cloak testing.
samples = [
    ("CMUWaterTestHuman", "Marine"), ("CMXenoLarva", "Larva"),
    ("CMXenoParasite", "Facehugger"), ("CMXenoRunner", "Runner"),
    ("CMXenoLurker", "Lurker - pounce and camouflage"),
    ("CMUMobYautja", "Yautja - cloak and water wakes"),
]
for index, (proto, name) in enumerate(samples):
    subject(proto, -22, -1 + index * 3, name)
label("BODY SELECTION", -23, -3,
      "As local admin, right-click a specimen and use Debug > Control mob. Try the same depth lanes with each body.")

# Actual production art and colors, including transparent water and shoreline tile fallback.
variants = [
    (-22, "RMCFloorShallowWaterEntityRed", "RED WATER"),
    (-14, "RMCFloorShallowWaterEntityDarkRed", "DARK RED WATER"),
    (-6, "RMCRiverSorokyne", "SOROKYNE RIVER"),
    (2, "CMUEntityShepDesertWaterDeep", "TRANSLUCENT DEEP WATER"),
    (10, "RMCEntityDesertWaterShallow", "DESERT SLOPE - 12"),
    (18, None, "SHORE TILES - 2"),
]
for x, proto, title in variants:
    overrides = (comp("Transform", anchored=False),) if proto == "CMUEntityShepDesertWaterDeep" else ()
    pool(proto, x, -10, 5, 5, *overrides)
    if proto is None:
        for dx in range(5):
            for dy in range(5):
                tiles[x + dx, -10 + dy] = 3
    label(title, x + 2, -4, "Compare the animated water texture and color on bodies, clothing and held items.")
    subject("CMUWaterTestHuman", x + 2, -8, title.lower() + " marine")

# Art-specific edge cases on a dry comparison strip.
for index, proto in enumerate(("RMCEntityDesertWaterShallowCornerEdge", "RMCEntityDesertWaterShallowEdge",
                               "RMCEntityDesertWaterShallowCorner", "AUEntityShepBeachCornerEdge",
                               "AUEntityShepBeachEdge", "AUEntityShepBeachCorner")):
    pool(proto, 20, index * 2, 3, 1)
label("DESERT / SHEPHERD EDGES", 21, -2, "Production edge and corner variants at depths 2 and 4.")

pool("CMFloorDeepWaterEntity", -22, -20, 10, 6)
for dx in range(10):
    spawn("CMCatwalk", -22 + dx, -17)
for dy in range(6):
    spawn("RMCGrate", -19, -20 + dy)
spawn("RMCWaterCover", -15, -19)
spawn("CMChair", -14, -16)
spawn("CMRollerBed", -14, -19)
subject("CMUWaterTestHuman", -24, -17, "Carry / buckle test marine", resting=True)
label("CATWALK / GRATE / CARRY / BUCKLE", -18, -13,
      "Walk across the bridge, then step into the water. Buckle to the chair or roller bed; carry the resting marine.")

pool("CMFloorDeepWaterEntity", -8, -20, 10, 6)
subject("CMUWaterTestHuman", -6, -17, "Living submerged marine - bubbles", resting=True)
subject("CMUWaterTestHuman", -2, -17, "Dead submerged marine - no bubbles", dead=True)
subject("CMXenoParasite", 0, -19, "Submerged facehugger - bubbles")
label("LIVING / DEAD SUBMERSION", -4, -13, "Resting living bodies and facehuggers bubble. Dead bodies do not.")

pool("CMFloorShallowWaterEntity", 5, -20, 6, 6)
subject("CMUWaterTestHuman", 6, -17, "Resting marine - shallow wake", resting=True)
subject("CMXenoLarva", 9, -17, "Resting larva - submerged", resting=True)
subject("CMXenoDrone", 8, -19, "Resting drone - shallow overlay suppressed", resting=True)
label("SHALLOW RESTING POSES", 8, -13, "Humans use prone wakes; resting larvae submerge; resting larger xenos lose the standing overlay.")

pool("CMFloorDeepWaterEntity", 15, -20, 9, 6)
subject("CMXenoLurker", 19, -12, "Pounce tester")
for x, proto in ((15, "CMBootsBlack"), (17, "JumpsuitMarine"), (21, "CMArmorM3Medium"), (23, "CMWarningCone")):
    spawn(proto, x, -12)
label("THROW / POUNCE SPLASHES", 19, -13, "Throw items into the pool or control the lurker and pounce into it. Airborne bodies should not sink.")

spawn("SpawnPointLatejoin", 0, -2)
spawn("CMPaper", 1, -2, comp("MetaData", name="Water test arena guide"), comp("Paper", content=(
    "WATER EFFECTS TEST ARENA\n\n"
    "NORTH: depth 2 / 4 / 8 / 12 / 18 lanes, with marines, drones and queens.\n"
    "WEST: bodies to control (marine, larva, facehugger, runner, lurker, Yautja).\n"
    "MIDDLE SOUTH: red, dark red, river, transparent, desert and shore water.\n"
    "FAR SOUTH: bridge, grate, carry, buckle, live/dead bubbles, resting poses, throws and pounces.\n\n"
    "Examine the yellow signs for instructions. You are a local admin in Sandbox.\n"
    "Right-click a body and use Debug > Control mob. Use the rest action/key binding to lie down.\n"
    "Hold your walk modifier to compare silent walking with running.\n"
    "Use the Yautja's cloak or lurker's camouflage to verify visible wakes.\n"
    "Samples and the initial player have godmode to stay available for testing."
)))
for x in range(-26, 27):
    spawn("CMWallMetal", x, -22)
    spawn("CMWallMetal", x, 17)
for y in range(-21, 17):
    spawn("CMWallMetal", -26, y)
    spawn("CMWallMetal", 26, y)

chunks = {}
for cx, cy in sorted({(x // 16, y // 16) for x, y in tiles}):
    data = b"".join(struct.pack("<iBBB", tiles.get((cx * 16 + x, cy * 16 + y), 0), 0, 0, 0)
                    for y in range(16) for x in range(16))
    chunks[f"{cx},{cy}"] = {"ind": f"{cx},{cy}", "tiles": base64.b64encode(data).decode(), "version": 7}

air = {"volume": 2500, "temperature": 293.15, "moles": {"Oxygen": 21.82, "Nitrogen": 82.08}}
groups[""] = [
    {"uid": 1, "components": [comp("Transform"), comp("Map", mapPaused=True, lightingEnabled=True),
                              comp("MapAtmosphere", space=False, mixture=air),
                              comp("MapLight", ambientLightColor="#C8D4E0")]},
    {"uid": 2, "components": [comp("Transform", parent=1), comp("MapGrid", chunks=chunks),
                              comp("Physics", bodyType="Static"), comp("Fixtures", fixtures={}), comp("Gravity"),
                              comp("Roof", data={}),
                              comp("GridAtmosphere", version=1, tiles={f"{x},{y}": 0 for x, y in tiles}, uniqueMixes=[air]),
                              comp("BecomesStation", id="CMUWaterTest"), comp("WaterTestMap")]},
]
document = {
    "meta": {"format": 7, "category": "Map", "engineVersion": "278.0.0", "forkId": "CMU",
             "forkVersion": "", "time": "09/26/2026 12:00:00", "entityCount": next_uid - 1},
    "maps": [1], "grids": [2], "orphans": [], "nullspace": [], "tilemap": tilemap,
    "entities": [{"proto": proto, "entities": entities} for proto, entities in groups.items()],
}
DEST.parent.mkdir(parents=True, exist_ok=True)
DEST.write_text("# Generated by Tools/water/build_test_map.py\n" + yaml.dump(document, Dumper=MapDumper, sort_keys=False), encoding="utf-8")
print(f"Wrote {DEST}: {next_uid - 1} entities, {len(tiles)} floor tiles.")
