"""Reproduce the five ready source-specific ladder drafts; Down2 remains deferred.

Use --staging [directory] for a separate output tree, or --output-root DIRECTORY
for dedicated installation. --check compares asset bytes without writing them.
No global export, native build, game launch, gameplay change or map link creation.
"""
import argparse
from collections import Counter
from copy import deepcopy
import hashlib
from io import BytesIO
import json
import math
from pathlib import Path
import sys

from PIL import Image, ImageDraw, ImageFont
import yaml

ROOT = next(p for p in Path(__file__).resolve().parents if (p/'Tools/three_d/build_models.py').is_file())
sys.path.insert(0, str(ROOT/'Tools/three_d'))
import build_models
import surfaces
from author_wide_machinery import world_parts, contacts

STAGE = ROOT/'.codex/ladders-production-staged'
EVIDENCE = Path('Tools/three_d/generated')
CHECK = False
SCENE_MANIFEST = None
MODEL_LIBRARY = ROOT/'Tools/three_d/generated/models.json'
CHECKED_ASSETS = []
SOURCE = ROOT/'Resources/Textures/_RMC14/Structures/ladder.rsi'
AUDIT = ROOT/'Tools/three_d/generated/next-ladders-audit.json'
MODELS = Path('Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_ladders.yml')
ART = MODELS.with_name('garrison_ladders_art.yml')
TEXTURES = Path('Content.CMU/Resources/Textures/CMU14/ThreeD/Surfaces')
NOTES = Path('Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_LADDERS.md')
VARIANTS = [
    ('CMUZLevelLadderThroughDown', 'CMU3DLadderThroughDown', 'ladder11', 'Through ladder descending section'),
    ('CMUZLevelLadderThroughDown3', 'CMU3DLadderThroughDown3', 'ladderdown', 'Marked descending ladder'),
    ('CMUZLevelLadderThroughUp1', 'CMU3DLadderThroughUp1', 'ladder10', 'Unmarked ascending ladder'),
    ('CMUZLevelLadderThroughUp3', 'CMU3DLadderThroughUp3', 'ladderup', 'Marked ascending ladder'),
    ('RMCLadder', 'CMU3DRMCLadder', 'ladderdown', 'RMC marked ladder entrance'),
]

CONTRACT = {
    'schemaVersion': 1,
    'status': 'source-specific inferred presentation geometry; shared strict source/admission gates control activation',
    'coordinateSystem': 'Game local X/Y/Z, one tile per unit; saved entity yaw rotates all geometry and apertures.',
    'floorOpening': {'min': [-.28125, -.4375], 'max': [.28125, -.03125]},
    'ceilingOpening': {'min': [-.28125, -.4375], 'max': [.28125, .09375]},
    'downRailPlaneY': [-.09375, -.03125],
    'upRailPlaneY': [-.140625, -.078125],
    'outerRailX': [-.28125, .28125],
    'sourceRimOuterX': [-.4375, .4375],
    'downRailBottom': -.80,
    'downRecessCapZ': [-.84, -.82],
    'roofSlabZ': [2.75, 2.95],
    'upRailTop': 2.95,
    'inference': 'Aperture footprint, physical depth, continuation heights and repeated full-height rungs are presentation design, not recovered 3D dimensions.',
    'mapVisibility': 'Local shallow continuation only. No other map content or invented endpoint is displayed.',
}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_json(path, doc):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(doc, indent=2)+'\n', encoding='utf-8')


def write_asset(path, data):
    """Keep byte comparison separate from writing so --check is read-only."""
    if CHECK:
        assert path.is_file() and path.read_bytes() == data, f'Generated asset differs: {path}'
    else:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
    CHECKED_ASSETS.append(path)


