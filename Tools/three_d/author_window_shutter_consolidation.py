"""Exact source-color preservation while consolidating window-shutter geometry.

Defaults to staging. Use --apply only after the caller has authorized updating
live resources. No global exports, builds, native client or server are started.
"""
import argparse
import copy
from fractions import Fraction as F
import hashlib
import itertools
import json
import math
from pathlib import Path
import re
import sys

from PIL import Image, ImageDraw, ImageFont
import yaml

ROOT = next(p for p in Path(__file__).resolve().parents if (p/'Resources/Textures/_RMC14').is_dir())
sys.path.insert(0,str(ROOT/'Tools/three_d'))
import build_models
import surfaces

STAGE = ROOT/'.codex/window-shutter-staged'
MODEL_REL = Path('Content.CMU/Resources/ThreeD/Prototypes/World/garrison_environment.yml')
ART_REL = Path('Content.CMU/Resources/ThreeD/Prototypes/World/garrison_window_shutter_art.yml')
TEXTURE_REL = Path('Content.CMU/Resources/Textures/CMU14/ThreeD/Surfaces')
REVIEW_REL = Path('Tools/three_d/generated/review/window-shutter-consolidated')
PROOF_REL = Path('Tools/three_d/generated/window-shutter-consolidation-proof.json')
SOURCE = ROOT/'Resources/Textures/_RMC14/Structures/Doors/Shutters/Hybrisa/window_shutter.rsi'
IDS = ('CMU3DHybrisaWindowShutter','CMU3DHybrisaWindowShutterOpen')
META = json.loads((SOURCE/'meta.json').read_text())
IMAGES = {}
# Source rectangles from the minimum guillotine partition in the read-only study.
# Exact union/color checks below independently verify every serialized pose.
SPECS = {'closed': [[[0, 0, 32, 2],
             [0, 2, 32, 2],
             [0, 4, 32, 5],
             [0, 9, 2, 1],
             [30, 9, 2, 1],
             [0, 10, 32, 1],
             [0, 11, 32, 1],
             [0, 12, 2, 4],
             [2, 13, 28, 2],
             [30, 12, 2, 4],
             [0, 16, 32, 1],
             [0, 17, 32, 1],
             [0, 18, 2, 4],
             [2, 19, 28, 2],
             [30, 18, 2, 4],
             [0, 22, 32, 2],
             [0, 24, 2, 1],
             [30, 24, 2, 1],
             [0, 25, 32, 3]]],
 'open': [[[0, 0, 32, 1], [0, 1, 2, 1], [30, 1, 2, 1], [0, 2, 32, 1], [0, 3, 32, 1], [0, 4, 32, 1]]],
 'opening': [[[0, 0, 32, 3],
              [0, 3, 32, 1],
              [0, 4, 32, 5],
              [0, 9, 2, 1],
              [30, 9, 2, 1],
              [0, 10, 32, 1],
              [0, 11, 32, 1],
              [0, 12, 2, 4],
              [2, 13, 28, 2],
              [30, 12, 2, 4],
              [0, 16, 32, 1],
              [0, 17, 32, 1],
              [0, 18, 2, 4],
              [2, 19, 28, 2],
              [30, 18, 2, 4],
              [0, 22, 32, 2],
              [0, 24, 2, 1],
              [30, 24, 2, 1],
              [0, 25, 32, 3]],
             [[0, 0, 32, 3],
              [0, 3, 32, 1],
              [0, 4, 32, 5],
              [0, 9, 2, 1],
              [30, 9, 2, 1],
              [0, 10, 32, 1],
              [0, 11, 32, 1],
              [0, 12, 2, 4],
              [2, 13, 28, 2],
              [30, 12, 2, 4],
              [0, 16, 32, 1],
              [0, 17, 32, 1],
              [0, 18, 2, 4],
              [2, 19, 28, 3],
              [30, 18, 2, 4]],
             [[0, 0, 32, 2],
              [0, 2, 32, 2],
              [0, 4, 32, 5],
              [0, 9, 2, 1],
              [30, 9, 2, 1],
              [0, 10, 32, 1],
              [0, 11, 32, 1],
              [0, 12, 2, 4],
              [2, 13, 28, 2],
              [30, 12, 2, 4],
              [0, 16, 32, 1],
              [0, 17, 32, 1],
              [0, 18, 32, 1]],
             [[0, 0, 32, 3],
              [0, 3, 32, 1],
              [0, 4, 32, 5],
              [0, 9, 2, 1],
              [30, 9, 2, 1],
              [0, 10, 32, 1],
              [0, 11, 2, 5],
              [2, 11, 28, 1],
              [2, 13, 28, 3],
              [30, 11, 2, 5]],
             [[0, 0, 32, 2],
              [0, 2, 32, 2],
              [0, 4, 32, 5],
              [0, 9, 2, 1],
              [30, 9, 2, 1],
              [0, 10, 32, 1],
              [0, 11, 32, 2]],
             [[0, 0, 32, 1], [0, 1, 2, 1], [30, 1, 2, 1], [0, 2, 32, 1], [0, 3, 32, 1], [0, 4, 32, 1]]],
 'closing': [[[0, 0, 32, 1], [0, 1, 2, 1], [30, 1, 2, 1], [0, 2, 32, 1], [0, 3, 32, 1], [0, 4, 32, 1]],
             [[0, 0, 32, 2],
              [0, 2, 32, 2],
              [0, 4, 32, 5],
              [0, 9, 2, 1],
              [30, 9, 2, 1],
              [0, 10, 32, 1],
              [0, 11, 32, 2]],
             [[0, 0, 32, 3],
              [0, 3, 32, 1],
              [0, 4, 32, 5],
              [0, 9, 2, 1],
              [30, 9, 2, 1],
              [0, 10, 32, 1],
              [0, 11, 2, 5],
              [2, 11, 28, 1],
              [2, 13, 28, 3],
              [30, 11, 2, 5]],
             [[0, 0, 32, 2],
              [0, 2, 32, 2],
              [0, 4, 32, 5],
              [0, 9, 2, 1],
              [30, 9, 2, 1],
              [0, 10, 32, 1],
              [0, 11, 32, 1],
              [0, 12, 2, 4],
              [2, 13, 28, 2],
              [30, 12, 2, 4],
              [0, 16, 32, 1],
              [0, 17, 32, 1],
              [0, 18, 32, 1]],
             [[0, 0, 32, 3],
              [0, 3, 32, 1],
              [0, 4, 32, 5],
              [0, 9, 2, 1],
              [30, 9, 2, 1],
              [0, 10, 32, 1],
              [0, 11, 32, 1],
              [0, 12, 2, 4],
              [2, 13, 28, 2],
              [30, 12, 2, 4],
              [0, 16, 32, 1],
              [0, 17, 32, 1],
              [0, 18, 2, 4],
              [2, 19, 28, 3],
              [30, 18, 2, 4]],
             [[0, 0, 32, 3],
              [0, 3, 32, 1],
              [0, 4, 32, 5],
              [0, 9, 2, 1],
              [30, 9, 2, 1],
              [0, 10, 32, 1],
              [0, 11, 32, 1],
              [0, 12, 2, 4],
              [2, 13, 28, 2],
              [30, 12, 2, 4],
              [0, 16, 32, 1],
              [0, 17, 32, 1],
              [0, 18, 2, 4],
              [2, 19, 28, 2],
              [30, 18, 2, 4],
              [0, 22, 32, 2],
              [0, 24, 2, 1],
              [30, 24, 2, 1],
              [0, 25, 32, 3]]]}


