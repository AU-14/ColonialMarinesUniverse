"""Source-pixel wall posters with silhouette backing and saved-wall context proof.

This authors only this batch. It never rebuilds the global library or launches the game.
The source has one screen-upright frame; saved yaw determines the physical wall face.
"""
from __future__ import annotations

from collections import Counter
import hashlib
import json
import math
from pathlib import Path
import re

from PIL import Image, ImageDraw, ImageFont
import yaml

import build_models
import inventory as source_inventory
import layout
import placement
import scene
from author_wide_machinery import world_parts, box_bounds

ROOT = Path(__file__).resolve().parents[2]
GENERATED = ROOT / 'Tools/three_d/generated'
REVIEW = GENERATED / 'review/wall-posters'
PROTOTYPES = ROOT / 'Content.CMU/Resources/Prototypes/CMU14/ThreeD'
TEXTURES = ROOT / 'Content.CMU/Resources/Textures/CMU14/ThreeD/Surfaces'
MODEL_FILE = PROTOTYPES / 'garrison_wall_posters.yml'
ART_FILE = PROTOTYPES / 'garrison_wall_posters_art.yml'
NOTES = ROOT / 'Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_WALL_POSTERS.md'
SELECTED = (
    'CMPostEat', 'CMPosterBepis', 'CMPosterBeth', 'CMPosterBobda',
    'CMPosterDavenportGin', 'CMPosterMissFebruary', 'CMPosterRememberIo',
    'CMPosterSafetyClean', 'PosterContrabandEAT', 'PosterContrabandHighEffectEngineering',
    'PosterLegitCleanliness', 'PosterLegitCohibaRobustoAd', 'PosterLegitSafetyEyeProtection',
    'PosterLegitSafetyInternals', 'PosterLegitSafetyReport',
)
SCENES = {
    ('classic', 0): GENERATED / 'prison-classic-scene.json',
    ('redux', 0): GENERATED / 'prison-redux-surface-scene.json',
    ('redux', -2): GENERATED / 'reagent-redux-minus2-scene.json',
}
ENTITY_FACING = {'CMPostEat', 'CMPosterBeth', 'CMPosterBobda', 'CMPosterMissFebruary', 'CMPosterSafetyClean'}
INFERRED_EAST = {'PosterContrabandEAT', 'PosterLegitCohibaRobustoAd'}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def relative(path):
    return path.relative_to(ROOT).as_posix()


def part(label, low, high, color, **extra):
    return dict(label=label, min=', '.join(f'{v:.7f}' for v in low),
                max=', '.join(f'{v:.7f}' for v in high), color=color, **extra)


def alpha_rectangles(image):
    """Disjoint, vertically merged opaque runs; preserve holes and torn corners."""
    rectangles, active = [], {}
    for y in range(image.height):
        spans, start = [], None
        for x in range(image.width + 1):
            occupied = x < image.width and image.getpixel((x, y))[3] > 0
            if occupied and start is None:
                start = x
            elif not occupied and start is not None:
                spans.append((start, x))
                start = None
        following = {}
        for a, b in spans:
            if (a, b) in active:
                index = active[a, b]
                rectangles[index][3] = y + 1
            else:
                index = len(rectangles)
                rectangles.append([a, y, b, y + 1])
            following[a, b] = index
        active = following
    return rectangles


def map_level(entry):
    if entry['variant'] == 'classic':
        return 0 if Path(entry['path']).stem == 'garrison' else (-1 if 'below' in entry['path'] else 1)
    return int(re.search(r'StableGarrisonMultiZ(-?\d+)\.yml$', entry['path'])[1])


