"""Sixteen source-owned glass drafts, dedicated assets and bounded map evidence."""
import argparse
from collections import Counter
from copy import deepcopy
import hashlib
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont
import yaml

import build_models as bm
import inventory
import layout
import placement
import scene
import solution_glass_states as sg
import surfaces
from author_loose_headgear import part, serialize
from author_wide_machinery import world_parts
from author_vendor_fans import occupied

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT/'.codex/model-batch-baseline1015'
GEN = ROOT/'Tools/three_d/generated'
PROTOS = ROOT/'Content.CMU/Resources/ThreeD/Prototypes/World'
MODEL = PROTOS/'garrison_solution_glasses.yml'
ART = PROTOS/'garrison_solution_glasses_art.yml'
TEXTURES = ROOT/'Content.CMU/Resources/Textures/CMU14/ThreeD/Surfaces/SolutionGlasses'
NOTE = ROOT/'Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_SOLUTION_GLASSES.md'
REVIEW = GEN/'review/solution-glasses'
IDS = ('DrinkCoffee', 'DrinkCoffeeLiqueurGlass', 'DrinkGrapeSodaGlass', 'DrinkIceGlass',
       'DrinkIcedCoffeeGlass', 'DrinkIrishCoffeeGlass', 'DrinkOrangeJuice', 'DrinkSakeGlass',
       'DrinkVodkaGlass', 'DrinkVodkaMartiniGlass', 'DrinkVodkaRedBool', 'DrinkVodkaTonicGlass',
       'DrinkWhiskeyColaGlass', 'DrinkWhiskeyGlass', 'DrinkWhiskeySodaGlass', 'RMCDrinkGlass')

# Source silhouette landmarks in pixels: axis, body bottom, rim row, bowl floor,
# lower radius, rim radius. Garnish/handle pixels do not widen the vessel itself.
PROFILE = {
    'glass_clear': (16, 23, 9, 21, 3, 4),
    'coffeeglass': (16.5, 24, 10, 22, 4.5, 5.5),
    'coffeeliqueurglass': (15, 26, 10, 18, 1.5, 5),
    'iceglass': (16, 25, 9, 23, 4, 6),
    'icedcoffeeglass': (16, 24, 10, 22, 3, 4),
    'irishcoffeeglass': (15.5, 23, 12, 19, 1.5, 4),
    'orangejuiceglass': (16, 26, 9, 24, 3, 4),
    'sakeglass': (16, 23, 5, 15, 1.5, 4),
    'ginvodkaglass': (16, 22, 10, 20, 4, 6),
    'martiniglass': (16.5, 25, 12, 17, .9, 4),
    'vodkatonicglass': (16, 24, 10, 22, 3, 4),
    'whiskeycolaglass': (16, 26, 13, 24, 4, 6),
    'whiskeyglass': (16, 22, 10, 20, 4, 6),
    'whiskeysodaglass': (16, 26, 13, 24, 4, 6),
    'drink_glass': (16, 24, 10, 22, 3, 4),
}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def source_image(rsi, state):
    return Image.open(bm.resource_file(rsi)/(state+'.png')).convert('RGBA')


class Pool:
    def __init__(self):
        self.entries, self.proof, self.cache = [], [], {}
        used = {r['atlasIndex'] for p in PROTOS.glob('*.yml') if p != ART
                for r in inventory.load_yaml(p.read_text(encoding='utf-8-sig')) or [] if r.get('type') == 'cmu3DSurface'}
        assert not used.intersection(range(3400, 3500)), 'Glass atlas range occupied'

    def crop(self, rsi, state):
        im = source_image(rsi, state); rect = im.getbbox(); crop = im.crop(rect)
        # Source profiles are not supported by glTF PNG consumers. Retain the
        # exact authored RGBA bytes and dimensions without inherited metadata.
        crop.info.clear()
        digest = hashlib.sha256(str(crop.size).encode()+crop.tobytes()).hexdigest()
        if digest not in self.cache:
            index = 3400+len(self.entries); assert index < 3500
            uid = 'CMU3DSolutionGlassSurface'+str(index)
            crop.save(TEXTURES/(uid+'.png'))
            self.entries.append(dict(type='cmu3DSurface', id=uid, atlasIndex=index,
                texture='/Textures/CMU14/ThreeD/Surfaces/SolutionGlasses/'+uid+'.png'))
            self.cache[digest] = uid
        uid = self.cache[digest]
        written = Image.open(TEXTURES/(uid+'.png')).convert('RGBA')
        assert not written.info, (rsi,state,'unexpected generated PNG metadata')
        assert written.tobytes() == crop.tobytes(), (rsi, state)
        reassembled = Image.new('RGBA', im.size); reassembled.paste(written, rect[:2])
        visible_rgba = lambda image: bytes(c for pixel in image.getdata() for c in (pixel if pixel[3] else (0,0,0,0)))
        assert visible_rgba(reassembled) == visible_rgba(im), (rsi, state)
        self.proof.append(dict(rsi=rsi, state=state, rect=list(rect), surface=uid,
            sourceSha256=sha(bm.resource_file(rsi)/(state+'.png')),
            sourceRgbaSha256=hashlib.sha256(im.tobytes()).hexdigest(), writtenCropRgbaExact=True, visibleRgbaReassemblyExact=True))
        return uid, rect


