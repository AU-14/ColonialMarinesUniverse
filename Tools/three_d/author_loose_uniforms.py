"""Source-specific loose garments. Worn sprite resources and gameplay remain untouched."""
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
from PIL import Image, ImageDraw, ImageFont
import yaml

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'Tools/three_d'))
import build_models as bm
import inventory
import placement
import scene
import sprite_states
import surfaces
from author_wide_machinery import world_parts, box_bounds

BASE = ROOT / '.codex/model-batch-baseline994'
GEN = ROOT / 'Tools/three_d/generated'
REVIEW = GEN / 'review/loose-uniforms'
MODEL = ROOT / 'Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_loose_uniforms.yml'
ART = MODEL.with_name('garrison_loose_uniforms_art.yml')
TEXTURES = ROOT / 'Content.CMU/Resources/Textures/CMU14/ThreeD/LooseUniforms'
NOTE = ROOT / 'Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_LOOSE_UNIFORMS.md'
SPECS = {
    'AU14CivilianPrisonJumpsuit': ('CMU14/Clothing/Civilian/Uniforms/prisonsuit.rsi', 8, 16),
    'AU14CivilianKellandMiningClothes': ('CMU14/Clothing/Civilian/Uniforms/kellandmininguniform.rsi', 10, 20),
    'RMCSwatCMBUniform': ('_RMC14/Objects/Clothing/Uniforms/CMB/cmb_swat_uniform.rsi', 9, 18),
}
CHECK = False
WRITTEN = []


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(',', ':'))


def write(path, data):
    if CHECK:
        assert path.is_file() and path.read_bytes() == data, f'Generated asset differs: {path}'
    else:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
    WRITTEN.append(path)


def png(image):
    buffer = BytesIO()
    image.save(buffer, format='PNG')
    return buffer.getvalue()


def save_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')


class Pool:
    def __init__(self):
        self.entries, self.crops, self.cache = [], [], {}
        for path in MODEL.parent.glob('*.yml'):
            if path == ART:
                continue
            for row in yaml.load(path.read_text(encoding='utf-8-sig'), Loader=yaml.CSafeLoader) or []:
                assert row.get('type') != 'cmu3DSurface' or not 2700 <= row['atlasIndex'] <= 2799, f'Atlas conflict: {path}'

    def crop(self, prototype, image, rect, turn):
        crop = image.crop(rect)
        assert crop.getchannel('A').getextrema() == (255, 255)
        if turn:
            crop = crop.transpose(Image.Transpose.ROTATE_180)
        key = hashlib.sha256(str(crop.size).encode() + crop.tobytes()).hexdigest()
        if key not in self.cache:
            index = 2700 + len(self.entries)
            assert index <= 2799
            uid = f'CMU3DLooseUniformSurface{index}'
            self.cache[key] = uid
            write(TEXTURES / f'{uid}.png', png(crop))
            self.entries.append(dict(type='cmu3DSurface', id=uid, atlasIndex=index,
                texture=f'/Textures/CMU14/ThreeD/LooseUniforms/{uid}.png'))
        uid = self.cache[key]
        actual = Image.open(TEXTURES / f'{uid}.png').convert('RGBA')
        assert actual.size == crop.size and actual.tobytes() == crop.tobytes()
        self.crops.append(dict(prototype=prototype, rect=list(rect), surface=uid, inverseArtworkRotation=180 if turn else 0,
            rgbaSha256=hashlib.sha256(crop.tobytes()).hexdigest()))
        return uid


def region(x, y, top, waist):
    """Explicit inferred cloth relief: collar, rolled sleeves, shirt and folded trousers."""
    if y < top + 2:
        return ('left collar lip' if x < 15 else 'right collar lip', .065, 'Box', 0)
    if y < waist:
        if x < 12 or x > 18:
            outside = x <= 10 or x >= 20
            return ('left sleeve cuff' if x < 15 else 'right sleeve cuff', .025 if outside else .039, 'Box', 0)
        if x in (14, 15, 16):
            return ('central shirt fold', .050, 'Box', 0)
        return ('left shirt drape' if x < 15 else 'right shirt drape', .050, 'WedgeY', 0)
    if y == waist:
        return ('waist fold seam', .030, 'Box', 0)
    if x < 12 or x > 18:
        return ('outer folded trouser hem', .033, 'Box', 0)
    return ('left folded trouser panel' if x < 15 else 'right folded trouser panel', .058, 'WedgeYReverse', 0)


