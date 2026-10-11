"""Replace color subdivisions with exact source surfaces on the same six solids.

Only CMU3DHybrisaPlatformThree is rewritten. Its two feet, open underside,
source mapping, saved-facing convention and inferred dimensions are retained.
"""
import copy
import hashlib
import itertools
import json
import math
from pathlib import Path
import re

from PIL import Image, ImageDraw, ImageFont, ImageOps
import yaml

import build_models
import surfaces

ROOT = Path(__file__).resolve().parents[2]
PROTOTYPES = ROOT / 'Content.CMU/Resources/ThreeD/Prototypes/World'
MODEL_FILE = PROTOTYPES / 'garrison_environment.yml'
ART_FILE = PROTOTYPES / 'garrison_hybrisa_platform_three_art.yml'
TEXTURES = ROOT / 'Content.CMU/Resources/Textures/CMU14/ThreeD/Surfaces'
GENERATED = ROOT / 'Tools/three_d/generated'
REVIEW = GENERATED / 'review/platform-three'
RSI = '_RMC14/Structures/platforms.rsi'
SOURCE = ROOT / 'Resources/Textures' / RSI / 'hybrisaplatform3.png'
MODEL = 'CMU3DHybrisaPlatformThree'
PREFIX = 'CMU3DPlatformThree'
# Original 64x64 sheet coordinates. North front is mirrored; South cap is not.
# Identical left/right source samples intentionally share support/foot artwork.
SPECS = [
    ('Beam', 'Beam', (32, 7, 64, 10), True, (-.5, -.49, .2125), (.5, -.36, .34), 'XZ'),
    ('LeftSupport', 'Support', (51, 10, 60, 14), True, (-.375, -.49, .0425), (-.09375, -.36, .2125), 'XZ'),
    ('RightSupport', 'Support', (35, 10, 44, 14), True, (.125, -.49, .0425), (.40625, -.36, .2125), 'XZ'),
    ('LeftFoot', 'Foot', (52, 14, 59, 15), True, (-.34375, -.49, 0), (-.125, -.36, .0425), 'XZ'),
    ('RightFoot', 'Foot', (36, 14, 43, 15), True, (.15625, -.49, 0), (.375, -.36, .0425), 'XZ'),
    ('Cap', 'Cap', (0, 25, 32, 32), False, (-.5, -.5, .34), (.5, -.35, .39), 'XY'),
]
SLOTS = {'Beam': 1200, 'Support': 1201, 'Foot': 1202, 'Cap': 1203}


def digest(data):
    return hashlib.sha256(data).hexdigest()


def decoded_parts(model):
    return [{**p, 'min': list(build_models.vector(p['min'])), 'max': list(build_models.vector(p['max']))}
            for p in model['parts']]


def contains(point, part):
    return all(part['min'][i] < point[i] < part['max'][i] for i in range(3))


def bounds(parts):
    return {key: [fn(p[key][i] for p in parts) for i in range(3)]
            for key, fn in (('min', min), ('max', max))}


def source_color_rectangles(image):
    """Recover the previous color-rectangle partition for repeatable verification."""
    active = {}
    result = []
    for y in range(image.height + 1):
        runs = {}
        x = 0
        while y < image.height and x < image.width:
            color = image.getpixel((x, y))
            end = x + 1
            while end < image.width and image.getpixel((end, y)) == color:
                end += 1
            if color[3] == 255:
                runs[(x, end, color)] = active.get((x, end, color), y)
            x = end
        for (x, end, color), start in active.items():
            if (x, end, color) not in runs:
                result.append((x, start, end, y, color))
        active = runs
    return result


def previous_parts(sheet):
    """Reconstruct the 37 existing opaque volumes from their source projections."""
    parts = []
    for x, y, xe, ye, color in source_color_rectangles(sheet.crop((32, 7, 64, 15))):
        parts.append(dict(min=[(16-xe)/32, -.49, .34*(1-ye/8)],
                          max=[(16-x)/32, -.36, .34*(1-y/8)], color=color))
    for x, y, xe, ye, color in source_color_rectangles(sheet.crop((0, 25, 32, 32))):
        parts.append(dict(min=[(x-16)/32, -.35-ye*.15/7, .34],
                          max=[(xe-16)/32, -.35-y*.15/7, .39], color=color))
    for part in parts:
        for key in ('min', 'max'):
            part[key] = [round(v, 7) for v in part[key]]
    assert len(parts) == 37
    return parts


def rotate(point, turn):
    x, y, z = point
    return ((x, y, z), (-y, x, z), (-x, -y, z), (y, -x, z))[turn % 4]


