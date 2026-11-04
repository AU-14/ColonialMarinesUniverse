import math
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import terrain_cutouts as terrain
from build_models import validate_model


def box(low=(-.5, -.5, 0), high=(.5, .5, 2.8), **extra):
    return dict(label='rock', min=low, max=high, color='#443529', **extra)


class TerrainCutoutTests(unittest.TestCase):
    def test_alcove_retains_roof_and_outside_strips_without_mutating_source(self):
        original = [box()]
        result, status = terrain.clip_parts(original, (0, 0), 0, [((-.49, -.49, 0), (.49, .49, 1.1))])
        self.assertEqual(status, 'clipped')
        self.assertEqual(len(result), 5)
        self.assertEqual(original, [box()])
        self.assertTrue(all(not terrain.overlaps(*terrain.part_bounds(p), (-.49, -.49, 0), (.49, .49, 1.1)) for p in result))
        volume = sum(math.prod(b-a for a,b in zip(p['min'],p['max'])) for p in result)
        self.assertAlmostEqual(volume, 2.8 - .98*.98*1.1)
        self.assertTrue(any(p['min'][2] == 1.1 and p['max'][2] == 2.8 for p in result))

    def test_six_slabs_partition_a_fully_internal_cut(self):
        result, status = terrain.clip_parts([box((-2,-2,-2),(2,2,2))], (0,0), 0, [((-1,-1,-1),(1,1,1))])
        self.assertEqual(status, 'clipped')
        self.assertEqual(len(result), 6)
        for i, a in enumerate(result):
            for b in result[i+1:]:
                self.assertFalse(terrain.overlaps(*terrain.part_bounds(a), *terrain.part_bounds(b)))
        self.assertAlmostEqual(sum(math.prod(b-a for a,b in zip(p['min'],p['max'])) for p in result), 56)

    def test_quarter_turns_and_part_rotations(self):
        for entity_turn in range(4):
            for part_turn in range(4):
                yaw = entity_turn * math.pi/2
                p = box((-1,-.5,0),(1,.5,2), yaw=part_turn*90)
                cut = terrain.world_bounds((-.2,-.2,0),(.2,.2,1), (10,-7), yaw)
                result, status = terrain.clip_parts([p], (10,-7), yaw, [cut])
                self.assertEqual(status,'clipped')
                self.assertAlmostEqual(sum(math.prod(b-a for a,b in zip(x['min'],x['max'])) for x in result),3.84)

    def test_unsupported_and_budget_fail_atomically(self):
        cut = [((-.25,-.25,0),(.25,.25,1))]
        for extra in ({'shape':'Ellipsoid'}, {'pitch':15}, {'yaw':12}, {'surface':'texture'}):
            original=[box(),box(**extra)]
            result,status=terrain.clip_parts(original,(0,0),0,cut)
            self.assertIs(result,original)
            self.assertEqual(status,'unsupported')
        original=[box() for _ in range(128)]
        result,status=terrain.clip_parts(original,(0,0),0,cut)
        self.assertIs(result,original)
        self.assertEqual(status,'budget')

    def test_duplicate_cuts_and_touching_faces_are_idempotent(self):
        original=[box()];cut=((-1,-1,0),(1,1,1.1))
        once,_=terrain.clip_parts(original,(0,0),0,[cut])
        twice,_=terrain.clip_parts(original,(0,0),0,[cut]*20)
        self.assertEqual(once,twice)
        result,status=terrain.clip_parts(original,(0,0),0,[((.5,-1,0),(2,1,3))])
        self.assertIs(result,original)
        self.assertEqual(status,'unchanged')

    def test_sub_quantization_sliver_preserves_original(self):
        original=[box()]
        result,status=terrain.clip_parts(original,(0,0),0,[((-.4999,-1,0),(1,1,1))])
        self.assertIs(result,original)
        self.assertEqual(status,'unsupported')

    def test_contract_requires_explicit_targets_complete_bounds_and_independent_layout(self):
        base=dict(id='Test',label='Test',parts=[box()],terrainCutoutTargets=['Rock'],
                  terrainCutoutMin='-1.49,-1.49,0',terrainCutoutMax='1.49,1.49,1.1')
        valid=validate_model(base)
        self.assertEqual(valid['terrainCutoutMax'],(1.49,1.49,1.1))
        for changes in ({'terrainCutoutTargets':['Rock','Rock']},{'terrainCutoutMax':'1,1,0'},
                        {'terrainCutoutMin':'-9,-1,0'},{'wallMounted':True},{'placement':'surface'}):
            with self.assertRaises(ValueError):validate_model({**base,**changes})

    def test_outside_crop_cutter_exact_targets_and_map_boundaries(self):
        models={
            'cut':dict(id='cut',sourcePrototypes=['Intake'],terrainCutoutTargets=['Rock'],terrainCutoutMin=(-1.49,-1.49,0),terrainCutoutMax=(1.49,1.49,1.1),parts=[box()]),
            'rock':dict(id='rock',parts=[box()])}
        records={1:dict(prototype='Intake'),2:dict(prototype='Rock'),3:dict(prototype='ChildRock'),4:dict(prototype='Rock'),5:dict(prototype='Rock')}
        positions={1:(0,0,0,10),2:(1,0,0,10),3:(1,0,0,10),4:(1,0,0,11),5:(1,0,0,10)}
        class Transforms:
            def resolve(self,uid):return positions[uid]
        instances=[dict(id=uid,prototype=records[uid]['prototype'],position=[1,0,0],yaw=0,renderYaw=0,modelId='rock',matchKind='inherited' if uid==5 else 'exact') for uid in (2,3,4,5)]
        variants={}
        result=terrain.apply_cutouts(instances,models,variants,records,lambda uid,name:{},Transforms(),lambda yaw,*args:yaw)
        self.assertEqual(result,{'terrainCutoutEntities':1})
        self.assertEqual(instances[0]['terrainCutoutSources'],[1])
        self.assertTrue(all('geometryKey' not in x for x in instances[1:]))
        instances[0].pop('geometryKey')
        terrain.apply_cutouts(instances,models,{},records,lambda uid,name:{},Transforms(),lambda yaw,*args:yaw,{1})
        self.assertNotIn('geometryKey',instances[0])


if __name__ == '__main__':
    unittest.main()
