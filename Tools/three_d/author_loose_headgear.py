"""Five dropped-headgear drafts. Dedicated assets only; no global export or runtime."""
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
from author_wide_machinery import world_parts, contacts
from author_vendor_fans import contact_witnesses, occupied

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / '.codex/model-batch-baseline994'
GEN = ROOT / 'Tools/three_d/generated'
PROTOS = ROOT / 'Content.CMU/Resources/Prototypes/CMU14/ThreeD'
MODEL = PROTOS / 'garrison_loose_headgear.yml'
ART = PROTOS / 'garrison_loose_headgear_art.yml'
TEXTURES = ROOT / 'Content.CMU/Resources/Textures/CMU14/ThreeD/Surfaces/LooseHeadgear'
REVIEW = GEN / 'review/loose-headgear'
NOTE = ROOT / 'Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_LOOSE_HEADGEAR.md'
SPECS = [
    ('RMCHardhatOrange', 'CMU3DLooseHardhatOrange', 'Orange mining hardhat', 'Head/Helmets/Hardhats/orange', '#B8672D', '#6B2321'),
    ('RMCHardHat', 'CMU3DLooseHardhatYellow', 'Yellow mining hardhat', 'Head/Helmets/Hardhats/yellow', '#D3AD39', '#804525'),
    ('RMCHardhatBlue', 'CMU3DLooseHardhatBlue', 'Blue mining hardhat', 'Head/Helmets/Hardhats/blue', '#3979D3', '#255180'),
    ('RMCArmorHelmetM10CMB', 'CMU3DLooseCMBHelmet', 'CMB M10 helmet', 'Head/Helmets/CMB/cmb_helmet', '#282828', '#121212'),
    ('RMCVisorSWAT', 'CMU3DLooseSWATVisor', 'Detached SWAT visor', 'Mask/swat_shield', '#282828', '#121212'),
]
IDS = [s[0] for s in SPECS]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def source_path(spec):
    return ROOT / 'Resources/Textures/_RMC14/Objects/Clothing' / (spec[3] + '.rsi')


def source(spec):
    return Image.open(source_path(spec) / 'icon.png').convert('RGBA')


def part(label, center, half, color, shape='Box', **extras):
    result = dict(label=label, min=[a-b for a, b in zip(center, half)], max=[a+b for a, b in zip(center, half)], color=color)
    if shape != 'Box':
        result['shape'] = shape
    result.update(extras)
    return result


def serialize(value):
    if isinstance(value, list):
        return [serialize(v) for v in value]
    if isinstance(value, dict):
        return {k: ', '.join(f'{a:.9f}' for a in v) if k in ('min', 'max') else serialize(v) for k, v in value.items()}
    return value


