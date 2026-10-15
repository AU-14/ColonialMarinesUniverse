import copy
import math
import unittest

from build_models import validate_model
from layout import fit_panel_ends, panel_end_limits, resolve_layout, window_mount_offset
from placement import resolve_placements
from scene import WorldTransforms


class PanelEndFitTests(unittest.TestCase):
    pane = [{'label':'pane','min':[-.49,-.46,.036],'max':[.49,-.38,1.5]}]
    curtain = [{'label':'cloth','min':[-.49,-.505,.08],'max':[.49,-.36,2.5]}]

    def test_inside_mount_gap_is_invariant_under_camera_independent_saved_facing(self):
        for grid in (0,.7):
            for turn in range(4):
                yaw=grid+turn*math.pi/2
                offset=window_mount_offset(self.curtain,yaw,self.pane,yaw,[0,0],True)
                self.assertAlmostEqual(offset[0],-.145*math.sin(yaw))
                self.assertAlmostEqual(offset[1],.145*math.cos(yaw))
                self.assertAlmostEqual(-.505+.145-(-.38),.02)

    def test_butt_end_keeps_all_members_and_a_gap_under_grid_rotation(self):
        source=copy.deepcopy(self.pane)
        for grid in (0,.7):
            left,right=panel_end_limits(self.pane,grid+math.pi/2,self.pane,grid,[0,0])
            fitted=fit_panel_ends(self.pane,left,right)
            self.assertAlmostEqual(left,-.37)
            self.assertAlmostEqual(right,.49)
            self.assertAlmostEqual(fitted[0]['min'][0],-.37)
            self.assertEqual(fitted[0]['min'][1:],self.pane[0]['min'][1:])
        self.assertEqual(self.pane,source)

    def test_oversized_obstruction_and_rotated_geometry_are_not_silently_squashed(self):
        for ends in ((-.1,.1),(-.6,.49),(-.49,.6),(float('nan'),.49)):
            self.assertIs(fit_panel_ends(self.pane,*ends),self.pane)
        parts=[{**self.pane[0],'yaw':15}]
        self.assertIs(fit_panel_ends(parts,-.4,.49),parts)
        self.assertEqual(panel_end_limits(self.pane,0,self.pane,.2,[0,0]),(-.49,.49))

    def test_front_wall_ribs_do_not_masquerade_as_two_end_supports(self):
        ribs=[{'min':[-.5,-.5,0],'max':[-.36,.5,2.6]},
              {'min':[.36,-.5,0],'max':[.5,.5,2.6]}]
        self.assertEqual(panel_end_limits(self.pane,0,ribs,0,[0,0]),(-.49,.49))

    def test_live_source_neighbors_resolve_without_export_order_or_same_grid_leakage(self):
        self.assert_source_neighbor_placement()

    def test_raw_yaml_coordinate_strings_resolve_like_exported_vectors(self):
        self.pane = [{**p, 'min': ','.join(map(str, p['min'])), 'max': ','.join(map(str, p['max']))} for p in self.pane]
        self.curtain = [{**p, 'min': ','.join(map(str, p['min'])), 'max': ','.join(map(str, p['max']))} for p in self.curtain]
        self.assert_source_neighbor_placement()

    def assert_source_neighbor_placement(self):
        records={1:{'prototype':'','components':{'MapGrid':{},'Transform':{'parent':0}}},
                 2:{'prototype':'','components':{'MapGrid':{},'Transform':{'parent':0}}},
                 3:{'prototype':'Pane','components':{'Transform':{'parent':1,'pos':'.5,.5','rot':f'{math.pi/2} rad'}}},
                 4:{'prototype':'Pane','components':{'Transform':{'parent':1,'pos':'.5,.5'}}},
                 5:{'prototype':'Curtain','components':{'Transform':{'parent':1,'pos':'.5,.5','rot':f'{math.pi/2} rad'}}}}
        defaults={'Pane':{'Transform':{'anchored':True},'Sprite':{'snapCardinals':True}},'Curtain':{'Transform':{'anchored':True}}}
        models=[{'id':'P','sourcePrototypes':['Pane'],'sourceDirections':1,'useEntityRotation':True,'panelEndTargets':['Pane'],'parts':self.pane},
                {'id':'C','sourcePrototypes':['Curtain'],'useEntityRotation':True,'windowMountTargets':['Pane'],'windowMountInside':True,'parts':self.curtain}]
        entities=[{'id':uid,'modelId':'C' if uid==5 else 'P','position':[.5,.5,0],
                   'yaw':0 if uid==4 else math.pi/2,'matchKind':'exact'} for uid in (3,4,5)]
        original=copy.deepcopy(records)
        for ordered in (entities,list(reversed(entities))):
            variants,stats=resolve_layout(ordered,models,records,defaults,WorldTransforms(records,defaults))
            resolve_placements(ordered,models,variants)
            self.assertEqual(entities[0]['panelEndFit']['left'],-.37)
            self.assertNotIn('panelEndFit',entities[1])
            self.assertEqual(entities[2]['renderOffset'],[-.145,0,0])
            self.assertEqual(stats['fittedPanelEnds'],1)
        self.assertEqual(records,original)
        # Cropped exports still use the off-crop anchored neighbor.
        resolve_layout([entities[0]],models,records,defaults,WorldTransforms(records,defaults))
        self.assertIn('panelEndFit',entities[0])
        for override in ({'parent':2},{'anchored':False},{'pos':'4.5,.5'}):
            records[4]['components']['Transform']={**original[4]['components']['Transform'],**override}
            resolve_layout([entities[0]],models,records,defaults,WorldTransforms(records,defaults))
            self.assertNotIn('panelEndFit',entities[0])
            self.assertNotIn('geometryKey',entities[0])

    def test_metadata_rejects_unsupported_compositions(self):
        base={'id':'Panel','label':'Panel','parts':self.pane,'panelEndTargets':['Wall']}
        validate_model(base)
        for change in ({'panelEndTargets':'Wall'},{'panelEndTargets':['Wall','Wall']},
                       {'connectToNeighbours':True},{'backWallMountTargets':['Wall']},
                       {'parts':[{**self.pane[0],'yaw':5}]}):
            with self.assertRaisesRegex(ValueError,'panelEndTargets'):validate_model({**base,**change})
        with self.assertRaisesRegex(ValueError,'windowMountInside'):
            validate_model({'id':'C','label':'C','parts':self.curtain,'windowMountInside':True})