class Pool:
    def __init__(self):
        self.entries, self.crops, self.cache = [], [], {}
        paths = [p for p in (ROOT/MODELS.parent).glob('*.yml') if p.name != ART.name]
        for staged in (ROOT/'.codex').glob('*staged*'):
            if staged != STAGE:
                paths.extend(staged.rglob('*.yml'))
        used = set()
        for path in paths:
            for item in yaml.load(path.read_text(encoding='utf-8-sig'), Loader=yaml.CSafeLoader) or []:
                if item.get('type') == 'cmu3DSurface':
                    if path.name == ART.name:
                        index = item['atlasIndex']
                        assert 1480 <= index <= 1490 and item['id'] == f'CMU3DLadderSurface{index}'
                        assert item['texture'] == f'/Textures/CMU14/ThreeD/Surfaces/CMU3DLadderSurface{index}.png'
                        continue  # Other immutable copies of this generator's dedicated allocation.
                    used.add(item['atlasIndex'])
        assert not set(range(1480, 1491)) & used, 'Ladder atlas reservation conflicts with another batch'

    def crop(self, state, rect, purpose):
        image = Image.open(SOURCE/(state+'.png')).convert('RGBA').crop(rect)
        digest = hashlib.sha256(str(image.size).encode()+image.tobytes()).hexdigest()
        if digest not in self.cache:
            index = 1480+len(self.entries)
            assert index <= 1490
            uid = f'CMU3DLadderSurface{index}'
            path = STAGE/TEXTURES/(uid+'.png')
            buffer = BytesIO()
            image.save(buffer, format='PNG')
            write_asset(path, buffer.getvalue())
            self.entries.append({'type': 'cmu3DSurface', 'id': uid, 'atlasIndex': index,
                                 'texture': f'/Textures/CMU14/ThreeD/Surfaces/{uid}.png'})
            self.cache[digest] = uid
        uid = self.cache[digest]
        decoded = Image.open(STAGE/TEXTURES/(uid+'.png')).convert('RGBA')
        assert decoded.size == image.size and decoded.tobytes() == image.tobytes()
        self.crops.append({'state': state, 'rect': list(rect), 'purpose': purpose, 'surface': uid,
                           'rgbaSha256': hashlib.sha256(image.tobytes()).hexdigest()})
        return uid


