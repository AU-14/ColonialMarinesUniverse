from copy import deepcopy
import json
import struct
import unittest
import yaml
import build_models as bm
from layout import inside_wall_parts


class LightStatesTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.models = [bm.validate_model(m) for filename in ('garrison_environment.yml', 'garrison_small_lights.yml', 'garrison_tube_lights.yml')
                      for m in yaml.load((bm.SOURCE / filename).read_text(), Loader=yaml.CSafeLoader)
                      if m.get('poweredLightStates')]

    def test_glb_contains_all_five_disjoint_poses_and_correct_default(self):
        self.assertEqual(len(self.models), 7)
        for model in self.models:
            data = bm.glb_bytes(model)
            size = struct.unpack_from('<I', data, 12)[0]
            doc = json.loads(data[20:20+size])
            self.assertNotIn('animations', doc, 'Random gameplay blinking is not a deterministic RSI strip')
            self.assertEqual(doc['scenes'][doc['scene']]['name'], model['referenceState'])
            seen = set()
            for scene in doc['scenes']:
                parts = model['poweredLightStates'][scene['name']]['parts']
                self.assertEqual(len(scene['nodes']), len(parts))
                self.assertFalse(seen.intersection(scene['nodes']))
                seen.update(scene['nodes'])
                for part, index in zip(parts, scene['nodes']):
                    x,y,z = [(a+b)/2 for a,b in zip(part['min'],part['max'])]
                    self.assertEqual(doc['nodes'][index]['translation'], [x,z,-y])
                for source, mounted in zip(parts, inside_wall_parts(parts)):
                    self.assertGreaterEqual(mounted['min'][1], -.499001)
                    self.assertAlmostEqual(mounted['max'][1], -1-source['min'][1])
            self.assertEqual(seen, set(range(len(doc['nodes']))))
            self.assertEqual(data, bm.glb_bytes(model))

    def test_invalid_or_incomplete_state_tables_are_rejected(self):
        for defect in ('missing', 'extra', 'empty', 'bounds', 'budget', 'default', 'directions', 'wall'):
            with self.subTest(defect=defect):
                model = deepcopy(self.models[0]); states = model['poweredLightStates']; key = next(iter(states))
                if defect=='missing': states.pop(key)
                if defect=='extra': states['unreviewed'] = states[key]
                if defect=='empty': states[key]['parts'] = []
                if defect=='bounds': states[key]['parts'][0]['min'] = [float('nan'),0,0]
                if defect=='budget': states[key]['parts'] *= 30
                if defect=='default': model['parts'] = model['parts'][:-1]
                if defect=='directions': model['sourceDirections'] = 1
                if defect=='wall': model['wallMounted'] = False
                with self.assertRaises(ValueError): bm.validate_model(model)

    def test_tube_states_keep_empty_and_broken_openings_and_do_not_mix_families(self):
        for model in self.models:
            if 'TubeLight' not in model['id'] and model['id'] != 'CMU3DWallBlueDoubleLight':
                continue
            prefix=model['referenceState'][:-1]
            empty=model['poweredLightStates'][prefix+'-empty']['parts']
            broken=model['poweredLightStates'][prefix+'-broken']['parts']
            self.assertFalse(any('glass' in p['label'].lower() for p in empty))
            self.assertTrue(any('retained glass' in p['label'].lower() for p in broken))
            self.assertFalse(any(p['min'][0] < 0 < p['max'][0] and 'glass' in p['label'].lower() for p in broken))
            wrong=deepcopy(model)
            wrong['poweredLightStates']['bulb1']=wrong['poweredLightStates'].pop(prefix+'1')
            with self.assertRaises(ValueError):bm.validate_model(wrong)

    def test_empty_socket_has_no_bulb_and_red_tint_applies_once_to_all_parts(self):
        by_id = {m['id']:m for m in self.models}
        empty = by_id['CMU3DSmallEmptyWallLight']
        self.assertEqual(empty['referenceState'],'bulb-empty')
        self.assertFalse(any('glass' in p['label'].lower() or 'bulb' in p['label'].lower() for p in empty['parts']))
        warm,red = by_id['CMU3DSmallWallLight'],by_id['CMU3DSmallRedWallLight']
        for state in warm['poweredLightStates']:
            for a,b in zip(warm['poweredLightStates'][state]['parts'],red['poweredLightStates'][state]['parts']):
                self.assertEqual(a['min'],b['min']); self.assertEqual(a['max'],b['max'])
                expected='#'+''.join(f'{round(int(a["color"][i:i+2],16)*int("#C02526"[i:i+2],16)/255):02X}' for i in (1,3,5))
                self.assertEqual(b['color'],expected)
