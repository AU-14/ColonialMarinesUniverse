from copy import deepcopy
import json
import struct
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory

import yaml

import build_models as bm
import barricade_states as states
import scene


class BarricadeStatesTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.model = bm.validate_model(next(m for m in yaml.load((bm.WORLD_SOURCE / 'garrison_plasteel_barricade.yml').read_text(),
                                                              Loader=yaml.CSafeLoader) if m['type'] == 'cmu3DModel'))
        cls.defaults = json.loads((bm.VIEWER / 'plasteel-state-review/report.json').read_text())['resolvedComponents']

    def test_all_eight_damage_and_wire_poses_are_distinct_static_export_scenes(self):
        model = {k:v for k,v in self.model.items() if not k.startswith('barricadeAcid')}
        data = bm.glb_bytes(model)
        size = struct.unpack_from('<I', data, 12)[0]
        document = json.loads(data[20:20+size])
        self.assertNotIn('animations', document)
        self.assertEqual(document['scene'], 0)
        keys = ('0', '4', '8', '12')
        self.assertEqual([s['name'] for s in document['scenes']],
                         [f'DamageOverlay_{key}{suffix}' for suffix in ('', '_wired') for key in keys])
        seen = set()
        for index, state in enumerate(document['scenes']):
            key = keys[index % 4]
            field = 'barricadeWiredStates' if index >= 4 else 'barricadeDamageStates'
            parts = self.model[field][key]['parts']
            if index >= 4:
                dry = self.model['barricadeDamageStates'][key]['parts']
                self.assertEqual(parts[:len(dry)], dry)
                self.assertEqual(len(parts)-len(dry), 23)
            self.assertEqual(len(parts), len(state['nodes']))
            self.assertFalse(seen.intersection(state['nodes']))
            for part, index in zip(parts, state['nodes']):
                x, y, z = [(a+b)/2 for a, b in zip(part['min'], part['max'])]
                self.assertEqual(document['nodes'][index]['translation'], [x, z, -y])
            seen.update(state['nodes'])
        self.assertEqual(seen, set(range(len(document['nodes']))))
        self.assertEqual(data, bm.glb_bytes(model))

    def test_malformed_wire_data_is_rejected(self):
        for defect in ('missing', 'extra', 'empty', 'bad-bounds', 'lost-body', 'different-wire', 'no-rsi', 'no-state', 'budget'):
            with self.subTest(defect=defect):
                model = deepcopy(self.model)
                poses = model['barricadeWiredStates']
                if defect == 'missing': poses.pop('4')
                if defect == 'extra': poses['16'] = deepcopy(poses['12'])
                if defect == 'empty': poses['8']['parts'] = []
                if defect == 'bad-bounds': poses['4']['parts'][-1]['min'] = [float('nan'), 0, 0]
                if defect == 'lost-body': poses['4']['parts'].pop(0)
                if defect == 'different-wire': poses['8']['parts'][-1]['color'] = '#FFFFFF'
                if defect == 'no-rsi': model.pop('barricadeWireRsi')
                if defect == 'no-state': model.pop('barricadeWireState')
                if defect == 'budget': poses['8']['parts'] *= 3
                with self.assertRaises(ValueError): bm.validate_model(model)

    def test_wired_reference_matches_original_layers_in_every_damage_state_and_direction(self):
        from PIL import Image
        for key in ('0', '4', '8', '12'):
            for index, direction in enumerate(('south', 'north', 'east', 'west')):
                expected = Image.open(bm.VIEWER / f'plasteel-state-review/{direction}-damage{key}-wire1-acid-1.png').convert('RGBA')
                actual, evidence = bm.reference_frame({**self.model, 'referenceState': 'DamageOverlay_'+key,
                                                       'referenceDirection': index}, {}, barricade_wired=True)
                self.assertEqual(actual.tobytes(), expected.tobytes(), (key, direction))
                self.assertEqual(evidence['wireLayer']['state'], 'plasteel_wire')

    def test_malformed_pose_data_is_rejected(self):
        for defect in ('missing', 'extra', 'empty', 'bad-bounds', 'default-differs', 'no-reinforcement', 'animation'):
            with self.subTest(defect=defect):
                model = deepcopy(self.model)
                poses = model['barricadeDamageStates']
                if defect == 'missing': poses.pop('4')
                if defect == 'extra': poses['16'] = deepcopy(poses['12'])
                if defect == 'empty': poses['8']['parts'] = []
                if defect == 'bad-bounds': poses['4']['parts'][0]['min'] = [float('nan'), 0, 0]
                if defect == 'default-differs': model['parts'][0]['color'] = '#FF0000'
                if defect == 'no-reinforcement': model.pop('barricadeReinforcementRsi')
                if defect == 'animation': model['frameAnimations'] = {'invented': []}
                with self.assertRaises(ValueError): bm.validate_model(model)

    def test_only_established_dry_snapshot_is_accepted_and_inputs_are_unchanged(self):
        defaults = deepcopy(self.defaults)
        self.assertEqual(states.saved_pose(self.model, defaults, {'Transform': {'rot': 90}}), '0')
        for field in ('Sprite', 'Appearance', 'Barbed', 'Damageable', 'Injurable', 'DamageVisuals', 'SprayAcided', 'Corrodible', 'Flammable'):
            self.assertIsNone(states.saved_pose(self.model, defaults, {field: {}}), field)
        defaults['Barbed']['isBarbed'] = True
        self.assertIsNone(states.saved_pose(self.model, defaults, {}))
        self.assertNotIn('isBarbed', self.defaults['Barbed'])

    def test_saved_state_components_are_read_instead_of_silently_discarded(self):
        with TemporaryDirectory() as tmp:
            path = Path(tmp) / 'map.yml'
            path.write_text('meta:\n  format: 7\nentities:\n- proto: TestBarricade\n  entities:\n  - uid: 1\n    components:\n    - type: Barbed\n      isBarbed: true\n    - type: Damageable\n      damage:\n        Blunt: 250\n    - type: SprayAcided\n      expireAt: 10\n')
            _, records = scene.read_map(path)
            self.assertTrue(records[1]['components']['Barbed']['isBarbed'])
            self.assertEqual(records[1]['components']['Damageable']['damage']['Blunt'], 250)
            self.assertEqual(records[1]['components']['SprayAcided']['expireAt'], 10)

    def test_reference_is_paired_body_and_reinforcement(self):
        from PIL import Image
        expected = Image.open(bm.VIEWER / 'plasteel-state-review/south-damage0-wire0-acid-1.png').convert('RGBA')
        actual, evidence = bm.reference_frame(self.model, {})
        self.assertEqual(actual.tobytes(), expected.tobytes())
        self.assertEqual(evidence['additionalLayer']['state'], 'AdditionalDamageOverlay_0')


if __name__ == '__main__':
    unittest.main()
