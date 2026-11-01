"""Reproduce nine placed Garrison flower clusters; dedicated assets only."""
from collections import Counter
from copy import deepcopy
from io import BytesIO
from pathlib import Path
import argparse
import hashlib
import json
import math
import sys

import numpy as np
from PIL import Image, ImageDraw
import yaml

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'Tools/three_d'))
import build_models as bm
import inventory
import surfaces
from author_wide_machinery import world_parts, contacts

PROTOTYPES = ['RMCFlowers' + suffix for suffix in ('br1', 'br2', 'br3', 'pv1', 'pv2', 'pv3', 'y1', 'y3', 'y4')]
SOURCE = ROOT / 'Resources/Textures/Decals/Flora/flora_flowers.rsi'
SOURCE_PROTOTYPES = ROOT / 'Resources/Prototypes/_RMC14/Entities/Objects/Misc/bushes.yml'
CONTEXT_BASELINE = ROOT / '.codex/model-batch-baseline916'
MODEL = ROOT / 'Content.CMU/Resources/ThreeD/Prototypes/World/garrison_flowers.yml'
ART = MODEL.with_name('garrison_flowers_art.yml')
TEXTURES = ROOT / 'Content.CMU/Resources/Textures/CMU14/ThreeD/Surfaces'
NOTE = ROOT / 'Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_FLOWERS.md'
GEN = ROOT / 'Tools/three_d/generated'
REVIEW = GEN / 'review/flowers'
CHECK = False
WRITTEN = []


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def png_bytes(image):
    out = BytesIO()
    image.save(out, format='PNG')
    return out.getvalue()


def write(path, data):
    if CHECK:
        assert path.is_file() and path.read_bytes() == data, f'Generated asset differs: {path}'
    else:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
    WRITTEN.append(path)


def json_write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')


def box(points):
    return [min(p[0] for p in points), min(p[1] for p in points),
            max(p[0] for p in points) + 1, max(p[1] for p in points) + 1]


def components(mask):
    todo = {(int(x), int(y)) for y, x in np.argwhere(mask)}
    result = []
    while todo:
        queue = [min(todo, key=lambda p: (p[1], p[0]))]
        todo.remove(queue[0])
        group = []
        while queue:
            x, y = queue.pop()
            group.append((x, y))
            neighbors = {(x + dx, y + dy) for dx in (-1, 0, 1) for dy in (-1, 0, 1)} & todo
            queue.extend(sorted(neighbors))
            todo -= neighbors
        result.append(sorted(group, key=lambda p: (p[1], p[0])))
    return result


def split_touching_blooms(group):
    """Separate two adjacent source heads without replacing their pixel arrangement."""
    x0, y0, x1, y1 = box(group)
    if x1 - x0 <= 6 and y1 - y0 <= 5:
        return [group]
    axis = 0 if (x1 - x0) / 6 > (y1 - y0) / 5 else 1
    low, high = (x0, x1) if axis == 0 else (y0, y1)
    middle = (low + high) // 2
    cut = min(range(max(low + 1, middle - 1), min(high, middle + 2)),
              key=lambda at: (sum(p[axis] == at for p in group), abs(at - middle)))
    first, second = [p for p in group if p[axis] < cut], [p for p in group if p[axis] >= cut]
    assert first and second
    return split_touching_blooms(first) + split_touching_blooms(second)


def color(rgb):
    return '#' + ''.join(f'{int(value):02X}' for value in rgb[:3])


def current_components():
    rows = {p['id']: p for p in inventory.load_yaml(SOURCE_PROTOTYPES.read_text()) if p.get('type') == 'entity'}
    def resolve(uid):
        row = rows[uid]
        result = {}
        parents = row.get('parent', [])
        for parent in [parents] if isinstance(parents, str) else parents:
            result.update(deepcopy(resolve(parent)))
        for component in row.get('components', []):
            result.setdefault(component['type'], {}).update(deepcopy(component))
        return result
    return {uid: resolve(uid) for uid in PROTOTYPES}