def source_audit(inventory, library):
    print('Reading source prototype defaults and saved poster contexts...', flush=True)
    kinds, issues, _ = source_inventory.load_prototypes(ROOT)
    resolver = source_inventory.Resolver(kinds['entity'])
    defaults, errors = {}, []
    for entry in inventory['prototypes']:
        try:
            defaults[entry['id']] = source_inventory.component_map(resolver.resolve(entry['id']))
        except source_inventory.ResolutionError as error:
            errors.append(dict(prototype=entry['id'], error=str(error)))
    assert all(p in defaults for p in SELECTED)
    maps, contexts, provenance = [], [], {}
    for entry in inventory['maps']:
        path = ROOT / entry['path']
        provenance[relative(path)] = sha(path)
        expected = sum(entry['prototypeCounts'].get(p, 0) for p in SELECTED)
        spec = dict(variant=entry['variant'], level=map_level(entry), path=entry['path'], expected=expected)
        maps.append(spec)
        if not expected:
            spec['visibleCount'] = 0
            continue
        header, records = scene.read_map(path)
        transforms = scene.WorldTransforms(records, defaults)
        hidden = scene.hidden_container_entities(records)
        walls = []
        for record in records.values():
            uid = record['id']
            smooth = {**defaults.get(record['prototype'], {}).get('IconSmooth', {}),
                      **record['components'].get('IconSmooth', {})}
            if uid not in hidden and smooth.get('key') == 'walls' and smooth.get('enabled', True):
                walls.append((record, transforms.resolve(uid)))
        entries = []
        for record in records.values():
            if record['prototype'] not in SELECTED:
                continue
            uid, prototype = record['id'], record['prototype']
            assert uid not in hidden, f'Hidden poster {uid} requires separate handling'
            x, y, yaw, root = transforms.resolve(uid)
            sprite = {**defaults[prototype]['Sprite'], **record['components'].get('Sprite', {})}
            reference = next(e for e in inventory['prototypes'] if e['id'] == prototype)['sprite']
            assert sprite.get('state') == reference['state'] and sprite.get('sprite') == reference['sprite']
            assert scene.finite_vector(sprite.get('offset', '0,0')) == (0, 0)
            assert scene.finite_vector(sprite.get('scale', '1,1')) == (1, 1)
            assert not sprite.get('noRot', False) and sprite.get('snapCardinals') is True
            assert scene.radians(sprite.get('rotation', 0)) == 0
            assert scene.normalize_tint(sprite.get('color', '#FFFFFF')) == '#FFFFFF'
            assert sprite.get('visible', True) and not sprite.get('layers')
            assert abs(yaw/(math.pi/2)-round(yaw/(math.pi/2))) < 1e-6
            neighbors = [dict(id=w['id'], prototype=w['prototype'], position=list(q[:2]),
                              delta=[round(q[0]-x, 7), round(q[1]-y, 7)], yaw=q[2])
                         for w, q in walls if abs(q[0]-x) < 1.6 and abs(q[1]-y) < 1.6]
            assert neighbors, f'No wall context for {prototype}/{uid}'
            entries.append(dict(variant=spec['variant'], level=spec['level'], id=uid,
                                prototype=prototype, position=[x, y, spec['level']], yaw=yaw,
                                effectiveSprite=sprite, savedComponents=record['components'],
                                nearbyWalls=neighbors, root=root, map=entry['path']))
        spec['visibleCount'] = len(entries)
        assert len(entries) == expected
        contexts.append((spec, header, records, transforms, entries))
    for prototype in SELECTED:
        effective = resolver.resolve(prototype)
        for ancestor in [prototype, *effective['_ancestors']]:
            path = ROOT / kinds['entity'][ancestor]['_source']
            provenance[relative(path)] = sha(path)
    return defaults, maps, contexts, provenance, errors


