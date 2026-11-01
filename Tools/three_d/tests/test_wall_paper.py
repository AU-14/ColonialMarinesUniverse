from copy import deepcopy
import math
import unittest

import build_models
from layout import resolve_layout
from scene import WorldTransforms
import wall_paper as paper


def model():
    return dict(id='Paper', label='Paper', sourcePrototypes=['PaperSource'], wallPaper=True, wallMounted=True,
                fitInsideWall=True, backWallMountTargets=['Wall'], referenceRsi='_RMC14/Structures/Wallmounts/posters.rsi',
                referenceState='bepis', sourceDirections=1,
                parts=[dict(label='paper', min=[-.2,-.511,1], max=[.2,-.501,1.6], color='#FFFFFF')])


def sprite():
    return dict(sprite=model()['referenceRsi'], state='bepis', snapCardinals=True, drawdepth='WallTops')


def pose(uid, y, x=0, yaw=0, inside=False):
    return paper.pose(uid, model(), [0,y], [math.cos(yaw)*x,math.sin(yaw)*x], yaw, inside, -1)


class WallPaperTests(unittest.TestCase):
    def test_gap_and_resolved_front_at_four_yaws_rotated_grid_and_inside_face(self):
        for yaw, inside in [(0,False),(math.pi/2,False),(math.pi,False),(-math.pi/2,False),(.37,False),(.37,True)]:
            a, b = pose(90,2,0,yaw,inside), pose(3,1,.1,yaw,inside)
            offsets = paper.offsets([b,a])
            normal = (math.sin(b['frontYaw']),-math.cos(b['frontYaw']))
            distance = sum(offsets[3][i]*normal[i] for i in range(2))
            self.assertEqual(offsets[90],[0,0,0]);self.assertAlmostEqual(distance,.014)
            self.assertAlmostEqual(b['rear']+distance-a['front'],.004)

    def test_source_depth_then_order_then_y_then_id(self):
        a,b = pose(1,2),pose(2,1)
        for update, front in [({'drawDepth':10},1),({'renderOrder':3},1),({},2)]:
            offsets=paper.offsets([{**a,**update},b]);self.assertGreater(math.hypot(*offsets[front][:2]),0)
        self.assertGreater(math.hypot(*paper.offsets([pose(1,1),b])[2][:2]),0)

    def test_chains_and_input_order_independence(self):
        poses=[pose(1,3,-.3),pose(2,2),pose(3,1,.3)]
        self.assertFalse(paper.overlaps(poses[0],poses[2]))
        offsets=paper.offsets(poses);self.assertAlmostEqual(math.hypot(*offsets[3][:2]),.028)
        self.assertEqual(offsets,paper.offsets(poses[::-1]))

    def test_other_faces_grids_and_touching_edges_stay_unchanged(self):
        a=pose(1,3)
        for b in [pose(2,1,.4),pose(2,1,0,math.pi),{**pose(2,1),'rear':3,'front':3.01},{**pose(2,1),'grid':4}]:
            self.assertFalse(paper.overlaps(a,b));self.assertTrue(all(not any(v) for v in paper.offsets([a,b]).values()))

    def test_cluster_and_duplicate_budget_are_atomic(self):
        with self.assertRaises(ValueError):paper.offsets([pose(i,-i) for i in range(17)])
        with self.assertRaises(ValueError):paper.offsets([pose(1,0),pose(1,0)])

    def test_source_and_geometry_validation(self):
        value=build_models.validate_model(model());paper.validate_source(value,build_models.resource_file)
        self.assertTrue(paper.source_supported(value,sprite()))
        for field,value in [('wallPaper',1),('groundOffset',[1,0]),('spriteStates',{'on':{}}),('fitInsideWall',False)]:
            with self.assertRaises(ValueError):paper.validate({**model(),field:value})
        for field in ('yaw','pitch'):
            value=model();value['parts'][0][field]=3
            with self.assertRaises(ValueError):paper.validate(value)

    def test_unknown_source_states_and_render_paths_fall_back(self):
        for change in [dict(state='broken'),dict(offset=[.1,0]),dict(scale=[2,1]),dict(rotation=90),dict(noRot=True),
                       dict(snapCardinals=False),dict(granularLayersRendering=True),dict(postShaders=['x']),
                       dict(layers=[dict(state='bepis'),dict(state='bepis')]),dict(layers=[dict(state='bepis',color='#FF0000')]),
                       dict(layers=[dict(state='bepis',shader='x')])]:
            self.assertFalse(paper.source_supported(model(),{**sprite(),**change}),change)
        self.assertTrue(paper.source_supported(model(),{**sprite(),'color':'#FF0000'}))
        self.assertFalse(paper.source_supported(model(),{**sprite(),'color':'#FFFFFF00'}))

    def fixture(self):
        records={1:dict(prototype='',components={'MapGrid':{},'Transform':{'parent':0}}),
                 2:dict(prototype='PaperSource',components={'Transform':{'parent':1,'pos':'.35,.58'}}),
                 3:dict(prototype='PaperSource',components={'Transform':{'parent':1,'pos':'.6,.45'}}),
                 4:dict(prototype='Wall',components={'Transform':{'parent':1,'pos':'.5,.5'}})}
        defaults={'PaperSource':{'Sprite':sprite(),'Transform':{'anchored':True}},
                  'Wall':{'Transform':{'anchored':True},'IconSmooth':{'key':'walls'}}}
        wall=dict(id='WallModel',sourcePrototypes=['Wall'],parts=[dict(min=[-.5,-.5,0],max=[.5,.5,2.5])])
        instances=[dict(id=i,prototype='PaperSource',modelId='Paper',position=[x,y,0],yaw=0,matchKind='exact')
                   for i,x,y in [(2,.35,.58),(3,.6,.45)]]
        return records,defaults,[model(),wall],instances

    def test_crop_includes_offscreen_predecessor_and_rerun_is_idempotent(self):
        records,defaults,models,instances=self.fixture()
        full=deepcopy(instances)
        _,stats=resolve_layout(full,models,records,defaults,WorldTransforms(records,defaults))
        self.assertEqual(stats['stackedWallPapers'],1)
        self.assertAlmostEqual(full[1]['wallPaperOffset'][1],-.014)
        cropped=[deepcopy(instances[1])]
        resolve_layout(cropped,models,records,defaults,WorldTransforms(records,defaults))
        self.assertEqual(cropped[0],full[1]);self.assertEqual(len(cropped),1)
        resolve_layout(cropped,models,records,defaults,WorldTransforms(records,defaults))
        self.assertEqual(cropped[0],full[1]);self.assertEqual(cropped[0]['position'],instances[1]['position'])

    def test_removal_and_hidden_sources_release_spacing(self):
        records,defaults,models,instances=self.fixture()
        for hidden in (False,True):
            source=deepcopy(records)
            if not hidden:source.pop(2)
            cropped=[deepcopy(instances[1])]
            resolve_layout(cropped,models,source,defaults,WorldTransforms(source,defaults),excluded_terrain_sources=[2] if hidden else [])
            self.assertNotIn('wallPaperOffset',cropped[0])

    def test_unknown_adjacent_source_falls_back_atomically(self):
        records,defaults,models,instances=self.fixture();records[2]['components']['Sprite']={'state':'broken'}
        _,stats=resolve_layout(instances,models,records,defaults,WorldTransforms(records,defaults))
        self.assertEqual(stats['unsupportedWallPapers'],2)
        self.assertTrue(all(e['modelId'] is None and 'layoutOffset' not in e for e in instances))


if __name__=='__main__':unittest.main()