def palette(im, translucent=False):
    colors = Counter(pixel for pixel in im.getdata() if pixel[3] and (not translucent or pixel[3] < 220))
    if not colors:
        colors = Counter(pixel for pixel in im.getdata() if pixel[3])
    return '#'+''.join(f'{v:02X}' for v in colors.most_common(1)[0][0])


def layer_geometry(pool, rsi, state, role):
    name = Path(rsi).stem; cx, bottom, rim, floor, lower, upper = PROFILE[name]
    cx = (cx-16)/32; z0 = (bottom-floor)/32; z1 = (bottom-rim)/32
    low_radius, radius = lower/32, upper/32
    im = source_image(rsi, state); surface, rect = pool.crop(rsi, state)
    x0, y0, x1, y1 = rect
    # Original front RGBA remains separate from inferred curved side construction.
    face_y = -radius-.006-(.003 if role == 'Fill' else .006 if role == 'Overlay' else 0)
    parts = [part(role.lower()+' original source artwork', ((x0+x1-32)/64, face_y, (2*bottom-y0-y1)/64),
        ((x1-x0)/64, .001, (y1-y0)/64), '#FFFFFF', surface=surface, surfaceAxis='XZ')]
    if role == 'Overlay':
        return parts
    if role == 'Fill':
        # Every source level has an actual interior volume, with the free surface
        # at its source fill's upper pixel row. Stepped radii stay inside the bowl.
        height = min(z1-.015, (bottom-y0)/32)
        start = max(.025, z0-.007)
        if name == 'iceglass':
            # Ice remains pieces, following disconnected bright source clusters.
            count = int(state.split('-')[-1])
            for i in range(count):
                x = cx+(-.046 if i % 2 else .035); z = start+.04+i*.028
                parts.append(part('source ice cube '+str(i+1), (x, .008, z), (.04, .045, .04), palette(im)))
            return parts
        if height <= start:
            height = start+.018
        for i in range(3):
            lo = start+(height-start)*i/3; hi = start+(height-start)*(i+1)/3
            frac = max(0, min(1, (lo-z0)/(z1-z0)))
            rad = max(.018, low_radius+(radius-low_radius)*frac-.019)
            parts.append(part('liquid volume tier '+str(i+1), (cx, 0, (lo+hi)/2), (rad, rad, (hi-lo)/2), palette(im), 'CylinderZ'))
        return parts
    sampled = palette(im, True)
    shell = sampled[:7]+f'{min(72,int(sampled[7:9],16)):02X}'
    edges = Counter(p for p in im.getdata() if p[3] >= 200 and p[2] > p[0]+5)
    rimcolor = '#'+''.join(f'{v:02X}' for v in edges.most_common(1)[0][0]) if edges else palette(im)
    # Three open octagonal wall tiers preserve a real hollow mouth. Shell tint
    # is sampled from the source instead of an opaque full-vessel backing.
    for tier in range(3):
        lo = z0+(z1-z0)*tier/3; hi = z0+(z1-z0)*(tier+1)/3
        rad = low_radius+(radius-low_radius)*(tier+.5)/3
        slope = (radius-low_radius)/(z1-z0)
        tilt = -math.degrees(math.atan(slope))
        for side in range(8):
            a = math.tau*side/8
            parts.append(part('hollow wall tier '+str(tier+1)+' facet '+str(side+1),
                (cx+rad*math.cos(a), rad*math.sin(a), (lo+hi)/2),
                (.007, (rad+(radius-low_radius)/6)*math.tan(math.pi/8)+.002,
                 (hi-lo)/2/math.cos(math.radians(tilt))+.001), shell, yaw=math.degrees(a), pitch=tilt))
    for side in range(8):
        a = math.tau*side/8
        parts.append(part('open rim facet '+str(side+1), (cx+radius*math.cos(a), radius*math.sin(a), z1),
            (radius*math.tan(math.pi/8)+.002, .009, .009), rimcolor, yaw=(math.degrees(a)+90)%360))
    parts.append(part('solid bowl bottom', (cx, 0, z0), (low_radius, low_radius, .012), shell, 'CylinderZ'))
    if z0 > .12:
        parts += [part('glass stem', (cx, 0, z0/2), (.016, .016, z0/2), rimcolor, 'CylinderZ'),
                  part('circular glass foot', (cx, 0, .012), (.08, .08, .012), shell, 'CylinderZ')]
    else:
        parts.append(part('heavy glass foot', (cx, 0, z0/2), (low_radius, low_radius, z0/2), shell, 'CylinderZ'))
    if name == 'coffeeglass':
        for label, center, half in [('handle top', (cx+.213, 0, .34), (.051,.017,.015)),
                                    ('handle lower', (cx+.213, 0, .12), (.051,.017,.015)),
                                    ('handle outer', (cx+.252, 0, .23), (.015,.017,.11))]:
            parts.append(part(label, center, half, rimcolor))
    if name == 'orangejuiceglass':
        parts.append(part('striped straw physical core', (.047, .006, .43), (.009,.009,.25), '#E6E6E6'))
    return parts


