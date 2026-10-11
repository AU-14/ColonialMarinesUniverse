"""Read exported shutter meshes, source pixels, STEP clips and saved placements.

No exports or historical reports are modified. Run after the parent export pass:
  python Tools/three_d/verify_window_shutter_consolidation_exports.py --scenes-json PATH
The scene config is a list of {variant, level, file} records in generated/.
"""
import argparse
from io import BytesIO
import hashlib
import itertools
import json
import math
from pathlib import Path
import struct

from PIL import Image
import yaml

ROOT = Path(__file__).resolve().parents[2]
GENERATED = ROOT/'Tools/three_d/generated'
BASELINE = ROOT/'.codex/interior-baseline864'
GLBS = ROOT/'Content.CMU/Resources/Models/CMU14/Garrison'
SOURCE = ROOT/'Resources/Textures/_RMC14/Structures/Doors/Shutters/Hybrisa/window_shutter.rsi'
IDS = ('CMU3DHybrisaWindowShutter', 'CMU3DHybrisaWindowShutterOpen')
TOLERANCE = 2e-6


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def glb(path):
    raw = path.read_bytes()
    assert struct.unpack_from('<4sII', raw) == (b'glTF', 2, len(raw))
    offset, document, binary = 12, None, None
    while offset < len(raw):
        size, kind = struct.unpack_from('<I4s', raw, offset)
        payload = raw[offset+8:offset+8+size]
        if kind == b'JSON':
            document = json.loads(payload)
        elif kind == b'BIN\0':
            binary = payload
        offset += 8+size
    assert document is not None and binary is not None and offset == len(raw)
    return document, binary


def accessor(document, binary, index):
    spec = document['accessors'][index]
    view = document['bufferViews'][spec['bufferView']]
    assert not spec.get('sparse') and view.get('buffer', 0) == 0
    fmt = {5126: 'f', 5123: 'H', 5125: 'I', 5121: 'B'}[spec['componentType']]
    count = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4}[spec['type']]
    packed = struct.calcsize('<'+fmt*count)
    stride = view.get('byteStride', packed)
    start = view.get('byteOffset', 0)+spec.get('byteOffset', 0)
    return [struct.unpack_from('<'+fmt*count, binary, start+i*stride) for i in range(spec['count'])]


def near(actual, expected):
    return len(actual) == len(expected) and all(abs(a-b) <= TOLERANCE for a, b in zip(actual, expected))


