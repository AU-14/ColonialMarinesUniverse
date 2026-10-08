"""Author source-matched wide machinery with exact source-clock frame surfaces.

This generator writes only its four model records, dedicated art, proof and review
outputs. It never exports the shared model library or changes saved gameplay data.
"""
import copy
import hashlib
import itertools
import json
import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont
import yaml

import build_models
import surfaces

ROOT = Path(__file__).resolve().parents[2]
PROTOTYPES = ROOT/'Content.CMU/Resources/Prototypes/CMU14/ThreeD'
MODEL_FILE = PROTOTYPES/'garrison_wide_machinery.yml'
ART_FILE = PROTOTYPES/'garrison_wide_machinery_art.yml'
TEXTURES = ROOT/'Content.CMU/Resources/Textures/CMU14/ThreeD/Surfaces'
GENERATED = ROOT/'Tools/three_d/generated'
REVIEW = GENERATED/'review/wide-machinery'
RSI = '_RMC14/Structures/hybrisa_machine_props.rsi'
SOURCE = ROOT/'Resources/Textures'/RSI
META = json.loads((SOURCE/'meta.json').read_text())
AUDIT_FILE = GENERATED/'next-wide-machinery-audit.json'
BASE_Z = .04
FRAME_EVIDENCE = []


class SurfacePool:
    def __init__(self):
        self.art = []
        self.by_hash = {}
        self.crops = []
        # Re-running this generator may replace its own slots only.
        existing = [(p.name, row) for p in PROTOTYPES.glob('*.yml') if p != ART_FILE
                    for row in (yaml.load(p.read_text(), Loader=yaml.CSafeLoader) or [])
                    if row.get('type') == 'cmu3DSurface']
        self.used = {row['atlasIndex'] for _, row in existing}
        assert not any(uid.startswith('CMU3DWideMachinerySurface')
                       for _, row in existing for uid in [row['id']])

    def store(self, pixels, context):
        signature = hashlib.sha256(str(pixels.size).encode()+pixels.tobytes()).hexdigest()
        if signature not in self.by_hash:
            index = 1250+len(self.art)
            assert index not in self.used, f'Atlas slot {index} is already in use'
            assert index <= surfaces.MAX_SURFACES
            uid = f'CMU3DWideMachinerySurface{index}'
            pixels.save(TEXTURES/(uid+'.png'))
            self.by_hash[signature] = uid
            self.art.append(dict(type='cmu3DSurface', id=uid, atlasIndex=index,
                                 texture=f'/Textures/CMU14/ThreeD/Surfaces/{uid}.png'))
        uid = self.by_hash[signature]
        actual = Image.open(TEXTURES/(uid+'.png')).convert('RGBA')
        assert actual.size == pixels.size and actual.tobytes() == pixels.tobytes()
        self.crops.append(dict(**context, surface=uid, rgbaSha256=hashlib.sha256(actual.tobytes()).hexdigest(),
                               size=list(actual.size), exactWrittenRgba=True))
        return uid


