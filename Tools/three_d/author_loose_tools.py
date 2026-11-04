"""Seven source-specific kitchen and garden tool drafts; dedicated assets only."""
import argparse
import copy
import hashlib
import json
import math
from collections import Counter, defaultdict
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont
import yaml

import build_models as bm
import inventory
import layout
import placement
import scene
import sprite_states
import surfaces
from author_loose_headgear import part, serialize
from author_vendor_fans import contact_witnesses, occupied
from author_wide_machinery import world_parts, contacts, box_bounds

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT/'.codex/model-batch-baseline1015'
GEN = ROOT/'Tools/three_d/generated'
PROTOS = ROOT/'Content.CMU/Resources/ThreeD/Prototypes/World'
MODEL = PROTOS/'garrison_loose_tools.yml'
ART = PROTOS/'garrison_loose_tools_art.yml'
TEXTURES = ROOT/'Content.CMU/Resources/Textures/CMU14/ThreeD/Surfaces/LooseTools'
NOTE = ROOT/'Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_LOOSE_TOOLS.md'
REVIEW = GEN/'review/loose-tools'
SPECS = [
    ('RMCSpoon', 'CMU3DLooseSpoon', 'Metal spoon', '_RMC14/Objects/Tools/Kitchen/spoon.rsi', 'icon'),
    ('CMWrench', 'CMU3DLooseWrench', 'Open jaw wrench', '_RMC14/Objects/Tools/wrench.rsi', 'icon'),
    ('RMCToolHatchet', 'CMU3DLooseHatchet', 'Gardening hatchet', '_RMC14/Objects/Tools/Hydroponics/hatchet.rsi', 'icon'),
    ('RMCToolMiniHoe', 'CMU3DLooseMiniHoe', 'Mini hoe', '_RMC14/Objects/Tools/Hydroponics/mini_hoe.rsi', 'icon'),
    ('RMCToolSpade', 'CMU3DLooseSpade', 'Hand spade', '_RMC14/Objects/Tools/Hydroponics/spade.rsi', 'icon'),
    ('RMCKitchenKnife', 'CMU3DLooseKitchenKnife', 'Kitchen and chef knife', '_RMC14/Objects/Weapons/Melee/Kitchen/knife.rsi', 'icon'),
    ('RMCKitchenKnifePlastic', 'CMU3DLoosePlasticKnife', 'Plastic kitchen knife', '_RMC14/Objects/Weapons/Melee/Kitchen/knife.rsi', 'plastic'),
]
IDS = [s[0] for s in SPECS] + ['RMCKitchenKnifeChef']



def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def source(spec):
    return Image.open(ROOT/'Resources/Textures'/spec[3]/(spec[4]+'.png')).convert('RGBA')


def regions(spec):
    """Exact source footprint with inferred physical section thickness and recesses."""
    pid = spec[0]
    rgba = np.array(source(spec)); alpha = rgba[:, :, 3] > 0
    y, x = np.mgrid[0:32, 0:32]
    if pid == 'RMCSpoon':
        labels = np.where(y <= 13, 1, np.where(y <= 18, 3, 4))
        labels[(y >= 7) & (y <= 11) & (x >= 14) & (x <= 16)] = 2
        sections = {1: ('raised bowl rim', .019, .075, '#898080'),
                    2: ('recessed spoon bowl', .019, .035, '#766D6A'),
                    3: ('narrow continuous neck', .015, .048, '#766D6A'),
                    4: ('thick riveted grip', 0, .080, '#393939')}
    elif pid == 'CMWrench':
        labels = np.where(x <= 9, 1, np.where(x <= 21, 2, 3))
        sections = {1: ('open jaw and wrench head', .006, .082, '#525252'),
                    2: ('continuous steel shank', .006, .062, '#969696'),
                    3: ('thicker butt grip', 0, .075, '#525252')}
    elif pid == 'RMCToolHatchet':
        labels = np.where((x-y <= 0) & (y <= 17), 1, 2)
        # A thinner exposed cutting side joins the heavier poll and shaft.
        labels[(labels == 1) & (x <= 8)] = 3
        sections = {1: ('hatchet head and poll', .014, .098, '#5B5555'),
                    2: ('continuous angled shaft and grip', 0, .078, '#767672'),
                    3: ('thin curved cutting edge', .035, .060, '#A6A6A0')}
    elif pid == 'RMCToolMiniHoe':
        labels = np.where(y < 16, 1, 3)
        # The source's dark pixels between ridges are opaque outlines, not
        # transparent holes. Keep them as a lower web; do not erase source art.
        labels[(y < 16) & (rgba[:, :, :3].max(axis=2) >= 68)] = 2
        sections = {1: ('lower tine web and neck', .014, .052, '#202020'),
                    2: ('raised source metal tine ridges', .014, .094, '#443F3F'),
                    3: ('thicker angled handle', 0, .083, '#766D6A')}
    elif pid == 'RMCToolSpade':
        labels = np.where(y < 13, 1, 3)
        labels[(y < 13) & (x >= 8) & (x <= 10) & (y >= 7)] = 2
        sections = {1: ('curved spade blade outline', .022, .078, '#5B5555'),
                    2: ('shallow recessed blade channel', .022, .052, '#484343'),
                    3: ('continuous wooden handle', 0, .085, '#61584C')}
    else:
        labels = np.where(y < 20, 1, 3)
        for row in range(20):
            points = np.flatnonzero(alpha[row])
            if len(points): labels[row, points[0]] = 2
        if spec[4] == 'plastic':
            sections = {1: ('plastic blade spine', .012, .045, '#B4B4AE'),
                        2: ('thin source curved cutting edge', .020, .032, '#D4D4CE'),
                        3: ('ribbed plastic handle', 0, .076, '#91918F')}
        else:
            sections = {1: ('steel blade spine and tang', 0, .057, '#766D6A'),
                        2: ('thin source curved cutting edge', .026, .040, '#D5CCC3'),
                        3: ('riveted dark knife handle', 0, .085, '#393939')}
    labels[~alpha] = 0
    # Side tones are exact source palette entries, including their dark outlines.
    palette = np.unique(rgba[alpha, :3], axis=0)
    for key, (label, low, high, color) in sections.items():
        wanted = np.array(tuple(bytes.fromhex(color[1:])))
        exact = palette[np.argmin(np.sum((palette.astype(int)-wanted)**2, axis=1))]
        sections[key] = label, low, high, '#' + ''.join(f'{v:02X}' for v in exact)
    return labels, sections


