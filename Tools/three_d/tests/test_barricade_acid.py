from copy import deepcopy
from pathlib import Path
import json, struct, unittest
import yaml
from PIL import Image
import build_models as bm


class BarricadeAcidTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.model = bm.validate_model(yaml.load((bm.WORLD_SOURCE/'garrison_plasteel_barricade.yml').read_text(),Loader=yaml.CSafeLoader)[-1])

    def test_all_160_source_compositions_keep_acid_before_wire(self):
        for wired in (False, True):
            for key in ('0', '4', '8', '12'):
                for frame in range(5):
                    for direction,name in enumerate(('south','north','east','west')):
                        actual,evidence = bm.reference_frame({**self.model,'referenceState':'DamageOverlay_'+key,'referenceDirection':direction},{},wired,frame)
                        expected = Image.open(bm.VIEWER/f'plasteel-state-review/{name}-damage{key}-wire{int(wired)}-acid{frame}.png').convert('RGBA')
                        self.assertEqual(actual.tobytes(),expected.tobytes(),(key,wired,frame,name))
                        self.assertEqual(evidence['acidLayer']['frame'],frame)

    def test_frames_retain_all_body_and_wire_parts_with_bounded_extra_geometry(self):
        for key,state in self.model['barricadeAcidStates'].items():
            for field,base in (('frames','barricadeDamageStates'),('wiredFrames','barricadeWiredStates')):
                for frame in state[field]:
                    parts=self.model[base][key]['parts']
                    self.assertEqual(parts,frame['parts'][:len(parts)])
                    self.assertLessEqual(len(frame['parts']),160)
        self.assertEqual(max(len(f['parts']) for s in self.model['barricadeAcidStates'].values() for field in ('frames','wiredFrames') for f in s[field]),129)

    def test_malformed_acid_data_is_rejected(self):
        for defect in ('missing-context','extra-context','missing-frame','empty','body-changed','effect-changed','timing','rsi','state','budget'):
            with self.subTest(defect=defect):
                model=deepcopy(self.model);states=model['barricadeAcidStates']
                if defect=='missing-context':states.pop('4')
                if defect=='extra-context':states['16']=deepcopy(states['8'])
                if defect=='missing-frame':states['8']['frames'].pop()
                if defect=='empty':states['8']['wiredFrames'][2]['parts']=[]
                if defect=='body-changed':states['8']['frames'][1]['parts'][0]['color']='#FFFFFF'
                if defect=='effect-changed':states['8']['wiredFrames'][1]['parts'][-1]['color']='#FFFFFF'
                if defect=='timing':model['barricadeAcidDelays']=[.2]*5
                if defect=='rsi':model['barricadeAcidRsi']=''
                if defect=='state':model['barricadeAcidState']='fire'
                if defect=='budget':states['8']['frames'][1]['parts']*=3
                with self.assertRaises(ValueError):bm.validate_model(model)

    def test_portable_clips_switch_exactly_one_frame_and_loop_without_defining_effect_expiry(self):
        raw=bm.glb_bytes(self.model);size=struct.unpack_from('<I',raw,12)[0]
        doc=json.loads(raw[20:20+size]);binary=raw[28+size:]
        self.assertEqual(len(doc['scenes']),16);self.assertEqual(len(doc['animations']),8)
        self.assertEqual(doc['scene'],0)
        groups=doc['extras']['barricadeAcidFrameGroups']
        def values(index):
            a=doc['accessors'][index];v=doc['bufferViews'][a['bufferView']];width=3 if a['type']=='VEC3' else 1
            data=struct.unpack_from('<'+str(a['count']*width)+'f',binary,v.get('byteOffset',0)+a.get('byteOffset',0))
            return [data[i:i+width] for i in range(0,len(data),width)]
        for clip,context in zip(doc['animations'],doc['extras']['barricadeAcidScenes']):
            tracks={c['target']['node']:clip['samplers'][c['sampler']] for c in clip['channels']}
            self.assertTrue(clip['extras']['loop']);self.assertFalse(clip['extras']['returnsToIdle'])
            self.assertIn('not the 0.5-second',clip['extras']['lifetimeControlledBy'])
            times=[v[0] for v in values(clip['samplers'][0]['input'])]
            for a,b in zip(times,[0,.1,.2,.3,.4,.5]):self.assertAlmostEqual(a,b,places=6)
            for step,frame in enumerate((0,1,2,3,4,0)):
                visible=set()
                for node,track in tracks.items():
                    self.assertEqual(track['interpolation'],'STEP')
                    if any(values(track['output'])[step]):visible.add(node)
                self.assertEqual(visible,set(groups[context+':acid:'+str(frame)]))
        for scene in doc['scenes'][:8]:
            self.assertTrue(all(all(v>0 for v in doc['nodes'][n]['scale']) for n in scene['nodes']))
        for context,index in doc['extras']['barricadeAcidScenes'].items():
            visible={n for n in doc['scenes'][index]['nodes'] if any(doc['nodes'][n]['scale'])}
            self.assertEqual(visible,set(groups[context+':acid:0']))
        self.assertEqual(raw,bm.glb_bytes(self.model))


if __name__=='__main__':unittest.main()
