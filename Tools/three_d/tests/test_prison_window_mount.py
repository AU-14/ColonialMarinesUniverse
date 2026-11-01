import copy
import math
import unittest

from layout import (connected_parts, prison_recess_parts, prison_shutter_side, resolve_layout,
                    prison_wall_join_face, prison_joined_relief_parts)
from placement import resolve_placements
from scene import WorldTransforms


class PrisonWindowMountTests(unittest.TestCase):
    window_id = 'CMU3DPrisonCellObservationWindow'
    shutter_id = 'CMU3DHybrisaWindowShutter'
    window = [
        {'label': 'facade', 'min': [-.5,-.525,0], 'max': [.5,.525,.59], 'color': '#515355'},
        {'label': 'glazing', 'min': [-.38,-.045,.61], 'max': [-.15,.045,2.24], 'color': '#8EC6CA9A'},
        {'label': 'upper facade', 'min': [-.5,-.525,2.24], 'max': [.5,.525,2.78], 'color': '#56595D'}]
    shutter = [{'label': 'closed', 'min': [-.5,-.0625,0], 'max': [.5,.0625,2.75]}]

    def fixture(self, turn=0, grid=0, opposite=False):
        records = {
            1: {'prototype': '', 'components': {'MapGrid': {}, 'Transform': {'parent': 0, 'pos': '2,3', 'rot': f'{grid}rad'}}},
            3: {'prototype': 'RMCShutterHybrisaWindow', 'components': {'Transform': {'parent': 1, 'pos': '.5,.5', 'rot': turn*90}}},
            4: {'prototype': 'RMCWindowPrisonCell', 'components': {'Transform': {'parent': 1, 'pos': '.5,.5'}}},
            5: {'prototype': 'Wall', 'components': {'Transform': {'parent': 1, 'pos': '.5,1.5' if turn % 2 else '1.5,.5'}}}}
        defaults = {
            'RMCShutterHybrisaWindow': {'Transform': {'anchored': True}},
            'RMCShutterHybrisaWindowOpen': {'Transform': {'anchored': True}},
            'RMCWindowPrisonCell': {'Transform': {'anchored': True}, 'IconSmooth': {'key': 'windows', 'additionalKeys': ['walls'], 'base': ''}},
            'Wall': {'Transform': {'anchored': True}, 'IconSmooth': {'key': 'walls'}}}
        models = [
            {'id': self.window_id, 'sourcePrototypes': ['RMCWindowPrisonCell'], 'connectToNeighbours': True, 'parts': copy.deepcopy(self.window)},
            {'id': self.shutter_id, 'sourcePrototypes': ['RMCShutterHybrisaWindow'], 'useEntityRotation': True,
             'windowMountTargets': ['RMCWindowPrisonCell'], 'parts': copy.deepcopy(self.shutter)},
            {'id': self.shutter_id+'Open', 'sourcePrototypes': ['RMCShutterHybrisaWindowOpen'], 'useEntityRotation': True,
             'windowMountTargets': ['RMCWindowPrisonCell'], 'parts': [{'min': [-.5,-.0625,2.48], 'max': [.5,.0625,2.75]}]}]
        if opposite:
            records[6] = {'prototype': 'RMCShutterHybrisaWindowOpen', 'components': {'Transform': {'parent': 1, 'pos': '.5,.5', 'rot': turn*90+180}}}
        instances = []
        transforms = WorldTransforms(records, defaults)
        for uid in (3, 4, 6) if opposite else (3, 4):
            x, y, yaw, _ = transforms.resolve(uid)
            model = next(m for m in models if records[uid]['prototype'] in m['sourcePrototypes'])
            instances.append(dict(id=uid, prototype=records[uid]['prototype'], modelId=model['id'], matchKind='exact', position=[x,y,0], yaw=yaw))
        return records, defaults, models, instances

    def resolve(self, records, defaults, models, instances):
        variants, stats = resolve_layout(instances, models, records, defaults, WorldTransforms(records, defaults))
        resolve_placements(instances, models, variants)
        return variants, stats

    def test_both_axes_sides_and_rotated_grids_keep_source_and_gap(self):
        for turn in range(4):
            for grid in (0, .7):
                with self.subTest(turn=turn, grid=grid):
                    records, defaults, models, instances = self.fixture(turn, grid)
                    original = copy.deepcopy((records, models, instances))
                    variants, stats = self.resolve(records, defaults, models, instances)
                    shutter, window = instances
                    side = 1 if turn in (0, 3) else 2
                    self.assertEqual(window['prisonShutterSides'], side)
                    self.assertEqual(stats['prisonWindowAssemblies'], 1)
                    normal = (math.sin(shutter['yaw']), -math.cos(shutter['yaw']))
                    self.assertAlmostEqual(sum(shutter['renderOffset'][i]*normal[i] for i in range(2)), .3875, places=6)
                    self.assertAlmostEqual(.3875-.0625-.305, .02)
                    self.assertEqual((records, models), original[:2])
                    for e, old in zip(instances, original[2]):
                        self.assertEqual((e['position'],e['yaw']), (old['position'],old['yaw']))
                    axis = 0 if turn % 2 else 1
                    base = connected_parts(self.window, 1 if turn % 2 else 4)
                    candidate = variants[window['geometryKey']]
                    for p, q in zip(base, candidate):
                        self.assertEqual({k:v for k,v in p.items() if k not in ('min','max')}, {k:v for k,v in q.items() if k not in ('min','max')})
                        for edge in ('min','max'):
                            for i in range(3):
                                if i != axis: self.assertEqual(p[edge][i], q[edge][i])
                        if p['label'] == 'glazing': self.assertEqual(p, q)

    def test_source_and_export_traversal_order_or_cropping_cannot_change_the_assembly(self):
        records, defaults, models, instances = self.fixture(1, .7, True)
        original = copy.deepcopy(instances)
        first, _ = self.resolve(records, defaults, models, instances)
        reversed_instances = list(reversed(copy.deepcopy(original)))
        second, _ = self.resolve(dict(reversed(list(records.items()))), defaults, models, reversed_instances)
        self.assertEqual(first, second)
        self.assertEqual(sorted(instances,key=lambda e:e['id']), sorted(reversed_instances,key=lambda e:e['id']))
        for index in (0, 1):
            subset = [copy.deepcopy(original[index])]
            variants, _ = self.resolve(records, defaults, models, subset)
            self.assertEqual(subset[0], instances[index])
            if index == 1: self.assertEqual(variants[subset[0]['geometryKey']], first[instances[1]['geometryKey']])

    def test_opposite_shutters_and_removal_or_unanchoring_restore_only_the_missing_side(self):
        for action in ('remove', 'unanchor', 'move', 'perpendicular'):
            records, defaults, models, instances = self.fixture(opposite=True)
            self.resolve(records, defaults, models, instances)
            self.assertEqual(instances[1]['prisonShutterSides'], 3)
            for uid, expected in ((6, 1), (3, 0)):
                if action == 'remove':
                    records.pop(uid); instances[:] = [e for e in instances if e['id'] != uid]
                else:
                    change = {'anchored': False} if action == 'unanchor' else {'pos': '.51,.5'} if action == 'move' else {'rot': 90}
                    records[uid]['components']['Transform'].update(change)
                    x,y,yaw,_ = WorldTransforms(records,defaults).resolve(uid)
                    instance = next(e for e in instances if e['id']==uid)
                    instance.update(position=[x,y,0],yaw=yaw)
                variants, _ = self.resolve(records, defaults, models, instances)
                window = next(e for e in instances if e['id']==4)
                self.assertEqual(window.get('prisonShutterSides',0),expected)
                if not expected: self.assertEqual(variants[window['geometryKey']],connected_parts(self.window,4))

    def test_unsupported_context_never_guesses_a_recess_or_exterior_mount(self):
        for change in ('corner','cross','isolated','offpivot','perpendicular','window_unanchored','hidden','unknown_shutter'):
            with self.subTest(change=change):
                records, defaults, models, instances = self.fixture()
                if change in ('corner','cross'):
                    records[7]={'prototype':'Wall','components':{'Transform':{'parent':1,'pos':'.5,1.5'}}}
                if change == 'cross':
                    for uid,pos in ((8,'-.5,.5'),(9,'.5,-.5')):
                        records[uid]={'prototype':'Wall','components':{'Transform':{'parent':1,'pos':pos}}}
                if change == 'isolated': records.pop(5)
                if change == 'offpivot': records[3]['components']['Transform']['pos']='.51,.5'; instances[0]['position'][0]+=.01
                if change == 'perpendicular': records[3]['components']['Transform']['rot']=90; instances[0]['yaw']=math.pi/2
                if change == 'window_unanchored': records[4]['components']['Transform']['anchored']=False
                if change == 'hidden': records[3]['components']['Sprite']={'visible':False}
                if change == 'unknown_shutter': records[3]['prototype']='OtherShutter'
                self.resolve(records, defaults, models, instances)
                self.assertNotIn('prisonShutterSides',instances[1])
                self.assertNotIn('windowMount',instances[0])

    def test_unknown_geometry_and_invalid_side_are_rejected_without_mutation(self):
        original = copy.deepcopy(self.window)
        for axis, sides in ((2,1),(1,0),(1,4)):
            self.assertIsNone(prison_recess_parts(self.window,axis,sides))
        for field,value in (('shape','CylinderZ'),('yaw',90),('surface',1300)):
            changed=copy.deepcopy(self.window);changed[0][field]=value
            self.assertIsNone(prison_recess_parts(changed,1,1))
        self.assertEqual(self.window,original)
        self.assertEqual(prison_shutter_side('RMCShutterHybrisaWindow',self.shutter_id,None,0,0,[0,0]),0)

    def wall_fixture(self, grid=0, wall_turn=0):
        records, defaults, models, instances = self.fixture(grid=grid)
        records[5]['prototype'] = 'RMCWallPrisonReinforced'
        records[5]['components']['Transform']['rot'] = wall_turn*90
        defaults['RMCWallPrisonReinforced'] = {'Transform': {'anchored': True},
            'IconSmooth': {'key': 'walls', 'additionalKeys': ['windows','doors'], 'base': 'rwall'}}
        wall = [{'label':'full tile wall core','min':[-.5,-.5,0],'max':[.5,.5,2.8],'color':'#616B6C'}]
        for axis in (0,1):
            for sign in (-1,1):
                lo,hi=[-.3,-.3,.2],[.3,.3,2.5]
                lo[axis],hi[axis]=sorted((sign*.505,sign*.54))
                wall.append({'label':f'relief {axis} {sign}','min':lo,'max':hi,'color':'#9B9E95'})
        models.append({'id':'CMU3DReinforcedPrisonWall','sourcePrototypes':['RMCWallPrisonReinforced'],'parts':wall})
        x,y,yaw,_ = WorldTransforms(records,defaults).resolve(5)
        instances.append(dict(id=5,prototype='RMCWallPrisonReinforced',modelId='CMU3DReinforcedPrisonWall',matchKind='exact',position=[x,y,0],yaw=yaw))
        return records,defaults,models,instances

    def test_joined_relief_follows_relative_grid_rotation_and_keeps_the_core_and_other_faces(self):
        for grid in (0,.7):
            for turn in range(4):
                records,defaults,models,instances=self.wall_fixture(grid,turn)
                originals=copy.deepcopy((records,models,instances))
                variants,stats=self.resolve(records,defaults,models,instances)
                wall=instances[-1];flag=wall['prisonJoinFaces'];self.assertIn(flag,(1,2,4,8))
                self.assertEqual(stats['prisonWallJoins'],1)
                parts=variants[wall['geometryKey']];self.assertEqual(parts[0],models[-1]['parts'][0])
                changed=[i for i,(p,q) in enumerate(zip(parts,models[-1]['parts'])) if p!=q]
                self.assertEqual(len(changed),1)
                q=parts[changed[0]];axis=0 if flag in (1,2) else 1;sign=1 if flag in (2,8) else -1
                self.assertAlmostEqual(max(sign*q[k][axis] for k in ('min','max')),.49)
                self.assertEqual((records,models),originals[:2])
                self.assertEqual((wall['position'],wall['yaw']),(originals[2][-1]['position'],originals[2][-1]['yaw']))
                # The wall does not depend on whether its source window is exported/visited first.
                alone=[copy.deepcopy(originals[2][-1])]
                cropped,_=self.resolve(dict(reversed(list(records.items()))),defaults,models,alone)
                self.assertEqual(alone[0],wall);self.assertEqual(cropped[wall['geometryKey']],parts)

    def test_joined_relief_restores_on_missing_unanchored_or_unsupported_source_assembly(self):
        for kind in ('shutter_unanchor','window_unanchor','wall_unanchor','offpivot','perpendicular','unknown_wall','smoothing_disabled','source_key_removed','removed'):
            records,defaults,models,instances=self.wall_fixture()
            self.resolve(records,defaults,models,instances);self.assertIn('prisonJoinFaces',instances[-1])
            if kind.endswith('_unanchor'):
                uid={'shutter_unanchor':3,'window_unanchor':4,'wall_unanchor':5}[kind]
                records[uid]['components']['Transform']['anchored']=False
            elif kind=='offpivot': records[4]['components']['Transform']['pos']='.51,.5';instances[1]['position'][0]+=.01
            elif kind=='perpendicular': records[3]['components']['Transform']['rot']=90;instances[0]['yaw']=math.pi/2
            elif kind=='unknown_wall':records[5]['prototype']='UnknownWall'
            elif kind=='smoothing_disabled':records[5]['components']['IconSmooth']={'enabled':False}
            elif kind=='source_key_removed':records[5]['components']['IconSmooth']={'additionalKeys':[]}
            else:records.pop(3);instances[:]=[e for e in instances if e['id']!=3]
            self.resolve(records,defaults,models,instances)
            wall=next(e for e in instances if e['id']==5)
            self.assertNotIn('prisonJoinFaces',wall,kind);self.assertNotIn('geometryKey',wall,kind)

    def test_joined_relief_rejects_unknown_shapes_and_preserves_multiple_joint_core(self):
        _,_,models,_=self.wall_fixture();parts=models[-1]['parts'];original=copy.deepcopy(parts)
        for flag in (3,12,15):
            joined=prison_joined_relief_parts(parts,flag);self.assertEqual(joined[0],parts[0])
        self.assertEqual(parts,original)
        changed=copy.deepcopy(parts);changed[1]['surface']=1300
        self.assertIsNone(prison_joined_relief_parts(changed,1))
        for delta,yaw,mask in (([0,1],0,12),([1.01,0],0,12),([1,0],.2,12),([1,0],0,5)):
            self.assertEqual(prison_wall_join_face(delta,yaw,0,mask),0)


if __name__ == '__main__':
    unittest.main()