def box(label, rect, low, high, color, surface=None):
    x0, y0, x1, y1 = rect
    result = dict(label=label, min=[(x0-16)/32, (16-y1)/32, low],
                  max=[(x1-16)/32, (16-y0)/32, high], color=color)
    if surface: result.update(surface=surface, surfaceAxis='XY')
    return result


def author():
    from author_cash_cutlery import rectangles
    TEXTURES.mkdir(parents=True, exist_ok=True); REVIEW.mkdir(parents=True, exist_ok=True)
    used = {r['atlasIndex'] for p in PROTOS.glob('*.yml') if p.name != ART.name
            for r in yaml.load(p.read_text(encoding='utf-8-sig'), Loader=yaml.CSafeLoader) or [] if r.get('type') == 'cmu3DSurface'}
    assert not used.intersection(range(3500, 3600))
    models, art, entries = [], [], []
    for spec in SPECS:
        proto, mid, label, rsi, state = spec
        folder = ROOT/'Resources/Textures'/rsi
        meta = json.loads((folder/'meta.json').read_text())
        state_meta = next(s for s in meta['states'] if s['name'] == state)
        assert state_meta.get('directions', 1) == 1 and state_meta.get('delays', [[1]]) == [[1]]
        im = source(spec); rgba = np.array(im); labels, sections = regions(spec)
        parts, patches = [], []
        for key, (name, low, high, color) in sections.items():
            mask = labels == key
            assert mask.any()
            for i, (rect, _) in enumerate(rectangles(mask.astype(int))):
                parts.append(box(name + ' solid ' + str(i+1), rect, low, high, color))
            ys, xs = np.where(mask)
            rect = (int(xs.min()), int(ys.min()), int(xs.max()+1), int(ys.max()+1))
            pixels = rgba.copy(); pixels[~mask] = 0
            patch = Image.fromarray(pixels).crop(rect)
            index = 3500 + len(art); assert index < 3600
            uid = 'CMU3DLooseToolsSurface' + str(index)
            patch.save(TEXTURES/(uid+'.png'))
            art.append(dict(type='cmu3DSurface', id=uid, atlasIndex=index,
                texture='/Textures/CMU14/ThreeD/Surfaces/LooseTools/'+uid+'.png'))
            parts.append(box(name + ' original surface', rect, high-.001, high, '#FFFFFF', uid))
            patches.append(dict(surface=uid, rect=list(rect), section=key, height=high,
                                rgbaSha256=hashlib.sha256(patch.tobytes()).hexdigest()))
        if proto == 'RMCSpoon':
            # The underside is actually curved. It remains wholly inside the
            # source bowl footprint and joins the rim and recessed interior.
            parts.append(part('rounded underside of spoon bowl', (-.015625, .203125, .0175),
                              (.078, .113, .0175), '#766D6A', 'Ellipsoid'))
        sources = [proto, 'RMCKitchenKnifeChef'] if proto == 'RMCKitchenKnife' else [proto]
        record = dict(type='cmu3DModel', id=mid, label=label, status='draft', sourcePrototypes=sources, referencePrototype=proto,
                      referenceRsi=rsi, referenceState=state, sourceDirections=1, sourceSpriteRotates=True,
                      sourceSpriteOffset='0,0', useEntityRotation=True, yawOffset=0, groundOffset='0,0', placement='surface', parts=parts,
                      spriteStates={state: dict(frames=[dict(parts=copy.deepcopy(parts))], delays=[1])},
                      description='Loose world tool only. Separate source-shaped solid head, shaft and grip with original top RGBA, real open jaw/gaps, stepped blade bevels and recessed spoon/spade interiors. Heights and hidden depth are inferred; original pivots, gameplay and held/equipped sprites are unchanged. Unsupported appearances retain sprite fallback. All drafts; see SOURCES_LOOSE_TOOLS.md.')
        if proto == 'RMCToolHatchet':
            record['supportProbePart'] = 'continuous angled shaft and grip solid 11'
        models.append(record)
        entries.append(dict(prototypes=sources, prototype=proto, modelId=mid, parts=len(parts), referenceRsi=rsi, state=state,
            sourceFrames=1, sourceDirections=1, sourceBounds=list(im.getbbox()), opaquePixels=int((rgba[:, :, 3]>0).sum()),
            sourceSha256=sha(folder/(state+'.png')), sourceMetaSha256=sha(folder/'meta.json'), license=meta['license'],
            copyright=meta['copyright'], patches=patches, inferredSections={k:list(v) for k,v in sections.items()}))
    MODEL.write_text('# Loose source-specific tools. All models remain drafts.\n'+yaml.safe_dump(serialize(models), sort_keys=False, width=110), encoding='utf-8')
    ART.write_text('# Original source crops; preserve source attribution in SOURCES_LOOSE_TOOLS.md.\n'+yaml.safe_dump(art, sort_keys=False), encoding='utf-8')
    surfaces.load_surfaces.cache_clear()
    loaded = bm.load_models(MODEL)
    proof = dict(status='seven draft assets written; parent export pending', models=entries, atlasIndices=[e['atlasIndex'] for e in art])
    print(json.dumps(dict(models=len(models), parts=[len(m['parts']) for m in models], textures=len(art))), flush=True)
    return loaded, proof