def author():
    TEXTURES.mkdir(parents=True, exist_ok=True)
    REVIEW.mkdir(parents=True, exist_ok=True)
    used = {r['atlasIndex'] for p in PROTOS.glob('*.yml') if p.name != ART.name
            for r in yaml.load(p.read_text(encoding='utf-8-sig'), Loader=yaml.CSafeLoader) or [] if r.get('type') == 'cmu3DSurface'}
    assert not used.intersection(range(2800, 2900)), 'Reserved headgear atlas range already occupied'
    models, art, evidence = [], [], []
    for spec in SPECS:
        proto, mid, label, _, main, dark = spec
        im = source(spec)
        meta = json.loads((source_path(spec) / 'meta.json').read_text())
        state = next(s for s in meta['states'] if s['name'] == 'icon')
        assert state.get('directions', 1) == 1 and state.get('delays', [[1]]) == [[1]] and im.size == (32, 32)
        patches = []

        def texture(rect, name):
            uid = 'CMU3DLooseHeadgearSurface' + str(2800 + len(art))
            patch = im.crop(rect)
            patch.save(TEXTURES / (uid + '.png'))
            art.append(dict(type='cmu3DSurface', id=uid, atlasIndex=2800+len(art),
                            texture='/Textures/CMU14/ThreeD/Surfaces/LooseHeadgear/' + uid + '.png'))
            patches.append(dict(surface=uid, name=name, rect=list(rect), rgbaSha256=hashlib.sha256(patch.tobytes()).hexdigest()))
            return uid

        if 'Hardhat' in mid:
            # A curved crown and separate skirts leave the lower rear/side opening;
            # this does not pretend to reconstruct a complete unseen inner shell.
            skirt = {'CMU3DLooseHardhatOrange': '#8C4230', 'CMU3DLooseHardhatYellow': '#A77235', 'CMU3DLooseHardhatBlue': '#3557A7'}[mid]
            parts = [
                part('curved protective crown', (0, .014, .155), (.155, .140, .09), main, 'Ellipsoid'),
                part('left shell skirt', (-.123, -.004, .084), (.032, .119, .073), skirt, 'Ellipsoid'),
                part('right shell skirt', (.123, -.004, .084), (.032, .119, .073), skirt, 'Ellipsoid'),
                part('front shell rim joining crown to brim', (0, -.086, .071), (.145, .034, .053), skirt, 'Ellipsoid'),
                part('broad forward brim', (0, -.094, .027), (.1875, .112, .020), dark, 'Ellipsoid'),
                part('original off lamp solid housing', (0, -.146, .151), (.0625, .023, .0625), '#FFFFFF',
                     surface=texture((14, 11, 18, 15), 'unlit headlamp and bezel'), surfaceAxis='XZ'),
            ]
        elif mid == 'CMU3DLooseCMBHelmet':
            parts = [
                part('rounded armored crown', (-.015625, .016, .204), (.143, .132, .116), main, 'Ellipsoid'),
                part('left cheek shell', (-.135, -.005, .112), (.042, .119, .090), dark, 'Ellipsoid'),
                part('right cheek shell', (.104, -.005, .112), (.042, .119, .090), dark, 'Ellipsoid'),
                part('rear neck shell', (-.015625, .128, .137), (.134, .028, .09), main, 'Ellipsoid'),
                part('reinforced brow plate', (-.015625, -.126, .250), (.115, .026, .033), '#3B3B3B'),
                part('original crown and brow highlights', (-.015625, -.154, .254), (.109375, .002, .046875), '#FFFFFF',
                     surface=texture((12, 11, 19, 14), 'original reinforced brow'), surfaceAxis='XZ'),
                part('lower chin rim', (-.015625, -.069, .026), (.103, .068, .019), '#282828', 'Ellipsoid'),
                part('original lower chin highlights', (-.015625, -.140, .041), (.109375, .002, .041), '#FFFFFF',
                     surface=texture((12, 18, 19, 21), 'original chin trim'), surfaceAxis='XZ'),
                part('left chin attachment', (-.109, -.095, .063), (.018, .035, .035), '#121212'),
                part('right chin attachment', (.078, -.095, .063), (.018, .035, .035), '#121212'),
            ]
        else:
            # Three exact pixel partitions form a shallow curved physical shield.
            # Texture alpha, including 191/255 amber glass, is never made opaque.
            a = math.radians(25)
            half = 2.5 / 32
            parts = [
                part('curved left shield and attachment', (-.125-half*math.cos(a), -.04+half*math.sin(a), .265625),
                     (half, .006, .265625), '#FFFFFF', yaw=-25,
                     surface=texture((7, 7, 12, 24), 'left shield'), surfaceAxis='XZ'),
                part('central amber shield', (.015625, -.04, .265625), (.140625, .006, .265625), '#FFFFFF',
                     surface=texture((12, 7, 21, 24), 'central shield'), surfaceAxis='XZ'),
                part('curved right shield and attachment', (.15625+half*math.cos(a), -.04+half*math.sin(a), .265625),
                     (half, .006, .265625), '#FFFFFF', yaw=25,
                     surface=texture((21, 7, 26, 24), 'right shield'), surfaceAxis='XZ'),
                part('left attachment pin', (-.245, .021, .471), (.019, .022, .022), '#282828', 'CylinderX'),
                part('right attachment pin', (.276, .021, .471), (.019, .022, .022), '#282828', 'CylinderX'),
            ]
        model = dict(type='cmu3DModel', id=mid, label=label, status='draft', sourcePrototypes=[proto], referencePrototype=proto,
                     referenceRsi='_RMC14/Objects/Clothing/' + spec[3] + '.rsi', referenceState='icon', sourceDirections=1,
                     sourceSpriteRotates=True, sourceSpriteOffset='0,0', useEntityRotation=True, yawOffset=0,
                     groundOffset='0,0', placement='surface', parts=parts,
                     description='Dropped world item only. Source-colored curved shell or separated shield panels with exact original detail pixels. Bare static icon only; source layer/state/stain guards retain unsupported live appearances as sprites. Hidden depth, height and curvature are inferred. Worn character art is unchanged. See SOURCES_LOOSE_HEADGEAR.md.',
                     spriteStates={'icon': dict(frames=[dict(parts=copy.deepcopy(parts))], delays=[1])})
        models.append(model)
        evidence.append(dict(prototype=proto, modelId=mid, parts=len(parts), sourceDirections=1, sourceFrames=1,
                             sourceState='icon', sourceSha256=sha(source_path(spec)/'icon.png'),
                             sourceMetaSha256=sha(source_path(spec)/'meta.json'), sourceBounds=list(im.getbbox()),
                             license=meta['license'], copyright=meta['copyright'], patches=patches))
    MODEL.write_text('# Dropped world headgear only. All geometry remains draft.\n' + yaml.safe_dump(serialize(models), sort_keys=False, width=110), encoding='utf-8')
    ART.write_text('# Original CC-BY-SA-3.0 source crops; see SOURCES_LOOSE_HEADGEAR.md.\n' + yaml.safe_dump(art, sort_keys=False), encoding='utf-8')
    surfaces.load_surfaces.cache_clear()
    loaded = bm.load_models(MODEL)
    proof = dict(status='draft assets written; final parent export pending', models=evidence,
                 atlasIndices=[a['atlasIndex'] for a in art], textureCount=len(art))
    print(json.dumps(dict(models=len(loaded), parts=[len(m['parts']) for m in loaded], textures=len(art))), flush=True)
    return loaded, proof