class Pool:
    def __init__(self):
        self.entries, self.proofs, self.cache = [], [], {}
        paths = [p for p in MODEL.parent.glob('*.yml') if p != ART]
        for folder in (ROOT / '.codex').glob('*staged*'):
            paths.extend(folder.rglob('*.yml'))
        for path in paths:
            for row in yaml.load(path.read_text(encoding='utf-8-sig'), Loader=yaml.CSafeLoader) or []:
                if row.get('type') == 'cmu3DSurface':
                    assert not 1720 <= row['atlasIndex'] <= 1799, f'Flower atlas reservation conflict: {path}'

    def bloom(self, state, image, points):
        rect = box(points)
        data = np.array(image.crop(rect))
        keep = np.zeros(data.shape[:2], dtype=bool)
        for x, y in points:
            keep[y - rect[1], x - rect[0]] = True
        data[:, :, 3][~keep] = 0
        actual = Image.fromarray(data)
        key = hashlib.sha256(str(actual.size).encode() + actual.tobytes()).hexdigest()
        if key not in self.cache:
            index = 1720 + len(self.entries)
            assert index <= 1799
            uid = f'CMU3DFlowerSurface{index}'
            path = TEXTURES / (uid + '.png')
            write(path, png_bytes(actual))
            self.cache[key] = uid
            self.entries.append({'type': 'cmu3DSurface', 'id': uid, 'atlasIndex': index,
                                 'texture': f'/Textures/CMU14/ThreeD/Surfaces/{uid}.png'})
        uid = self.cache[key]
        stored = Image.open(TEXTURES / (uid + '.png')).convert('RGBA')
        assert stored.size == actual.size and stored.tobytes() == actual.tobytes()
        original = np.array(image.crop(rect))
        assert np.array_equal(np.array(stored)[keep], original[keep])
        self.proofs.append({'state': state, 'rect': rect, 'surface': uid,
                            'retainedPixels': len(points), 'retainedRgbaExact': True,
                            'mask': 'Only this source bloom; other original pixels remain in the full reference.',
                            'rgbaSha256': hashlib.sha256(stored.tobytes()).hexdigest()})
        return uid