def written_proof(models, proof):
    raw = yaml.safe_load(MODEL.read_text())
    assert all(isinstance(p[k], str) for m in raw for p in m['parts'] for k in ('min', 'max'))
    library = {m['id']:m for m in models}
    for spec, entry in zip(SPECS, proof['models']):
        m = library[entry['modelId']]; im = source(spec); rgba = np.array(im); alpha = rgba[:,:,3]>0
        labels, sections = regions(spec)
        assert m['parts'] == m['spriteStates'][spec[4]]['frames'][0]['parts']
        reconstructed = Image.new('RGBA', im.size)
        physical = np.zeros((32,32), bool); heights = np.zeros((32,32))
        solids = [p for p in m['parts'] if not p.get('surface') and p.get('shape','Box') == 'Box']
        for p in solids:
            x0,x1 = round(p['min'][0]*32+16),round(p['max'][0]*32+16)
            y0,y1 = round(16-p['max'][1]*32),round(16-p['min'][1]*32)
            physical[y0:y1,x0:x1] = True
            heights[y0:y1,x0:x1] = np.maximum(heights[y0:y1,x0:x1],p['max'][2])
        assert np.array_equal(physical, alpha)
        expected_heights = np.zeros((32,32))
        for key, (_, _, high, _) in sections.items(): expected_heights[labels == key] = high
        assert np.allclose(heights, expected_heights)
        for patch in entry['patches']:
            png = Image.open(TEXTURES/(patch['surface']+'.png')).convert('RGBA')
            assert hashlib.sha256(png.tobytes()).hexdigest() == patch['rgbaSha256']
            x0,y0,x1,y1=patch['rect'];wanted=rgba[y0:y1,x0:x1].copy()
            wanted[labels[y0:y1,x0:x1] != patch['section']] = 0
            assert png.tobytes() == wanted.tobytes()
            reconstructed.alpha_composite(png, patch['rect'][:2])
        assert reconstructed.tobytes() == im.tobytes()
        palette = {tuple(p[:3]) for p in rgba[alpha]}
        assert all(tuple(bytes.fromhex(p['color'][1:7])) in palette for p in m['parts'] if not p.get('surface'))
        # Exact source partition solids must form a single joined physical tool.
        # A shared face must have two positive dimensions; corner-touching pixels
        # alone are not accepted as a handle/head join.
        adjacency = {i:[] for i in range(len(solids))}; joins=[]
        for i,a in enumerate(solids):
            for j in range(i+1,len(solids)):
                b=solids[j];overlap=np.minimum(a['max'],b['max'])-np.maximum(a['min'],b['min'])
                if np.all(overlap >= -1e-8) and sum(overlap > 1e-8) >= 2:
                    adjacency[i].append(j);adjacency[j].append(i)
                    joins.append(dict(a=a['label'],b=b['label'],overlapDimensions=overlap.tolist()))
        visited={0}; pending=[0]
        while pending:
            for j in adjacency[pending.pop()]:
                if j not in visited:visited.add(j);pending.append(j)
        assert len(visited) == len(solids), (m['id'],'disconnected source solid')
        entry.update(writtenRgbaReconstructsSource=True,writtenSolidFootprintEqualsSourceAlpha=True,
            writtenInferredHeightFieldVerified=True,sourcePaletteSideColors=True,physicalSolidCount=len(solids),
            allHeadShaftHandleSolidsConnected=True,positiveFaceJoins=joins)
    spoon=library['CMU3DLooseSpoon'];extra=next(p for p in spoon['parts'] if p['label']=='rounded underside of spoon bowl')
    rim=[p for p in spoon['parts'] if p['label'].startswith('raised bowl rim solid')]
    hits,_=contacts([extra],rim);contact_witnesses(hits,[extra],rim)
    assert any('solidWitness' in h for h in hits)
    proof['spoonUndersideJoin']=next(h['solidWitness'] for h in hits if 'solidWitness' in h)
    # Dense projection sampling proves the added smooth underside fills no
    # transparent source pixel. The underside stays below the bowl interior.
    points=[]
    for x in np.linspace(-.094,.063,251):
        for y in np.linspace(.09,.317,251):points.append((x,y,.0175))
    pts=np.array(points);covered=occupied(extra,pts);im=np.array(source(SPECS[0]));
    ix=np.clip(np.floor(pts[:,0]*32+16).astype(int),0,31);iy=np.clip(np.floor(16-pts[:,1]*32).astype(int),0,31)
    assert np.all(im[iy[covered],ix[covered],3] == 255)
    # The upper interior must be lower than the rim, with actual empty space.
    cavity=[-.015625,.203125,.06]
    assert not any(occupied(p,np.array([cavity]))[0] for p in spoon['parts'])
    proof.update(writtenScalarVectorsVerified=True,spoonCavityEmptySample=cavity,
        spoonRimTop=.075,spoonInteriorTop=.035,curvedUndersideProjectionSamples=len(points))
    def pixel_probe(model, x, y, height, expected):
        point = [(x+.5-16)/32, (16-y-.5)/32, height]
        filled = any(occupied(p, np.array([point]))[0] for p in model['parts'])
        assert filled == expected, (model['id'], point, expected)
        return dict(sourcePixel=[x, y], point=point, solid=filled)
    wrench = library['CMU3DLooseWrench']
    proof['openWrenchJaw'] = dict(
        openMouthToExterior=[pixel_probe(wrench, x, 16, .045, False) for x in range(5)],
        opposingJawMaterial=[pixel_probe(wrench, 3, y, .045, True) for y in (14, 18)])
    hoe = library['CMU3DLooseMiniHoe']
    proof['threeHoeProngs'] = dict(
        raisedTines=[pixel_probe(hoe, x, 11, .075, True) for x in (7, 10, 13)],
        emptyUpperChannels=[pixel_probe(hoe, x, 11, .075, False) for x in (8, 9, 11, 12)],
        sourceOpaqueLowerWeb=[pixel_probe(hoe, x, 11, .035, True) for x in (8, 9, 11, 12)],
        assessment='Three raised metal tines with empty upper channels; the lower dark web preserves opaque source outlines.')


