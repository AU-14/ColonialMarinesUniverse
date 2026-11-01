from copy import deepcopy
import unittest

from multiz import stack_scenes, stair_parts
from author_multiz_elevation import fit_approaches
from elevation import Field
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

    def test_stair_curve_turns_with_game_direction_and_has_solid_sides(self):
        curve = [1.05, 1.05, .575, .1]
        south = stair_parts(curve, 0)
        east = stair_parts(curve, 1.5707963267948966)
        self.assertGreater(south[-1]['max'][2], south[0]['max'][2])
        self.assertGreater(east[0]['max'][2], east[-1]['max'][2])
        for part in south + east:
            self.assertEqual(part['min'][2], 0)
            self.assertTrue(all(a < b for a, b in zip(part['min'], part['max'])))
        self.assertAlmostEqual(south[-1]['max'][2], 3.15)

    def test_decorative_approaches_meet_physics_and_descend_to_upper_landing(self):
        def step(uid, x, level):
            return {'id': uid, 'prototype': 'FlightStairs', 'modelId': 'Stairs', 'position': [x, .5, level], 'yaw': -1.5707963267948966}
        lower = {'instances': [step(1, -.5, 0), step(2, 1.5, 0),
                              {'id': 3, 'prototype': 'CMUMultiZStairsFlight', 'position': [.5, .5, 0],
                               'yaw': -1.5707963267948966,
                               'zStair': {'heightCurve': [1.05, 1.05, .575, .1]}}],
                 'tiles': [{'x': x, 'y': 0} for x in range(-2, 3)]}
        upper = {'instances': [step(1, 1.5, 1)], 'tiles': [{'x': x, 'y': 0} for x in range(3)]}
        profiles = {0: {'regions': [{'bounds': '-2,0,-1,1', 'height': .39}]}, 1: {}}
        report = fit_approaches({0: lower, 1: upper}, profiles)
        bottom, top = Field(profiles[0]), Field(profiles[1])
        self.assertAlmostEqual(bottom.height(-.999999, .5), .39, places=5)
        self.assertAlmostEqual(bottom.height(-.000001, .5), 1.26, places=5)
        self.assertAlmostEqual(top.height(1, .5), -.87)
        self.assertAlmostEqual(top.height(1.999999, .5), 0, places=5)
        self.assertIn(((1, 0), 'FlightStairs'), bottom.suppressed)
        self.assertIn((0, 0), top.openings)
        self.assertEqual(report[0]['suppressedProjections'], [2])
