"""Closed clothing volumes must preserve occupancy and the four painted sides."""
from pathlib import Path
import sys
import unittest

import numpy as np

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

if __name__ == '__main__':
    unittest.main()