def author(inventory, contexts, provenance):
    by_proto = {p['id']: p for p in inventory['prototypes']}
    placements = [e for _, _, _, _, entries in contexts for e in entries]
    # Exact mapped wall families actually adjacent to the selected saved posters.
    backing = sorted({w['prototype'] for e in placements for w in e['nearbyWalls']})
    occupied = {e['atlasIndex']: e['id'] for path in PROTOTYPES.glob('*.yml') if path != ART_FILE
                for e in (yaml.safe_load(path.read_text(encoding='utf-8')) or [])
                if e.get('type') == 'cmu3DSurface'}
    models, surfaces, proof = [], [], []
    for index, prototype in enumerate(SELECTED):
        entry = by_proto[prototype]
        sprite = entry['sprite']
        state, rsi = sprite['state'], sprite['sprite']
        source = source_inventory.resource_path(ROOT, '/Textures/' + rsi)
        assert source is not None
        metadata = json.loads((source / 'meta.json').read_text())
        state_meta = next(s for s in metadata['states'] if s['name'] == state)
        assert state_meta.get('directions', 1) == 1 and 'delays' not in state_meta
        frame = Image.open(source / (state + '.png')).convert('RGBA')
        assert frame.size == (32, 32) and set(frame.getchannel('A').get_flattened_data()) == {0, 255}
        for path in (source / 'meta.json', source / (state + '.png')):
            provenance[relative(path)] = sha(path)
        bounds = frame.getbbox()
        assert bounds is not None
        crop = frame.crop(bounds)
        uid = 'CMU3DWallPoster' + prototype
        surface = uid + 'Face'
        atlas = 1600 + index
        assert atlas not in occupied, f'Atlas {atlas} already belongs to {occupied.get(atlas)}'
        texture = TEXTURES / (surface + '.png')
        crop.save(texture)
        assert Image.open(texture).convert('RGBA').tobytes() == crop.tobytes()
        surfaces.append(dict(type='cmu3DSurface', id=surface, atlasIndex=atlas,
                             texture='/Textures/CMU14/ThreeD/Surfaces/' + texture.name))
        rectangles = alpha_rectangles(frame)
        x = lambda pixel: (pixel-16)/32
        # Larger upstream prints clear the actual 2.27+ wall light fittings.
        mount_height = 1.9 if rsi.startswith('_RMC14/') else 1.75
        z = lambda pixel: mount_height+(16-pixel)/32
        parts = [part('silhouette paper backing ' + str(n), (x(a), -.509, z(d)),
                      (x(c), -.501, z(b)), '#B8AD95')
                 for n, (a, b, c, d) in enumerate(rectangles)]
        a, b, c, d = bounds
        parts.append(part('unchanged printed face', (x(a), -.511, z(d)), (x(c), -.509, z(b)),
                          '#FFFFFF', surface=surface, surfaceAxis='XZ'))
        model = dict(type='cmu3DModel', id=uid, label=entry['id'] + ' wall poster', status='draft',
                     sourcePrototypes=[prototype], referencePrototype=prototype,
                     referenceRsi=rsi, referenceState=state, sourceDirections=1,
                     wallMounted=True, useEntityRotation=prototype in ENTITY_FACING, backWallMountTargets=backing,
                     fitInsideWall=True, wallPaper=True,
                     description='Unmodified source print on alpha-silhouette paper backing. Source pixel pivot is retained; '
                     'physical facing uses the documented source-upright or context inference. The '
                     f'{mount_height}-tile source-center height, 0.010-tile total thickness and blank reverse '
                     'material are inferred. Static single-frame artwork.', parts=parts)
        if prototype in INFERRED_EAST:
            model['yawOffset'] = 90
        if prototype == 'PosterLegitCleanliness':
            model['wallFacingTargets'] = ['RMCLightFixture']
        models.append(model)
        sample_count = 0
        for py in range(32):
            for px in range(32):
                backing_hits = sum(ra <= px < rc and rb <= py < rd for ra, rb, rc, rd in rectangles)
                assert backing_hits == int(frame.getpixel((px, py))[3] > 0)
                if a <= px < c and b <= py < d:
                    assert crop.getpixel((px-a, py-b)) == frame.getpixel((px, py))
                    sample_count += 1
        proof.append(dict(prototype=prototype, modelId=uid, sourceRsi=rsi, sourceState=state,
                          sourceDirections=1, sourceSpriteOffset=[0, 0], noRot=False, snapCardinals=True,
                          inferredSourceCenterHeight=mount_height, physicalUsesEntityYaw=prototype in ENTITY_FACING,
                          inferredYawOffset=model.get('yawOffset', 0), wallFacingTargets=model.get('wallFacingTargets', []),
                          sourceFrameSize=[32, 32], crop=list(bounds), atlasIndex=atlas,
                          sourcePixels=sample_count, opaquePixels=sum(bool(p[3]) for p in frame.get_flattened_data()),
                          sourceRgbaSha256=hashlib.sha256(frame.tobytes()).hexdigest(),
                          cropRgbaSha256=hashlib.sha256(crop.tobytes()).hexdigest(),
                          textureSha256=sha(texture), backingRectangles=rectangles,
                          silhouettePixelsChecked=1024, silhouetteMismatches=0, pixelMismatches=0,
                          parts=len(parts), instanceCounts=entry['instanceCounts'],
                          license=metadata.get('license'), copyright=metadata.get('copyright')))
    MODEL_FILE.write_text('# Generated by author_wall_posters.py; see SOURCES_WALL_POSTERS.md.\n' +
                          yaml.safe_dump(models, sort_keys=False), encoding='utf-8')
    ART_FILE.write_text('# Verbatim source poster crops; see SOURCES_WALL_POSTERS.md for attribution.\n' +
                        yaml.safe_dump(surfaces, sort_keys=False), encoding='utf-8')
    return build_models.load_models(MODEL_FILE), proof