def build_assets(kinds):
    TEXTURES.mkdir(parents=True, exist_ok=True); REVIEW.mkdir(parents=True, exist_ok=True)
    sg.configure_source(kinds); resolver = inventory.Resolver(kinds['entity'])
    pool = Pool(); catalog = {}; models = []; sources = {}
    for uid in IDS:
        default = inventory.component_map(resolver.resolve(uid)); sources[uid] = default
        rsi = default['Sprite']['sprite']; solution = sg.source_solution(uid)
        vessels = [(rsi, 'icon', 9 if rsi == sg.CLEAR else 5)]
        for content in solution.get('reagents', []):
            reagent = sg.source_reagent(content['ReagentId'])
            if (other := reagent.get('metamorphicSprite')):
                vessels.append((other['sprite'], other['state'], reagent['metamorphicMaxFillLevels']))
        layers = []
        for vrsi, vstate, levels in vessels:
            for role, state in [('Base', vstate), *[('Fill', 'fill-'+str(i)) for i in range(1, levels+1)]]:
                key = (role, vrsi, state)
                if key not in catalog:
                    catalog[key] = dict(role=role, rsi=vrsi, state=state, parts=layer_geometry(pool, vrsi, state, role))
                layers.append(deepcopy(catalog[key]))
        defaults = [sg.pose('Base', rsi, 'icon'), sg.pose('Fill', rsi, 'fill-1', False)]
        if rsi == sg.CLEAR:
            key = ('Overlay', rsi, 'icon-front')
            if key not in catalog:
                catalog[key] = dict(role='Overlay', rsi=rsi, state='icon-front', parts=layer_geometry(pool, rsi, 'icon-front', 'Overlay'))
            layers.append(deepcopy(catalog[key])); defaults.append(sg.pose('Overlay', rsi, 'icon-front'))
        model = dict(type='cmu3DModel', id='CMU3D'+uid, label=resolver.resolve(uid).get('name', uid), status='draft',
            sourcePrototypes=[uid], referencePrototype=uid, referenceRsi=rsi, referenceState='icon', sourceDirections=1,
            useEntityRotation=True, yawOffset=0, groundOffset='0,0', placement='surface',
            solutionAppearance=dict(layers=layers, defaultLayers=defaults),
            description='Source-owned glass layers and physical hollow vessel, rim, foot and liquid. All source fill levels for the original and default metamorphic vessel. Original RGBA crops preserved. Hidden depth, curved walls and liquid volume are inferred; transparency follows the renderer approximation. Unknown live vessels retain source fallback. See SOURCES_SOLUTION_GLASSES.md.')
        pose, error = sg.saved_pose(model, default, {}, scene.normalize_tint)
        assert pose is not None, (uid, error)
        model['solutionAppearance']['defaultLayers'] = pose['layers']
        model['parts'] = sg.compose_parts(model)
        models.append(model)
    ART.write_text('# Original source RGBA crops; provenance in SOURCES_SOLUTION_GLASSES.md.\n'+yaml.safe_dump(pool.entries, sort_keys=False), encoding='utf-8')
    MODEL.write_text('# Source-owned static layer studies; physical glass construction remains draft.\n'+yaml.safe_dump(serialize(models), sort_keys=False, width=110), encoding='utf-8')
    surfaces.load_surfaces.cache_clear()
    # Read the written scalar-vector YAML, then exercise the same shared validator.
    models = [sg.validate(bm.validate_model(m), bm.validate_model) for m in inventory.load_yaml(MODEL.read_text())]
    for model in models:
        sg.validate_source(model, bm.resource_file)
    return models, sources, pool


