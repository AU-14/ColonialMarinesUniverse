import copy
import math
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import slab_cladding


def part(label='grating', lo=(-.5, -.5, 0), hi=(.5, .5, .035), **more):
    return {'label': label, 'min': list(lo), 'max': list(hi), 'color': '#434540', **more}


def grid_rect(p, offset, yaw):
    lo, hi = p['min'], p['max']
    center = [(a+b)/2 for a, b in zip(lo, hi)]
    half = [(b-a)/2 for a, b in zip(lo, hi)]
    c, s = math.cos(yaw), math.sin(yaw)
    cx, cy = offset[0]+c*center[0]-s*center[1], offset[1]+s*center[0]+c*center[1]
    angle = yaw+math.radians(p.get('yaw', 0))
    ex = abs(math.cos(angle))*half[0]+abs(math.sin(angle))*half[1]
    ey = abs(math.sin(angle))*half[0]+abs(math.cos(angle))*half[1]
    return cx-ex, cy-ey, cx+ex, cy+ey


class SlabCladdingTests(unittest.TestCase):
    def test_four_target_rotations_return_local_geometry_with_unchanged_entity_transform(self):
        source = [part(lo=(-.5, -.25, 0), hi=(.5, .25, .035))]
        original = copy.deepcopy(source)
        opening = (-.1, -.1, .1, .1)
        for turn in range(4):
            yaw = turn*math.pi/2
            offset = (.08, -.03)
            result = slab_cladding.clip_parts(source, offset, yaw, [opening])
            self.assertEqual(len(result), 4)
            self.assertAlmostEqual(sum((p['max'][0]-p['min'][0])*(p['max'][1]-p['min'][1]) for p in result), .46)
            for p in result:
                self.assertEqual(p['color'], '#434540')
                self.assertEqual(p['label'], 'grating')
                self.assertEqual((p['min'][2], p['max'][2]), (0, .035))
                x0, y0, x1, y1 = grid_rect(p, offset, yaw)
                self.assertTrue(min(x1, .1)-max(x0, -.1) < 1e-9 or min(y1, .1)-max(y0, -.1) < 1e-9)
        self.assertEqual(source, original)

    def test_part_cardinal_yaw_is_included_and_output_world_orientation_is_grid_aligned(self):
        source = [part(lo=(-.4, -.2, 0), hi=(.4, .2, .035), yaw=90)]
        result = slab_cladding.clip_parts(source, (0, 0), math.pi/2, [(-.1, -.1, .1, .1)])
        self.assertAlmostEqual(sum((p['max'][0]-p['min'][0])*(p['max'][1]-p['min'][1]) for p in result), .28)
        self.assertTrue(all(p['yaw'] == -90 for p in result))

    def test_actual_ladder_aperture_preserves_outer_grate_and_removes_opaque_underplate(self):
        source = [part('front frame', (-.435, -.5, 0), (.435, -.435, .035)),
                  part('underplate', (-.435, -.435, .005385), (.435, .435, .013462)),
                  part('horizontal bar', (-.435, -.237222, .018846), (.435, -.207222, .029615)),
                  part('vertical bar', (-.148333, -.435, .0175), (-.118333, .435, .028269))]
        opening = (-.28125, -.4375, .28125, -.03125)
        result = slab_cladding.clip_parts(source, (0, 0), 0, [opening])
        self.assertEqual(len(result), 9)
        for fragment in result:
            lo, hi = fragment['min'], fragment['max']
            self.assertTrue(min(hi[0], opening[2])-max(lo[0], opening[0]) <= 1e-12 or
                            min(hi[1], opening[3])-max(lo[1], opening[1]) <= 1e-12)
        self.assertEqual(sum(p['label'] == 'underplate' for p in result), 3)
        self.assertEqual(sum(p['label'] == 'front frame' for p in result), 3)

    def test_unknown_later_part_or_excess_union_rejects_without_mutation(self):
        for change in ({'surface': 'Textured'}, {'shape': 'Ellipsoid'}, {'pitch': 1}, {'yaw': 5},
                       {'max': [.5, .5, .05]}, {'min': [-.5, -.5, -.11]}, {'max': [math.inf, .5, .035]}):
            source = [part(), part('unsupported', **change)]
            original = copy.deepcopy(source)
            with self.subTest(change=change), self.assertRaises(ValueError):
                slab_cladding.clip_parts(source, (0, 0), 0, [(-.1, -.1, .1, .1)])
            self.assertEqual(source, original)
        with self.assertRaises(ValueError):
            slab_cladding.clip_parts([part()], (0, 0), 0, [(-.1, -.1, .1, .1)]*17)

    def test_aggregate_fragment_limit_applies_across_all_parts(self):
        source = [part(str(i)) for i in range(33)]
        with self.assertRaises(ValueError):
            slab_cladding.clip_parts(source, (0, 0), 0, [(-.1, -.1, .1, .1)])
        self.assertEqual(len(source), 33)

    def test_no_cut_retains_original_yaw_and_complete_cut_removes_part(self):
        source = [part(lo=(-.1, -.1, 0), hi=(.1, .1, .035), yaw=180)]
        result = slab_cladding.clip_parts(source, (0, 0), 0, [( .2, .2, .4, .4)])
        self.assertEqual(result, source)
        self.assertIsNot(result[0], source[0])
        self.assertEqual(slab_cladding.clip_parts(source, (0, 0), 0, [(-.2, -.2, .2, .2)]), [])

    def test_nonfinite_offsets_and_rotations_are_rejected(self):
        for offset, yaw in (((math.nan, 0), 0), ((0, 0), math.inf), ((0, 0), math.nan)):
            with self.subTest(offset=offset, yaw=yaw), self.assertRaises(ValueError):
                slab_cladding.clip_parts([part()], offset, yaw, [])

    def test_wrapped_source_turns_keep_fragment_yaw_inside_model_schema(self):
        yaw = math.pi/2 + 10*math.tau
        fragments = slab_cladding.clip_parts([part()], (0, 0), yaw, [(-.1, -.1, .1, .1)])
        for fragment in fragments:
            self.assertAlmostEqual(fragment['yaw'], -90)
            self.assertLessEqual(abs(fragment['yaw']), 180)
            lo_x, lo_y, hi_x, hi_y = grid_rect(fragment, (0, 0), yaw)
            self.assertTrue(min(hi_x, .1)-max(lo_x, -.1) < 1e-9 or min(hi_y, .1)-max(lo_y, -.1) < 1e-9)


if __name__ == '__main__':
    unittest.main()