def rotated_bounds(parts, turn):
    result = []
    for part in parts:
        corners = [rotate(p, turn) for p in itertools.product(*zip(part['min'], part['max']))]
        result.append(dict(min=[min(p[i] for p in corners) for i in range(3)],
                           max=[max(p[i] for p in corners) for i in range(3)]))
    return result


def union_proof(before, after):
    assert bounds(before) == bounds(after)
    cuts = [sorted({p[key][i] for p in before+after for key in ('min', 'max')}) for i in range(3)]
    centers = [[(a+b)/2 for a, b in zip(cut, cut[1:])] for cut in cuts]
    count = occupied = 0
    for point in itertools.product(*centers):
        old = any(contains(point, p) for p in before)
        new = any(contains(point, p) for p in after)
        assert old == new, point
        count += 1
        occupied += old
    return dict(partitionCells=count, occupiedCells=occupied, occupancyPreserved=True, bounds=bounds(after))


def rgba_at(point, part, images):
    if 'surface' not in part:
        color = part['color']
        return tuple(bytes.fromhex(color[1:])) + (255,) if isinstance(color, str) else color
    local = [(point[i]-part['min'][i])/(part['max'][i]-part['min'][i])-.5 for i in range(3)]
    u, v = surfaces.uv(local, part['surfaceAxis'])
    im = images[part['surface']]
    x = min(im.width-1, max(0, math.floor(u*im.width)))
    y = min(im.height-1, max(0, math.floor(v*im.height)))
    return im.getpixel((x, y))


def color_field_proof(before, after, images):
    """Check the volume color field and each exposed face of its exact box partition."""
    cuts = [sorted({p[key][i] for p in before+after for key in ('min', 'max')}) for i in range(3)]
    centers = [[(a+b)/2 for a, b in zip(cut, cut[1:])] for cut in cuts]
    occupied = {}
    for cell in itertools.product(*(range(len(c)) for c in centers)):
        point = [centers[i][cell[i]] for i in range(3)]
        old = [p for p in before if contains(point, p)]
        new = [p for p in after if contains(point, p)]
        assert bool(old) == bool(new)
        if old:
            assert len(old) == len(new) == 1
            assert rgba_at(point, old[0], images) == rgba_at(point, new[0], images), point
            occupied[cell] = (old[0], new[0])
    faces = {}
    for cell, (old, new) in occupied.items():
        for axis in range(3):
            for side in (-1, 1):
                neighbor = list(cell)
                neighbor[axis] += side
                if tuple(neighbor) in occupied:
                    continue
                point = [centers[i][cell[i]] for i in range(3)]
                boundary = cuts[axis][cell[axis]+(1 if side > 0 else 0)]
                point[axis] = boundary-side*1e-9
                assert rgba_at(point, old, images) == rgba_at(point, new, images), (cell, axis, side)
                name = 'XYZ'[axis]+('+' if side > 0 else '-')
                faces[name] = faces.get(name, 0)+1
    return dict(occupiedPartitionCellSamples=len(occupied), allRgbaEqual=True,
                exposedFaceSamples=faces, totalExposedFaceSamples=sum(faces.values()),
                method='Compare actual written textures through shared XZ/XY UVs against the old voxel color at every occupied partition-cell center and just inside every exposed +/-X/Y/Z partition face.',
                numericalLimit='Source cap texel boundaries are exact sevenths of .15; old voxel boundaries were rounded to seven decimal places. Continuous boundary equality is limited by that rounding, less than 0.00000005 tile.')


def verify_pixels(sheet, before, after, images, turn):
    """Sample actual written PNGs through the shared UV projection at every source pixel."""
    samples = opaque = empty = 0
    front = ImageOps.mirror(sheet.crop((32, 7, 64, 15)))
    for kind, image in (('front', front), ('cap', sheet.crop((0, 25, 32, 32)))):
        for y in range(image.height):
            for x in range(image.width):
                point = ([-.5+(x+.5)/32, -.425, .34*(1-(y+.5)/8)] if kind == 'front'
                         else [-.5+(x+.5)/32, -.35-(y+.5)*.15/7, .365])
                world = rotate(point, turn)
                local = rotate(world, -turn)
                expected = image.getpixel((x, y))
                previous = [p for p in before if contains(local, p)]
                current = [p for p in after if contains(local, p)]
                if expected[3] == 255:
                    assert len(previous) == len(current) == 1
                    assert rgba_at(local, previous[0], images) == expected
                    assert rgba_at(local, current[0], images) == expected
                    opaque += 1
                else:
                    assert expected[3] == 65 and not previous and not current
                    empty += 1
                samples += 1
    assert opaque == 406 and empty == 74
    return dict(samples=samples, exactOpaqueRgbaSamples=opaque, excludedShadowSamples=empty,
                actualWrittenTexturesSampled=True)


