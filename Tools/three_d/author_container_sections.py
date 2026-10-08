"""Reproduce eight placed container sections; dedicated assets and offline evidence only."""
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
import sprite_states
import surfaces
from author_wide_machinery import world_parts, contacts

STATES = dict(zip(
    ['AU14ContainerLightHyperdyne' + s for s in ('Left', 'Middle', 'Right')] +
    ['AU14ContainerMK6' + s for s in ('Left', 'Middle', 'Right')] +
    ['AU14ContainerTartarusLeftMedicalEmpty', 'AU14ContainerTartarusRightMedicalEmptyAlt'],
    ['hd_l_alt', 'hd_m_alt', 'hd_r_alt', 'mk6_l', 'mk6_m', 'mk6_r', 'emptymedicalleft', 'medicalright']))
SOURCE = ROOT / 'Content.CMU/Resources/Textures/CMU14/Structures/containers.rsi'
SOURCE_YAML = ROOT / 'Content.CMU/Resources/Prototypes/CMU14/Entities/Structures/Misc/containers.yml'
BASELINE = ROOT / '.codex/model-batch-baseline950'
MODEL = ROOT / 'Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_container_sections.yml'
ART = MODEL.with_name('garrison_container_sections_art.yml')
TEXTURES = ROOT / 'Content.CMU/Resources/Textures/CMU14/ThreeD/ContainerSections'
NOTE = ROOT / 'Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_CONTAINER_SECTIONS.md'
GEN = ROOT / 'Tools/three_d/generated'
REVIEW = GEN / 'review/container-sections'
CHECK, WRITTEN = False, []


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def json_write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')


def write(path, data):
    if CHECK:
        assert path.is_file() and path.read_bytes() == data, f'Generated asset differs: {path}'
    else:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
    WRITTEN.append(path)


def png(image):
    stream = BytesIO()
    image.save(stream, format='PNG')
    return stream.getvalue()


def vec(value):
    return ', '.join(f'{x:.8f}'.rstrip('0').rstrip('.') if x else '0' for x in value)


def box(parts, label, color, low, high, **kwargs):
    parts.append(dict(label=label, color=color, min=list(low), max=list(high), **kwargs))


def dominant(image, rect):
    pixels = np.array(image.crop(rect)).reshape((-1, 4))
    rgb = Counter(tuple(map(int, p[:3])) for p in pixels if p[3] == 255).most_common(1)[0][0]
    return '#' + ''.join(f'{v:02X}' for v in rgb)


def resolved_components():
    rows = {row['id']: row for row in inventory.load_yaml(SOURCE_YAML.read_text()) if row.get('type') == 'entity'}
    def resolve(uid):
        result, row = {}, rows[uid]
        parents = row.get('parent', [])
        for parent in [parents] if isinstance(parents, str) else parents:
            result.update(deepcopy(resolve(parent)))
        for component in row.get('components', []):
            result.setdefault(component['type'], {}).update(deepcopy(component))
        return result
    return {uid: resolve(uid) for uid in STATES}


class Pool:
    def __init__(self):
        self.entries, self.proofs, self.cache = [], [], {}
        paths = [p for p in MODEL.parent.glob('*.yml') if p != ART]
        for folder in (ROOT / '.codex').glob('*staged*'):
            paths.extend(folder.rglob('*.yml'))
        for path in paths:
            for row in yaml.load(path.read_text(encoding='utf-8-sig'), Loader=yaml.CSafeLoader) or []:
                if row.get('type') == 'cmu3DSurface':
                    assert not 2000 <= row['atlasIndex'] <= 2099, f'Container atlas conflict: {path}'

    def crop(self, state, image, role, rectangle):
        crop = image.crop(rectangle)
        key = (crop.size, hashlib.sha256(crop.tobytes()).hexdigest())
        if key not in self.cache:
            index = 2000 + len(self.entries)
            assert index <= 2099
            uid = f'CMU3DContainerSectionArt{index}'
            path = TEXTURES / (uid + '.png')
            write(path, png(crop))
            self.entries.append(dict(type='cmu3DSurface', id=uid, atlasIndex=index,
                                     texture='/Textures/CMU14/ThreeD/ContainerSections/' + path.name))
            self.cache[key] = uid
        uid = self.cache[key]
        decoded = Image.open(TEXTURES / (uid + '.png')).convert('RGBA')
        assert decoded.size == crop.size and decoded.tobytes() == crop.tobytes()
        self.proofs.append(dict(state=state, role=role, surface=uid, crop=list(rectangle),
                                size=list(crop.size), rgbaSha256=key[1], exactOriginalPixels=True))
        return uid


