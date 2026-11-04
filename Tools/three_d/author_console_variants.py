"""Author seven console designs, with exact four-view source-clock artwork.

Only dedicated assets and focused evidence are written; no shared exports or
runtime processes are touched. Native vectors are scalar resource strings.
"""
import argparse
import copy
import hashlib
import json
import math
from collections import Counter
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageOps
import yaml

import build_models
import placement
import surfaces
from author_wide_machinery import world_parts, contacts

ROOT = Path(__file__).resolve().parents[2]
GEN = ROOT / 'Tools/three_d/generated'
PROTOS = ROOT / 'Content.CMU/Resources/ThreeD/Prototypes/World'
MODEL = PROTOS / 'garrison_console_variants.yml'
ART = PROTOS / 'garrison_console_variants_art.yml'
TEXTURES = ROOT / 'Content.CMU/Resources/Textures/CMU14/ThreeD/Surfaces'
NOTES = ROOT / 'Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_CONSOLE_VARIANTS.md'
REVIEW = GEN / 'review/console-variants'
RSI = '_RMC14/Structures/Machines/computer.rsi'
SOURCE = ROOT / 'Resources/Textures' / RSI
META = json.loads((SOURCE / 'meta.json').read_text())
DESIGNS = {
    'terminal1_old': ('FactionTerminal', ['AU14AllianceConsoleGovfor', 'AU14AmbassadorConsoleUPP',
        'AU14WithdrawConsoleColony', 'AU14WithdrawConsoleGovFor', 'CMUColonyRosterConsole', 'CMUGovforRosterConsole']),
    'security_cam': ('CameraTerminal', ['CMUMonitorCameraColonyCMB', 'CMUMonitorCameraColonyGovfor',
        'CMUMonitorCameraColonyWEYU', 'RMCMonitorCameraAlmayer']),
    'research': ('ResearchTerminal', ['CMUResearchDataTerminal']),
    'engineering_terminal': ('IdTerminal', ['RMCIDComputer']),
    'overwatch': ('WeYaOverwatchTerminal', ['RMCOverwatchConsoleWeYaRotating']),
    'atmos': ('StationAlertTerminal', ['RMCStationAlertComputer']),
    'techweb': ('TelecomMappingConsole', ['RMCTelecomMappingComputer']),
}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def frame(state, direction, index):
    meta = next(s for s in META['states'] if s['name'] == state)
    delays = meta.get('delays', [[1]] * meta.get('directions', 1))
    sheet = Image.open(SOURCE / (state + '.png')).convert('RGBA')
    tile = sum(len(row) for row in delays[:direction]) + index
    x, y = tile % (sheet.width // 32) * 32, tile // (sheet.width // 32) * 32
    return sheet.crop((x, y, x + 32, y + 32)), delays[direction]


def transparent_background(image):
    """Keep hidden RGB in padding (some original art uses transparent white)."""
    result = image.copy()
    result.putdata([(0, 0, 0, 0) if p[3] else p for p in image.get_flattened_data()])
    return result


class Pool:
    def __init__(self):
        self.entries, self.crops, self.by_hash = [], [], {}
        self.used = {e['atlasIndex'] for p in PROTOS.glob('*.yml') if p.name != ART.name
                     for e in (yaml.load(p.read_text(encoding='utf-8-sig'), Loader=yaml.CSafeLoader) or [])
                     if e.get('type') == 'cmu3DSurface'}
        assert not self.used.intersection(range(1800, 2000)), 'Console atlas reservation conflicts'

    def store(self, image, context):
        digest = hashlib.sha256(str(image.size).encode() + image.tobytes()).hexdigest()
        if digest not in self.by_hash:
            index = 1800 + len(self.entries)
            assert index < 2000 and index not in self.used, 'Reserved console surfaces exhausted'
            uid = f'CMU3DConsoleSurface{index}'
            image.save(TEXTURES / (uid + '.png'))
            self.entries.append(dict(type='cmu3DSurface', id=uid, atlasIndex=index,
                texture=f'/Textures/CMU14/ThreeD/Surfaces/{uid}.png'))
            self.by_hash[digest] = uid
        uid = self.by_hash[digest]
        actual = Image.open(TEXTURES / (uid + '.png')).convert('RGBA')
        assert actual.size == image.size and actual.tobytes() == image.tobytes()
        self.crops.append(dict(**context, surface=uid, rgbaSha256=hashlib.sha256(actual.tobytes()).hexdigest()))
        return uid


def compose(state, index, pool, view_direction=0):
    """One physical console uses the corresponding four source views at one clock tick."""
    count = next(s for s in META['states'] if s['name'] == state).get('directions', 1)
    images = [frame(state, d, index)[0] for d in range(count)]
    reconstructed = [transparent_background(image) for image in images]
    parts = []
    main = images[0]
    sample = (18, 2) if state == 'techweb' else (9, 8) if state == 'overwatch' else (9, 2)
    tint = '#' + ''.join(f'{v:02X}' for v in main.getpixel(sample)[:3])

    def box(label, low, high, color=tint, **extra):
        parts.append(dict(label=label, min=list(low), max=list(high), color=color, **extra))

    def crop(direction, rect, label, hole=None, flip=False, rotate=0):
        image = images[direction].crop(rect)
        if hole:
            ImageDraw.Draw(image).rectangle((hole[0]-rect[0], hole[1]-rect[1],
                hole[2]-rect[0]-1, hole[3]-rect[1]-1), fill=(0, 0, 0, 0))
        reconstructed[direction].paste(image, rect[:2])
        if flip:
            image = ImageOps.mirror(image)
        if rotate:
            image = image.rotate(rotate, expand=True)
        return pool.store(image, dict(state=state, frame=index, direction=direction,
            rect=list(rect), hole=list(hole) if hole else None, flip=flip, rotate=rotate, region=label))

    if state != 'techweb':
        wide = state == 'overwatch'
        x0, x1, top, bottom, split = (5, 27, 6, 29, 22) if wide else (7, 25, 0, 23, 16)
        w, h, base = (x1-x0)/64, (bottom-top)/32, (bottom-split)/32
        screen = (9, 12, 23, 19) if wide else (10, 6, 22, 13)
        sx0, sy0, sx1, sy1 = screen
        screen_lo, screen_hi = (sx0-16)/32, (sx1-16)/32
        z0, z1 = (bottom-sy1)/32, (bottom-sy0)/32
        # The case remains solid behind a genuine .042-tile display recess.
        box('monitor rear casting', (-w+.015625, -.025, base), (w-.015625, .29, h-.03125))
        box('stepped crown', (-w+.03125, -.091, h-.03125), (w-.03125, .29, h))
        box('upper monitor bezel', (-w, -.095, z1), (w, .29, h-.03125))
        box('left monitor bezel', (-w, -.095, base), (screen_lo, .29, z1))
        box('right monitor bezel', (screen_hi, -.095, base), (w, .29, z1))
        box('lower monitor bezel', (screen_lo, -.095, base), (screen_hi, .29, z0))
        art = crop(0, (x0, top, x1, split), 'front casing and bezel', screen)
        box('original front bezel', (-w, -.100, base), (w, -.097, h), '#FFFFFF', surface=art, surfaceAxis='XZ')
        art = crop(0, screen, 'recessed display')
        box('recessed display glass', (screen_lo, -.055, z0), (screen_hi, -.052, z1), '#FFFFFF', surface=art, surfaceAxis='XZ')
        if view_direction in (0,1):
            art = crop(0, (x0, split, x1, bottom), 'sloping keyboard and front bevel')
        else:
            direction = view_direction
            left, upper, right, lower = images[direction].getbbox()
            reversed_profile = (direction == 3) != wide
            divider = left+6 if reversed_profile else right-6
            region = (left,upper,divider,lower) if reversed_profile else (divider,upper,right,lower)
            sub = images[direction].crop(region).getbbox()
            rect = (region[0]+sub[0],region[1]+sub[1],region[0]+sub[2],region[1]+sub[3])
            art = crop(direction, rect, 'side-view keyboard on physical sloping deck', rotate=90 if reversed_profile else -90)
        box('source keyboard on sloping deck', (-w, -.34, 0), (w, -.095, base), '#FFFFFF',
            shape='WedgeY', surface=art, surfaceAxis='XY')
        box('keyboard lower foot', (-w+.03125, -.32, 0), (w-.03125, .29, .03125), '#363431')
        box('rear base and hinge', (-w+.015625, -.095, 0), (w-.015625, .29, base), '#4F4A47')
        # North artwork is a rear service cover, never an extra front display.
        rear_rect = images[1].getbbox()
        art = crop(1, rear_rect, 'rear service panel', flip=True)
        box('original rear service cover', (-w, .291, .03125), (w, .294, h), '#FFFFFF', surface=art, surfaceAxis='XZ')
        # The side-view keyboard belongs on the sloping deck, not an upright
        # side billboard. Only the monitor profile remains on the side casing.
        for direction, side in [(2, -1), (3, 1)]:
            image = images[direction]
            bounds = image.getbbox()
            # Overwatch's E/W source profiles point opposite to the ordinary art.
            # Mirror the surface projection only; never change source or entity yaw.
            flip = (direction == 3) != wide
            left, upper, right, lower = bounds
            rect = (left+6,upper,right,lower) if flip else (left,upper,right-6,lower)
            art = crop(direction, rect, 'monitor side casting and edge profile', flip=flip)
            xx = side*(w+.002)
            box(f'original {"left" if side < 0 else "right"} monitor profile',
                (xx-.001, -.095, 0), (xx+.001, .294, h),
                '#FFFFFF', surface=art, surfaceAxis='YZ')
    else:
        # Asymmetric two-height tower: map glass left, status/keyboard stack right.
        def solid(label, rect, near=-.24, far=.25, color=tint):
            a,b,c,d = rect
            box(label, ((a-16)/32, near, (32-d)/32), ((c-16)/32, far, (32-b)/32), color)
        def face(label, rect, depth, hole=None):
            art = crop(0, rect, label, hole)
            solid(label, rect, depth, depth+.003, '#FFFFFF')
            parts[-1].update(surface=art, surfaceAxis='XZ')
        map_rect, status_rect = (4, 7, 15, 24), (23, 9, 30, 17)
        solid('tower rear casting', (1, 7, 32, 32), -.09)
        solid('lower controller cabinet', (1, 24, 32, 32), -.255)
        solid('left map upper lintel', (1, 3, 15, 7))
        solid('left map jamb', (1, 7, 4, 24))
        solid('central equipment column', (15, 0, 23, 24))
        solid('right display top', (23, 0, 32, 9))
        solid('right display jamb', (30, 9, 32, 24))
        solid('right keyboard plinth', (23, 17, 30, 24))
        face('left map housing', (1, 3, 15, 24), -.263, map_rect)
        face('recessed mapping glass', map_rect, -.215)
        face('right controller housing', (15, 0, 32, 24), -.263, status_rect)
        face('recessed status display', status_rect, -.215)
        face('lower original service cabinet', (1, 24, 32, 32), -.266)
    # Every model contains the full pixels of its selected source direction.
    # Companion viewpoints supply physically consistent hidden faces.
    assert reconstructed[view_direction].tobytes() == images[view_direction].tobytes(), (state, view_direction, index)
    return parts


def serialize(value):
    if isinstance(value, list):
        return [serialize(v) for v in value]
    if isinstance(value, dict):
        return {k: ', '.join(f'{n:.9f}' for n in v) if k in ('min', 'max') else serialize(v)
                for k, v in value.items()}
    return value


def write_assets():
    REVIEW.mkdir(parents=True, exist_ok=True)
    pool, models = Pool(), []
    for state, (name, prototypes) in DESIGNS.items():
        meta = next(s for s in META['states'] if s['name'] == state)
        directions = meta.get('directions', 1)
        delays = frame(state, 0, 0)[1]
        ids = ['CMU3D' + name + ('South' if d == 0 else ['South','North','East','West'][d])
               for d in range(directions)] if directions == 4 else ['CMU3D' + name]
        for direction, uid in enumerate(ids):
            compositions = [{'parts': compose(state, index, pool, direction)} for index in range(len(delays))]
            label = {'terminal1_old':'Faction terminal','security_cam':'Camera terminal','research':'Research terminal',
                'engineering_terminal':'ID terminal','overwatch':'Weyland-Yutani overwatch terminal',
                'atmos':'Station alert terminal','techweb':'Telecom mapping console'}[state]
            model = dict(type='cmu3DModel', id=uid, label=label + ' / ' + ['South','North','East','West'][direction],
                status='draft', sourcePrototypes=prototypes if direction == 0 else [],
                referencePrototype=prototypes[0], referenceRsi=RSI, referenceState=state,
                referenceDirection=direction, sourceDirections=directions, sourceSpriteOffset='0,0',
                sourceSpriteRotates=True, useEntityRotation=True, yawOffset=0,
                groundOffset='0,0', placement='floor' if state == 'techweb' else 'surface',
                description='Source-clock console draft. Recessed display, stepped solid casting and keyboard retain original front/rear/side artwork. Hidden depth and projection reconciliation are inferred. Original table support, source pivot and saved yaw are retained. See SOURCES_CONSOLE_VARIANTS.md.',
                parts=copy.deepcopy(compositions[0]['parts']),
                spriteStates={state: dict(frames=copy.deepcopy(compositions), delays=delays)})
            if directions == 4:
                model['directionalModels'] = ids
            models.append(model)
    ART.write_text('# Source-derived console artwork; CC-BY-SA-3.0.\n' + yaml.safe_dump(pool.entries, sort_keys=False), encoding='utf-8')
    MODEL.write_text('# Generated by author_console_variants.py; all models are drafts.\n' +
        yaml.safe_dump(serialize(models), sort_keys=False, width=110), encoding='utf-8')
    surfaces.load_surfaces.cache_clear()
    loaded = build_models.load_models(MODEL)
    by_id = {m['id']:m for m in models}
    for model in loaded:
        expected = by_id[model['id']]
        assert model['spriteStates'][model['referenceState']]['delays'] == expected['spriteStates'][model['referenceState']]['delays']
        for index, actual in enumerate(model['spriteStates'][model['referenceState']]['frames']):
            source_parts = expected['spriteStates'][model['referenceState']]['frames'][index]['parts']
            assert len(actual['parts']) == len(source_parts)
            for part, original in zip(actual['parts'], source_parts):
                for key in ('min','max'):
                    assert all(abs(a-b) < 1e-9 for a,b in zip(part[key],original[key]))
                assert part.get('surface') == original.get('surface') and part.get('color') == original.get('color')
            if index:
                first = model['spriteStates'][model['referenceState']]['frames'][0]['parts']
                assert all({k:v for k,v in p.items() if k != 'surface'} == {k:v for k,v in q.items() if k != 'surface'}
                           for p,q in zip(actual['parts'],first)), 'Animation unexpectedly changes physical bounds'
    crops = pool.crops
    proofs = []
    for state in DESIGNS:
        directions = next(s for s in META['states'] if s['name'] == state).get('directions', 1)
        for direction in range(directions):
            for index in range(len(frame(state, direction, 0)[1])):
                original = frame(state, direction, index)[0]
                actual = transparent_background(original)
                selected = [c for c in crops if c['state'] == state and c['direction'] == direction and c['frame'] == index]
                for c in selected:
                    image = Image.open(TEXTURES / (c['surface'] + '.png')).convert('RGBA')
                    if c['rotate']:
                        image = image.rotate(-c['rotate'],expand=True)
                    if c['flip']:
                        image = ImageOps.mirror(image)
                    actual.paste(image, tuple(c['rect'][:2]))
                assert original.tobytes() == actual.tobytes()
                source_dir = REVIEW / 'source-frames'
                source_dir.mkdir(exist_ok=True)
                source_path = source_dir / f'{state}-dir{direction}-frame{index}.png'
                original.save(source_path)
                assert Image.open(source_path).convert('RGBA').tobytes() == original.tobytes()
                proofs.append(dict(state=state, direction=direction, frame=index,
                    writtenRgbaReassembled=True, transparentPaddingRgbPreservedInSourceReference=True,
                    sourceReference=source_path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(actual.tobytes()).hexdigest(),
                    sourceOpaquePixels=sum(p[3] > 0 for p in original.get_flattened_data())))
    assert len(proofs) == 202
    proof = dict(schemaVersion=1, status='authored drafts; global export and native validation pending parent',
        models=[dict(id=m['id'], parts=len(m['parts']), frames=len(m['spriteStates'][m['referenceState']]['frames']),
                     sourcePrototypes=m['sourcePrototypes']) for m in loaded],
        writtenFrames=proofs, sourceCrops=crops, textureCount=len(pool.entries),
        atlasIndices=[e['atlasIndex'] for e in pool.entries], sourceSpriteRotates=True,
        sourceNoRot=False, sourceSnapCardinals=False, sourceSpriteOffset=[0,0],
        sourceReferences={p.relative_to(ROOT).as_posix():sha(p) for p in [SOURCE/'meta.json', *[SOURCE/(s+'.png') for s in DESIGNS]]})
    (GEN / 'console-variants-proof.json').write_text(json.dumps(proof, indent=2)+'\n')
    print(json.dumps(dict(models=len(loaded), textures=len(pool.entries), frames=len(proofs),
        parts=Counter(len(m['parts']) for m in loaded))), flush=True)
    return loaded, proof


def cards(models):
    font, small = ImageFont.load_default(size=18), ImageFont.load_default(size=14)
    for model in models:
        card = Image.new('RGB', (1340, 425), '#18232C')
        draw = ImageDraw.Draw(card)
        draw.text((14, 10), model['label'] + ' / original source and four solid views', fill='white', font=font)
        draw.text((14, 37), 'Exact source pixels; physical depth, hidden faces and source-view reconciliation inferred. Draft.', fill='#C2D2DC', font=small)
        src = frame(model['referenceState'], model['referenceDirection'], 0)[0].resize((230,230), Image.Resampling.NEAREST)
        card.paste(src, (8,95), src)
        for index, yaw in enumerate([-math.pi/2, -math.pi/3, math.pi/3, math.pi]):
            panel = build_models.render_model(model, (270,310), yaw, .4, pixels_per_unit=255, screen_origin=(135,260))
            card.paste(panel, (250+index*272,75))
        draw.text((14,397), 'Source clock: '+str(model['spriteStates'][model['referenceState']]['delays']), fill='#C2D2DC', font=small)
        card.save(REVIEW / (model['id']+'.png'))


def contexts(models, proof):
    library = {m['id']:m for m in json.loads((GEN/'models.json').read_text())['models']}
    library.update({m['id']:m for m in models})
    mappings = {p:m for m in models for p in m['sourcePrototypes']}
    records, counts, summaries = [], Counter(), []
    font = ImageFont.load_default(size=16)
    for spec in json.loads((GEN/'fast-batch-scenes.json').read_text()):
        path = GEN/spec['file']
        doc = json.loads(path.read_text())
        targets = [i for i in doc['instances'] if i['prototype'] in mappings]
        for entity in targets:
            model = mappings[entity['prototype']]
            direction = [0,2,1,3][round(entity['yaw']/(math.pi/2))%4] if model['sourceDirections'] == 4 else 0
            uid = model.get('directionalModels', [model['id']])[direction]
            entity.update(modelId=uid, matchKind='exact', renderYaw=entity['yaw'])
        placement.resolve_placements(doc['instances'], list(library.values()), doc.get('geometryVariants', {}))
        indexed = {i['id']:i for i in doc['instances']}
        for entity in targets:
            model = library[entity['modelId']]
            own = world_parts(model['parts'], entity['position'], entity['renderYaw'], entity.get('renderOffset',[0,0,0]))
            near = [i for i in doc['instances'] if i['id'] != entity['id'] and
                sum((a-b)**2 for a,b in zip(i['position'][:2],entity['position'][:2])) <= 1.6**2]
            neighbors, unknown, assembled = [], [], list(own)
            for n in near:
                nm = library.get(n.get('modelId'))
                if nm is None:
                    unknown.append(dict(id=n['id'], prototype=n['prototype']))
                    continue
                other = world_parts(doc.get('geometryVariants',{}).get(n.get('geometryKey'),nm['parts']),
                    n['position'], n.get('renderYaw',n['yaw']), n.get('renderOffset',[0,0,0]))
                hits, gap = contacts(own,other)
                neighbors.append(dict(id=n['id'], prototype=n['prototype'], contacts=hits, minimumPartAabbSeparation=gap))
                assembled.extend(other)
            record = dict(variant=spec['variant'], level=spec['level'], id=entity['id'], prototype=entity['prototype'],
                modelId=model['id'], savedPosition=entity['position'], savedYaw=entity['yaw'], renderYaw=entity['renderYaw'],
                renderOffset=entity.get('renderOffset',[0,0,0]), support=entity.get('support'),
                modeledNeighbors=neighbors, unknownNeighbors=unknown,
                contactPairs=sum(len(n['contacts']) for n in neighbors))
            records.append(record)
            counts[(spec['variant'],spec['level'])] += 1
            for part in assembled:
                for key in ('min','max'):
                    part[key][0] -= entity['position'][0]
                    part[key][1] -= entity['position'][1]
            card = Image.new('RGB',(1200,560),'#18232C')
            draw = ImageDraw.Draw(card)
            draw.text((12,10),f'Redux {spec["level"]:+d} / UID {entity["id"]} / {entity["prototype"]}',fill='white',font=font)
            draw.text((12,36),f'Saved yaw {math.degrees(entity["yaw"]):.0f}° / support {entity.get("support")} / original pivots retained',fill='#C2D2DC',font=font)
            for col,yaw in enumerate([-math.pi/2+.25,math.pi/2+.25]):
                panel=build_models.render_model(dict(parts=assembled),(590,440),yaw,.55,pixels_per_unit=150,screen_origin=(295,355))
                card.paste(panel,(col*600,75))
            draw.text((12,531),f'{record["contactPairs"]} conservative part contacts; {len(unknown)} unknown neighbors. These are not blanket clearance claims.',fill='#C2D2DC',font=font)
            card.save(REVIEW/f'context-{spec["level"]}-{entity["id"]}.png')
        summaries.append(dict(file=spec['file'],sha256=sha(path),exactConsolePlacements=len(targets)))
    assert len(records) == 43 and sum(bool(r['support']) for r in records)==42
    proof.update(contexts=records,sceneInputs=summaries,placementCount=43,
        placementCounts=[dict(variant=k[0],level=k[1],count=v) for k,v in sorted(counts.items())],
        contextLimitations=['AABB contacts include thin source-textured surfaces and conservative wedge bounds.',
            'All visible animation frames retain identical solid bounds; frame color does not alter the contacts.',
            'Unknown or fallback neighbors remain explicitly unverified.',
            'Source sprites overlap at some fractional saved desk positions. No per-UID shrink or source movement is applied.'])
    (GEN/'console-variants-proof.json').write_text(json.dumps(proof,indent=2)+'\n')
    print(json.dumps(dict(placements=len(records),supported=42,contactPairs=sum(r['contactPairs'] for r in records))),flush=True)


def source_contracts(models, proof):
    import inventory
    import scene
    import sprite_states
    index, issues, _ = inventory.load_prototypes(ROOT)
    resolver = inventory.Resolver(index['entity'])
    defaults = {p:inventory.component_map(resolver.resolve(p)) for _,protos in DESIGNS.values() for p in protos}
    by_id = {m['id']:m for m in models}
    topology = []
    raw = yaml.safe_load(MODEL.read_text())
    def vectors_are_scalar(value):
        if isinstance(value,list):
            return all(vectors_are_scalar(v) for v in value)
        if isinstance(value,dict):
            return all(isinstance(v,str) if k in ('min','max') else vectors_are_scalar(v) for k,v in value.items())
        return True
    assert vectors_are_scalar(raw)
    for model in models:
        state = model['referenceState']
        first = model['spriteStates'][state]['frames'][0]['parts']
        for frame_index, definition in enumerate(model['spriteStates'][state]['frames']):
            parts = definition['parts']
            assert len(first) == len(parts)
            assert all({k:v for k,v in a.items() if k!='surface'} == {k:v for k,v in b.items() if k!='surface'}
                       for a,b in zip(parts,first))
            used_surfaces = {p.get('surface') for p in parts}
            required = {c['surface'] for c in proof['sourceCrops'] if c['state']==state and
                c['direction']==model['referenceDirection'] and c['frame']==frame_index}
            assert required <= used_surfaces, (model['id'],frame_index,required-used_surfaces)
            topology.append(dict(modelId=model['id'],state=state,direction=model['referenceDirection'],frame=frame_index,
                parts=len(parts),unchangedSolidsAndTopology=True,allSelectedSourceCropSurfacesUsed=True))
    assert len(topology)==202
    proof['writtenAnimationTopologyChecks']=topology
    proof['nativeScalarVectorsVerified']=True
    by_map = {}
    for spec in json.loads((GEN/'fast-batch-scenes.json').read_text()):
        if any(r['variant']==spec['variant'] and r['level']==spec['level'] for r in proof['contexts']):
            doc = json.loads((GEN/spec['file']).read_text())
            path = ROOT/doc['map']['path']
            by_map[(spec['variant'],spec['level'])] = (path,scene.read_map(path)[1])
    contracts = []
    for record in proof['contexts']:
        path, records = by_map[(record['variant'],record['level'])]
        saved = records[record['id']]['components']
        model = by_id[record['modelId']]
        state, reason = sprite_states.saved_pose(model,defaults[record['prototype']],saved,scene.normalize_tint)
        assert state == model['referenceState'] and reason is None, (record['id'],reason)
        assert abs(record['savedYaw']/(math.pi/2)-round(record['savedYaw']/(math.pi/2))) < 1e-6
        contracts.append(dict(variant=record['variant'],level=record['level'],id=record['id'],
            prototype=record['prototype'],map=path.relative_to(ROOT).as_posix(),state=state,
            direction=model['referenceDirection'],spriteOverride=saved.get('Sprite'),
            appearanceOverride=saved.get('Appearance'),sourceContractAccepted=True,
            savedTransform=saved.get('Transform')))
    proof['savedSourceContracts'] = contracts
    proof['prototypeSourceContracts'] = [dict(prototype=p,sprite=c['Sprite'],
        relevantOwners=[k for k in c if k in ('Appearance','GenericVisualizer','RMCCameraComputer','OverwatchConsole',
            'ResearchDataTerminal','IdModificationConsole','AllianceConsole','AmbassadorConsole','WithdrawConsole',
            'FactionRosterConsole','RMCPowerReceiver','ApcPowerReceiver')]) for p,c in defaults.items()]
    groups = Counter()
    pairs = []
    for record in proof['contexts']:
        for neighbor in record['modeledNeighbors']:
            hits = neighbor['contacts']
            if not hits:
                continue
            if all(h['neighborPart'] in ('wood grain','side border rivet') for h in hits):
                category = 'table decorative relief: 0.003 wood grain or 0.007 border rivet penetration'
            elif 'Shutter' in neighbor['prototype']:
                category = 'closed shutter: genuine co-centered solid conflict remains'
            elif any(term in neighbor['prototype'] for term in ('Console','Computer','Monitor')):
                category = 'closely spaced original desk machines: solid overlap remains'
            else:
                category = 'desk clutter contact: alpha/shape-specific visibility not fully classified'
            groups[category] += 1
            pairs.append(dict(level=record['level'],id=record['id'],neighborId=neighbor['id'],
                category=category,maximumOverlap=[max(h['overlap'][i] for h in hits) for i in range(3)],
                partPairs=len(hits)))
    proof['contactClassification'] = dict(directedPairCounts=dict(groups),pairs=pairs,
        limitation='Directed pairs include a pair twice when both endpoints are new consoles. Thin textured parts and wedges retain conservative AABB false positives; thick casting/shutter overlaps and table-foot intersections are real.')
    asset_paths = [MODEL,ART,NOTES,*[TEXTURES/(e['id']+'.png') for e in yaml.safe_load(ART.read_text())]]
    proof['writtenAssetSha256'] = {p.relative_to(ROOT).as_posix():sha(p) for p in asset_paths}
    proof['generatorSha256'] = sha(Path(__file__))
    (GEN/'console-variants-proof.json').write_text(json.dumps(proof,indent=2)+'\n')
    print(json.dumps(dict(acceptedSourceContracts=len(contracts),contactCategories=dict(groups))),flush=True)


def notes():
    NOTES.write_text('''# Console variant drafts

Seven source designs produce 25 directional models and 15 exact prototype mappings. All 43 saved placements are in Stable Garrison Redux: 20 on level 0, 12 on level -2 and 11 on level +1. No classic placement is added. The four directional records are reciprocal source poses, not extra entity mappings. Identical source bodies are shared only across the six terminal1_old prototypes and four security_cam prototypes.

Source: `Resources/Textures/_RMC14/Structures/Machines/computer.rsi`, 32 x 32 pixels. States and original clocks: terminal1_old (four directions, 12 frames: .1 eight times, 1, .1, .1, 1); security_cam (four directions, 20 frames: .5, .1, .1, .1, .1 repeated four times); research (four directions, one static frame); engineering_terminal (four directions, five .1 frames); overwatch (four directions, eight .2 frames); atmos (four directions, four .1 frames); techweb (one direction, two .1 frames). There are 202 exact source frames. Written PNG crops reconstruct every selected source frame, including alpha; hidden RGB in transparent outer padding is retained in the full source-reference PNGs. The written resource vectors are scalar strings, and all written frame geometry and delays are validated.

All sources use noRot=false, snapCardinals=false, offset (0,0), unit scale, ordinary single-layer rendering and default white tint. Most explicitly declare an empty layers list with a top-level state; the engine initializes that state as one layer. Overwatch's overrideDir=South is disabled. Each model opts into sourceSpriteRotates, uses the real layer's frame clock and rotates once with the saved entity yaw. All current saved yaws are cardinal, including fractional XY pivots. Unsupported source layers, effects, appearance changes or states must retain the original sprite through the adapter; this asset work does not invent a gameplay state controller.

The desk designs use 14 solids: stepped CRT casting, physically recessed display, original service rear, sloping keyboard and foot, plus two original side-casing profiles. South artwork supplies the front; North is the rear service panel. The selected East/West keyboard is rotated onto the physical sloping deck, while only the monitor profile is mapped onto the side casing. This avoids upright keyboard billboards. Ordinary bodies are .5625 tile wide (.5685 including thin side artwork), .634 deep and .71875 high. Overwatch is .6875 wide (.6935 including side artwork) and .71875 high. Depth, keyboard slope, rear construction and reconciling differing source-view padding are inferred. Overwatch East/West profile artwork points opposite the ordinary family; the casing projection is mirrored locally without reversing entity yaw. This historical 2D-view inconsistency remains a fidelity limitation, especially for future unsaved side orientations. Source reconstruction verifies pixels, not an exact arbitrary-camera image or approved fidelity.

The 13-part techweb model is an asymmetric two-height cabinet, with a recessed tall map display, separate recessed status display and lower controller. Its footprint is X [-.46875,.5], Y [-.266,.25], height 1 tile. All hidden depth and faces are inferred. No invented OFF state is supplied. Its prototype has a power GenericVisualizer mapping, but its explicit techweb layer has no Powered map key; the existing source owner remains authoritative. The other consoles retain their existing camera, overwatch, roster, research, ID and faction UI owners. Their RSI display animation is presentation, not a new gameplay simulation.

All 42 desk placements resolve an existing authored table at height .86 with the existing .002 support separation. The tower is floor placed. Original transforms and physics are untouched. Source width is deliberately not shrunk to hide saved overlaps. Current contextual limitations are measured in console-variants-proof.json: close pairs of desk consoles intersect, three console placements intersect closed shutters, and some desk feet touch existing raised table decoration (.003 tile wood grain or .007 tile rivets). Desk clutter also has unresolved contacts. Conservative AABB reports include thin texture alpha and wedge false positives, but thick case/shutter and case/case intersections are genuine. These are follow-up layout/context issues, not cleared by this batch. All animation poses have unchanged solid bounds, so contact evidence covers the full source clock.

Review: `Tools/three_d/generated/review/console-variants/` contains 25 source/four-view cards, 43 saved-context cards and all 202 full original source frames. `Tools/three_d/generated/console-variants-proof.json` records the exact sources, selected frames, saved appearance contracts, geometry, support and contacts. Context uses the eight fast-batch snapshots plus this complete console batch, applying each model-local variant, renderYaw and offset once. Unknown neighboring sprites are reported explicitly. Parent integration owns global exports, native admission and running-game verification; those are not claimed by this focused asset generator.

Artwork license: CC-BY-SA-3.0. Original RSI attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/d5b119380250ea512db2a5319e36592c7f604250/icons/obj/structures/machinery/computer.dmi, edits to overwatch and register by github noctyrnal. Derived crops retain original RGBA texels. Preserve this note and the original RSI attribution with redistributed assets.

Reproduce dedicated asset files, source proof and review cards with `Tools/three_d/author_console_variants.py`. `--reviews-only` leaves production model/art/texture files unchanged while rebuilding focused context and source-contract evidence. The generator does not run the game, a server, shared builds or global exports.
''',encoding='utf-8')


def export_parity():
    """Read-only asset/scene comparison after the parent publishes shared exports."""
    proof_path = GEN/'console-variants-proof.json'
    proof = json.loads(proof_path.read_text())
    models = build_models.load_models(MODEL)
    library = {m['id']:m for m in json.loads((GEN/'models.json').read_text())['models']}
    plain = lambda value: json.loads(json.dumps(value))
    frame_checks = []
    for model in models:
        exported = library[model['id']]
        assert plain(model['parts']) == exported['parts']
        for key in ('referenceState','referenceDirection','sourceDirections','sourceSpriteRotates','placement','useEntityRotation'):
            assert model[key] == exported[key], (model['id'],key)
        assert list(model['groundOffset']) == exported['groundOffset']
        for state,data in model['spriteStates'].items():
            assert data['delays'] == exported['spriteStates'][state]['delays']
            for index,composition in enumerate(data['frames']):
                assert plain(composition['parts']) == exported['spriteStates'][state]['frames'][index]['parts']
                frame_checks.append(dict(modelId=model['id'],state=state,frame=index,exactWrittenPartsAndDelays=True))
    assert len(frame_checks)==202
    accepted = []
    inputs = {str((GEN/'models.json').relative_to(ROOT)):sha(GEN/'models.json'),
              str((GEN/'surfaces.json').relative_to(ROOT)):sha(GEN/'surfaces.json')}
    for spec in json.loads((GEN/'source-batch-scenes.json').read_text()):
        path = GEN/spec['file']
        inputs[path.relative_to(ROOT).as_posix()] = sha(path)
        doc = json.loads(path.read_text())
        indexed = {i['id']:i for i in doc['instances']}
        targets = [r for r in proof['contexts'] if r['variant']==spec['variant'] and r['level']==spec['level']]
        for raw in targets:
            actual = indexed[raw['id']]
            model = library[raw['modelId']]
            assert actual['position'] == raw['savedPosition'] and actual['yaw'] == raw['savedYaw']
            assert actual['modelId'] == raw['modelId'] and actual['renderYaw'] == raw['renderYaw']
            assert actual.get('renderOffset',[0,0,0]) == raw['renderOffset']
            assert actual.get('support') == raw['support']
            assert actual['matchKind']=='exact' and actual['spriteState']==model['referenceState'] and actual['spriteFrame']==0
            assert actual.get('sourceDirection',0)==model['referenceDirection']
            parts = doc['geometryVariants'][actual['geometryKey']]
            assert parts == model['spriteStates'][actual['spriteState']]['frames'][0]['parts']
            accepted.append(dict(variant=spec['variant'],level=spec['level'],id=raw['id'],
                modelId=actual['modelId'],sourceDirection=model['referenceDirection'],
                unchangedSourceTransform=True,unchangedAuthoredSupport=True,unchangedRenderYaw=True,
                exactModelLocalFrameZero=True,frame=0))
    assert len(accepted)==43
    for path,digest in inputs.items():
        assert sha(ROOT/path)==digest
    severity = Counter()
    unique = set()
    for pair in proof['contactClassification']['pairs']:
        category = pair['category']
        pair['severity'] = ('minor decorative relief' if category.startswith('table') else
            'high priority visible solid fit' if category.startswith(('closed','closely')) else
            'unresolved clutter / alpha assessment needed')
        identity=(pair['level'],*sorted((pair['id'],pair['neighborId'])))
        if identity not in unique:
            unique.add(identity)
            severity[pair['severity']]+=1
    proof['contactClassification']['uniqueNeighborPairSeverityCounts']=dict(severity)
    proof['contactClassification']['geometryScope']='Visual drafts only; gameplay fixtures and saved transforms unchanged.'
    proof['finalExportParity']=dict(status='passed',exportedModels=25,exportedPoseChecks=frame_checks,
        savedDefaultChecks=accepted,inputSha256=inputs,
        note='43 exact saved instances; 42 original authored table supports at .862 and one floor cabinet. No geometry or source transform edits during export audit.')
    proof['status']='authored draft assets; source and final export parity passed; native/live validation owned by parent'
    proof['generatorSha256']=sha(Path(__file__))
    proof_path.write_text(json.dumps(proof,indent=2)+'\n')
    print(json.dumps(dict(exportedModels=25,exactPoseChecks=len(frame_checks),savedInstances=len(accepted),
        uniqueNeighborPairs=len(unique),severity=dict(severity))),flush=True)


def regeneration_check():
    """Recreate dedicated files in an isolated directory; never write live assets."""
    global MODEL, ART, TEXTURES, GEN, REVIEW
    originals = dict(model=MODEL,art=ART,textures=TEXTURES,generated=GEN,review=REVIEW)
    destination = ROOT/'.codex/console-regeneration-check'
    assert destination.resolve().is_relative_to((ROOT/'.codex').resolve())
    MODEL = destination/originals['model'].relative_to(ROOT)
    ART = destination/originals['art'].relative_to(ROOT)
    TEXTURES = destination/originals['textures'].relative_to(ROOT)
    GEN = destination/'generated'
    REVIEW = GEN/'review'
    for folder in (MODEL.parent,TEXTURES,GEN,REVIEW):
        folder.mkdir(parents=True,exist_ok=True)
    write_assets()
    paths = [(MODEL,originals['model']),(ART,originals['art'])]
    paths.extend((p,originals['textures']/p.name) for p in TEXTURES.glob('CMU3DConsoleSurface*.png'))
    assert len(paths)==103
    for candidate, original in paths:
        assert candidate.read_bytes()==original.read_bytes(), original
    proof_path = originals['generated']/'console-variants-proof.json'
    proof = json.loads(proof_path.read_text())
    proof['deterministicRegeneration']=dict(status='passed',byteIdenticalFiles=len(paths),
        isolatedOutput=destination.relative_to(ROOT).as_posix(),productionFilesWritten=False,
        assetHashes={b.relative_to(ROOT).as_posix():sha(b) for _,b in paths})
    proof['generatorSha256']=sha(Path(__file__))
    proof_path.write_text(json.dumps(proof,indent=2)+'\n')
    print(json.dumps(dict(deterministicAssetFiles=len(paths),productionFilesWritten=False)),flush=True)


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--reviews-only',action='store_true')
    parser.add_argument('--export-parity',action='store_true')
    parser.add_argument('--check-assets',action='store_true')
    args=parser.parse_args()
    if args.check_assets:
        regeneration_check()
        return
    if args.export_parity:
        export_parity()
        return
    if args.reviews_only:
        models=build_models.load_models(MODEL)
        proof=json.loads((GEN/'console-variants-proof.json').read_text())
    else:
        models,proof=write_assets()
    notes()
    cards(models)
    contexts(models,proof)
    source_contracts(models,proof)


if __name__ == '__main__':
    main()