def review_facings(model, inventory):
    reviews = []
    names = ('South', 'East', 'North', 'West')
    sheet = Image.new('RGB', (1200, 440), '#101820')
    draw = ImageDraw.Draw(sheet)
    draw.text((15, 12), 'PLATFORM THREE / source facings and physical tile edge / OFFLINE DRAFT',
              fill='#F0E3BE', font=ImageFont.load_default(size=18))
    draw.text((15, 38), 'Original directional shading differs. Rotation/solid projection is preserved; inferred height and depth remain.',
              fill='#AAC0CD', font=ImageFont.load_default(size=13))
    for turn, name in enumerate(names):
        posed = copy.deepcopy(model)
        posed.update(id=MODEL+'Review'+name, label='Platform Three / '+name+' saved facing',
                     referenceDirection=(0, 2, 1, 3)[turn])
        for p in posed['parts']:
            center = [(a+b)/2 for a, b in zip(p['min'], p['max'])]
            half = [(b-a)/2 for a, b in zip(p['min'], p['max'])]
            center = rotate(center, turn)
            p.update(min=[c-h for c, h in zip(center, half)], max=[c+h for c, h in zip(center, half)], yaw=turn*90)
        reviews.append(posed)
        reference, _ = build_models.reference_frame(posed, inventory)
        assert reference is not None
        reference = reference.resize((192, 192), Image.Resampling.NEAREST)
        x = turn*300
        sheet.paste(reference, (x+54, 82), reference)
        draw.text((x+25, 64), name+' / yaw '+str(turn*90), fill='#F0E3BE', font=ImageFont.load_default(size=16))
        # Fixed origin retains which tile edge the model occupies.
        top = build_models.render_model(posed, (280, 155), yaw=-math.pi/2, pitch=math.pi/2,
                                       pixels_per_unit=120, screen_origin=(140, 78))
        sheet.paste(top, (x+10, 274))
    build_models.write_reviews(reviews, REVIEW/'source-facings', inventory)
    sheet.save(REVIEW/'source-facing-overview.png')