def source_ownership(models, proof):
    index, _, _ = inventory.load_prototypes(ROOT)
    resolver = inventory.Resolver(index['entity'])
    audit = []
    for model in models:
        folder = ROOT/'Resources/Textures'/model['referenceRsi']
        meta = json.loads((folder/'meta.json').read_text())
        state = next(s for s in meta['states'] if s['name'] == model['referenceState'])
        assert state.get('directions', 1) == 1 and state.get('delays', [[1]]) == [[1]]
        for proto in model['sourcePrototypes']:
            resolved = resolver.resolve(proto)
            components = inventory.component_map(resolved)
            assert components == proof['sourceDefinitions'][proto]['components']
            sprite = components['Sprite']
            assert sprite['sprite'] == model['referenceRsi'] and sprite['state'] == model['referenceState']
            assert sprite.get('noRot', False) is False and not sprite.get('snapCardinals', False)
            assert sprite.get('offset', '0,0') == '0,0'
            assert model['sourceSpriteRotates'] and model['useEntityRotation'] and model['yawOffset'] == 0
            excluded = [dict(name=s['name'], directions=s.get('directions', 1),
                reason='Separate held appearance' if s['name'].startswith('inhand-') else 'Unselected world state')
                for s in meta['states'] if s['name'] != model['referenceState']]
            audit.append(dict(prototype=proto, definition=resolved['_source'],
                definitionSha256=sha(ROOT/resolved['_source']), worldRsi=model['referenceRsi'],
                worldState=model['referenceState'], directions=1, frames=1,
                unsnappedEntityRotation=True, zeroSourceOffset=True, excludedRsiStates=excluded,
                itemAppearance=components.get('Item', {}),
                gameplayOwners=sorted(k for k in components if k not in ('Sprite', 'Item', 'Transform', 'Physics', 'Fixtures'))))
    proof['sourceOwnershipAudit'] = audit
    assert proof['sourceDefinitions']['RMCKitchenKnife']['components']['Sprite'] == proof['sourceDefinitions']['RMCKitchenKnifeChef']['components']['Sprite']
    proof['kitchenChefWorldAppearanceIdentical'] = True


def cards(models):
    montage=Image.new('RGB',(1320,7*245),'#182C38')
    library={m['id']:m for m in models}
    for row,spec in enumerate(SPECS):
        m=library[spec[1]]
        card=Image.new('RGB',(1320,245),'#182C38');d=ImageDraw.Draw(card)
        d.text((10,8),f'{m["label"]}: source and four physical views; {len(m["parts"])} parts',fill='white',font=ImageFont.load_default(size=16))
        im=source(spec).resize((192,192),Image.Resampling.NEAREST);card.paste(im,(10,38),im)
        for j,yaw in enumerate((-math.pi/2,-math.pi/3,math.pi/3,math.pi)):
            panel=bm.render_model(m,(270,205),yaw,.62,pixels_per_unit=250,screen_origin=(135,120))
            card.paste(panel,(220+j*275,35))
        card.save(REVIEW/(m['id']+'.png'));montage.paste(card,(0,row*245))
    montage.save(REVIEW/'source-four-view-montage.png')