def digest(value):
    return hashlib.sha256(value).hexdigest()

def source_frame(state, index=0, direction=0):
    metadata = next(s for s in META['states'] if s['name'] == state)
    delays = metadata.get('delays', [[1]]*4)
    flat = sum(len(row) for row in delays[:direction])+index
    sheet = Image.open(SOURCE/(state+'.png')).convert('RGBA')
    columns = sheet.width//32
    return sheet.crop((flat%columns*32, flat//columns*32, flat%columns*32+32, flat//columns*32+32)), delays[direction]

def exact_parts(parts):
    out = []
    for p in parts:
        assert p.get('shape', 'Box') == 'Box' and not p.get('yaw') and not p.get('pitch') and not p.get('surface')
        item = copy.deepcopy(p)
        for key in ('min', 'max'):
            item[key] = tuple(F(v.strip()) for v in p[key].split(','))
        assert item['min'][1] == -F(1, 16) and item['max'][1] == F(1, 16)
        item['rgba'] = tuple(bytes.fromhex(p['color'][1:]))+(255,)
        out.append(item)
    return out

def z_edge(row):
    # Preserve the historical writer's actual nine-decimal geometry, not an
    # idealized 2.74/28 lattice that would subtly change its occupied volume.
    return F(f'{2.74-row*2.74/28:.9f}')

def candidate(image, rectangles, name):
    result = []
    for index, (x, y, w, h) in enumerate(rectangles):
        cropped = image.crop((x, y, x+w, y+h))
        assert all(c[3] == 255 for c in cropped.get_flattened_data())
        uid = f'{name}_{index}'
        IMAGES[uid] = cropped
        result.append(dict(label=f'opaque source rectangle {x}:{y}:{w}:{h}',
            min=(F(x, 32)-F(1, 2), -F(1, 16), z_edge(y+h)),
            max=(F(x+w, 32)-F(1, 2), F(1, 16), z_edge(y)),
            color='#FFFFFF', surface=uid, surfaceAxis='XZ', sourceRect=[x, y, w, h]))
    return result

def contains(point, part):
    return all(part['min'][i] < point[i] < part['max'][i] for i in range(3))

def rgba_at(point, part):
    if 'surface' not in part:
        return part['rgba']
    image = IMAGES[part['surface']]
    x = int((point[0]-part['min'][0])/(part['max'][0]-part['min'][0])*image.width)
    y = int((part['max'][2]-point[2])/(part['max'][2]-part['min'][2])*image.height)
    return image.getpixel((min(image.width-1, max(0, x)), min(image.height-1, max(0, y))))

def box_bounds(parts):
    return dict(min=[min(p['min'][i] for p in parts) for i in range(3)],
                max=[max(p['max'][i] for p in parts) for i in range(3)])

def field_proof(before, after, split_texels):
    """Every open cell in the arrangement has constant occupancy and RGBA."""
    cuts = [{p[key][i] for p in before+after for key in ('min', 'max')} for i in range(3)]
    if split_texels:
        for p in after:
            image = IMAGES[p['surface']]
            cuts[0].update(p['min'][0]+(p['max'][0]-p['min'][0])*F(i, image.width) for i in range(image.width+1))
            cuts[2].update(p['max'][2]-(p['max'][2]-p['min'][2])*F(i, image.height) for i in range(image.height+1))
    cuts = [sorted(values) for values in cuts]
    centers = [[(a+b)/2 for a, b in zip(values, values[1:])] for values in cuts]
    occupied, mismatches, occupancy_errors = {}, [], []
    for cell in itertools.product(*(range(len(c)) for c in centers)):
        point = tuple(centers[i][cell[i]] for i in range(3))
        old = [p for p in before if contains(point, p)]
        new = [p for p in after if contains(point, p)]
        if bool(old) != bool(new):
            occupancy_errors.append(dict(cell=cell, point=point))
        if old:
            assert len(old) == len(new) == 1
            same = rgba_at(point, old[0]) == rgba_at(point, new[0])
            occupied[cell] = same
            if not same:
                mismatches.append(dict(cell=cell, point=point, zWidth=cuts[2][cell[2]+1]-cuts[2][cell[2]],
                    before=rgba_at(point, old[0]), after=rgba_at(point, new[0])))
    faces, bad_faces = {}, {}
    for cell, same in occupied.items():
        for axis, side in itertools.product(range(3), (-1, 1)):
            neighbor = list(cell)
            neighbor[axis] += side
            if tuple(neighbor) not in occupied:
                key = 'XYZ'[axis]+('+' if side > 0 else '-')
                faces[key] = faces.get(key, 0)+1
                if not same:
                    bad_faces[key] = bad_faces.get(key, 0)+1
    assert not occupancy_errors and box_bounds(before) == box_bounds(after)
    return dict(geometryPartitionCells=math.prod(len(c) for c in centers), occupiedCells=len(occupied),
        exactOpaqueUnionPreserved=True, boundsPreserved=True, exactRgbaColorFieldPreserved=not mismatches,
        mismatchedColorCells=len(mismatches), maxMismatchZWidth=max((m['zWidth'] for m in mismatches), default=F(0)),
        mismatchSamples=mismatches[:4], exposedFacePatches=faces, totalExposedFacePatches=sum(faces.values()),
        mismatchedExposedFacePatches=bad_faces, method='Exact rational partition using actual geometry boundaries'+
        (' and every proposed texture texel boundary. Constant-field cells and exposed face limits are exhaustively checked.' if split_texels
         else '. This alone proves shape; projected texture boundaries are checked separately.'))

def source_pixels(image, before, after):
    opaque = gaps = 0
    for y, x in itertools.product(range(32), repeat=2):
        point = (F(2*x+1, 64)-F(1, 2), F(0), (z_edge(y)+z_edge(y+1))/2)
        expected = image.getpixel((x, y))
        old = [p for p in before if contains(point, p)]
        new = [p for p in after if contains(point, p)]
        if expected[3]:
            assert len(old) == len(new) == 1
            assert rgba_at(point, old[0]) == rgba_at(point, new[0]) == expected
            opaque += 1
        else:
            assert not old and not new
            gaps += 1
    return dict(canvasSamples=1024, opaqueSourceRgbaSamples=opaque, emptyAlphaSamples=gaps,
                exactSourcePixelCenters=True, noTransparentVolumes=True)

def renderable(parts):
    return [{k: ([float(v) for v in value] if k in ('min', 'max') else value)
             for k, value in p.items() if k not in ('rgba', 'sourceRect')} for p in parts]

def metadata(model):
    result = copy.deepcopy(model)
    result.pop('parts')
    for state in result['doorSpriteStates'].values():
        for frame in state['frames']:
            frame.pop('parts')
    return result

def exportable(value):
    if isinstance(value, F):
        return float(value)
    if isinstance(value, dict):
        return {k: exportable(v) for k, v in value.items()}
    if isinstance(value, (list, tuple)):
        return [exportable(v) for v in value]
    return value


def source_voxels(image, state, frame):
    """Reproduce the original colored extrusion for repeat authoring after apply."""
    used, parts = set(), []
    for y, x in itertools.product(range(32), repeat=2):
        color = image.getpixel((x, y))
        if not color[3] or (x, y) in used:
            continue
        w = 1
        while x+w < 32 and (x+w, y) not in used and image.getpixel((x+w, y)) == color:
            w += 1
        h = 1
        while y+h < 32 and all((xx, y+h) not in used and image.getpixel((xx, y+h)) == color for xx in range(x, x+w)):
            h += 1
        used.update((xx, yy) for xx in range(x, x+w) for yy in range(y, y+h))
        parts.append(dict(label=f'{state} original color rectangle {frame}:{x}:{y}:{w}:{h}',
            min=(F(x, 32)-F(1, 2), -F(1, 16), z_edge(y+h)),
            max=(F(x+w, 32)-F(1, 2), F(1, 16), z_edge(y)),
            color='#'+''.join(f'{v:02X}' for v in color[:3]), rgba=color))
    return parts


class SurfacePool:
    def __init__(self, output):
        self.output = output
        self.art, self.by_hash, self.crops = [], {}, []
        self.used = {row['atlasIndex']: row['id']
                     for path in (ROOT/MODEL_REL.parent).glob('*.yml') if path != ROOT/ART_REL
                     for row in (yaml.load(path.read_text(), Loader=yaml.CSafeLoader) or [])
                     if row.get('type') == 'cmu3DSurface'}
        (output/TEXTURE_REL).mkdir(parents=True, exist_ok=True)

    def store(self, image, state, frame, rect):
        key = digest(str(image.size).encode()+image.tobytes())
        if key not in self.by_hash:
            slot = 1300+len(self.art)
            assert slot not in self.used, (slot, self.used.get(slot))
            assert slot <= surfaces.MAX_SURFACES
            uid = f'CMU3DWindowShutterSurface{slot}'
            image.save(self.output/TEXTURE_REL/(uid+'.png'))
            self.by_hash[key] = uid
            self.art.append(dict(type='cmu3DSurface', id=uid, atlasIndex=slot,
                texture=f'/Textures/CMU14/ThreeD/Surfaces/{uid}.png'))
        uid = self.by_hash[key]
        self.crops.append(dict(state=state, frame=frame, sourceRect=rect, surface=uid,
            size=list(image.size), rgbaSha256=digest(image.tobytes())))
        return uid


def decimal_parts(parts):
    result = []
    for p in parts:
        result.append({k: (', '.join(f'{float(v):.9f}' for v in value) if k in ('min', 'max') else value)
                       for k, value in p.items() if k not in ('rgba', 'sourceRect')})
    return result


def decode_written_parts(parts, registry):
    result = []
    for part in parts:
        assert part.get('surface') in registry and part['surfaceAxis'] == 'XZ' and part['color'] == '#FFFFFF'
        assert part.get('shape', 'Box') == 'Box' and not part.get('yaw') and not part.get('pitch')
        p = copy.deepcopy(part)
        for key in ('min', 'max'):
            p[key] = tuple(F(v.strip()) for v in p[key].split(','))
        result.append(p)
    return result


def masked_model_records(text):
    for uid in IDS:
        pattern = r'^- type: cmu3DModel\r?\n  id: '+uid+r'\r?\n.*?(?=^- type:|\Z)'
        text, count = re.subn(pattern, '<TARGET:'+uid+'>\n', text, flags=re.M|re.S)
        assert count == 1
    return text


def write_reviews(loaded, before_poses, after_poses, output):
    destination = output/REVIEW_REL
    destination.mkdir(parents=True, exist_ok=True)
    inventory = {p['id']: p for p in json.loads((ROOT/'Tools/three_d/generated/inventory.json').read_text())['prototypes']}
    build_models.write_reviews(loaded, destination, inventory)
    sheet = Image.new('RGB', (1200, 14*220+68), '#101820')
    draw = ImageDraw.Draw(sheet)
    draw.text((16, 12), 'WINDOW SHUTTERS / source, original solids, serialized candidate / ALL POSES / DRAFT',
              fill='#E4D6B8', font=ImageFont.load_default(size=18))
    for row, (key, before) in enumerate(before_poses.items()):
        state, index = key
        after = after_poses[key]
        top = 68+row*220
        draw.text((12, top), f'{state.title()} frame {index}: {len(before)} original to {len(after)} parts',
                  fill='#D4DBD9', font=ImageFont.load_default(size=16))
        image, _ = source_frame(state, index)
        image = image.resize((192, 192), Image.Resampling.NEAREST)
        sheet.paste(image, (60, top+25), image)
        for column, parts in enumerate((before, after)):
            rendered = build_models.render_model(dict(parts=renderable(parts)), (360, 192), yaw=-math.pi/3, pitch=.25)
            sheet.paste(rendered, (390+column*400, top+25))
    sheet.save(destination/'all-serialized-poses.png')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--apply', action='store_true', help='Explicitly apply to live resources instead of the default staging directory.')
    args = parser.parse_args()
    output = ROOT if args.apply else STAGE
    output.mkdir(parents=True, exist_ok=True)
    original_bytes = (ROOT/MODEL_REL).read_bytes()
    original_text = original_bytes.decode('utf-8')
    originals = [m for m in yaml.load(original_text, Loader=yaml.CSafeLoader) if m.get('id') in IDS]
    assert len(originals) == 2 and originals[0]['doorSpriteStates'] == originals[1]['doorSpriteStates']
    if not args.apply:
        (STAGE/'baseline').mkdir(parents=True, exist_ok=True)
        baseline_path = STAGE/'baseline/original-shutter-records.json'
        if not baseline_path.exists():
            baseline_path.write_text(json.dumps(originals, indent=2)+'\n')
        assert json.loads(baseline_path.read_text()) == originals
    pool = SurfacePool(output)
    before_poses, state_parts, original_kind = {}, {}, {}
    for state, definition in originals[0]['doorSpriteStates'].items():
        metadata_row = next(s for s in META['states'] if s['name'] == state)
        assert all(delays == definition['delays'] for delays in metadata_row.get('delays', [[1]]*4))
        assert len(definition['frames']) == len(SPECS[state])
        state_parts[state] = dict(frames=[], delays=copy.deepcopy(definition['delays']))
        for index, authored in enumerate(definition['frames']):
            image, _ = source_frame(state, index)
            if not any(p.get('surface') for p in authored['parts']):
                before = exact_parts(authored['parts'])
                original_kind[(state, index)] = 'Actual original authored YAML color rectangles'
            else:
                assert all(p.get('surface', '').startswith('CMU3DWindowShutterSurface') for p in authored['parts'])
                before = source_voxels(image, state, index)
                original_kind[(state, index)] = 'Original color extrusion regenerated from source for an already consolidated record'
            before_poses[(state, index)] = before
            proposed = candidate(image, SPECS[state][index], f'{state}_{index}')
            for part in proposed:
                part['surface'] = pool.store(IMAGES[part['surface']], state, index, part['sourceRect'])
            state_parts[state]['frames'].append(dict(parts=decimal_parts(proposed)))
    assert len(pool.art) == 43
    (output/ART_REL).parent.mkdir(parents=True, exist_ok=True)
    (output/ART_REL).write_text('# Exact CC-BY-SA-3.0 shutter source crops; see SOURCES_WINDOW_SHUTTER_CONSOLIDATION.md.\n'+
                              yaml.safe_dump(pool.art, sort_keys=False, width=110), encoding='utf-8')
    replacements = []
    for original in originals:
        model = copy.deepcopy(original)
        model['doorSpriteStates'] = copy.deepcopy(state_parts)
        model['parts'] = copy.deepcopy(state_parts[model['referenceState']]['frames'][0]['parts'])
        assert metadata(model) == metadata(original)
        replacements.append(model)
    updated_text = original_text
    for model in replacements:
        pattern = r'^- type: cmu3DModel\r?\n  id: '+model['id']+r'\r?\n.*?(?=^- type:|\Z)'
        updated_text, count = re.subn(pattern, lambda _: yaml.safe_dump([model], sort_keys=False, width=120)+'\n',
                                     updated_text, flags=re.M|re.S)
        assert count == 1
    assert masked_model_records(updated_text) == masked_model_records(original_text)
    (output/MODEL_REL).write_bytes(updated_text.encode('utf-8'))
    assert masked_model_records((output/MODEL_REL).read_bytes().decode('utf-8')) == masked_model_records(original_text)
    # Re-read serialized YAML and PNGs. In-memory construction images are discarded.
    written_art = yaml.load((output/ART_REL).read_text(), Loader=yaml.CSafeLoader)
    registry = {}
    IMAGES.clear()
    for record in written_art:
        path = output/'Content.CMU/Resources'/record['texture'].lstrip('/')
        im = Image.open(path).convert('RGBA')
        assert all(p[3] == 255 for p in im.get_flattened_data())
        IMAGES[record['id']] = im
        registry[record['id']] = {**record, 'file': path, 'image': im}
    for crop in pool.crops:
        actual = IMAGES[crop['surface']]
        assert list(actual.size) == crop['size'] and digest(actual.tobytes()) == crop['rgbaSha256']
    surfaces.load_surfaces = lambda: registry
    written_models = [m for m in yaml.load((output/MODEL_REL).read_text(), Loader=yaml.CSafeLoader) if m.get('id') in IDS]
    verification_records = STAGE/'verification-records.yml'
    verification_records.write_text(yaml.safe_dump(written_models, sort_keys=False), encoding='utf-8')
    loaded = build_models.load_models(verification_records)
    pose_proof, after_poses = [], {}
    for state, definition in written_models[0]['doorSpriteStates'].items():
        for index, frame in enumerate(definition['frames']):
            image, source_delays = source_frame(state, index)
            before = before_poses[(state, index)]
            after = decode_written_parts(frame['parts'], registry)
            after_poses[(state, index)] = after
            assert definition['delays'] == source_delays
            field = field_proof(before, after, True)
            assert field['exactRgbaColorFieldPreserved']
            pixels = source_pixels(image, before, after)
            pose_proof.append(dict(state=state, frame=index, originalKind=original_kind[(state, index)],
                oldParts=len(before), parts=len(after), field=field, sourcePixels=pixels,
                sourceFrameRgbaSha256=digest(image.tobytes()), actualSerializedYamlAndPngs=True))
    assert written_models[0]['doorSpriteStates'] == written_models[1]['doorSpriteStates']
    for before, after in zip(originals, written_models):
        assert metadata(before) == metadata(after)
        assert after['parts'] == after['doorSpriteStates'][after['referenceState']]['frames'][0]['parts']
    write_reviews(loaded, before_poses, after_poses, output)
    proof = dict(status='Applied' if args.apply else 'Staged only; live resources are unchanged',
        sourceRsi=SOURCE.relative_to(ROOT).as_posix(), license=META['license'], copyright=META['copyright'],
        modelIds=list(IDS), metadataIdentical=True, unrelatedModelRecordsByteIdentical=True,
        unrelatedRecordTextSha256=digest(masked_model_records(original_text).encode()),
        sourceRecordMetadata=[dict(id=m['id'], fields=metadata(m)) for m in originals],
        poseCount=len(pose_proof), storedModelFrameCompositions=len(pose_proof)*2,
        exactSerializedPoseProof=pose_proof, crops=pool.crops, uniqueTextureCount=len(written_art),
        atlasIndices=[r['atlasIndex'] for r in written_art],
        writtenTextureFiles=[dict(path=(TEXTURE_REL/(r['id']+'.png')).as_posix(),
                                 sha256=digest((output/TEXTURE_REL/(r['id']+'.png')).read_bytes())) for r in written_art],
        sourceFiles=[dict(path=p.relative_to(ROOT).as_posix(), sha256=digest(p.read_bytes()))
                     for p in [SOURCE/'meta.json', *[SOURCE/(s+'.png') for s in SPECS]]],
        limitations=['Canonical South source reconstruction and existing four-direction saved-facing behavior are preserved, not replaced with independent direction models.',
            'Height, depth, hidden mechanics and existing mounting contact limitations remain inherited drafts.',
            'The strict rational proof covers serialized decimal geometry and actual source PNG color fields. Production GPU/export and native packing checks are separate.',
            'No global exports, content build, game or server launch are performed.'])
    (output/PROOF_REL).parent.mkdir(parents=True, exist_ok=True)
    (output/PROOF_REL).write_text(json.dumps(exportable(proof), indent=2)+'\n')
    if not args.apply:
        assert (ROOT/MODEL_REL).read_bytes() == original_bytes
        assert not (ROOT/ART_REL).exists()
        assert all(not (ROOT/TEXTURE_REL/(r['id']+'.png')).exists() for r in written_art)
    print(json.dumps(dict(status=proof['status'], defaultParts=[len(m['parts']) for m in loaded],
        poseParts=[p['parts'] for p in pose_proof], writtenPngs=len(written_art),
        atlasRange=[written_art[0]['atlasIndex'], written_art[-1]['atlasIndex']],
        verifiedSerializedPoses=len(pose_proof), output=str(output)), indent=2))


if __name__ == '__main__':
    main()
