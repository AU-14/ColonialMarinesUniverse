# Native scene reference packing

The native first-person encoder now retains every reference to admitted geometry in a cell. The previous 128-primary / 192-total greedy admission could omit an entire source when one tile became crowded, and improving one model could displace an unrelated object. The new layout removes that per-cell admission rule. Source validation and the existing 8,192 normal / 32,768 extended snapshot capacities still apply.

`CMU3DSceneEncoding` groups source parts atomically, quantizes geometry once for footprint membership and CPU picking, and encodes each original part once for the GPU. Prefix offsets describe a shared array of 16-bit box IDs. Invalid parts reject their whole source. The exceptional 4,194,304-reference memory guard rejects the whole snapshot and sets `ReferenceBudgetExceeded`; it never truncates a cell silently.

The box texture remains 256 pixels wide with six texels per box. The grid texture is 1,024 pixels wide, grows in powers of two, and retains its allocation for later smaller snapshots. `CMU3DSceneControl` recreates its owned grid texture when the height grows.

| Grid region | Encoding |
| --- | --- |
| First descriptor texel per cell | RGB: little-endian 24-bit offset into the shared reference array |
| Second descriptor texel per cell | RG: little-endian 16-bit reference count |
| After all two-texel descriptors | Two 16-bit IDs per RGBA texel, packed across cell boundaries |

Odd-length cells can share a texel with the following cell. Shader selection must use the parity of the **global reference offset**, not the local candidate index. The shader reads all references up to the captured snapshot limit; counts above 255 are decoded from both bytes. Empty cells and stale allocation tails are cleared on every build. Geometry shapes, alpha-cutout tests, source order within admitted groups and nearest-hit tie behavior are preserved.

The frozen 867-model audit checks 532 captured camera/pose cases, 13,848,830 references, and the actual C# GPU bytes. Its 584 previously omitted source occurrences across 44 distinct map entities become zero omissions. Peak captured demand is 21,150 boxes, 329 references in one cell, and 54,664 references overall. The grid texture at that peak is 256 KiB, plus approximately 107 KiB of reference IDs and the existing 768 KiB box texture. This evidence covers those captured cases, not arbitrary future scenes or complete gameplay visibility.

`CMU3DCellCapacityTest` covers old boundaries, 255/256 counts, odd packing, full 32,768-entry membership/picking, growth/reset behavior, and the memory guard. `validate_native_shader.py` uses the actual Robust-generated GLSL and actual C# packed pixels in a hidden graphics context, with eleven dense-cell tail-reference fixtures per shader variant. The extreme 8,192/32,768 fixtures use a single pixel; they do not prove frame rate at gameplay resolution.

A dynamic-bound loop experiment terminated the Python graphics validation process in `ntdll.dll` with Windows exception `0xc000070a`. Shader causality is unproven. That experiment was reverted; the retained constant upper bound with an early break passed on two successful offscreen runs. NVIDIA reports that it keeps this loop rather than unrolling 32,768 iterations. Full-resolution performance and other graphics drivers remain unverified. No game or server was launched.

Evidence: `generated/packed-renderer-audit.json`, `generated/packed-native-shader-validation.json`, `.codex/packed-scene-audit/`, and `.codex/packed-grid-validation/`. The historical fixed-capacity reports remain unchanged.

The final 872-model checkpoint adds the reagent carts and twelve corrected door mounts. Its 1,036 cases verify 29,856,374 reference entries with zero omissions; peak demand reaches 449 references in one cell and 21,820 captured parts. All 1,411 sampled tank occurrences are retained. See `generated/reagent-native-budget-audit.json`; these counts include repeated camera views and poses.