def contexts(models, proof):
    index, _, _ = inventory.load_prototypes(ROOT)
    resolver = inventory.Resolver(index['entity'])
    defaults = {}

    def default(proto):
        if proto not in defaults:
            try:
                defaults[proto] = inventory.component_map(resolver.resolve(proto))
            except (KeyError, ValueError):
                defaults[proto] = {}
        return defaults[proto]

    library = {m['id']: m for m in json.loads((BASE/'models.json').read_text())['models']}
    library.update({m['id']: m for m in models})
    mapping = {pid: m for m in models for pid in m['sourcePrototypes']}
    evidence, hidden, duplicate, guards, inputs = [], [], [], [], {}
    for proto, m in mapping.items():
        dc = default(proto)
        assert sprite_states.saved_pose(m, dc, {}, scene.normalize_tint) == (m['referenceState'], None)
        for name, changed in [('unexpected source state', {'Sprite': {'layers': [{'state': 'crushed'}]}}),
                              ('extra layer', {'Sprite': {'layers': [{'state': m['referenceState']}, {'state': m['referenceState']}]}}),
                              ('layer tint', {'Sprite': {'layers': [{'state': m['referenceState'], 'color': '#FF0000'}]}}),
                              ('offset change', {'Sprite': {'offset': '.5,0'}})]:
            state, reason = sprite_states.saved_pose(m, dc, changed, scene.normalize_tint)
            assert state is None
            guards.append(dict(prototype=proto, case=name, fallbackReason=reason))
    for spec in json.loads((BASE/'scenes.json').read_text()):
        path = BASE/spec['file']
        doc = json.loads(path.read_text())
        targets = [e for e in doc['instances'] if e['prototype'] in IDS]
        if not targets:
            continue
        inputs[spec['file']] = sha(path)
        _, records = scene.read_map(ROOT/doc['map']['path'])
        h = scene.hidden_container_entities(records)
        hidden.extend(dict(variant=spec['variant'], level=spec['level'], id=uid, prototype=r['prototype'], savedComponentTypes=r['componentTypes'])
                      for uid, r in records.items() if r['prototype'] in IDS and uid in h)
        for r in records.values():
            default(r['prototype'])
        transforms = scene.WorldTransforms(records, defaults)
        for e in targets:
            m = mapping[e['prototype']]
            raw = records[e['id']]
            assert e['id'] not in h
            e.update(modelId=m['id'], matchKind='exact', renderYaw=e['yaw'])
            assert sprite_states.resolve_scene_pose(e, m, default(e['prototype']), raw['components'], scene.normalize_tint)
        layout.resolve_layout(targets, list(library.values()), records, defaults, transforms)
        sprite_states.scene_variants(targets, library, doc['geometryVariants'])
        # Preserve the original pivot-support expectation so the final export
        # audit must explicitly account for the one authored contact correction.
        original_support = [{k: v for k, v in m.items() if k != 'supportProbePart'} for m in library.values()]
        placement.resolve_placements(doc['instances'], original_support, doc['geometryVariants'])
        groups = defaultdict(list)
        for e in targets:
            near = [n for n in doc['instances'] if n['id'] != e['id'] and sum((a-b)**2 for a, b in zip(n['position'][:2], e['position'][:2])) < .7**2]
            evidence.append(dict(variant=spec['variant'], level=spec['level'], id=e['id'], prototype=e['prototype'], modelId=e['modelId'],
                                 position=e['position'], savedYaw=e['yaw'], renderYaw=e['renderYaw'], renderOffset=e.get('renderOffset', [0, 0, 0]),
                                 support=e.get('support'), sourceState=e['spriteState'], sourceFrame=0, savedComponents=records[e['id']]['components'],
                                 nearbySources=[dict(id=n['id'], prototype=n['prototype'], modeled=bool(n.get('modelId'))) for n in near]))
            groups[(e['prototype'], tuple(e['position']))].append(e['id'])
        duplicate.extend(dict(variant=spec['variant'], level=spec['level'], prototype=k[0], position=k[1], ids=v)
                         for k, v in groups.items() if len(v) > 1)
        selected = ([22979, 16869, 22628] if spec['level'] == 0 else [3312] if spec['level'] == -2 else [4275] if spec['level'] == 1 else []) if spec['variant'] == 'redux' else []
        for uid in selected:
            e = next(t for t in targets if t['id'] == uid)
            assembled, omitted = [], []
            for n in doc['instances']:
                if sum((a-b)**2 for a, b in zip(n['position'][:2], e['position'][:2])) > .9**2:
                    continue
                nm = library.get(n.get('modelId'))
                if nm is None:
                    omitted.append(dict(id=n['id'], prototype=n['prototype']))
                    continue
                nparts = doc['geometryVariants'].get(n.get('geometryKey'), nm['parts'])
                assembled.extend(world_parts(nparts, [n['position'][0]-e['position'][0], n['position'][1]-e['position'][1], 0],
                                             n.get('renderYaw', n['yaw']), n.get('renderOffset', [0, 0, 0])))
            assembled.append(part('flat reference floor', (0, 0, -.035), (.9, .9, .025), '#53504A'))
            card = Image.new('RGB', (1200, 575), '#182C38')
            d = ImageDraw.Draw(card)
            d.text((12, 10), f'{spec["variant"]} {spec["level"]:+d} / UID {uid} / saved pivots and duplicate objects retained', fill='white', font=ImageFont.load_default(size=16))
            for j, yaw in enumerate((-math.pi/3, math.pi/2+.2)):
                panel = bm.render_model(dict(parts=assembled), (590, 460), yaw, .65, pixels_per_unit=270, screen_origin=(295, 350))
                card.paste(panel, (j*600, 50))
            d.text((12, 540), f'{len(omitted)} unknown neighbors omitted; flat reference underlay. Item pile fit is not resolved.', fill='#BBD4DF', font=ImageFont.load_default(size=15))
            card.save(REVIEW/f'context-{spec["variant"]}-{spec["level"]}-{uid}.png')
            proof.setdefault('representativeContexts', []).append(dict(variant=spec['variant'], level=spec['level'], id=uid, omittedNeighbors=omitted))
    assert len(evidence) == 54
    proof.update(contexts=evidence, hiddenSourceExclusions=hidden, exactCoincidentGroups=duplicate, appearanceFallbackChecks=guards,
                 inputScenesSha256=inputs, placementCounts=dict(Counter(e['variant'] for e in evidence)),
                 sourceDefinitions={proto: dict(file=resolver.resolve(proto)['_source'], components=default(proto)) for proto in IDS})
    print(json.dumps(dict(visiblePlacements=len(evidence), hiddenSources=len(hidden), duplicateGroups=len(duplicate))), flush=True)