def source_frame(state, index, direction=0):
    meta = json.loads((SOURCE/'meta.json').read_text())
    entry = next(s for s in meta['states'] if s['name'] == state)
    delays = entry.get('delays', [[1]]*4)
    flat = sum(len(row) for row in delays[:direction])+index
    sheet = Image.open(SOURCE/(state+'.png')).convert('RGBA')
    columns = sheet.width//32
    return sheet.crop((flat%columns*32, flat//columns*32, flat%columns*32+32, flat//columns*32+32))


def verify_model(model, raw_model, art):
    uid = model['id']
    path = GLBS/(uid+'.glb')
    document, binary = glb(path)
    assert document['extras']['cmuPrototype'] == uid
    groups = document['extras']['doorSpriteFrameGroups']
    assert document['extras']['defaultDoorSpriteFrame'] == model['referenceState']+':0'
    assert document['extras']['doorAnimationDurations'] == model['doorAnimationDurations']
    assert model['doorAnimationDurations'] == dict(opening=1.0, closing=1.0)
    nodes = document['nodes']
    assert len(nodes) == 165 and len(document['animations']) == 2
    assert set(groups) == {f'{state}:{i}' for state, d in model['doorSpriteStates'].items() for i in range(len(d['frames']))}
    active_scales = {}
    tracks = {}
    clip_samples = 0
    for clip in document['animations']:
        state = clip['name'].lower()
        assert state in ('opening', 'closing') and len(clip['channels']) == len(nodes)
        channels = {}
        for channel in clip['channels']:
            target = channel['target']
            assert target['path'] == 'scale' and target['node'] not in channels
            sampler = clip['samplers'][channel['sampler']]
            assert sampler['interpolation'] == 'STEP'
            times = [v[0] for v in accessor(document, binary, sampler['input'])]
            values = accessor(document, binary, sampler['output'])
            assert near(times, [0, .1, .2, .3, .4, .5, 1])
            channels[target['node']] = (times, values)
            for value in values:
                if any(value):
                    assert all(v > 0 for v in value)
                    if target['node'] in active_scales:
                        assert near(value, active_scales[target['node']])
                    active_scales[target['node']] = value
        # Immediately before/after all source transitions plus the last-frame hold.
        sample_times = [0, .099, .10001, .199, .20001, .299, .30001, .399,
                        .40001, .499, .50001, .599, .6, .8, .999, 1, 1.1]
        for time in sample_times:
            expected = f'{state}:{min(5, int(time*10))}' if time < 1 else ('open:0' if state == 'opening' else 'closed:0')
            visible = []
            for node, (times, values) in channels.items():
                key = max(i for i, t in enumerate(times) if t <= time)
                if any(values[key]):
                    visible.append(node)
            assert visible == groups[expected], (uid, state, time)
            clip_samples += 1
        tracks[state] = dict(channels=len(channels), sampleTimes=sample_times, finalHold=[.5, 1.0],
                             sourceStripEnds=.6, stepOnly=True)
    assert len(active_scales) == len(nodes)
    default_nodes = groups[model['referenceState']+':0']
    assert [i for i, node in enumerate(nodes) if any(node['scale'])] == default_nodes
    decoded_images = {}
    for index, definition in enumerate(document['images']):
        view = document['bufferViews'][definition['bufferView']]
        start = view.get('byteOffset', 0)
        image = Image.open(BytesIO(binary[start:start+view['byteLength']])).convert('RGBA')
        reference = Image.open(art[definition['name']]).convert('RGBA')
        assert image.size == reference.size and image.tobytes() == reference.tobytes()
        decoded_images[index] = image
    assert len(decoded_images) == 43
    references = pixels = node_parts = uv_vertices = triangles = 0
    for state, definition in model['doorSpriteStates'].items():
        assert definition['delays'] == raw_model['doorSpriteStates'][state]['delays']
        for index, frame in enumerate(definition['frames']):
            group = groups[f'{state}:{index}']
            assert len(group) == len(frame['parts'])
            sampled_parts = []
            for node_index, part in zip(group, frame['parts']):
                node = nodes[node_index]
                assert node['name'] == part['label'] and not node.get('rotation')
                low, high = part['min'], part['max']
                expected_center = [(low[0]+high[0])/2, (low[2]+high[2])/2, -(low[1]+high[1])/2]
                expected_scale = [high[0]-low[0], high[2]-low[2], high[1]-low[1]]
                assert near(node['translation'], expected_center) and near(active_scales[node_index], expected_scale)
                primitive = document['meshes'][node['mesh']]['primitives'][0]
                positions = accessor(document, binary, primitive['attributes']['POSITION'])
                normals = accessor(document, binary, primitive['attributes']['NORMAL'])
                uvs = accessor(document, binary, primitive['attributes']['TEXCOORD_0'])
                indices = accessor(document, binary, primitive['indices'])
                assert len(positions) == len(normals) == len(uvs) == 24 and len(indices) == 36
                assert {p for p in positions} == set(itertools.product((-.5, .5), repeat=3))
                assert set(normals) == {(1.,0.,0.),(-1.,0.,0.),(0.,1.,0.),(0.,-1.,0.),(0.,0.,1.),(0.,0.,-1.)}
                for point, uv in zip(positions, uvs):
                    assert near(uv, (point[0]+.5, .5-point[1]))
                mesh_faces = {}
                for at in range(0, 36, 3):
                    tri = [positions[indices[at+i][0]] for i in range(3)]
                    planes = [(axis, tri[0][axis]) for axis in range(3) if len({v[axis] for v in tri}) == 1]
                    assert len(planes) == 1 and len(set(tri)) == 3
                    a, b = ([tri[j][i]-tri[0][i] for i in range(3)] for j in (1, 2))
                    cross = [a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0]]
                    assert sum(v*v for v in cross) == 1
                    mesh_faces.setdefault(planes[0], []).append(set(tri))
                assert set(mesh_faces) == set(itertools.product(range(3), (-.5, .5)))
                assert all(len(tris) == 2 and len(tris[0] | tris[1]) == 4 for tris in mesh_faces.values())
                material = document['materials'][primitive['material']]
                pbr = material['pbrMetallicRoughness']
                assert pbr['baseColorFactor'] == [1, 1, 1, 1]
                assert material['alphaMode'] == 'MASK' and material['alphaCutoff'] == .5
                assert material['doubleSided'] is True
                texture = document['textures'][pbr['baseColorTexture']['index']]
                sampler = document['samplers'][texture['sampler']]
                assert sampler == dict(magFilter=9728, minFilter=9728, wrapS=33071, wrapT=33071)
                assert document['images'][texture['source']]['name'] == part['surface']
                sampled_parts.append((node['translation'], active_scales[node_index], decoded_images[texture['source']]))
                node_parts += 1
                uv_vertices += len(uvs)
                triangles += len(indices)//3
            # Evaluate the actual GLB translations, STEP scales and embedded images.
            canonical = source_frame(state, index)
            for y in range(32):
                for x in range(32):
                    point = ((x+.5)/32-.5, 2.74-(y+.5)*2.74/28, 0)
                    hit = []
                    for translation, scale, image in sampled_parts:
                        local = [(point[i]-translation[i])/scale[i] for i in range(3)]
                        if all(-.5 < p < .5 for p in local):
                            px = min(image.width-1, max(0, math.floor((local[0]+.5)*image.width)))
                            py = min(image.height-1, max(0, math.floor((.5-local[1])*image.height)))
                            hit.append(image.getpixel((px, py)))
                    expected = canonical.getpixel((x, y))
                    assert hit == ([expected] if expected[3] else []), (uid, state, index, x, y)
                    pixels += 1
            for direction in range(4):
                url = model['doorSpriteReferences'][state][index][direction]
                actual = Image.open(GENERATED/url.removeprefix('../generated/')).convert('RGBA')
                expected = source_frame(state, index, direction)
                assert actual.size == expected.size and actual.tobytes() == expected.tobytes()
                references += 1
    return dict(model=uid, file=path.relative_to(ROOT).as_posix(), sha256=digest(path), poseCount=len(groups),
        exportedPartNodes=node_parts, meshTrianglesChecked=triangles, projectedUvVertices=uv_vertices,
        exactEmbeddedPngs=len(decoded_images), sourceGeometrySamples=pixels, exactDirectionalReferencePngs=references,
        actualStepClipSamples=clip_samples, clips=tracks, defaultStateMatches=True,
        transformTolerance=TOLERANCE, note='Animation scales use glTF float32; exact authored decimal field preservation is proven separately.')


def verify_scenes(config):
    previous = {(r['variant'], r['level']): r for r in json.loads((BASELINE/'scenes.json').read_text())}
    assert {(r['variant'], r['level']) for r in config} == set(previous)
    result = []
    for row in config:
        key = (row['variant'], row['level'])
        before_path = BASELINE/previous[key]['file']
        after_path = GENERATED/row['file']
        before = json.loads(before_path.read_text())
        after = json.loads(after_path.read_text())
        old = {i['id']: i for i in before['instances'] if i.get('modelId') in IDS}
        new = {i['id']: i for i in after['instances'] if i.get('modelId') in IDS}
        assert old == new, (key, [i for i in old if old.get(i) != new.get(i)])
        assert not any(i.get('geometryKey') for i in new.values()), 'Unexpected shutter geometry variant needs explicit audit'
        result.append(dict(variant=key[0], level=key[1], baseline=before_path.relative_to(ROOT).as_posix(),
            scene=after_path.relative_to(ROOT).as_posix(), sha256=digest(after_path),
            preservedInstances=len(new), preservedMountedOffsets=sum(bool(i.get('renderOffset')) for i in new.values()),
            entireInstanceRecordsEqual=True, ids=sorted(new)))
    assert sum(r['preservedInstances'] for r in result) == 1488
    assert sum(r['preservedMountedOffsets'] for r in result) == 1321
    return result


def verify_regions(config, manifest_path, library, art):
    """Every saved shutter in the new assembled GLBs, plus mount context examples."""
    manifest = json.loads(manifest_path.read_text())
    records = [r for r in manifest if r['kind'] in ('shutter-only', 'shutter-context')]
    scene_paths = {(r['variant'], r['level']): GENERATED/r['file'] for r in config}
    wanted = {(r['variant'], r['level']) for r in records}
    scenes = {key: json.loads(scene_paths[key].read_text()) for key in wanted}
    targets = {key: {i['id']: i for i in scene['instances'] if i.get('modelId') in IDS} for key, scene in scenes.items()}
    covered = {key: set() for key in wanted}
    context_combinations = {key: set() for key in wanted}
    expected_combinations = {key: {(round(e.get('renderYaw', e['yaw'])/(math.pi/2))%4, e['modelId'])
                                  for e in entries.values()} for key, entries in targets.items()}
    results = []
    for record in records:
        key = record['variant'], record['level']
        path = GENERATED/'interior-regions'/record['file']
        document, binary = glb(path)
        assert document['extras']['map'] == scenes[key]['map']['path']
        assert not document.get('animations'), 'Assembled map regions must retain stable saved poses'
        root_indices = document['scenes'][document.get('scene', 0)]['nodes']
        nodes = document['nodes']
        roots = {nodes[i]['extras']['savedUid']: nodes[i] for i in root_indices
                 if nodes[i].get('extras', {}).get('modelId') in IDS}
        if record['kind'] == 'shutter-only':
            assert set(roots) == set(targets[key]) and len(root_indices) == len(roots)
            assert not covered[key], 'Expected exactly one full shutter-only assembly per map'
            covered[key].update(roots)
        else:
            focus = targets[key][record['focusUid']]
            assert record['focusUid'] in roots
            context_combinations[key].add((round(focus.get('renderYaw', focus['yaw'])/(math.pi/2))%4, focus['modelId']))
        decoded_images = {}
        checked_meshes = set()
        child_count = 0
        for saved_id, root in roots.items():
            instance = targets[key][saved_id]
            model = library[instance['modelId']]
            extra = root['extras']
            assert extra['sourcePosition'] == instance['position'] and extra['sourceYaw'] == instance['yaw']
            assert extra['sourcePrototype'] == instance['prototype'] and extra['modelId'] == model['id']
            assert extra['doorState'] == instance.get('doorState') and extra['referenceState'] == instance.get('referenceState')
            yaw = instance.get('renderYaw', instance['yaw'])
            assert extra['renderYaw'] == yaw
            offset = instance.get('renderOffset', [0, 0, 0])
            world = [a+b for a, b in zip(instance['position'], offset)]
            assert near(root['translation'], [world[0], world[2], -world[1]])
            assert near(root['rotation'], [0, math.sin(yaw/2), 0, math.cos(yaw/2)])
            assert len(root['children']) == len(model['parts'])
            for index, part in zip(root['children'], model['parts']):
                child = nodes[index]
                low, high = part['min'], part['max']
                center = [(a+b)/2 for a, b in zip(low, high)]
                scale = [b-a for a, b in zip(low, high)]
                assert child['name'] == part['label'] and not child.get('rotation')
                assert near(child['translation'], [center[0], center[2], -center[1]])
                assert near(child['scale'], [scale[0], scale[2], scale[1]])
                primitive = document['meshes'][child['mesh']]['primitives'][0]
                material = document['materials'][primitive['material']]
                pbr = material['pbrMetallicRoughness']
                assert pbr['baseColorFactor'] == [1, 1, 1, 1]
                assert material['alphaMode'] == 'MASK' and material['alphaCutoff'] == .5
                texture = document['textures'][pbr['baseColorTexture']['index']]
                image_definition = document['images'][texture['source']]
                assert image_definition['name'] == part['surface']
                if texture['source'] not in decoded_images:
                    view = document['bufferViews'][image_definition['bufferView']]
                    start = view.get('byteOffset', 0)
                    image = Image.open(BytesIO(binary[start:start+view['byteLength']])).convert('RGBA')
                    expected = Image.open(art[part['surface']]).convert('RGBA')
                    assert image.size == expected.size and image.tobytes() == expected.tobytes()
                    decoded_images[texture['source']] = image
                if child['mesh'] not in checked_meshes:
                    points = accessor(document, binary, primitive['attributes']['POSITION'])
                    uvs = accessor(document, binary, primitive['attributes']['TEXCOORD_0'])
                    assert len(points) == len(uvs) == 24
                    assert set(points) == set(itertools.product((-.5, .5), repeat=3))
                    assert all(near(uv, [point[0]+.5, .5-point[1]]) for point, uv in zip(points, uvs))
                    checked_meshes.add(child['mesh'])
                child_count += 1
        results.append(dict(file=path.relative_to(ROOT).as_posix(), sha256=digest(path), kind=record['kind'],
            variant=key[0], level=key[1], checkedShutterRoots=len(roots), checkedShutterPartNodes=child_count,
            checkedProjectedMeshes=len(checked_meshes), checkedEmbeddedSourceImages=len(decoded_images),
            savedTransformsAndMountOffsetsPreserved=True, stablePosePartListsPreserved=True))
    assert sum(len(v) for v in covered.values()) == 1488
    assert context_combinations == expected_combinations
    return dict(regions=results, allPlacementRootsVerified=1488,
        contextCoverage=[dict(variant=k[0], level=k[1], yawAndModelCombinations=sorted(v)) for k, v in context_combinations.items()],
        note='Actual assembled root transforms, mounting offsets, stable-pose child transforms, projected mesh UVs and embedded source PNGs were read from the new GLBs. All 14 animated pose meshes are independently covered by the library GLB checks.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--scenes-json', required=True, type=Path)
    parser.add_argument('--regions-json', type=Path, default=GENERATED/'interior-regions.json')
    args = parser.parse_args()
    library = {m['id']: m for m in json.loads((GENERATED/'models.json').read_text())['models']}
    raw = {m['id']: m for m in yaml.load((ROOT/'Content.CMU/Resources/ThreeD/Prototypes/World/garrison_environment.yml').read_text(),
                                       Loader=yaml.CSafeLoader) if m.get('id') in IDS}
    art = {r['id']: ROOT/'Content.CMU/Resources'/r['texture'].lstrip('/') for r in yaml.load(
        (ROOT/'Content.CMU/Resources/ThreeD/Prototypes/World/garrison_window_shutter_art.yml').read_text(), Loader=yaml.CSafeLoader)}
    assert [len(library[uid]['parts']) for uid in IDS] == [19, 6], 'Wait for the parent EXPORT READY checkpoint'
    models = [verify_model(library[uid], raw[uid], art) for uid in IDS]
    config = json.loads(args.scenes_json.read_text())
    scenes = verify_scenes(config)
    regions = verify_regions(config, args.regions_json, library, art)
    proof = json.loads((GENERATED/'window-shutter-consolidation-proof.json').read_text())
    assert proof['status'] == 'Applied' and all(p['field']['exactRgbaColorFieldPreserved'] for p in proof['exactSerializedPoseProof'])
    report = dict(status='Passed actual exported GLB, source, STEP and complete saved-placement checks',
        models=models, scenes=scenes, assembledRegions=regions,
        sourceProof='Tools/three_d/generated/window-shutter-consolidation-proof.json',
        preservedPlacements=sum(s['preservedInstances'] for s in scenes),
        preservedMountedOffsets=sum(s['preservedMountedOffsets'] for s in scenes),
        limitations=['No historical reports or region exports are rewritten by this verifier.',
            'Animated library exports cover all motion poses; assembled saved-map regions retain stable source poses.',
            'Native packing, gameplay collision, directional reconstruction and runtime fidelity are not certified by this export proof. No game/server was launched.'])
    output = GENERATED/'window-shutter-consolidation-export-audit.json'
    output.write_text(json.dumps(report, indent=2)+'\n')
    print(json.dumps(dict(models=len(models), poseGroups=sum(m['poseCount'] for m in models),
        embeddedPngs=sum(m['exactEmbeddedPngs'] for m in models), sourceSamples=sum(m['sourceGeometrySamples'] for m in models),
        stepSamples=sum(m['actualStepClipSamples'] for m in models), preservedPlacements=1488, preservedMountedOffsets=1321), indent=2))


if __name__ == '__main__':
    main()
