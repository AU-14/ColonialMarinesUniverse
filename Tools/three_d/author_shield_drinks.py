"""Source-specific riot shield and beer can; dedicated assets and bounded evidence."""
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
from author_wide_machinery import world_parts, contacts

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT/'.codex/model-batch-baseline1006'
GEN = ROOT/'Tools/three_d/generated'
PROTOS = ROOT/'Content.CMU/Resources/ThreeD/Prototypes/World'
MODEL = PROTOS/'garrison_shield_drinks.yml'
ART = PROTOS/'garrison_shield_drinks_art.yml'
TEXTURES = ROOT/'Content.CMU/Resources/Textures/CMU14/ThreeD/Surfaces/ShieldDrinks'
NOTE = ROOT/'Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_SHIELD_DRINKS.md'
REVIEW = GEN/'review/shield-drinks'
SPECS = [
    ('RiotShield', 'CMU3DRiotShield', 'Riot shield', 'Objects/Weapons/Melee/shields.rsi', 'riot-icon'),
    ('CMDrinkCanBeerLite', 'CMU3DBeerLiteCan', 'Light beer can', '_RMC14/Objects/Consumable/Drinks/beer.rsi', 'icon'),
]
IDS = [s[0] for s in SPECS]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def source(spec):
    return Image.open(ROOT/'Resources/Textures'/spec[3]/(spec[4]+'.png')).convert('RGBA')


