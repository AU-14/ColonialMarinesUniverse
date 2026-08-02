#!/usr/bin/env node

const fs = require("fs");
const os = require("os");
const path = require("path");

const repoRoot = path.resolve(__dirname, "..", "..", "..");
const sourceRoot = process.env.CMSS13_REPO && fs.existsSync(process.env.CMSS13_REPO)
  ? process.env.CMSS13_REPO
  : path.join(os.tmpdir(), "cmss13-yautja-source");
const sourceDir = path.join(sourceRoot, "maps", "templates", "lazy_templates", "pred");
const outputDir = path.join(repoRoot, "Resources", "Maps", "_CMU14", "HuntingGrounds");

const maps = [
  {
    source: "jungle_moon.dmm",
    outputs: {
      1: {
        file: "jungle_moon.yml",
        name: "Yautja Jungle Moon",
        mapUid: 1,
        gridUid: 2,
        ambientSoundCollection: "CMUYautjaHuntingGroundJungle",
      },
    },
    destinationId: "jungle_moon",
    destinationProto: "CMUYautjaHuntDestinationJungleMoon",
    youngDestinationProto: "CMUYautjaYoungbloodDestinationJungleMoon",
    defaultTile: "FloorGrassJungle",
  },
  {
    source: "desert_moon.dmm",
    outputs: {
      1: {
        file: "desert_moon.yml",
        name: "Yautja Desert Moon Caves",
        mapUid: 1,
        gridUid: 2,
        ambientSoundCollection: "CMUYautjaHuntingGroundCaves",
      },
      2: {
        file: "desert_moon_caves.yml",
        name: "Yautja Desert Moon",
        mapUid: 1,
        gridUid: 2,
        fillSourceAreaSpace: true,
        ambientSoundCollection: "CMUYautjaHuntingGroundDesert",
      },
    },
    destinationId: "desert_moon",
    destinationProto: "CMUYautjaHuntDestinationDesertMoon",
    youngDestinationProto: "CMUYautjaYoungbloodDestinationDesertMoon",
    defaultTile: "FloorDesert",
  },
];

const tileIds = [
  "Space",
  "FloorGrassJungle",
  "FloorDesert",
  "FloorCave",
  "FloorDirt",
  "CMUYautjaTileHunterFloorsHunterRed",
  "CMUYautjaTileHunterFloorsHunterRed2",
  "CMUYautjaTileHunterFloorsHunterRed3",
  "CMUYautjaTileHunterFloorsHunterRed5",
  "CMUYautjaTileHunterFloorsOuterhull",
  "CMUYautjaTileHunterFloorsFloor",
];

const tileIdByName = new Map(tileIds.map((id, index) => [id, index]));
const sourceBounds = { minX: 0, minY: 0, maxX: 112, maxY: 96 };

function parseDefinitions(source) {
  const definitions = new Map();
  let index = 0;

  while (index < source.length) {
    const match = /^"([^"]+)" = \(/m.exec(source.slice(index));
    if (!match)
      break;

    const start = index + match.index;
    const key = match[1];
    let position = start + match[0].length;
    let depth = 1;
    let inString = false;
    const bodyStart = position;

    for (; position < source.length; position++) {
      const char = source[position];
      if (char === "\"" && source[position - 1] !== "\\")
        inString = !inString;

      if (inString)
        continue;

      if (char === "(") {
        depth++;
      } else if (char === ")") {
        depth--;
        if (depth === 0)
          break;
      }
    }

    definitions.set(key, splitEntries(source.slice(bodyStart, position)));
    index = position + 1;
  }

  return definitions;
}

function splitEntries(body) {
  const entries = [];
  let current = "";
  let braces = 0;
  let inString = false;

  for (const char of body) {
    if (char === "\"" && current[current.length - 1] !== "\\")
      inString = !inString;

    if (!inString) {
      if (char === "{")
        braces++;
      else if (char === "}")
        braces--;
      else if (char === "," && braces === 0) {
        if (current.trim())
          entries.push(current.trim());
        current = "";
        continue;
      }
    }

    current += char;
  }

  if (current.trim())
    entries.push(current.trim());

  return entries;
}

function parseCells(source, definitions) {
  const blocks = [];
  const blockRegex = /\((\d+),(\d+),(\d+)\) = \{"\n([^]*?)\n"\}/g;
  let match;

  while ((match = blockRegex.exec(source)) !== null) {
    blocks.push({
      x: Number(match[1]),
      z: Number(match[3]),
      rows: match[4].split("\n"),
    });
  }

  const heights = new Map();
  for (const block of blocks)
    heights.set(block.z, Math.max(heights.get(block.z) ?? 0, block.rows.length));

  const cells = [];
  for (const block of blocks) {
    const height = heights.get(block.z) ?? block.rows.length;
    for (let row = 0; row < block.rows.length; row++) {
      const key = block.rows[row].trim();
      cells.push({
        x: block.x - 1,
        y: height - row - 1,
        z: block.z,
        entries: definitions.get(key) ?? [],
      });
    }
  }

  return cells;
}

