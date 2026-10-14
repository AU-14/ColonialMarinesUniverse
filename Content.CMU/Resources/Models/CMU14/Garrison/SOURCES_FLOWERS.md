# Flower-cluster source drafts

Nine exact prototype mappings cover 39 Redux and 4 classic placements: RMCFlowersbr1/br2/br3, pv1/pv2/pv3 and y1/y3/y4. Each source is a single static 32 by 32 frame from Decals/Flora/flora_flowers.rsi. The original source is already the complete one-layer reference, so no duplicate reference RSI is needed. Model references preserve every original RGBA pixel, including low-alpha ground shadows.

Each model preserves the source-colored head positions at one pixel per 1/32 tile, the original entity pivot and zero Sprite offset. Broad blossoms use intersecting solid rounded petal lobes, small heads use solid buds, and isolated colored tips use small solid buds. Exact source head crops retain their original RGB and alpha; only pixels belonging to other source structures are masked from that separate head crop. These heads have individual stems and low leaves, with real gaps between plants. There is no full-cluster rectangular backing or extruded full sprite.

Head thickness, 0.118–0.205 tile heights, leaning/branching stems, convex hidden petals, leaf thickness and the interpretation of opaque green regions as low foliage are authored inference. The reference proves original colors and arrangement, not these hidden dimensions. Low-alpha dark source pixels are interpreted as drawn ground shadows; they stay in the original reference and do not become solid black foliage. All draft dimensions and colors are recorded in flowers-source-audit.json.

Sprite.noRot is true: model useEntityRotation is false, including saved half-turn entities. Saved transforms are retained; rendering uses zero yaw as the source does. Physics is static and noncolliding. SpriteFade remains an existing source behavior; no growth, wind, consumption, damage-state artwork or animation controller is invented. This is a static geometry batch, not proof of complete live appearance equivalence.

All contexts are inspected with every new flower mapping admitted together, including new-flower neighbors. Contact counts use conservative bounding boxes and can overcount curved petals, stems and transparent cropped corners. Neighbors and saved overrides remain visible in the source audit; no entity positions or other fixture geometry are edited.

Original RSI metadata declares CC-BY-SA-3.0, taken from tgstation at https://github.com/tgstation/tgstation/commit/729d858807905263adab8b5a331c1d8a04982dd3. Exact attribution, source hashes and crop checks are retained in flowers-source-audit.json and flowers-proof.json. Preserve this attribution when distributing derived textures.

Regenerate only these dedicated assets with `python Tools/three_d/author_flowers.py`; compare asset bytes using `--check --skip-context --skip-reviews`. The generator does not run global exports, builds, a game or a server.
