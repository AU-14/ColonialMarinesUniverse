import math
import unittest
from unittest.mock import patch
from build_models import validate_model
from layout import corner_states, corner_parts, resolve_layout
from scene import WorldTransforms


def model():
    return dict(id='Carpet',label='Carpet',sourcePrototypes=['Carpet'],cornerSurfaces=[f'Surface{i}' for i in range(32)],parts=[
        dict(min=[l,b,0],max=[r,t,.012],color='#FFFFFF',surface='Surface0',surfaceAxis='XY')
        for l,b,r,t in ((0,-.5,.5,0),(0,0,.5,.5),(-.5,0,0,.5),(-.5,-.5,0,0))])


class CornerSurfaceTests(unittest.TestCase):
    def test_source_borders_and_diagonal_holes(self):
        expected={0:[0,0,0,0],1:[0,1,4,0],2:[4,0,0,1],4:[1,4,0,0],8:[0,0,1,4],
                  16:[0,2,0,0],32:[2,0,0,0],64:[0,0,0,2],128:[0,0,2,0],
                  5:[1,5,4,0],21:[1,7,4,0],15:[5,5,5,5],255:[7,7,7,7]}
        for mask,states in expected.items():self.assertEqual(corner_states(mask),states)
        m=model();result=corner_parts(m,5)
        self.assertEqual([p['surface'] for p in result],['Surface4','Surface22','Surface17','Surface3'])
        self.assertEqual([p['surface'] for p in m['parts']],['Surface0']*4)
        self.assertEqual(corner_parts(m,21)[1]['surface'],'Surface30')

    def test_all_neighbours_outside_crop_follow_keys_anchor_and_grid(self):
        records={1:{'prototype':'','components':{'MapGrid':{},'Transform':{'parent':0,'rot':90}}},
                 2:{'prototype':'Carpet','components':{'Transform':{'parent':1,'pos':'.5,.5','rot':180}}},
                 3:{'prototype':'Carpet','components':{'Transform':{'parent':1,'pos':'.5,1.5'}}},
                 4:{'prototype':'Other','components':{'Transform':{'parent':1,'pos':'1.5,.5'}}},
                 5:{'prototype':'Carpet','components':{'Transform':{'parent':1,'pos':'1.5,1.5','anchored':False}}},
                 6:{'prototype':'Carpet','components':{'Transform':{'parent':1,'pos':'-.5,1.5'},'IconSmooth':{'enabled':False}}},
                 7:{'prototype':'Carpet','components':{'Transform':{'parent':1,'pos':'1.5,-.5'},'IconSmooth':{'key':'walls'}}},
                 8:{'prototype':'','components':{'MapGrid':{},'Transform':{'parent':0}}},
                 9:{'prototype':'Carpet','components':{'Transform':{'parent':8,'pos':'.5,-.5'}}}}
        defaults={'Carpet':{'Transform':{'anchored':True},'IconSmooth':{'key':'carpet','additionalKeys':['matching'],'base':'carpet_'}},
                  'Other':{'Transform':{'anchored':True},'IconSmooth':{'key':'matching'}}}
        entity={'id':2,'modelId':'Carpet','yaw':3*math.pi/2,'position':[-.5,.5,0]}
        variants,stats=resolve_layout([entity],[model()],records,defaults,WorldTransforms(records,defaults))
        self.assertEqual(entity['connectionMask'],5)
        self.assertEqual(entity['cornerStates'],[1,5,4,0])
        self.assertEqual(entity['cornerStateBase'],'carpet_')
        self.assertAlmostEqual(entity['renderYaw'],math.pi/2)
        self.assertEqual(entity['yaw'],3*math.pi/2)
        self.assertEqual(entity['position'],[-.5,.5,0])
        self.assertEqual(stats['cornerSurfaceInstances'],1)
        self.assertEqual(variants[entity['geometryKey']][1]['surface'],'Surface22')
        records[5]['components']['Transform']['anchored']=True
        resolve_layout([entity],[model()],records,defaults,WorldTransforms(records,defaults))
        self.assertEqual(entity['connectionMask'],21)
        records[2]['components']['IconSmooth']={'enabled':False}
        resolve_layout([entity],[model()],records,defaults,WorldTransforms(records,defaults))
        self.assertNotIn('cornerStates',entity)
        self.assertNotIn('geometryKey',entity)

    def test_reject_unknown_surfaces_or_misplaced_quadrants(self):
        with patch('surfaces.load_surfaces',return_value={f'Surface{i}':{} for i in range(32)}):
            m=model();self.assertEqual(len(validate_model(m)['cornerSurfaces']),32)
            for bad in (['missing']*32, ['Surface0']*31, [['Surface0']]*32):
                with self.assertRaises(ValueError):validate_model({**m,'cornerSurfaces':bad})
            m['parts'][0]['max'][0]=.49
            with self.assertRaises(ValueError):validate_model(m)

    def test_asymmetric_wall_patches_preserve_the_structure_below(self):
        m=model()
        for p in m['parts']:p['min'][2]=2.78;p['max'][2]=2.8
        for i in (0,3):m['parts'][i]['max'][1]=.3125
        for i in (1,2):m['parts'][i]['min'][1]=.3125
        body=dict(label='wall body',min=[-.5,-.5,0],max=[.5,.5,2.78],color='#444645')
        m['parts'].append(body)
        with patch('surfaces.load_surfaces',return_value={f'Surface{i}':{} for i in range(32)}):
            valid=validate_model(m)
            for mask in range(256):
                parts=corner_parts(valid,mask)
                self.assertEqual(parts[4],valid['parts'][4])
                self.assertEqual(parts[0]['max'][1],.3125)
                self.assertEqual(parts[1]['min'][1],.3125)
                self.assertEqual(len(parts),5)
            m['parts'][1]['min'][1]=0
            with self.assertRaisesRegex(ValueError,'corner parts'):validate_model(m)
