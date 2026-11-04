"""Selected states must resolve before facing/placement; absent choices must never use default tree art."""
from copy import deepcopy
import math
from pathlib import Path
import sys
import tempfile
import unittest
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import build_models
import scene
from test_scene import entity
from test_coverage import coverage, fixture


def variants():
    return [{'id': f'Tree{i}', 'label': f'Tree {i}', 'type': 'cmu3DModel', 'sourcePrototypes': [],
             'randomSpritePrototypes': ['Tree'], 'randomSpriteLayer': 'random',
             'referenceRsi': '/Textures/Trees.rsi', 'referenceState': f'tree0{i}',
             'parts': [{'min': '-.1,-.1,0', 'max': '.1,.1,2', 'color': '#808080'}],
             'sourceDirections': 1, 'groundOffset': '.125,0'} for i in range(1, 7)]


class RandomSpriteTests(unittest.TestCase):
    def test_both_engine_tuple_forms_choose_all_six_variants_and_white_aliases(self):
        index = scene.random_sprite_index(variants())['Tree']
        for i in range(1, 7):
            for color in (None, 'white', '#FFF', '#FFFF', '#FFFFFF', '#FFFFFFFF'):
                for choice in ([f'tree0{i}', color], {f'tree0{i}': color}):
                    item = {'prototype': 'Tree', 'modelId': None}
                    self.assertTrue(scene.resolve_random_sprite(item, {'random': choice}, index))
                    self.assertEqual((item['modelId'], item['referenceState'], item['matchKind']),
                                     (f'Tree{i}', f'tree0{i}', 'exact'))

    def test_unknown_malformed_extra_and_tinted_selections_remain_markers(self):
        index = scene.random_sprite_index(variants())['Tree']
        choices = [None, {}, 'tree01', {'random': 'tree01'}, {'random': ['tree01']},
                   {'random': ['tree01', None, None]}, {'random': {'tree01': None, 'tree02': None}},
                   {'random': ['missing', None]}, {'other': ['tree01', None]},
                   {'random': ['tree01', '#FFFFFF80']}, {'random': ['tree01', 'red']},
                   {'random': ['tree01', 123]}, {'random': ['tree01', 'bad-color']},
                   {'random': ['tree01', None], 'other': ['tree01', None]}]
        for choice in choices:
            with self.subTest(choice=choice):
                item = {'prototype': 'Tree', 'modelId': 'WrongDefault', 'matchKind': 'inherited'}
                self.assertFalse(scene.resolve_random_sprite(item, choice, index))
                self.assertIsNone(item['modelId'])
                self.assertEqual(item['matchKind'], 'unmapped')
                self.assertEqual(item['baseModelId'], 'WrongDefault')
                self.assertIn('unsupportedState', item)

    def test_scene_resolves_before_facing_and_ignores_prototype_default_choice(self):
        inventory = {'prototypes': [{'id': 'Tree', 'classification': 'visual', 'ancestors': []}], 'resources': {}}
        records = {1: entity(1, '', 0, extra={'Map': {}}),
                   2: entity(2, 'Tree', pos='3,4', rotation=90, extra={'RandomSprite': {'selected': {'random': ['tree06', None]}}}),
                   3: entity(3, 'Tree', pos='5,6', rotation=90)}
        defaults = {'Tree': {'Sprite': {'noRot': True, 'offset': '0,1.55'},
                             'RandomSprite': {'selected': {'random': ['tree01', None]}}}}
        result = scene.build_scene({'maps': [1], 'tilemap': {}}, records, inventory, variants(), defaults)
        known, unknown = result['instances']
        self.assertEqual((known['modelId'], known['position'], known['renderYaw']), ('Tree6', [3, 4, 0], 0))
        self.assertAlmostEqual(known['yaw'], math.pi / 2)
        self.assertEqual(known['renderOffset'], [.125, 0, 0])
        self.assertEqual(known['unappliedStateComponents'], [])
        self.assertIsNone(unknown['modelId'])
        self.assertEqual(unknown['position'], [5, 6, 0])
        self.assertEqual(result['diagnostics']['randomSpritePoses'], {'modeled': 1, 'Random sprite selection is not saved': 1})

    def test_saved_map_reader_retains_selected_tuple(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'map.yml'
            path.write_text('meta:\n  format: 7\nentities:\n- proto: Tree\n  entities:\n  - uid: 2\n    components:\n'
                            '    - type: RandomSprite\n      selected:\n        random: [tree06, null]\n')
            _, records = scene.read_map(path)
            self.assertEqual(records[2]['components']['RandomSprite']['selected'], {'random': ['tree06', None]})

    def test_conditional_metadata_exports_without_promoting_static_coverage(self):
        model = variants()[0]
        model['randomSpritePrototypes'] = ['Chair']
        model = build_models.validate_model(model)
        doc, _ = build_models.glb_document(model)
        self.assertEqual(doc['extras']['randomSpritePrototypes'], ['Chair'])
        self.assertEqual(doc['extras']['randomSpriteLayer'], 'random')
        report = coverage.calculate_coverage(fixture(), [model])
        self.assertEqual(report['summary']['classic']['categories']['unmapped']['prototypes'], 1)
        self.assertEqual(report['randomSpriteMappings'][0]['state'], 'tree01')

    def test_duplicate_and_incomplete_bindings_are_rejected(self):
        models = variants()
        duplicate = {**deepcopy(models[0]), 'id': 'Duplicate'}
        with self.assertRaisesRegex(ValueError, 'Duplicate random sprite'):
            scene.random_sprite_index([*models, duplicate])
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'trees.yml'
            path.write_text(build_models.yaml.safe_dump([*models, duplicate]))
            with self.assertRaisesRegex(ValueError, 'Duplicate random sprite'):
                build_models.load_models(Path(directory))
        for change in ({'randomSpriteLayer': None}, {'referenceRsi': None},
                       {'randomSpritePrototypes': ['Tree', 'Tree']}):
            with self.assertRaises(ValueError):
                build_models.validate_model({**models[0], **change})


if __name__ == '__main__':
    unittest.main()
