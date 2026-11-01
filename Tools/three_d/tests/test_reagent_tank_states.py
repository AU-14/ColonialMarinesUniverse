from copy import deepcopy
import json
import math
from pathlib import Path
import tempfile
import unittest

from PIL import Image
import build_models as bm
import reagent_tank_states as tanks
from reagent_tank_placement import cladding_offset
from placement import resolve_placements
from scene import normalize_tint
from export_scene import export_region


def model():
    return dict(id='Tank', label='Test tank', sourcePrototypes=['TankSource'], referenceRsi=tanks.RSI,
                referenceState='tank_normal', sourceDirections=1, reagentTankAppearance={'vesselParts': ['vessel']},
                parts=[dict(label='body', min=[-.4,-.3,0], max=[.4,.3,.2], color='#808080'),
                       dict(label='vessel', min=[-.3,-.2,.2], max=[.3,.2,.8], color='#FFFFFF')])


def sprite():
    return dict(noRot=True, sprite=tanks.RSI, layers=[{'state':'tank_normal'}, {'state':'tn_color-1'},
        {'state':'tn_color-1', 'map':[tanks.FILL_MAP], 'visible':False}, {'state':'t_inactive'}])


class ReagentTankTests(unittest.TestCase):
    def test_source_masks_prove_composite_equivalence_and_reference_has_permanent_vessel(self):
        value = bm.validate_model(model())
        layers = tanks.validate_source(value, bm.resource_file)
        self.assertEqual(layers['tn_color-1'].tobytes(), layers['tn_color-2'].tobytes())
        self.assertEqual(set(layers['tn_color-1'].getchannel('A').getdata()), {0,255})
        reference, meta = bm.reference_frame(value, {})
        expected = Image.alpha_composite(Image.alpha_composite(layers['tank_normal'], layers['tn_color-1']), layers['t_inactive'])
        self.assertEqual(reference.tobytes(), expected.tobytes())
        self.assertEqual(meta['layers'], ['tank_normal','tn_color-1','t_inactive'])
        # Check every opaque source texel and several fill alphas independently of model geometry.
        for rgba in ((20,70,180,255), (20,70,180,128), (0,0,0,0)):
            tinted = layers['tn_color-1'].copy()
            tinted.putdata([tuple(round(pixel[i]*rgba[i]/255) for i in range(4)) for pixel in tinted.getdata()])
            composed = Image.alpha_composite(layers['tn_color-1'], tinted)
            for original, result in zip(layers['tn_color-1'].getdata(), composed.getdata()):
                if original[3]:
                    for i in range(3): self.assertLessEqual(abs(result[i] - original[i]*(1-rgba[3]/255+rgba[3]*rgba[i]/255**2)), 1)
                    self.assertEqual(result[3], 255)

    def test_schema_rejects_wrong_state_owner_and_ambiguous_vessel_labels(self):
        for patch in ({'referenceRsi':'other.rsi'}, {'referenceState':'t_active'}, {'useEntityRotation':True},
                      {'bakedSpriteTint':'#FFFFFF80'}, {'reagentTankAppearance':{'vesselParts':['missing']}},
                      {'reagentTankAppearance':{'vesselParts':['vessel','vessel']}}, {'wallMounted':True}):
            with self.subTest(patch=patch), self.assertRaises(ValueError): bm.validate_model({**model(),**patch})
        changed=model();changed['parts'][1]['color']='#FEFFFF'
        with self.assertRaises(ValueError): bm.validate_model(changed)

    def test_fill_hidden_alpha_and_overall_tint_do_not_remove_or_double_tint_vessel(self):
        value = bm.validate_model(model())
        for visible, color in ((False,'#FF0000'), (True,'#FF000000')):
            parts=tanks.compose_parts(value,visible=visible,color=color)
            self.assertEqual(parts[1]['color'],'#FFFFFFFF'); self.assertEqual(len(parts),2)
        parts=tanks.compose_parts(value,visible=True,color='#3366CC80',sprite_tint='#80FF80')
        self.assertEqual(parts[0]['color'],'#408040FF')
        self.assertEqual(parts[1]['color'],'#4DB273FF')
        with self.assertRaises(ValueError): tanks.compose_parts(value,sprite_tint='#80FF8040')
        self.assertEqual(value['parts'][1]['color'],'#FFFFFF')

    def test_layer_snapshot_requires_exact_order_and_never_infers_solution_color(self):
        value=bm.validate_model(model()); defaults={'Sprite':sprite(), 'Solution':{'solution':{'reagents':[{'ReagentId':'Water','Quantity':1000}]}}}
        pose,reason=tanks.saved_pose(value,defaults,{},normalize_tint)
        self.assertIsNone(reason); self.assertFalse(pose['visible']); self.assertIn('before live',pose['source'])
        for state in ('tn_color-1','tn_color-2'):
            layers=sprite();layers['layers'][2].update(state=state,visible=True,color='#12345678')
            pose,reason=tanks.saved_pose(value,defaults,{'Sprite':layers},normalize_tint)
            self.assertIsNone(reason);self.assertEqual(pose['color'],'#12345678')
        for defect in ('active','boom','fill','extra','vessel','fixed-tint','map','rsi','offset','noRot','appearance','shader','sprite-alpha',
                       'granular','post-shader','shader-parameter-layer'):
            changed=sprite();saved={'Sprite':changed}
            if defect in ('active','boom'): changed['layers'][3]['state']='t_'+defect
            if defect=='fill': changed['layers'][2]['state']='te_color-1'
            if defect=='extra': changed['layers'].append({'state':'tank_normal'})
            if defect=='vessel': changed['layers'][1]['visible']=False
            if defect=='fixed-tint': changed['layers'][1]['color']='#FF0000'
            if defect=='map': changed['layers'][2]['map']=[]
            if defect=='rsi': changed['sprite']='other.rsi'
            if defect=='offset': changed['offset']='.1,0'
            if defect=='noRot': changed['noRot']=False
            if defect=='appearance': saved['Appearance']={'data':{'anything':1}}
            if defect=='shader': changed['layers'][2]['shader']='unshaded'
            if defect=='sprite-alpha': changed['color']='#FFFFFF40'
            if defect=='granular': changed['granularLayersRendering']=True
            if defect=='post-shader': changed['postShaders']=[{'id':'unknown'}]
            if defect=='shader-parameter-layer': changed['layers'][2]['copyToShaderParameters']={'parameterTexture':'unknown'}
            with self.subTest(defect=defect): self.assertIsNone(tanks.saved_pose(value,defaults,saved,normalize_tint)[0])

    def test_export_uses_proven_layer_variant_and_has_no_activation_animation(self):
        value=bm.validate_model(model());instance={'id':1,'modelId':'Tank','prototype':'TankSource','position':[0,0,0],'yaw':0,'matchKind':'exact'}
        self.assertTrue(tanks.resolve_scene_pose(instance,value,{'Sprite':sprite()},{},normalize_tint))
        variants={};tanks.scene_variants([instance],{'Tank':value},variants)
        scene={'instances':[instance],'geometryVariants':variants,'tiles':[],'map':{'level':0,'path':'synthetic-tank-fixture'}}
        data,report=export_region(scene,[value],[0,0,0],1,floors=False)
        self.assertTrue(data.startswith(b'glTF'));self.assertEqual(report['reagentTankLayerSnapshots'],1)
        doc,binary=tanks.model_document(value,bm.glb_document)
        self.assertNotIn('animations',doc);self.assertEqual(doc['extras']['reagentTankAppearance'],value['reagentTankAppearance'])
        instance.pop('reagentTankPose')
        with self.assertRaises(ValueError): export_region(scene,[value],[0,0,0],1,floors=False)

    def test_floor_cladding_parity_all_yaws_and_unknown_supports_stay_at_floor(self):
        value=bm.validate_model(model())
        for prototype in ('CMCatwalk','CMCatwalkPrison','RMCCatwalkHybrisaElevator'):
            support={'id':'Grate','sourcePrototypes':[prototype],'parts':[dict(min=[-.5,-.5,0],max=[.5,.5,.035])]}
            for yaw in (0,math.pi/2,math.pi,-math.pi/2):
                self.assertAlmostEqual(cladding_offset(value,support,prototype,[0,0],yaw),.037)
            for delta,yaw in (([.1,0],0),([0,0],.4),([math.nan,0],0)):
                self.assertEqual(cladding_offset(value,support,prototype,delta,yaw),0)
            tank=dict(id=1,prototype='TankSource',modelId='Tank',position=[2.5,3.5,0],yaw=math.pi,renderYaw=0,matchKind='exact')
            grate=dict(id=2,prototype=prototype,modelId='Grate',position=[2.5,3.5,0],yaw=0,matchKind='exact')
            resolve_placements([tank,grate],[value,support])
            self.assertEqual(tank['renderOffset'],[0,0,.037]);self.assertEqual(tank['position'],[2.5,3.5,0])
            self.assertEqual(tank['renderYaw'],0)
            resolve_placements([tank],[value,support]);self.assertNotIn('renderOffset',tank);self.assertNotIn('floorCladding',tank)
            grate['position'][2]=1
            resolve_placements([tank,grate],[value,support]);self.assertNotIn('renderOffset',tank)
            support['parts'][0]['max'][2]=.7
            self.assertEqual(cladding_offset(value,support,prototype,[0,0],0),0)


if __name__ == '__main__': unittest.main()
