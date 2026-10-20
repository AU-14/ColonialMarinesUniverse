// Usage: node validate_glb.cjs [gltf-validator module path] [model directory] [report file]
// Khronos validator: npm package gltf-validator@2.0.0-dev.3.10.
const fs = require('node:fs');
const path = require('node:path');
const validator = require(process.argv[2] || 'gltf-validator');
const root = path.resolve(__dirname, '../..');
const directory = path.resolve(process.argv[3] || path.join(root, 'Content.CMU/Resources/Models/CMU14/Garrison'));

async function main() {
  const files = fs.readdirSync(directory).filter(name => name.endsWith('.glb')).sort();
  if (!files.length) throw new Error(`No GLB assets found in ${directory}`);
  const results = [];
  for (const name of files) {
    const result = await validator.validateBytes(new Uint8Array(fs.readFileSync(path.join(directory, name))), {
      uri: name,
      maxIssues: 100,
    });
    results.push({ file: name, errors: result.issues.numErrors, warnings: result.issues.numWarnings,
      messages: result.issues.messages });
  }
  const errors = results.reduce((sum, result) => sum + result.errors, 0);
  const warnings = results.reduce((sum, result) => sum + result.warnings, 0);
  const report = { validator: validator.version(), files: results.length, errors, warnings, results };
  const destination = process.argv[4] ? path.resolve(process.argv[4]) : path.join(__dirname, 'generated/glb-validation.json');
  fs.mkdirSync(path.dirname(destination), { recursive: true });
  fs.writeFileSync(destination, JSON.stringify(report, null, 2) + '\n');
  console.log(`Khronos glTF validation: ${results.length} assets; ${errors} errors; ${warnings} warnings`);
  if (errors || warnings) {
    for (const result of results.filter(entry => entry.errors || entry.warnings)) console.log(JSON.stringify(result));
    process.exitCode = 1;
  }
}

main().catch(error => { console.error(error); process.exitCode = 1; });
