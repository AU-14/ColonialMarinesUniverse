"""Reproduce two source-specific static secure-case drafts and their saved contexts."""
from collections import Counter
from copy import deepcopy
from io import BytesIO
from pathlib import Path
import argparse
import hashlib
import json
import math
import struct
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
from author_wide_machinery import world_parts, box_bounds
from scene import normalize_tint

SOURCES = {'RMCSecureCaseChest': 'chest', 'RMCSecureCaseMini': 'minicase'}
RSI_ROOT = ROOT / 'Resources/Textures/_RMC14/Structures/Storage/Crates'
PROTOTYPES = ROOT / 'Resources/Prototypes/_RMC14/Entities/Structures/Storage/securecrates.yml'
OWNER = ROOT / 'Content.Shared/_RMC14/Crate/CrateOpenableSystem.cs'
BASELINE = ROOT / '.codex/model-batch-baseline978'
MODEL = ROOT / 'Content.CMU/Resources/ThreeD/Prototypes/World/garrison_secure_cases.yml'
ART = MODEL.with_name('garrison_secure_cases_art.yml')
TEXTURES = ROOT / 'Content.CMU/Resources/Textures/CMU14/ThreeD/SecureCases'
NOTE = ROOT / 'Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_SECURE_CASES.md'
GEN = ROOT / 'Tools/three_d/generated'
REVIEW = GEN / 'review/secure-cases'
CHECK, WRITTEN = False, []


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write(path, data):
    if CHECK:
        assert path.is_file() and path.read_bytes() == data, f'Generated asset differs: {path}'
    else:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
    WRITTEN.append(path)


def png(image):
    output = BytesIO()
    image.save(output, format='PNG')
    return output.getvalue()


def json_write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')


def vector(values):
    return ', '.join(f'{v:.8f}'.rstrip('0').rstrip('.') if v else '0' for v in values)


def box(parts, label, color, low, high, **kwargs):
    parts.append(dict(label=label, color=color, min=list(low), max=list(high), **kwargs))


class Pool:
    def __init__(self):
        self.entries, self.proofs, self.cache = [], [], {}
        paths = [p for p in MODEL.parent.glob('*.yml') if p != ART]
        for folder in (ROOT / '.codex').glob('*staged*'):
            paths.extend(folder.rglob('*.yml'))
        for path in paths:
            for row in yaml.load(path.read_text(encoding='utf-8-sig'), Loader=yaml.CSafeLoader) or []:
                if row.get('type') == 'cmu3DSurface':
                    assert not 2400 <= row['atlasIndex'] <= 2499, f'Secure-case atlas conflict: {path}'

    def crop(self, source, image, role, rectangle):
        crop = image.crop(rectangle)
        digest = hashlib.sha256(crop.tobytes()).hexdigest()
        key = (crop.size, digest)
        if key not in self.cache:
            index = 2400 + len(self.entries)
            assert index <= 2499
            uid = f'CMU3DSecureCaseArt{index}'
            file = TEXTURES / (uid + '.png')
            write(file, png(crop))
            self.entries.append(dict(type='cmu3DSurface', id=uid, atlasIndex=index,
                                     texture='/Textures/CMU14/ThreeD/SecureCases/' + file.name))
            self.cache[key] = uid
        uid = self.cache[key]
        actual = Image.open(TEXTURES / (uid + '.png')).convert('RGBA')
        assert actual.size == crop.size and actual.tobytes() == crop.tobytes()
        self.proofs.append(dict(source=source, role=role, rectangle=list(rectangle), surface=uid,
                                rgbaSha256=digest, exactOriginalPixels=True))
        return uid