function tileFor(entries, fallbackTile) {
  const joined = entries.join("\n");

  if (joined.includes("/turf/open/predship/tile/red5"))
    return "CMUYautjaTileHunterFloorsHunterRed5";
  if (joined.includes("/turf/open/predship/tile/red3"))
    return "CMUYautjaTileHunterFloorsHunterRed3";
  if (joined.includes("/turf/open/predship/tile/red2"))
    return "CMUYautjaTileHunterFloorsHunterRed2";
  if (joined.includes("/turf/open/predship/tile/red"))
    return "CMUYautjaTileHunterFloorsHunterRed";
  if (joined.includes("/turf/open/predship/hull"))
    return "CMUYautjaTileHunterFloorsOuterhull";
  if (joined.includes("/turf/open/predship"))
    return "CMUYautjaTileHunterFloorsFloor";
  if (joined.includes("/turf/open/floor/plating"))
    return "FloorDirt";
  if (joined.includes("/turf/open/auto_turf/strata_grass"))
    return "FloorGrassJungle";
  if (joined.includes("/turf/open/auto_turf/snow") || joined.includes("/turf/open/auto_turf/ice"))
    return "FloorCave";
  if (joined.includes("/turf/open/mars_cave") || joined.includes("/turf/open/mars"))
    return "FloorCave";
  if (joined.includes("/turf/open/gm/river/desert"))
    return "FloorDesert";
  if (joined.includes("/turf/open/auto_turf") || joined.includes("/turf/open/floor"))
    return fallbackTile;
  if (joined.includes("/turf/closed/wall"))
    return joined.includes("desert") ? "FloorDesert" : fallbackTile;

  return "Space";
}

function wallFor(entries) {
  const joined = entries.join("\n");

  if (!joined.includes("/turf/closed/wall"))
    return null;
  // BYOND allows the preserve console and its source wall turf to share a cell.
  // In Robust, the extra WallRock blocks interaction with the dense console.
  if (joined.includes("/obj/structure/machinery/hunt_ground_escape"))
    return null;
  if (joined.includes("/turf/closed/wall/cult/dark_temple"))
    return "RMCWallSandstoneTemple";
  if (joined.includes("/turf/closed/wall/strata_ice"))
    return "RMCWallStrataIce";

  return "WallRock";
}

function entitiesForCell(entries, mapConfig) {
  const entities = [];
  for (const entry of entries) {
    if (entry.startsWith("/obj/effect/landmark/yautja_young_teleport")) {
      entities.push({
        proto: mapConfig.youngDestinationProto,
      });
      entities.push({
        proto: "CMUYautjaYoungbloodSpawn",
        components: [
          "    - type: YautjaHuntSpawnPoint",
          "      kind: Youngblood",
          `      destinationId: ${mapConfig.destinationId}`,
        ],
      });
    } else if (entry.startsWith("/obj/effect/landmark/yautja_teleport")) {
      entities.push({
        proto: mapConfig.destinationProto,
      });
    } else if (entry.startsWith("/obj/effect/landmark/ert_spawns/distress/hunt_spawner")) {
      entities.push({
        proto: "CMUYautjaHuntPreySpawn",
        components: [
          "    - type: YautjaHuntSpawnPoint",
          "      kind: Prey",
          `      destinationId: ${mapConfig.destinationId}`,
        ],
      });
    } else if (entry.startsWith("/obj/item/hunting_trap")) {
      entities.push({ proto: "CMUYautjaHuntingTrap" });
    } else if (entry.startsWith("/obj/item/weapon/harpoon/yautja")) {
      entities.push({ proto: "CMUYautjaHarpoon" });
    } else if (entry.startsWith("/obj/item/weapon/yautja/knife")) {
      entities.push({ proto: "CMUYautjaDuellingKnife" });
    } else if (entry.startsWith("/obj/item/weapon/twohanded/yautja/glaive")) {
      entities.push({ proto: "CMUYautjaWarGlaive" });
    } else if (entry.startsWith("/obj/item/bracer_attachments/wristblades")) {
      entities.push({ proto: "CMUYautjaWristBlades" });
    } else if (entry.startsWith("/obj/item/clothing/mask/gas/yautja")) {
      entities.push({ proto: "CMUYautjaMask" });
    } else if (entry.startsWith("/obj/structure/machinery/door/poddoor/yautja/hunting_grounds")) {
      entities.push({
        proto: "CMUYautjaHuntingGroundPreserveShutter",
        rotation: /\bdir\s*=\s*4\b/.test(entry) ? "1.5707963267948966 rad" : null,
      });
    } else if (entry.startsWith("/obj/structure/machinery/hunt_ground_escape")) {
      entities.push({ proto: "CMUYautjaHuntingGroundEscapeConsole" });
    } else if (entry.startsWith("/obj/structure/blocker/preserve_edge")) {
      entities.push({ proto: "CMUYautjaHuntingGroundPreserveEdge" });
    }
  }

  return entities;
}