def notes():
    source_details=[]
    for spec in SPECS:
        meta=json.loads((ROOT/'Resources/Textures'/spec[3]/'meta.json').read_text())
        source_details.append(f'- {spec[0]}: `{spec[3]}`, `{spec[4]}`. License: {meta["license"]}. {meta["copyright"]}')
    NOTE.write_text("""# Loose kitchen and garden tool drafts

Seven physical designs cover eight exact source prototypes: RMCSpoon, CMWrench, RMCToolHatchet, RMCToolMiniHoe, RMCToolSpade, RMCKitchenKnife, RMCKitchenKnifeChef and RMCKitchenKnifePlastic. Kitchen and chef knife resolve to identical world source art and share one model. The frozen 1015 baseline contains 54 visible targets: 36 Redux and 18 classic. Saved pivots and cardinal angles remain unchanged. All are drafts.

Every selected world state is static, one direction, ordinary rotating unsnapped Sprite with no source offset. In-hand and equipped resources are separate character appearances and remain sprites. Plastic spoon is an unrelated resource, not an automatic variant; only the explicitly requested plastic knife state is modeled. Generic spriteStates and sourceSpriteRotates preserve strict actual RSI/state/frame/appearance fallback. Tool use, digging, utensils, mixing, melee, damage, surgery, corroding and interactions remain owned by original components; no animation or live state controller is invented.

The tools lie in the source X/Y footprint, using exact separated opaque source regions as actual solid extrusions, with original RGBA crops on their exposed upper surfaces. The wrench jaw and all source alpha gaps remain genuinely empty; the garden tools keep their source diagonal silhouettes. Thicker shafts/grips join their heads through positive-area faces. Thin knife/hatchet cutting edges connect to heavier spines. Spoon and spade interiors are physically lower than their rims, and the spoon has a smooth ellipsoid underside fully within the source alpha footprint. The miniature hoe's dark inter-ridge pixels are opaque source outlines: they are preserved as a lowered web beneath raised metal ridges, not falsely erased as transparency. Source pixel steps are retained; subpixel blade smoothing is not claimed. Thickness, underside, bevel and recess depths are inferred (.012-.098 tiles), not recovered 3D measurements. No entire-tool image plane substitutes for the separate physical head, shaft and grip.

The proof reconstructs every original RGBA pixel from written PNGs and checks written geometry footprints, exact height regions, source palette side colors, single connected physical assemblies and a real empty spoon cavity. It also checks all saved source frames, rotations, support offsets and duplicate groups. Dense original tool piles may intersect; their source transforms are retained. Unknown neighbor models and shared support gaps remain explicit limitations. Context cards use clearly labeled flat reference underlays. There is no exhaustive unrelated contact matrix or gameplay/GPU claim.

The hatchet alone opts into supportProbePart `continuous angled shaft and grip solid 11`. Its local center (.171875,-.125) lies in opaque material at the model bottom. On Redux -2, hatchet 3313's saved pivot misses rack 549 by .02915 tiles; this complete grip box lies within the rack top. After ordinary pivot support misses, the shared/native helper tests this rotated contact point and applies only the .950 vertical render offset. Existing pivot support takes priority; invalid, animated, nonopaque or elevated probe parts are rejected. Source position, yaw, artwork and physical geometry are unchanged. The final export audit distinguishes this one corrected contact from the original support expectations.

The steel kitchen/chef knife spine has a flat underside at local Z=0. Five existing spine solids extend .014 tiles lower than the initial inferred draft, retaining their source footprint and .057 top height. The thinner cutting edge is unchanged. This lets the blade support the two chef knives whose grips overhang their tables by .03889 tiles, while preserving their .862 vertical offset and original source X/Y/yaw. Final blade-contact witnesses cover Redux 16869 and classic 11349.

Reproduce dedicated assets with Tools/three_d/author_loose_tools.py. --check-assets regenerates into an isolated directory and compares exact bytes. --export-parity MANIFEST.json verifies final source/render/support/frame/geometry parity and local contacts for the 54 tools. Shared supportProbePart schema and matching Python/native helpers implement the explicit contact fallback. Gameplay and collision are unchanged.

Source attribution, preserved verbatim from each RSI metadata file:

"""+'\n'.join(source_details)+'\n',encoding='utf-8')


