import copy
import math
import unittest

from build_models import validate_model
from layout import (connected_parts, resolve_layout, window_mount_offset, shutter_exterior_target,
                    shutter_exterior_axis, shutter_mount_envelope)
from placement import resolve_placements
from scene import WorldTransforms


class WindowMountTests(unittest.TestCase):
    shutter = [{'label': 'housing', 'min': [-.5,-.13,2.48], 'max': [.5,.13,2.74]}]
    window = [{'label': 'sill', 'min': [-.5,-.15,0], 'max': [.5,.15,.65]},
              {'label': 'glass', 'min': [-.5,-.035,.65], 'max': [.5,.035,2.6]}]

    def test_saved_facing_and_rotated_grid_select_the_same_clearance_for_each_axis(self):
        for grid in (0, .7):
            for turn in range(4):
                yaw=grid+turn*math.pi/2
                parts=connected_parts(self.window, 3 if turn%2 else 12)
                normal=(math.sin(yaw),-math.cos(yaw))
                offset=window_mount_offset(self.shutter,yaw,parts,grid,[.1*normal[0],.1*normal[1]])
                self.assertAlmostEqual(offset[0],.4*normal[0])
                self.assertAlmostEqual(offset[1],.4*normal[1])
                # The shutter rear and nearest sill face are separated by the assembly gap.
                self.assertAlmostEqual(sum(offset[i]*normal[i] for i in range(2))-.13-(.1+.15),.02)

    def test_corner_perpendicular_empty_and_already_clear_mounts_do_not_get_guessed(self):
        self.assertIsNone(window_mount_offset(self.shutter,0,connected_parts(self.window,5),0,[0,0]))
        self.assertIsNone(window_mount_offset(self.shutter,0,connected_parts(self.window,3),0,[0,0]))
        self.assertIsNone(window_mount_offset([],0,self.window,0,[0,0]))
        self.assertEqual(window_mount_offset(self.shutter,0,self.window,0,[0,1]),[0,0,0])

    def test_mount_uses_exact_anchored_same_grid_glazing_even_outside_the_exported_entity_list(self):
        records={1:{'prototype':'','components':{'MapGrid':{},'Transform':{'parent':0}}},
                 2:{'prototype':'','components':{'MapGrid':{},'Transform':{'parent':0}}},
                 3:{'prototype':'Shutter','components':{'Transform':{'parent':1,'pos':'.5,.5'}}},
                 4:{'prototype':'Glass','components':{'Transform':{'parent':1,'pos':'.5,.5'}}},
                 5:{'prototype':'Wall','components':{'Transform':{'parent':1,'pos':'1.5,.5'}}}}
        defaults={'Shutter':{'Transform':{'anchored':True}},
                  'Glass':{'Transform':{'anchored':True},'IconSmooth':{'key':'walls','base':'window'}},
                  'Wall':{'Transform':{'anchored':True},'IconSmooth':{'key':'walls'}}}
        models=[{'id':'Shutter','windowMountTargets':['Glass'],'parts':self.shutter},
                {'id':'Glass','sourcePrototypes':['Glass'],'connectToNeighbours':True,'parts':self.window}]
        e={'id':3,'modelId':'Shutter','position':[.5,.5,0],'yaw':0,'matchKind':'exact'}
        before=copy.deepcopy(records)
        variants,stats=resolve_layout([e],models,records,defaults,WorldTransforms(records,defaults))
        resolve_placements([e],models,variants)
        self.assertEqual(e['renderOffset'],[0,-.3,0]); self.assertEqual(e['windowMount'],4)
        self.assertEqual(stats['windowMountedShutters'],1); self.assertEqual(e['position'],[.5,.5,0])
        self.assertEqual(records,before)
        # Reusing a scene record after removing the target must clear stale mounting metadata.
        for overrides in ({'parent':2},{'anchored':False},{'pos':'1.5,.5'}):
            records[4]['components']['Transform']={**before[4]['components']['Transform'],**overrides}
            variants,_=resolve_layout([e],models,records,defaults,WorldTransforms(records,defaults))
            resolve_placements([e],models,variants)
            self.assertNotIn('renderOffset',e); self.assertNotIn('windowMount',e)
        records[4]=before[4]
        defaults['Glass']['IconSmooth']['enabled']=False
        resolve_layout([e],models,records,defaults,WorldTransforms(records,defaults))
        self.assertNotIn('windowMount',e)

    def test_mount_target_validation_rejects_ambiguous_layout_metadata(self):
        raw={'id':'S','label':'Shutter','sourcePrototypes':[],'windowMountTargets':['Glass'],
             'parts':[{'min':'-.5,-.13,0','max':'.5,.13,2.74'}]}
        self.assertEqual(validate_model(raw)['windowMountTargets'],['Glass'])
        for change in ({'windowMountTargets':['Glass','Glass']},{'windowMountTargets':'Glass'},
                       {'connectToNeighbours':True},{'wallMounted':True},{'placement':'surface'}):
            with self.assertRaisesRegex(ValueError,'window mounting'):
                validate_model({**raw,**change})

    def test_thick_observation_window_has_an_explicit_connected_exterior_face(self):
        body=[{'min':[-.5,-.515,0],'max':[.5,.515,2.78]}]
        for grid in (0,.7):
            for turn in range(4):
                yaw=grid+turn*math.pi/2
                mask=3 if turn%2 else 12
                axis=shutter_exterior_axis('RMCWindowPrisonCell',mask)
                part=body if turn%2==0 else [{'min':[-.515,-.5,0],'max':[.515,.5,2.78]}]
                offset=window_mount_offset(self.shutter,yaw,part,grid,[0,0],exterior_axis=axis)
                normal=(math.sin(yaw),-math.cos(yaw))
                self.assertAlmostEqual(sum(offset[i]*normal[i] for i in range(2)),.665)
                self.assertAlmostEqual(sum(offset[i]*normal[i] for i in range(2))-.13-.515,.02)
        for mask in (0,5,15):
            self.assertIsNone(shutter_exterior_axis('RMCWindowPrisonCell',mask))
        self.assertIsNone(window_mount_offset(self.shutter,math.pi/2,body,0,[0,0],exterior_axis=1))
        self.assertIsNone(window_mount_offset(self.shutter,0,body,0,[.01,0],exterior_axis=1))
        self.assertIsNone(window_mount_offset(self.shutter,0,body,0,[0,0],inside=True,exterior_axis=1))
        self.assertTrue(shutter_exterior_target('CMU3DHybrisaWindowShutter','CMAirlockGlassHybrisa'))
        self.assertFalse(shutter_exterior_target('CMU3DHybrisaWindowShutter','RMCWindowPrisonCell'))
        self.assertFalse(shutter_exterior_target('Curtain','RMCWindowPrisonCell'))
        self.assertFalse(shutter_exterior_target('CMU3DHybrisaWindowShutter','UnknownSquareWindow'))

    def test_support_envelope_includes_open_closed_and_authored_transition_depth(self):
        closed={'id':'Closed','parts':[{'min':[-.5,-.18,0],'max':[.5,.18,2.7]}],
                'alternateDoorModel':'Open','sourceDirections':4}
        opened={'id':'Open','parts':[{'min':[-.5,-.20,0],'max':[-.4,.20,2.7]}],
                'alternateDoorModel':'Closed','sourceDirections':4,
                'doorSpriteStates':{'opening':{'frames':[{'parts':[{'min':[-.5,-.25,0],'max':[-.4,.25,2.7]}]}]}}}
        original=copy.deepcopy((closed,opened));library={'Closed':closed,'Open':opened}
        for model in (closed,opened):
            parts=shutter_mount_envelope(model,library)
            self.assertEqual(window_mount_offset(self.shutter,0,parts,0,[0,0],exterior_axis=1),[0,-.4,0])
        self.assertEqual((closed,opened),original)
        self.assertIsNone(shutter_mount_envelope(closed,{'Closed':closed}))
        opened['yawOffset']=90
        self.assertIsNone(shutter_mount_envelope(closed,library))

    def test_exact_door_opt_in_keeps_saved_transform_and_selects_both_pose_envelope(self):
        records={1:{'prototype':'','components':{'MapGrid':{},'Transform':{'parent':0}}},
                 3:{'prototype':'RMCShutterHybrisaWindow','components':{'Transform':{'parent':1,'pos':'.5,.5'}}},
                 4:{'prototype':'CMAirlockGlassHybrisa','components':{'Transform':{'parent':1,'pos':'.5,.5'}}}}
        defaults={p:{'Transform':{'anchored':True}} for p in ('RMCShutterHybrisaWindow','CMAirlockGlassHybrisa')}
        models=[{'id':'CMU3DHybrisaWindowShutter','windowMountTargets':['CMAirlockGlassHybrisa'],'parts':self.shutter},
                {'id':'Closed','sourcePrototypes':['CMAirlockGlassHybrisa'],'parts':[{'min':[-.5,-.18,0],'max':[.5,.18,2.7]}],'alternateDoorModel':'Open'},
                {'id':'Open','parts':[{'min':[-.5,-.20,0],'max':[-.4,.20,2.7]}]}]
        e={'id':3,'modelId':'CMU3DHybrisaWindowShutter','position':[.5,.5,0],'yaw':0,'matchKind':'exact'}
        before=copy.deepcopy(records)
        variants,_=resolve_layout([e],models,records,defaults,WorldTransforms(records,defaults))
        resolve_placements([e],models,variants)
        self.assertEqual(e['position'],[.5,.5,0]);self.assertEqual(e['yaw'],0)
        self.assertEqual(e['windowMount'],4);self.assertEqual(e['renderOffset'],[0,-.35,0])
        self.assertEqual(records,before)
        records[4]['components']['Transform']['pos']='.51,.5'
        resolve_layout([e],models,records,defaults,WorldTransforms(records,defaults))
        self.assertNotIn('windowMount',e)
