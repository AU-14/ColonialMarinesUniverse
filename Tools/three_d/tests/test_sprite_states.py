from copy import deepcopy
from pathlib import Path
import struct
import sys
import unittest

sys.path[:0] = [str(Path(__file__).resolve().parents[1]), str(Path(__file__).resolve().parent)]
import build_models as bm
import sprite_states as ss

def model(direction=0, directions=1):
    def frame(color):
        return {'parts': [{'min': [-.4, -.3, .1], 'max': [.4, .3, 1.2], 'color': color}]}
    frames = [frame('#A04020'), frame('#20A040')]
    value = dict(id=f'States{direction}', label='State test', type='cmu3DModel', sourcePrototypes=['Machine'] if direction == 0 else [],
                 sourceDirections=directions, referenceRsi='test.rsi', referenceState='active', sourceSpriteOffset='.25,.5',
                 parts=deepcopy(frames[0]['parts']), spriteStates={'active': {'frames': frames, 'delays': [.1, .2]},
                                                                 'off': {'frames': [frame('#2040A0')], 'delays': [1]}})
    if directions == 4:
        value.update(referenceDirection=direction, directionalModels=[f'States{i}' for i in range(4)])
    return value

class SpriteStateTests(unittest.TestCase):

    def test_portable_step_loop_and_static_off_scene_are_separate(self):
        value = bm.validate_model(model())
        doc, binary = ss.model_document(value, bm.glb_document)
        self.assertEqual([a['name'] for a in doc['animations']], ['active'])
        self.assertTrue(doc['animations'][0]['extras']['loop'])
        self.assertAlmostEqual(doc['animations'][0]['extras']['durationSeconds'], .3)
        def read(index):
            a = doc['accessors'][index]; view = doc['bufferViews'][a['bufferView']]
            width = 1 if a['type'] == 'SCALAR' else 3
            return struct.unpack_from('<' + 'f' * a['count'] * width, binary, view['byteOffset'])
        for time, expected in [(0, 'active:0'), (.1001, 'active:1'), (.299, 'active:1'), (.3001, 'active:0')]:
            active = []
            for channel in doc['animations'][0]['channels']:
                sampler = doc['animations'][0]['samplers'][channel['sampler']]
                self.assertEqual(sampler['interpolation'], 'STEP')
                times, values = read(sampler['input']), read(sampler['output'])
                frame = max(i for i, t in enumerate(times) if t <= time)
                if any(values[frame * 3:frame * 3 + 3]): active.append(channel['target']['node'])
            self.assertEqual(active, doc['extras']['spriteFrameGroups'][expected])
        static = doc['scenes'][doc['extras']['spriteStaticScenes']['off']]['nodes']
        self.assertTrue(all(any(doc['nodes'][i]['scale']) for i in static))
        self.assertFalse(set(static) & set(doc['scenes'][0]['nodes']))
        value['spriteStates'] = {'off': value['spriteStates']['off']}
        value['referenceState'] = 'off'; value['parts'] = value['spriteStates']['off']['frames'][0]['parts']
        self.assertNotIn('animations', ss.model_document(value, bm.glb_document)[0])

if __name__ == '__main__': unittest.main()
