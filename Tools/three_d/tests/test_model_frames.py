from copy import deepcopy
import json
import struct
import unittest

from PIL import Image
import yaml

import build_models as bm
from button_animation_review import source_frames


class ModelFrameTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.models = [bm.validate_model(m) for m in yaml.load((bm.WORLD_SOURCE / 'garrison_architecture.yml').read_text(encoding='utf-8'), Loader=yaml.CSafeLoader)
                      if m.get('doorButtonStates')]

    def test_all_authored_frame_solids_preserve_source_projection(self):
        self.assertEqual(len(self.models), 2)
        samples = 0
        for model in self.models:
            _, source = source_frames(bm.ROOT / 'Resources/Textures' / model['referenceRsi'])
            overlay = source['doorctrl-p']['frames'][0]
            self.assertEqual(set(model['doorButtonStates']), set(source))
            for name, state in model['doorButtonStates'].items():
                for kind in ('frames', 'unpoweredFrames'):
                    self.assertEqual(len(state[kind]), len(source[name]['frames']))
                    for index, frame in enumerate(state[kind]):
                        expected = source[name]['frames'][index]
                        if kind == 'unpoweredFrames': expected = Image.alpha_composite(expected, overlay)
                        actual = Image.new('RGBA', (32, 32))
                        for part in frame['parts']:
                            self.assertAlmostEqual(part['min'][1], -.562)
                            self.assertAlmostEqual(part['max'][1], -.502)
                            left = round(part['min'][0] * 32 + 16)
                            right = round(part['max'][0] * 32 + 16)
                            # CMU14: Coalesced solids can span several source rows; compare their full projection.
                            top = round(16 - (part['max'][2] - 1.335) * 32)
                            bottom = round(16 - (part['min'][2] - 1.335) * 32)
                            color = tuple(round(v * 255) for v in bm.rgba(part['color']))
                            for row in range(top, bottom):
                                for x in range(left, right):
                                    self.assertEqual(actual.getpixel((x, row))[3], 0)
                                    actual.putpixel((x, row), color)
                        self.assertEqual(actual.getchannel('A').tobytes(), expected.getchannel('A').tobytes())
                        for y in range(32):
                            for x in range(32):
                                if expected.getpixel((x, y))[3]: self.assertEqual(actual.getpixel((x, y)), expected.getpixel((x, y)))
                        samples += 1024
        self.assertEqual(samples, 45056)

    def test_invalid_clip_and_frame_data_are_rejected_before_export(self):
        for defect in ('missing-state', 'missing-frame', 'negative-frame', 'unordered', 'nonzero-start',
                       'nonfinite-time', 'power-count', 'empty-frame', 'default-mismatch', 'orphan-timeline'):
            with self.subTest(defect=defect):
                model = deepcopy(self.models[0])
                keys = model['frameAnimations']['Press']
                if defect == 'missing-state': keys[0]['state'] = 'does-not-exist'
                elif defect == 'missing-frame': keys[0]['frame'] = 99
                elif defect == 'negative-frame': keys[0]['frame'] = -1
                elif defect == 'unordered': keys[1]['time'] = 0
                elif defect == 'nonzero-start': keys[0]['time'] = .1
                elif defect == 'nonfinite-time': keys[1]['time'] = float('nan')
                elif defect == 'power-count': model['doorButtonStates']['doorctrl1']['unpoweredFrames'].pop()
                elif defect == 'empty-frame': model['doorButtonStates']['doorctrl1']['frames'][0]['parts'] = []
                elif defect == 'default-mismatch': model['parts'][0]['color'] = '#FF00FF'
                elif defect == 'orphan-timeline': model['doorButtonStates'] = {}
                with self.assertRaises(ValueError): bm.validate_model(model)

    def test_library_clips_select_current_geometry_then_restore_default(self):
        for model in self.models:
            payload = bm.glb_bytes(model)
            size = struct.unpack_from('<I', payload, 12)[0]
            doc = json.loads(payload[20:20 + size]); binary = payload[28 + size:]
            def values(index):
                accessor = doc['accessors'][index]; view = doc['bufferViews'][accessor['bufferView']]
                width = 3 if accessor['type'] == 'VEC3' else 1
                return struct.unpack_from(f"<{accessor['count'] * width}f", binary, view['byteOffset'])
            self.assertEqual([clip['name'] for clip in doc['animations']], ['Press', 'Denied'])
            self.assertEqual(sum(any(n['scale']) for n in doc['nodes']), len(model['parts']))
            for clip in doc['animations']:
                state = 'doorctrl1' if clip['name'] == 'Press' else 'doorctrl-denied'
                for time, frame in [(0, 0), (.501, 0), (.701, 1), (1.251, None)]:
                    active = set()
                    for channel in clip['channels']:
                        sampler = clip['samplers'][channel['sampler']]
                        times = values(sampler['input']); index = max(i for i, t in enumerate(times) if t <= time + 1e-8)
                        if any(values(sampler['output'])[index * 3:index * 3 + 3]): active.add(channel['target']['node'])
                    key = 'doorctrl:0:powered' if frame is None else f'{state}:{frame}:powered'
                    self.assertEqual(active, set(doc['extras']['frameGroups'][key]))
            self.assertEqual(payload, bm.glb_bytes(model))