def geometry(state, image, pool):
    a = np.array(image)
    r, g, b = (a[:, :, n].astype(int) for n in range(3))
    petal = (a[:, :, 3] == 255) & (((r > 100) & (g < r * 1.2)) | ((b > 120) & (b > g * 1.05)))
    greenery = (a[:, :, 3] == 255) & ~petal
    green_groups = components(greenery)
    blooms = [part for group in components(petal) for part in split_touching_blooms(group)]
    blooms.sort(key=lambda ps: (box(ps)[1], box(ps)[0]))
    assert sum(map(len, blooms)) == int(petal.sum())
    roots, parts, bloom_records = [], [], []

    def solid(label, center, size, tint, shape='Ellipsoid', yaw=0, pitch=0):
        part = {'label': label, 'min': [c - s / 2 for c, s in zip(center, size)],
                'max': [c + s / 2 for c, s in zip(center, size)], 'color': tint}
        if shape != 'Box':
            part['shape'] = shape
        if yaw:
            part['yaw'] = yaw
        if pitch:
            part['pitch'] = pitch
        parts.append(part)
        return part

    def stem(label, start, end, tint):
        delta = np.array(end) - start
        length = float(np.linalg.norm(delta))
        yaw = math.degrees(math.atan2(delta[1], delta[0]))
        # The content solid convention rotates +Z toward -X for positive pitch.
        pitch = -math.degrees(math.atan2(math.hypot(delta[0], delta[1]), delta[2]))
        solid(label, (np.array(start) + end) / 2, [.014, .014, length], tint, 'CylinderZ', yaw, pitch)
        angle, tilt = math.radians(yaw), math.radians(pitch)
        axis = np.array([-math.cos(angle) * math.sin(tilt), -math.sin(angle) * math.sin(tilt), math.cos(tilt)])
        assert np.allclose(np.array(start) + axis * length, end, atol=1e-10), 'Stem does not reach its source head'

    for index, group in enumerate(green_groups):
        x0, y0, x1, y1 = box(group)
        bottom = [p for p in group if p[1] == y1 - 1]
        root = [(sum(p[0] + .5 for p in bottom) / len(bottom) - 16) / 32, (16 - (y1 - .5)) / 32, 0]
        palette = Counter(tuple(int(v) for v in a[y, x, :3]) for x, y in group)
        dark = min(palette, key=lambda v: sum(v))
        bright = max(palette, key=lambda v: (sum(v), palette[v]))
        roots.append({'group': group, 'point': root, 'stemColor': color(dark)})
        # Low leaves occupy the original source foliage region. Their thickness,
        # convex undersides and vertical spacing are explicitly inferred.
        cx, cy = ((x0 + x1) / 2 - 16) / 32, (16 - (y0 + y1) / 2) / 32
        solid(f'foliage {index + 1} low leaf', [cx, cy, .043],
              [max(.022, (x1 - x0) / 32 * .42), (y1 - y0) / 32 * .72, .020], color(dark), yaw=-25)
        if len(group) >= 5:
            solid(f'foliage {index + 1} raised leaf', [cx, cy + .012, .065],
                  [max(.022, (x1 - x0) / 32 * .72), max(.018, (y1 - y0) / 32 * .25), .018], color(bright), yaw=25)

    for index, group in enumerate(blooms):
        x0, y0, x1, y1 = box(group)
        cx, cy = ((x0 + x1) / 2 - 16) / 32, (16 - (y0 + y1) / 2) / 32
        root = min(roots, key=lambda entry: min((x - gx) ** 2 + (y - gy) ** 2 for x, y in group for gx, gy in entry['group']))
        # One pixel bud tips and broad blossoms retain different proportions.
        height = .11 + min(.095, len(group) * .008)
        palette = Counter(tuple(int(v) for v in a[y, x, :3]) for x, y in group)
        main = max(palette, key=lambda value: (palette[value], sum(value)))
        width, depth = (x1 - x0) / 32, (y1 - y0) / 32
        stem(f'bloom {index + 1} branching stem', root['point'], [cx, cy, height], root['stemColor'])
        if len(group) == 1:
            solid(f'bloom {index + 1} single colored bud', [cx, cy, height], [width, depth, .028], color(main))
        else:
            if len(group) >= 5:
                solid(f'bloom {index + 1} broad petal lobes', [cx, cy, height], [width, depth * .47, .032], color(main))
                solid(f'bloom {index + 1} crossing petal lobes', [cx, cy, height], [width * .47, depth, .035], color(main))
            else:
                solid(f'bloom {index + 1} rounded bud', [cx, cy, height], [width, depth, .033], color(main))
            part = solid(f'bloom {index + 1} original petal colors', [cx, cy, height + .019], [width, depth, .002], '#FFFFFF', 'Box')
            part.update(surface=pool.bloom(state, image, group), surfaceAxis='XY')
        bloom_records.append({'sourcePixelBounds': [x0, y0, x1, y1], 'sourcePixels': len(group),
                              'headCenterXY': [cx, cy], 'inferredHeight': height, 'root': root['point']})
    assert len(parts) <= 128
    return parts, {'solidPartCount': len(parts), 'sourceColoredPixels': int(petal.sum()),
                   'sourceGreenPixels': int(greenery.sum()), 'lowAlphaShadowPixels': int(((a[:, :, 3] > 0) & (a[:, :, 3] < 255)).sum()),
                   'foliageGroups': len(roots), 'bloomAndBudGroups': len(blooms), 'blooms': bloom_records}


