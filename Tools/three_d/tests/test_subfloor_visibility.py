from copy import deepcopy
import math
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from scene import WorldTransforms
from subfloor_visibility import SubfloorVisibility


class SubfloorVisibilityTests(unittest.TestCase):
    def fixture(self, angle=0):
        records = {
            1: dict(prototype='', components={'Map': {}, 'Transform': {}}),
            2: dict(prototype='', components={'MapGrid': {'chunks': {'test': {}}},
                'Transform': {'parent': 1, 'pos': '3, 7', 'rot': str(angle)+'rad'}}),
            3: dict(prototype='DisposalJunction', components={'Transform': {'parent': 2, 'pos': '-.5, .5'}})}
        defaults = {'DisposalJunction': {'Transform': {'anchored': True}, 'SubFloorHide': {}}}
        tiles = {'Plating': {'isSubfloor': True}, 'Steel': {'isSubfloor': False}}
        owner = SubfloorVisibility({'tilemap': {0:'Steel', 1:'Plating'}}, records, defaults, tiles,
            WorldTransforms(records, defaults), lambda chunk: [dict(x=-1, y=0, palette=1)])
        return owner, records, defaults

    def test_grid_rotation_negative_cell_and_covered_neighbor(self):
        for angle in (0, .37, math.pi/2):
            owner, records, _ = self.fixture(angle)
            original = deepcopy(records)
            self.assertEqual(owner.check(3), (True, 'Exposed subfloor tile', 'Plating'))
            self.assertEqual(records, original)
            records[4] = dict(prototype='DisposalJunction', components={'Transform': {'parent':2, 'pos':'.5,.5'}})
            self.assertEqual(owner.check(4), (False, 'Covered by source floor tile', 'Steel'))

    def test_unanchored_source_is_visible_on_covered_floor(self):
        owner, records, _ = self.fixture()
        records[3]['components']['Transform'].update(anchored=False, pos='.5,.5')
        self.assertEqual(owner.check(3)[0], True)

    def test_unknown_metadata_and_layer_owners_do_not_guess_visibility(self):
        owner, _, defaults = self.fixture()
        del owner.definitions['Plating']['isSubfloor']
        self.assertIsNone(owner.check(3)[0])
        defaults['DisposalJunction']['SubFloorHide']['visibleLayers'] = ['pipe']
        self.assertIsNone(owner.check(3)[0])

    def test_bad_chunk_or_grid_is_reported_without_omitting_a_source(self):
        owner, records, _ = self.fixture()
        records[2]['components']['MapGrid']['tileSize'] = 2
        self.assertIsNone(owner.check(3)[0])
        records[2]['components']['MapGrid']['tileSize'] = 1
        owner.decode_chunk = lambda chunk: [dict(x=0,y=0,palette=0)]*2
        self.assertIsNone(owner.check(3)[0])


if __name__ == '__main__': unittest.main()