def large_section(uid, state, image, pool):
    """A corrugated shell, projected roof, and outer frames; internal joins stay open."""
    x0, _, x1, _ = image.getbbox()
    left, right = (x0 - 16) / 32, (x1 - 16) / 32
    body = dominant(image, (10, 30, 28, 56))
    roof = dominant(image, (8, 4, 29, 23))
    rim = dominant(image, (2, 58, 29, 62))
    side = dominant(image, (2, 28, 29, 59))
    p = []
    box(p, 'closed cargo shell', body, (left, -.46, .055), (right, 1.48, 2.002))
    cuts = sorted(set([x0, x1] + [x for x in range(4, 32, 4) if x0 < x < x1]))
    for index, (a, b) in enumerate(zip(cuts, cuts[1:])):
        surface = pool.crop(state, image, f'front corrugation {index + 1}', (a, 26, b, 62))
        depth = -.478 if index % 2 == 0 else -.499
        box(p, f'painted corrugation {index + 1}', '#FFFFFF', ((a - 16) / 32, depth, .055),
            ((b - 16) / 32, -.457, 2.002), surface=surface, surfaceAxis='XZ')
    box(p, 'source-painted roof', '#FFFFFF', (left, -.474, 2.002), (right, 1.48, 2.027),
        surface=pool.crop(state, image, 'roof', (x0, 2, x1, 26)), surfaceAxis='XY')
    for z in (.04, 2.015):
        box(p, 'rear horizontal frame', rim, (left, 1.445, z), (right, 1.496, z + .035))
    for x in (-.32, -.1, .12, .34):
        box(p, 'rear corrugation', roof, (x - .02, 1.472, .12), (x + .02, 1.499, 1.92))
    if not uid.endswith('Middle'):
        is_left = uid.endswith('Left')
        a, b = (left, left + .032) if is_left else (right - .032, right)
        box(p, 'outer end wall', side, (a, -.48, .08), (b, 1.48, 2.014))
        for y in (-.37, 1.37):
            box(p, 'corner lifting foot', rim, (a, y - .065, 0), (b, y + .065, .12))
            box(p, 'corner upright', roof, (a, y - .035, .08), (b, y + .035, 2.04))
        for y in (-.11, .24, .59, .94):
            box(p, 'outer end rib', rim, (max(-.5, a - .008) if is_left else a, y, .15),
                (b if is_left else min(.5, b + .008), y + .035, 1.90))
    for index, a in enumerate((2, 10, 18, 26)):
        a, b = max(a, x0), min(a + 3, x1)
        if a < b:
            box(p, f'source-painted roof ridge {index + 1}', '#FFFFFF',
                ((a - 16) / 32, 1.48 - 22 / 24 * 1.954, 2.024),
                ((b - 16) / 32, 1.48 - 2 / 24 * 1.954, 2.045),
                surface=pool.crop(state, image, f'roof ridge {index + 1}', (a, 4, b, 24)), surfaceAxis='XY')
    return p


