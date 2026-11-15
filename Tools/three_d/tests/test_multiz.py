from copy import deepcopy
import unittest

from multiz import stack_scenes, stair_parts
from scene_io import read_scene, write_scene
from tempfile import TemporaryDirectory
from pathlib import Path

class MultiZTests(unittest.TestCase):
    def test_stack_preserves_overlapping_saved_ids_and_distinct_geometry_and_materials(self):
        lower = {'map': {'level': -1, 'bounds': [0, 0, 2, 2], 'defaultFocus': [.5, .5, -1]},
                 'instances': [{'id': 1, 'position': [.5, .5, -1], 'geometryKey': 'pose'}],
                 'tiles': [{'x': 0, 'y': 0, 'z': -1, 'grid': 2, 'palette': 1, 'elevation': -.39}],
                 'tilePalette': {'1': {'prototype': 'Metal'}},
                 'geometryVariants': {'pose': [{'min': [0, 0, 0], 'max': [1, 1, 1]}]}}
        upper = deepcopy(lower)
        upper['map']['level'] = 0
        upper['instances'][0]['position'][2] = 0
        upper['tiles'][0]['z'] = 0
        upper['tilePalette']['1']['prototype'] = 'Grass'
        upper['geometryVariants']['pose'][0]['max'][2] = 2
        result = stack_scenes([lower, upper], default_level=0)
        self.assertEqual([e['position'][2] for e in result['instances']], [-3, 0])
        self.assertEqual(len({e['sceneKey'] for e in result['instances']}), 2)
        for entity, expected in zip(result['instances'], [1, 2]):
            self.assertEqual(result['geometryVariants'][entity['geometryKey']][0]['max'][2], expected)
        self.assertEqual([result['tilePalette'][t['palette']]['prototype'] for t in result['tiles']], ['Metal', 'Grass'])
        self.assertEqual(result['tiles'][0]['elevation'], -.39)
        self.assertEqual(lower['instances'][0]['position'][2], -1)
        with TemporaryDirectory() as directory:
            path = Path(directory) / 'scene.json'
            write_scene(result, path)
            loaded = read_scene(path)
            self.assertEqual(loaded['instances'], result['instances'])
            self.assertEqual(loaded['geometryVariants'], result['geometryVariants'])