def author():
    TEXTURES.mkdir(parents=True, exist_ok=True)
    REVIEW.mkdir(parents=True, exist_ok=True)
    used = {r['atlasIndex'] for p in PROTOS.glob('*.yml') if p.name != ART.name
            for r in yaml.load(p.read_text(encoding='utf-8-sig'), Loader=yaml.CSafeLoader) or [] if r.get('type') == 'cmu3DSurface'}
    assert not used.intersection(range(3200, 3300))
    models, art, entries = [], [], []
    for spec in SPECS:
        proto, mid, label, rsi, state = spec
        folder = ROOT/'Resources/Textures'/rsi
        meta = json.loads((folder/'meta.json').read_text())
        state_meta = next(s for s in meta['states'] if s['name'] == state)
        assert state_meta.get('directions', 1) == 1 and state_meta.get('delays', [[1]]) == [[1]]
        im = source(spec)
        patches = []

        def crop(rect, name):
            uid = 'CMU3DShieldDrinksSurface' + str(3200+len(art))
            pixels = im.crop(rect)
            pixels.save(TEXTURES/(uid+'.png'))
            art.append(dict(type='cmu3DSurface', id=uid, atlasIndex=3200+len(art),
                            texture='/Textures/CMU14/ThreeD/Surfaces/ShieldDrinks/'+uid+'.png'))
            patches.append(dict(surface=uid, rect=list(rect), name=name, rgbaSha256=hashlib.sha256(pixels.tobytes()).hexdigest()))
            return uid

        if proto == 'RiotShield':
            parts = []
            # The seven disjoint source partitions retain every glass alpha and
            # stepped corner, while frame and central reinforcement have depth.
            rects = [('top frame', (11, 8, 21, 9), .025), ('bottom frame', (11, 23, 21, 24), .025),
                     ('left frame', (11, 9, 12, 23), .025), ('right frame', (20, 9, 21, 23), .025),
                     ('upper glazing', (12, 9, 20, 13), .006), ('central reinforcement', (12, 13, 20, 17), .032),
                     ('lower glazing', (12, 17, 20, 23), .006)]
            for name, rect, depth in rects:
                x0, y0, x1, y1 = rect
                parts.append(part(name, ((x0+x1-32)/64, 0, (48-y0-y1)/64),
                                  ((x1-x0)/64, depth, (y1-y0)/64), '#FFFFFF',
                                  surface=crop(rect, name), surfaceAxis='XZ'))
            parts += [part('lower rear handle stand-off', (0, .052, .231), (.022, .028, .012), '#363636'),
                      part('upper rear handle stand-off', (0, .052, .287), (.022, .028, .012), '#363636'),
                      part('rear cylindrical grip', (0, .080, .259), (.016, .016, .041), '#1B1B1B', 'CylinderZ')]
        else:
            cx = -.015625
            parts = [part('round amber can body', (cx, 0, .222), (.132, .132, .204), '#B06C00', 'CylinderZ'),
                     part('folded lower can rim', (cx, 0, .020), (.137, .137, .012), '#424242', 'CylinderZ')]
            # The front label follows the cylinder in three shallow facets. The
            # hidden rear label is deliberately not invented or repeated.
            for name, rect, xpos, ypos, yaw in [
                    ('left label', (11, 12, 14, 24), cx-.08278, -.11187, -40),
                    ('central label', (14, 12, 17, 24), cx, -.142, 0),
                    ('right label', (17, 12, 20, 24), cx+.08278, -.11187, 40)]:
                parts.append(part(name, (xpos, ypos, .213), (.046875, .012, .1875), '#FFFFFF',
                                  yaw=yaw, surface=crop(rect, name), surfaceAxis='XZ'))
            # Raised rolled rim surrounding a physically lower lid, rather than
            # an opaque disk placed over the rim's central recess.
            for i in range(12):
                angle = i*math.tau/12
                parts.append(part('rolled rim segment '+str(i+1), (cx+.132*math.cos(angle), .132*math.sin(angle), .435),
                                  (.132*math.tan(math.pi/12)+.002, .008, .019), '#BFBFBF', yaw=(math.degrees(angle)+90)%360))
            parts.append(part('recessed metal lid', (cx, 0, .425), (.125, .125, .004), '#999999', 'CylinderZ'))
            parts.append(part('inset original lid and scored opening', (cx, 0, .430), (.110, .048888889, .001), '#FFFFFF',
                              surface=crop((11, 8, 20, 12), 'entire original lid'), surfaceAxis='XY'))
            # Original icon does not distinguish open/closed: the dark source
            # scoring is retained, without inventing a state-dependent hole.
            parts += [part('pull tab left raised edge', (cx-.019, .014, .433), (.005, .029, .003), '#A6A6A6'),
                      part('pull tab right raised edge', (cx+.019, .014, .433), (.005, .029, .003), '#A6A6A6'),
                      part('pull tab bridge', (cx, .038, .433), (.019, .005, .003), '#A6A6A6')]
        record = dict(type='cmu3DModel', id=mid, label=label, status='draft', sourcePrototypes=[proto], referencePrototype=proto,
                      referenceRsi=rsi, referenceState=state, sourceDirections=1, sourceSpriteRotates=True,
                      sourceSpriteOffset='0,0', useEntityRotation=True, yawOffset=0, groundOffset='0,0', placement='surface', parts=parts,
                      spriteStates={state: dict(frames=[dict(parts=copy.deepcopy(parts))], delays=[1])},
                      description='Dropped world source only. Original RGBA partitions, physical frame/handle or cylindrical can with rolled rim and recessed lid. Depth, rear construction and height are inferred. Original open/closed beer visuals both use icon; no extra state or gameplay is invented. Source-layer fallback and original gameplay remain active. See SOURCES_SHIELD_DRINKS.md.')
        models.append(record)
        entries.append(dict(prototype=proto, modelId=mid, parts=len(parts), referenceRsi=rsi, state=state, sourceFrames=1,
                            sourceDirections=1, sourceBounds=list(im.getbbox()), sourceSha256=sha(folder/(state+'.png')),
                            sourceMetaSha256=sha(folder/'meta.json'), license=meta['license'], copyright=meta['copyright'], patches=patches))
    MODEL.write_text('# Static world appearances; all models remain source-guided drafts.\n'+yaml.safe_dump(serialize(models), sort_keys=False, width=110), encoding='utf-8')
    ART.write_text('# CC-BY-SA-3.0 original shield and beer source partitions.\n'+yaml.safe_dump(art, sort_keys=False), encoding='utf-8')
    surfaces.load_surfaces.cache_clear()
    loaded = bm.load_models(MODEL)
    proof = dict(status='draft assets written; parent export pending', models=entries, atlasIndices=[e['atlasIndex'] for e in art])
    print(json.dumps(dict(models=len(models), parts=[len(m['parts']) for m in models], textures=len(art))), flush=True)
    return loaded, proof