def cards(models):
    width, height = 1200, 270
    montage = Image.new('RGB', (width, height*len(models)), '#182C38')
    draw = ImageDraw.Draw(montage); font = ImageFont.load_default(size=17)
    for row, model in enumerate(models):
        y = row*height; pose = model['solutionAppearance']['defaultLayers']
        draw.text((12,y+8), model['referencePrototype']+' / '+Path(pose[0]['rsi']).stem+' / '+pose[1]['state']+(' visible' if pose[1]['visible'] else ' hidden'), fill='white', font=font)
        ref = sg.default_source_composite(model, bm.resource_file).resize((192,192), Image.Resampling.NEAREST)
        montage.paste(ref, (0,y+40), ref)
        for j, yaw in enumerate((-math.pi/2, -math.pi/3, math.pi/3, math.pi)):
            rendered = bm.render_model(model, (245,220), yaw, .55, pixels_per_unit=280, screen_origin=(122,194))
            montage.paste(rendered, (207+j*245,y+37))
    montage.save(REVIEW/'source-four-view-montage.png')


def contexts(models, sources):
    library = {m['id']: m for m in json.loads((BASE/'models.json').read_text())['models']}
    library.update({m['id']: m for m in models}); mapping = {m['referencePrototype']: m for m in models}
    records_out = []; hashes = {}; all_defaults = {}; resolver = inventory.Resolver(sg.source_kinds()['entity'])
    for spec in json.loads((BASE/'scenes.json').read_text()):
        path = BASE/spec['file']; doc = json.loads(path.read_text())
        targets = [e for e in doc['instances'] if e['prototype'] in IDS]
        if not targets:
            continue
        map_path, _ = scene.configured_map(ROOT, spec['variant'], spec['level'])
        _, saved = scene.read_map(map_path)
        hidden = scene.hidden_container_entities(saved)
        # Only target ancestor chains need default transforms, not every map item.
        pending = [e['id'] for e in targets]; visited = set()
        while pending:
            uid = pending.pop()
            if uid in visited:
                continue
            visited.add(uid); record = saved[uid]; proto = record['prototype']
            if proto and proto not in all_defaults:
                all_defaults[proto] = inventory.component_map(resolver.resolve(proto))
            transform = {**all_defaults.get(proto,{}).get('Transform',{}), **record['components'].get('Transform',{})}
            parent = transform.get('parent')
            if isinstance(parent,int) and parent:
                pending.append(parent)
        transforms = scene.WorldTransforms(saved,all_defaults)
        hashes[str(map_path.relative_to(ROOT)).replace('\\','/')] = sha(map_path)
        for e in targets:
            model = mapping[e['prototype']]; record = saved[e['id']]
            actual = transforms.resolve(e['id'])
            assert e['id'] not in hidden
            assert e['position'] == [round(actual[0],6),round(actual[1],6),spec['level']]
            assert e['yaw'] == round(actual[2],9)
            pose, error = sg.saved_pose(model, sources[e['prototype']], record['components'], scene.normalize_tint)
            assert pose is not None, (spec, e['id'], error)
            e.update(modelId=model['id'], modelStatus='draft', matchKind='exact', solutionPose=pose)
            e['renderYaw'] = e['yaw']; e['renderOffset'] = [0,0,0]
        sg.scene_variants(targets, library, doc['geometryVariants'])
        placement.resolve_placements(doc['instances'], list(library.values()), doc['geometryVariants'])
        for e in targets:
            nearby = [n for n in doc['instances'] if n['id'] != e['id'] and sum((n['position'][i]-e['position'][i])**2 for i in (0,1)) < .65**2]
            record = dict(variant=spec['variant'], level=spec['level'], id=e['id'], prototype=e['prototype'], modelId=e['modelId'],
                position=e['position'], savedYaw=e['yaw'], renderYaw=e['renderYaw'], renderOffset=e.get('renderOffset'),
                support=e.get('support'), pose=e['solutionPose'], savedComponents=saved[e['id']]['components'],
                sourceTransformVerified=True, sourceVisibilityVerified=True,
                nearbySources=[dict(id=n['id'], prototype=n['prototype'], modeled=bool(n.get('modelId'))) for n in nearby])
            records_out.append(record)
    assert len(records_out) == 41, len(records_out)
    assert Counter(r['prototype'] == 'RMCDrinkGlass' for r in records_out) == {False:25, True:16}
    return records_out, hashes


