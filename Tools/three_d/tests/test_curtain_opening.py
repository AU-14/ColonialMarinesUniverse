import copy
import math
import unittest
from build_models import validate_model
from layout import opening_fixture_yaw, panel_end_limits, resolve_layout
from scene import WorldTransforms


class CurtainOpeningTests(unittest.TestCase):
    def test_only_wall_blocked_openings_use_clear_fixture_facing_on_rotated_grids(self):
        for grid in (0,.7):
            self.assertAlmostEqual(opening_fixture_yaw(grid,grid,2,grid+math.pi),grid+math.pi)
            self.assertAlmostEqual(opening_fixture_yaw(grid-math.pi/2,grid,10,grid+math.pi),grid+math.pi)
            self.assertEqual(opening_fixture_yaw(grid,grid,1,grid+math.pi),grid)
            self.assertEqual(opening_fixture_yaw(grid,grid,3,grid+math.pi),grid)
            self.assertEqual(opening_fixture_yaw(grid+.2,grid,2,grid+math.pi),grid+.2)
            self.assertEqual(opening_fixture_yaw(grid,grid,2,grid+.2),grid)

    def test_textured_rail_posts_are_physical_end_supports(self):
        panel=[{'min':[-.5,-.505,.08],'max':[.5,-.36,2.5]}]
        rail=[{'min':[-.5,-.5,0],'max':[.5,-.40625,1],'surface':'original-wire-art'}]
        a,b=panel_end_limits(panel,math.pi,rail,-math.pi/2,[0,0])
        self.assertAlmostEqual(a,-.5)
        self.assertAlmostEqual(b,.39625)

    def test_same_tile_unanchored_fixture_can_correct_but_other_grid_cannot(self):
        records={1:{'prototype':'','components':{'MapGrid':{},'Transform':{'parent':0}}},
                 2:{'prototype':'','components':{'MapGrid':{},'Transform':{'parent':0}}},
                 3:{'prototype':'Curtain','components':{'Transform':{'parent':1,'pos':'.5,.5'}}},
                 4:{'prototype':'Shower','components':{'Transform':{'parent':1,'pos':'.5,.5','rot':f'{math.pi} rad'}}},
                 5:{'prototype':'Wall','components':{'Transform':{'parent':1,'pos':'.5,-.5'}}}}
        defaults={p:{'Transform':{'anchored':True}} for p in ('Curtain','Shower','Wall')}
        # CMShower inherits anchored:false; requiring an anchor misses the real fixture.
        defaults['Shower']['Transform']['anchored']=False
        defaults['Wall']['IconSmooth']={'key':'walls'}
        models=[{'id':'C','sourcePrototypes':['Curtain'],'useEntityRotation':True,'openingFacingTargets':['Shower'],'parts':[]}]
        e={'id':3,'modelId':'C','position':[.5,.5,0],'yaw':0}
        before=copy.deepcopy(records)
        resolve_layout([e],models,records,defaults,WorldTransforms(records,defaults))
        self.assertAlmostEqual(e['renderYaw'],math.pi,places=8)
        self.assertEqual(e['openingFacingSource'],4)
        self.assertEqual(e['yaw'],0)
        self.assertEqual(records,before)
        for override in ({'parent':2},{'pos':'1.5,.5'},{'rot':'0 rad'}):
            records[4]['components']['Transform']={**before[4]['components']['Transform'],**override}
            resolve_layout([e],models,records,defaults,WorldTransforms(records,defaults))
            self.assertEqual(e['renderYaw'],0)
            self.assertNotIn('openingFacingSource',e)

    def test_fixture_facing_metadata_requires_explicit_independent_opening(self):
        m={'id':'C','label':'C','parts':[{'min':[-.5,-.5,0],'max':[.5,-.4,2.5]}],
           'useEntityRotation':True,'openingFacingTargets':['Shower']}
        validate_model(m)
        for change in ({'useEntityRotation':False},{'openingFacingTargets':['Shower','Shower']},
                       {'sourceDirections':4},{'connectToNeighbours':True}):
            with self.assertRaisesRegex(ValueError,'openingFacingTargets'):validate_model({**m,**change})