def written_proof(models, proof):
    raw = yaml.safe_load(MODEL.read_text())
    assert all(isinstance(p[k], str) for m in raw for p in m['parts'] for k in ('min', 'max'))
    library = {m['id']: m for m in models}
    for spec, e in zip(SPECS, proof['models']):
        m = library[e['modelId']]
        assert m['parts'] == m['spriteStates'][spec[4]]['frames'][0]['parts']
        im = source(spec)
        reconstructed = Image.new('RGBA', im.size)
        palette = {tuple(p[:3]) for p in np.array(im)[np.array(im)[:, :, 3] > 0]}
        assert all(tuple(bytes.fromhex(p['color'][1:7])) in palette for p in m['parts'] if not p.get('surface'))
        for patch in e['patches']:
            png = Image.open(TEXTURES/(patch['surface']+'.png')).convert('RGBA')
            assert png.tobytes() == im.crop(patch['rect']).tobytes()
            reconstructed.paste(png, patch['rect'][:2])
        assert reconstructed.tobytes() == im.tobytes()
        e.update(writtenRgbaPartitionsReconstructEntireSource=True, staticPoseEqualsDefault=True, writtenSolidPaletteExact=True,
                 sourceAlphaValues=sorted(set(np.array(im)[:, :, 3].ravel().tolist())))
    shield = library['CMU3DRiotShield']
    p = {p['label']: p for p in shield['parts']}
    joins = []
    for a, b in [('central reinforcement', 'lower rear handle stand-off'), ('central reinforcement', 'upper rear handle stand-off'),
                 ('lower rear handle stand-off', 'rear cylindrical grip'), ('upper rear handle stand-off', 'rear cylindrical grip')]:
        hits, _ = contacts([p[a]], [p[b]])
        contact_witnesses(hits, [p[a]], [p[b]])
        assert any('solidWitness' in h for h in hits), (a, b)
        joins.append(dict(fromPart=a, toPart=b, witness=next(h['solidWitness'] for h in hits if 'solidWitness' in h)))
    proof.update(writtenScalarVectorsVerified=True, handleJoins=joins, canLidBelowRim=.023,
                 sourceOpenableMapping={'false': 'icon', 'true': 'icon'}, unsupportedResourceStates=['crushed'])
    assert not any(occupied(part, np.array([[0, .05, .259]]))[0] for part in shield['parts'])
    can = library['CMU3DBeerLiteCan']
    assert not any(occupied(part, np.array([[-.015625, 0, .445]]))[0] for part in can['parts'])
    cp = {part['label']: part for part in can['parts']}
    can_joins = []
    for name in ['folded lower can rim', 'left label', 'central label', 'right label', 'recessed metal lid',
                 *['rolled rim segment '+str(i+1) for i in range(12)]]:
        hits, _ = contacts([cp['round amber can body']], [cp[name]])
        contact_witnesses(hits, [cp['round amber can body']], [cp[name]])
        assert any('solidWitness' in h for h in hits), name
        can_joins.append(dict(part=name, bodyJoinWitness=next(h['solidWitness'] for h in hits if 'solidWitness' in h)))
    proof.update(canBodyJoins=can_joins, shieldHandGapSample=[0, .05, .259], canRimRecessSample=[-.015625, 0, .445])