def geometry(state, pool):
    parts = []
    image = Image.open(SOURCE/(state+'.png')).convert('RGBA')
    palette = {tuple(v[:3]) for v in image.get_flattened_data() if v[3]}
    def box(label, lo, hi, color, **more):
        parts.append({'label': label, 'min': list(lo), 'max': list(hi), 'color': color, **more})
    def colored(label, lo, hi, rgb, **more):
        assert tuple(rgb) in palette, (state, rgb)
        box(label, lo, hi, '#'+''.join(f'{v:02X}' for v in rgb), **more)
    def textured(label, lo, hi, rect, purpose, axis='XZ'):
        box(label, lo, hi, '#FFFFFF', surface=pool.crop(state, rect, purpose), surfaceAxis=axis)

    down = state in ('ladder00', 'ladder11', 'ladderdown')
    up = state in ('ladder10', 'ladderup')
    top = 2.95 if up else 1.0625 if state == 'ladder11' else .625
    bottom = -.8 if down else .02
    if state != 'ladder00':
        rail_rects = ((7, 8, 10, 16), (22, 8, 25, 16)) if up else (
            ((7, 8, 10, 16), (22, 8, 25, 16)) if state == 'ladder11' else
            ((7, 16, 10, 24), (22, 16, 25, 24)))
        rung_rect = (10, 9, 22, 13) if up else (10, 12, 22, 16) if state == 'ladder11' else (10, 20, 22, 24)
        segments = math.ceil((top-bottom)/.28)
        step = (top-bottom)/segments
        for side, x, rect in zip(('left', 'right'), (-.28125, .1875), rail_rects):
            for n in range(segments):
                z0, z1 = bottom+n*step, bottom+(n+1)*step
                textured(f'{side} rail segment {n+1}', (x, -.09375, z0), (x+.09375, -.03125, z1), rect,
                         'Original metal rail colors on a solid rectangular rail; vertical continuation is inferred.')
        rungs = [round(bottom+.14+n*.27, 6) for n in range(math.floor((top-bottom-.20)/.27)+1)]
        for n, z in enumerate(rungs):
            textured(f'physical rung {n+1}', (-.1875, -.109375, z), (.1875, -.03125, z+.09), rung_rect,
                     'Original rung face on a separate solid step, leaving actual gaps.')
    else:
        # The short source is visibly bent and uneven. Preserve its three crooked
        # rung levels above the rim, with a distinct inferred lower continuation.
        for side, centers in [('left', [-.205, -.238, -.233]), ('right', [.245, .213, .187])]:
            for n, x in enumerate(centers):
                z0, z1 = n*.20+.025, (n+1)*.20+.025
                colored(f'{side} bent upper rail {n+1}', (x-.035, -.09375, z0), (x+.035, -.03125, z1), (64, 64, 70))
                colored(f'{side} worn rail highlight {n+1}', (x-.012, -.098, z0), (x+.012, -.094, z1), (128, 128, 142))
            x = centers[0]
            colored(f'{side} inferred descending rail', (x-.035, -.09375, -.8), (x+.035, -.03125, .025), (64, 64, 70))
        for n, z in enumerate((.095, .285, .485)):
            for band, dz, height, rgb in [('lower edge', 0, .028, (64, 64, 70)),
                                          ('face', .028, .032, (128, 128, 142)),
                                          ('highlight', .06, .018, (168, 168, 173))]:
                colored(f'crooked rung {n+1} {band}', (-.212, -.109375, z+dz), (.220, -.03125, z+dz+height), rgb,
                        pitch=5 if n != 1 else 2)
        for n, z in enumerate((-.64, -.38, -.12)):
            colored(f'inferred lower rung {n+1}', (-.20, -.109375, z), (.22, -.03125, z+.065), (110, 110, 124))

    if down:
        # Open center, actual thin sidewalls, then a deliberately shallow local
        # continuation cap. No opaque patch occupies the aperture at floor level.
        dark = (29, 29, 35)
        side = (64, 64, 70)
        colored('recess west wall', (-.30625, -.4375, -.82), (-.28125, -.03125, 0), side)
        colored('recess east wall', (.28125, -.4375, -.82), (.30625, -.03125, 0), side)
        colored('recess north wall', (-.28125, -.03125, -.82), (.28125, -.0125, 0), dark)
        colored('recess south wall', (-.28125, -.45625, -.82), (.28125, -.4375, 0), dark)
        colored('inferred recessed continuation cap', (-.28125, -.4375, -.84), (.28125, -.03125, -.82), dark)
        if state in ('ladderdown', 'ladder11'):
            textured('source left hazard rim', (-.4375, -.46875, .005), (-.28125, .03125, .055), (2, 20, 7, 31), 'Original painted shaft rim, west side.', 'XY')
            textured('source right hazard rim', (.28125, -.46875, .005), (.4375, .03125, .055), (25, 20, 30, 31), 'Original painted shaft rim, east side.', 'XY')
            colored('metal near rim', (-.28125, -.46875, .005), (.28125, -.4375, .055), (29, 29, 35))
            colored('metal rear rim', (-.28125, -.03125, .005), (.28125, .03125, .055), (64, 64, 70))
        else:
            for label, lo, hi in [
                ('unpainted west entrance edge', (-.3125, -.46875, .005), (-.28125, .03125, .038)),
                ('unpainted east entrance edge', (.28125, -.46875, .005), (.3125, .03125, .038)),
                ('unpainted near entrance edge', (-.28125, -.46875, .005), (.28125, -.4375, .038)),
                ('unpainted rear entrance edge', (-.28125, -.03125, .005), (.28125, .03125, .038))]:
                colored(label, lo, hi, (64, 64, 70))
    if up:
        for side, x in [('left', -.28125), ('right', .1875)]:
            colored(side+' foot collar', (x-.015625, -.125, .002), (x+.109375, 0, .055), (64, 64, 70))
    if state in ('ladderdown', 'ladderup'):
        rect = (13, 6, 19, 9) if down else (13, 24, 19, 27)
        textured('original direction marker', ((rect[0]-16)/32, (16-rect[3])/32, .057),
                 ((rect[2]-16)/32, (16-rect[1])/32, .06), rect,
                 'Exact source direction marking. The glyph does not create a destination or a collision surface.', 'XY')
    if up:
        # Final saved-context trial clears red light 4167. Indexed follow-up
        # found two new shallow foot/grate support joins, not new obstructions.
        # Short/down forms retain their original plane.
        for part in parts:
            if any(word in part['label'] for word in ('rail', 'rung', 'foot collar')):
                part['min'][1] -= .046875
                part['max'][1] -= .046875
    assert len(parts) <= 128
    if down:
        assert not any(p['min'][0] < 0 < p['max'][0] and p['min'][1] < -.25 < p['max'][1] and
                       p['min'][2] < 0 < p['max'][2] for p in parts), 'Opening blocked at floor level'
    return parts


def stage_registry(pool):
    safe_load = yaml.safe_load
    try:
        # This process-local parser selection preserves safe YAML semantics and
        # avoids reparsing the held large library with the slower Python loader.
        yaml.safe_load = lambda value: yaml.load(value, Loader=yaml.CSafeLoader)
        registry = dict(surfaces.load_surfaces())
    finally:
        yaml.safe_load = safe_load
    for entry in pool.entries:
        path = STAGE/TEXTURES/(entry['id']+'.png')
        registry[entry['id']] = {**entry, 'file': path, 'image': Image.open(path).convert('RGBA')}
    surfaces.load_surfaces = lambda: registry