def save(proof):
    proof['writtenAssetSha256'] = {p.relative_to(ROOT).as_posix(): sha(p) for p in [MODEL, ART, NOTE,
        *[TEXTURES/(r['id']+'.png') for r in yaml.safe_load(ART.read_text())]]}
    proof['generatorSha256'] = sha(Path(__file__))
    (GEN/'loose-tools-proof.json').write_text(json.dumps(proof, indent=2)+'\n')


def check_assets():
    global MODEL, ART, TEXTURES, REVIEW
    originals = [MODEL, ART, *[TEXTURES/(r['id']+'.png') for r in yaml.safe_load(ART.read_text())]]
    stage = ROOT/'.codex/loose-tools-regeneration-check'
    stage.mkdir(parents=True, exist_ok=True)
    MODEL, ART, TEXTURES, REVIEW = stage/MODEL.name, stage/ART.name, stage/'textures', stage/'review'
    author()
    checks = []
    for p in originals:
        candidate = (TEXTURES if p.suffix == '.png' else stage)/p.name
        assert candidate.read_bytes() == p.read_bytes(), p
        checks.append(dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p)))
    (stage/'comparison.json').write_text(json.dumps(dict(byteIdentical=len(checks), files=checks), indent=2)+'\n')
    print(json.dumps(dict(byteIdentical=len(checks))), flush=True)


def export_parity(manifest):
    proof = json.loads((GEN/'loose-tools-proof.json').read_text())
    expected = {(e['variant'], e['level'], e['id']): e for e in proof['contexts']}
    models = {m['id']: m for m in bm.load_models(MODEL)}
    checks, inputs, contact_audit = [], {}, []
    library = {m['id']: m for m in json.loads((GEN/'models.json').read_text())['models']}
    hidden = {(e['variant'], e['level'], e['id']) for e in proof['hiddenSourceExclusions']}
    for spec in json.loads((GEN/manifest).read_text()):
        path = GEN/spec['file']
        doc = json.loads(path.read_text())
        inputs[spec['file']] = sha(path)
        instances = {entity['id']: entity for entity in doc['instances']}
        for entity in doc['instances']:
            assert (spec['variant'], spec['level'], entity['id']) not in hidden
            if entity['prototype'] not in IDS:
                continue
            e = expected[(spec['variant'], spec['level'], entity['id'])]
            for key in ('position', 'modelId', 'renderYaw'):
                assert entity.get(key) == e.get(key), (e['id'], key)
            assert entity['yaw'] == e['savedYaw']
            offset = entity.get('renderOffset', [0, 0, 0])
            support_change = entity.get('support') != e.get('support') or offset != e['renderOffset']
            if support_change:
                assert (spec['variant'], spec['level'], e['id']) == ('redux', -2, 3313)
                assert offset[:2] == e['renderOffset'][:2] and offset[2] == .95
                assert entity['support']['entity'] == 549 and entity['support']['height'] == .948
            assert entity['spriteState'] == e['sourceState'] and entity['spriteFrame'] == 0 and entity['matchKind'] == 'exact'
            m = models[entity['modelId']]
            parts = doc['geometryVariants'].get(entity.get('geometryKey'), m['parts'])
            assert json.dumps(parts, sort_keys=True) == json.dumps(m['parts'], sort_keys=True)
            own = world_parts(parts, entity['position'], entity['renderYaw'], offset)
            audit = final_contacts(entity, own, instances, library, doc['geometryVariants'])
            if support_change:
                assert audit['supportContact']['bottomFaceWitness']
            contact_audit.append(dict(variant=spec['variant'], level=spec['level'], **audit))
            checks.append(dict(variant=spec['variant'], level=spec['level'], id=e['id'], sourceTransformUnchanged=True,
                               renderSupportEqual=not support_change, boundedSupportCorrection=support_change,
                               actualRenderOffset=offset, actualSupport=entity.get('support'), geometryEqual=True, stateFrameEqual=True))
    assert len(checks) == 54
    proof.update(finalSceneParity=checks, finalSceneInputsSha256=inputs, finalHiddenExclusionsVerified=True,
                 finalTargetedContacts=contact_audit, finalContactScope='Only 54 loose-tool sources and neighbors within .85 tiles; physical/alpha witnesses, no whole-map audit.',
                 status='seven draft assets and all 54 final visible placements verified; source tool piles may overlap')
    regeneration = json.loads((ROOT/'.codex/loose-tools-regeneration-check/comparison.json').read_text())
    assert all(sha(ROOT/e['path']) == e['sha256'] for e in regeneration['files'])
    proof['deterministicRegeneration'] = regeneration
    save(proof)
    final = dict(passed=True, placements=len(checks), placementCounts=dict(Counter(e['variant'] for e in checks)),
        sourceTransformsUnchanged=True, geometryUnchanged=True,
        correctedSupports=[e for e in checks if e['boundedSupportCorrection']],
        supportedPlacements=sum(bool(e['actualSupport']) for e in checks),
        bottomFaceContactWitnesses=sum(bool((e['supportContact'] or {}).get('bottomFaceWitness')) for e in contact_audit),
        sourcesWithNeighborSolidOverlap=sum(any(n['solidOverlapWitnesses'] for n in e['knownNeighbors']) for e in contact_audit),
        neighboringUnknownAppearances=sum(len(e['unknownNeighbors']) for e in contact_audit),
        chefBladeContactWitnesses=[dict(variant=e['variant'], level=e['level'], id=e['id'],
            witness=e['supportContact']['bottomFaceWitness']) for e in contact_audit
            if (e['variant'], e['level'], e['id']) in (('redux', 0, 16869), ('classic', 0, 11349))],
        scope=proof['finalContactScope'], sceneInputsSha256=inputs,
        proof='loose-tools-proof.json', proofSha256=sha(GEN/'loose-tools-proof.json'))
    assert len(final['correctedSupports']) == 1 and final['supportedPlacements'] == 54
    assert final['bottomFaceContactWitnesses'] == 54
    assert len(final['chefBladeContactWitnesses']) == 2 and all(
        e['witness']['itemPart'].startswith('steel blade spine and tang solid') for e in final['chefBladeContactWitnesses'])
    (GEN/'loose-tools-final-export-audit.json').write_text(json.dumps(final, indent=2)+'\n')
    print(json.dumps(dict(finalPlacements=len(checks), proofSha256=sha(GEN/'loose-tools-proof.json'))), flush=True)