def frames(state):
    meta = next(s for s in META['states'] if s['name'] == state)
    delays = meta.get('delays', [[1]])[0]
    image = Image.open(SOURCE/(state+'.png')).convert('RGBA')
    cols = image.width//64
    return [image.crop((i%cols*64, i//cols*64, i%cols*64+64, i//cols*64+64))
            for i in range(len(delays))], delays


def composition(design, state, frame_index, frame, pool):
    parts = []
    reconstructed = Image.new('RGBA', (64, 64))
    opaque = [c for c in frame.get_flattened_data() if c[3]]
    body_color = '#'+''.join(f'{v:02X}' for v in max(set(opaque), key=lambda c: (opaque.count(c), c))[:3])

    def box(label, rect, near=-.205, far=.2, color=None, shape='Box'):
        x0, y0, x1, y1 = rect
        part = dict(label=label, min=[(x0-16)/32, near, BASE_Z+(64-y1)/32],
                    max=[(x1-16)/32, far, BASE_Z+(64-y0)/32], color=color or body_color)
        if shape != 'Box': part['shape'] = shape
        parts.append(part)

    def face(label, rect, depth, hole=None):
        crop = frame.crop(rect)
        if hole:
            ImageDraw.Draw(crop).rectangle((hole[0]-rect[0], hole[1]-rect[1],
                                            hole[2]-rect[0]-1, hole[3]-rect[1]-1), fill=(0, 0, 0, 0))
        reconstructed.alpha_composite(crop, (rect[0], rect[1]))
        bounds = crop.getbbox()
        if not bounds: return
        image = crop.crop(bounds)
        actual = (rect[0]+bounds[0], rect[1]+bounds[1], rect[0]+bounds[2], rect[1]+bounds[3])
        uid = pool.store(image, dict(design=design, state=state, frame=frame_index, region=label, crop=list(actual), hole=hole))
        box(label, actual, depth, depth+.003, '#FFFFFF')
        parts[-1].update(surface=uid, surfaceAxis='XZ')

    def ring(rect):
        x0, y0, x1, y1 = rect
        center = [((x0+x1)/2-16)/32, -.239, BASE_Z+(64-(y0+y1)/2)/32]
        rx, rz = (x1-x0)/64, (y1-y0)/64
        for index in range(12):
            angle = math.tau*index/12
            c = [center[0]+rx*math.cos(angle), center[1], center[2]+rz*math.sin(angle)]
            half = [.012, .011, min(rx, rz)*math.tan(math.pi/12)]
            # A box is unchanged by a half-turn. Keep equivalent pitch in the
            # supported [-90, 90] range without changing its ring placement.
            pitch = (math.degrees(angle)+90)%180-90
            parts.append(dict(label='inferred recessed fan lip', min=[v-h for v,h in zip(c,half)],
                              max=[v+h for v,h in zip(c,half)], color=body_color, pitch=pitch))

    if design in (3, 6, 7):
        box('left service cabinet', (7, 30, 28, 64))
        box('left raised cap', (8, 29, 27, 31), -.215, .21)
        face('left top casting', (0, 0, 29, 40), -.244)
        face('left recessed grille', (0, 40, 29, 55), -.233)
        face('left status controls', (0, 55, 29, 64), -.249)
    if design == 3:
        screen = (40, 42, 52, 50)
        box('monitor rear cabinet', (30, 35, 57, 64), -.055, .2)
        for label, rect in [('top bezel', (29, 34, 58, 42)), ('left bezel', (29, 42, 40, 64)),
                            ('right bezel', (52, 42, 58, 64)), ('lower screen shelf', (40, 50, 52, 64))]:
            box(label, rect, -.22, .2)
        face('monitor casting and bezel', (29, 0, 64, 64), -.249, screen)
        face('recessed green display', screen, -.21)
    elif design in (6, 7):
        recess = (33, 50, 55, 58)
        box('rack rear wall', (30, 43, 58, 63), .01, .2)
        for label, rect in [('rack left jamb', (30, 43, 33, 64)), ('rack right jamb', (55, 43, 58, 64)),
                            ('rack top vent', (33, 43, 55, 50)), ('rack base ports', (33, 58, 55, 64))]:
            box(label, rect, -.225, .2)
        face('rack opening frame and ports', (29, 41, 64, 64), -.249, recess)
        face('rack dark recess and status row', recess, -.18)
        if design == 6:
            face('rack top source outline', (29, 0, 64, 41), -.23)
        else:
            fan = (39, 14, 49, 24)
            box('upper fan rear case', (30, 16, 59, 41), -.06, .2)
            box('upper fan crown', (34, 12, 55, 16), -.06, .2)
            face('upper fan casting and lower vent', (29, 0, 64, 41), -.239, fan)
            face('recessed fan source face', fan, -.19)
            ring(fan)
    else:
        fan = (42, 36, 52, 46)
        box('left vent housing', (11, 31, 31, 64), -.2, .2)
        box('right fan and grille housing', (32, 30, 54, 64), -.17, .2)
        box('left stepped cap', (13, 29, 30, 32), -.2, .21)
        box('right stepped cap', (33, 29, 51, 32), -.2, .21)
        face('left top intake', (0, 0, 32, 40), -.249)
        face('left lower grille', (0, 40, 32, 55), -.233)
        face('left lower controls', (0, 55, 32, 64), -.249)
        face('right service face and fan surround', (32, 0, 64, 64), -.249, fan)
        face('recessed circular fan artwork', fan, -.205)
        ring(fan)
    assert reconstructed.tobytes() == frame.tobytes(), (design, state, frame_index)
    FRAME_EVIDENCE.append(dict(design=design, state=state, frame=frame_index, parts=len(parts),
                               sourceRgbaReassembled=True, sourceRgbaSha256=hashlib.sha256(frame.tobytes()).hexdigest()))
    return parts


def world_parts(parts, position, yaw=0, offset=(0,0,0)):
    out = []
    c, s = math.cos(yaw), math.sin(yaw)
    for original in parts:
        p = copy.deepcopy(original)
        lo, hi = build_models.vector(p['min']), build_models.vector(p['max'])
        center = [(a+b)/2 for a,b in zip(lo,hi)]
        half = [(b-a)/2 for a,b in zip(lo,hi)]
        x, y = center[:2]
        center = [c*x-s*y+position[0]+offset[0], s*x+c*y+position[1]+offset[1], center[2]+offset[2]]
        p.update(min=[v-h for v,h in zip(center,half)], max=[v+h for v,h in zip(center,half)],
                 yaw=p.get('yaw',0)+math.degrees(yaw))
        out.append(p)
    return out


def box_bounds(part):
    lo, hi = part['min'], part['max']
    center = [(a+b)/2 for a,b in zip(lo,hi)]
    half = [(b-a)/2 for a,b in zip(lo,hi)]
    p = math.radians(part.get('pitch',0)); c,s=abs(math.cos(p)),abs(math.sin(p))
    half = [half[0]*c+half[2]*s,half[1],half[0]*s+half[2]*c]
    y = math.radians(part.get('yaw',0)); c,s=abs(math.cos(y)),abs(math.sin(y))
    half = [half[0]*c+half[1]*s,half[0]*s+half[1]*c,half[2]]
    return [v-h for v,h in zip(center,half)],[v+h for v,h in zip(center,half)]


def contacts(first,second):
    hits=[]; minimum=math.inf
    for a in first:
        lo,hi=box_bounds(a)
        for b in second:
            blo,bhi=box_bounds(b)
            gaps=[max(blo[i]-hi[i],lo[i]-bhi[i],0) for i in range(3)]
            minimum=min(minimum,math.sqrt(sum(g*g for g in gaps)))
            overlap=[min(hi[i],bhi[i])-max(lo[i],blo[i]) for i in range(3)]
            if all(v>1e-6 for v in overlap):hits.append(dict(part=a['label'],neighborPart=b['label'],overlap=overlap))
    return hits,minimum


def fixture_bounds(neighbor):
    found=[]
    for definition in (neighbor.get('fixtures') or {}).get('fixtures',{}).values():
        shape=definition.get('shape',{})
        if 'bounds' not in shape:continue
        x0,y0,x1,y1=map(float,shape['bounds'].split(','));a=neighbor['yaw'];c,s=math.cos(a),math.sin(a)
        corners=[(c*x-s*y+neighbor['position'][0],s*x+c*y+neighbor['position'][1]) for x,y in itertools.product((x0,x1),(y0,y1))]
        found.append(dict(min=[min(p[i] for p in corners) for i in range(2)],max=[max(p[i] for p in corners) for i in range(2)]))
    return found


def ordered_geometry(parts):
    return [{k: v for k, v in p.items() if k not in ('color', 'surface')} for p in parts]


def written_texture_proof(models, pool):
    """Reconstruct source canvases using actual YAML coordinates and on-disk PNGs."""
    loaded_art = surfaces.load_surfaces()
    evidence = []
    openings = {3: (46, 46, -.23), 5: (47, 41, -.225),
                6: (44, 54, -.215), 7: (44, 19, -.22)}
    for model in models:
        design = int(model['referencePrototype'].removeprefix('RMCMachinePropBig'))
        assert model['status'] == 'draft' and model['useEntityRotation'] is False
        assert model['sourceDirections'] == 1 and model['referenceDirection'] == 0
        pair = lambda value: tuple(float(v) for v in (value.split(',') if isinstance(value, str) else value))
        assert pair(model['groundOffset']) == (0.0, 0.0)
        assert pair(model['sourceSpriteOffset']) == (.5, .5)
        assert model['parts'] == model['spriteStates'][model['referenceState']]['frames'][0]['parts']
        for state, definition in model['spriteStates'].items():
            original, delays = frames(state)
            assert definition['delays'] == delays
            for index, stored in enumerate(definition['frames']):
                assert ordered_geometry(stored['parts']) == ordered_geometry(model['parts'])
                reconstructed = Image.new('RGBA', (64, 64))
                surface_count = 0
                for part in stored['parts']:
                    if not part.get('surface'):
                        continue
                    assert part['surfaceAxis'] == 'XZ' and not part.get('yaw', 0) and not part.get('pitch', 0)
                    assert part['color'] == '#FFFFFF'
                    image = Image.open(loaded_art[part['surface']]['file']).convert('RGBA')
                    lo, hi = part['min'], part['max']
                    pixels = (lo[0]*32+16, 64-(hi[2]-BASE_Z)*32,
                              hi[0]*32+16, 64-(lo[2]-BASE_Z)*32)
                    rect = tuple(round(v) for v in pixels)
                    assert all(abs(a-b) < 1e-5 for a, b in zip(pixels, rect))
                    assert image.size == (rect[2]-rect[0], rect[3]-rect[1])
                    reconstructed.alpha_composite(image, rect[:2])
                    surface_count += 1
                assert reconstructed.tobytes() == original[index].tobytes(), (model['id'], state, index)
                # A point behind the surrounding face and ahead of its inset art
                # must stay empty, verifying that the recess is not a solid box.
                px, py, depth = openings[design]
                point = ((px-16)/32, depth, BASE_Z+(64-py)/32)
                for part in stored['parts']:
                    lo, hi = box_bounds(part)
                    assert not all(lo[i] < point[i] < hi[i] for i in range(3)), (state, index, part['label'])
                evidence.append(dict(model=model['id'], state=state, frame=index,
                    parts=len(stored['parts']), surfaceParts=surface_count,
                    exactRgbaFromWrittenTexturesAndGeometry=True,
                    rgbaSha256=hashlib.sha256(reconstructed.tobytes()).hexdigest(),
                    recessProbe=list(point), recessProbeEmpty=True))
    assert len(evidence) == 28 and all(c['exactWrittenRgba'] for c in pool.crops)
    return evidence


def write_source_sheet(models):
    sheet = Image.new('RGB', (1440, 1010), '#101820')
    draw = ImageDraw.Draw(sheet)
    draw.text((15, 12), 'WIDE MACHINERY / original ON and OFF source frames / DRAFT ASSETS',
              fill='#F0E3BE', font=ImageFont.load_default(size=19))
    for row, model in enumerate(models):
        source, delays = frames(model['referenceState'])
        off, _ = frames(model['referenceState']+'_off')
        top = 70+row*235
        draw.text((15, top), f'{model["label"]} / {len(model["parts"])} parts / source RGB changes only',
                  fill='#E6DABA', font=ImageFont.load_default(size=17))
        for index, original in enumerate(source+off):
            x = 10+index*158
            image = original.resize((150, 150), Image.Resampling.NEAREST)
            sheet.paste(image, (x, top+35), image)
            label = f'{index}: {delays[index]} seconds' if index < len(source) else 'Static OFF'
            draw.text((x, top+190), label, fill='#ADC4CF', font=ImageFont.load_default(size=13))
    sheet.save(REVIEW/'all-source-states.png')


def write_context_audit(models):
    """Check the authored records against current saved-context scene snapshots."""
    library_file = GENERATED/'models.json'
    library = {m['id']: m for m in json.loads(library_file.read_text())['models']}
    library.update({m['id']: m for m in models})
    direct = {p: m for m in models for p in m['sourcePrototypes']}
    scene_files = {-1: GENERATED/'platform-three-redux-minus1-scene.json',
                   -2: GENERATED/'platform-three-redux-minus2-scene.json',
                   1: GENERATED/'platform-three-redux-plus1-scene.json',
                   2: GENERATED/'platform-three-redux-plus2-scene.json'}
    scenes = {level: json.loads(path.read_text()) for level, path in scene_files.items()}
    indexed = {level: {i['id']: i for i in scene['instances']} for level, scene in scenes.items()}
    output = REVIEW/'contexts'
    output.mkdir(parents=True, exist_ok=True)
    results = []
    for record in json.loads(AUDIT_FILE.read_text())['placements']:
        model = direct[record['prototype']]
        level = record['level']
        px, py = record['position'][:2]
        candidate = world_parts(model['parts'], record['position'])
        result = dict(id=record['id'], level=level, prototype=record['prototype'],
            savedPosition=record['position'], savedYaw=record['yaw'], renderYaw=0,
            geometryInvariantAcrossAllSourceStates=True, modeledContacts=[], clearances=[],
            unknownNeighbors=[], existingFixtureOverlapCandidates=[])
        assembled = [dict(label='flat reference floor', min=[px-1.8, py-1.4, -.008],
                         max=[px+2.2, py+1.6, -.004], color='#35434A')]+candidate
        for neighbor in record['neighbors']:
            own = record['worldFixtureAabb']
            for other in fixture_bounds(neighbor):
                overlap = [min(own['max'][i], other['max'][i])-max(own['min'][i], other['min'][i]) for i in range(2)]
                if all(v > 1e-6 for v in overlap):
                    result['existingFixtureOverlapCandidates'].append(dict(id=neighbor['id'],
                        prototype=neighbor['prototype'], overlap=overlap, fixtures=neighbor['fixtures'],
                        assessment='Existing saved fixture AABB overlap; masks and runtime interaction are not simulated. Subfloor fixtures may intentionally share ground XY.'))
            instance = indexed[level].get(neighbor['id'])
            model_id = (instance or {}).get('modelId', (neighbor.get('model') or {}).get('id'))
            other_model = library.get(model_id) or direct.get(neighbor['prototype'])
            if other_model is None:
                result['unknownNeighbors'].append(dict(id=neighbor['id'], prototype=neighbor['prototype']))
                continue
            yaw = (instance or {}).get('renderYaw', 0 if (neighbor.get('sprite') or {}).get('noRot')
                and other_model.get('sourceDirections', 1) == 1 else neighbor['yaw'])
            offset = (instance or {}).get('renderOffset', [0, 0, 0])
            source_parts = scenes[level].get('geometryVariants', {}).get((instance or {}).get('geometryKey'), other_model['parts'])
            other = world_parts(source_parts, neighbor['position'], yaw, offset)
            hits, gap = contacts(candidate, other)
            if hits:
                result['modeledContacts'].append(dict(id=neighbor['id'], prototype=neighbor['prototype'],
                    partPairs=len(hits), samples=hits[:6]))
            if math.isfinite(gap):
                result['clearances'].append(dict(id=neighbor['id'], prototype=neighbor['prototype'],
                    minimumPartAabbSeparation=gap))
            if abs(neighbor['delta'][0]) <= 1.7 and abs(neighbor['delta'][1]) <= 1.5:
                assembled.extend(other)
        result['clearances'].sort(key=lambda item: item['minimumPartAabbSeparation'])
        results.append(result)
        for part in assembled:
            for bound in ('min', 'max'):
                part[bound] = [part[bound][0]-px, part[bound][1]-py, part[bound][2]]
        card = Image.new('RGB', (1100, 560), '#101820')
        draw = ImageDraw.Draw(card)
        draw.text((16, 12), f'{model["label"]} / Redux {level:+d} UID {record["id"]} / saved yaw {math.degrees(record["yaw"]):.0f}, visual yaw 0',
                  fill='#F0E3BE', font=ImageFont.load_default(size=17))
        draw.text((16, 40), 'Authored drafts + current neighbors; floor is a flat reference. Orange = saved physics; cyan = visual XY extent.',
                  fill='#A8BFCE', font=ImageFont.load_default(size=13))
        panel = build_models.render_model(dict(parts=assembled), size=(740, 470), yaw=-math.pi/2+.22,
            pitch=.55, pixels_per_unit=120, screen_origin=(310, 345))
        card.paste(panel, (355, 75))
        original = frames(model['referenceState'])[0][0].resize((256, 256), Image.Resampling.NEAREST)
        card.paste(original, (25, 68), original)
        ox, oy, scale = 145, 422, 75
        for gx in range(-1, 3):
            draw.line((ox+gx*scale, 347, ox+gx*scale, 535), fill='#344651')
        for gy in range(-1, 2):
            draw.line((20, oy-gy*scale, 340, oy-gy*scale), fill='#344651')
        own = record['worldFixtureAabb']
        draw.rectangle((ox+(own['min'][0]-px)*scale, oy-(own['max'][1]-py)*scale,
                        ox+(own['max'][0]-px)*scale, oy-(own['min'][1]-py)*scale), outline='#E7A24D', width=3)
        lows, highs = zip(*(box_bounds(p) for p in model['parts']))
        draw.rectangle((ox+min(p[0] for p in lows)*scale, oy-max(p[1] for p in highs)*scale,
                        ox+max(p[0] for p in highs)*scale, oy-min(p[1] for p in lows)*scale), outline='#51C6DE', width=2)
        draw.ellipse((ox-3, oy-3, ox+3, oy+3), fill='white')
        draw.text((20, 540), f'{len(result["modeledContacts"])} modeled contact candidates; {len(result["existingFixtureOverlapCandidates"])} source fixture overlaps',
                  fill='#A8BFCE', font=ImageFont.load_default(size=13))
        card.save(output/f'context-{level}-{record["id"]}.png')
    report = dict(placementCount=len(results),
        modeledNeighborComparisons=sum(len(r['clearances']) for r in results),
        modeledContactPlacements=sum(bool(r['modeledContacts']) for r in results),
        unknownNeighborOccurrences=sum(len(r['unknownNeighbors']) for r in results),
        placements=results,
        inputs=[dict(path=str(p.relative_to(ROOT)).replace('\\', '/'),
                     sha256=hashlib.sha256(p.read_bytes()).hexdigest())
                for p in (AUDIT_FILE, MODEL_FILE, library_file, *scene_files.values())],
        limitations=['Part AABB separation is conservative geometry evidence, not gameplay movement clearance or collision-mask simulation.',
            'Saved physics is preserved, including noRot visual and rotated-fixture discrepancies. Unknown neighbors are explicitly unresolved.',
            'Floor shown in review cards is a flat reference underlay; source terrain elevation is not reconstructed here.',
            'The corner model is being refined separately without changing its bounds; these input hashes identify the exact geometry reviewed.',
            'Native frame selection, source fallbacks, packing, global export and runtime verification are owned by the parent task; no game or server was launched.'])
    (GENERATED/'wide-machinery-context-audit.json').write_text(json.dumps(report, indent=2)+'\n')
    return report


def main():
    FRAME_EVIDENCE.clear()
    TEXTURES.mkdir(parents=True, exist_ok=True)
    REVIEW.mkdir(parents=True, exist_ok=True)
    pool = SurfacePool()
    models = []
    for design in (3, 5, 6, 7):
        proto = f'RMCMachinePropBig{design}'
        states = {}
        for state in (f'buildingventbig{design}', f'buildingventbig{design}_off'):
            source_frames, delays = frames(state)
            definitions = []
            for index, source in enumerate(source_frames):
                parts = composition(design, state, index, source, pool)
                for part in parts:
                    for bound in ('min', 'max'):
                        part[bound] = ', '.join(f'{v:.7f}' for v in part[bound])
                definitions.append(dict(parts=parts))
            states[state] = dict(frames=definitions, delays=delays)
        on_state = f'buildingventbig{design}'
        models.append(dict(type='cmu3DModel', id='CMU3D'+proto, label=f'Hybrisa wide machinery {design}',
            status='draft', sourcePrototypes=[proto], referencePrototype=proto, referenceRsi=RSI,
            referenceState=on_state, referenceDirection=0, sourceDirections=1,
            sourceSpriteOffset='0.5,0.5', groundOffset='0,0', useEntityRotation=False, yawOffset=0,
            description='Source-specific cabinet solids and physically recessed controls/intakes with exact source frame surfaces. '
            'The left pivot and rightward footprint include Sprite.offset.X once in local X; noRot fixes visual yaw while saved physics remains unchanged. '
            'Source height at 32 pixels per tile, shallow depth, plain rear faces, segmented fan lips and base Z .04 are inferred. '
            'ON frame timing and static OFF resources follow the source without inventing a power controller or fan rotation. '
            'Draft; native source-clock and packing evidence is tracked separately. CC-BY-SA-3.0 CM-SS13 artwork; see SOURCES_WIDE_MACHINERY.md.',
            parts=copy.deepcopy(states[on_state]['frames'][0]['parts']), spriteStates=states))
    ART_FILE.write_text('# Exact CC-BY-SA-3.0 source crops; see SOURCES_WIDE_MACHINERY.md.\n'+
                       yaml.safe_dump(pool.art, sort_keys=False, width=110), encoding='utf-8')
    MODEL_FILE.write_text('# Four fixed-facing wide machinery drafts; exact source states and original timing.\n'+
                         yaml.safe_dump(models, sort_keys=False, width=110), encoding='utf-8')
    surfaces.load_surfaces.cache_clear()
    loaded = build_models.load_models(MODEL_FILE)
    frame_proof = written_texture_proof(loaded, pool)
    inventory = {p['id']: p for p in json.loads((GENERATED/'inventory.json').read_text())['prototypes']}
    build_models.write_reviews(loaded, REVIEW, inventory)
    off_models = []
    for original in loaded:
        model = copy.deepcopy(original)
        state = model['referenceState']+'_off'
        model['referenceState'] = state
        model['label'] += ' / static OFF resource'
        model['parts'] = copy.deepcopy(model['spriteStates'][state]['frames'][0]['parts'])
        off_models.append(model)
    build_models.write_reviews(off_models, REVIEW/'off-states', inventory)
    write_source_sheet(loaded)
    context = write_context_audit(loaded)
    summaries = []
    for model in loaded:
        lows, highs = zip(*(box_bounds(p) for p in model['parts']))
        summaries.append(dict(id=model['id'], sourcePrototype=model['referencePrototype'],
            partCount=len(model['parts']),
            bounds=dict(min=[min(p[i] for p in lows) for i in range(3)], max=[max(p[i] for p in highs) for i in range(3)]),
            states={state: dict(frameCount=len(data['frames']), delays=data['delays'], cycleSeconds=sum(data['delays']))
                    for state, data in model['spriteStates'].items()}))
    evidence = dict(status='Four authored drafts; focused resource/source/context checks complete. Global exports and native verification are separate.',
        sourceRsi=RSI, license=META['license'], copyright=META['copyright'],
        sourceInputs=[dict(path=str(p.relative_to(ROOT)).replace('\\', '/'), sha256=hashlib.sha256(p.read_bytes()).hexdigest())
                      for p in [ROOT/'Resources/Prototypes/_RMC14/Entities/Structures/hybrisa_machine_props.yml',
                                SOURCE/'meta.json', *[SOURCE/(state+'.png') for m in loaded for state in m['spriteStates']]]],
        modelCount=len(loaded), models=summaries,
        uniqueTextureCount=len(pool.art), atlasIndices=[s['atlasIndex'] for s in pool.art],
        sourceFrameCount=len(frame_proof), writtenFrameProof=frame_proof, sourceCrops=pool.crops,
        sourcePlacementRule='Local X=(pixelX-16)/32 includes source offset X once; GroundOffset is zero. Screen offset Y does not become ground Y. noRot preserves visual yaw zero independently of saved collision yaw.',
        inferredDimensions=dict(baseZ=BASE_Z, mainBodyDepth=[-.225, .2], frontArtwork=-.249),
        contextSummary={k: context[k] for k in ('placementCount', 'modeledNeighborComparisons', 'modeledContactPlacements', 'unknownNeighborOccurrences')},
        limitations=context['limitations'])
    (GENERATED/'wide-machinery-source-audit.json').write_text(json.dumps(evidence, indent=2)+'\n')
    print(json.dumps(dict(modelCount=len(loaded), models=[(m['id'], len(m['parts'])) for m in loaded],
        textures=len(pool.art), atlasRange=[pool.art[0]['atlasIndex'], pool.art[-1]['atlasIndex']],
        writtenFrames=len(frame_proof), context=evidence['contextSummary']), indent=2))


if __name__ == '__main__':
    main()
