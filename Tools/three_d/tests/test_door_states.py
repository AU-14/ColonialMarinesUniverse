from copy import deepcopy
import json, struct, unittest
import build_models as bm
from door_states import timeline


def model():
    base = dict(type='cmu3DModel', id='TestShutter', label='Test shutter', sourcePrototypes=['Test'],
                sourceDirections=4, referenceRsi='/Textures/test.rsi', referenceState='closed')
    def frame(height):
        return {'parts': [dict(min=[-.5, -.0625, height], max=[.5, .0625, 2.74], color='#607050')]}
    states = {s: dict(frames=[frame(.1 if s == 'closed' else 2.5)], delays=[1]) for s in ('closed', 'open')}
    for s in ('opening', 'closing'):
        heights = [.1, .5, .9, 1.3, 1.9, 2.5]
        if s == 'closing': heights.reverse()
        states[s] = dict(frames=[frame(h) for h in heights], delays=[.1]*6)
    return {**base, 'parts': states['closed']['frames'][0]['parts'], 'doorSpriteStates': states,
            'doorAnimationDurations': {'opening': 1., 'closing': 1.}}


class DoorStateTests(unittest.TestCase):
    def test_strip_holds_last_frame_until_owner_completion(self):
        m = bm.validate_model(model())
        for state, settled in [('opening', 'open'), ('closing', 'closed')]:
            keys = timeline(m, state)
            self.assertEqual([k['time'] for k in keys], [0, .1, .2, .3, .4, .5, 1.])
            self.assertEqual(keys[-2]['frame'], state + ':5')
            self.assertEqual(keys[-1]['frame'], settled + ':0')

    def test_incomplete_unknown_or_mounted_depth_changing_frames_rejected(self):
        for defect in ('missing', 'unknown', 'delay', 'short', 'nan', 'default', 'depth', 'overlay'):
            with self.subTest(defect=defect):
                m = deepcopy(model())
                if defect == 'missing': del m['doorSpriteStates']['opening']
                if defect == 'unknown': m['doorSpriteStates']['denying'] = m['doorSpriteStates']['closed']
                if defect == 'delay': m['doorSpriteStates']['opening']['delays'][2] = 0
                if defect == 'short': m['doorAnimationDurations']['opening'] = .5
                if defect == 'nan': m['doorAnimationDurations']['closing'] = float('nan')
                if defect == 'default': m['parts'] = m['doorSpriteStates']['open']['frames'][0]['parts']
                if defect == 'depth':
                    m['windowMountTargets'] = ['TestGlass']
                    m['doorSpriteStates']['opening']['frames'][2]['parts'][0]['max'][1] = .09
                if defect == 'overlay': m['poweredLightStates'] = {'bulb0': {}}
                with self.assertRaises(ValueError): bm.validate_model(m)

    def test_glb_tracks_show_one_frame_at_boundaries_and_completion(self):
        m = bm.validate_model(model())
        data = bm.glb_bytes(m)
        size = struct.unpack_from('<I', data, 12)[0]
        doc, binary = json.loads(data[20:20+size]), data[28+size:]
        groups = doc['extras']['doorSpriteFrameGroups']
        def read(index):
            a = doc['accessors'][index];v = doc['bufferViews'][a['bufferView']]
            width = 1 if a['type'] == 'SCALAR' else 3
            values = struct.unpack_from('<' + 'f'*(a['count']*width), binary, v.get('byteOffset', 0)+a.get('byteOffset', 0))
            return [values[i:i+width] for i in range(0, len(values), width)]
        for clip in doc['animations']:
            state = clip['name'].lower()
            for time, expected in [(0, state+':0'), (.101, state+':1'), (.599, state+':5'),
                                   (.999, state+':5'), (1., ('open' if state == 'opening' else 'closed')+':0')]:
                active = []
                for channel in clip['channels']:
                    sampler = clip['samplers'][channel['sampler']]
                    self.assertEqual(sampler['interpolation'], 'STEP')
                    times, values = read(sampler['input']), read(sampler['output'])
                    key = max(i for i, value in enumerate(times) if value[0] <= time)
                    if any(values[key]): active.append(channel['target']['node'])
                self.assertEqual(active, groups[expected])


if __name__ == '__main__': unittest.main()
