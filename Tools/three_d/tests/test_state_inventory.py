import json
from pathlib import Path
import struct
import tempfile
import unittest

from state_inventory import build_report, glb_animations, resource_states, sprite_layers, state_fields


class StateInventoryTest(unittest.TestCase):
    def test_explicit_layers_replace_implicit_state_and_keep_hidden_power_layer(self):
        layers = sprite_layers({'sprite': 'buttons.rsi', 'state': 'unused', 'color': '#123456', 'layers': [
            {'state': 'idle', 'map': ['Animation']}, {'state': 'off', 'visible': False},
            {'rsi': 'other.rsi', 'state': 'blink', 'color': '#ABCDEF'}]})
        self.assertEqual([layer['state'] for layer in layers], ['idle', 'off', 'blink'])
        self.assertFalse(layers[1]['visibleByDefault'])
        self.assertEqual(layers[2]['rsi'], '/Textures/other.rsi')
        self.assertEqual(layers[2]['spriteColor'], '#123456')
        self.assertEqual(sprite_layers({'layers': [], 'state': 'unused'}), [])

    def test_state_fields_preserve_visualizer_branch_provenance(self):
        fields = state_fields({'Visualizer': {'visuals': {'Powered': {'Layer': {False: {'state': 'off'}}}}},
                               'Door': {'openState': 'open'}, 'Random': {'states': ['a', 'b']}})
        self.assertEqual(fields, [{'field': 'Visualizer.visuals.Powered.Layer.False.state', 'value': 'off'},
                                  {'field': 'Door.openState', 'value': 'open'}, {'field': 'Random.states', 'value': ['a', 'b']}])

    def test_frame_delays_and_direction_counts_are_retained(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            path = root / 'Resources/Textures/example.rsi'; path.mkdir(parents=True)
            meta = {'size': {'x': 32, 'y': 32}, 'states': [{'name': 'static'},
                    {'name': 'motion', 'directions': 4, 'delays': [[.2, .3], [.5], [.1, .2, .2], [.5]]}]}
            (path / 'meta.json').write_text(json.dumps(meta))
            for name in ('static', 'motion'): (path / (name + '.png')).touch()
            info = resource_states(root, '/Textures/example.rsi')
            self.assertEqual(info['issues'], [])
            self.assertIsNone(info['states'][0]['durationSeconds'])
            motion = info['states'][1]
            self.assertEqual(motion['framesPerDirection'], [2, 1, 3, 1])
            self.assertEqual(motion['durationSeconds'], [.5] * 4)
            self.assertTrue(motion['animated'])
            meta['states'][1]['delays'][0][0] = 0
            (path / 'meta.json').write_text(json.dumps(meta))
            self.assertEqual(resource_states(root, '/Textures/example.rsi')['issues'], ['Invalid frame timing: motion'])

    def test_glb_clip_scan_distinguishes_missing_and_malformed_exports(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'test.glb'
            self.assertFalse(glb_animations(path)['exists'])
            path.write_bytes(b'broken')
            self.assertIn('issue', glb_animations(path))
            payload = json.dumps({'animations': [{'name': 'Press', 'channels': [{}]}]}).encode()
            path.write_bytes(struct.pack('<5I', 0x46546C67, 2, 20 + len(payload), len(payload), 0x4E4F534A) + payload)
            self.assertEqual(glb_animations(path)['clips'], [{'name': 'Press', 'channels': 1}])

    def test_off_map_alternate_does_not_claim_complete_states(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            kinds = {'entity': {'Control': {'id': 'Control', '_source': 'control.yml', 'components': [
                {'type': 'Sprite', 'sprite': 'missing.rsi', 'state': 'idle'}]}},
                'cmu3DModel': {'ControlDraft': {'id': 'ControlDraft', '_source': 'model.yml',
                                               'referencePrototype': 'Control', 'referenceState': 'pressed',
                                               'referenceRsi': 'missing.rsi'}}}
            report = build_report(root, {'prototypes': []}, kinds, [])
            self.assertEqual(report['summary']['modelCount'], 1)
            self.assertEqual(report['summary']['reduxVisualPrototypes'], 0)
            self.assertEqual(report['models'][0]['resources'], ['/Textures/missing.rsi'])
            self.assertFalse(report['models'][0]['allStatesVerified'])
            self.assertFalse(report['scope']['fullSourceStateInventoryComplete'])
            self.assertEqual(report['summary']['resourceIssues'], 1)
            self.assertEqual(report['summary']['exportIssues'], 1)
