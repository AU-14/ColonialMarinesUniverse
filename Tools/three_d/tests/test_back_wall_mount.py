import copy
import math
import unittest

from build_models import validate_model
from layout import back_wall_mount_offset, resolve_layout
from placement import resolve_placements
from scene import WorldTransforms


class BackWallMountTests(unittest.TestCase):
    basin=[{'min':[-.3,-.4,.65],'max':[.3,0,.85],'label':'bowl'}]
    wall=[{'min':[-.5,-.55,0],'max':[.5,.55,2.8],'label':'solid wall'}]

    def test_room_side_fixture_clears_projecting_wall_at_all_grid_and_fixture_rotations(self):
        parts=[{'min':[-.06,-.78,2.2],'max':[.06,-.501,2.4]}]
        for grid in (0,.7):
            for turn in range(4):
                yaw=grid+turn*math.pi/2;normal=(math.sin(yaw),-math.cos(yaw));right=(math.cos(yaw),math.sin(yaw))
                offset=back_wall_mount_offset(parts,yaw,self.wall,yaw,normal,room_side=True)
                self.assertAlmostEqual(sum(offset[i]*normal[i] for i in range(2)),-.059)
                self.assertAlmostEqual(sum(offset[i]*right[i] for i in range(2)),0)
                self.assertIsNone(back_wall_mount_offset(parts,yaw,self.wall,yaw,[-n for n in normal],room_side=True))

    def test_mount_clears_the_actual_rear_face_without_moving_along_wall(self):
        for grid in (0,.7):
            for turn in range(4):
                yaw=grid+turn*math.pi/2;front=(math.sin(yaw),-math.cos(yaw));right=(math.cos(yaw),math.sin(yaw))
                delta=[-.15*front[i]+.08*right[i] for i in range(2)]
                offset=back_wall_mount_offset(self.basin,yaw,self.wall,yaw,delta)
                self.assertAlmostEqual(sum(offset[i]*front[i] for i in range(2)),.41)
                self.assertAlmostEqual(sum(offset[i]*right[i] for i in range(2)),0)
                self.assertAlmostEqual(sum(offset[i]*front[i] for i in range(2))-(.55-.15),.01)

    def test_no_pullback_side_wall_front_wall_or_uncertain_angle(self):
        for delta,angle in [([0,.8],0),([1,.15],0),([0,-.2],0),([0,.15],.2),([0,2],0)]:
            self.assertIsNone(back_wall_mount_offset(self.basin,0,self.wall,angle,delta))
        oversized=[{'min':[-.5,-1,0],'max':[.5,1,2.8]}]
        self.assertIsNone(back_wall_mount_offset(self.basin,0,oversized,0,[0,.15]))

    def test_wall_axis_is_independent_of_the_fixture_facing(self):
        for turn in range(4):
            yaw=turn*math.pi/2;front=(math.sin(yaw),-math.cos(yaw))
            offset=back_wall_mount_offset(self.basin,yaw,self.wall,0,[-.15*n for n in front])
            self.assertAlmostEqual(sum(a*b for a,b in zip(offset,front)),.36 if turn%2 else .41)

    def test_overhead_footing_and_textured_bounds_do_not_push_the_basin(self):
        ignored=[{'min':[-.5,-.9,1.1],'max':[.5,.5,2.8]},
                 {'min':[-.5,-.9,0],'max':[.5,.5,.6]},
                 {'min':[-.5,-.9,0],'max':[.5,.5,2.8],'surface':'Cutout'},
                 {'min':[-.5,-.9,0],'max':[.5,.5,2.8],'shape':'Ellipsoid'}]
        self.assertIsNone(back_wall_mount_offset(self.basin,0,ignored,0,[0,.15]))
        self.assertEqual(back_wall_mount_offset(self.basin,0,self.wall+ignored,0,[0,.15]),[0,-.41000000000000003,0])

    def test_same_grid_exact_anchored_support_is_resolved_even_outside_export_crop(self):
        records={1:{'prototype':'','components':{'MapGrid':{},'Transform':{'parent':0}}},
                 2:{'prototype':'','components':{'MapGrid':{},'Transform':{'parent':0}}},
                 3:{'prototype':'Sink','components':{'Transform':{'parent':1,'pos':'.42,.35'}}},
                 4:{'prototype':'Wall','components':{'Transform':{'parent':1,'pos':'.5,.5'}}}}
        defaults={'Wall':{'Transform':{'anchored':True}}}
        models=[{'id':'Basin','backWallMountTargets':['Wall'],'parts':self.basin},
                {'id':'Panel','sourcePrototypes':['Wall'],'parts':self.wall}]
        e={'id':3,'modelId':'Basin','position':[.42,.35,0],'yaw':0,'matchKind':'exact'}
        before=copy.deepcopy(records)
        variants,stats=resolve_layout([e],models,records,defaults,WorldTransforms(records,defaults));resolve_placements([e],models,variants)
        self.assertEqual(e['renderOffset'],[0,-.41,0]);self.assertEqual(e['backWallMount'],4)
        self.assertEqual(e['position'],[.42,.35,0]);self.assertEqual(e['renderYaw'],0)
        self.assertEqual(records,before);self.assertEqual(stats['backWallMountedFixtures'],1)
        for overrides in ({'parent':2},{'anchored':False},{'pos':'1.5,.5'}):
            records[4]['components']['Transform']={**before[4]['components']['Transform'],**overrides}
            variants,_=resolve_layout([e],models,records,defaults,WorldTransforms(records,defaults));resolve_placements([e],models,variants)
            self.assertNotIn('backWallMount',e);self.assertNotIn('renderOffset',e)
        records[4]=before[4];models[1]['sourcePrototypes']=['OtherWall']
        resolve_layout([e],models,records,defaults,WorldTransforms(records,defaults));self.assertNotIn('backWallMount',e)

    def test_opt_in_targets_cannot_conflict_with_other_placement_rules(self):
        raw={'id':'B','label':'Basin','sourcePrototypes':[],'backWallMountTargets':['Wall'],'parts':[{'min':'-.3,-.4,.65','max':'.3,0,.85'}]}
        self.assertEqual(validate_model(raw)['backWallMountTargets'],['Wall'])
        for change in ({'backWallMountTargets':['Wall','Wall']},{'backWallMountTargets':'Wall'},
                       {'windowMountTargets':['Glass']},{'connectToNeighbours':True}):
            with self.assertRaisesRegex(ValueError,'rear wall mounting'):validate_model({**raw,**change})
        self.assertTrue(validate_model({**raw,'wallMounted':True})['wallMounted'])
        self.assertEqual(validate_model({**raw,'placement':'surface'})['placement'],'surface')

    def test_wall_fixture_normalizes_pivot_before_clearing_trim(self):
        records={1:{'prototype':'','components':{'MapGrid':{},'Transform':{'parent':0}}},
                 2:{'prototype':'Mirror','components':{'Transform':{'parent':1,'pos':'.62,.38'}}},
                 3:{'prototype':'Wall','components':{'Transform':{'parent':1,'pos':'.5,.5'}}}}
        defaults={'Mirror':{'Sprite':{'snapCardinals':True}},'Wall':{'Transform':{'anchored':True},'IconSmooth':{'key':'walls'}}}
        parts=[{'min':[-.2,-.556,1.2],'max':[.2,-.502,1.8]}]
        models=[{'id':'Mirror','parts':parts,'wallMounted':True,'useEntityRotation':True,'backWallMountTargets':['Wall']},
                {'id':'Wall','parts':self.wall,'sourcePrototypes':['Wall']}]
        for turn in range(4):
            yaw=turn*math.pi/2;e={'id':2,'modelId':'Mirror','position':[.62,.38,0],'yaw':yaw}
            variants,_=resolve_layout([e],models,records,defaults,WorldTransforms(records,defaults));resolve_placements([e],models,variants)
            front=(math.sin(yaw),-math.cos(yaw));right=(math.cos(yaw),math.sin(yaw));delta=[e['position'][i]+e['renderOffset'][i]-.5 for i in range(2)]
            self.assertAlmostEqual(sum(delta[i]*front[i] for i in range(2)),.008 if turn%2 else .058)
            self.assertAlmostEqual(sum(delta[i]*right[i] for i in range(2)),.12*right[0]-.12*right[1])
            self.assertEqual(e['position'],[.62,.38,0]);self.assertEqual(e['backWallMount'],3)

    def test_adjacent_room_mirror_keeps_its_existing_flipped_mount(self):
        records={1:{'prototype':'','components':{'MapGrid':{},'Transform':{'parent':0}}},
                 2:{'prototype':'Mirror','components':{'Transform':{'parent':1,'pos':'.5,1.5'}}},
                 3:{'prototype':'Wall','components':{'Transform':{'parent':1,'pos':'.5,.5'}}}}
        defaults={'Wall':{'Transform':{'anchored':True},'IconSmooth':{'key':'walls'}}}
        models=[{'id':'Mirror','parts':[{'min':[-.2,-.556,1.2],'max':[.2,-.502,1.8]}],'wallMounted':True,'backWallMountTargets':['Wall']},
                {'id':'Wall','parts':self.wall,'sourcePrototypes':['Wall']}]
        e={'id':2,'modelId':'Mirror','position':[.5,1.5,0],'yaw':0};variants,_=resolve_layout([e],models,records,defaults,WorldTransforms(records,defaults))
        self.assertEqual(e['geometryKey'],'Mirror:inside-wall');self.assertNotIn('backWallMount',e)
        self.assertAlmostEqual(variants[e['geometryKey']][0]['min'][1],-.498)

    def test_connected_wall_artwork_uses_grid_axis_without_changing_support_volume(self):
        records={1:{'prototype':'','components':{'MapGrid':{},'Transform':{'parent':0,'rot':90}}},
                 3:{'prototype':'Sink','components':{'Transform':{'parent':1,'pos':'.42,.35'}}},
                 4:{'prototype':'Wall','components':{'Transform':{'parent':1,'pos':'.5,.5'}}}}
        defaults={'Wall':{'Transform':{'anchored':True},'Sprite':{'noRot':True},'IconSmooth':{'key':'walls'}}}
        patches=[dict(min=[l,b,2.78],max=[r,t,2.8],surface='Cap',surfaceAxis='XY') for l,b,r,t in ((0,-.5,.5,.3125),(0,.3125,.5,.5),(-.5,.3125,0,.5),(-.5,-.5,0,.3125))]
        models=[{'id':'Basin','backWallMountTargets':['Wall'],'parts':self.basin},
                {'id':'Panel','sourcePrototypes':['Wall'],'parts':patches+self.wall,'cornerSurfaces':['Cap']*32}]
        e={'id':3,'modelId':'Basin','position':[-.35,.42,0],'yaw':math.pi/2,'matchKind':'exact'}
        variants,_=resolve_layout([e],models,records,defaults,WorldTransforms(records,defaults));resolve_placements([e],models,variants)
        self.assertEqual(e['renderOffset'],[.41,0,0])
        defaults['Wall']['IconSmooth']['enabled']=False
        variants,_=resolve_layout([e],models,records,defaults,WorldTransforms(records,defaults));resolve_placements([e],models,variants)
        self.assertEqual(e['renderOffset'],[.36,0,0])
