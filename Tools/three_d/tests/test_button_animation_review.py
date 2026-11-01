import json
from pathlib import Path
import struct
import tempfile
import unittest

from PIL import Image

from button_animation_review import ROOT, build_study, extrude, flick_timeline, frame_at, source_frames


class ButtonAnimationReviewTest(unittest.TestCase):
    def test_interval_keyframes_restart_at_half_second_and_hold_final_source_frame(self):
        self.assertEqual(flick_timeline([.2, .2, .1]), [(0, 0), (.2, 1), (.4, 2), (.5, 0), (.7, 1), (.9, 2)])
        self.assertEqual(flick_timeline([.2] * 6 + [.1]), [(0, 0), (.2, 1), (.4, 2), (.5, 0), (.7, 1), (.9, 2), (1.1, 3)])
        self.assertEqual(flick_timeline([.2, .2]), [(0, 0), (.2, 1), (.5, 0), (.7, 1)])
        self.assertEqual(frame_at([.2, .2], 100), 1)

    def test_all_source_pixels_and_power_composites_round_trip_through_solid_front_faces(self):
        samples = 0
        for rsi in ('door_button', 'door_button_br'):
            _, states = source_frames(ROOT / 'Resources/Textures/_RMC14/Objects' / (rsi + '.rsi'))
            overlay = states['doorctrl-p']['frames'][0]
            for entry in states.values():
                for frame in entry['frames']:
                    for expected in (frame, Image.alpha_composite(frame, overlay)):
                        result = Image.new('RGBA', (32, 32))
                        for part in extrude(expected):
                            x0 = round(part['min'][0] * 32 + 16)
                            x1 = round(part['max'][0] * 32 + 16)
                            y = round(16 - (part['max'][2] - 1.3) * 32)
                            color = tuple(int(part['color'][i:i + 2], 16) for i in (1, 3, 5, 7))
                            for x in range(x0, x1):
                                self.assertEqual(result.getpixel((x, y))[3], 0, 'Overlapping source solids')
                                result.putpixel((x, y), color)
                        # Transparent RGB is irrelevant to visible color; compare premultiplied occupancy and RGBA for occupied pixels.
                        self.assertEqual(result.getchannel('A').tobytes(), expected.getchannel('A').tobytes())
                        for y in range(32):
                            for x in range(32):
                                if expected.getpixel((x, y))[3]: self.assertEqual(result.getpixel((x, y)), expected.getpixel((x, y)))
                        samples += 1024
        self.assertEqual(samples, 45056)

    def test_exported_tracks_select_only_expected_frame_and_restore_idle(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory)
            report = build_study(ROOT, output)
            self.assertEqual(report['studyExportedAnimationClips'], 4)
            # Expectations are independent of the generator's timeline helper.
            for study in report['studies']:
                data = (output / study['glb']).read_bytes()
                json_size = struct.unpack_from('<I', data, 12)[0]
                doc = json.loads(data[20:20 + json_size])
                binary = data[28 + json_size:]
                def values(index):
                    acc = doc['accessors'][index]; view = doc['bufferViews'][acc['bufferView']]
                    count = acc['count'] * (3 if acc['type'] == 'VEC3' else 1)
                    return struct.unpack_from(f'<{count}f', binary, view['byteOffset'])
                groups = doc['extras']['frameGroups']
                self.assertEqual([c['name'] for c in doc['animations']], ['Press', 'Denied'])
                for clip in doc['animations']:
                    state = 'doorctrl1' if clip['name'] == 'Press' else 'doorctrl-denied'
                    for time in (0, .199, .201, .401, .501, .701, .901, 1.101, 1.249, 1.251):
                        elapsed = time if time < .5 else time - .5
                        limit = 1 if state == 'doorctrl-denied' else 2 if 'Orange' in study['modelId'] else 6
                        frame = min(int((elapsed + 1e-6) / .2), limit)
                        expected = 'doorctrl:0:powered' if time >= 1.25 else f'{state}:{frame}:powered'
                        active = set()
                        for channel in clip['channels']:
                            sampler = clip['samplers'][channel['sampler']]
                            self.assertEqual(sampler['interpolation'], 'STEP')
                            times = values(sampler['input'])
                            index = max(i for i, t in enumerate(times) if t <= time + 1e-8)
                            scale = values(sampler['output'])[index * 3:index * 3 + 3]
                            if any(scale): active.add(channel['target']['node'])
                        self.assertEqual(active, set(groups[expected]), (study['modelId'], clip['name'], time))
                # Rebuilding produces identical portable files.
                original = data
                second = output / 'again'; build_study(ROOT, second)
                self.assertEqual(original, (second / study['glb']).read_bytes())