def configure_surfaces(pool):
    registry = {}
    for path in sorted(MODEL.parent.glob('*.yml')):
        for entry in yaml.load(path.read_text(encoding='utf-8-sig'), Loader=yaml.CSafeLoader) or []:
            if entry.get('type') == 'cmu3DSurface':
                image = Image.open(surfaces.texture_path(entry['texture'])).convert('RGBA')
                registry[entry['id']] = {**entry, 'image': image}
    for entry in pool.entries:
        registry[entry['id']] = {**entry, 'image': Image.open(TEXTURES / (entry['id'] + '.png')).convert('RGBA')}
    surfaces.load_surfaces = lambda: registry


def saved_records(path):
    prototype, block, result = None, [], {}
    def finish():
        if block:
            value = inventory.load_yaml(''.join(block))[0]
            result[value['uid']] = {'prototype': prototype, **value}
    with path.open(encoding='utf-8-sig') as stream:
        for line in stream:
            if line.startswith('- proto:'):
                finish()
                block = []
                prototype = line.split(':', 1)[1].strip().strip('"\'')
            elif prototype in PROTOTYPES:
                if line.startswith('  - uid:'):
                    finish()
                    block = [line]
                elif block:
                    block.append(line)
    finish()
    return result


def contexts(models, render=True):
    by_proto = {p: m for m in models for p in m['sourcePrototypes']}
    model_path, manifest = CONTEXT_BASELINE / 'models.json', CONTEXT_BASELINE / 'scenes.json'
    index = {m['id']: m for m in json.loads(model_path.read_text())['models']}
    index.update({m['id']: m for m in models})
    records, hashes, rendered = [], {str(p.relative_to(ROOT)).replace('\\', '/'): sha(p) for p in (model_path, manifest)}, 0
    for spec in json.loads(manifest.read_text()):
        path = CONTEXT_BASELINE / spec['file']
        document = json.loads(path.read_text())
        flowers = [e for e in document['instances'] if e['prototype'] in by_proto]
        if not flowers:
            continue
        map_path = ROOT / document['map']['path']
        raw = saved_records(map_path)
        hashes[path.relative_to(ROOT).as_posix()] = sha(path)
        hashes[map_path.relative_to(ROOT).as_posix()] = sha(map_path)
        # Admit all nine new types before neighbor/contact inspection.
        for item in flowers:
            item.update(modelId=by_proto[item['prototype']]['id'], renderYaw=0, matchKind='exact')
        for item in flowers:
            overrides = {c['type']: c for c in raw[item['id']].get('components', []) if c['type'] != 'Transform'}
            assert not overrides.get('Sprite'), 'Saved Sprite needs separate source review'
            model = by_proto[item['prototype']]
            own = world_parts(model['parts'], item['position'], 0)
            nearby, picture = [], deepcopy(own)
            for other in document['instances']:
                if other['id'] == item['id'] or other['position'][2] != item['position'][2]:
                    continue
                if max(abs(other['position'][axis] - item['position'][axis]) for axis in (0, 1)) > 1.2:
                    continue
                target = index.get(other.get('modelId'))
                entry = {'id': other['id'], 'prototype': other['prototype'], 'position': other['position'],
                         'yaw': other['yaw'], 'modelId': other.get('modelId'), 'newFlower': other['prototype'] in by_proto}
                if target:
                    parts = document.get('geometryVariants', {}).get(other.get('geometryKey'), target['parts'])
                    placed = world_parts(parts, other['position'], other.get('renderYaw', other['yaw']), other.get('renderOffset', [0, 0, 0]))
                    hits, _ = contacts(own, placed)
                    entry.update(conservativeContactPairs=len(hits), firstContacts=hits[:4])
                    picture.extend(placed)
                nearby.append(entry)
            record = {'variant': spec['variant'], 'level': spec['level'], 'id': item['id'], 'prototype': item['prototype'],
                      'position': item['position'], 'savedYaw': item['yaw'], 'renderYaw': 0,
                      'orientationReason': 'Source Sprite.noRot is true.', 'modelId': model['id'],
                      'savedComponents': raw[item['id']].get('components', []), 'neighborsWithin1_2': nearby}
            records.append(record)
            if render and rendered < 5 and (rendered < 2 or item['yaw'] != 0 or spec['level'] == -2):
                local = []
                for piece in picture:
                    p = deepcopy(piece)
                    for bound in ('min', 'max'):
                        for axis in (0, 1):
                            p[bound][axis] -= item['position'][axis]
                    if p['min'][2] > 1.25:
                        continue
                    p['max'][2] = min(p['max'][2], 1.25)
                    local.append(p)
                bm.render_model({'parts': local}, (850, 650), -1.0, .83, pixels_per_unit=260, screen_origin=(425, 420)).save(REVIEW / f'context-{spec["variant"]}-{spec["level"]}-{item["id"]}.png')
                rendered += 1
    assert Counter(row['variant'] for row in records) == {'redux': 39, 'classic': 4}
    return records, hashes


