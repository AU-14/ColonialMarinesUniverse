from copy import deepcopy
import math
import unittest

from elevation import Field, apply_scene
from scene import WorldTransforms


class ElevationTests(unittest.TestCase):
    def test_rotated_grid_carries_floor_fixture_and_stairs_without_changing_saved_level(self):
        profile = {'regions': [{'bounds': '0, 1, 2, 3', 'height': .39}],
                   'ramps': [{'tile': '0, 0', 'direction': '0, 1', 'bottom': 0, 'top': .39,
                              'sourcePrototype': 'Stairs'}]}
        records = {1: {'prototype': '', 'components': {'MapGrid': {},
                       'Transform': {'pos': '10,20', 'rot': '90'}, 'CMU3DElevation': {'profile': 'Raised'}}},
                   2: {'prototype': 'Lamp', 'components': {'Transform': {'parent': 1}}},
                   3: {'prototype': 'Stairs', 'components': {'Transform': {'parent': 1}}}}
        entities = [{'id': 2, 'prototype': 'Lamp', 'position': [8.5, 20.5, -2],
                     'modelId': 'Lamp', 'renderOffset': [0, 0, 1.2]},
                    {'id': 3, 'prototype': 'Stairs', 'position': [9.5, 20.5, -2], 'modelId': 'Stairs'}]
        tiles = [{'grid': 1, 'x': 9, 'y': 20, 'z': -2, 'yaw': math.pi/2}]
        library = {'Stairs': {'parts': [{'min': [-.5, -.5, 0], 'max': [.5, .5, .8]}]}}
        original = deepcopy(library)
        variants = {}
        apply_scene(entities, tiles, records, WorldTransforms(records), library, variants, {'Raised': profile})
        self.assertEqual(entities[0]['position'], [8.5, 20.5, -2])
        self.assertAlmostEqual(entities[0]['renderOffset'][2], 1.59)
        self.assertEqual(tiles[0]['z'], -2)
        self.assertEqual(tiles[0]['elevation'], .39)
        self.assertEqual(tiles[0]['foundationDepth'], .46)
        self.assertAlmostEqual(variants[entities[1]['geometryKey']][0]['max'][2], .39)
        self.assertEqual(library, original)

    def test_recessed_stair_is_continuous_at_both_landings_and_negative_cells(self):
        field = Field({'regions': [{'bounds': '-2, -1, 0, 0', 'height': -.39}],
                       'ramps': [{'tile': '-1, -1', 'direction': '1, 0', 'bottom': -.39, 'top': 0,
                                  'sourcePrototype': 'Stairs'}]})
        for x, expected in [(-1.001, -.39), (-1, -.39), (-.5, -.195), (-.000001, -.00000039), (0, 0)]:
            with self.subTest(x=x):
                self.assertAlmostEqual(field.height(x, -.5), expected)

    def test_overlapping_regions_and_conflicting_ramps_are_rejected(self):
        with self.assertRaises(ValueError):
            Field({'regions': [{'bounds': '0,0,2,2', 'height': .39}, {'bounds': '1,1,3,3', 'height': -.39}]})
        with self.assertRaises(ValueError):
            Field({'ramps': [{'tile': '0,0', 'direction': '1,1', 'bottom': 0, 'top': .39}]})