def physical_proof(models):
    records = []
    for model in models:
        for name,state in sg.portable_states(model).items():
            parts = state['frames'][0]['parts']; layers = state['layers']; rsi = Path(layers[0]['rsi']).stem
            cx,bottom,rim,*_ = PROFILE[rsi]
            point = [(cx-16)/32,0,(bottom-rim)/32+.004]
            assert not any(occupied(p,np.array([point]))[0] for p in parts), (model['id'],name)
            liquid = [p for p in parts if p['label'].startswith('liquid volume')]
            cubes = [p for p in parts if p['label'].startswith('source ice cube')]
            assert bool(liquid or cubes) == layers[1]['visible']
            records.append(dict(modelId=model['id'],state=name,partCount=len(parts),openMouthWitness=point,
                sourceFillVisible=layers[1]['visible'],physicalLiquidParts=len(liquid),physicalIceParts=len(cubes)))
    return records


def write_notes(pool):
    notes = '''# Source-owned drinking-glass drafts

Sixteen exact source prototypes cover 41 visible Redux placements: 15 upstream drink types (25 placements) and RMCDrinkGlass (16). Their dropped-world pivots and saved rotations remain unchanged. Source SolutionContainerVisualsSystem owns selection of the actual Base, Fill and Overlay RSI, state, tint and visibility. Native sampling reads those actual layers and never simulates chemistry or invents a timer. Unknown vessels, layers, shaders or transforms retain original sprite fallback.

The upstream glass_clear source has three layers and nine fill levels. The RMC glass has two layers and five fill levels. The 13 authored metamorphic RSIs replace the Base and Fill resources and hide the clear Overlay; their own 3–5 fill levels are included. Every upstream default contains 30 units and inherits maxVol 50 through Solution's AlwaysPushInheritance. A narrow resolver retains that nested inheritance; treating those drinks as full would be incorrect. Static saved-scene inference accepts empty or single-reagent source solutions only, leaving mixtures/reactions to the live owner.

Models contain a hollow eight-facet vessel wall, separate open rim, glass foot/base, stems where shown, a physical coffee handle, and an interior liquid volume at every authored fill level. Ice is modeled as separate pieces. Exact cropped front source RGBA—including garnish, ice detail, straws and alpha—is retained on a thin surface; side curvature, hidden construction, optical thickness and liquid depth are inferred from tiny one-direction icons. Inferred side walls keep source RGB and cap their optical alpha at 72/255 so opaque outline pixels do not become opaque glass walls. The renderer approximates translucent solids; these remain drafts, not physically accurate glass refraction. The source-four-view montage shows each actual default composite alongside four geometric views. Empty and every fill-level study are available through solutionStates and portable GLB static scenes.

Regeneration: run Tools/three_d/author_solution_glasses.py. --check-assets regenerates the dedicated assets and compares hashes. The generator writes no common export, atlas image or game file. Proof records source PNG hashes, byte-identical written RGBA crops, exact reconstruction of all visible source pixels, all default/synthetic source layer poses and every visible placement with neighboring source IDs. RGB values outside source alpha bounds have no rendered contribution and are excluded from reassembly comparison. Generic support placement uses the existing table/counter geometry. Existing source piles are retained. --export-parity MANIFEST checks the final parent-exported scene files against every proven transform, pose, support and composed part.

Source owner: Content.Client/Chemistry/Visualizers/SolutionContainerVisualsSystem.cs; Content.Shared/Rounding/ContentHelpers.cs; Content.Shared/Chemistry/Components/SolutionComponent.cs. No source gameplay files are modified.

## Source artwork and licenses

'''
    for rsi in sorted({r['rsi'] for r in pool.proof}):
        meta = json.loads((bm.resource_file(rsi)/'meta.json').read_text())
        notes += f"- `{rsi}` — {meta.get('license')}: {meta.get('copyright')}\n"
    NOTE.write_text(notes, encoding='utf-8')


def asset_hashes():
    return {p.relative_to(ROOT).as_posix():sha(p) for p in [MODEL, ART, NOTE, *sorted(TEXTURES.glob('*.png'))]}