def reviews(models, images):
    REVIEW.mkdir(parents=True, exist_ok=True)
    sheet = Image.new('RGB', (1500, 1200), (23, 33, 43))
    draw = ImageDraw.Draw(sheet)
    draw.text((14, 10), 'FLOWERS / original source and solid cluster draft; height, leaves and branching depth inferred', fill='white')
    for index, model in enumerate(models):
        image = images[model['referenceState']]
        original = image.resize((192, 192), Image.Resampling.NEAREST)
        card = Image.new('RGB', (1100, 390), (23, 33, 43))
        label = ImageDraw.Draw(card)
        label.text((12, 10), model['referencePrototype'], fill='white')
        card.paste(original, (10, 100), original)
        for j, (yaw, pitch) in enumerate(((-math.pi / 2, .9), (-.6, .5), (math.pi / 2, .9))):
            preview = bm.render_model(model, (280, 300), yaw, pitch)
            card.paste(preview, (235 + j * 285, 50))
        card.save(REVIEW / (model['id'] + '.png'))
        x, y = index % 3 * 500, 40 + index // 3 * 380
        sheet.paste(original, (x + 5, y + 80), original)
        sheet.paste(bm.render_model(model, (285, 310), -math.pi / 2, .95), (x + 205, y + 30))
        draw.text((x + 10, y + 345), model['referencePrototype'], fill='white')
    sheet.save(REVIEW / 'comparison-montage.png')


