"""Author three disposal branches and both original static anchoring states.

Dedicated assets only: no shared export, native build or game launch.
"""
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
import disposal_pipe_states
import inventory
from scene import normalize_tint
import sprite_states
import surfaces
from author_wide_machinery import world_parts, box_bounds

BASE = ROOT / '.codex/model-batch-baseline1006'
GEN = ROOT / 'Tools/three_d/generated'
REVIEW = GEN / 'review/disposal-junctions'
MODEL = ROOT / 'Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_disposal_junctions.yml'
ART = MODEL.with_name('garrison_disposal_junctions_art.yml')
TEXTURES = ROOT / 'Content.CMU/Resources/Textures/CMU14/ThreeD/DisposalJunctions'
NOTE = ROOT / 'Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_DISPOSAL_JUNCTIONS.md'
RSI = 'Structures/Piping/disposal.rsi'
SOURCE = ROOT / 'Resources/Textures' / RSI
PROTOTYPE_SOURCE = ROOT / 'Resources/Prototypes/Entities/Structures/Piping/Disposal/pipes.yml'
SPECS = {'DisposalJunction': 'j1', 'DisposalJunctionFlipped': 'j2', 'DisposalXJunction': 'x'}
CHECK, WRITTEN = False, []
ANCHORED_TOP = -.004


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
    stream = BytesIO()
    image.save(stream, format='PNG')
    return stream.getvalue()


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
                assert row.get('type') != 'cmu3DSurface' or not 3000 <= row['atlasIndex'] <= 3099, f'Atlas conflict: {path}'

    def crop(self, state, image, rect, role, turn=0):
        crop = image.crop(rect)
        if turn:
            crop = crop.transpose(Image.Transpose.ROTATE_270)
        key = hashlib.sha256(str(crop.size).encode() + crop.tobytes()).hexdigest()
        if key not in self.cache:
            index = 3000 + len(self.entries)
            assert index <= 3099
            uid = f'CMU3DDisposalJunctionSurface{index}'
            self.cache[key] = uid
            write(TEXTURES / f'{uid}.png', png(crop))
            self.entries.append(dict(type='cmu3DSurface', id=uid, atlasIndex=index,
                texture=f'/Textures/CMU14/ThreeD/DisposalJunctions/{uid}.png'))
        uid = self.cache[key]
        actual = Image.open(TEXTURES / f'{uid}.png').convert('RGBA')
        assert actual.size == crop.size and actual.tobytes() == crop.tobytes()
        self.crops.append(dict(state=state, rectangle=list(rect), role=role, surface=uid,
            rgbaSha256=hashlib.sha256(crop.tobytes()).hexdigest(), originalPixelsUnchanged=True,
            inverseArtworkRotation=turn))
        return uid


def patches(kind, construction):
    """Axis-specific cast barrels, broad coupling rims and small branch welds.

    These are source-art partitions, not a pixel-voxel extrusion. Each entire
    curved barrel/collar has a single primitive and its unchanged source crop.
    """
    if construction:
        main = [((8, 2, 24, 5), 'CylinderY', 'north construction coupling rim'),
                ((9, 5, 24, 27), 'CylinderY', 'main barrel and south arrow'),
                ((8, 27, 24, 30), 'CylinderY', 'south construction coupling rim')]
        west = [((2, 8, 5, 24), 'CylinderX', 'west construction coupling rim'),
                ((5, 8, 8, 23), 'CylinderX', 'west branch barrel'),
                ((8, 8, 9, 24), 'CylinderX', 'west branch weld')]
    else:
        main = [((7, 0, 25, 4), 'CylinderY', 'north installed collar'),
                ((8, 4, 24, 28), 'CylinderY', 'main barrel and south arrow'),
                ((7, 28, 25, 32), 'CylinderY', 'south installed collar')]
        west = [((0, 7, 4, 25), 'CylinderX', 'west installed collar'),
                ((4, 8, 7, 24), 'CylinderX', 'west branch barrel'),
                ((7, 7, 8, 25), 'CylinderX', 'west branch weld')]
    east = [((32-r[2], r[1], 32-r[0], r[3]), shape, label.replace('west', 'east')) for r, shape, label in west]
    if kind == 'x' and construction:
        main[1] = ((9, 5, 23, 27), 'CylinderY', 'main barrel and south arrow')
    if kind == 'j2' and construction:
        main = [((32-r[2], r[1], 32-r[0], r[3]), shape, label) for r, shape, label in main]
    return main + (west if kind in ('j1', 'x') else []) + (east if kind in ('j2', 'x') else [])


