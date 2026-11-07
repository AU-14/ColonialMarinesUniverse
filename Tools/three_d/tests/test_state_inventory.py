import json
from pathlib import Path
import struct
import tempfile
import unittest

from state_inventory import build_report, glb_animations, resource_states, sprite_layers, state_fields

class StateInventoryTest(unittest.TestCase):

    def test_glb_clip_scan_distinguishes_missing_and_malformed_exports(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'test.glb'
            self.assertFalse(glb_animations(path)['exists'])
            path.write_bytes(b'broken')
            self.assertIn('issue', glb_animations(path))
            payload = json.dumps({'animations': [{'name': 'Press', 'channels': [{}]}]}).encode()
            path.write_bytes(struct.pack('<5I', 0x46546C67, 2, 20 + len(payload), len(payload), 0x4E4F534A) + payload)
            self.assertEqual(glb_animations(path)['clips'], [{'name': 'Press', 'channels': 1}])