def written_proof(models, proof):
    raw = yaml.safe_load(MODEL.read_text())
    assert all(isinstance(p[k], str) for m in raw for p in m['parts'] for k in ('min', 'max'))
    library = {m['id']: m for m in models}
    for spec, e in zip(SPECS, proof['models']):
        m = library[e['modelId']]
        im = source(spec)
        assert m['parts'] == m['spriteStates']['icon']['frames'][0]['parts']
        palette = {tuple(p[:3]) for p in np.array(im)[np.array(im)[:, :, 3] > 0]}
        assert all(tuple(bytes.fromhex(p['color'][1:7])) in palette for p in m['parts'] if not p.get('surface'))
        projected = Image.new('RGBA', (32, 32))
        for patch in e['patches']:
            png = Image.open(TEXTURES / (patch['surface']+'.png')).convert('RGBA')
            expected = im.crop(patch['rect'])
            assert png.tobytes() == expected.tobytes()
            assert hashlib.sha256(png.tobytes()).hexdigest() == patch['rgbaSha256']
            # Paste without alpha multiplication, including the 191-alpha glass.
            projected.paste(png, patch['rect'][:2])
        if m['id'] == 'CMU3DLooseSWATVisor':
            assert projected.tobytes() == im.tobytes()
            e.update(fullSourcePartitionExact=True, glassAlpha191Preserved=True)
        e.update(writtenDetailPixelsExact=True, writtenSolidPaletteExact=True, staticPoseEqualsDefault=True)
    proof['writtenScalarVectorsVerified'] = True
    joins = []
    for m in models:
        ps = {p['label']: p for p in m['parts']}
        pairs = []
        if 'Hardhat' in m['id']:
            pairs = [('curved protective crown', 'left shell skirt'), ('curved protective crown', 'right shell skirt'),
                     ('curved protective crown', 'front shell rim joining crown to brim'),
                     ('front shell rim joining crown to brim', 'broad forward brim'),
                     ('left shell skirt', 'broad forward brim'), ('right shell skirt', 'broad forward brim')]
            assert not any(occupied(p, np.array([[0, .015, .045]]))[0] for p in m['parts'])
        elif m['id'] == 'CMU3DLooseCMBHelmet':
            pairs = [('left cheek shell', 'left chin attachment'), ('left chin attachment', 'lower chin rim'),
                     ('right cheek shell', 'right chin attachment'), ('right chin attachment', 'lower chin rim')]
        for first, second in pairs:
            hits, _ = contacts([ps[first]], [ps[second]])
            contact_witnesses(hits, [ps[first]], [ps[second]])
            assert any('solidWitness' in h for h in hits), (m['id'], first, second)
            joins.append(dict(modelId=m['id'], fromPart=first, toPart=second,
                              witness=next(h['solidWitness'] for h in hits if 'solidWitness' in h)))
    proof['physicalJoinWitnesses'] = joins
    proof['hardhatUndersideOpeningSample'] = [0, .015, .045]


