import math,unittest
import yaml
import build_models as bm
from layout import connected_parts,render_yaw


class ColonyUltraWindowTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.models={m['id']:bm.validate_model(m) for m in yaml.load((bm.WORLD_SOURCE/'garrison_colony_ultra_windows.yml').read_text(encoding='utf-8'),Loader=yaml.CSafeLoader)}

    def test_broken_replacement_retains_structure_and_removes_every_pane_in_all_connections(self):
        intact=self.models['CMU3DColonyReinforcedWindow'];broken=self.models['CMU3DColonyReinforcedWindowFrame']
        self.assertEqual(intact['parts'][:len(broken['parts'])],broken['parts'])
        self.assertEqual(broken['sourcePrototypes'],['RMCWindowFrameColonyReinforced'])
        for mask in range(16):
            a=connected_parts(intact['parts'],mask,end_inset=intact['connectionEndInset']);b=connected_parts(broken['parts'],mask,end_inset=broken['connectionEndInset'])
            self.assertEqual([p for p in a if not p['label'].startswith('Colony pane')],b)
            self.assertTrue(all(bm.rgba(p['color'])[3]==1 for p in b))
            self.assertLessEqual(len(a),128)
            self.assertFalse(any('lintel' in p['label'] for p in b))
            # Every assembled member has positive volume, including clipped junction members.
            self.assertTrue(all(all(x<y for x,y in zip(p['min'],p['max'])) for p in a+b))

    def test_colony_joins_have_no_carriers_on_connected_ends(self):
        source=self.models['CMU3DColonyReinforcedWindow']['parts']
        straight=connected_parts(source,12,end_inset=.06)
        self.assertFalse(any('free end carrier' in p['label'] for p in straight))
        self.assertTrue(any(p['min'][0]==-.5 and p['max'][0]==.5 and p['label']=='Deep colony sill' for p in straight))
        northsouth=connected_parts(source,3,end_inset=.06)
        self.assertTrue(any(p['min'][1]==-.5 and p['max'][1]==.5 and p['label']=='Deep colony sill' for p in northsouth))

    def test_free_ends_clear_wall_trim_without_shortening_joined_edges(self):
        source=[{'min':[-.44,-.1,0],'max':[.44,.1,2]}]
        for mask in range(16):
            parts=connected_parts(source,mask,end_inset=.06)
            for flag,axis,high in ((1,1,True),(2,1,False),(4,0,True),(8,0,False)):
                if mask & flag:
                    edge=(max(p['max'][axis] for p in parts) if high else min(p['min'][axis] for p in parts))
                    self.assertAlmostEqual(edge,.5 if high else -.5)
        self.assertEqual(connected_parts(source,4,end_inset=.06)[0]['min'][0],-.44)
        self.assertEqual(connected_parts(source,8,end_inset=.06)[0]['max'][0],.44)
        self.assertEqual(source[0]['max'][0],.44)

    def test_bad_endpoint_extensions_are_rejected(self):
        base={'id':'Panel','label':'Panel','parts':[{'min':[-.44,-.1,0],'max':[.44,.1,2]}],'connectToNeighbours':True}
        for value in (float('nan'),-.01,.21,'0.06',True):
            with self.assertRaises(ValueError):bm.validate_model({**base,'connectionEndInset':value})
        with self.assertRaises(ValueError):bm.validate_model({**base,'connectToNeighbours':False,'connectionEndInset':.06})

    def test_ultra_glazing_uses_two_source_spaced_rails_and_exact_directional_fixture_footprint(self):
        m=self.models['CMU3DUltraDirectionalWindow']
        rails=[p for p in m['parts'] if 'reinforcing rail' in p['label']]
        self.assertEqual(len(rails),2)
        self.assertEqual(sorted((p['min'][0]+p['max'][0])/2 for p in rails),[-.3125,.3125])
        self.assertEqual(m['referenceState'],'fwindow')
        self.assertFalse(m.get('connectToNeighbours',False))
        for p in m['parts']:
            self.assertGreaterEqual(p['min'][0],-.49);self.assertLessEqual(p['max'][0],.49)
            self.assertGreaterEqual(p['min'][1],-.49);self.assertLessEqual(p['max'][1],-.38)
        for turn in range(4):
            yaw=turn*math.pi/2
            self.assertAlmostEqual(render_yaw(yaw,m['sourceDirections'],True),yaw)