def medical_section(uid, state, image, pool):
    """The large crate continues seven pixels into the right tile, beside two cases."""
    left = state == 'emptymedicalleft'
    a, b = (-.5, .5) if left else (-.5, -.28125)
    p = []
    crate = '#BAB9B3'
    box(p, 'medical crate shell' if left else 'medical crate seam continuation', crate,
        (a, -.46, .055), (b, .48, .942))
    crop_right = 32 if left else 7
    box(p, 'source-painted medical crate face', '#FFFFFF', (a, -.499, .055), (b, -.457, .942),
        surface=pool.crop(state, image, 'crate front', (0, 42, crop_right, 61)), surfaceAxis='XZ')
    box(p, 'source-painted medical crate lid', '#FFFFFF', (a, -.474, .942), (b, .48, .967),
        surface=pool.crop(state, image, 'crate lid', (0, 22, crop_right, 42)), surfaceAxis='XY')
    for z in (.04, .955):
        box(p, 'rear crate frame', '#78756B', (a, .445, z), (b, .496, z + .035))
    edge_a, edge_b = (a, a + .032) if left else (b - .032, b)
    box(p, 'outer medical crate end', '#B1AFA8', (edge_a, -.48, .08), (edge_b, .48, .954))
    for y in (-.37, .37):
        box(p, 'medical crate lifting foot', '#4E3230', (edge_a, y - .065, 0), (edge_b, y + .065, .12))
        box(p, 'medical crate end frame', '#78756B', (edge_a, y - .035, .08), (edge_b, y + .035, .99))
    strap_a, strap_b = (0, 8) if left else (0, 6)
    xa, xb = (strap_a - 16) / 32, (strap_b - 16) / 32
    box(p, 'raised medical locking strap', '#C4C2BD', (xa, -.435, .968), (xb, .445, 1.026))
    box(p, 'rounded strap end', '#B1AFA8', (xa, -.499, .79), (xb, -.415, .993), shape='Ellipsoid')
    box(p, 'strap latch recess', '#78756B', (xa + .045, -.499, .84), (xb - .045, -.48, .9))
    box(p, 'source-painted locking strap', '#FFFFFF', (xa, -.435, 1.026), (xb, .445, 1.029),
        surface=pool.crop(state, image, 'strap', (strap_a, 22, strap_b, 42)), surfaceAxis='XY')
    if not left:
        # The source has a visible gap between the large crate strap and the separate cases.
        x0, x1 = -9 / 32, 15 / 32
        box(p, 'green medical case body', '#416957', (x0, -.48, .045), (x1, .48, .52))
        box(p, 'green medical case lid', '#BEBAB1', (x0, -.48, .52), (x1, .48, .56))
        box(p, 'green case source front and cross', '#FFFFFF', (x0, -.499, .045), (x1, -.48, .52),
            surface=pool.crop(state, image, 'green case front', (7, 50, 31, 61)), surfaceAxis='XZ')
        box(p, 'green case exposed front lid strip', '#FFFFFF', (x0, -.48, .56), (x1, -.16, .564),
            surface=pool.crop(state, image, 'green case exposed lid', (7, 45, 31, 50)), surfaceAxis='XY')
        for x in (x0, x1 - .035):
            box(p, 'green case edge bumper', '#5D5C57', (x, -.48, .045), (x + .035, .48, .56))
        # A smaller physical case sits above the green chest. Its original red straps and cross stay distinct.
        xa, xb = -6 / 32, 11 / 32
        box(p, 'red medical case body', '#B3AFA8', (xa, -.05, .56), (xb, .45, .96))
        box(p, 'red medical case front and cross', '#FFFFFF', (xa, -.065, .56), (xb, -.047, .96),
            surface=pool.crop(state, image, 'red case front', (10, 33, 27, 45)), surfaceAxis='XZ')
        box(p, 'red medical case lid', '#FFFFFF', (xa, -.05, .96), (xb, .45, .98),
            surface=pool.crop(state, image, 'red case lid', (10, 24, 27, 33)), surfaceAxis='XY')
        for x, crop in ((-3 / 32, (13, 24, 15, 33)), (6 / 32, (22, 24, 24, 33))):
            box(p, 'raised red case strap', '#FFFFFF', (x, -.05, .98), (x + 2 / 32, .45, .992),
                surface=pool.crop(state, image, 'red case strap', crop), surfaceAxis='XY')
    return p


