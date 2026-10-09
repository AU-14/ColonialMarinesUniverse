"""Closed clothing volumes must preserve occupancy and the four painted sides."""
from pathlib import Path
import sys
import unittest

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).parents[1]))
from equipment_hull import cuboids, hull


class EquipmentHullTest(unittest.TestCase):
    def test_merging_preserves_cavities_disconnected_pieces_and_material_boundaries(self):
        voxels = np.full((7, 6, 5), -1, dtype=np.int16)
        voxels[1:5, 1:5, 1:4] = 0
        voxels[2:4, 2:4, 2:4] = -1
        voxels[4, 1:5, 1:4] = 1
        voxels[6, 0, 0] = 2
        restored = np.full_like(voxels, -1)
        for low, high, color in cuboids(voxels):
            region = tuple(slice(a, b) for a, b in zip(low, high))
            self.assertTrue(np.all(restored[region] == -1), 'Merged volumes overlap')
            restored[region] = color
        np.testing.assert_array_equal(restored, voxels)

    def test_front_back_and_sides_keep_their_paint_on_a_complete_volume(self):
        colors = ['#FF0000', '#0000FF', '#00FF00', '#FFFF00']
        parts = hull([Image.new('RGBA', (6, 6), c) for c in colors], 32, 4)

        def paint(voxel):
            point = ((voxel[0] - 3 + .5) * .05, (voxel[1] - 3 + .5) * .05, (voxel[2] + .5) * .05)
            return next(p['color'] for p in parts if all(a <= v < b for a, v, b in zip(p['min'], point, p['max'])))

        self.assertEqual([paint(p) for p in [(2, 0, 2), (2, 5, 2), (5, 2, 2), (0, 2, 2)]], colors)
        # Every internal voxel and the top/underside remain solid after merging.
        self.assertTrue(all(paint((x, y, z)) in colors for x in range(6) for y in range(6) for z in range(6)))


if __name__ == '__main__':
    unittest.main()