def main():
    global CHECK
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--skip-reviews', action='store_true')
    parser.add_argument('--skip-context', action='store_true')
    args = parser.parse_args()
    CHECK = args.check
    indexed = {p['id']: p for p in json.loads((GEN / 'inventory.json').read_text())['prototypes']}
    coverage = {p['id']: p for p in json.loads((GEN / 'coverage.json').read_text())['prototypes']}
    meta = json.loads((SOURCE / 'meta.json').read_text())
    assert meta['size'] == {'x': 32, 'y': 32}
    effective = current_components()
    pool, models, sources, images = Pool(), [], [], {}
    for prototype in PROTOTYPES:
        row, sprite = indexed[prototype], indexed[prototype]['sprite']
        assert coverage[prototype]['category'] == 'unmapped' or all(m.get('modelId', '').startswith('CMU3DFlowers') for m in coverage[prototype]['matches'])
        state = sprite['state']
        assert sprite == {'sprite': 'Decals/Flora/flora_flowers.rsi', 'state': state, 'noRot': True, 'drawdepth': 'FloorObjects'}
        assert effective[prototype]['Sprite'] == {'type': 'Sprite', **sprite}
        assert effective[prototype]['Physics'] == {'type': 'Physics', 'bodyType': 'Static', 'canCollide': False}
        assert 'SpriteFade' in effective[prototype]
        metadata = next(s for s in meta['states'] if s['name'] == state)
        assert metadata == {'name': state}
        image = Image.open(SOURCE / (state + '.png')).convert('RGBA')
        assert image.size == (32, 32)
        images[state] = image
        parts, interpretation = geometry(state, image, pool)
        model = {'type': 'cmu3DModel', 'id': 'CMU3DFlowers' + prototype.removeprefix('RMCFlowers'),
                 'label': prototype.removeprefix('RMC'), 'status': 'draft', 'sourcePrototypes': [prototype],
                 'referencePrototype': prototype, 'referenceRsi': 'Decals/Flora/flora_flowers.rsi', 'referenceState': state,
                 'sourceDirections': 1, 'useEntityRotation': False, 'groundOffset': '0, 0',
                 'description': 'Distinct solid flower/bud heads and branching stems from this source cluster. Original head XY footprint, pixel palette and source noRot retained. Height, hidden petals, leaves and branch depth are inferred; no growth or damage animation is invented. See SOURCES_FLOWERS.md.',
                 'parts': parts}
        models.append(model)
        sources.append({'prototype': prototype, 'state': state, 'sourceFile': (SOURCE / (state + '.png')).relative_to(ROOT).as_posix(),
                        'sourceSha256': sha(SOURCE / (state + '.png')), 'sourceRgbaSha256': hashlib.sha256(image.tobytes()).hexdigest(),
                        'metaSha256': sha(SOURCE / 'meta.json'), 'license': meta['license'], 'copyright': meta['copyright'],
                        'instanceCounts': row['instanceCounts'], 'alphaValues': np.unique(np.array(image)[:, :, 3]).tolist(),
                        'sourceDirections': 1, 'frames': 1, 'sourceNoRot': True, 'sourceOffset': [0, 0],
                        'sourcePhysics': {'bodyType': 'Static', 'canCollide': False}, 'sourceSpriteFade': True,
                        'sourcePrototypeFile': SOURCE_PROTOTYPES.relative_to(ROOT).as_posix(),
                        'sourcePrototypeSha256': sha(SOURCE_PROTOTYPES), 'resolvedComponentTypes': sorted(effective[prototype]),
                        'geometryInterpretation': interpretation})
    configure_surfaces(pool)
    models = [bm.validate_model(model) for model in models]
    serialized = deepcopy(models)
    for model in serialized:
        for key in ('groundOffset',):
            model[key] = ', '.join(str(v) for v in model[key])
        for part in model['parts']:
            for key in ('min', 'max'):
                part[key] = ', '.join(f'{v:.8f}' for v in part[key])
    for path, rows in ((MODEL, serialized), (ART, pool.entries)):
        write(path, ('# Generated by Tools/three_d/author_flowers.py; source-specific drafts.\n' + yaml.safe_dump(rows, sort_keys=False, width=110)).encode('utf-8'))
    actual = bm.load_models(MODEL)
    for model in actual:
        assert model['useEntityRotation'] is False and model['sourceDirections'] == 1
        bm.glb_bytes(model)
    note = '''# Flower-cluster source drafts

Nine exact prototype mappings cover 39 Redux and 4 classic placements: RMCFlowersbr1/br2/br3, pv1/pv2/pv3 and y1/y3/y4. Each source is a single static 32 by 32 frame from Decals/Flora/flora_flowers.rsi. The original source is already the complete one-layer reference, so no duplicate reference RSI is needed. Model references preserve every original RGBA pixel, including low-alpha ground shadows.

Each model preserves the source-colored head positions at one pixel per 1/32 tile, the original entity pivot and zero Sprite offset. Broad blossoms use intersecting solid rounded petal lobes, small heads use solid buds, and isolated colored tips use small solid buds. Exact source head crops retain their original RGB and alpha; only pixels belonging to other source structures are masked from that separate head crop. These heads have individual stems and low leaves, with real gaps between plants. There is no full-cluster rectangular backing or extruded full sprite.

Head thickness, 0.118–0.205 tile heights, leaning/branching stems, convex hidden petals, leaf thickness and the interpretation of opaque green regions as low foliage are authored inference. The reference proves original colors and arrangement, not these hidden dimensions. Low-alpha dark source pixels are interpreted as drawn ground shadows; they stay in the original reference and do not become solid black foliage. All draft dimensions and colors are recorded in flowers-source-audit.json.

Sprite.noRot is true: model useEntityRotation is false, including saved half-turn entities. Saved transforms are retained; rendering uses zero yaw as the source does. Physics is static and noncolliding. SpriteFade remains an existing source behavior; no growth, wind, consumption, damage-state artwork or animation controller is invented. This is a static geometry batch, not proof of complete live appearance equivalence.

All contexts are inspected with every new flower mapping admitted together, including new-flower neighbors. Contact counts use conservative bounding boxes and can overcount curved petals, stems and transparent cropped corners. Neighbors and saved overrides remain visible in the source audit; no entity positions or other fixture geometry are edited.

Original RSI metadata declares CC-BY-SA-3.0, taken from tgstation at https://github.com/tgstation/tgstation/commit/729d858807905263adab8b5a331c1d8a04982dd3. Exact attribution, source hashes and crop checks are retained in flowers-source-audit.json and flowers-proof.json. Preserve this attribution when distributing derived textures.

Regenerate only these dedicated assets with `python Tools/three_d/author_flowers.py`; compare asset bytes using `--check --skip-context --skip-reviews`. The generator does not run global exports, builds, a game or a server.
'''
    write(NOTE, note.encode('utf-8'))
    if CHECK:
        print(json.dumps({'models': len(actual), 'assetFilesChecked': len(WRITTEN), 'byteIdentical': True}))
        return
    REVIEW.mkdir(parents=True, exist_ok=True)
    if not args.skip_reviews:
        reviews(actual, images)
    context, context_hashes = ([], {}) if args.skip_context else contexts(actual, not args.skip_reviews)
    json_write(GEN / 'flowers-source-audit.json', {'schemaVersion': 1, 'prototypes': sources, 'contexts': context,
                                               'contextInputSha256': context_hashes})
    report = {'schemaVersion': 1, 'assetChecksPass': True, 'counts': {'models': 9, 'exactMappings': 9,
               'reduxInstances': 39, 'classicInstances': 4, 'parts': sum(len(m['parts']) for m in actual), 'textures': len(pool.entries)},
              'models': [{'id': m['id'], 'sourcePrototypes': m['sourcePrototypes'], 'parts': len(m['parts'])} for m in actual],
              'atlasIndices': [row['atlasIndex'] for row in pool.entries], 'sourceCrops': pool.proofs,
              'allStaticOneFrameOneDirection': True, 'sourceNoRotPreserved': True,
              'nativeScalarVector2Count': 9, 'nativeScalarVector3Count': sum(len(m['parts']) * 2 for m in actual),
              'contexts': {'placements': len(context), 'savedHalfTurns': sum(abs(c['savedYaw'] - math.pi) < 1e-6 for c in context),
                           'conservativeContactNeighbors': sum(n.get('conservativeContactPairs', 0) > 0 for c in context for n in c['neighborsWithin1_2']),
                           'newFlowerContactNeighbors': sum(n.get('conservativeContactPairs', 0) > 0 and n['newFlower'] for c in context for n in c['neighborsWithin1_2'])},
              'writtenAssetsSha256': {p.relative_to(ROOT).as_posix(): sha(p) for p in WRITTEN},
              'generatorSha256': sha(Path(__file__)), 'sourceAuditSha256': sha(GEN / 'flowers-source-audit.json'),
              'limitations': ['Hidden stem and leaf depth, petal thickness and height are inferred.',
                              'Static source color/placement checks do not prove live SpriteFade or destruction-state parity.',
                              'Conservative context contacts are not exact curved-surface collision results.']}
    if not args.skip_reviews:
        report['reviewSha256'] = {p.relative_to(ROOT).as_posix(): sha(p) for p in REVIEW.glob('*.png')}
    json_write(GEN / 'flowers-proof.json', report)
    print(json.dumps(report['counts']))


if __name__ == '__main__':
    main()