function addEntity(grouped, proto, cell, components = null, rotation = null) {
  const key = [proto, rotation, ...(components ?? [])].join("\n");
  const bucket = grouped.get(key) ?? { proto, components, rotation, entries: [] };
  bucket.entries.push({ x: cell.x + 0.5, y: cell.y + 0.5 });
  grouped.set(key, bucket);
}

function encodeChunk(tileValues) {
  const bytesPerTile = 7;
  const buffer = Buffer.alloc(16 * 16 * bytesPerTile);
  for (let i = 0; i < tileValues.length; i++) {
    const offset = i * bytesPerTile;
    buffer.writeInt32LE(tileValues[i], offset);
    buffer.writeUInt8(0, offset + 4);
    buffer.writeUInt8(0, offset + 5);
    buffer.writeUInt8(0, offset + 6);
  }

  return buffer.toString("base64");
}

function buildChunks(grid) {
  const byChunk = new Map();

  for (const [coord, tileId] of grid.entries()) {
    const [x, y] = coord.split(",").map(Number);
    const cx = Math.floor(x / 16);
    const cy = Math.floor(y / 16);
    const lx = x - cx * 16;
    const ly = y - cy * 16;
    const key = `${cx},${cy}`;
    const values = byChunk.get(key) ?? Array(16 * 16).fill(tileIdByName.get("Space"));
    values[lx + ly * 16] = tileId;
    byChunk.set(key, values);
  }

  return [...byChunk.entries()]
    .map(([key, values]) => {
      const [cx, cy] = key.split(",").map(Number);
      return { cx, cy, data: encodeChunk(values) };
    })
    .sort((a, b) => a.cy - b.cy || a.cx - b.cx);
}

function buildMapYaml(output, grid, grouped) {
  const entityCount = 2 + [...grouped.values()].reduce((sum, bucket) => sum + bucket.entries.length, 0);
  const lines = [
    "meta:",
    "  format: 7",
    "  category: Map",
    "  engineVersion: 277.0.0",
    "  forkId: cmu14",
    "  forkVersion: yautja-hunting-grounds-source-port",
    "  time: 2026-05-29T00:00:00.000Z",
    `  entityCount: ${entityCount}`,
    "maps:",
    `- ${output.mapUid}`,
    "grids:",
    `- ${output.gridUid}`,
    "orphans: []",
    "nullspace: []",
    "tilemap:",
  ];

  tileIds.forEach((id, index) => lines.push(`  ${index}: ${id}`));
  lines.push("entities:");
  lines.push("- proto: \"\"");
  lines.push("  entities:");
  lines.push(`  - uid: ${output.mapUid}`);
  lines.push("    components:");
  lines.push("    - type: MetaData");
  lines.push(`      name: ${output.name} Map`);
  lines.push("    - type: Transform");
  lines.push("    - type: Map");
  lines.push("      mapPaused: True");
  lines.push("    - type: GridTree");
  lines.push("    - type: MapLight");
  lines.push("      ambientLightColor: '#FFFFFFFF'");
  if (output.ambientSoundCollection) {
    lines.push("    - type: AmbientSound");
    lines.push("      sound:");
    lines.push(`        collection: ${output.ambientSoundCollection}`);
    lines.push("      range: 512");
    lines.push("      volume: -8");
  }
  lines.push("    - type: Broadphase");
  lines.push("    - type: OccluderTree");
  lines.push(`  - uid: ${output.gridUid}`);
  lines.push("    components:");
  lines.push("    - type: MetaData");
  lines.push(`      name: ${output.name}`);
  lines.push("    - type: Transform");
  lines.push(`      parent: ${output.mapUid}`);
  lines.push("    - type: MapGrid");
  lines.push("      chunks:");

  for (const chunk of buildChunks(grid)) {
    lines.push(`        ${chunk.cx},${chunk.cy}:`);
    lines.push(`          ind: ${chunk.cx},${chunk.cy}`);
    lines.push(`          tiles: ${chunk.data}`);
    lines.push("          version: 7");
  }

  lines.push("    - type: Broadphase");
  lines.push("    - type: Physics");
  lines.push("      bodyStatus: InAir");
  lines.push("      fixedRotation: False");
  lines.push("      bodyType: Dynamic");
  lines.push("    - type: Fixtures");
  lines.push("      fixtures: {}");
  lines.push("    - type: OccluderTree");
  lines.push("    - type: SpreaderGrid");
  lines.push("    - type: ImplicitRoof");
  lines.push("    - type: GridPathfinding");
  lines.push("    - type: Gravity");
  lines.push("      gravityShakeSound: !type:SoundPathSpecifier");
  lines.push("        path: /Audio/Effects/alert.ogg");
  lines.push("      inherent: True");
  lines.push("      enabled: True");
  lines.push("    - type: DecalGrid");
  lines.push("      chunkCollection:");
  lines.push("        version: 2");
  lines.push("        nodes: []");
  lines.push("    - type: MapAtmosphere");
  lines.push("      space: False");
  lines.push("      mixture:");
  lines.push("        volume: 2500");
  lines.push("        immutable: True");
  lines.push("        temperature: 293.15");
  lines.push("        moles:");
  for (let i = 0; i < 12; i++)
    lines.push("        - 0");
  lines.push("    - type: GridAtmosphere");
  lines.push("      version: 2");
  lines.push("      data:");
  lines.push("        chunkSize: 4");
  lines.push("    - type: GasTileOverlay");

  let nextUid = output.mapUid + 2;
  for (const bucket of [...grouped.values()].sort((a, b) => a.proto.localeCompare(b.proto))) {
    lines.push(`- proto: ${bucket.proto}`);
    lines.push("  entities:");
    for (const entry of bucket.entries.sort((a, b) => a.y - b.y || a.x - b.x)) {
      lines.push(`  - uid: ${nextUid++}`);
      lines.push("    components:");
      lines.push("    - type: Transform");
      if (bucket.rotation)
        lines.push(`      rot: ${bucket.rotation}`);
      lines.push(`      pos: ${entry.x},${entry.y}`);
      lines.push(`      parent: ${output.gridUid}`);
      if (bucket.components) {
        for (const componentLine of bucket.components)
          lines.push(componentLine);
      }
    }
  }

  return lines.join("\n") + "\n";
}

