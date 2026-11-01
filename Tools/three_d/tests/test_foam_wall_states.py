from copy import deepcopy
from pathlib import Path
import math
import sys
import unittest

sys.path[:0] = [str(Path(__file__).resolve().parents[1])]
import build_models as bm
import foam_wall_states as fs
import scene


def part(label):
    return dict(label=label, min=[0,0,0], max=[.1,.1,.1], color=fs.TINT)


def model():
    result = dict(id='FoamTest', label='Test foam', sourcePrototypes=[fs.PROTOTYPE],
        referencePrototype=fs.PROTOTYPE, referenceRsi=fs.RSI, referenceState=fs.BASE,
        referenceTint=fs.TINT, bakedSpriteTint=fs.TINT, useEntityRotation=True,
        foamAppearance=dict(baseParts=[part('body')], edgeParts={d:dict(parts=[part(d)]) for d in fs.EDGES}))
    result['parts'] = fs.compose_parts(result)
    return bm.validate_model(result)


def source():
    return dict(Sprite=dict(sprite=fs.RSI, color=fs.TINT,
        layers=[dict(state=s,map=[key]) for s,key in zip(fs.STATES,fs.MAPS)]),
        SmoothEdge={}, IconSmooth=dict(key='walls',mode='NoSprite'),Transform=dict(anchored=True),Appearance={})


def record(uid, proto, x, y, parent=1, **transform):
    return dict(id=uid,prototype=proto,components=dict(Transform=dict(pos=f'{x},{y}',parent=parent,**transform)))