def geometry(kind, state, image, pool):
    construction = state.startswith('con')
    # Round diameters come from source width; the vertical interpretation is an
    # inference. Anchored material is deliberately recessed below grate frames.
    max_diameter = .5 if construction else .5625
    center_z = max_diameter / 2 if construction else ANCHORED_TOP - max_diameter / 2
    parts, rebuilt = [], Image.new('RGBA', image.size)
    coverage = np.zeros((32, 32), dtype=np.uint8)
    source_patches = patches(kind, construction)
    # The X construction drawing has three asymmetric rim texels. Keep them;
    # mirroring the three-way art would erase the original small differences.
    if kind == 'x' and construction:
        source_patches += [((23, 5, 24, 6), 'CylinderY', 'north rim source tab'),
                           ((23, 26, 24, 27), 'CylinderY', 'south rim source tab'),
                           ((26, 23, 27, 24), 'CylinderX', 'east rim source tab')]
    for rect, shape, label in source_patches:
        x0, y0, x1, y1 = rect
        crop = image.crop(rect)
        assert crop.getbbox(), (state, rect)
        rebuilt.alpha_composite(crop, (x0, y0))
        coverage[y0:y1, x0:x1] += (np.asarray(crop)[:, :, 3] > 0).astype(np.uint8)
        diameter = (x1-x0 if shape == 'CylinderY' else y1-y0) / 32
        parts.append(dict(label=label+' round core', shape=shape, color='#323232',
            min=[(x0-16)/32, (16-y1)/32, center_z-diameter/2],
            max=[(x1-16)/32, (16-y0)/32, center_z+diameter/2]))
        # The native surface contract supports box/prism artwork, not cylinders.
        # Circumscribed crown facets carry source pixels without flattened tubes.
        across = x1-x0 if shape == 'CylinderY' else y1-y0
        cuts = [0, max(1, across//4), across-max(1, across//4), across] if across >= 4 else [0, across]
        for index, (a, b) in enumerate(zip(cuts, cuts[1:])):
            patch = (x0+a, y0, x0+b, y1) if shape == 'CylinderY' else (x0, y0+a, x1, y0+b)
            px0, py0, px1, py1 = patch
            cx, cy = ((px0+px1)/2-16)/32, (16-(py0+py1)/2)/32
            dx, dy = (px1-px0)/32, (py1-py0)/32
            yaw = 90 if shape == 'CylinderY' else 0
            if yaw:
                dx, dy = dy, dx
            style = 'Box' if len(cuts)==2 or index==1 else ('WedgeYReverse' if index==0 else 'WedgeY')
            part = dict(label=label+f' source crown {index}', shape=style, color='#FFFFFF',
                min=[cx-dx/2, cy-dy/2, center_z+diameter/4],
                max=[cx+dx/2, cy+dy/2, center_z+diameter/2],
                surface=pool.crop(state, image, patch, label, 270 if yaw else 0), surfaceAxis='XY')
            if yaw:
                part['yaw'] = yaw
            parts.append(part)
    assert np.array_equal(coverage, (np.asarray(image)[:, :, 3] > 0).astype(np.uint8)), state
    assert rebuilt.tobytes() == image.tobytes(), state
    return parts, dict(state=state, parts=len(parts), sourceBounds=list(image.getbbox()),
        opaquePixels=int(coverage.sum()), exactSourcePlanPartition=True,
        sourceRgbaSha256=hashlib.sha256(image.tobytes()).hexdigest(),
        minZ=min(p['min'][2] for p in parts), maxZ=max(p['max'][2] for p in parts),
        geometryInference='Round pipe cores with three supported textured crown facets, inferred depth and recessed installed height. Original planar colors and arrow pixels are unchanged; hidden undersides are not source-proven.')


def channel(kind):
    """Bounded inferred service trench; finite bottom and flush opaque corners.

    A rectangular slab opening is filled back to an actual cross/T plan here.
    Every fixture above Z=0 is preserved, including grating and furniture feet.
    Colors are sampled from the original installed source's dark metal palette.
    """
    left, right = (-.5 if kind in ('j1', 'x') else -.3), (.5 if kind in ('j2', 'x') else .3)
    parts = []
    def box(label, low, high, color):
        parts.append(dict(label=label, min=low, max=high, color=color))
    box('inferred finite service channel bottom', [left, -.5, -.62], [right, .5, -.59], '#292929')
    for side in (-1, 1):
        branches = kind == 'x' or (side == -1 and kind == 'j1') or (side == 1 and kind == 'j2')
        a, b = (-.5, -.3) if side == -1 else (.3, .5)
        if branches:
            for y0, y1 in ((-.5, -.3), (.3, .5)):
                box('inferred flush metal service surround', [a, y0, -.04], [b, y1, 0], '#5A5A5A')
                x0, x1 = (-.312, -.3) if side == -1 else (.3, .312)
                box('inferred service channel side wall', [x0, y0, -.59], [x1, y1, 0], '#323232')
                bottom, top = (-.312, -.3) if y0 < 0 else (.3, .312)
                box('inferred branch channel wall', [a, bottom, -.59], [b, top, 0], '#323232')
        else:
            x0, x1 = (-.3, -.288) if side == -1 else (.288, .3)
            box('inferred service channel side wall', [x0, -.5, -.59], [x1, .5, 0], '#323232')
    return parts, {'min': [left, -.5], 'max': [right, .5]}


def serialize(models):
    result = deepcopy(models)
    def vector(values):
        return ', '.join(f'{v:.8f}'.rstrip('0').rstrip('.') if v else '0' for v in values)
    for model in result:
        for key in ('groundOffset', 'sourceSpriteOffset'):
            model[key] = vector(model[key])
        if 'floorOpening' in model:
            model['floorOpening'] = {key: vector(values) for key, values in model['floorOpening'].items()}
        for parts in [model['parts']] + [f['parts'] for s in model['spriteStates'].values() for f in s['frames']]:
            for part in parts:
                part['min'], part['max'] = vector(part['min']), vector(part['max'])
    return result


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


def context_proof(models, defaults):
    prior = json.loads((GEN / 'next-physical-families.json').read_text())
    expected = {(p['variant'], p['level'], p['id']): (f['prototype'], p)
                for f in prior['families'] if f['prototype'] in SPECS for p in f['placements']}
    library = {m['id']: m for m in json.loads((BASE / 'models.json').read_text())['models']}
    authored = {m['referencePrototype']: m for m in models if m.get('anchored') is True}
    pairs = {m['id']: m for m in models}
    records, inputs = [], {}
    for spec in json.loads((BASE / 'scenes.json').read_text()):
        path = BASE / spec['file']
        doc = json.loads(path.read_text())
        targets = [e for e in doc['instances'] if e['prototype'] in SPECS]
        if not targets:
            continue
        map_path = ROOT / doc['map']['path']
        raw = saved_records(map_path)
        assert {e['id'] for e in targets} == set(raw)
        inputs[path.relative_to(ROOT).as_posix()] = sha(path)
        inputs[map_path.relative_to(ROOT).as_posix()] = sha(map_path)
        for entity in targets:
            uid = entity['id']
            prototype, previous = expected[(spec['variant'], spec['level'], uid)]
            assert prototype == entity['prototype'] and previous['position'] == entity['position'] and previous['yaw'] == entity['yaw']
            saved = {c['type']: c for c in raw[uid].get('components', [])}
            assert set(saved) == {'Transform'} and saved['Transform'] == previous['savedTransform']
            assert saved['Transform'].get('anchored', defaults[prototype]['Transform']['anchored']) is True
            model = authored[prototype]
            immutable = deepcopy((defaults[prototype], saved))
            selected, reason = disposal_pipe_states.select_saved(model, pairs, defaults[prototype], saved)
            assert selected is model and reason is None
            state, reason = disposal_pipe_states.saved_pose(model, defaults[prototype], saved, normalize_tint)
            assert state == model['referenceState'] and reason is None
            assert (defaults[prototype], saved) == immutable
            own = world_parts(model['parts'], entity['position'], entity['yaw'], [0, 0, 0])
            neighbors = []
            for other in doc['instances']:
                if other['id'] == uid or other['position'][2] != entity['position'][2] or max(abs(other['position'][k]-entity['position'][k]) for k in (0, 1)) > 1.01:
                    continue
                entry = dict(id=other['id'], prototype=other['prototype'], modelId=other.get('modelId'),
                    position=other['position'], sameCenter=other['position'] == entity['position'])
                neighbor_model = library.get(other.get('modelId'))
                if neighbor_model:
                    parts = doc.get('geometryVariants', {}).get(other.get('geometryKey'), neighbor_model['parts'])
                    placed = world_parts(parts, other['position'], other.get('renderYaw', other['yaw']), other.get('renderOffset', [0, 0, 0]))
                    entry['relativeZRange'] = [min(p['min'][2] for p in placed), max(p['max'][2] for p in placed)]
                    # Exact zero AABB overlap proves separation; nonzero only
                    # records a conservative candidate for curved geometry.
                    hits = []
                    for i, a in enumerate(own):
                        lo, hi = box_bounds(a)
                        for j, b in enumerate(placed):
                            bl, bh = box_bounds(b)
                            if all(min(hi[k], bh[k])-max(lo[k], bl[k]) > 1e-6 for k in range(3)):
                                hits.append([i, j])
                    entry['conservativeContactPairs'] = hits
                neighbors.append(entry)
            records.append(dict(variant=spec['variant'], level=spec['level'], id=uid, prototype=prototype,
                position=entity['position'], savedYaw=entity['yaw'], renderYaw=entity['yaw'], renderOffset=[0, 0, 0],
                sourceState=model['referenceState'], sourceFrame=0, modelId=model['id'],
                exactSourceOwnerPoseAccepted=True, sourceComponentsUnchanged=True,
                sourceVisible=previous['defaultPlayerVisibleAfterSubfloorOwner'], tile=previous['tile'],
                savedComponents=saved, inheritedSprite=defaults[prototype]['Sprite'], neighbors=neighbors))
    assert len(records) == 29 and len(expected) == 29
    assert Counter((r['variant'], r['sourceVisible']) for r in records) == {('redux', True): 16, ('redux', False): 6, ('classic', True): 7}
    return records, inputs


def reviews(models, images):
    montage = Image.new('RGB', (1260, 6*320), '#17212B')
    font = ImageFont.load_default(size=16)
    row = 0
    for model in models:
        for state, value in model['spriteStates'].items():
            card = Image.new('RGB', (1260, 320), '#17212B')
            ImageDraw.Draw(card).text((10, 8), f'{model["referencePrototype"]} / {state} / original, overhead, oblique, low side', fill='white', font=font)
            source = Image.new('RGBA', (256, 256), '#62727C')
            source.alpha_composite(images[state].resize((256, 256), Image.Resampling.NEAREST))
            card.paste(source.convert('RGB'), (10, 44))
            for column, (yaw, pitch) in enumerate(((-math.pi/2, math.pi/2-.001), (-1.1, .65), (.3, .2))):
                card.paste(bm.render_model(value['frames'][0], (320, 270), yaw, pitch), (280+column*320, 40))
            card.save(REVIEW / f'{model["id"]}-{state}.png')
            montage.paste(card, (0, row*320))
            row += 1
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
    meta = json.loads((SOURCE / 'meta.json').read_text())
    pool, models, references, images = Pool(), [], [], {}
    for uid, kind in SPECS.items():
        sprite = defaults[uid]['Sprite']
        assert sprite['sprite'] == RSI and sprite.get('noRot', False) is False and not sprite.get('offset') and sprite['visible'] is True
        states = {}
        for state in (f'pipe-{kind}', f'conpipe-{kind}'):
            assert next(s for s in meta['states'] if s['name'] == state) == {'name': state}
            image = Image.open(SOURCE / (state+'.png')).convert('RGBA')
            assert image.size == (32, 32) and set(image.getchannel('A').get_flattened_data()) == {0, 255}
            images[state] = image
            parts, ref = geometry(kind, state, image, pool)
            states[state] = dict(frames=[dict(parts=parts)], delays=[1])
            references.append(dict(prototype=uid, **ref, sourcePng=(SOURCE/(state+'.png')).relative_to(ROOT).as_posix(), sourcePngSha256=sha(SOURCE/(state+'.png'))))
        for anchored in (True, False):
            state = f'pipe-{kind}' if anchored else f'conpipe-{kind}'
            parts = deepcopy(states[state]['frames'][0]['parts'])
            extra = {}
            if anchored:
                surrounds, opening = channel(kind)
                parts.extend(surrounds)
                extra.update(floorOpening=opening, preserveSlabCladding=True)
            else:
                extra['sourceSpriteRotates'] = True
            models.append(dict(type='cmu3DModel', id='CMU3D'+uid+('' if anchored else 'Loose'),
                label=resolver.resolve(uid).get('name', uid)+(' installed' if anchored else ' construction'), status='draft',
                sourcePrototypes=[uid] if anchored else [], referencePrototype=uid, referenceRsi=RSI, referenceState=state,
                referenceDirection=0, sourceDirections=1, sourceSpriteOffset=[0, 0], anchored=anchored,
                alternateAnchorModel='CMU3D'+uid+('Loose' if anchored else ''),
                useEntityRotation=True, placement='floor', groundOffset=[0, 0],
                description='Round source-specific disposal branch with original direction arrow. Installed geometry and finite channel are recessed below intact grating; depth, service surround and hidden surfaces are inferred. See SOURCES_DISPOSAL_JUNCTIONS.md.',
                parts=parts, spriteStates={state: dict(frames=[dict(parts=deepcopy(parts))], delays=[1])}, **extra))
    write(MODEL, ('# Generated by Tools/three_d/author_disposal_junctions.py.\n'+yaml.safe_dump(serialize(models), sort_keys=False, width=115)).encode())
    write(ART, ('# Original source crops; retain disposal.rsi CC-BY-SA-3.0 attribution.\n'+yaml.safe_dump(pool.entries, sort_keys=False)).encode())
    note = '''# Disposal junction drafts

Three exact source prototypes: DisposalJunction (west branch), DisposalJunctionFlipped (east branch) and DisposalXJunction (both). Main passages run north/south and the original painted arrow points south in model-local coordinates. Saved yaw rotates geometry and artwork together. Each prototype has reciprocal installed and Loose models: one static anchored pipe-* state and one static construction conpipe-* state; each is a single 32 x 32 frame with one direction and zero sprite offset. No transit, pressure or construction animation is invented.

Barrels and wider collars have real round CylinderX/CylinderY cores with three source-textured crown facets per casting. These use supported Box/WedgeY primitives around the round core, because native textured cylinders are intentionally unsupported. Their plan dimensions and state-specific short construction rims come from source pixels. The six source-frame partitions reassemble every original RGBA pixel exactly once, including the arrow, shading, rim details and transparent corners. Curved depth, crown faceting, hidden undersides, and the installed vertical position are inferred; exact source-plan artwork does not prove arbitrary 3D view fidelity. The lower casting uses source dark metal colors; alpha corners on the crown can expose that inferred underlying casting. Construction states use their own smaller footprint and remain above the floor. Installed collar crowns are at Z=-0.004, with barrels below them: this clears co-located grate frames, benches and platform feet. An inferred service channel has a finite bottom at -0.62 to -0.59 and vertical walls. Opaque flush corner infill restores the T/cross outline inside the rectangular slab aperture; its dark metal service surround is an explicitly inferred addition, not original floor art. The linked aperture preserves all slab cladding, and can activate only with supported exact anchored source appearance. It does not change gameplay floors or reveal another map.

The 1006-model saved scenes contain 29 raw placements: 22 Redux and 7 classic. SubFloorHide resolves 16 exposed Redux placements and 6 covered placements; all 7 classic placements are exposed. All saved targets are anchored and have only Transform overrides. Original source positions, parents and yaw remain unchanged. Covered pipes must remain hidden under normal viewing; scanners and runtime tile changes remain owned by the original systems. The paired source state is selected by AnchorVisuals.Anchored. A raw prototype default conpipe-* is not the saved anchored appearance.

Every exposed source shares its tile with a grate or larger fixture. Those neighbors remain present. The dedicated proof checks all saved targets against the frozen scene geometry, retaining unknown neighbors and conservative contact candidates rather than deleting fixtures. It does not imply source-visible pipe crowns are visible through an unmodified solid floor.

Source: Resources/Textures/Structures/Piping/disposal.rsi. License: CC-BY-SA-3.0.
'''
    note += '\nOriginal attribution: '+meta['copyright']+'\n'
    note += '\nReproduce with `Tools/three_d/author_disposal_junctions.py`; deterministic asset check: `--check --skip-context --skip-reviews`. No global export, content build or game launch.\n'
    write(NOTE, note.encode())
    surfaces.load_surfaces.cache_clear()
    actual = bm.load_models(MODEL)
    disposal_pipe_states.validate_links(actual)
    for model in actual:
        sprite_states.validate_source(model, bm.resource_file)
    if not args.skip_reviews:
        reviews(actual, images)
    raw_yaml = yaml.safe_load(MODEL.read_text())
    assert all(isinstance(m[k], str) for m in raw_yaml for k in ('groundOffset', 'sourceSpriteOffset'))
    assert all(isinstance(v, str) for m in raw_yaml if 'floorOpening' in m for v in m['floorOpening'].values())
    assert all(isinstance(p[k], str) for m in raw_yaml for parts in [m['parts']]+[f['parts'] for s in m['spriteStates'].values() for f in s['frames']] for p in parts for k in ('min', 'max'))
    if CHECK:
        print(json.dumps(dict(byteIdenticalFiles=len(WRITTEN), models=len(actual), staticStates=6, textures=len(pool.entries))))
        return
    contexts, inputs = ([], {}) if args.skip_context else context_proof(actual, defaults)
    proof = dict(schemaVersion=1, assetChecksPass=True, sourceVisibilityBridgeRequired=True,
        modelIds=[m['id'] for m in actual], counts=dict(models=6, staticStates=6, textures=len(pool.entries),
        defaultParts=sum(len(m['parts']) for m in actual), stateParts=sum(len(f['parts']) for m in actual for s in m['spriteStates'].values() for f in s['frames']),
        rawPlacements=len(contexts), reduxExposed=sum(r['variant']=='redux' and r['sourceVisible'] for r in contexts),
        reduxCovered=sum(r['variant']=='redux' and not r['sourceVisible'] for r in contexts), classicExposed=sum(r['variant']=='classic' and r['sourceVisible'] for r in contexts)),
        sourceReferences=references, sourcePixelCrops=pool.crops, sourceMetadata=meta, scalarVectorsVerified=True,
        anchorAdapterSha256=sha(Path(disposal_pipe_states.__file__)),
        contexts=contexts, sourceInputSha256={PROTOTYPE_SOURCE.relative_to(ROOT).as_posix():sha(PROTOTYPE_SOURCE), (SOURCE/'meta.json').relative_to(ROOT).as_posix():sha(SOURCE/'meta.json')},
        inputScenesSha256=inputs, writtenAssetsSha256={p.relative_to(ROOT).as_posix():sha(p) for p in WRITTEN}, generatorSha256=sha(Path(__file__)),
        limitations=['Installed depth and round cross-section are inferred.', 'Recessed anchored geometry needs coordinated source-visible floor presentation.',
            'No transit or unanchoring animation is invented.', 'Unmapped neighboring entities are retained but have no solid contact test.'])
    save_json(GEN / 'disposal-junctions-proof.json', proof)
    print(json.dumps(proof['counts']), flush=True)


if __name__ == '__main__':
    main()