def rectangles(image, top, waist):
    """Merge anatomical regions into maximal rectangles; never fill transparent gaps."""
    alpha = np.asarray(image)[:, :, 3] > 0
    claimed = np.zeros_like(alpha)
    result = []
    for y in range(image.height):
        for x in range(image.width):
            if not alpha[y, x] or claimed[y, x]:
                continue
            kind = region(x, y, top, waist)
            right = x + 1
            while right < image.width and alpha[y, right] and not claimed[y, right] and region(right, y, top, waist) == kind:
                right += 1
            bottom = y + 1
            while bottom < image.height and all(alpha[bottom, u] and not claimed[bottom, u] and region(u, bottom, top, waist) == kind for u in range(x, right)):
                bottom += 1
            claimed[y:bottom, x:right] = True
            result.append(((x, y, right, bottom), kind))
    assert np.array_equal(claimed, alpha)
    return result


def geometry(prototype, image, pool):
    _, top, waist = SPECS[prototype]
    parts, patches = [], []
    rebuilt = Image.new('RGBA', image.size)
    coverage = np.zeros((image.height, image.width), dtype=np.uint8)
    for index, (rect, (name, height, shape, turn)) in enumerate(rectangles(image, top, waist)):
        x0, y0, x1, y1 = rect
        low = [(x0 - 16) / 32, (16 - y1) / 32, 0]
        high = [(x1 - 16) / 32, (16 - y0) / 32, .006]
        # Closed cloth beneath the source-colored upper fold. Source's darkest local
        # color supplies its thin edge; no colored opaque volume extends beyond alpha.
        colors = [color for _, color in image.crop(rect).getcolors()]
        edge = min(colors, key=lambda c: sum(c[:3]))
        color = '#' + ''.join(f'{v:02X}' for v in edge[:3])
        parts.append(dict(label=f'{name} {index} lower cloth', min=low, max=high, color=color))
        uid = pool.crop(prototype, image, rect, turn)
        parts.append(dict(label=f'{name} {index} source fold', min=[*low[:2], .006], max=[*high[:2], height],
            color='#FFFFFF', surface=uid, surfaceAxis='XY', shape=shape, yaw=turn))
        crop = Image.open(TEXTURES / f'{uid}.png').convert('RGBA')
        if turn:
            crop = crop.transpose(Image.Transpose.ROTATE_180)
        rebuilt.paste(crop, (x0, y0))
        coverage[y0:y1, x0:x1] += 1
        patches.append(dict(rect=list(rect), anatomy=name, maxZ=height, shape=shape, yaw=turn, surface=uid))
    assert rebuilt.tobytes() == image.tobytes()
    assert np.array_equal(coverage, (np.asarray(image)[:, :, 3] > 0).astype(np.uint8))
    assert len(parts) <= 128
    return parts, dict(prototype=prototype, parts=len(parts), sourceBounds=list(image.getbbox()),
        sourceOpaquePixels=int(coverage.sum()), exactSourcePlanPixelReconstruction=True,
        originalRgbaSha256=hashlib.sha256(image.tobytes()).hexdigest(), sourcePatches=patches,
        inference='Loose garment laid in the source icon footprint. Collar lips, cuff rolls, shirt drapes and folded trouser height are inferred; not a worn body or cloth simulation.')


def serialize(models):
    result = deepcopy(models)
    def vector(values):
        return ', '.join(f'{v:.8f}'.rstrip('0').rstrip('.') if v else '0' for v in values)
    for model in result:
        for key in ('groundOffset', 'sourceSpriteOffset'):
            model[key] = vector(model[key])
        for parts in [model['parts'], model['spriteStates']['icon']['frames'][0]['parts']]:
            for part in parts:
                part['min'], part['max'] = vector(part['min']), vector(part['max'])
    return result