def cards(models):
    library = {m['id']: m for m in models}
    montage = Image.new('RGB', (1500, 760), '#182C38')
    for row, spec in enumerate(SPECS):
        m = library[spec[1]]
        card = Image.new('RGB', (1500, 380), '#182C38')
        d = ImageDraw.Draw(card)
        d.text((12, 12), f'{m["label"]} / original static source + four physical views / {len(m["parts"])} parts', fill='white', font=ImageFont.load_default(size=17))
        im = source(spec).resize((256, 256), Image.Resampling.NEAREST)
        card.paste(im, (0, 70), im)
        for j, yaw in enumerate((-math.pi/2, -math.pi/3, math.pi/3, math.pi)):
            panel = bm.render_model(m, (300, 300), yaw, .42, pixels_per_unit=460, screen_origin=(150, 263))
            card.paste(panel, (275+j*305, 50))
        d.text((12, 357), 'Draft: original pixels and alpha preserved; depth and hidden rear geometry inferred.', fill='#BBD4DF', font=ImageFont.load_default(size=15))
        card.save(REVIEW/(m['id']+'.png'))
        montage.paste(card, (0, row*380))
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
    mapping = {m['referencePrototype']: m for m in models}
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
    beer = default('CMDrinkCanBeerLite')
    visuals = beer['GenericVisualizer']['visuals']['enum.OpenableVisuals.Opened']['enum.OpenableVisuals.Layer']
    assert visuals.get('true', visuals.get(True)) == visuals.get('false', visuals.get(False)) == {'state': 'icon'}
    proof['openClosedSameSourceVerified'] = True
    for opened in (False, True):
        assert sprite_states.saved_pose(mapping['CMDrinkCanBeerLite'], beer, {'Openable': {'opened': opened}}, scene.normalize_tint) == ('icon', None)
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
            assert e['id'] not in h and raw['componentTypes'] == ['Transform']
            e.update(modelId=m['id'], matchKind='exact', renderYaw=e['yaw'])
            assert sprite_states.resolve_scene_pose(e, m, default(e['prototype']), raw['components'], scene.normalize_tint)
        layout.resolve_layout(targets, list(library.values()), records, defaults, transforms)
        sprite_states.scene_variants(targets, library, doc['geometryVariants'])
        placement.resolve_placements(doc['instances'], list(library.values()), doc['geometryVariants'])
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
        selected = [2697] if spec['level'] == 1 else ([2881, 2884, 2887] if spec['variant'] == 'redux' else [])
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
    assert len(evidence) == 27 and len(hidden) == 3
    proof.update(contexts=evidence, hiddenSourceExclusions=hidden, exactCoincidentGroups=duplicate, appearanceFallbackChecks=guards,
                 inputScenesSha256=inputs, placementCounts=dict(Counter(e['variant'] for e in evidence)),
                 sourceDefinitions={proto: dict(file=resolver.resolve(proto)['_source'], components=default(proto)) for proto in IDS})
    print(json.dumps(dict(visiblePlacements=len(evidence), hiddenSources=len(hidden), duplicateGroups=len(duplicate))), flush=True)


def notes():
    NOTE.write_text('''# Riot shield and light beer can drafts

Two exact source prototypes: RiotShield and CMDrinkCanBeerLite. Frozen 1006-model map baseline has 10 visible Redux shields and 17 visible beer cans (10 Redux, 7 classic). Three additional Redux shields are inside the closed entity storage of source entity 2; their hidden children/actions are not independent physical targets. All 27 visible sources have only saved Transform data and yaw zero. Their pivots, gameplay fixtures and inventory/held character sprites are unchanged.

RiotShield uses Objects/Weapons/Melee/shields.rsi, static one-direction riot-icon. Seven disjoint original RGBA partitions form an actual thick outer frame, central reinforcing band and separate thinner glazing. The source's alpha 127, 163 and 166 is preserved exactly with the existing renderer's dither policy. A cylindrical rear grip and two stand-offs join the central band, leaving a hand gap behind it. Solid join witnesses are recorded. Depth, rear construction and .5 tile height are inferred from the tiny source; this remains a draft. No opaque entire-shield backing fills the glazing. Blocking, repair and damage/destruction remain with their original systems. No broken or blocking animation is invented; other shield RSI designs and in-hand states are outside scope.

CMDrinkCanBeerLite uses _RMC14/Objects/Consumable/Drinks/beer.rsi, static one-direction icon. The body and bottom rim are true cylinders, with three source-colored label facets, a twelve-segment rolled upper rim and lower inset original lid, plus a raised pull-tab outline. The exact source pixels partition into three label crops and the complete lid crop, including alpha. Label curvature, hidden amber rear, top projection/depth and .454 tile rim height are inferred; the model does not claim a recovered cylindrical UV unwrap. The visible dark lid scoring/opening motif is retained, without making a false state-dependent hole through a closed can.

OpenableSystem publishes OpenableVisuals.Opened; this can's GenericVisualizer maps both true and false to the identical icon. Therefore both gameplay states use the same physical source appearance. There is no separate opening frame or invented open/close clip. The crushed RSI resource exists but is not selected by this prototype's source controller: TrashOnSolutionEmptySystem adds/removes a Trash tag and does not select crushed art. Unexpected crushed or changed live states retain sprite fallback. Drinking, spilling, pressure, shaking, solution quantities, destruction, opening interactions and sounds remain owned by the game. Saved unresolved Appearance data is not guessed. Source spriteStates validates rotating, unsnapped, one-visible-layer icons with unit transforms and exact RSI/state/offset; unsupported layers, tint/scale/offset/state changes retain their original sprites.

The ten Redux shields are co-centered in the original source. They remain separate objects and intersect; no hidden deduplication or artificial stack is introduced. Several beer cans are close together among tables, trash and unmapped clutter. Every target's source/support result and nearby identities is recorded, and four bounded context cards retain original duplicates. There is no exhaustive neighbor collision proof or claim that every item pile is physically separated. Flat review floor underlays are explicitly labeled; map tiles are retained by the parent's scene export.

Attribution: original shield and beer artwork is CC-BY-SA-3.0. Shield metadata credits Citadel-Station-13 commit 84223c65f5caf667a84f3c0f49bc2a41cdc6c4e3 (the unrelated card-shield art credits TaoNewt). Beer metadata credits cmss13 drinkcans.dmi at commit 0e5b77c99f162aa1462823e996ba8e0d52656448; held art links are retained in the proof's exact source copyright string. Preserve source attribution and license for derived PNGs.

Reproduce dedicated assets and proof with Tools/three_d/author_shield_drinks.py. --check-assets writes an isolated regeneration and compares all asset bytes. --export-parity MANIFEST.json verifies final source, state, render transform, support and geometry. No common code, global build/export or runtime process operation occurs. All models remain drafts.
''', encoding='utf-8')