def overlaps(a, b):
    low_a, high_a = box_bounds(a)
    low_b, high_b = box_bounds(b)
    dimensions = [min(high_a[i], high_b[i])-max(low_a[i], low_b[i]) for i in range(3)]
    return all(v > 1e-6 for v in dimensions)


def context_reviews(models, library, contexts, defaults, provenance):
    direct = {m['sourcePrototypes'][0]: m for m in models}
    merged = [*library.values(), *models]
    combined = {m['id']: m for m in merged}
    results = []
    (REVIEW / 'contexts').mkdir(parents=True, exist_ok=True)
    for spec, _, records, transforms, sources in contexts:
        path = SCENES[spec['variant'], spec['level']]
        provenance[relative(path)] = sha(path)
        saved_scene = json.loads(path.read_text())
        nearby_instances = saved_scene['instances']
        candidates = [dict(id=e['id'], prototype=e['prototype'], position=e['position'], yaw=e['yaw'],
                           modelId=direct[e['prototype']]['id'], matchKind='exact') for e in sources]
        variants, _ = layout.resolve_layout(candidates, merged, records, defaults, transforms)
        placement.resolve_placements(candidates, merged, variants)
        for source, entity in zip(sources, candidates):
            model = direct[entity['prototype']]
            parts = variants.get(entity.get('geometryKey'), model['parts'])
            yaw = entity.get('renderYaw', entity['yaw'])
            offset = entity.get('renderOffset', [0, 0, 0])
            px, py = source['position'][:2]
            candidate = world_parts(parts, source['position'], yaw, offset)
            known, contacts, unknown = [], [], []
            for neighbor in nearby_instances:
                if neighbor['id'] == source['id']:
                    continue
                dx, dy = neighbor['position'][0]-px, neighbor['position'][1]-py
                if abs(dx) > 1.55 or abs(dy) > 1.55:
                    continue
                other_model = combined.get(neighbor.get('modelId'))
                if other_model is None or neighbor.get('matchKind') != 'exact':
                    unknown.append(dict(id=neighbor['id'], prototype=neighbor['prototype'], matchKind=neighbor['matchKind']))
                    continue
                raw = saved_scene['geometryVariants'].get(neighbor.get('geometryKey'), other_model['parts'])
                other_parts = world_parts(raw, neighbor['position'], neighbor.get('renderYaw', neighbor['yaw']),
                                          neighbor.get('renderOffset', [0, 0, 0]))
                hits = [(a.get('label'), b.get('label')) for a in candidate for b in other_parts if overlaps(a, b)]
                if hits:
                    contacts.append(dict(id=neighbor['id'], prototype=neighbor['prototype'],
                                         partPairs=len(hits), labels=hits))
                known.append((neighbor, other_parts))
            # Keep the poster-facing half-space in the cutaway; remote wall faces otherwise hide the print.
            normal = (math.sin(yaw), -math.cos(yaw))
            if entity.get('layoutAlignment') == 'room side of adjacent wall' or entity.get('geometryKey', '').endswith(':inside-wall'):
                normal = (-normal[0], -normal[1])
            eye_yaw = math.atan2(normal[1], normal[0]) + .28
            assembled = []
            drawn = []
            for neighbor, other_parts in known:
                delta = [neighbor['position'][0]-px, neighbor['position'][1]-py]
                if sum(delta[i]*normal[i] for i in (0, 1)) > .55:
                    continue
                assembled.extend(other_parts)
                drawn.append(neighbor['id'])
            assembled.extend(candidate)
            for p in assembled:
                for bound in ('min', 'max'):
                    p[bound] = [p[bound][0]-px, p[bound][1]-py, p[bound][2]]
            card = Image.new('RGB', (1040, 540), '#14212A')
            draw = ImageDraw.Draw(card)
            draw.text((14, 10), f"{source['variant']} {source['level']:+d} / UID {source['id']} / {source['prototype']}",
                      fill='#EEE7DA', font=ImageFont.load_default(size=16))
            draw.text((14, 36), f"Saved yaw {math.degrees(source['yaw']):.0f} / physical yaw {math.degrees(yaw):.0f}; offset {offset}",
                      fill='#BACBD4', font=ImageFont.load_default(size=13))
            frame = Image.open(build_models.resource_file('/Textures/'+model['referenceRsi']) / (model['referenceState']+'.png')).convert('RGBA')
            frame = frame.resize((256, 256), Image.Resampling.NEAREST)
            card.paste(frame, (14, 100), frame)
            image = build_models.render_model(dict(parts=assembled), size=(750, 430), yaw=eye_yaw,
                                              pitch=.2, pixels_per_unit=190, screen_origin=(360, 380))
            card.paste(image, (280, 72))
            draw.text((14, 380), 'Original source frame', fill='#EEE7DA', font=ImageFont.load_default(size=14))
            draw.text((14, 408), 'Wall context cutaway', fill='#BACBD4', font=ImageFont.load_default(size=14))
            draw.text((14, 430), f'{len(contacts)} contact candidates', fill='#BACBD4', font=ImageFont.load_default(size=14))
            draw.text((14, 507), 'Source print and saved pose preserved. Height / depth / unseen reverse inferred. Cutaway is a review view.',
                      fill='#AEBCC5', font=ImageFont.load_default(size=13))
            review_path = REVIEW/'contexts'/f"{source['variant']}-{source['level']}-{source['id']}.png"
            card.save(review_path)
            results.append({**source, 'presentation': entity, 'review': relative(review_path),
                            'modeledNeighbors': len(known), 'unknownNearbyInstances': unknown,
                            'modeledContactCandidates': contacts, 'cutawayDrawnNeighborIds': drawn,
                            'conservativeAabbOnly': True})
    return results


