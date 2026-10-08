from copy import deepcopy
import math
import unittest

from layout import back_wall_mount_offset, resolve_layout
from placement import resolve_placements
from scene import WorldTransforms


class SurfaceRearWallTests(unittest.TestCase):
    def setup_scene(self, min_y=-.8, max_y=.8, trim_bottom=1.1):
        models = [dict(id='Fan', sourcePrototypes=['Fan'], placement='surface', backWallMountTargets=['Wall'],
                       useEntityRotation=True, parts=[dict(min=[-.2,-.15,0], max=[.2,.15,.5])]),
                  dict(id='Table', sourcePrototypes=['Table'], supportSurface='top',
                       parts=[dict(label='top', min=[-.8,min_y,.76], max=[.8,max_y,.86])]),
                  dict(id='Wall', sourcePrototypes=['Wall'], parts=[dict(min=[-1,.3,0], max=[1,.6,2]),
                       dict(min=[-1,-.1,trim_bottom], max=[1,.3,1.3])])]
        records = {1: dict(prototype='', components={'MapGrid':{}, 'Transform':{'parent':0}})}
        instances = []
        for uid, prototype in enumerate(('Fan','Table','Wall'),2):
            records[uid] = dict(prototype=prototype, components={'Transform':{'parent':1,'pos':'0,0'}})
            instances.append(dict(id=uid, prototype=prototype, modelId=prototype, matchKind='exact', position=[0,0,0], yaw=0))
        defaults = {'Wall':{'Transform':{'anchored':True}}}
        return models, records, instances, defaults

    def test_layout_defers_clearance_until_the_actual_support_is_known(self):
        models, records, instances, defaults = self.setup_scene()
        source = deepcopy(instances)
        variants, _ = resolve_layout(instances, models, records, defaults, WorldTransforms(records, defaults))
        self.assertIn('_surfaceMountWalls', instances[0])
        stats = resolve_placements(instances, models, variants)
        fan = instances[0]
        self.assertEqual(fan['renderOffset'], [0,-.26,.862])
        self.assertEqual(fan['support']['entity'], 3)
        self.assertEqual(fan['backWallMount'], 4)
        self.assertEqual(stats['surfaceRearWallsPlaced'], 1)
        self.assertNotIn('_surfaceMountWalls', fan)
        for before, after in zip(source,instances):
            self.assertEqual(before['position'], after['position'])
            self.assertEqual(before['yaw'], after['yaw'])

    def test_height_aware_wall_clearance_rotates_through_all_four_facings(self):
        models, _, _, _ = self.setup_scene()
        for facing in range(4):
            yaw = facing * math.pi/2
            self.assertIsNone(back_wall_mount_offset(models[0]['parts'],yaw,models[2]['parts'],yaw,[0,0]))
            offset = back_wall_mount_offset(models[0]['parts'],yaw,models[2]['parts'],yaw,[0,0],height_offset=.862)
            self.assertAlmostEqual(offset[0], .26*math.sin(yaw))
            self.assertAlmostEqual(offset[1], -.26*math.cos(yaw))

    def test_displaced_pivot_uses_its_new_support_and_converges(self):
        models, records, instances, defaults = self.setup_scene(-.05,.05,.8)
        models.append(dict(id='Lower',supportSurface='top',parts=[dict(label='top',min=[-.8,-.4,.3],max=[.8,-.1,.4])]))
        instances.append(dict(id=5,modelId='Lower',matchKind='exact',position=[0,0,0],yaw=0))
        variants, _ = resolve_layout(instances[:3],models,records,defaults,WorldTransforms(records,defaults))
        resolve_placements(instances,models,variants)
        self.assertEqual(instances[0]['renderOffset'],[0,-.26,.402])
        self.assertEqual(instances[0]['support']['entity'],5)

    def test_support_cycle_keeps_source_fallback_without_temporary_mount_data(self):
        models, records, instances, defaults = self.setup_scene(-.05,.05)
        variants, _ = resolve_layout(instances,models,records,defaults,WorldTransforms(records,defaults))
        stats=resolve_placements(instances,models,variants)
        fan=instances[0]
        self.assertIsNone(fan['modelId'])
        self.assertEqual(fan['baseModelId'],'Fan')
        self.assertEqual(fan['position'],[0,0,0])
        self.assertEqual(stats['surfaceRearWallFallbacks'],1)
        self.assertNotIn('_surfaceMountWalls',fan)
        self.assertNotIn('renderOffset',fan)