def chest(image, pool):
    p, south, north, top = [], -.4375, .09375, .375
    box(p, 'yellow chest body', '#9C8224', (-.34375, south + .022, .012), (.34375, north - .018, .34375))
    box(p, 'source chest front', '#FFFFFF', (-.375, south + .008, .012), (.375, south + .023, .34375),
        surface=pool.crop('chest', image, 'front', (4, 19, 28, 30)), surfaceAxis='XZ')
    box(p, 'closed yellow chest lid', '#FFFFFF', (-.375, south + .008, .34375), (.375, north, top),
        surface=pool.crop('chest', image, 'lid', (4, 2, 28, 19)), surfaceAxis='XY')
    for a, b in ((7, 9), (23, 25)):
        x0, x1 = (a - 16) / 32, (b - 16) / 32
        box(p, 'raised metal lid band', '#FFFFFF', (x0, south + .008, top), (x1, north, .393),
            surface=pool.crop('chest', image, 'top band', (a, 2, b, 19)), surfaceAxis='XY')
        box(p, 'front metal band', '#FFFFFF', (x0, south + .001, .012), (x1, south + .01, .34375),
            surface=pool.crop('chest', image, 'front band', (a, 19, b, 30)), surfaceAxis='XZ')
        box(p, 'rear metal band', '#5A5454', (x0, north - .02, .012), (x1, north, .375))
    for x in (-.36, .32875):
        for y in (south + .022, north - .05):
            box(p, 'reinforced chest corner', '#2F2E2E', (x, y, 0), (x + .03125, y + .028, .354))
    for a, b in ((6, 9), (23, 26)):
        box(p, 'projecting metal hasp', '#FFFFFF', ((a - 16) / 32, south, .065),
            ((b - 16) / 32, south + .012, .15875),
            surface=pool.crop('chest', image, 'hasp', (a, 25, b, 28)), surfaceAxis='XZ')
    # A real U grip follows the dark handle in the front source crop. Its center remains open.
    for x in (-.15625, .15625):
        box(p, 'handle attachment', '#252424', (x, south, .0975), (x + .03125, south + .012, .20625))
    box(p, 'horizontal carrying grip', '#252424', (-.15625, south, .065), (.1875, south + .012, .09625))
    for x in (-.26, .22):
        box(p, 'rear lid hinge', '#5A5454', (x, north - .019, .324), (x + .04, north, .379))
    return p


def mini(image, pool):
    p, south, north, top = [], -.375, -.09375, .27875
    box(p, 'blue black mini case body', '#303C48', (-.1975, south + .02, .012), (.1975, north - .018, .245))
    box(p, 'source mini case front', '#FFFFFF', (-.21875, south + .006, .012), (.21875, south + .021, .245),
        surface=pool.crop('minicase', image, 'front', (9, 20, 23, 28)), surfaceAxis='XZ')
    box(p, 'vented mini case lid', '#FFFFFF', (-.21875, south + .006, .245), (.21875, north, top),
        surface=pool.crop('minicase', image, 'vented lid', (9, 11, 23, 20)), surfaceAxis='XY')
    # Raised ribs use their exact source strips. Dark vent channels stay between them.
    for a in (11, 14, 17, 20):
        y0 = north - 7 / 9 * (north - south - .006)
        y1 = north - 2 / 9 * (north - south - .006)
        box(p, 'raised vent rib', '#FFFFFF', ((a - 16) / 32, y0, top),
            ((a - 15) / 32, y1, .28875),
            surface=pool.crop('minicase', image, 'vent rib', (a, 13, a + 1, 18)), surfaceAxis='XY')
    for a, b in ((10, 11), (21, 22)):
        box(p, 'brown lid catch', '#8B552E', ((a - 16) / 32, north - .12, .28),
            ((b - 16) / 32, north - .06, .29))
    for x in (-.21875, .19):
        for y in (south + .016, north - .04):
            box(p, 'mini case corner bumper', '#11171C', (x, y, 0), (x + .02875, y + .024, .255))
    box(p, 'amber front retaining strip', '#A78612', (-.15625, south + .001, .166), (.1875, south + .008, .196))
    box(p, 'front handle recess', '#262E36', (-.0625, south + .001, .065), (.0625, south + .007, .123))
    box(p, 'small silver handle', '#9C9C9C', (-.03125, south, .077), (.03125, south + .008, .10825))
    for x in (-.14, .1):
        box(p, 'rear lid hinge', '#414142', (x, north - .015, .225), (x + .04, north, .28))
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
            elif prototype in SOURCES:
                if line.startswith('  - uid:'):
                    finish()
                    block = [line]
                elif block:
                    block.append(line)
    finish()
    return result