def final_contacts(entity, own, instances, library, variants):
    """Local exported geometry evidence; positive witnesses never use AABB alone."""
    result = dict(id=entity['id'], prototype=entity['prototype'], knownNeighbors=[], unknownNeighbors=[], supportContact=None)
    support_id = (entity.get('support') or {}).get('entity')
    solids = [p for p in own if not p.get('surface')]
    bottom = min(box_bounds(p)[0][2] for p in solids)
    for other in instances.values():
        if other['id'] == entity['id']:
            continue
        if other['id'] != support_id and sum((a-b)**2 for a,b in zip(other['position'][:2], entity['position'][:2])) > .85**2:
            continue
        model = library.get(other.get('modelId'))
        if model is None:
            result['unknownNeighbors'].append(dict(id=other['id'], prototype=other['prototype']))
            continue
        parts = variants.get(other.get('geometryKey'), model['parts'])
        world = world_parts(parts, other['position'], other.get('renderYaw', other['yaw']), other.get('renderOffset', [0, 0, 0]))
        hits, gap = contacts(solids, world)
        witnesses = []
        # One exact interior witness establishes a neighboring intersection.
        # Stop after three; retain the full broad-phase candidate count.
        for hit in hits:
            contact_witnesses([hit], solids, world)
            if hit.get('solidWitness'):
                witnesses.append(hit)
                if len(witnesses) == 3:
                    break
        result['knownNeighbors'].append(dict(id=other['id'], prototype=other['prototype'],
            minimumAabbSeparation=gap, aabbPairCandidates=len(hits), solidOverlapWitnesses=witnesses,
            assessment='Positive physical/alpha overlap; source transforms retained' if witnesses else
                'AABB candidates without a sampled solid witness; separation not claimed' if hits else 'Separated part bounds'))
        if other['id'] != support_id:
            continue
        labels = model.get('supportSurfaces', []) or [model.get('supportSurface')]
        support_parts = [p for p in world if p['label'] in labels]
        top = entity['support']['height']
        assert abs(bottom-top-.002) < 1e-6
        probe = None
        for part in solids:
            lo, hi = box_bounds(part)
            if abs(lo[2]-bottom) > 1e-6:
                continue
            for support_part in support_parts:
                slo, shi = box_bounds(support_part)
                low, high = np.maximum(lo[:2], slo[:2]), np.minimum(hi[:2], shi[:2])
                if min(high-low) <= 1e-8:
                    continue
                x, y = np.meshgrid(*[np.linspace(l+(h-l)*.05,h-(h-l)*.05,11) for l,h in zip(low,high)])
                points = np.column_stack((x.ravel(), y.ravel(), np.full(x.size, bottom+.00001)))
                lower = points.copy(); lower[:, 2] = top-.00001
                inside = occupied(part, points) & occupied(support_part, lower)
                if inside.any():
                    i = np.flatnonzero(inside)[0]
                    probe = dict(itemPart=part['label'], supportPart=support_part['label'],
                        xy=points[i,:2].tolist(), itemBottom=bottom, supportTop=top, clearance=.002)
                    break
            if probe:
                break
        result['supportContact'] = dict(entity=support_id, top=top, itemBottom=bottom,
            bottomFaceWitness=probe, assessment='Bottom face overlaps authored support with .002 render clearance' if probe else
                'Pivot support selected, but no bottom-face contact witness; local overhang remains')
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--check-assets', action='store_true')
    parser.add_argument('--export-parity')
    parser.add_argument('--refresh-proof', action='store_true')
    args = parser.parse_args()
    if args.check_assets:
        check_assets()
        return
    if args.export_parity:
        export_parity(args.export_parity)
        return
    if args.refresh_proof:
        proof = json.loads((GEN/'loose-tools-proof.json').read_text())
        assert all(sha(ROOT/path) == digest for path, digest in proof['writtenAssetSha256'].items())
        assert all(sha(BASE/path) == digest for path, digest in proof['inputScenesSha256'].items())
        models = bm.load_models(MODEL)
        written_proof(models, proof)
        source_ownership(models, proof)
        cards(models)
        save(proof)
        return
    models, proof = author()
    notes()
    written_proof(models, proof)
    cards(models)
    contexts(models, proof)
    source_ownership(models, proof)
    save(proof)


if __name__ == '__main__':
    main()