def cards(models):
    font = ImageFont.load_default(size=17)
    montage = Image.new('RGB', (1470, 5*320), '#182C38')
    library = {m['id']: m for m in models}
    for row, spec in enumerate(SPECS):
        m = library[spec[1]]
        card = Image.new('RGB', (1470, 320), '#182C38')
        d = ImageDraw.Draw(card)
        d.text((12, 8), m['label'] + f' / static icon / {len(m["parts"])} parts / inferred draft shell', fill='white', font=font)
        im = source(spec).resize((224, 224), Image.Resampling.NEAREST)
        card.paste(im, (8, 62), im)
        for j, yaw in enumerate((-math.pi/2, -math.pi/3, math.pi/3, math.pi)):
            panel = bm.render_model(m, (300, 245), yaw, .38, pixels_per_unit=420, screen_origin=(150, 198))
            card.paste(panel, (250+j*305, 55))
        card.save(REVIEW / (m['id']+'.png'))
        montage.paste(card, (0, row*320))
    montage.save(REVIEW / 'source-four-view-montage.png')


def context(models, proof):
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
    evidence, groups, guards, scenes = [], [], [], {}
    for proto, m in mapping.items():
        dc = default(proto)
        assert sprite_states.saved_pose(m, dc, {}, scene.normalize_tint) == ('icon', None)
        for label, override in [('stained item', {'CMUItemStain': {'color': '#FF0000'}}),
                                ('changed scale', {'Sprite': {'scale': '2,1'}}),
                                ('unexpected state', {'Sprite': {'layers': [{'state': 'equipped-HELMET'}]}}),
                                ('extra visible layer', {'Sprite': {'layers': [{'state': 'icon'}, {'state': 'icon_on'}]}})]:
            state, reason = sprite_states.saved_pose(m, dc, override, scene.normalize_tint)
            assert state is None, (proto, label)
            guards.append(dict(prototype=proto, case=label, fallbackReason=reason))
    for spec in json.loads((BASE/'scenes.json').read_text()):
        path = BASE/spec['file']
        doc = json.loads(path.read_text())
        targets = [e for e in doc['instances'] if e['prototype'] in IDS]
        if not targets:
            continue
        scenes[spec['file']] = sha(path)
        _, records = scene.read_map(ROOT/doc['map']['path'])
        for raw in records.values():
            default(raw['prototype'])
        hidden = scene.hidden_container_entities(records)
        transforms = scene.WorldTransforms(records, defaults)
        source_candidates = [r for r in records.values() if r['prototype'] in IDS]
        assert len(source_candidates) == len(targets)
        for e in targets:
            raw = records[e['id']]
            m = mapping[e['prototype']]
            assert e['id'] not in hidden, (e['id'], 'hidden container')
            assert sprite_states.saved_pose(m, default(e['prototype']), raw['components'], scene.normalize_tint) == ('icon', None)
            e.update(modelId=m['id'], matchKind='exact', renderYaw=e['yaw'])
            sprite_states.resolve_scene_pose(e, m, default(e['prototype']), raw['components'], scene.normalize_tint)
        layout.resolve_layout(targets, list(library.values()), records, defaults, transforms)
        sprite_states.scene_variants(targets, library, doc['geometryVariants'])
        placement.resolve_placements(doc['instances'], list(library.values()), doc['geometryVariants'])
        clusters = defaultdict(list)
        for e in targets:
            assert e['renderYaw'] == e['yaw'] == 0
            nearby = [n for n in doc['instances'] if n['id'] != e['id'] and sum((a-b)**2 for a, b in zip(n['position'][:2], e['position'][:2])) < .6**2]
            evidence.append(dict(variant=spec['variant'], level=spec['level'], id=e['id'], prototype=e['prototype'], modelId=e['modelId'],
                                 position=e['position'], savedYaw=e['yaw'], renderYaw=e['renderYaw'], renderOffset=e.get('renderOffset', [0, 0, 0]),
                                 support=e.get('support'), sourceState=e['spriteState'], sourceFrame=e['spriteFrame'], sourceVisible=True,
                                 onlyTransformSaved=records[e['id']]['componentTypes'] == ['Transform'],
                                 savedComponentTypes=records[e['id']]['componentTypes'], savedComponents=records[e['id']]['components'],
                                 nearbySources=[dict(id=n['id'], prototype=n['prototype'], modeled=bool(n.get('modelId'))) for n in nearby]))
            clusters[(tuple(e['position']), e['prototype'])].append(e['id'])
        for (pos, proto), uids in clusters.items():
            if len(uids) > 1:
                groups.append(dict(variant=spec['variant'], level=spec['level'], prototype=proto, position=pos, ids=uids, count=len(uids)))
        # Four representative contexts, with all saved duplicate parts retained.
        chosen = {'redux': {0: [16678, 16679, 16680], 1: [2735]}, 'classic': {0: [8848]}}.get(spec['variant'], {}).get(spec['level'], [])
        for uid in chosen:
            e = next(t for t in targets if t['id'] == uid)
            assembled, unknown, drawn, aabb_contacts = [], [], [], []
            own = world_parts(library[e['modelId']]['parts'], [0, 0, 0], e['renderYaw'], e.get('renderOffset', [0, 0, 0]))
            for n in doc['instances']:
                if sum((a-b)**2 for a, b in zip(n['position'][:2], e['position'][:2])) > .85**2:
                    continue
                nm = library.get(n.get('modelId'))
                if nm is None:
                    unknown.append(dict(id=n['id'], prototype=n['prototype']))
                    continue
                offsetpos = [n['position'][0]-e['position'][0], n['position'][1]-e['position'][1], 0]
                nparts = world_parts(doc['geometryVariants'].get(n.get('geometryKey'), nm['parts']), offsetpos,
                                    n.get('renderYaw', n['yaw']), n.get('renderOffset', [0, 0, 0]))
                assembled.extend(nparts)
                drawn.append(n['id'])
                if n['id'] != uid:
                    hits, _ = contacts(own, nparts)
                    if hits:
                        aabb_contacts.append(dict(id=n['id'], prototype=n['prototype'], partPairs=len(hits), conservativeOnly=True))
            assembled.append(part('flat reference floor', (0, 0, -.035), (.85, .85, .025), '#53504A'))
            card = Image.new('RGB', (1200, 580), '#182C38')
            d = ImageDraw.Draw(card)
            d.text((12, 12), f'{spec["variant"]} {spec["level"]:+d}, UID {uid}: source pivots and all duplicates retained', fill='white', font=ImageFont.load_default(size=17))
            for j, yaw in enumerate((-math.pi/3, math.pi/2+.2)):
                panel = bm.render_model(dict(parts=assembled), (590, 455), yaw, .7, pixels_per_unit=300, screen_origin=(295, 350))
                card.paste(panel, (j*600, 55))
            d.text((12, 538), f'{len(unknown)} unknown nearby source objects omitted; flat reference floor. No full intersection proof.', fill='#BBD4DF', font=ImageFont.load_default(size=16))
            card.save(REVIEW/f'context-{spec["variant"]}-{spec["level"]}-{uid}.png')
            proof.setdefault('representativeContexts', []).append(dict(variant=spec['variant'], level=spec['level'], id=uid,
                drawnIds=drawn, unknownNeighbors=unknown, conservativeContacts=aabb_contacts))
    assert len(evidence) == 71 and Counter(e['variant'] for e in evidence) == {'redux': 50, 'classic': 21}
    proof.update(contexts=evidence, placementCounts=dict(Counter(e['variant'] for e in evidence)),
                 perPrototypeCounts={proto: dict(Counter(e['variant'] for e in evidence if e['prototype'] == proto)) for proto in IDS},
                 exactCoincidentGroups=groups, appearanceFallbackChecks=guards, inputScenesSha256=scenes,
                 sourceDefinitions={proto: dict(file=resolver.resolve(proto)['_source'], components=default(proto)) for proto in IDS})
    print(json.dumps(dict(placements=len(evidence), coincidentGroups=len(groups), guards=len(guards))), flush=True)


