import math
import unittest

from layout import connected_parts, render_yaw, resolve_layout, wall_mount_offset, face_away_from_wall_yaw, wall_target_yaw, offset_target_mask
from placement import resolve_placements
from scene import WorldTransforms


class LayoutTests(unittest.TestCase):
    def test_half_tile_fixture_target_preserves_grid_sides_and_ambiguity(self):
        for grid in (0,.7):
            for (x,y),expected in [((.5,.02),4),((-.5,0),8),((0,.5),1),((.02,-.5),2),((.3,.3),0),((.1,0),0)]:
                c,s=math.cos(grid),math.sin(grid)
                self.assertEqual(offset_target_mask((c*x-s*y,s*x+c*y),grid),expected)
        records={1:{'prototype':'','components':{'MapGrid':{},'Transform':{'parent':0}}},
                 2:{'prototype':'Mirror','components':{'Transform':{'parent':1,'pos':'.5,.5'}}},
                 3:{'prototype':'Wall','components':{'Transform':{'parent':1,'pos':'.5,.5'}}},
                 4:{'prototype':'Basin','components':{'Transform':{'parent':1,'pos':'.48,0'}}}}
        defaults={'Wall':{'Transform':{'anchored':True},'IconSmooth':{'key':'walls'}}}
        model={'id':'Mirror','wallMounted':True,'useEntityRotation':True,'wallFacingTargets':['Basin'],'parts':[]}
        e={'id':2,'modelId':'Mirror','yaw':-math.pi/2,'position':[.5,.5,0]}
        resolve_layout([e],[model],records,defaults,WorldTransforms(records,defaults));self.assertEqual(e['renderYaw'],0)
        self.assertEqual(e['yaw'],-math.pi/2);self.assertEqual(e['position'],[.5,.5,0])
        records[2]['components']['Transform']['pos']='.969,.5';records[4]['components']['Transform']['pos']='1,0'
        e['position']=[.969,.5,0]
        resolve_layout([e],[model],records,defaults,WorldTransforms(records,defaults));self.assertEqual(e['renderYaw'],0)
        self.assertEqual(e['position'],[.969,.5,0])

    def test_vehicle_aliases_preserve_source_facing_without_changing_saved_transform(self):
        facings = [0, 2, 2, 0]
        model = {'id': 'Van', 'sourceDirections': 4, 'sourceCardinalFacings': facings, 'yawOffset': -90}
        for angle, front_x in zip((0, math.pi/2, math.pi, -math.pi/2), (-1, 1, 1, -1)):
            record = {1: {'prototype': 'Van', 'components': {}}}
            defaults = {'Van': {'Sprite': {'noRot': True}}}
            entity = {'id': 1, 'modelId': 'Van', 'yaw': angle, 'position': [3.5, 7.5, 0]}
            resolve_layout([entity], [model], record, defaults, WorldTransforms(record, defaults))
            self.assertAlmostEqual(math.sin(entity['renderYaw']), front_x)
            self.assertAlmostEqual(math.cos(entity['renderYaw']), 0)
            self.assertEqual(entity['yaw'], angle)
            self.assertEqual(entity['position'], [3.5, 7.5, 0])
        self.assertAlmostEqual(render_yaw(math.pi/2+.1, 4, offset=-90, cardinal_facings=facings), math.pi/2+.1)
        self.assertAlmostEqual(render_yaw(math.pi/2+.1, 4, no_rotation=True, offset=-90, cardinal_facings=facings), math.pi/2)

    def test_connected_supports_join_only_matching_grid_neighbours_and_support_seam_items(self):
        parts = [{'label': 'top', 'min': [-.47,-.44,.73], 'max': [.47,.44,.835]},
                 {'label': 'east rim', 'min': [.42,-.43,.833], 'max': [.46,.43,.836], 'omitWhenConnected': 4},
                 {'label': 'west rim', 'min': [-.46,-.43,.833], 'max': [-.42,.43,.836], 'omitWhenConnected': 8}]
        for mask in range(16):
            joined = connected_parts(parts, mask, 'top')
            self.assertEqual(joined[0]['min'], [-.5 if mask & 8 else -.47, -.5 if mask & 2 else -.44, .73])
            self.assertEqual(joined[0]['max'], [.5 if mask & 4 else .47, .5 if mask & 1 else .44, .835])
            self.assertEqual(len(joined), 1 + int(not mask & 4) + int(not mask & 8))
        self.assertEqual(parts[0]['max'], [.47,.44,.835])
        records = {
            1: {'prototype':'', 'components': {'MapGrid':{}, 'Transform':{'parent':0}}},
            2: {'prototype':'Table', 'components': {'Transform':{'parent':1,'pos':'.5,.5','rot':90}}},
            3: {'prototype':'Table', 'components': {'Transform':{'parent':1,'pos':'1.5,.5'}}},
            4: {'prototype':'Wall', 'components': {'Transform':{'parent':1,'pos':'.5,1.5'}}},
            5: {'prototype':'Pill', 'components': {'Transform':{'parent':1,'pos':'.99,.5'}}},
        }
        defaults = {p: {'Transform':{'anchored':True},'IconSmooth':{'key':key}} for p,key in [('Table','table'),('Wall','walls')]}
        models = [{'id':'Table', 'connectToNeighbours':True, 'supportSurface':'top', 'parts':parts},
                  {'id':'Pill', 'placement':'surface', 'parts':[{'min':[-.1,-.1,0],'max':[.1,.1,.3]}]}]
        entities = [{'id':2,'modelId':'Table','position':[.5,.5,0],'yaw':math.pi/2,'matchKind':'exact'},
                    {'id':5,'modelId':'Pill','position':[.99,.5,0],'yaw':0,'matchKind':'exact'}]
        variants, stats = resolve_layout(entities, models, records, defaults, WorldTransforms(records, defaults))
        self.assertEqual(entities[0]['connectionMask'], 4)
        self.assertEqual(entities[0]['renderYaw'], 0)
        self.assertEqual(entities[0]['yaw'], math.pi/2)
        self.assertEqual(stats['connectedSurfaceInstances'], 1)
        self.assertNotIn('connectionState', entities[0])
        resolve_placements(entities, models, variants)
        self.assertEqual(entities[1]['renderOffset'], [0,0,.837])
        self.assertEqual(entities[1]['position'], [.99,.5,0])
        # The neighbour beyond the exported crop was necessary; a wall cannot join a table.
        del records[3]
        variants, _ = resolve_layout(entities, models, records, defaults, WorldTransforms(records, defaults))
        resolve_placements(entities, models, variants)
        self.assertEqual(entities[0]['connectionMask'], 0)
        self.assertNotIn('renderOffset', entities[1])

    def test_corner_frame_permutation_joins_saved_north_west_pipe_endpoints(self):
        facings = [0, 1, 3, 2]
        yaw = render_yaw(-math.pi/2, 4, cardinal_facings=facings)
        pivot = (105.5, -45.5)
        def world(x, y):
            return (pivot[0]+math.cos(yaw)*x-math.sin(yaw)*y,
                    pivot[1]+math.sin(yaw)*x+math.cos(yaw)*y)
        self.assertLess(math.dist(world(0, -.5), (105.5, -45)), 1e-6)
        self.assertLess(math.dist(world(.5, 0), (105, -45.5)), 1e-6)
        self.assertAlmostEqual(render_yaw(math.pi+.1, 4, cardinal_facings=facings), 3*math.pi/2+.1)
        records = {1: {'prototype': 'Pipe', 'components': {}}}
        e = {'id': 1, 'modelId': 'Elbow', 'yaw': -math.pi/2, 'position': [*pivot, 0]}
        resolve_layout([e], [{'id': 'Elbow', 'sourceDirections': 4, 'sourceCardinalFacings': facings}], records, {}, WorldTransforms(records))
        self.assertAlmostEqual(e['renderYaw'], yaw)
        self.assertEqual(e['yaw'], -math.pi/2)
        self.assertEqual(e['position'], [*pivot, 0])

    def test_monitor_targets_choose_visible_wall_side_without_ambiguous_guessing(self):
        self.assertAlmostEqual(wall_target_yaw(math.pi, 0, True, 2, 12), 0)
        self.assertAlmostEqual(abs(wall_target_yaw(0, 0, False, 2, 1)), math.pi)
        self.assertEqual(wall_target_yaw(.2, 0, True, 3, 12), .2)
        self.assertEqual(wall_target_yaw(.2, 0, False, 2, 0), .2)
        self.assertAlmostEqual(wall_target_yaw(math.pi, math.pi/2, True, 2, 12), math.pi/2)

    def test_workstation_outside_crop_selects_monitor_mount_without_moving_pivot(self):
        records = {
            1: {'prototype': '', 'components': {'MapGrid': {}, 'Transform': {'parent': 0}}},
            2: {'prototype': 'Monitor', 'components': {'Transform': {'parent': 1, 'pos': '.5,.5'}}},
            3: {'prototype': 'Wall', 'components': {'Transform': {'parent': 1, 'pos': '.5,.5'}}},
            4: {'prototype': 'Computer', 'components': {'Transform': {'parent': 1, 'pos': '.5,-.5'}}},
        }
        defaults = {'Wall': {'Transform': {'anchored': True}, 'IconSmooth': {'key': 'walls'}},
                    'Computer': {'Physics': {'bodyType': 'Static'}}}
        model = {'id': 'Monitor', 'wallMounted': True, 'wallFacingTargets': ['Computer'],
                 'parts': [{'min': [-.4,-.68,1.3], 'max': [.4,-.501,2]}]}
        entity = {'id': 2, 'modelId': 'Monitor', 'yaw': math.pi, 'position': [.5,.5,0]}
        _, stats = resolve_layout([entity], [model], records, defaults, WorldTransforms(records, defaults))
        self.assertAlmostEqual(entity['renderYaw'], 0)
        self.assertEqual(entity['position'], [.5,.5,0])
        self.assertEqual(stats['workstationDisplayFacings'], 1)
        records[3]['components']['Transform']['pos'] = '.5,1.5'
        entity['yaw'] = 0
        variants, _ = resolve_layout([entity], [model], records, defaults, WorldTransforms(records, defaults))
        self.assertAlmostEqual(abs(entity['renderYaw']), math.pi)
        self.assertEqual(entity['geometryKey'], 'Monitor:inside-wall')
        self.assertGreater(variants[entity['geometryKey']][0]['min'][1], -.5)

    def test_directionless_counter_appliances_face_the_room_without_guessing_ambiguous_sides(self):
        self.assertAlmostEqual(face_away_from_wall_yaw(math.pi, 0, 8), math.pi/2)
        self.assertAlmostEqual(face_away_from_wall_yaw(math.pi/2, 0, 6), -math.pi/2)
        self.assertAlmostEqual(face_away_from_wall_yaw(math.pi, 0, 1), 0)
        self.assertEqual(face_away_from_wall_yaw(.2, 0, 0), .2)
        self.assertAlmostEqual(face_away_from_wall_yaw(math.pi/2, 0, 12), math.pi/2)
        self.assertAlmostEqual(face_away_from_wall_yaw(math.pi, math.pi/2, 4), 0)

    def test_appliance_context_reads_walls_beyond_crop_and_keeps_saved_transform(self):
        records = {
            1: {'prototype': '', 'components': {'MapGrid': {}, 'Transform': {'parent': 0}}},
            2: {'prototype': 'Dispenser', 'components': {'Transform': {'parent': 1, 'pos': '.5,.5'}}},
            3: {'prototype': 'Wall', 'components': {'Transform': {'parent': 1, 'pos': '.5,1.5'}}},
        }
        defaults = {'Wall': {'Transform': {'anchored': True}, 'IconSmooth': {'key': 'walls'}}}
        model = {'id': 'Dispenser', 'faceAwayFromWall': True, 'useEntityRotation': True}
        entity = {'id': 2, 'modelId': 'Dispenser', 'yaw': math.pi, 'position': [.5,.5,0]}
        _, stats = resolve_layout([entity], [model], records, defaults, WorldTransforms(records, defaults))
        self.assertAlmostEqual(entity['renderYaw'], 0)
        self.assertEqual(entity['yaw'], math.pi)
        self.assertEqual(entity['position'], [.5,.5,0])
        self.assertEqual(stats['applianceWallFacings'], 1)

    def test_reversed_side_frames_keep_operator_facing_and_residual_rotation(self):
        self.assertEqual(render_yaw(0, 4, swap_east_west=True), 0)
        self.assertEqual(render_yaw(math.pi, 4, swap_east_west=True), math.pi)
        self.assertAlmostEqual(render_yaw(-math.pi/2, 4, swap_east_west=True), math.pi/2)
        self.assertAlmostEqual(render_yaw(math.pi/2+.1, 4, swap_east_west=True), 3*math.pi/2+.1)
        self.assertAlmostEqual(render_yaw(-math.pi/2, 4, offset=90, swap_east_west=True), math.pi)
        records = {1: {'prototype': 'Overwatch', 'components': {}}}
        entity = {'id': 1, 'modelId': 'Console', 'yaw': -math.pi/2, 'position': [61.5,1.5,0]}
        resolve_layout([entity], [{'id':'Console','sourceDirections':4,'swapEastWest':True}], records, {}, WorldTransforms(records))
        self.assertAlmostEqual(entity['renderYaw'], math.pi/2)
        self.assertEqual(entity['yaw'], -math.pi/2)
        self.assertEqual(entity['position'], [61.5,1.5,0])

    def test_vendor_window_backing_is_opt_in_and_ignores_disabled_or_unanchored_glass(self):
        records = {
            1: {'prototype': '', 'components': {'MapGrid': {}, 'Transform': {'parent': 0}}},
            2: {'prototype': 'Vendor', 'components': {'Transform': {'parent': 1, 'pos': '.5,.5'}}},
            3: {'prototype': 'Glass', 'components': {'Transform': {'parent': 1, 'pos': '-.5,.5'}}},
        }
        for opt_in, anchored, enabled, key, expected in (
                (False, True, True, 'windows', 0), (True, True, True, 'windows', math.pi/2),
                (True, False, True, 'windows', 0), (True, True, False, 'windows', 0),
                (True, True, True, 'doors', 0)):
            with self.subTest(opt_in=opt_in, anchored=anchored, enabled=enabled, key=key):
                defaults = {'Glass': {'Transform': {'anchored': anchored}, 'IconSmooth': {'key': key, 'enabled': enabled}}}
                model = {'id': 'Vendor', 'faceAwayFromWall': True, 'faceAwayFromWindows': opt_in}
                entity = {'id': 2, 'modelId': 'Vendor', 'yaw': 0, 'position': [.5,.5,0]}
                resolve_layout([entity], [model], records, defaults, WorldTransforms(records, defaults))
                self.assertAlmostEqual(entity['renderYaw'], expected)
                self.assertEqual(entity['position'], [.5,.5,0])
                self.assertEqual(entity['yaw'], 0)

    def test_fractional_wall_fixture_corrects_depth_but_keeps_along_wall_position(self):
        records = {
            1: {'prototype': '', 'components': {'MapGrid': {}, 'Transform': {'parent': 0}}},
            2: {'prototype': 'Vent', 'components': {'Transform': {'parent': 1, 'pos': '97.86334,-46.53297'}}},
            3: {'prototype': 'Wall', 'components': {'Transform': {'parent': 1, 'pos': '97.5,-46.5'}}},
        }
        defaults = {'Wall': {'Transform': {'anchored': True}, 'IconSmooth': {'key': 'walls'}}}
        model = {'id': 'Vent', 'wallMounted': True, 'parts': [{'min': [-.2,-.6,1], 'max': [.2,-.501,1.5]}]}
        entity = {'id': 2, 'modelId': 'Vent', 'yaw': math.pi/2, 'position': [97.86334,-46.53297,0]}
        _, stats = resolve_layout([entity], [model], records, defaults, WorldTransforms(records, defaults))
        resolve_placements([entity], [model])
        self.assertAlmostEqual(entity['renderOffset'][0], -.36334)
        self.assertAlmostEqual(entity['renderOffset'][1], 0)
        self.assertEqual(entity['position'], [97.86334,-46.53297,0])
        self.assertEqual(stats['wallDepthCorrections'], 1)
        # Pivot + authored mount reaches the east wall face, with only its intended standoff.
        self.assertAlmostEqual(entity['position'][0]+entity['renderOffset'][0]+.501, 98.001)

    def test_mount_depth_correction_rotates_with_grid_and_preserves_tangent(self):
        offset = wall_mount_offset([6.72,22.86,0], [6.5,22.5], math.pi)
        self.assertAlmostEqual(offset[0], 0)
        self.assertAlmostEqual(offset[1], -.36)

    def test_physical_curtain_axis_survives_billboard_cardinal_snapping(self):
        records = {1: {'id': 1, 'prototype': 'Curtain', 'components': {}, 'componentTypes': []}}
        defaults = {'Curtain': {'Sprite': {'snapCardinals': True}}}
        instance = {'id': 1, 'modelId': 'CurtainModel', 'yaw': math.pi/2, 'position': [2, 3, 0]}
        model = {'id': 'CurtainModel', 'sourceDirections': 1, 'useEntityRotation': True}
        resolve_layout([instance], [model], records, defaults, WorldTransforms(records, defaults))
        self.assertAlmostEqual(instance['renderYaw'], math.pi/2)
        self.assertEqual(instance['position'], [2, 3, 0])

    def test_wall_fixtures_use_room_side_only_when_wall_is_adjacent(self):
        records={1:{'prototype':'','components':{'MapGrid':{},'Transform':{'parent':0}}},
                 2:{'prototype':'Lamp','components':{'Transform':{'parent':1,'pos':'.5,.5'}}},
                 3:{'prototype':'Wall','components':{'Transform':{'parent':1,'pos':'.5,1.5'}}},
                 4:{'prototype':'Lamp','components':{'Transform':{'parent':1,'pos':'.5,1.5'}}}}
        defaults={'Wall':{'Transform':{'anchored':True},'IconSmooth':{'key':'walls'}}}
        model={'id':'Lamp','wallMounted':True,'parts':[{'min':[-.4,-.68,2.2],'max':[.4,-.501,2.4]}]}
        entities=[{'id':2,'modelId':'Lamp','yaw':math.pi,'position':[.5,.5,0]},
                  {'id':4,'modelId':'Lamp','yaw':0,'position':[.5,1.5,0]}]
        variants,stats=resolve_layout(entities,[model],records,defaults,WorldTransforms(records,defaults))
        part=variants[entities[0]['geometryKey']][0]
        self.assertGreater(part['min'][1],-.5)
        self.assertAlmostEqual(part['max'][1],-.32)
        self.assertEqual(part['min'][2],2.2)
        self.assertNotIn('geometryKey',entities[1])
        self.assertEqual(stats['adjacentWallFixtures'],1)
        self.assertEqual(entities[0]['position'],[.5,.5,0])

    def test_single_frame_fixed_objects_and_directional_chairs_are_different(self):
        self.assertEqual(render_yaw(math.pi/2, 1, True), 0)
        self.assertAlmostEqual(render_yaw(math.pi/2, 4, True), math.pi/2)
        self.assertAlmostEqual(render_yaw(1.4, 4, True), math.pi/2)
        self.assertAlmostEqual(render_yaw(math.pi/2+.1, 1, False, True), .1)
        self.assertAlmostEqual(render_yaw(-math.pi/2, 4, False, False, 90), 0)

    def test_connection_geometry_follows_runs_and_clips_corners(self):
        parts=[{'min':[-.5,-.08,0],'max':[.5,.08,2],'color':'#AABBCC'}]
        vertical=connected_parts(parts,3)
        self.assertEqual(vertical[0]['min'],[-.08,-.5,0])
        self.assertEqual(vertical[0]['max'],[.08,.5,2])
        self.assertEqual(connected_parts(parts,12)[0]['min'],[-.5,-.08,0])
        corner=connected_parts(parts,5)
        self.assertEqual(len(corner),2)
        self.assertTrue(all(p['min'][0]>=-.08 and p['min'][1]>=-.08 for p in corner))
        # End pieces keep the complete frame, so an unsmoothed adjacent wall has no gap.
        self.assertEqual(connected_parts(parts,1),vertical)

    def test_neighbours_use_grid_keys_and_full_map_beyond_export_crop(self):
        def record(uid,prototype,parent,position,extra=None):
            return {'id':uid,'prototype':prototype,'componentTypes':['Transform'],
                    'components':{'Transform':{'parent':parent,'pos':position},**(extra or {})}}
        records={1:record(1,'',0,'10,20',{'MapGrid':{},'Transform':{'parent':0,'pos':'10,20','rot':90}}),
                 2:record(2,'Window',1,'.5,.5'),3:record(3,'Door',1,'.5,1.5'),
                 4:record(4,'Window',1,'.5,-.5'),5:record(5,'WrongKey',1,'1.5,.5'),
                 6:record(6,'Window',1,'-.5,.5',{'Transform':{'parent':1,'pos':'-.5,.5','anchored':False}})}
        defaults={p:{'Transform':{'anchored':True},'IconSmooth':{'key':key,'additionalKeys':['doors']}}
                  for p,key in [('Window','windows'),('Door','doors'),('WrongKey','fences')]}
        model={'id':'Glass','connectToNeighbours':True,'parts':[{'min':[-.5,-.08,0],'max':[.5,.08,2]}]}
        # Only the window is in the exported region. Its neighbours must still connect.
        instance={'id':2,'modelId':'Glass','yaw':math.pi/2,'position':[9.5,20.5,0]}
        variants,stats=resolve_layout([instance],[model],records,defaults,WorldTransforms(records,defaults))
        self.assertEqual(instance['connectionMask'],3)
        self.assertAlmostEqual(instance['renderYaw'],math.pi/2)
        self.assertEqual(variants['Glass:3'][0]['min'],[-.08,-.5,0])
        self.assertEqual(stats['connectedInstances'],1)

    def test_saved_sprite_override_changes_fixed_facing_without_moving_transform(self):
        records={1:{'id':1,'prototype':'Crate','components':{'Sprite':{'noRot':True}},'componentTypes':['Sprite']}}
        instance={'id':1,'modelId':'CrateModel','yaw':math.pi,'position':[2,3,0]}
        resolve_layout([instance],[{'id':'CrateModel'}],records,{},WorldTransforms(records))
        self.assertEqual(instance['renderYaw'],0)
        self.assertEqual(instance['yaw'],math.pi)
        self.assertEqual(instance['position'],[2,3,0])

    def test_isolated_panel_uses_only_unambiguous_wall_opening_axis(self):
        records={1:{'prototype':'','components':{'MapGrid':{},'Transform':{'parent':0}}},
                 2:{'prototype':'Panel','components':{'Transform':{'parent':1,'pos':'.5,.5'}}},
                 3:{'prototype':'Wall','components':{'Transform':{'parent':1,'pos':'.5,1.5'}}},
                 4:{'prototype':'Wall','components':{'Transform':{'parent':1,'pos':'.5,-.5'}}}}
        defaults={'Wall':{'Transform':{'anchored':True},'IconSmooth':{'key':'walls'}},
                  'Panel':{'Transform':{'anchored':True},'IconSmooth':{'key':'windows'}}}
        model={'id':'Glass','connectToNeighbours':True,'parts':[{'min':[-.5,-.08,0],'max':[.5,.08,2]}]}
        entity={'id':2,'modelId':'Glass','yaw':0,'position':[.5,.5,0]}
        resolve_layout([entity],[model],records,defaults,WorldTransforms(records,defaults))
        self.assertEqual(entity['connectionMask'],0)
        self.assertEqual(entity['layoutMask'],3)
        self.assertEqual(entity['layoutAlignment'],'adjacent wall opening')
