from copy import deepcopy
import math
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from placement import resolve_placements
from support_probe import probe, validate


def prop():
    parts = [dict(label='grip', min=[.15625, -.1875, 0], max=[.1875, -.0625, .078], color='#767672')]
    return dict(id='Hatchet', placement='surface', sourceDirections=1, sourceSpriteRotates=True,
                useEntityRotation=True, referenceState='icon', supportProbePart='grip', parts=parts,
                spriteStates={'icon': dict(frames=[dict(parts=deepcopy(parts))], delays=[1])})


def entity(uid, model, x, y, yaw=0):
    return dict(id=uid, modelId=model, position=[x, y, 0], yaw=yaw, matchKind='exact')


def table():
    return dict(id='Rack', supportSurface='top', parts=[dict(label='top', min=[-.4,-.275,.92], max=[.4,.275,.948])])


class SupportProbeTests(unittest.TestCase):
    def test_saved_hatchet_overhang_uses_real_bottom_without_moving_xy(self):
        tool = entity(3313, 'Hatchet', 316.6801, -87.19585)
        rack = entity(549, 'Rack', 316.5, -87.5)
        validate(prop())
        self.assertEqual(probe(prop()), (.171875, -.125))
        resolve_placements([rack, tool], [table(), prop()])
        self.assertEqual(tool['position'], [316.6801, -87.19585, 0])
        self.assertEqual(tool['renderOffset'], [0, 0, .95])
        self.assertEqual(tool['support'], dict(entity=549, height=.948, method='authored bottom contact', part='grip'))
        old = prop(); old.pop('supportProbePart')
        resolve_placements([rack, tool], [table(), old])
        self.assertNotIn('support', tool)

    def test_rotated_probe_and_existing_pivot_priority(self):
        rack = table(); rack['parts'][0].update(min=[.1,.15,.7], max=[.15,.19,.8])
        tool = entity(2, 'Hatchet', 0, 0, math.pi/2)
        resolve_placements([entity(1,'Rack',0,0), tool], [rack, prop()])
        self.assertEqual(tool['renderOffset'], [0,0,.802])
        pivot = dict(id='Pivot',supportSurface='top',parts=[dict(label='top',min=[-.03,-.03,.5],max=[.03,.03,.6])])
        resolve_placements([entity(1,'Rack',0,0), entity(3,'Pivot',0,0), tool], [rack,pivot,prop()])
        self.assertEqual(tool['renderOffset'], [0,0,.602])
        self.assertEqual(tool['support']['entity'], 3)
        self.assertEqual(tool['support']['method'], 'authored surface footprint')

    def test_missing_surface_self_support_and_wrong_floor_do_not_float(self):
        for target in (entity(1,'Hatchet',316.6801,-87.19585),entity(2,'Hatchet',316.6801,-87.19585)):
            rack = entity(1,'Rack',316.5,-87.5)
            if target['id'] == 2: target['position'][2] = 1
            resolve_placements([rack,target],[table(),prop()])
            self.assertNotIn('support',target)

    def test_invalid_probe_is_rejected_and_never_guessed(self):
        for defect in ('missing','duplicate','curve','yaw','pitch','transparent','textured','elevated',
                       'animated','multi-state','frame-mismatch','nonrotating','directional','bad-delay','connected','nan'):
            m=prop(); p=m['parts'][0]
            if defect=='missing': m['supportProbePart']='absent'
            if defect=='duplicate': m['parts'].append(deepcopy(p))
            if defect=='curve': p['shape']='Ellipsoid'
            if defect=='yaw': p['yaw']=90
            if defect=='pitch': p['pitch']=5
            if defect=='transparent': p['color']='#76767280'
            if defect=='textured': p['surface']='Texture'
            if defect=='elevated': m['parts'].append(dict(label='lower',min=[0,0,-.1],max=[.1,.1,0],color='#767672'))
            if defect=='nonrotating': m['sourceSpriteRotates']=False
            if defect=='directional': m['sourceDirections']=4
            if defect=='bad-delay': m['spriteStates']['icon']['delays']=[float('nan')]
            if defect=='connected': m['connectToNeighbours']=True
            if defect=='nan': p['max'][2]=float('nan')
            m['spriteStates']['icon']['frames'][0]['parts']=deepcopy(m['parts'])
            if defect=='animated':
                m['spriteStates']['icon']['frames']*=2; m['spriteStates']['icon']['delays']=[1,1]
            if defect=='multi-state': m['spriteStates']['other']=deepcopy(m['spriteStates']['icon'])
            if defect=='frame-mismatch': m['spriteStates']['icon']['frames'][0]['parts'][0]['max'][2]=.2
            with self.subTest(defect=defect):
                with self.assertRaises(ValueError): validate(m)
                self.assertIsNone(probe(m))


if __name__ == '__main__':
    unittest.main()