class FoamWallStateTests(unittest.TestCase):
    def test_all_sixteen_source_edge_compositions_preserve_order_alpha_and_immutable_source(self):
        m=model();before=deepcopy(m)
        states=fs.portable_states(m)
        self.assertEqual(len(states),16)
        for mask in range(16):
            parts=fs.compose_parts(m,mask)
            self.assertEqual([p['label'] for p in parts],['body']+[d for i,d in enumerate(fs.EDGES) if mask&(1<<i)])
            self.assertTrue(all(p['color']==fs.TINT for p in parts))
            tinted=fs.compose_parts(m,mask,'#80402066')
            self.assertTrue(all(p['color']=='#80402066' for p in tinted))
            self.assertEqual(states[f'edges-{mask}']['delays'],[1])
        self.assertEqual(m,before)

    def test_portable_glb_has_sixteen_static_scenes_and_no_animations(self):
        doc,_=fs.model_document(model(),bm.glb_document)
        self.assertEqual(len(doc['extras']['spriteStaticScenes']),16)
        self.assertFalse(doc.get('animations'))
        self.assertEqual(doc['extras']['foamAppearance']['maskOrder'],list(fs.EDGES))
        self.assertEqual(doc['extras']['foamAppearance']['originalSpriteAlpha'],.8)

    def test_source_retains_original_fractional_alpha_and_unfiltered_five_layer_references(self):
        images=fs.validate_source(model(),bm.resource_file)
        self.assertEqual(set(images),set(fs.STATES))
        self.assertEqual(set(images[fs.BASE].getchannel('A').tobytes()),{255})
        for state in fs.STATES[1:]:
            self.assertEqual(set(images[state].getchannel('A').tobytes()),{0,240,255})
        from surfaces import png_bytes
        outputs,references=fs.viewer_references(model(),bm.resource_file,png_bytes)
        self.assertEqual(len(outputs),16);self.assertEqual(len(references),16)
        from io import BytesIO
        from PIL import Image
        full=Image.open(BytesIO(outputs['references/FoamTest-foam-edges-15.png']))
        self.assertEqual(set(full.getchannel('A').tobytes()),{0,192,204})

    def test_cardinal_owner_uses_any_enabled_anchored_wall_key_independent_of_source_yaw(self):
        defaults={fs.PROTOTYPE:source(), 'OrdinaryWall':dict(Transform=dict(anchored=True),IconSmooth=dict(key='walls')),
                  'OtherKey':dict(Transform=dict(anchored=True),IconSmooth=dict(key='windows'))}
        records={1:dict(id=1,prototype='',components={'MapGrid':{},'Transform':dict(pos='7,9',rot='31')})}
        records[2]=record(2,fs.PROTOTYPE,.5,.5,rot='-90')
        records[3]=record(3,'OrdinaryWall',.5,-.5)
        records[4]=record(4,'OrdinaryWall',1.5,.5,anchored=False)
        records[5]=record(5,'OrdinaryWall',.5,1.5)
        records[5]['components']['IconSmooth']={'enabled':False}
        records[6]=record(6,'OtherKey',-.5,.5)
        records[7]=record(7,fs.PROTOTYPE,.5,.5)  # A duplicate cell does not hide neighboring edges.
        graph=fs.EdgeGraph(records,defaults,scene.WorldTransforms(records,defaults))
        mask,neighbors=graph.mask(2)
        self.assertEqual(mask,14);self.assertEqual(neighbors['south'],[3])
        self.assertEqual(graph.mask(7),(mask,neighbors))
        records[2]['components']['Transform']['rot']='44'
        graph=fs.EdgeGraph(records,defaults,scene.WorldTransforms(records,defaults))
        self.assertEqual(graph.mask(2),(mask,neighbors))
        records[8]=record(8,'OrdinaryWall',.5,-.5)
        graph=fs.EdgeGraph(records,defaults,scene.WorldTransforms(records,defaults))
        self.assertEqual(graph.mask(2)[1]['south'],[3,8])

    def test_unresolved_anchored_neighbor_preserves_original_sprite_and_does_not_emit_variant(self):
        defaults={fs.PROTOTYPE:source()}
        records={1:dict(id=1,prototype='',components={'MapGrid':{}}),2:record(2,fs.PROTOTYPE,.5,.5),
                 3:record(3,'Unresolved',1.5,.5,anchored=True)}
        instances=[dict(id=2,prototype=fs.PROTOTYPE,modelId='FoamTest',matchKind='exact')];variants={}
        result=fs.apply_scene(instances,{'FoamTest':model()},records,defaults,
                              scene.WorldTransforms(records,defaults),variants,scene.normalize_tint)
        self.assertFalse(result['applied']);self.assertEqual(len(result['rejected']),1)
        self.assertIsNone(instances[0]['modelId']);self.assertEqual(variants,{})

    def test_saved_compositions_preserve_duplicate_instances_and_saved_tint(self):
        defaults={fs.PROTOTYPE:source()}
        records={1:dict(id=1,prototype='',components={'MapGrid':{}}),2:record(2,fs.PROTOTYPE,.5,.5),
                 3:record(3,fs.PROTOTYPE,.5,.5)}
        records[3]['components']['Sprite']={'color':'#88442266'}
        instances=[dict(id=i,prototype=fs.PROTOTYPE,modelId='FoamTest',matchKind='exact') for i in (2,3)]
        variants={};before=deepcopy(records)
        result=fs.apply_scene(instances,{'FoamTest':model()},records,defaults,
                              scene.WorldTransforms(records,defaults),variants,scene.normalize_tint)
        self.assertEqual(len(result['applied']),2);self.assertFalse(result['rejected'])
        self.assertEqual(len(instances),2);self.assertEqual(len(variants),2)
        self.assertEqual(records,before)
        self.assertTrue(all(p['color']=='#88442266' for p in variants[instances[1]['geometryKey']]))

    def test_unknown_saved_owner_layer_or_sprite_transform_fails_conservatively(self):
        for defect in ('missing-owner','disabled','key','mode','additional-key','cm-owner','noRot','snap','sprite-offset',
                       'layer-offset','layer-state','layer-order','layer-color','layer-shader','appearance','visualizer'):
            d=source()
            if defect=='missing-owner':d.pop('SmoothEdge')
            if defect=='disabled':d['IconSmooth']['enabled']=False
            if defect=='key':d['IconSmooth']['key']='windows'
            if defect=='mode':d['IconSmooth']['mode']='Corners'
            if defect=='additional-key':d['IconSmooth']['additionalKeys']=['windows']
            if defect=='cm-owner':d['CMIconSmooth']={'smooth':True}
            if defect=='noRot':d['Sprite']['noRot']=True
            if defect=='snap':d['Sprite']['snapCardinals']=True
            if defect=='sprite-offset':d['Sprite']['offset']=[.1,0]
            if defect=='layer-offset':d['Sprite']['layers'][1]['offset']=[0,0]
            if defect=='layer-state':d['Sprite']['layers'][1]['state']='iron_foam-south'
            if defect=='layer-order':d['Sprite']['layers'].reverse()
            if defect=='layer-color':d['Sprite']['layers'][1]['color']='#FFFFFFCC'
            if defect=='layer-shader':d['Sprite']['layers'][1]['shader']='unshaded'
            if defect=='appearance':d['Appearance']['data']={'unknown':1}
            if defect=='visualizer':d['GenericVisualizer']={}
            with self.subTest(defect=defect):
                pose,error=fs.saved_pose(model(),{},d,scene.normalize_tint)
                self.assertIsNone(pose);self.assertTrue(error)

    def test_invalid_masks_source_contract_and_composed_budget_are_rejected(self):
        for mask in (-1,16,1.0,True):
            with self.assertRaises(ValueError):fs.compose_parts(model(),mask)
        for defect in ('tint','default','rotation','edge','budget'):
            m=model()
            if defect=='tint':m['foamAppearance']['baseParts'][0]['color']='#FFFFFF'
            if defect=='default':m['parts']=m['parts'][:1]
            if defect=='rotation':m['useEntityRotation']=False
            if defect=='edge':m['foamAppearance']['edgeParts'].pop('south')
            if defect=='budget':m['foamAppearance']['baseParts']=[part(str(i)) for i in range(128)]
            with self.subTest(defect=defect):
                with self.assertRaises(ValueError):fs.validate(m,bm.validate_model)


if __name__=='__main__':
    unittest.main()