def source_proof(pool):
    results = []
    for state in sorted({v[2] for v in VARIANTS}):
        original = Image.open(SOURCE/(state+'.png')).convert('RGBA')
        selected = Image.new('L', original.size)
        for crop in [c for c in pool.crops if c['state'] == state]:
            image = Image.open(STAGE/TEXTURES/(crop['surface']+'.png')).convert('RGBA')
            expected = original.crop(crop['rect'])
            assert image.size == expected.size and image.tobytes() == expected.tobytes()
            ImageDraw.Draw(selected).rectangle((crop['rect'][0], crop['rect'][1], crop['rect'][2]-1, crop['rect'][3]-1), fill=255)
        results.append({'state': state, 'originalSha256': sha(SOURCE/(state+'.png')),
                        'originalAlphaHistogram': dict(Counter(original.getchannel('A').get_flattened_data())),
                        'exactWrittenCropCount': sum(c['state'] == state for c in pool.crops),
                        'selectedSourcePixels': sum(v > 0 for v in selected.get_flattened_data()),
                        'sourcePaletteUsedForSolids': True,
                        'alpha29Treatment': 'Source-only 2D gap shading remains visible in the reference; real geometry leaves the rung gaps open. It is not converted to an opaque or translucent membrane.'})
    return results


def review(models, audit):
    folder = STAGE/EVIDENCE/'review/ladders'
    folder.mkdir(parents=True, exist_ok=True)
    font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 18)
    small = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 14)
    for model in models:
        card = Image.new('RGB', (1200, 720), '#17212B')
        draw = ImageDraw.Draw(card)
        draw.text((16, 12), model['label']+' — draft', font=font, fill='white')
        draw.text((16, 40), model['referencePrototype']+' / '+model['referenceState']+' / '+str(len(model['parts']))+' parts', font=small, fill='#B8CDD8')
        source = Image.open(SOURCE/(model['referenceState']+'.png')).convert('RGBA').resize((224, 224), Image.Resampling.NEAREST)
        bg = Image.new('RGBA', source.size, '#63717A')
        bg.alpha_composite(source)
        card.paste(bg.convert('RGB'), (20, 110))
        draw.text((20, 348), 'Original static world sprite', font=small, fill='white')
        draw.text((20, 374), 'Reference retains alpha 29 gap shading.', font=small, fill='#B8CDD8')
        context_parts = deepcopy(model['parts'])
        opening = model.get('floorOpening')
        regions = [(-.8, -.7, .8, .6)]
        if opening:
            (x0, y0), (x1, y1) = opening['min'], opening['max']
            regions = [(-.8, -.7, x0, .6), (x1, -.7, .8, .6), (x0, -.7, x1, y0), (x0, y1, x1, .6)]
        for n, (x0, y0, x1, y1) in enumerate(regions):
            context_parts.append({'label': f'illustrative floor outside opening {n}', 'min': [x0, y0, -.07],
                                  'max': [x1, y1, -.01], 'color': '#677079'})
        for index, yaw in enumerate((-math.pi/2+.35, math.pi/2+.35)):
            panel = build_models.render_model({'parts': context_parts}, (450, 580), yaw, .73 if opening else .48,
                                               pixels_per_unit=150, screen_origin=(225, 440))
            card.paste(panel, (270+index*455, 75))
        draw.text((16, 671), 'Illustrative local floor with the proposed opening; height, depth and continuation are explicit inference.', font=small, fill='#B8CDD8')
        card.save(folder/(model['id']+'.png'))

    if SCENE_MANIFEST is None:
        return []
    library_path = MODEL_LIBRARY
    library = {m['id']: m for m in json.loads(library_path.read_text())['models']}
    library.update({m['id']: m for m in models})
    direct = {m['sourcePrototypes'][0]: m for m in models}
    specs = json.loads(SCENE_MANIFEST.read_text())
    scenes = {(s['variant'], s['level']): json.loads((SCENE_MANIFEST.parent/s['file']).read_text()) for s in specs}
    records = []
    selected_placements = [p for p in audit['placements'] if p['prototype'] in direct]
    for placement in selected_placements:
        scene = scenes[(placement['variant'], placement['level'])]
        index = {e['id']: e for e in scene['instances']}
        entity = index[placement['id']]
        model = direct[placement['prototype']]
        assert entity['position'] == placement['position'] and entity['yaw'] == placement['yaw']
        assert placement['sourceContract']['sourceOffset'] == '0,0' and not placement['sourceContract']['noRot']
        assert entity.get('modelId') == model['id'], f'Final scene has not installed {model["id"]}'
        effective = scene.get('geometryVariants', {}).get(entity.get('geometryKey'), model['parts'])
        candidate = world_parts(effective, entity['position'], entity.get('renderYaw', entity['yaw']), entity.get('renderOffset', [0,0,0]))
        neighbors, assembled = [], list(candidate)
        for reference in placement['nearbyModeled']:
            neighbor = index[reference['id']]
            other_model = library.get(neighbor.get('modelId'))
            if not other_model:
                continue
            other_parts = scene.get('geometryVariants', {}).get(neighbor.get('geometryKey'), other_model['parts'])
            other = world_parts(other_parts, neighbor['position'], neighbor.get('renderYaw', neighbor['yaw']), neighbor.get('renderOffset', [0, 0, 0]))
            hits, gap = contacts(candidate, other)
            neighbors.append({'id': neighbor['id'], 'prototype': neighbor['prototype'], 'contactsBeforeApertureAdapter': hits,
                              'minimumAabbSeparation': gap, 'position': neighbor['position'],
                              'renderYaw': neighbor.get('renderYaw', neighbor['yaw']), 'renderOffset': neighbor.get('renderOffset', [0, 0, 0])})
            assembled.extend(other)
        result = {'variant': placement['variant'], 'level': placement['level'], 'id': entity['id'], 'prototype': entity['prototype'],
                  'position': entity['position'], 'savedYaw': entity['yaw'], 'renderYaw': entity['yaw'], 'sourceOffset': [0, 0],
                  'modelId': model['id'], 'neighbors': neighbors, 'unknownNeighbors': placement['nearbyUnmapped'],
                  'contactPairsBeforeApertureAdapter': sum(len(n['contactsBeforeApertureAdapter']) for n in neighbors),
                  'floorFacts': placement['floor'], 'destinationFacts': placement['destination'],
                  'status': 'Final exported instance and neighboring geometry; overlap counts are conservative and include intended support joins.',
                  'exportedSource': {k: entity.get(k) for k in ('spriteState','spriteFrame','geometryKey','floorOpening','ceilingOpening','floorOpeningCompanions') if k in entity}}
        records.append(result)
        # These cutaways affect review pictures only; all contact checks above
        # include the complete unmodified source neighbor geometry.
        review_parts = []
        px, py = entity['position'][:2]
        for p in assembled:
            q = deepcopy(p)
            for key in ('min', 'max'):
                q[key][0] -= px
                q[key][1] -= py
            if not any(q['label'] == candidate_part['label'] and q.get('surface') == candidate_part.get('surface') for candidate_part in candidate):
                if q['min'][2] > 1.5:
                    continue
                if q['max'][2] > 1.5:
                    q['max'][2] = .4
                    if q['max'][2] <= q['min'][2]:
                        continue
            review_parts.append(q)
        card = Image.new('RGB', (1200, 670), '#17212B')
        draw = ImageDraw.Draw(card)
        draw.text((16, 12), f'{placement["variant"]} {placement["level"]:+d} / UID {entity["id"]} / {entity["prototype"]}', font=font, fill='white')
        draw.text((16, 42), 'Actual final exported geometry; floor/roof slabs omitted. Tall neighbors cut away in pictures only.', font=small, fill='#B8CDD8')
        for panel, yaw in enumerate((-math.pi/2+.35, math.pi/2+.35)):
            card.paste(build_models.render_model({'parts': review_parts}, (585, 540), yaw, .52,
                       pixels_per_unit=135, screen_origin=(292, 420)), (10+panel*600, 75))
        draw.text((16, 629), f'{result["contactPairsBeforeApertureAdapter"]} conservative geometry pairs, including intentional support joins; saved transforms unchanged.', font=small, fill='#B8CDD8')
        card.save(folder/f'context-{placement["variant"]}-{placement["level"]}-{entity["id"]}.png')
    assert len(records) == len(selected_placements)
    return records