function convertMap(mapConfig) {
  const sourcePath = path.join(sourceDir, mapConfig.source);
  if (!fs.existsSync(sourcePath))
    throw new Error(`Missing CMSS13 source map: ${sourcePath}`);

  const source = fs.readFileSync(sourcePath, "utf8");
  const cells = parseCells(source, parseDefinitions(source));
  const grids = new Map();
  const entities = new Map();
  const stats = new Map();

  for (const cell of cells) {
    if (!mapConfig.outputs[cell.z])
      continue;

    const grid = grids.get(cell.z) ?? new Map();
    const grouped = entities.get(cell.z) ?? new Map();
    const output = mapConfig.outputs[cell.z];
    let tile = tileFor(cell.entries, mapConfig.defaultTile);
    if (tile === "Space" && output.fillSourceAreaSpace &&
        cell.x >= sourceBounds.minX && cell.x <= sourceBounds.maxX &&
        cell.y >= sourceBounds.minY && cell.y <= sourceBounds.maxY) {
      tile = mapConfig.defaultTile;
    }
    if (tile !== "Space")
      grid.set(`${cell.x},${cell.y}`, tileIdByName.get(tile));

    const wall = wallFor(cell.entries);
    if (wall)
      addEntity(grouped, wall, cell);

    for (const entity of entitiesForCell(cell.entries, mapConfig))
      addEntity(grouped, entity.proto, cell, entity.components, entity.rotation);

    grids.set(cell.z, grid);
    entities.set(cell.z, grouped);
  }

  for (const [z, output] of Object.entries(mapConfig.outputs)) {
    const zNumber = Number(z);
    const grid = grids.get(zNumber) ?? new Map();
    const grouped = entities.get(zNumber) ?? new Map();
    const target = path.join(outputDir, output.file);
    fs.writeFileSync(target, buildMapYaml(output, grid, grouped));
    stats.set(output.file, {
      tiles: grid.size,
      entities: [...grouped.values()].reduce((sum, bucket) => sum + bucket.entries.length, 0),
    });
  }

  return stats;
}

fs.mkdirSync(outputDir, { recursive: true });
const report = {};
for (const mapConfig of maps) {
  for (const [file, stats] of convertMap(mapConfig))
    report[file] = stats;
}

console.log(`Converted Yautja hunting grounds from ${sourceDir}`);
console.log(JSON.stringify(report, null, 2));
