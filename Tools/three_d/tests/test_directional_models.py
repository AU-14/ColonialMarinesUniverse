from copy import deepcopy
import math
from pathlib import Path
import sys
import tempfile
import unittest
from PIL import Image
import yaml
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import build_models as bm
import scene
from test_scene import entity


def poses(count=8):
    ids = [f'Paper{i}' for i in range(count)]
    yaws = (0, 180, 90, -90, 45, -45, 135, -135)
    return [dict(type='cmu3DModel', id=uid, label=uid, sourcePrototypes=['Paper'] if i == 0 else [],
                 referencePrototype='Paper', referenceRsi='/Textures/CMU14/N14content/world.rsi',
                 referenceState='scattered_papers', referenceDirection=i, sourceDirections=count,
                 directionalModels=ids.copy(), yawOffset=-yaws[i],
                 parts=[dict(min='-.1,-.1,0', max='.1,.1,.01')]) for i, uid in enumerate(ids)]


class DirectionalModelTests(unittest.TestCase):
    def test_entity_direction_selects_each_pose_and_retains_provenance_and_transform(self):
        for count in (4, 8):
            models = poses(count)
            library = {m['id']: m for m in models}
            angles = (0, 180, 90, -90, 45, -45, 135, -135)
            for frame, angle in enumerate(angles[:count]):
                for turns in (-2, 0, 3):
                    item = dict(modelId='Paper0', matchKind='inherited', matchedPrototype='Paper',
                                position=[3, 7, 0], yaw=math.radians(angle) + turns * math.tau)
                    original = deepcopy(item)
                    self.assertTrue(scene.resolve_direction(item, library))
                    self.assertEqual(item['modelId'], f'Paper{frame}')
                    self.assertEqual(item['sourceDirection'], frame)
                    for key in ('matchKind', 'matchedPrototype', 'position', 'yaw'):
                        self.assertEqual(item[key], original[key])
                    self.assertTrue(scene.resolve_direction(item, library), 'Resolving a selected pose again is stable')

    def test_missing_or_inconsistent_pose_is_an_explicit_marker(self):
        for defect in ('missing', 'mismatched', 'wrong-index', 'nan'):
            models = poses(4)
            library = {m['id']: m for m in models}
            yaw = math.pi / 2
            if defect == 'missing': del library['Paper2']
            if defect == 'mismatched': library['Paper2']['referenceState'] = 'different'
            if defect == 'wrong-index': library['Paper2']['referenceDirection'] = 1
            if defect == 'nan': yaw = math.nan
            item = dict(modelId='Paper0', yaw=yaw)
            self.assertFalse(scene.resolve_direction(item, library))
            self.assertIsNone(item['modelId'])
            self.assertEqual(item['matchKind'], 'unmapped')
            self.assertEqual(item['baseModelId'], 'Paper0')

    def test_scene_selects_before_layout_and_retains_saved_rotation(self):
        inventory = {'prototypes': [{'id': 'Paper', 'classification': 'visual', 'ancestors': [],
                                    'sprite': {'noRot': True}}], 'resources': {}}
        records = {1: entity(1, '', 0, extra={'Map': {}}),
                   2: entity(2, 'Paper', pos='3,4', rotation=90)}
        result = scene.build_scene({'maps': [1], 'tilemap': {}}, records, inventory, poses(4),
                                   defaults={'Paper': {'Sprite': {'noRot': True}}})
        item = result['instances'][0]
        self.assertEqual(item['modelId'], 'Paper2')
        self.assertAlmostEqual(item['yaw'], math.pi / 2)
        self.assertAlmostEqual(item['renderYaw'], 0)
        self.assertEqual(item['position'], [3, 4, 0])

    def test_export_reference_uses_explicit_rsi_slot_not_first_frame(self):
        model = poses()[2]
        reference, details = bm.reference_frame(model, {'Paper': {'sprite': {}}})
        original = Image.open(bm.resource_file(model['referenceRsi']) / 'scattered_papers.png').convert('RGBA')
        self.assertEqual(reference.tobytes(), original.crop((64, 0, 96, 48)).tobytes())
        self.assertNotEqual(reference.tobytes(), original.crop((0, 0, 32, 48)).tobytes())
        self.assertEqual(details['direction'], 2)
        validated = bm.validate_model(model)
        document, _ = bm.glb_document(validated)
        self.assertEqual(document['extras']['referenceDirection'], 2)
        self.assertEqual(document['extras']['directionalModels'], model['directionalModels'])
        import json
        outputs = bm.viewer_outputs([validated], {'Paper': {'sprite': {}}})
        entry = json.loads(outputs['models.json'])['models'][0]
        self.assertEqual(entry['referenceDirection'], 2)
        self.assertEqual(entry['directionalModels'], model['directionalModels'])

    def test_authoring_rejects_broken_pose_links_and_direction_metadata(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / 'models.yml'
            models = poses(4)
            path.write_text(yaml.safe_dump(models))
            self.assertEqual(len(bm.load_models(path)), 4)
            models[2]['referenceState'] = 'different'
            path.write_text(yaml.safe_dump(models))
            with self.assertRaisesRegex(ValueError, 'reciprocal'): bm.load_models(path)
        for change in ({'referenceDirection': 8}, {'referenceDirection': True},
                       {'directionalModels': ['Paper0']}, {'referenceRsi': None}):
            with self.assertRaises(ValueError): bm.validate_model({**poses()[0], **change})