def contacts(first, second):
    result = []
    for i, a in enumerate(first):
        low, high = box_bounds(a)
        for j, b in enumerate(second):
            lo, hi = box_bounds(b)
            overlap = [min(high[k], hi[k]) - max(low[k], lo[k]) for k in range(3)]
            if min(overlap) > 1e-6:
                result.append(dict(partIndex=i, part=a['label'], neighborPartIndex=j,
                                   neighborPart=b.get('label'), overlap=overlap))
    return result


def contexts(models, components, render):
    by_proto = {m['referencePrototype']: m for m in models}
    index = {m['id']: m for m in json.loads((BASELINE / 'models.json').read_text())['models']}
    index.update({m['id']: m for m in models})
    records, hashes = [], {}
    for spec in json.loads((BASELINE / 'scenes.json').read_text()):
        path = BASELINE / spec['file']
        document = json.loads(path.read_text())
        targets = [e for e in document['instances'] if e['prototype'] in SOURCES]
        if not targets:
            continue
        raw_path = ROOT / document['map']['path']
        raw = saved_records(raw_path)
        for input_path in (path, raw_path):
            hashes[input_path.relative_to(ROOT).as_posix()] = sha(input_path)
        for item in targets:
            item.update(modelId=by_proto[item['prototype']]['id'], renderYaw=0, matchKind='exact')
        for item in targets:
            model = by_proto[item['prototype']]
            overrides = {c['type']: c for c in raw[item['id']].get('components', [])}
            state, reason = sprite_states.saved_pose(model, components[item['prototype']], overrides, normalize_tint)
            assert state == 'base' and reason is None, (item['id'], reason)
            assert item['yaw'] == 0 and not overrides.get('Sprite')
            own = world_parts(model['parts'], item['position'], 0)
            neighbors, picture = [], deepcopy(own)
            for other in document['instances']:
                if other['id'] == item['id'] or other['position'][2] != item['position'][2]:
                    continue
                if max(abs(other['position'][a] - item['position'][a]) for a in (0, 1)) > 2.1:
                    continue
                entry = {k: other.get(k) for k in ('id', 'prototype', 'position', 'yaw', 'modelId', 'renderOffset')}
                entry['exactCoLocation'] = other['position'] == item['position']
                target = index.get(other.get('modelId'))
                if target:
                    parts = document.get('geometryVariants', {}).get(other.get('geometryKey'), target['parts'])
                    offset = other.get('renderOffset', [*target.get('groundOffset', [0, 0]), 0])
                    placed = world_parts(parts, other['position'], other.get('renderYaw', other['yaw']), offset)
                    hits = contacts(own, placed)
                    entry.update(conservativeContactPairs=len(hits), contactPairs=hits)
                    picture.extend(placed)
                neighbors.append(entry)
            record = dict(variant=spec['variant'], level=spec['level'], id=item['id'], prototype=item['prototype'],
                          position=item['position'], savedYaw=item['yaw'], renderYaw=0, modelId=model['id'],
                          sourceState=state, sourceGatePassed=True, savedComponents=raw[item['id']].get('components', []),
                          supportSurfaceDeclared=False, neighborsWithin2_1=neighbors)
            records.append(record)
            if render:
                local = deepcopy(picture)
                for part in local:
                    for bound in ('min', 'max'):
                        for axis in (0, 1):
                            part[bound][axis] -= item['position'][axis]
                card = Image.new('RGB', (1100, 500), (23, 33, 43))
                draw = ImageDraw.Draw(card)
                draw.text((12, 10), f'{spec["variant"]} level {spec["level"]}, saved UID {item["id"]}; all modeled neighbors retained', fill='white')
                for col, yaw in enumerate((-math.pi / 2, -.6)):
                    im = bm.render_model({'parts': local}, (540, 450), yaw, .9, pixels_per_unit=113, screen_origin=(270, 295))
                    card.paste(im, (col * 550, 35))
                card.save(REVIEW / f'context-{spec["variant"]}-{spec["level"]}-{item["id"]}.png')
    assert Counter(r['prototype'] for r in records) == {'RMCSecureCaseChest': 11, 'RMCSecureCaseMini': 10}
    assert Counter(r['level'] for r in records) == {0: 5, -1: 5, -2: 4, 1: 7}
    if render:
        montage = Image.new('RGB', (2200, 1500), (23, 33, 43))
        for i, r in enumerate(records):
            card = Image.open(REVIEW / f'context-{r["variant"]}-{r["level"]}-{r["id"]}.png')
            montage.paste(card.resize((550, 250)), (i % 4 * 550, i // 4 * 250))
        montage.save(REVIEW / 'context-montage.png')
    for path in (BASELINE / 'models.json', BASELINE / 'scenes.json'):
        hashes[path.relative_to(ROOT).as_posix()] = sha(path)
    return records, hashes


def reviews(models, images):
    sheet = Image.new('RGB', (1450, 850), (23, 33, 43))
    for i, model in enumerate(models):
        card = Image.new('RGB', (1450, 425), (23, 33, 43))
        ImageDraw.Draw(card).text((12, 10), model['referencePrototype'] + ' / source, south, oblique, rear; physical hidden depth inferred', fill='white')
        original = images[model['referencePrototype']].resize((256, 256), Image.Resampling.NEAREST)
        card.paste(original, (15, 110), original)
        for col, (yaw, pitch) in enumerate(((-math.pi / 2, .85), (-.6, .55), (math.pi / 2, .75))):
            card.paste(bm.render_model(model, (370, 365), yaw, pitch), (290 + col * 385, 50))
        card.save(REVIEW / (model['id'] + '.png'))
        sheet.paste(card, (0, i * 425))
    sheet.save(REVIEW / 'comparison-montage.png')


def serialized(model):
    model = deepcopy(model)
    for key in ('groundOffset', 'sourceSpriteOffset'):
        model[key] = vector(model[key])
    for parts in [model['parts'], model['spriteStates']['base']['frames'][0]['parts']]:
        for part in parts:
            part['min'], part['max'] = vector(part['min']), vector(part['max'])
    return model


def main():
    global CHECK
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--skip-reviews', action='store_true')
    parser.add_argument('--skip-context', action='store_true')
    args = parser.parse_args()
    CHECK = args.check
    pool, models, source_records, images = Pool(), [], [], {}
    kinds, _, _ = inventory.load_prototypes(ROOT)
    resolver = inventory.Resolver(kinds['entity'])
    components = {uid: inventory.component_map(resolver.resolve(uid)) for uid in SOURCES}
    for uid, folder in SOURCES.items():
        path = RSI_ROOT / (folder + '.rsi')
        meta = json.loads((path / 'meta.json').read_text())
        assert meta['size'] == {'x': 32, 'y': 32} and meta['states'] == [{'name': 'base'}]
        sprite = components[uid]['Sprite']
        assert sprite['sprite'] == f'_RMC14/Structures/Storage/Crates/{folder}.rsi' and sprite['noRot'] is True
        assert sprite.get('offset', '0, 0') == '0, 0' and sprite['layers'] == [{'state': 'base', 'map': ['enum.StorageVisualLayers.Base']}]
        assert components[uid]['PlaceableSurface']['isPlaceable'] is False
        image = Image.open(path / 'base.png').convert('RGBA')
        assert image.size == (32, 32)
        images[uid] = image
        parts = chest(image, pool) if folder == 'chest' else mini(image, pool)
        palette = {'#' + ''.join(f'{int(v):02X}' for v in p[:3]) for p in np.array(image).reshape((-1, 4)) if p[3]}
        assert all(p['color'] in palette for p in parts if not p.get('surface'))
        model = dict(type='cmu3DModel', id='CMU3D' + uid, label='Yellow secure chest' if folder == 'chest' else 'Mini secure case',
                     status='draft', sourcePrototypes=[uid], referencePrototype=uid,
                     referenceRsi='/Textures/' + sprite['sprite'], referenceState='base', sourceDirections=1,
                     placement='floor', useEntityRotation=False, groundOffset=[0, 0], sourceSpriteOffset=[0, 0],
                     parts=parts, spriteStates={'base': dict(frames=[dict(parts=deepcopy(parts))], delays=[1])},
                     description='Source-specific closed case with separate physical lid, shell and hardware. Original source pixels, palette, noRot and entity pivot retained; hidden depth, underside and metal thickness inferred. Opening deletes the entity and spawns contents; no lid animation is invented. See SOURCES_SECURE_CASES.md.')
        models.append(model)
        source_records.append(dict(prototype=uid, sourceFile=(path / 'base.png').relative_to(ROOT).as_posix(),
                                   sourceSha256=sha(path / 'base.png'), metaSha256=sha(path / 'meta.json'),
                                   originalRgbaSha256=hashlib.sha256(image.tobytes()).hexdigest(),
                                   sourceMetadata=meta, sourceBounds=list(image.getbbox()), sourceNoRot=True, sourceOffset=[0, 0],
                                   frames=1, directions=1, components=components[uid],
                                   sourceFloorInterpretation='Original opaque X columns and bottom row anchor the footprint; projected lid-row depth and hidden height are explicitly inferred.'))
    configure_surfaces(pool)
    models = [bm.validate_model(m) for m in models]
    for path, rows in ((MODEL, [serialized(m) for m in models]), (ART, pool.entries)):
        write(path, ('# Generated by Tools/three_d/author_secure_cases.py; exact source drafts.\n' +
                     yaml.safe_dump(rows, sort_keys=False, width=115)).encode('utf-8'))
    actual, glbs, references = bm.load_models(MODEL), {}, []
    for model in actual:
        sprite_states.validate_source(model, bm.resource_file)
        data = bm.glb_bytes(model)
        glbs[model['id']] = hashlib.sha256(data).hexdigest()
        length = struct.unpack_from('<I', data, 12)[0]
        assert not json.loads(data[20:20 + length]).get('animations')
        outputs, _ = sprite_states.viewer_references(model, bm.resource_file, png)
        reference = Image.open(BytesIO(next(iter(outputs.values())))).convert('RGBA')
        assert reference.tobytes() == images[model['referencePrototype']].tobytes()
        references.append(dict(modelId=model['id'], fullOriginalRgbaPixels=1024, exactSource=True))
    note = '''# Secure chest and mini-case drafts

Two exact mappings cover 21 visible Redux placements: 11 RMCSecureCaseChest and 10 RMCSecureCaseMini, with no classic placements. The source is one static 32 by 32 base frame in each of _RMC14/Structures/Storage/Crates/chest.rsi and minicase.rsi. Both Sprite and Transform use noRot. Source offset is zero, and all saved transforms remain intact. A strict one-frame source composition is provided; no opening animation is authored.

The yellow chest has a separate shell and broad lid, two raised metal bands that continue around the front and rear, reinforced corner members, projecting hasps, and an open U-shaped carrying grip. Original lid and front pixels are placed on their corresponding horizontal and vertical surfaces. The mini case is a narrower blue-black shell with a physical vented lid: original vent stripes sit on raised ribs with dark channels between them. Amber trim, brown lid catches and the small silver handle retain the source palette. Hidden rear faces use only source colors. Neither model is an extruded full-sprite plate.

Footprint X coordinates preserve source columns at one pixel per 1/32 tile: chest [-0.375, 0.375], mini [-0.21875, 0.21875]. The source bottom rows place their southern ground edges at -0.4375 and -0.375. Depth reconstructed from the projected lid spans is inferred: chest north edge 0.09375 and mini north edge -0.09375. Chest height reaches 0.393; mini reaches 0.29. Thickness, hidden sides/undersides, grip projection, hinges and the physical lid depth are reconstruction choices, not claims of source-proven dimensions. The inherited gameplay collider is left unchanged.

CrateOpenableSystem handles a pry tool by deleting the source entity and spawning contents. Its source RSI has no opened lid state. SpawnOnTerminate also defines loot behavior. This static batch preserves the closed object and leaves that gameplay ownership untouched; runtime loot is outside the saved-map audit.

PlaceableSurface.isPlaceable is false. No 3D supportSurface is declared. Chest UID 4792 on level +1 shares its center with still-unmapped RMCSmallChestRed UID 5162, so that unresolved composition is explicitly retained in the context report. Two level -1 chests share catwalk/water layers; those floor-cladding contacts remain visible, without a blanket lift that would change ordinary-floor cases. All 21 contexts include both new mappings and existing modeled neighbors, and retain unknown neighbor records. Conservative part bounds can overcount curved or transparent contacts; no other entity is hidden or moved.

Original art license: CC-BY-SA-3.0. Attribution from both RSI metadata files: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/crates.dmi. Preserve this attribution and source license for all derived crops. Original PNGs are not modified.

Regenerate only these dedicated assets with `python Tools/three_d/author_secure_cases.py`; verify byte identity with `--check --skip-context --skip-reviews`. The generator does not export the global library, build native projects, or launch a game or server.
'''
    write(NOTE, note.encode('utf-8'))
    scalar_count = Counter()
    for model in yaml.load(MODEL.read_text(), Loader=yaml.CSafeLoader):
        for key in ('groundOffset', 'sourceSpriteOffset'):
            assert isinstance(model[key], str) and len(model[key].split(',')) == 2
            scalar_count['vector2'] += 1
        for parts in [model['parts'], model['spriteStates']['base']['frames'][0]['parts']]:
            for part in parts:
                for key in ('min', 'max'):
                    assert isinstance(part[key], str) and len(part[key].split(',')) == 3
                    scalar_count['vector3'] += 1
    if CHECK:
        print(json.dumps(dict(byteIdentical=True, models=2, checkedAssetFiles=len(WRITTEN))))
        return
    REVIEW.mkdir(parents=True, exist_ok=True)
    if not args.skip_reviews:
        reviews(actual, images)
    context, hashes = ([], {}) if args.skip_context else contexts(actual, components, not args.skip_reviews)
    source_path = GEN / 'secure-cases-source-audit.json'
    json_write(source_path, dict(schemaVersion=1, sources=source_records, contexts=context, contextInputSha256=hashes,
                                sourcePrototypeSha256=sha(PROTOTYPES), controllerFile=OWNER.relative_to(ROOT).as_posix(), controllerSha256=sha(OWNER)))
    proof = dict(schemaVersion=1, assetChecksPass=True, counts=dict(models=2, exactMappings=2, reduxPlacements=21,
                 classicPlacements=0, parts=sum(len(m['parts']) for m in actual), textures=len(pool.entries)),
                 modelIds=[m['id'] for m in actual], atlasIndices=[e['atlasIndex'] for e in pool.entries],
                 sourcePixelCrops=pool.proofs, referenceChecks=references, untexturedColorsFromOriginalPalette=True,
                 scalarVectors=dict(scalar_count), sourceFrameGatesPassed=sum(r['sourceGatePassed'] for r in context),
                 savedTransformCount=len(context), dedicatedGlbSha256=glbs,
                 conservativeContactNeighbors=[dict(level=r['level'], id=r['id'], neighborId=n['id'],
                                                    neighborPrototype=n['prototype'], partPairs=n['conservativeContactPairs'])
                                               for r in context for n in r['neighborsWithin2_1'] if n.get('conservativeContactPairs')],
                 unmappedCoLocations=[dict(level=r['level'], id=r['id'], neighborId=n['id'], neighborPrototype=n['prototype'])
                                      for r in context for n in r['neighborsWithin2_1'] if n['exactCoLocation'] and not n['modelId']],
                 writtenAssetsSha256={p.relative_to(ROOT).as_posix(): sha(p) for p in WRITTEN},
                 generatorSha256=sha(Path(__file__)), sourceAuditSha256=sha(source_path),
                 limitations=['Hidden dimensions and lid-depth reconstruction are inferred.',
                              'Unknown co-located red case remains unresolved; no support surface is invented.',
                              'Floor-cladding and other conservative contact candidates are explicitly retained.',
                              'Static source parity does not validate runtime spawning, live gameplay or GPU presentation.'])
    if not args.skip_reviews:
        proof['reviewSha256'] = {p.relative_to(ROOT).as_posix(): sha(p) for p in REVIEW.glob('*.png')}
    json_write(GEN / 'secure-cases-proof.json', proof)
    print(json.dumps(dict(**proof['counts'], contacts=proof['conservativeContactNeighbors'], unresolvedCoLocations=proof['unmappedCoLocations'])))


if __name__ == '__main__':
    main()
