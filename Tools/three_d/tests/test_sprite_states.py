from copy import deepcopy
from io import BytesIO
import json
import math
from pathlib import Path
import struct
import sys
import tempfile
import unittest
from unittest.mock import patch

from PIL import Image
import yaml

sys.path[:0] = [str(Path(__file__).resolve().parents[1]), str(Path(__file__).resolve().parent)]
import build_models as bm
import scene
import sprite_states as ss
from export_scene import export_region
from state_inventory import build_report
from test_scene import entity


def model(direction=0, directions=1):
    def frame(color):
        return {'parts': [{'min': [-.4, -.3, .1], 'max': [.4, .3, 1.2], 'color': color}]}
    frames = [frame('#A04020'), frame('#20A040')]
    value = dict(id=f'States{direction}', label='State test', type='cmu3DModel', sourcePrototypes=['Machine'] if direction == 0 else [],
                 sourceDirections=directions, referenceRsi='test.rsi', referenceState='active', sourceSpriteOffset='.25,.5',
                 parts=deepcopy(frames[0]['parts']), spriteStates={'active': {'frames': frames, 'delays': [.1, .2]},
                                                                 'off': {'frames': [frame('#2040A0')], 'delays': [1]}})
    if directions == 4:
        value.update(referenceDirection=direction, directionalModels=[f'States{i}' for i in range(4)])
    return value


def sprite():
    return {'sprite': 'test.rsi', 'state': 'active', 'offset': '.25,.5', 'noRot': True}