def configure_surfaces():
    registry = {}
    for path in MODEL.parent.glob('*.yml'):
        for row in yaml.load(path.read_text(encoding='utf-8-sig'), Loader=yaml.CSafeLoader) or []:
            if row.get('type') == 'cmu3DSurface':
                path = surfaces.texture_path(row['texture'])
                registry[row['id']] = {**row, 'file': path, 'image': Image.open(path).convert('RGBA')}
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
            elif prototype in SPECS:
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
        lo, hi = box_bounds(a)
        for j, b in enumerate(second):
            bl, bh = box_bounds(b)
            overlap = [min(hi[k], bh[k]) - max(lo[k], bl[k]) for k in range(3)]
            if min(overlap) > 1e-6:
                result.append(dict(partIndex=i, neighborPartIndex=j, overlap=overlap))
    return result


def contexts(models, defaults, render):
    library = {m['id']: m for m in json.loads((BASE / 'models.json').read_text())['models'] + models}
    by_proto = {m['referencePrototype']: m for m in models}
    records, excluded, inputs, groups = [], [], {}, set()
    for spec in json.loads((BASE / 'scenes.json').read_text()):
        path = BASE / spec['file']
        doc = json.loads(path.read_text())
        targets = [e for e in doc['instances'] if e['prototype'] in SPECS]
        raw = saved_records(ROOT / doc['map']['path'])
        if not raw:
            continue
        inputs[path.relative_to(ROOT).as_posix()] = sha(path)
        map_path = ROOT / doc['map']['path']
        inputs[map_path.relative_to(ROOT).as_posix()] = sha(map_path)
        visible = {e['id'] for e in targets}
        for uid, r in raw.items():
            if uid not in visible:
                components = {c['type']: c for c in r.get('components', [])}
                assert 'InsideEntityStorage' in components
                excluded.append(dict(variant=spec['variant'], level=spec['level'], id=uid, prototype=r['prototype'],
                    reason='InsideEntityStorage; not a loose visible garment', savedComponents=components))
        for entity in targets:
            m = by_proto[entity['prototype']]
            saved = {c['type']: c for c in raw[entity['id']].get('components', [])}
            assert set(saved) == {'Transform'} and entity['yaw'] == 0
            state, reason = sprite_states.saved_pose(m, defaults[entity['prototype']], saved, scene.normalize_tint)
            assert state == 'icon' and reason is None, (entity['id'], reason)
            entity.update(modelId=m['id'], matchKind='exact', renderYaw=entity['yaw'])
            entity.pop('geometryKey', None)
        # Existing placement rules are used; no duplicate stacking or scene rebuilding.
        placement.resolve_placements(doc['instances'], list(library.values()), doc.get('geometryVariants', {}))
        for entity in targets:
            model = by_proto[entity['prototype']]
            own = world_parts(model['parts'], entity['position'], entity['renderYaw'], entity.get('renderOffset', [0, 0, 0]))
            neighbors, picture = [], deepcopy(own)
            for other in doc['instances']:
                if other['id'] == entity['id'] or other['position'][2] != entity['position'][2]:
                    continue
                if max(abs(other['position'][k] - entity['position'][k]) for k in (0, 1)) > .8:
                    continue
                entry = dict(id=other['id'], prototype=other['prototype'], modelId=other.get('modelId'),
                    position=other['position'], exactDuplicate=other['prototype'] == entity['prototype'] and other['position'] == entity['position'])
                m = library.get(other.get('modelId'))
                if m:
                    parts = doc['geometryVariants'].get(other.get('geometryKey'), m['parts'])
                    placed = world_parts(parts, other['position'], other.get('renderYaw', other['yaw']), other.get('renderOffset', [0, 0, 0]))
                    # The map contains large exact duplicate piles. Their unchanged
                    # coordinates explain the overlap; avoid an exhaustive repeated
                    # garment-versus-garment conservative contact enumeration.
                    if other['prototype'] in SPECS:
                        entry['savedGarmentPile'] = True
                    else:
                        hits = contacts(own, placed)
                        entry.update(conservativePartPairs=len(hits), contacts=hits)
                    picture.extend(placed)
                neighbors.append(entry)
            records.append(dict(variant=spec['variant'], level=spec['level'], id=entity['id'], prototype=entity['prototype'],
                modelId=model['id'], position=entity['position'], savedYaw=entity['yaw'], renderYaw=entity['renderYaw'],
                renderOffset=entity.get('renderOffset', [0, 0, 0]), support=entity.get('support'), sourceState='icon',
                sourceGatePassed=True, savedComponents=raw[entity['id']].get('components', []), neighbors=neighbors))
            key = (spec['variant'], spec['level'], tuple(entity['position']))
            if render and key not in groups:
                groups.add(key)
                for p in picture:
                    for bound in ('min', 'max'):
                        p[bound][0] -= entity['position'][0]
                        p[bound][1] -= entity['position'][1]
                card = Image.new('RGB', (1000, 480), '#17212B')
                draw = ImageDraw.Draw(card)
                draw.text((12, 10), f'{spec["variant"]} {spec["level"]:+d} / loose garment {entity["id"]}; source duplicates retained', fill='white')
                for col, yaw in enumerate((-math.pi/2+.3, -.1)):
                    card.paste(bm.render_model({'parts': picture}, (490, 425), yaw, .85,
                        pixels_per_unit=240, screen_origin=(245, 335)), (col*500, 40))
                card.save(REVIEW / f'context-{spec["variant"]}-{spec["level"]}-{entity["id"]}.png')
    assert len(records) == 57 and len(excluded) == 46
    assert Counter(r['variant'] for r in records) == {'redux': 48, 'classic': 9}
    return records, excluded, inputs