def main():
    raw = MODEL_FILE.read_text(encoding='utf-8')
    blocks = re.split(r'(?=^- type: cmu3DModel\s*$)', raw, flags=re.M)
    matches = [i for i, block in enumerate(blocks) if re.search(r'^  id: '+MODEL+r'$', block, re.M)]
    assert len(matches) == 1
    index = matches[0]
    model = yaml.safe_load(blocks[index])[0]
    metadata = {k: v for k, v in model.items() if k != 'parts'}
    sheet = Image.open(SOURCE).convert('RGBA')
    old_parts = previous_parts(sheet)
    frozen = ROOT/'.codex/platform-three-baseline/garrison_environment.yml'
    baseline_method = 'Previous 37-piece source projection reconstructed from the original PNG'
    if frozen.exists():
        baseline = next(m for m in yaml.load(frozen.read_text(), Loader=yaml.CSafeLoader) if m['id'] == MODEL)
        assert {k: v for k, v in baseline.items() if k != 'parts'} == metadata
        baseline_parts = decoded_parts(baseline)
        assert len(baseline_parts) == 37
        union_proof(old_parts, baseline_parts)
        old_parts = baseline_parts
        baseline_method = 'Frozen 860-model checkpoint in .codex/platform-three-baseline/garrison_environment.yml'
    existing = {s['id']: s['atlasIndex'] for p in PROTOTYPES.glob('*.yml')
                for s in (yaml.load(p.read_text(), Loader=yaml.CSafeLoader) or []) if s['type'] == 'cmu3DSurface'}
    for name, slot in SLOTS.items():
        uid = PREFIX+name
        assert uid not in existing or existing[uid] == slot
        assert not any(other != uid and used == slot for other, used in existing.items())
    images = {}
    art = []
    crops = []
    parts = []
    for label, name, crop, mirror, low, high, axis in SPECS:
        image = sheet.crop(crop)
        if mirror:
            image = ImageOps.mirror(image)
        assert all(c[3] == 255 for c in image.get_flattened_data())
        uid = PREFIX+name
        if uid in images:
            assert images[uid].size == image.size and images[uid].tobytes() == image.tobytes()
        else:
            path = TEXTURES/(uid+'.png')
            image.save(path)
            written = Image.open(path).convert('RGBA')
            assert written.size == image.size and written.tobytes() == image.tobytes()
            images[uid] = written
            art.append(dict(type='cmu3DSurface', id=uid, atlasIndex=SLOTS[name],
                            texture='/Textures/CMU14/ThreeD/Surfaces/'+uid+'.png'))
        crops.append(dict(label=label, sourceSheetCrop=list(crop), mirrorHorizontally=mirror, surface=uid,
                          size=list(image.size), sourceRgbaSha256=digest(image.tobytes()), allOpaque=True))
        parts.append(dict(label='source '+label, min=', '.join(f'{v:.7f}' for v in low),
                          max=', '.join(f'{v:.7f}' for v in high), color='#FFFFFF', surface=uid, surfaceAxis=axis))
    assert len(images) == 4 and len(parts) == 6
    ART_FILE.write_text('# Exact CC-BY-SA-3.0 source crops; see SOURCES_PLATFORM_THREE.md.\n'+
                        yaml.safe_dump(art, sort_keys=False), encoding='utf-8')
    model['parts'] = parts
    candidate = decoded_parts(model)
    color_field = color_field_proof(old_parts, candidate, images)
    facings = []
    for turn, name in enumerate(('South', 'East', 'North', 'West')):
        facings.append(dict(direction=name, rsiDirection=(0, 2, 1, 3)[turn], yaw=turn*math.pi/2,
                            union=union_proof(rotated_bounds(old_parts, turn), rotated_bounds(candidate, turn)),
                            pixels=verify_pixels(sheet, old_parts, candidate, images, turn)))
    blocks[index] = yaml.safe_dump([model], sort_keys=False, width=112)+'\n'
    updated = ''.join(blocks)
    # Byte-level preservation outside the single record, including unrelated agents' edits.
    assert re.split(r'(?=^- type: cmu3DModel\s*$)', updated, flags=re.M)[:index] == blocks[:index]
    MODEL_FILE.write_text(updated, encoding='utf-8')
    actual_blocks = re.split(r'(?=^- type: cmu3DModel\s*$)', MODEL_FILE.read_text(), flags=re.M)
    original_blocks = re.split(r'(?=^- type: cmu3DModel\s*$)', raw, flags=re.M)
    assert all(actual_blocks[i] == original_blocks[i] for i in range(len(blocks)) if i != index)
    written_model = yaml.safe_load(actual_blocks[index])[0]
    assert {k: v for k, v in written_model.items() if k != 'parts'} == metadata
    surfaces.load_surfaces.cache_clear()
    loaded = build_models.validate_model(written_model)
    inventory = json.loads((GENERATED/'inventory.json').read_text())
    inventory = {row['id']: row for row in inventory['prototypes']}
    build_models.write_reviews([loaded], REVIEW, inventory)
    review_facings(loaded, inventory)
    meta = json.loads((SOURCE.parent/'meta.json').read_text())
    proof = dict(schemaVersion=1, model=MODEL, previousParts=37, parts=6, savedMapEdits=0,
                 baseline=baseline_method, metadataUnchanged=list(metadata), otherModelRecordsUnchanged=True,
                 source=SOURCE.relative_to(ROOT).as_posix(), sourceFileSha256=digest(SOURCE.read_bytes()),
                 sourceRgbaSha256=digest(sheet.tobytes()), license=meta['license'], copyright=meta['copyright'],
                 sourceCrops=crops, uniqueTextures=4, atlasIndices=list(SLOTS.values()),
                 rgbaHashEncoding='SHA256 of raw RGBA bytes, size recorded separately', facings=facings,
                 volumeAndExposedFaceColorProof=color_field,
                 scope='Actual six-box YAML and written PNGs preserve the previous solid union and all 406 opaque front/cap pixels; 74 alpha65 profile samples remain empty.',
                 limitations=['Source North/West shading differs from South/East; the existing physical projection and all four facings are retained, not reinterpreted.',
                              'Height, depth, terrain elevation and hidden construction remain inferred.',
                              'Broken/construction states and existing neighboring contacts remain outside this reduction.',
                              'Global export, all-map placement verification and native packing reruns belong to the parent task; no game/server is launched.'])
    (GENERATED/'platform-three-verification.json').write_text(json.dumps(proof, indent=2)+'\n')
    print(f'{MODEL}: 37 -> 6 parts; four exact RGBA textures at 1200..1203; four facing proofs passed.')


if __name__ == '__main__':
    main()