def export_parity(manifest):
    proof = json.loads((GEN/'solution-glasses-proof.json').read_text())
    models = {m['id']:m for m in bm.load_models(MODEL)}
    expected = {(e['variant'],e['level'],e['id']):e for e in proof['placements']}
    checks = []; inputs = {}
    for path,digest in proof['inputMapsSha256'].items():
        assert sha(ROOT/path) == digest, 'Source map changed since pose audit: '+path
    for spec in json.loads((GEN/manifest).read_text()):
        path = GEN/spec['file']; doc = json.loads(path.read_text()); inputs[spec['file']] = sha(path)
        for entity in doc['instances']:
            if entity['prototype'] not in IDS:
                continue
            key = spec['variant'],spec['level'],entity['id']; original = expected[key]
            assert entity['matchKind'] == 'exact'
            for field in ('position','modelId','renderYaw','support'):
                assert entity.get(field) == original.get(field), (key,field,entity.get(field),original.get(field))
            assert entity['yaw'] == original['savedYaw']
            assert entity.get('renderOffset',[0,0,0]) == (original['renderOffset'] or [0,0,0])
            assert entity['solutionPose'] == original['pose'], (key,'pose')
            model = models[entity['modelId']]
            parts = doc['geometryVariants'].get(entity.get('geometryKey'),model['parts'])
            composed = sg.compose_parts(model,original['pose']['layers'],original['pose']['spriteTint'])
            assert json.dumps(parts,sort_keys=True) == json.dumps(composed,sort_keys=True), (key,'geometry')
            checks.append(dict(variant=spec['variant'],level=spec['level'],id=entity['id'],prototype=entity['prototype'],
                sourceTransformEqual=True,sourcePoseEqual=True,geometryCompositionEqual=True,supportEqual=True))
    assert len(checks) == 41 and len({(e['variant'],e['level'],e['id']) for e in checks}) == 41
    report = dict(passed=True,modelCount=16,placementCount=41,checks=checks,inputScenesSha256=inputs,
        allSourceTransformsRecomputed=True,allSourceVisibilityVerified=True)
    (GEN/'solution-glasses-final-export-audit.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    proof.update(status='sixteen draft assets and all 41 final visible source poses verified',finalSceneParity=report)
    (GEN/'solution-glasses-proof.json').write_text(json.dumps(proof,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(dict(passed=True,placements=41)))


def main():
    parser = argparse.ArgumentParser(); parser.add_argument('--check-assets', action='store_true'); parser.add_argument('--export-parity'); args = parser.parse_args()
    if args.export_parity:
        export_parity(args.export_parity); return
    before = asset_hashes() if args.check_assets else None
    kinds, issues, _ = inventory.load_prototypes(ROOT); assert not issues
    models, sources, pool = build_assets(kinds); write_notes(pool)
    if args.check_assets:
        after = asset_hashes(); assert before == after, 'Glass regeneration changed written assets'
        proof_path = GEN/'solution-glasses-proof.json'; proof = json.loads(proof_path.read_text())
        proof['deterministicRegeneration'] = dict(byteIdentical=True,files=len(after),writtenAssetsSha256=after)
        proof_path.write_text(json.dumps(proof,indent=2)+'\n',encoding='utf-8')
        print(json.dumps(dict(deterministic=True, files=len(after)))); return
    cards(models); context, map_hashes = contexts(models, sources)
    poses = {m['id']:{k:dict(layers=v['layers'], parts=len(v['frames'][0]['parts'])) for k,v in sg.portable_states(m).items()} for m in models}
    proof = dict(status='dedicated draft assets and all visible source poses checked; parent export pending',
        modelCount=len(models), placementCount=len(context), atlasIndices=[s['atlasIndex'] for s in pool.entries],
        sourceLayers=pool.proof, portablePoses=poses, physicalPoses=physical_proof(models), placements=context, inputMapsSha256=map_hashes,
        writtenAssetsSha256=asset_hashes(), generatorSha256=sha(Path(__file__)),
        limitations=['Curved walls, hidden construction and liquid depth inferred.', 'Renderer transparency approximation; no refraction.',
                      'Static export accepts source single reagents only; unknown runtime vessel/layer states retain fallback.'])
    (GEN/'solution-glasses-proof.json').write_text(json.dumps(proof, indent=2)+'\n', encoding='utf-8')
    print(json.dumps(dict(models=len(models), textures=len(pool.entries), placements=len(context), states=sum(len(v) for v in poses.values()))))


if __name__ == '__main__':
    main()
