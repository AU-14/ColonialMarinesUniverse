import math
import unittest
from placement import resolve_placements, surfaces


class PlacementTests(unittest.TestCase):
    def models(self):
        return [{'id':'Table','supportSurface':'top','parts':[{'label':'top','min':[0,-.2,.7],'max':[2,.2,.86]}]},
                {'id':'Phone','placement':'surface','parts':[{'min':[-.1,-.1,.1],'max':[.1,.1,.5]}]}]

    def entity(self,uid,model,x,y,z=0):
        return {'id':uid,'modelId':model,'position':[x,y,z],'yaw':math.pi/2 if model=='Table' else 0,'matchKind':'exact'}

    def test_actual_rotated_footprint_and_minimum_height(self):
        entries=[self.entity(1,'Table',4,5),self.entity(2,'Phone',4,6),self.entity(3,'Phone',5,5)]
        stats=resolve_placements(entries,self.models())
        self.assertEqual(entries[1]['position'],[4,6,0])
        self.assertEqual(entries[1]['renderOffset'],[0,0,.762])
        self.assertEqual(entries[1]['support']['entity'],1)
        self.assertNotIn('renderOffset',entries[2])
        self.assertEqual(stats,{'surfacePropsPlaced':1,'surfacePropsWithoutSupport':1})

    def test_different_floor_inherited_support_and_rebuild_do_not_float(self):
        table=self.entity(1,'Table',4,5)
        prop=self.entity(2,'Phone',4,6,1)
        resolve_placements([table,prop],self.models());self.assertNotIn('support',prop)
        prop['position'][2]=0;table['matchKind']='inherited'
        resolve_placements([table,prop],self.models());self.assertNotIn('support',prop)
        table['matchKind']='exact'
        resolve_placements([table,prop],self.models());self.assertIn('support',prop)
        resolve_placements([prop],self.models());self.assertNotIn('support',prop)
        self.assertNotIn('renderOffset',prop)

    def test_nearest_support_has_deterministic_tie_break(self):
        entries=[self.entity(9,'Table',4,5),self.entity(1,'Table',4,5),self.entity(2,'Phone',4,6)]
        resolve_placements(entries,self.models())
        self.assertEqual(entries[2]['support']['entity'],1)

    def test_map_axis_ground_offset_keeps_all_facings_at_the_same_pivot(self):
        models = self.models()
        models[0]['groundOffset'] = [.5, 0]
        models[1]['groundOffset'] = [-.5, 0]
        for yaw in (0, math.pi/2, math.pi, -math.pi/2):
            table = self.entity(1,'Table',4,5);table['yaw'] = yaw
            prop = self.entity(2,'Phone',5 + math.cos(yaw),5 + math.sin(yaw))
            saved = [*prop['position']]
            stats = resolve_placements([table,prop],models)
            self.assertEqual(table['renderOffset'],[.5,0,0])
            self.assertEqual(prop['position'],saved)
            self.assertEqual(prop['renderOffset'],[-.5,0,.762])
            self.assertEqual(stats['surfacePropsPlaced'],1)
            resolve_placements([table,prop],models)
            self.assertEqual(prop['renderOffset'],[-.5,0,.762])

    def boards(self):
        return {'id':'Table', 'supportSurfaces':['left','right'], 'parts':[
            {'label':'left','min':[-.4,-.4,.1],'max':[-.2,.4,.15]},
            {'label':'right','min':[.2,-.4,.1],'max':[.4,.4,.15]}]}

    def test_separate_rotated_boards_leave_gaps_and_other_floors_unsupported(self):
        library=[self.boards(),self.models()[1]]
        entries=[self.entity(1,'Table',4,5),self.entity(2,'Phone',4,4.7),
                 self.entity(3,'Phone',4,5.3),self.entity(4,'Phone',4,5),self.entity(5,'Phone',4,5.3,1)]
        stats=resolve_placements(entries,library)
        self.assertEqual(stats,{'surfacePropsPlaced':2,'surfacePropsWithoutSupport':2})
        for entity in entries[1:3]: self.assertEqual(entity['renderOffset'],[0,0,.052])
        for entity in entries[3:]: self.assertNotIn('support',entity)
        entries[0]['matchKind']='inherited'
        self.assertEqual(resolve_placements(entries,library)['surfacePropsPlaced'],0)

    def test_highest_overlapping_board_is_order_independent(self):
        board=self.boards()
        board['parts'][1].update(min=[-.4,-.4,.2],max=[-.2,.4,.3])
        entries=[self.entity(1,'Table',4,5),self.entity(2,'Phone',4,4.7)]
        for labels in (['left','right'],['right','left']):
            board['supportSurfaces']=labels
            resolve_placements(entries,[board,self.models()[1]])
            self.assertEqual(entries[1]['renderOffset'],[0,0,.202])

    def test_invalid_multi_support_declarations_are_not_partially_accepted(self):
        for labels in (['left','missing'],['left','left'],['left',''],[None], 'left'):
            self.assertEqual(surfaces({**self.boards(),'supportSurfaces':labels}),[])
        for patch in ({'supportSurface':'left'}, {'connectToNeighbours':True}):
            self.assertEqual(surfaces({**self.boards(),**patch}),[])
        for patch in ({'shape':'Ellipsoid'}, {'max':[-.2,.4,0]}, {'max':[-.2,.4,float('nan')]}):
            model=self.boards();model['parts'][1].update(patch)
            self.assertEqual(surfaces(model),[])

    def test_multi_support_uses_resolved_geometry(self):
        board=self.boards()
        parts=[{**board['parts'][0],'min':[-.4,-.4,.4],'max':[-.2,.4,.5]},board['parts'][1]]
        table=self.entity(1,'Table',4,5);table['geometryKey']='raised'
        prop=self.entity(2,'Phone',4,4.7)
        resolve_placements([table,prop],[board,self.models()[1]],{'raised':parts})
        self.assertEqual(prop['renderOffset'],[0,0,.402])
        self.assertEqual(board['parts'][0]['max'][2],.15)