def main():
    global STAGE, CHECK, SCENE_MANIFEST, MODEL_LIBRARY
    parser = argparse.ArgumentParser(description=__doc__)
    destination = parser.add_mutually_exclusive_group()
    destination.add_argument('--staging', nargs='?', const=str(ROOT/'.codex/ladders-production-staged'), help='Generate in a separate repository-shaped output tree.')
    destination.add_argument('--output-root', type=Path, help='Root for dedicated assets; use the repository root to install.')
    parser.add_argument('--check', action='store_true', help='Compare dedicated asset bytes without writing any output.')
    parser.add_argument('--scene-manifest', type=Path, help='Review already exported final map scenes; never runs the global exporter.')
    parser.add_argument('--models-json', type=Path, default=MODEL_LIBRARY, help='Model library used by the optional final scene review.')
    args = parser.parse_args()
    STAGE = (args.output_root or Path(args.staging or STAGE)).resolve()
    CHECK = args.check
    SCENE_MANIFEST = args.scene_manifest.resolve() if args.scene_manifest else None
    MODEL_LIBRARY = args.models_json.resolve()
    held_roots = [ROOT/'.codex/ladders-staged', ROOT/'.codex/ladder-down2-candidate']
    assert all(STAGE != root.resolve() and root.resolve() not in STAGE.parents for root in held_roots), 'Historical proposals are immutable.'
    held = {p.relative_to(ROOT).as_posix():sha(p) for root in held_roots for p in root.rglob('*') if p.is_file()}
    audit = json.loads(AUDIT.read_text())
    changing = ('.codex/', 'Content.CMU/Client/ThreeD/', 'Content.CMU/Shared/ThreeD/', 'Tools/three_d/')
    immutable = {p:h for p,h in audit['sourceSha256'].items() if not p.startswith(changing)}
    for path, expected in immutable.items():
        assert sha(ROOT/path) == expected, f'Audited source changed: {path}'
    included_prototypes = {v[0] for v in VARIANTS}
    placements = [p for p in audit['placements'] if p['prototype'] in included_prototypes]
    assert not audit['excludedPlacements']
    assert len(placements) == 40
    for placement in placements:
        source = placement['sourceContract']
        assert source == {'rsi':'_RMC14/Structures/ladder.rsi', 'state':next(v[2] for v in VARIANTS if v[0]==placement['prototype']),
                          'directions':1,'frames':1,'sourceOffset':'0,0','scale':'1,1','spriteRotation':0,
                          'noRot':False,'snapCardinals':False,'color':'#FFFFFF'}
        assert placement['yaw'] == 0 and not placement['hiddenContainer'] and placement['rootRecognized']
    for item in audit['maps']:
        assert sha(ROOT/item['path']) == item['sha256'], f'Saved map changed: {item["path"]}'
    pool = Pool()
    print('Atlas range 1480–1490 checked; reproducing five dedicated ladder models.', flush=True)
    models = []
    for proto,uid,state,label in VARIANTS:
        model = {'type':'cmu3DModel','id':uid,'label':label,'status':'draft','sourcePrototypes':[proto],
                 'referencePrototype':proto,'referenceRsi':'_RMC14/Structures/ladder.rsi','referenceState':state,
                 'referenceDirection':0,'sourceDirections':1,'useEntityRotation':True,'yawOffset':0,
                 'placement':'floor','groundOffset':[0,0],'sourceSpriteOffset':[0,0],
                 'description':'Source-specific static ladder with separate rails, rungs and open gaps. Saved yaw and source offset are retained. Height, depth, aperture footprint and bounded local continuation are inferred; no remote map or destination is invented. Draft; see SOURCES_LADDERS.md.',
                 'parts':geometry(state,pool)}
        model['spriteStates'] = {state:{'frames':[{'parts':deepcopy(model['parts'])}],'delays':[1]}}
        opening = 'floorOpening' if state in ('ladder11','ladderdown') else 'ceilingOpening'
        model[opening] = deepcopy(CONTRACT[opening])
        models.append(model)
    assert len(models) == 5 and all('floorOpeningCompanions' not in m for m in models)
    stage_registry(pool)
    validated = [build_models.validate_model(model) for model in models]
    serialized = deepcopy(models)
    vector_checks = []
    part_vectors = 0
    def scalar(container,key,count,model_id,field):
        values = container[key]
        assert isinstance(values,(list,tuple)) and len(values)==count and all(math.isfinite(v) for v in values)
        container[key] = ', '.join(f'{v:.7f}' for v in values)
        vector_checks.append({'model':model_id,'field':field,'scalar':container[key],'dimensions':count})
    for model in serialized:
        for field in ('groundOffset','sourceSpriteOffset'):
            scalar(model,field,2,model['id'],field)
        for opening in ('floorOpening','ceilingOpening'):
            if opening in model:
                for bound in ('min','max'):
                    scalar(model[opening],bound,2,model['id'],opening+'.'+bound)
        for composition in [model['parts'],*[frame['parts'] for state in model['spriteStates'].values() for frame in state['frames']]]:
            for part in composition:
                for bound in ('min','max'):
                    part[bound] = ', '.join(f'{v:.7f}' for v in part[bound])
                    assert len(part[bound].split(',')) == 3
                    part_vectors += 1
    for rel,data in ((MODELS,serialized),(ART,pool.entries)):
        write_asset(STAGE/rel,('# Source-specific ladder drafts; generated by Tools/three_d/author_ladders.py.\n'+yaml.safe_dump(data,sort_keys=False,width=110)).encode('utf-8'))
    written = build_models.load_models(STAGE/MODELS)
    import sprite_states
    for model in written:
        sprite_states.validate_source(model,build_models.resource_file)
    for a,b in zip(validated,written,strict=True):
        assert a['id'] == b['id']
        for first,second in zip(a['parts'],b['parts'],strict=True):
            for bound in ('min','max'):
                assert all(abs(x-y)<6e-8 for x,y in zip(first[bound],second[bound]))
    prior_path = held_roots[0]/MODELS
    staged_comparisons = []
    if prior_path.exists():
        prior = {m['id']:m for m in build_models.load_models(prior_path)}
        for model in written:
            assert model['parts'] == prior[model['id']]['parts']
            assert model['spriteStates'] == prior[model['id']]['spriteStates']
            for key in ('floorOpening','ceilingOpening','sourcePrototypes','sourceDirections','referenceState','useEntityRotation','yawOffset','groundOffset','sourceSpriteOffset'):
                assert model.get(key) == prior[model['id']].get(key)
            staged_comparisons.append({'model':model['id'],'partsAndSourceFramesAndMountingUnchanged':True})
        for entry in pool.entries:
            assert (STAGE/TEXTURES/(entry['id']+'.png')).read_bytes() == (held_roots[0]/TEXTURES/(entry['id']+'.png')).read_bytes()
    notes = """# Static source-specific ladders

Five exact mappings use four original static states in `_RMC14/Structures/ladder.rsi`. They cover 40 saved placements across all ten configured Redux/classic map files: 37 Redux and three classic. All retain their saved positions, zero yaw and zero sprite offset. The source noRot flag is false, so supported entity rotations rotate both geometry and apertures. Static source-state, direction, frame and transformation gates precede replacement geometry and openings. No activation animation, return endpoint or cross-map view is invented.

| Model | Exact source prototype | State | Saved placements |
|---|---|---|---:|
| CMU3DLadderThroughDown | CMUZLevelLadderThroughDown | ladder11 | 1 |
| CMU3DLadderThroughDown3 | CMUZLevelLadderThroughDown3 | ladderdown | 15 |
| CMU3DLadderThroughUp1 | CMUZLevelLadderThroughUp1 | ladder10 | 5 |
| CMU3DLadderThroughUp3 | CMUZLevelLadderThroughUp3 | ladderup | 16 |
| CMU3DRMCLadder | RMCLadder | ladderdown | 3 |

Separate solid rails and rungs leave actual open spaces. Ladder11 keeps its longer visible upper section and existing downward controller. Ladder10 and ladderup have an inferred full-height extension to the existing 2.95 ceiling top. Written rail, rung, paint and marker crops preserve exact original RGBA pixels. Other faces use original palette colors. Alpha-29 source shading between rungs remains in the reference; it is not converted into a membrane between physical rungs.

The rail span is 18 source pixels / 0.5625 tile; the marked rim spans 28 pixels / 0.875 tile. Aperture depth, hidden faces, rail standoff, full-height continuation and recessed cap remain inferred. The floor opening is X [-0.28125, 0.28125], Y [-0.4375, -0.03125]. The ceiling opening is X [-0.28125, 0.28125], Y [-0.4375, 0.09375]. Recess walls end at a cap below -0.82, never at another rendered map. Gameplay tiles, collisions and links are unchanged.

The up rail/rung/foot plane shifts 0.046875 tile south, following the held source/context trial. That trial cleared red light 4167. A later indexed audit found two new shallow foot/grate joins at surface UID 19969 while removing two light and two lower-rail/grate pairs. Wire rails, APCs, lights and other fixtures remain present. This asset release preserves the reviewed geometry; it does not assert every context contact or packed admission is resolved. The corner platform at Redux UID 16881 and final native/export checks remain distinct context work.

## Deferred composition

CMU3DLadderThroughDown2 / CMUZLevelLadderThroughDown2 is deliberately excluded. Its one Redux surface placement, UID 12468, shares a pivot with a pallet and two cartons. Its separate inferred fitting proposal and proofs remain held under `.codex/ladder-down2-candidate`. This release installs no Down2 model, no companion part frames and no global pallet/carton changes. The unresolved composition retains its original representation.

## Source attribution and regeneration

Original ladder artwork is CC-BY-SA-3.0 per `Resources/Textures/_RMC14/Structures/ladder.rsi/meta.json`, from CM-SS13 structures.dmi at https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/structures.dmi and dropship_equipment.dmi at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/dropship/dropship_equipment.dmi. All eleven derived crops retain exact source RGBA pixels and this attribution. Atlas slots are 1480–1490.

Run `python Tools/three_d/author_ladders.py --staging <directory>` for a separate output tree, or `--output-root .` to regenerate only these dedicated assets. Add `--check` for read-only asset comparison. Optional `--scene-manifest <final-scenes.json> --models-json <models.json>` reads already exported scenes for context cards; it never runs a global exporter. Native Vector2/Vector3 fields serialize as scalar comma-separated values. The generator verifies written PNGs, serialized YAML, source frames and saved map hashes. Historical staged inputs and the Down2 proposal are preserved. These remain drafts with inferred physical dimensions. No game client/server or build is started or stopped.
"""
    write_asset(STAGE/NOTES,notes.encode('utf-8'))
    proof = source_proof(pool)
    for path,expected in held.items():
        assert sha(ROOT/path) == expected, f'Historical staged input changed: {path}'
    if CHECK:
        print(json.dumps({'assetFilesChecked':len(CHECKED_ASSETS),'models':len(written),'textures':len(pool.entries),
                          'sourceStatesChecked':len(proof),'savedPlacements':len(placements),'byteIdentical':True,'writes':0}),flush=True)
        return
    contexts = review(written,audit)
    inputs = {**immutable,AUDIT.relative_to(ROOT).as_posix():sha(AUDIT)}
    scene_inputs = {}
    if SCENE_MANIFEST:
        for path in [SCENE_MANIFEST,MODEL_LIBRARY,*[SCENE_MANIFEST.parent/item['file'] for item in json.loads(SCENE_MANIFEST.read_text())]]:
            scene_inputs[str(path)] = sha(path)
    report = {'schemaVersion':2,'status':'Five ready source-matched drafts installed; Down2 deferred. Source/asset checks passed, final native/export/context validation remains separate.',
              'assetChecksPass':True,'contextReviewCount':len(contexts),'finalContextFittingClaimed':False,
              'models':[{'id':m['id'],'sourcePrototypes':m['sourcePrototypes'],'parts':len(m['parts']),
                         'savedPlacements':sum(p['prototype'] in m['sourcePrototypes'] for p in placements)} for m in written],
              'sourceProof':proof,'sourceCrops':pool.crops,'atlasIndices':[e['atlasIndex'] for e in pool.entries],
              'nativeScalarVectorChecks':vector_checks,'nativeScalarPartVectorCount':part_vectors,
              'staticSourceFrameChecks':[{'model':m['id'],'state':m['referenceState'],'directions':1,'frames':1,
                                         'sourceSpriteOffset':[0,0],'useEntityRotation':True,'sourceResourceValidated':True} for m in written],
              'savedPlacementCount':len(placements),'allConfiguredMapCount':len(audit['maps']),
              'placementCounts':dict(Counter(p['variant'] for p in placements)),
              'placements':[{k:p[k] for k in ('variant','level','map','id','prototype','position','yaw','sourceContract')} for p in placements],
              'deferred':{'model':'CMU3DLadderThroughDown2','prototype':'CMUZLevelLadderThroughDown2','savedPlacements':1,'reason':'Pallet/carton compound fitting and native admission remain deferred.'},
              'stagedModelComparisons':staged_comparisons,'apertureContract':CONTRACT,'contexts':contexts,
              'inputSha256':inputs,'sceneInputSha256':scene_inputs,
              'writtenAssetsSha256':{path.relative_to(STAGE).as_posix():sha(path) for path in CHECKED_ASSETS},
              'generatorSha256':sha(Path(__file__)),'heldStagedInputsSha256':held,'heldStagedInputsUnchanged':True,
              'limitations':['Physical height, depth, aperture placement and hidden construction remain inferred.',
                             'Down2 and all companion substitutions are excluded.',
                             'Source checks and review images do not establish final native admission, player visibility or gameplay acceptance.']}
    write_json(STAGE/EVIDENCE/'ladders-proof.json',report)
    write_json(STAGE/EVIDENCE/'ladders-aperture-contract.json',CONTRACT)
    print(json.dumps({'models':len(written),'parts':[len(m['parts']) for m in written],'textures':len(pool.entries),
                      'savedPlacements':len(placements),'contextCards':len(contexts),
                      'outputRoot':str(STAGE),'historicalStagingUnchanged':True}),flush=True)


if __name__ == '__main__':
    main()