def configure_surfaces(pool):
    registry = {}
    for path in sorted(MODEL.parent.glob('*.yml')):
        if path == ART:
            continue
        for entry in yaml.load(path.read_text(encoding='utf-8-sig'), Loader=yaml.CSafeLoader) or []:
            if entry.get('type') == 'cmu3DSurface':
                registry[entry['id']] = {**entry, 'image': Image.open(surfaces.texture_path(entry['texture'])).convert('RGBA')}
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
                block, prototype = [], line.split(':', 1)[1].strip().strip('"\'')
            elif prototype in STATES:
                if line.startswith('  - uid:'):
                    finish()
                    block = [line]
                elif block:
                    block.append(line)
    finish()
    return result


def contexts(models, components, render):
    index = {m['id']: m for m in json.loads((BASELINE / 'models.json').read_text())['models']}
    index.update({m['id']: m for m in models})
    by_proto = {m['referencePrototype']: m for m in models}
    records, hashes, seams = [], {}, []
    for spec in json.loads((BASELINE / 'scenes.json').read_text()):
        path = BASELINE / spec['file']
        document = json.loads(path.read_text())
        targets = [e for e in document['instances'] if e['prototype'] in STATES]
        if not targets:
            continue
        raw_path = ROOT / document['map']['path']
        raw = saved_records(raw_path)
        for input_path in (path, raw_path):
            hashes[input_path.relative_to(ROOT).as_posix()] = sha(input_path)
        for item in targets:
            item.update(modelId=by_proto[item['prototype']]['id'], renderYaw=item['yaw'], matchKind='exact')
        for item in targets:
            model = by_proto[item['prototype']]
            overrides = {c['type']: c for c in raw[item['id']].get('components', [])}
            from scene import normalize_tint
            state, reason = sprite_states.saved_pose(model, components[item['prototype']], overrides, normalize_tint)
            assert state == STATES[item['prototype']] and reason is None, (item['id'], reason)
            assert item['yaw'] == 0 and not overrides.get('Sprite')
            own = world_parts(model['parts'], item['position'], item['yaw'])
            picture, neighbors = deepcopy(own), []
            for other in document['instances']:
                if other['id'] == item['id'] or other['position'][2] != item['position'][2]:
                    continue
                if max(abs(other['position'][a] - item['position'][a]) for a in (0, 1)) > 2.1:
                    continue
                entry = {k: other.get(k) for k in ('id', 'prototype', 'position', 'yaw', 'modelId', 'renderOffset')}
                target = index.get(other.get('modelId'))
                entry['newSection'] = other['prototype'] in STATES
                if target:
                    pieces = document.get('geometryVariants', {}).get(other.get('geometryKey'), target['parts'])
                    placed = world_parts(pieces, other['position'], other.get('renderYaw', other['yaw']), other.get('renderOffset', [0, 0, 0]))
                    hits, _ = contacts(own, placed)
                    entry.update(conservativeContactPairs=len(hits), contactPairs=hits)
                    picture.extend(placed)
                neighbors.append(entry)
            record = dict(variant=spec['variant'], level=spec['level'], id=item['id'], prototype=item['prototype'],
                          position=item['position'], yaw=item['yaw'], modelId=model['id'], sourceState=state,
                          savedComponents=raw[item['id']].get('components', []), sourceGatePassed=True, neighborsWithin2_1=neighbors)
            records.append(record)
            if render:
                local = []
                for part in picture:
                    part = deepcopy(part)
                    for bound in ('min', 'max'):
                        for axis in (0, 1):
                            part[bound][axis] -= item['position'][axis]
                    local.append(part)
                card = Image.new('RGB', (1200, 500), (23, 33, 43))
                draw = ImageDraw.Draw(card)
                draw.text((12, 10), f'{spec["variant"]} level {spec["level"]}, saved UID {item["id"]}: all nearby modeled fixtures retained', fill='white')
                for col, yaw in enumerate((-math.pi / 2, -.55)):
                    im = bm.render_model({'parts': local}, (590, 450), yaw, .82, pixels_per_unit=87, screen_origin=(295, 310))
                    card.paste(im, (col * 600, 35))
                card.save(REVIEW / f'context-{spec["variant"]}-{spec["level"]}-{item["id"]}.png')
        for left in targets:
            for right in targets:
                if right['position'] == [left['position'][0] + 1, left['position'][1], left['position'][2]]:
                    if ('Medical' in left['prototype']) != ('Medical' in right['prototype']):
                        continue
                    lp, rp = by_proto[left['prototype']]['parts'][0], by_proto[right['prototype']]['parts'][0]
                    gap = 1 + rp['min'][0] - lp['max'][0]
                    assert abs(gap) < 1e-9
                    seams.append(dict(level=spec['level'], left=left['id'], right=right['id'], shellGap=gap,
                                      shellDepthEqual=lp['min'][1] == rp['min'][1] and lp['max'][1] == rp['max'][1],
                                      shellHeightEqual=lp['max'][2] == rp['max'][2]))
    assert Counter(row['level'] for row in records) == {0: 8, -2: 8}
    assert len(seams) == 10 and all(s['shellDepthEqual'] and s['shellHeightEqual'] for s in seams)
    if render:
        paths = sorted(REVIEW / f'context-{r["variant"]}-{r["level"]}-{r["id"]}.png' for r in records)
        assert len(paths) == 16
        montage = Image.new('RGB', (2400, 1000), (23, 33, 43))
        for index, path in enumerate(paths):
            montage.paste(Image.open(path).resize((600, 250)), (index % 4 * 600, index // 4 * 250))
        montage.save(REVIEW / 'context-montage.png')
    hashes.update({p.relative_to(ROOT).as_posix(): sha(p) for p in (BASELINE / 'models.json', BASELINE / 'scenes.json')})
    return records, seams, hashes


def reviews(models, images):
    sheet = Image.new('RGB', (1400, 1600), (23, 33, 43))
    draw = ImageDraw.Draw(sheet)
    draw.text((12, 8), 'CONTAINER SECTIONS / exact source pixels, physical roof/front split; hidden depth and hardware inferred', fill='white')
    for i, model in enumerate(models):
        card = Image.new('RGB', (1400, 395), (23, 33, 43))
        ImageDraw.Draw(card).text((12, 8), model['referencePrototype'], fill='white')
        original = images[model['referenceState']].resize((160, 320), Image.Resampling.NEAREST)
        card.paste(original, (15, 50), original)
        for j, (yaw, pitch) in enumerate(((-math.pi / 2, .85), (-.55, .55), (math.pi / 2, .65))):
            preview = bm.render_model(model, (385, 340), yaw, pitch)
            card.paste(preview, (200 + j * 395, 35))
        card.save(REVIEW / (model['id'] + '.png'))
        x, y = i % 2 * 700, 35 + i // 2 * 390
        sheet.paste(card.crop((0, 0, 585, 390)), (x, y))
    sheet.save(REVIEW / 'comparison-montage.png')
    for label, group in (('hyperdyne', models[:3]), ('mk6', models[3:6]), ('medical', models[6:])):
        parts = []
        source = Image.new('RGBA', (32 * len(group), 64))
        for index, model in enumerate(group):
            parts.extend(world_parts(model['parts'], [index, 0, 0], 0))
            source.paste(images[model['referenceState']], (index * 32, 0))
        card = Image.new('RGB', (1400, 650), (23, 33, 43))
        ImageDraw.Draw(card).text((12, 10), label + ' / source assembly, south-facing and reverse oblique drafts', fill='white')
        source = source.resize((source.width * 4, 256), Image.Resampling.NEAREST)
        card.paste(source, (15, 170), source)
        for index, yaw in enumerate((-math.pi / 2, .7)):
            card.paste(bm.render_model({'parts': parts}, (490, 570), yaw, .65), (410 + index * 490, 60))
        card.save(REVIEW / ('assembly-' + label + '.png'))


def serialize(model):
    model = deepcopy(model)
    for key in ('groundOffset', 'sourceSpriteOffset'):
        model[key] = vec(model[key])
    for parts in [model['parts']] + [f['parts'] for s in model['spriteStates'].values() for f in s['frames']]:
        for part in parts:
            part['min'], part['max'] = vec(part['min']), vec(part['max'])
    return model


def main():
    global CHECK
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--skip-reviews', action='store_true')
    parser.add_argument('--skip-context', action='store_true')
    args = parser.parse_args()
    CHECK = args.check
    pool, images, models, audit = Pool(), {}, [], []
    meta, components = json.loads((SOURCE / 'meta.json').read_text()), resolved_components()
    assert meta['size'] == {'x': 32, 'y': 64}
    for uid, state in STATES.items():
        sprite = components[uid]['Sprite']
        assert sprite == dict(type='Sprite', sprite='CMU14/Structures/containers.rsi', offset='0, 0.5', state=state)
        assert next(s for s in meta['states'] if s['name'] == state) == {'name': state}
        image = Image.open(SOURCE / (state + '.png')).convert('RGBA')
        assert image.size == (32, 64)
        images[state] = image
        parts = medical_section(uid, state, image, pool) if 'Medical' in uid else large_section(uid, state, image, pool)
        palette = {'#' + ''.join(f'{int(v):02X}' for v in pixel[:3])
                   for pixel in np.array(image).reshape((-1, 4)) if pixel[3]}
        assert all(p['color'] in palette for p in parts if not p.get('surface'))
        fixture_max_y = .8 if 'Medical' in uid else 1.5
        assert all(p['min'][0] >= -.5 and p['max'][0] <= .5 and p['min'][1] >= -.5 and
                   p['max'][1] <= fixture_max_y and p['min'][2] >= 0 for p in parts)
        model = dict(type='cmu3DModel', id='CMU3DSection' + uid, label=uid.removeprefix('AU14Container') + ' container section',
                     status='draft', sourcePrototypes=[uid], referencePrototype=uid,
                     referenceRsi='/Textures/CMU14/Structures/containers.rsi', referenceState=state,
                     sourceDirections=1, useEntityRotation=True, placement='floor', groundOffset=[0, 0],
                     sourceSpriteOffset=[0, .5], sourceSpriteRotates=True,
                     description='Source-matched static freight section. Original roof/front artwork, physical shared seams, source offset and ordinary entity yaw retained. Hidden height, depth and hardware are inferred. See SOURCES_CONTAINER_SECTIONS.md.',
                     parts=parts, spriteStates={state: dict(frames=[dict(parts=deepcopy(parts))], delays=[1])})
        models.append(model)
        audit.append(dict(prototype=uid, state=state, sourceFile=(SOURCE / (state + '.png')).relative_to(ROOT).as_posix(),
                          sourceSha256=sha(SOURCE / (state + '.png')), rgbaSha256=hashlib.sha256(image.tobytes()).hexdigest(),
                          sourceMetaSha256=sha(SOURCE / 'meta.json'),
                          originalAlphaValues=np.unique(np.array(image)[:, :, 3]).tolist(), originalBounds=list(image.getbbox()),
                          resolvedComponents=components[uid], frames=1, directions=1, sourceOffset=[0, .5], sourceNoRot=False,
                          sourceMetadata=meta, hiddenGeometryInferred=True))
    configure_surfaces(pool)
    models = [bm.validate_model(model) for model in models]
    serialized = [serialize(model) for model in models]
    for path, rows in ((MODEL, serialized), (ART, pool.entries)):
        write(path, ('# Generated by Tools/three_d/author_container_sections.py; source-specific drafts.\n' +
                     yaml.safe_dump(rows, sort_keys=False, width=115)).encode('utf-8'))
    actual = bm.load_models(MODEL)
    written = yaml.load(MODEL.read_text(), Loader=yaml.CSafeLoader)
    scalar_counts = Counter()
    for model in written:
        for key in ('groundOffset', 'sourceSpriteOffset'):
            assert isinstance(model[key], str) and len(model[key].split(',')) == 2
            scalar_counts['vector2'] += 1
        for parts in [model['parts']] + [f['parts'] for s in model['spriteStates'].values() for f in s['frames']]:
            for part in parts:
                for key in ('min', 'max'):
                    assert isinstance(part[key], str) and len(part[key].split(',')) == 3
                    scalar_counts['vector3'] += 1
    glbs, references = {}, []
    for model in actual:
        sprite_states.validate_source(model, bm.resource_file)
        binary = bm.glb_bytes(model)
        glbs[model['id']] = hashlib.sha256(binary).hexdigest()
        import struct
        length = struct.unpack_from('<I', binary, 12)[0]
        document = json.loads(binary[20:20 + length])
        assert not document.get('animations'), 'Static sources must not gain invented animation clips'
        outputs, _ = sprite_states.viewer_references(model, bm.resource_file, png)
        assert len(outputs) == 1
        reference = Image.open(BytesIO(next(iter(outputs.values())))).convert('RGBA')
        original = images[model['referenceState']]
        assert reference.size == original.size and reference.tobytes() == original.tobytes()
        references.append(dict(modelId=model['id'], sourceState=model['referenceState'],
                               fullReferencePixels=reference.width * reference.height,
                               rgbaSha256=hashlib.sha256(reference.tobytes()).hexdigest(), exactOriginalPixels=True))
    note = '''# Container-section source drafts

Eight exact mappings cover 16 Redux placements, eight on the surface and eight on level -2. Six tall Hyperdyne/MK6 sections form four complete three-section cargo containers. The two medical prototypes form two joined assemblies. All remain drafts.

All sources are static single-direction 32 by 64 frames in CMU14/Structures/containers.rsi. The source Sprite offset is 0, 0.5 and noRot is false. The offset is recorded in a strict one-frame source contract; it is already accounted for in reconstructed coordinates and is not applied again as a model translation. Saved entity transforms stay unchanged. No door, lid-opening, inventory or damage animation is invented. DeleteOnExplosion and XenoToggleChargingDamage destroy the source entity; they do not define alternate source frames for this batch.

Tall sections occupy the original one-by-two-tile footprint. Corrugated front strips retain exact source pixels, with the original roof projection laid onto a real roof. Raised roof seams, rear corrugations, outer end frames and feet provide depth from several views. Interior joins contain no redundant end wall. Front height is inferred at 2.002 tiles, consistent with the existing freight drafts. Hidden ends and rear colors are sampled from the original palette, not newly painted branding.

The medical pair is source-specific: emptymedicalleft is the main closed white crate; the first seven columns of medicalright continue its right end. The remaining right section contains a separate green medical chest with a smaller red case above it. Despite the prototype name RightMedicalEmptyAlt, its actual state is medicalright, not emptymedicalright. The correct reference and visible medical crosses are retained. Cases are separate solids with lid, front, straps and bumpers, not the whole source image pasted on a rectangle.

Medical physics is shorter than the tall container: fixture bounds are -0.5,-0.5,0.5,0.8. The compact visual draft stays within that envelope at Y -0.499 through 0.496, matching the existing compact source peers and the immediately adjacent northern wall on level -2. It does not reinterpret the collider as a two-tile container. The exact physical depth, case stack height and hidden assembly remain inference. The medical crate top is 1.029; the red case reaches 0.992. The geometry does not alter any source collider or neighboring fixture.

Original source metadata declares CC-BY-SA-3.0. Attribution: Taken from cmss13 at https://github.com/Steelpoint/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/containers/containersextended.dmi and https://github.com/Steelpoint/cmss13/blob/bf751971a956160f294b8f0e71addb109f0242be/icons/obj/structures/props/containers/contain.dmi. Preserve that license and attribution for these exact derived source crops. Geometric inference is not a claim of original-asset authorship.

The dedicated proof records original RGBA crops, scalar Vector2/Vector3 YAML, source frame gates, joined seam tests, all 16 saved contexts and conservative neighbor contacts against frozen baseline 950. Context contact candidates are not proof of physics collision or live source-state parity. Unknown neighbors remain explicit. Reviews show each model from three angles, three source assemblies and every saved placement.

Regenerate only this batch with `python Tools/three_d/author_container_sections.py`; check byte identity with `--check --skip-context --skip-reviews`. The generator never exports the whole library, builds native projects or launches a game/server.
'''
    write(NOTE, note.encode('utf-8'))
    if CHECK:
        print(json.dumps(dict(byteIdentical=True, models=len(actual), assetFilesChecked=len(WRITTEN))))
        return
    REVIEW.mkdir(parents=True, exist_ok=True)
    if not args.skip_reviews:
        reviews(actual, images)
    records, seams, hashes = ([], [], {}) if args.skip_context else contexts(actual, components, not args.skip_reviews)
    source_path = GEN / 'container-sections-source-audit.json'
    json_write(source_path, dict(schemaVersion=1, prototypes=audit, contexts=records, joinedSeams=seams,
                                contextInputSha256=hashes, sourcePrototypeSha256=sha(SOURCE_YAML)))
    report = dict(schemaVersion=1, assetChecksPass=True,
                  counts=dict(models=8, exactMappings=8, reduxPlacements=16, classicPlacements=0,
                              parts=sum(len(m['parts']) for m in actual), textures=len(pool.entries)),
                  modelIds=[m['id'] for m in actual], sourceMappings=STATES,
                  atlasIndices=[r['atlasIndex'] for r in pool.entries], exactSourceCrops=pool.proofs,
                  allStaticSourceStates=True, allSourceOffsetsPreserved=True,
                  nativeScalarVector2Count=scalar_counts['vector2'], nativeScalarVector3Count=scalar_counts['vector3'],
                  viewerReferenceChecks=references, allUntexturedColorsInOriginalPalette=True,
                  allPartsWithinSourceFixtureXY=True, noInventedGlbAnimations=True,
                  dedicatedGlbSha256=glbs, joinedSeams=seams,
                  contexts=dict(placements=len(records), sourceGatePassed=sum(r['sourceGatePassed'] for r in records),
                                conservativeContactNeighbors=sum(n.get('conservativeContactPairs', 0) > 0 for r in records for n in r['neighborsWithin2_1']),
                                newSectionContactNeighbors=sum(n.get('conservativeContactPairs', 0) > 0 and n['newSection'] for r in records for n in r['neighborsWithin2_1'])),
                  fitExceptions=[dict(level=r['level'], id=r['id'], prototype=r['prototype'], neighborId=n['id'],
                                      neighborPrototype=n['prototype'], conservativePartPairs=n['conservativeContactPairs'],
                                      limitation='Existing small-case draft intersects medical crate; saved transforms and both objects retained.')
                                 for r in records for n in r['neighborsWithin2_1'] if n.get('conservativeContactPairs', 0)],
                  writtenAssetsSha256={p.relative_to(ROOT).as_posix(): sha(p) for p in WRITTEN},
                  generatorSha256=sha(Path(__file__)), sourceAuditSha256=sha(source_path),
                  limitations=['Hidden rear/end geometry, height and compact medical depth are inferred.',
                               'Medicalright includes actual source cases despite the EmptyAlt prototype name.',
                               'Context contacts are conservative part bounds; unknown neighbors remain unmodeled.',
                               'Static snapshot checks do not prove live multiplayer appearance or deletion behavior.'])
    if not args.skip_reviews:
        report['reviewSha256'] = {p.relative_to(ROOT).as_posix(): sha(p) for p in REVIEW.glob('*.png')}
    json_write(GEN / 'container-sections-proof.json', report)
    print(json.dumps(dict(**report['counts'], contexts=report['contexts'])))


if __name__ == '__main__':
    main()