def rsi(folder, directions=1):
    folder.mkdir(parents=True, exist_ok=True)
    meta = {'size': {'x': 2, 'y': 2}, 'states': [{'name': 'active', 'directions': directions, 'delays': [[.1, .2]] * directions},
                                             {'name': 'off', 'directions': directions}]}
    (folder / 'meta.json').write_text(json.dumps(meta))
    for state, frames in [('active', 2), ('off', 1)]:
        sheet = Image.new('RGBA', (6, math.ceil(frames * directions / 3) * 2))
        for index in range(frames * directions):
            for y in range(2):
                for x in range(2):
                    sheet.putpixel((index % 3 * 2 + x, index // 3 * 2 + y), (index * 20 + x, y * 30, 45, 127 + x * 128))
        sheet.save(folder / (state + '.png'))
    return meta


class SpriteStateTests(unittest.TestCase):
    def test_static_rotating_surface_props_can_use_rear_wall_clearance_but_animated_poses_cannot(self):
        value = model()
        value.update(placement='surface', sourceSpriteRotates=True, backWallMountTargets=['Wall'])
        with self.assertRaises(ValueError): bm.validate_model(value)
        value['spriteStates'] = {'active': {'frames': [{'parts': deepcopy(value['parts'])}], 'delays': [1]}}
        self.assertEqual(bm.validate_model(value)['backWallMountTargets'], ['Wall'])
        value['spriteStates']['off'] = deepcopy(value['spriteStates']['active'])
        with self.assertRaises(ValueError): bm.validate_model(value)

    def test_schema_bounds_and_incompatible_owners_reject_before_export(self):
        for defect in ('empty', 'too-many', 'missing-reference', 'negative-delay', 'bool-delay', 'nan', 'frame-count', 'frame-budget',
                       'empty-frame', 'default', 'unsafe-state', 'missing-offset', 'offset', 'door', 'fold', 'layout'):
            value = model()
            if defect == 'empty': value['spriteStates'] = {}
            if defect == 'too-many': value['spriteStates'].update({str(i): value['spriteStates']['off'] for i in range(33)})
            if defect == 'missing-reference': value['referenceState'] = 'missing'
            if defect == 'negative-delay': value['spriteStates']['active']['delays'][0] = -1
            if defect == 'bool-delay': value['spriteStates']['active']['delays'][0] = True
            if defect == 'nan': value['spriteStates']['active']['delays'][0] = float('nan')
            if defect == 'frame-count': value['spriteStates']['active']['frames'].pop()
            if defect == 'frame-budget': value['spriteStates']['active'] = {'frames': [value['spriteStates']['off']['frames'][0]] * 65, 'delays': [1] * 65}
            if defect == 'empty-frame': value['spriteStates']['off']['frames'][0]['parts'] = []
            if defect == 'default': value['parts'][0]['color'] = '#000000'
            if defect == 'unsafe-state': value['spriteStates']['../escape'] = value['spriteStates']['off']
            if defect == 'missing-offset': del value['sourceSpriteOffset']
            if defect == 'offset': value['sourceSpriteOffset'] = 'nan,0'
            if defect == 'door': value['doorState'] = 'Closed'
            if defect == 'fold': value['folded'] = False
            if defect == 'layout': value['wallMounted'] = True
            with self.subTest(defect=defect), self.assertRaises(ValueError): bm.validate_model(value)
        # Explicit physical facing and ground pivot are compatible with source frame ownership.
        value = model(0, 4)
        value.update(sourceCardinalFacings=[1, 1, 3, 3], groundOffset='.125,0')
        self.assertEqual(bm.validate_model(value)['sourceCardinalFacings'], [1, 1, 3, 3])

    def test_source_timing_is_identical_in_all_directions_and_crops_retain_rgba(self):
        with tempfile.TemporaryDirectory() as directory:
            folder = Path(directory); meta = rsi(folder, 4)
            value = bm.validate_model(model(2, 4))
            outputs, refs = ss.viewer_references(value, lambda _: folder, bm.surfaces.png_bytes)
            sheet = Image.open(folder / 'active.png').convert('RGBA')
            for index, url in enumerate(refs['active']):
                actual = Image.open(BytesIO(outputs[url.removeprefix('../generated/')])).convert('RGBA')
                tile = 4 + index
                self.assertEqual(actual.tobytes(), sheet.crop((tile % 3 * 2, tile // 3 * 2, tile % 3 * 2 + 2, tile // 3 * 2 + 2)).tobytes())
            for defect in ('different-direction', 'wrong-direction-count', 'missing', 'different-timing', 'short-strip'):
                changed = deepcopy(meta)
                if defect == 'different-direction': changed['states'][0]['delays'][0] = [.3]
                if defect == 'wrong-direction-count': changed['states'][0]['directions'] = 1
                if defect == 'missing': changed['states'].pop(0)
                if defect == 'different-timing': changed['states'][0]['delays'] = [[.15, .15]] * 4
                if defect == 'short-strip': Image.new('RGBA', (2, 2)).save(folder / 'active.png')
                (folder / 'meta.json').write_text(json.dumps(changed))
                with self.subTest(defect=defect), self.assertRaises(ValueError): ss.viewer_references(value, lambda _: folder, bm.surfaces.png_bytes)

    def test_portable_step_loop_and_static_off_scene_are_separate(self):
        value = bm.validate_model(model())
        doc, binary = ss.model_document(value, bm.glb_document)
        self.assertEqual([a['name'] for a in doc['animations']], ['active'])
        self.assertTrue(doc['animations'][0]['extras']['loop'])
        self.assertAlmostEqual(doc['animations'][0]['extras']['durationSeconds'], .3)
        def read(index):
            a = doc['accessors'][index]; view = doc['bufferViews'][a['bufferView']]
            width = 1 if a['type'] == 'SCALAR' else 3
            return struct.unpack_from('<' + 'f' * a['count'] * width, binary, view['byteOffset'])
        for time, expected in [(0, 'active:0'), (.1001, 'active:1'), (.299, 'active:1'), (.3001, 'active:0')]:
            active = []
            for channel in doc['animations'][0]['channels']:
                sampler = doc['animations'][0]['samplers'][channel['sampler']]
                self.assertEqual(sampler['interpolation'], 'STEP')
                times, values = read(sampler['input']), read(sampler['output'])
                frame = max(i for i, t in enumerate(times) if t <= time)
                if any(values[frame * 3:frame * 3 + 3]): active.append(channel['target']['node'])
            self.assertEqual(active, doc['extras']['spriteFrameGroups'][expected])
        static = doc['scenes'][doc['extras']['spriteStaticScenes']['off']]['nodes']
        self.assertTrue(all(any(doc['nodes'][i]['scale']) for i in static))
        self.assertFalse(set(static) & set(doc['scenes'][0]['nodes']))
        value['spriteStates'] = {'off': value['spriteStates']['off']}
        value['referenceState'] = 'off'; value['parts'] = value['spriteStates']['off']['frames'][0]['parts']
        self.assertNotIn('animations', ss.model_document(value, bm.glb_document)[0])

    def test_saved_appearance_proof_and_unknown_states_fall_back(self):
        value = bm.validate_model(model())
        self.assertEqual(ss.saved_pose(value, {'Sprite': sprite()}, {}, scene.normalize_tint), ('active', None))
        supported = {**sprite(), 'color': '#80402080', 'layers': [{'state': 'off'}, {'state': 'unknown', 'visible': False}]}
        self.assertEqual(ss.saved_pose(value, {'Sprite': supported}, {}, scene.normalize_tint), ('off', None))
        for change in ({'state': 'unknown'}, {'offset': '.26,.5'}, {'scale': '2,1'}, {'rotation': 90}, {'noRot': False},
                       {'sprite': 'wrong.rsi'}, {'layers': [], 'state': None}, {'layers': [{'state': 'off'}, {'state': 'active'}]},
                       *({'layers': [{'state': 'off', **field}]} for field in [{'color': '#AABBCC'}, {'shader': 'shaded'},
                           {'texture': 'x.png'}, {'offset': '.01,0'}, {'scale': '1,2'}, {'rotation': '1rad'}, {'dirOffset': 1}])):
            with self.subTest(change=change):
                self.assertIsNone(ss.saved_pose(value, {'Sprite': sprite()}, {'Sprite': change}, scene.normalize_tint)[0])
        self.assertIsNone(ss.saved_pose(value, {'Sprite': sprite()}, {'Appearance': {'data': {'Powered': True}}}, scene.normalize_tint)[0])

    def test_rotating_console_contract_accepts_empty_layer_list_and_retains_source_pose(self):
        values = []
        for index in range(4):
            value = model(index, 4)
            value.update(sourceSpriteRotates=True, placement='surface')
            values.append(bm.validate_model(value))
        source = {**sprite(), 'noRot': False, 'layers': []}
        self.assertEqual(ss.saved_pose(values[0], {'Sprite': source}, {}, scene.normalize_tint), ('active', None))
        for changed in ({'noRot': True}, {'snapCardinals': True}, {'granularLayersRendering': True},
                        {'postShader': 'custom'}, {'postShaders': ['custom']}, {'texture': 'custom.png'},
                        {'layers': [{'state': 'active', 'copyToShaderParameters': {}}]}):
            with self.subTest(changed=changed):
                self.assertIsNone(ss.saved_pose(values[0], {'Sprite': source}, {'Sprite': changed}, scene.normalize_tint)[0])
        for yaw, direction in ((0, 0), (90, 2), (180, 1), (270, 3)):
            records = {1: entity(1, '', 0, extra={'Map': {}}), 2: entity(2, 'Machine', rotation=yaw)}
            inv = {'prototypes': [{'id': 'Machine', 'classification': 'visual', 'ancestors': [], 'sprite': source}],
                   'tiles': [], 'resources': {}}
            result = scene.build_scene({'maps': [1], 'tilemap': {}}, records, inv, values, {'Machine': {'Sprite': source}})
            selected = result['instances'][0]
            self.assertEqual(selected['modelId'], f'States{direction}')
            self.assertEqual(selected['spriteState'], 'active')
            self.assertAlmostEqual(selected['renderYaw'], math.radians(yaw))
            self.assertEqual(result['geometryVariants'][selected['geometryKey']], values[direction]['parts'])
        for changed in ({'sourceSpriteRotates': 'true'}, {'sourceSpriteRotates': True, 'floorOpening': {'min': '-.2,-.2', 'max': '.2,.2'}},
                        {'placement': 'surface'}):
            with self.subTest(changed=changed), self.assertRaises(ValueError):
                bm.validate_model({**model(), **changed})

    def test_empty_layers_without_state_stay_empty_and_texture_does_not_become_rsi(self):
        value = bm.validate_model(model())
        self.assertEqual(ss.saved_pose(value, {'Sprite': {**sprite(), 'layers': []}}, {}, scene.normalize_tint), ('active', None))
        for source in ({**sprite(), 'layers': [], 'state': None}, {**sprite(), 'layers': [], 'texture': 'x.png'}):
            self.assertIsNone(ss.saved_pose(value, {'Sprite': source}, {}, scene.normalize_tint)[0])

    def test_empty_webbing_placeholder_uses_bare_world_icon_only_when_owner_is_empty(self):
        value = bm.validate_model(model())
        placeholder = {'map': ['enum.WebbingVisualLayers.Base']}
        defaults = {'Sprite': {**sprite(), 'layers': [{'state': 'active'}, placeholder]},
                    'WebbingClothing': {}}
        for saved in ({}, {'ContainerContainer': {'containers': {'cm_clothing_webbing_slot': {'ent': None}}}},
                      {'WebbingClothing': {'container': 'custom'},
                       'ContainerContainer': {'containers': {'custom': {'ents': []}}}}):
            with self.subTest(saved=saved):
                self.assertEqual(ss.saved_pose(value, defaults, saved, scene.normalize_tint), ('active', None))
        for change in ({'WebbingClothing': {'webbing': 7}}, {'WebbingClothing': {'startingWebbing': 'Webbing'}},
                       {'WebbingClothing': {'container': ''}},
                       {'ContainerContainer': {'containers': {'cm_clothing_webbing_slot': {'ent': 7}}}},
                       {'ContainerContainer': {'containers': {'cm_clothing_webbing_slot': {'ents': [7]}}}},
                       {'GenericVisualizer': {'visuals': {'owner': {}}}},
                       {'Appearance': {'data': {'webbing': True}}}):
            for visible in (True, False):
                source = deepcopy(defaults)
                source['Sprite']['layers'][1]['visible'] = visible
                with self.subTest(change=change, visible=visible):
                    self.assertIsNone(ss.saved_pose(value, source, change, scene.normalize_tint)[0])
        for layer in ({'map': ['unknown']}, {**placeholder, 'offset': '1,0'},
                      {**placeholder, 'state': 'active'}, {**placeholder, 'texture': 'webbing.png'}):
            source = deepcopy(defaults)
            source['Sprite']['layers'][1] = layer
            with self.subTest(layer=layer):
                self.assertIsNone(ss.saved_pose(value, source, {}, scene.normalize_tint)[0])
        source = deepcopy(defaults)
        source['Sprite']['layers'][1].update(state='attached', visible=False)
        self.assertIsNone(ss.saved_pose(value, source, {'WebbingClothing': {'webbing': 7}}, scene.normalize_tint)[0])
        del defaults['WebbingClothing']
        self.assertIsNone(ss.saved_pose(value, defaults, {}, scene.normalize_tint)[0])

    def test_stained_world_items_fall_back_and_explicitly_clean_overrides_are_retained(self):
        value = bm.validate_model(model())
        defaults = {'Sprite': sprite(), 'CMUItemStain': {'color': '#A0102080', 'kind': 'Blood'}}
        self.assertIsNone(ss.saved_pose(value, defaults, {}, scene.normalize_tint)[0])
        self.assertEqual(ss.saved_pose(value, defaults, {'CMUItemStain': {'color': None}}, scene.normalize_tint),
                         ('active', None))
        for color in ('#101010', '#00000000', 'invalid'):
            with self.subTest(color=color):
                self.assertIsNone(ss.saved_pose(value, {'Sprite': sprite()},
                    {'CMUItemStain': {'color': color, 'canStain': False}}, scene.normalize_tint)[0])

    def test_map_reader_keeps_saved_clothing_owners_for_appearance_fallback(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'map.yml'
            path.write_text('meta:\n  format: 7\nentities:\n- proto: Uniform\n  entities:\n  - uid: 7\n'
                            '    components:\n    - type: WebbingClothing\n      webbing: 8\n'
                            '      startingWebbing: Webbing\n    - type: CMUItemStain\n      color: "#A0102080"\n'
                            '    - type: HandheldLight\n      activated: true\n'
                            '    - type: ToggleableVisuals\n      spriteLayer: light\n')
            _, records = scene.read_map(path)
            self.assertEqual(records[7]['components']['WebbingClothing']['webbing'], 8)
            self.assertEqual(records[7]['components']['WebbingClothing']['startingWebbing'], 'Webbing')
            self.assertEqual(records[7]['components']['CMUItemStain']['color'], '#A0102080')
            self.assertIs(records[7]['components']['HandheldLight']['activated'], True)
            self.assertEqual(records[7]['components']['ToggleableVisuals']['spriteLayer'], 'light')

    def test_activated_layer_owner_does_not_use_serialized_off_icon(self):
        value = bm.validate_model(model())
        defaults = {'Sprite': {**sprite(), 'layers': [{'state': 'active'}, {'state': 'light', 'visible': False}]},
                    'ToggleableVisuals': {'spriteLayer': 'light'}, 'HandheldLight': {}, 'ItemToggle': {}}
        self.assertEqual(ss.saved_pose(value, defaults, {}, scene.normalize_tint), ('active', None))
        for owner in ('HandheldLight', 'ItemToggle'):
            for active in (True, None, 'invalid'):
                with self.subTest(owner=owner, active=active):
                    self.assertIsNone(ss.saved_pose(value, defaults, {owner: {'activated': active}}, scene.normalize_tint)[0])
            source = {**defaults, owner: {'activated': True}}
            self.assertIsNone(ss.saved_pose(value, source, {}, scene.normalize_tint)[0])
            self.assertEqual(ss.saved_pose(value, source, {owner: {'activated': False}}, scene.normalize_tint), ('active', None))

    def test_scene_selects_direction_and_saved_off_frame_and_exports_only_that_pose(self):
        models = [bm.validate_model(model(i, 4)) for i in range(4)]
        records = {1: entity(1, '', 0, extra={'Map': {}}),
                   2: entity(2, 'Machine', rotation=90, extra={'Sprite': {'state': 'off'}}),
                   3: entity(3, 'Machine', pos='2,0', extra={'Sprite': {'state': 'unknown'}})}
        inv = {'prototypes': [{'id': 'Machine', 'classification': 'visual', 'ancestors': [], 'sprite': sprite()}], 'tiles': [], 'resources': {}}
        result = scene.build_scene({'maps': [1], 'tilemap': {}}, records, inv, models, {'Machine': {'Sprite': sprite()}})
        selected, fallback = result['instances']
        self.assertEqual((selected['modelId'], selected['spriteState'], selected['spriteFrame']), ('States2', 'off', 0))
        self.assertEqual(result['geometryVariants'][selected['geometryKey']][0]['color'], '#2040A0')
        self.assertEqual(selected['position'], [0, 0, 0]); self.assertAlmostEqual(selected['yaw'], math.pi / 2)
        self.assertEqual(fallback['matchKind'], 'unmapped'); self.assertIsNone(fallback['modelId'])
        result['map']['path'] = 'fixture'
        payload, report = export_region(result, models, [0, 0, 0], 4, floors=False)
        self.assertEqual(report['exportedEntities'], 1); self.assertEqual(report['spriteStatePreviewInstances'], 1)
        size = struct.unpack_from('<I', payload, 12)[0]; document = json.loads(payload[20:20 + size])
        self.assertNotIn('animations', document)
        self.assertEqual(document['nodes'][0]['extras']['spriteStatePreview']['state'], 'off')
        # An old scene may still reference a model ID converted from a static asset.
        # It must be regenerated for appearance proof, rather than silently using frame zero.
        stale = deepcopy(result)
        del stale['instances'][0]['spriteState']
        with self.assertRaisesRegex(ValueError, 'No mapped geometry'):
            export_region(stale, models, [0, 0, 0], 4, floors=False)

    def test_global_tint_and_alpha_are_preserved_without_mutating_source_frames(self):
        value = bm.validate_model(model()); original = deepcopy(value)
        instance = {'id': 1, 'modelId': value['id'], 'spriteState': 'active', 'spriteStateTint': '#80FF4080'}
        variants = {}; ss.scene_variants([instance], {value['id']: value}, variants)
        self.assertEqual(variants[instance['geometryKey']][0]['color'], '#50400880')
        self.assertEqual(value, original)
        for baked in ('#AABBCC', '#FFFFFF80', '#00000000'):
            with self.subTest(baked=baked), self.assertRaises(ValueError):
                bm.validate_model({**model(), 'bakedSpriteTint': baked})
        value = bm.validate_model({**model(), 'bakedSpriteTint': '#FFFFFFFF'})
        ss.scene_variants([instance], {value['id']: value}, variants)
        self.assertEqual(variants[instance['geometryKey']][0]['color'], '#50400880')

    def test_direction_groups_and_export_checks_require_matching_source_contract(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); folder = root / 'test.rsi'; rsi(folder, 4)
            values = [model(i, 4) for i in range(4)]
            path = root / 'models.yml'; path.write_text(yaml.safe_dump(values))
            with patch.object(bm, 'resource_file', return_value=folder):
                checked = bm.load_models(path)
                self.assertEqual(len(checked), 4)
                self.assertTrue(bm.glb_bytes(checked[0]).startswith(b'glTF'))
                values[3]['sourceSpriteOffset'] = '0,0'
                path.write_text(yaml.safe_dump(values))
                with self.assertRaisesRegex(ValueError, 'offsets'): bm.load_models(path)

    def test_inventory_counts_authored_compositions_without_claiming_verified_states(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); rsi(root / 'Resources/Textures/test.rsi')
            value = {**model(), '_source': 'model.yml'}
            kinds = {'entity': {'Machine': {'id': 'Machine', '_source': 'entity.yml', 'components': [{'type': 'Sprite', **sprite()}]}},
                     'cmu3DModel': {value['id']: value}}
            report = build_report(root, {'prototypes': []}, kinds, [])
            self.assertEqual(report['summary']['authoredGenericSpriteCompositions'], 3)
            self.assertEqual(report['summary']['authoredGenericSpriteStates'], 2)
            self.assertEqual(report['summary']['authoredGenericSpriteLoops'], 1)
            self.assertEqual(report['summary']['modelsWithAllStatesVerified'], 0)
            self.assertEqual(report['summary']['authoredSourceFrameCompositions'], 3)


if __name__ == '__main__': unittest.main()
