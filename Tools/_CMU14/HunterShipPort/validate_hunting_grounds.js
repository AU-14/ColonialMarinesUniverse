#!/usr/bin/env node

const fs = require("fs");
const path = require("path");

const repoRoot = path.resolve(__dirname, "..", "..", "..");
const mapRoot = path.join(repoRoot, "Resources", "Maps", "_CMU14", "HuntingGrounds");
const sourceBounds = { minX: 0, minY: 0, maxX: 112, maxY: 96 };

const expectations = [
  { file: "desert_moon.yml", maxSpace: 0, minCave: 1 },
  { file: "desert_moon_caves.yml", maxSpace: 0, minCave: 1 },
  { file: "jungle_moon.yml", maxSpace: 0, minCave: 0 },
];

function parseTileMap(text) {
  const tileMap = new Map();
  let inTileMap = false;

  for (const line of text.split(/\r?\n/)) {
    if (line === "tilemap:") {
      inTileMap = true;
      continue;
    }

    if (inTileMap && line === "entities:")
      break;

    if (inTileMap) {
      const match = /^  (\d+): (\S+)$/.exec(line);
      if (match)
        tileMap.set(Number(match[1]), match[2]);
    }
  }

  return tileMap;
}

function readMap(file) {
  const text = fs.readFileSync(path.join(mapRoot, file), "utf8");
  const tileMap = parseTileMap(text);
  const counts = new Map();
  const insideCounts = new Map();
  let chunks = 0;

  const chunkRegex = /^        (-?\d+),(-?\d+):\r?\n          ind: -?\d+,-?\d+\r?\n          tiles: ([A-Za-z0-9+/=]+)\r?\n          version: 7/gm;
  let match;

  while ((match = chunkRegex.exec(text)) !== null) {
    chunks++;
    const chunkX = Number(match[1]);
    const chunkY = Number(match[2]);
    const bytes = Buffer.from(match[3], "base64");
    if (bytes.length !== 16 * 16 * 7)
      throw new Error(`${file}: chunk ${chunkX},${chunkY} has ${bytes.length} bytes`);

    for (let index = 0; index < 16 * 16; index++) {
      const tileId = bytes.readInt32LE(index * 7);
      if (!tileMap.has(tileId))
        throw new Error(`${file}: chunk ${chunkX},${chunkY} uses unknown tile id ${tileId}`);

      counts.set(tileId, (counts.get(tileId) ?? 0) + 1);

      const x = chunkX * 16 + (index % 16);
      const y = chunkY * 16 + Math.floor(index / 16);
      if (x < sourceBounds.minX || x > sourceBounds.maxX ||
          y < sourceBounds.minY || y > sourceBounds.maxY)
        continue;

      insideCounts.set(tileId, (insideCounts.get(tileId) ?? 0) + 1);
    }
  }

  if (chunks !== 56)
    throw new Error(`${file}: expected 56 chunks, found ${chunks}`);

  return {
    tileMap,
    counts,
    insideCounts,
    spaceId: [...tileMap.entries()].find(([, name]) => name === "Space")?.[0],
    caveId: [...tileMap.entries()].find(([, name]) => name === "FloorCave")?.[0],
  };
}

const failures = [];
for (const expectation of expectations) {
  const result = readMap(expectation.file);
  const space = result.insideCounts.get(result.spaceId) ?? 0;
  const cave = result.insideCounts.get(result.caveId) ?? 0;
  const summary = `${expectation.file}: source-area space=${space}, cave=${cave}`;

  if (expectation.maxSpace !== undefined && space > expectation.maxSpace)
    failures.push(`${summary}; expected space <= ${expectation.maxSpace}`);
  if (expectation.exactSpace !== undefined && space !== expectation.exactSpace)
    failures.push(`${summary}; expected space == ${expectation.exactSpace}`);
  if (cave < expectation.minCave)
    failures.push(`${summary}; expected cave >= ${expectation.minCave}`);

  console.log(summary);
}

if (failures.length > 0) {
  console.error("Hunting-ground validation failed:");
  for (const failure of failures)
    console.error(`- ${failure}`);
  process.exitCode = 1;
}
