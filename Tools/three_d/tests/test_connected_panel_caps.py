import copy
import unittest
from build_models import reference_frame, validate_model
from layout import connected_parts


class ConnectedPanelCapTests(unittest.TestCase):
    def test_caps_follow_the_panel_axis_and_do_not_block_straight_or_corner_joins(self):
        parts = [
            {'label': 'pane', 'min': [-.5,-.06,.5], 'max': [.5,.06,2]},
            {'label': 'west cap', 'min': [-.5,-.12,0], 'max': [-.4,.12,2.1], 'omitWhenConnected': 8},
            {'label': 'east cap', 'min': [.4,-.12,0], 'max': [.5,.12,2.1], 'omitWhenConnected': 4},
        ]
        original = copy.deepcopy(parts)
        for mask in range(16):
            with self.subTest(mask=mask):
                result = connected_parts(parts, mask)
                caps = [p for p in result if p['label'].endswith('cap')]
                expected = {0: ['west cap','east cap'], 1: ['west cap'], 2: ['east cap'],
                            4: ['west cap'], 8: ['east cap']}.get(mask, [])
                self.assertEqual([p['label'] for p in caps], expected)
                if mask in (1, 2):
                    self.assertLess(caps[0]['max'][1], 0) if mask == 1 else self.assertGreater(caps[0]['min'][1], 0)
                self.assertTrue(any(p['label'] == 'pane' for p in result))
        self.assertEqual(parts, original)

    def test_cap_metadata_requires_connection_but_no_support_surface(self):
        model = dict(id='Panel', label='Panel', sourcePrototypes=[], connectToNeighbours=True,
                     parts=[dict(min='-.5,-.1,0', max='.5,.1,1', omitWhenConnected=8)])
        self.assertEqual(validate_model(model)['parts'][0]['omitWhenConnected'], 8)
        model['connectToNeighbours'] = False
        with self.assertRaisesRegex(ValueError, 'connected geometry'):
            validate_model(model)

    def test_explicit_frame_reference_does_not_require_a_saved_map_instance(self):
        model = dict(id='Frame', sourcePrototypes=['RMCWindowFrameSPPReinforcedGrey'],
                     referenceRsi='_RMC14/Structures/Windows/Frames/spp_grey_frame.rsi',
                     referenceState='uppwall_window_frame0')
        image, source = reference_frame(model, {})
        self.assertIsNotNone(image)
        self.assertEqual(image.size, (32, 32))
        self.assertEqual(source['state'], 'uppwall_window_frame0')
        self.assertEqual(source['prototype'], 'RMCWindowFrameSPPReinforcedGrey')
        self.assertEqual(source['license'], 'CC-BY-SA-3.0')