def model_reviews(models, proof):
    REVIEW.mkdir(parents=True, exist_ok=True)
    thumb_paths = []
    for model, entry in zip(models, sorted(proof, key=lambda e: e['modelId'])):
        assert model['id'] == entry['modelId']
        frame = Image.open(build_models.resource_file('/Textures/'+model['referenceRsi']) / (model['referenceState']+'.png')).convert('RGBA')
        card = Image.new('RGB', (1050, 390), '#14212A')
        draw = ImageDraw.Draw(card)
        draw.text((12, 12), model['label'], fill='#EEE7DA', font=ImageFont.load_default(size=17))
        draw.text((12, 38), f"{len(model['parts'])} parts / exact source crop / silhouette backing / inferred 0.010 tile thickness",
                  fill='#BACBD4', font=ImageFont.load_default(size=13))
        frame = frame.resize((288, 288), Image.Resampling.NEAREST)
        card.paste(frame, (10, 68), frame)
        for n, angle in enumerate((-math.pi/2, -math.pi/2+.5, math.pi/2-.5)):
            image = build_models.render_model(model, size=(240, 280), yaw=angle, pitch=.15)
            card.paste(image, (309+n*245, 72))
        draw.text((12, 366), 'Source frame                         Front                              Oblique                             Inferred reverse',
                  fill='#BACBD4', font=ImageFont.load_default(size=13))
        path = REVIEW/(model['id']+'.png')
        card.save(path)
        thumb_paths.append(path)
    montage = Image.new('RGB', (1050, 195*len(thumb_paths)), '#14212A')
    for n, path in enumerate(thumb_paths):
        montage.paste(Image.open(path).resize((525, 195)), ((n%2)*525, (n//2)*195))
    montage = montage.crop((0, 0, 1050, math.ceil(len(thumb_paths)/2)*195))
    montage.save(REVIEW/'source-model-montage.png')


def main():
    REVIEW.mkdir(parents=True, exist_ok=True)
    inventory = json.loads((GENERATED/'inventory.json').read_text())
    library_path = GENERATED/'models.json'
    library = {m['id']: m for m in json.loads(library_path.read_text())['models']}
    owned_ids = {'CMU3DWallPoster'+p for p in SELECTED}
    assert not any(p in SELECTED for m in library.values() if m['id'] not in owned_ids for p in m['sourcePrototypes'])
    library = {k:v for k,v in library.items() if k not in owned_ids}
    defaults, maps, contexts, provenance, errors = source_audit(inventory, library)
    provenance[relative(library_path)] = sha(library_path)
    models, proof = author(inventory, contexts, provenance)
    print(f'Authored {len(models)} source plates; rendering model and placement reviews...', flush=True)
    model_reviews(models, proof)
    contexts_proof = context_reviews(models, library, contexts, defaults, provenance)
    assert Counter(p['variant'] for p in contexts_proof) == {'redux': 23, 'classic': 17}
    contacts = [p for p in contexts_proof if p['modeledContactCandidates']]
    assert all(p['variant'] == 'classic' and p['id'] == 8816 for p in contacts), 'Unexpected poster obstruction'
    # Only owned output and read source inputs are recorded, never a global-build claim.
    for path in (MODEL_FILE, ART_FILE, Path(__file__).resolve()):
        provenance[relative(path)] = sha(path)
    result = dict(schemaVersion=1, modelCount=len(models), newExactPrototypeMappings=list(SELECTED),
                  counts=dict(Counter(p['variant'] for p in contexts_proof)),
                  sourceFramePixelComparisons=sum(p['sourcePixels'] for p in proof),
                  silhouettePixelComparisons=sum(p['silhouettePixelsChecked'] for p in proof),
                  models=proof, maps=maps, placements=contexts_proof,
                  unrelatedPrototypeResolutionErrors=errors, inputAndOwnedOutputSha256=provenance,
                  limitations=['Static saved appearances only; no native build, global export, game or server was run.',
                    'Saved transforms are unchanged. Physical facing is an explicit source-upright/context inference; it is not claimed as recovered source geometry.',
                    'Height, physical thickness and unseen reverse material are explicit inferences.',
                    'Contact checks use conservative part AABBs. Unknown/unmapped neighbors are retained in the report.',
                    'Classic-only CMPosterSafetyGoggles1 and decals.rsi no-smoking signs are outside this Redux poster batch.'])
    result['summary'] = dict(sourcePixelMismatches=0, silhouettePixelMismatches=0,
        modeledNeighborComparisons=sum(p['modeledNeighbors'] for p in contexts_proof),
        fullyComparedSavedPlacements=len(contexts_proof),
        reduxPlacementsWithoutModeledContacts=sum(p['variant']=='redux' and not p['modeledContactCandidates'] for p in contexts_proof),
        classicPlacementsWithoutModeledContacts=sum(p['variant']=='classic' and not p['modeledContactCandidates'] for p in contexts_proof),
        unknownOrInheritedNearbyOccurrences=sum(len(p['unknownNearbyInstances']) for p in contexts_proof),
        atlasIndices=[1600,1614], partsPerModel=2, sourceDirections=1, sourceFramesPerModel=1,
        sourceAnimations=0, nativeOrGlobalBuildExecuted=False)
    result['deferredPlacements'] = [dict(variant='classic', level=0, id=8816, prototype='PosterLegitCleanliness',
        neighborId=46208, neighborPrototype='RMCWallPrisonHull',
        reason='Source-upright physical face intersects southern wall. North/south/west walls and a half-tile east sink '
               'require a bounded separate attachment treatment; broad sink target would misorient other washroom contexts.')]
    result['limitations'].append('Classic Cleanliness UID8816 remains embedded against neighboring prison wall46208; '
                                 'this is explicitly not part of the 39 clear placements.')
    result['sourceNotes'] = dict(path=relative(NOTES), sha256=sha(NOTES))
    (GENERATED/'wall-posters-proof.json').write_text(json.dumps(result, indent=2)+'\n', encoding='utf-8')
    print(json.dumps(dict(models=len(models), counts=result['counts'],
                          contacts=[(p['variant'],p['level'],p['id'],p['modeledContactCandidates'])
                                    for p in contexts_proof if p['modeledContactCandidates']],
                          wallTargets=sorted({w['prototype'] for p in contexts_proof for w in p['nearbyWalls']})), indent=2), flush=True)


if __name__ == '__main__':
    main()