def notes():
    NOTE.write_text('''# Dropped headgear drafts

Five exact dropped-item prototypes: RMCHardhatOrange, RMCHardHat (yellow), RMCHardhatBlue, RMCArmorHelmetM10CMB and RMCVisorSWAT. They cover 50 Redux plus 21 classic saved world entities: 32 orange, 1 yellow, 2 blue, 18 CMB helmets and 18 SWAT visors. All have yaw zero, visible source sprites, and no hidden container membership. Seventy have only saved Transform data; classic CMB helmet 8848 additionally stores its CycleableVisor action entity 8849, an empty storage container and action-container membership. This does not add a world sprite layer or a saved alternate visor appearance. Character and mob equipment art is unchanged.

Each world icon is a single static 32 × 32 RSI direction. Sources use ordinary rotating Sprite rendering, noRot=false, no snapCardinals, unit scale, zero offset and white tint. The existing sourceSpriteRotates adapter matches one actual visible icon layer and preserves source yaw. Model ground offset is zero and surface placement uses the existing exact support lookup. Saved positions, collisions, item handling and UI are not edited. No animation is invented.

Hardhats have a real curved crown, overlapping side skirts and front band, forward brim and projecting off-lamp housing. The original lamp/bezel pixels are reproduced exactly. Crown and skirts use the exact source palette; darker midtones distinguish the skirt bulk from the dark brim edge. The CMB model has an armored curved crown, separate cheeks and neck cover, a reinforced brow with original highlights and a lower chin rim physically joined to the cheek shells by two dark attachments, leaving the front gap open. These are small physical objects, not whole-item sprite cards. Full unseen inner shells, depths, heights and rear geometry are inferred; this is not exact 3D reconstruction. Hardhat height is .245 tiles and the CMB crown reaches .320 tiles. Their front detail crops are exact, but the entire original icon is not claimed to be reproduced by the inferred shell.

The naturally thin SWAT shield is divided into three original RGBA partitions with two angled sides and attachment pins. The partitions reconstruct the entire source icon without lost texels or alpha changes; the amber glass retains source alpha 191/255. Physical curvature (25 degrees per side), .012 tile panel thickness and hinge depth are inferred. The renderer's existing dithered alpha policy is used, without claiming optical/refraction fidelity. Source alpha gaps are left open on the panels; the added pins are limited inferred hardware.

Hardhat ToggleableVisuals owns the second icon_on layer through the mapped light layer. OFF has one visible icon; ON exposes both layers and must retain the original sprite fallback. It is not a second authored single-layer state. HandheldLight, ItemTogglePointLight, Battery and PointLight still own original light behavior. No powered 3D lamp appearance is claimed. HelmetAccessoryHolder, IntegratedVisors, CycleableVisor and SquadArmor provide worn equipment layers through GetEquipmentVisuals, while FoldableClothing changes equipped/held prefixes. The SWAT accessory's helmet and helmet-down states are attachment views, not world-icon animations. Those states are outside this dropped-icon batch. CMUItemStain can add world shader/layers: non-null saved stains and unsupported live layers/shaders retain sprite fallback. Changed source states, textures or layer transforms also require fallback.

Saved context contains deliberate or historical duplicate world piles. Redux has ten orange hats at one pivot and fourteen at another very near two additional original hats; Redux +1 has ten CMB helmets and ten SWAT visors co-centered. They remain separate entities at their original pivots, with no deduplication, artificial stacking or UID-specific move. Consequently those 3D objects intersect, and the surrounding unmapped clutter is not a complete reconstructed assembly. Classic also contains closely overlapping individual helmets and visors. The proof lists every source placement, support result, near neighbor, exact duplicate group and representative conservative contacts. No claim is made that all item piles fit or that conservative AABB overlap alone proves an opaque intersection. Review floors are labeled flat reference underlays.

Source attribution: all five RSI folders retain CC-BY-SA-3.0 metadata. Hardhat artwork is from cmss13 hats.dmi and head_0.dmi at commit eb675ffc9c92f3453d9451130b81ee77dc011d0c; CMB helmet artwork is from cmss13 cm_hats.dmi and head_1.dmi at commit 06d35efb20e830eacc57d3c77fea047dadbcdee3. SWAT artwork is from glasses.dmi at commit f2b3774f6ca9173e76e7783d88e3c2f765cd385f and eyes.dmi at commit 0c23c26bc85b7ab45fdb212c84807f0215c0a53e; in-hand art attribution includes Errant. Exact upstream URLs and copyright strings from each original meta.json are retained in generated/loose-headgear-proof.json. Derived PNGs preserve this attribution/license.

Reproduce with Tools/three_d/author_loose_headgear.py. --check-assets regenerates an isolated dedicated copy and compares byte hashes; --export-parity MANIFEST.json checks final parent source/render/support exports. The script performs no global export, game build or process operation. All five models remain drafts; final library/native admission is checked by the parent batch.
''', encoding='utf-8')