def save(proof):
    proof['writtenAssetSha256'] = {p.relative_to(ROOT).as_posix(): sha(p) for p in [MODEL, ART, NOTE,
        *[TEXTURES/(r['id']+'.png') for r in yaml.safe_load(ART.read_text())]]}
    proof['generatorSha256'] = sha(Path(__file__))
    (GEN/'shield-drinks-proof.json').write_text(json.dumps(proof, indent=2)+'\n')


def check_assets():
    global MODEL, ART, TEXTURES, REVIEW
    originals = [MODEL, ART, *[TEXTURES/(r['id']+'.png') for r in yaml.safe_load(ART.read_text())]]
    stage = ROOT/'.codex/shield-drinks-regeneration-check'
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
    proof = json.loads((GEN/'shield-drinks-proof.json').read_text())
    expected = {(e['variant'], e['level'], e['id']): e for e in proof['contexts']}
    models = {m['id']: m for m in bm.load_models(MODEL)}
    checks, inputs = [], {}
    hidden = {(e['variant'], e['level'], e['id']) for e in proof['hiddenSourceExclusions']}
    for spec in json.loads((GEN/manifest).read_text()):
        path = GEN/spec['file']
        doc = json.loads(path.read_text())
        inputs[spec['file']] = sha(path)
        for entity in doc['instances']:
            assert (spec['variant'], spec['level'], entity['id']) not in hidden
            if entity['prototype'] not in IDS:
                continue
            e = expected[(spec['variant'], spec['level'], entity['id'])]
            for key in ('position', 'modelId', 'renderYaw', 'support'):
                assert entity.get(key) == e.get(key), (e['id'], key)
            assert entity['yaw'] == e['savedYaw'] and entity.get('renderOffset', [0, 0, 0]) == e['renderOffset']
            assert entity['spriteState'] == e['sourceState'] and entity['spriteFrame'] == 0 and entity['matchKind'] == 'exact'
            m = models[entity['modelId']]
            parts = doc['geometryVariants'].get(entity.get('geometryKey'), m['parts'])
            assert json.dumps(parts, sort_keys=True) == json.dumps(m['parts'], sort_keys=True)
            checks.append(dict(variant=spec['variant'], level=spec['level'], id=e['id'], sourceTransformUnchanged=True,
                               renderSupportEqual=True, geometryEqual=True, stateFrameEqual=True))
    assert len(checks) == 27
    proof.update(finalSceneParity=checks, finalSceneInputsSha256=inputs, finalHiddenExclusionsVerified=True,
                 status='two draft assets and all 27 final visible placements verified; source item piles remain overlapping')
    regeneration = json.loads((ROOT/'.codex/shield-drinks-regeneration-check/comparison.json').read_text())
    assert all(sha(ROOT/e['path']) == e['sha256'] for e in regeneration['files'])
    proof['deterministicRegeneration'] = regeneration
    save(proof)
    print(json.dumps(dict(finalPlacements=len(checks), proofSha256=sha(GEN/'shield-drinks-proof.json'))), flush=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--check-assets', action='store_true')
    parser.add_argument('--export-parity')
    args = parser.parse_args()
    if args.check_assets:
        check_assets()
        return
    if args.export_parity:
        export_parity(args.export_parity)
        return
    models, proof = author()
    notes()
    written_proof(models, proof)
    cards(models)
    contexts(models, proof)
    save(proof)


if __name__ == '__main__':
    main()
