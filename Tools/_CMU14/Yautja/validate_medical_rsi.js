#!/usr/bin/env node

const fs = require("fs");
const path = require("path");

const repoRoot = path.resolve(__dirname, "..", "..", "..");
const manifestPath = path.join(__dirname, "medical_rsi_manifest.json");
const manifest = JSON.parse(fs.readFileSync(manifestPath, "utf8"));
const rsiRoot = path.join(repoRoot, manifest.target);
const metadata = JSON.parse(fs.readFileSync(path.join(rsiRoot, "meta.json"), "utf8"));
const failures = [];

function pngSize(file) {
  const buffer = fs.readFileSync(file);
  if (buffer.toString("ascii", 1, 4) !== "PNG")
    throw new Error(`${file} is not a PNG`);

  return {
    width: buffer.readUInt32BE(16),
    height: buffer.readUInt32BE(20),
  };
}

if (metadata.size.x !== manifest.size[0] || metadata.size.y !== manifest.size[1])
  failures.push(`RSI size is ${metadata.size.x}x${metadata.size.y}, expected ${manifest.size.join("x")}`);

const metadataStates = metadata.states.map(state => state.name);
const manifestStates = manifest.states.map(state => state.name);
if (JSON.stringify(metadataStates) !== JSON.stringify(manifestStates))
  failures.push(`RSI states differ from the CMSS13 manifest: ${metadataStates.join(", ")}`);

for (const expected of manifest.states) {
  const state = metadata.states.find(candidate => candidate.name === expected.name);
  const pngPath = path.join(rsiRoot, `${expected.name}.png`);

  if (!state) {
    failures.push(`${expected.name}: missing from meta.json`);
    continue;
  }
  if (!fs.existsSync(pngPath)) {
    failures.push(`${expected.name}: missing PNG`);
    continue;
  }

  const size = pngSize(pngPath);
  const actualFrames = size.width / manifest.size[0];
  if (size.height !== manifest.size[1] || actualFrames !== expected.frames)
    failures.push(`${expected.name}: PNG is ${size.width}x${size.height}, expected ${expected.frames} frame(s)`);

  const actualDelays = state.delays?.[0] ?? [];
  const expectedDelays = expected.delays ?? [];
  if (JSON.stringify(actualDelays) !== JSON.stringify(expectedDelays))
    failures.push(`${expected.name}: delays differ from the CMSS13 manifest`);

  const sourcePath = path.join(repoRoot, "cmss13-ref-full", "icons", "obj", "items", expected.dmi);
  if (fs.existsSync(path.join(repoRoot, "cmss13-ref-full")) && !fs.existsSync(sourcePath))
    failures.push(`${expected.name}: missing referenced CMSS13 source DMI ${expected.dmi}`);
}

for (const expected of manifest.states)
  console.log(`${expected.name}: ${expected.frames} frame(s), ${expected.dmi}`);

if (failures.length > 0) {
  console.error("Yautja medical RSI validation failed:");
  for (const failure of failures)
    console.error(`- ${failure}`);
  process.exitCode = 1;
}