def save(proof):
    models = {m['referencePrototype']: m for m in bm.load_models(MODEL)}
    activated = []
    for proto, definition in proof.get('sourceDefinitions', {}).items():
        dc = definition['components']
        if 'ToggleableVisuals' not in dc:
            continue
        for component in ('HandheldLight', 'ItemToggle'):
            state, reason = sprite_states.saved_pose(models[proto], dc, {component: {'activated': True}}, scene.normalize_tint)
            assert state is None
            activated.append(dict(prototype=proto, owner=component, activated=True, fallbackReason=reason))
    proof['activatedOwnerFallbackChecks'] = activated
    proof['writtenAssetSha256'] = {p.relative_to(ROOT).as_posix(): sha(p) for p in [MODEL, ART, NOTE,
        *[TEXTURES/(e['id']+'.png') for e in yaml.safe_load(ART.read_text())]]}
    proof['generatorSha256'] = sha(Path(__file__))
    (GEN/'loose-headgear-proof.json').write_text(json.dumps(proof, indent=2)+'\n')


def check_assets():
    global MODEL, ART, TEXTURES, REVIEW
    originals = [MODEL, ART, *[TEXTURES/(e['id']+'.png') for e in yaml.safe_load(ART.read_text())]]
    stage = ROOT/'.codex/loose-headgear-regeneration-check'
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
    proof = json.loads((GEN/'loose-headgear-proof.json').read_text())
    expected = {(e['variant'], e['level'], e['id']): e for e in proof['contexts']}
    models = {m['id']: m for m in bm.load_models(MODEL)}
    checks, inputs = [], {}
    for spec in json.loads((GEN/manifest).read_text()):
        path = GEN/spec['file']
        doc = json.loads(path.read_text())
        inputs[spec['file']] = sha(path)
        for entity in doc['instances']:
            if entity['prototype'] not in IDS:
                continue
            e = expected[(spec['variant'], spec['level'], entity['id'])]
            for key in ('position', 'modelId', 'renderYaw', 'support'):
                assert entity.get(key) == e.get(key), (entity['id'], key, entity.get(key), e.get(key))
            assert entity['yaw'] == e['savedYaw'] and entity.get('renderOffset', [0, 0, 0]) == e['renderOffset']
            assert entity['spriteState'] == 'icon' and entity['spriteFrame'] == 0 and entity['matchKind'] == 'exact'
            m = models[entity['modelId']]
            parts = doc['geometryVariants'].get(entity.get('geometryKey'), m['parts'])
            assert json.dumps(parts, sort_keys=True) == json.dumps(m['parts'], sort_keys=True)
            checks.append(dict(variant=spec['variant'], level=spec['level'], id=e['id'], sourceTransformUnchanged=True,
                               renderYawOffsetSupportEqual=True, geometryEqual=True, stateFrameEqual=True))
    assert len(checks) == 71
    proof.update(finalSceneParity=checks, finalSceneInputsSha256=inputs, status='five draft assets and all 71 final saved placements verified; item-pile fitting limitations retained')
    regeneration = json.loads((ROOT/'.codex/loose-headgear-regeneration-check/comparison.json').read_text())
    assert all(sha(ROOT/e['path']) == e['sha256'] for e in regeneration['files'])
    proof['deterministicRegeneration'] = regeneration
    save(proof)
    print(json.dumps(dict(finalPlacements=len(checks), proofSha256=sha(GEN/'loose-headgear-proof.json'))), flush=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--check-assets', action='store_true')
    parser.add_argument('--export-parity')
    parser.add_argument('--reviews-only', action='store_true')
    parser.add_argument('--finalize-proof', action='store_true')
    args = parser.parse_args()
    if args.check_assets:
        check_assets()
        return
    if args.export_parity:
        export_parity(args.export_parity)
        return
    if args.reviews_only or args.finalize_proof:
        models = bm.load_models(MODEL)
        proof = json.loads((GEN/'loose-headgear-proof.json').read_text())
    else:
        models, proof = author()
    if not args.finalize_proof:
        notes()
    written_proof(models, proof)
    if not args.finalize_proof:
        cards(models)
        context(models, proof)
    save(proof)


if __name__ == '__main__':
    main()