def reviews(models, images):
    montage = Image.new('RGB', (1340, 1050), '#17212B')
    font = ImageFont.load_default(size=16)
    for n, m in enumerate(models):
        card = Image.new('RGB', (1340, 350), '#17212B')
        draw = ImageDraw.Draw(card)
        draw.text((12, 8), f'{m["referencePrototype"]} / original icon, oblique cloth, low side, overhead', fill='white', font=font)
        original = Image.new('RGBA', (256, 256), '#62727C')
        original.alpha_composite(images[m['referencePrototype']].resize((256, 256), Image.Resampling.NEAREST))
        card.paste(original.convert('RGB'), (12, 65))
        for col, (yaw, pitch) in enumerate(((-1.15, .7), (-.35, .23), (-math.pi/2, math.pi/2-.001))):
            card.paste(bm.render_model(m, (350, 295), yaw, pitch, pixels_per_unit=560, screen_origin=(175, 155)), (280+350*col, 45))
        card.save(REVIEW / f'{m["id"]}.png')
        montage.paste(card, (0, n*350))
    montage.save(REVIEW / 'comparison-montage.png')


def main():
    global CHECK
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--skip-context', action='store_true')
    parser.add_argument('--skip-reviews', action='store_true')
    args = parser.parse_args()
    CHECK = args.check
    REVIEW.mkdir(parents=True, exist_ok=True)
    kinds, _, _ = inventory.load_prototypes(ROOT)
    resolver = inventory.Resolver(kinds['entity'])
    defaults = {uid: inventory.component_map(resolver.resolve(uid)) for uid in SPECS}
    pool, models, references, images = Pool(), [], [], {}
    for uid, (rsi, _, _) in SPECS.items():
        sprite = defaults[uid]['Sprite']
        assert sprite['sprite'] == rsi and sprite['noRot'] is False and not sprite.get('offset')
        assert sprite['layers'] == [{'state': 'icon'}, {'map': ['enum.WebbingVisualLayers.Base']}]
        assert defaults[uid]['WebbingClothing'] == {'type': 'WebbingClothing'}
        path = bm.resource_file(rsi)
        meta = json.loads((path/'meta.json').read_text())
        state = next(s for s in meta['states'] if s['name'] == 'icon')
        assert state == {'name': 'icon'} and meta['size'] == {'x': 32, 'y': 32}
        image = Image.open(path/'icon.png').convert('RGBA')
        images[uid] = image
        parts, evidence = geometry(uid, image, pool)
        models.append(dict(type='cmu3DModel', id='CMU3DLoose'+uid, label=resolver.resolve(uid).get('name', uid),
            status='draft', sourcePrototypes=[uid], referencePrototype=uid, referenceRsi=rsi, referenceState='icon',
            referenceDirection=0, sourceDirections=1, sourceSpriteOffset=[0, 0], sourceSpriteRotates=True,
            useEntityRotation=True, placement='surface', groundOffset=[0, 0],
            description='Loose world garment with closed cloth folds, collar opening, cuffs and folded lower panels. Exact source pixels retain the icon plan footprint; height and hidden folds are inferred. Worn appearance is untouched. See SOURCES_LOOSE_UNIFORMS.md.',
            parts=parts, spriteStates={'icon': dict(frames=[dict(parts=deepcopy(parts))], delays=[1])}))
        references.append(dict(**evidence, sourceRsi=rsi, sourceState='icon', sourceDirections=1, sourceFrames=1,
            sourceNoRot=False, sourceOffset=[0, 0], sourceSprite=sprite, sourceWebbing=defaults[uid]['WebbingClothing'],
            sourcePng=(path/'icon.png').relative_to(ROOT).as_posix(), sourcePngSha256=sha(path/'icon.png'),
            metadata=meta, metadataSha256=sha(path/'meta.json')))
    write(MODEL, ('# Generated by Tools/three_d/author_loose_uniforms.py; world clothing only.\n'+yaml.safe_dump(serialize(models), sort_keys=False, width=115)).encode())
    write(ART, ('# Original source pixel crops; preserve source CC-BY-SA-3.0 attribution.\n'+yaml.safe_dump(pool.entries, sort_keys=False)).encode())
    source_owners = [ROOT/'Content.Client/_RMC14/Webbing/WebbingSystem.cs', ROOT/'Content.Shared/_RMC14/Clothing/RMCClothingSystem.cs']
    note = '''# Loose world uniform drafts

Three exact world sources: AU14CivilianPrisonJumpsuit, AU14CivilianKellandMiningClothes and RMCSwatCMBUniform. These are dropped items, never replacements for character or equipped clothing sprites. The original icon is static, one direction, 32 x 32, noRot=false and offset zero. Saved source positions and yaw remain unchanged. The existing authored surface resolver may rest a garment on an actual table or rack; no support surface or duplicate stack height is invented.

The compact source silhouettes are interpreted as loose uniforms lying with folded lower sections. Separate collar lips retain the transparent neck opening. Short cuff volumes, two sloping shirt drapes, a central fold, waist seam and separate lower folded panels have closed physical thickness. Anatomical patches partition every opaque source pixel exactly once. Original crop colors and the planar silhouette are preserved, including the prison orange fabric, Kelland gray shirt/red high-visibility bands/brown lower bundle, and dark CMB uniform with pale shoulder detail. Hidden folds, cloth thickness (0.006 to 0.065 tile) and the meaning of the lower bundle are inferred. Exact source-plan reconstruction does not claim an arbitrary 3D camera reproduces the 2D sprite; this is a draft, not cloth simulation.

Every target has an empty WebbingVisualLayers.Base placeholder. Client WebbingSystem hides it when no webbing is attached and supplies a real RSI/state when attached. Only the plain icon composition is authored. The strict source-state adapter must fall back when added webbing/artwork is visible or an unsupported source transform/state appears. The clothing fold owner changes the equipped prefix; no dropped-item animation, worn state or equipment effect is invented.

The frozen 994-model maps contain 57 visible targets: Redux has 26 mining uniforms, 12 prison suits and 10 SWAT uniforms; classic has two mining uniforms and seven SWAT uniforms. Another 46 prison suits (23 in each surface map) are InsideEntityStorage and remain excluded. Exact duplicate piles and closely overlapping saved garments stay co-located; they are not silently spread, raised or deleted. Modeled contact candidates and unmodeled clothing/helmet neighbors remain explicit in loose-uniforms-proof.json.

All three RSI sources are CC-BY-SA-3.0. Attribution and original source URLs are retained below:
'''
    for ref in references:
        note += f'\n- `{ref["sourceRsi"]}`: {ref["metadata"]["copyright"]}\n'
    note += '\nReproduce with `Tools/three_d/author_loose_uniforms.py`; check held asset bytes with `--check --skip-context --skip-reviews`. This script performs no global exports, native builds or game/server launch.\n'
    write(NOTE, note.encode())
    configure_surfaces()
    actual = bm.load_models(MODEL)
    for m in actual:
        sprite_states.validate_source(m, bm.resource_file)
    if not args.skip_reviews:
        reviews(actual, images)
    if CHECK:
        print(json.dumps(dict(byteIdenticalFiles=len(WRITTEN), models=len(actual), parts=sum(len(m['parts']) for m in actual), textures=len(pool.entries))))
        return
    contexts_result, hidden, inputs = ([], [], {}) if args.skip_context else contexts(actual, defaults, False)
    duplicate_groups = {}
    for record in contexts_result:
        key = (record['variant'], record['level'], record['prototype'], tuple(record['position']))
        duplicate_groups.setdefault(key, []).append(record['id'])
    raw_yaml = yaml.safe_load(MODEL.read_text())
    assert all(isinstance(m[k], str) for m in raw_yaml for k in ('groundOffset', 'sourceSpriteOffset'))
    assert all(isinstance(p[k], str) for m in raw_yaml for parts in [m['parts'], m['spriteStates']['icon']['frames'][0]['parts']] for p in parts for k in ('min', 'max'))
    proof = dict(schemaVersion=1,assetChecksPass=True,modelIds=[m['id'] for m in actual],
        counts=dict(models=3,parts=sum(len(m['parts']) for m in actual),textures=len(pool.entries),visiblePlacements=len(contexts_result),excludedContained=len(hidden)),
        sourceReferences=references,sourcePixelCrops=pool.crops,atlasIndices=[r['atlasIndex'] for r in pool.entries],
        scalarVectorsVerified=True,strictStaticSourceFrames=3,contexts=contexts_result,excluded=hidden,
        exactDuplicateGroups=[dict(variant=k[0],level=k[1],prototype=k[2],position=list(k[3]),savedIds=ids)
                              for k,ids in duplicate_groups.items() if len(ids)>1],
        contactGroups=[dict(variant=r['variant'],level=r['level'],id=r['id'],neighborId=n['id'],prototype=n['prototype'],
            exactDuplicate=n['exactDuplicate'],partPairs=n['conservativePartPairs']) for r in contexts_result for n in r['neighbors'] if n.get('conservativePartPairs')],
        sourceOwnerSha256={p.relative_to(ROOT).as_posix():sha(p) for p in source_owners},inputScenesSha256=inputs,
        writtenAssetsSha256={p.relative_to(ROOT).as_posix():sha(p) for p in WRITTEN},generatorSha256=sha(Path(__file__)),
        limitations=['All garment thickness and hidden fold reconstruction are inferred drafts.',
            'Exact source plan pixels are not an arbitrary-camera fidelity claim.',
            'Saved duplicate garments remain overlapping. No pile stacking or hidden inventory expansion is invented.',
            'Webbing, worn sprites and runtime source changes are not represented by these static world poses.'])
    save_json(GEN/'loose-uniforms-proof.json',proof)
    print(json.dumps(dict(**proof['counts'],modelIds=proof['modelIds'],contacts=len(proof['contactGroups']))),flush=True)


if __name__ == '__main__':
    main()
