from copy import deepcopy
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path[:0] = [str(Path(__file__).resolve().parents[1])]
import scene
import sprite_states
from stack_states import saved_threshold_state
import stack_states


STATES = ['spacecash' + ('' if i == 1 else '_' + str(i)) for i in (1, 10, 20, 50, 100, 200, 500, 1000)]
CUTS = [10, 20, 50, 100, 200, 500, 1000]


def source():
    return dict(Stack=dict(stackType='Dollar', layerFunction='Threshold', count=1, baseLayer='base', layerStates=STATES),
                StackLayerThreshold=dict(thresholds=CUTS), Appearance={},
                Sprite=dict(sprite='cash.rsi', layers=[dict(state='spacecash', map=['base'])]))


class StackStateTests(unittest.TestCase):
    def test_each_threshold_before_at_after_and_large_count_selects_source_state(self):
        defaults = source()
        for count, index in [(1, 0), (9, 0), (10, 1), (19, 1), (20, 2), (49, 2), (50, 3),
                             (99, 3), (100, 4), (199, 4), (200, 5), (499, 5), (500, 6),
                             (999, 6), (1000, 7), (5000, 7), (2**31-1, 7)]:
            with self.subTest(count=count):
                self.assertEqual(saved_threshold_state(defaults, dict(Stack=dict(count=count)),
                    defaults['Sprite']['layers'][0], STATES), (STATES[index], None))
        self.assertEqual(defaults['Stack']['count'], 1)
        self.assertEqual(defaults['Sprite']['layers'][0]['state'], 'spacecash')

    def test_small_maximum_uses_engine_threshold_then_equal_level_rounding(self):
        d = source()
        for count, maximum, state in [(9, 2, 0), (10, 2, 4), (20, 2, 7), (10, 1, 7), (1, 1, 0)]:
            self.assertEqual(saved_threshold_state(d, dict(Stack=dict(count=count, maxCountOverride=maximum)),
                d['Sprite']['layers'][0], STATES), (STATES[state], None))

    def test_unknown_owners_maps_counts_and_incomplete_frames_keep_fallback(self):
        for defect in ('composite', 'function', 'stack-type', 'map', 'zero', 'boolean', 'negative', 'count-overflow',
                       'max', 'missing-frames', 'unordered-thresholds', 'duplicate-thresholds', 'extra-owner'):
            d, states = source(), list(STATES)
            if defect == 'composite': d['Stack']['composite'] = True
            if defect == 'function': d['Stack']['layerFunction'] = 'None'
            if defect == 'stack-type': d['Stack']['stackType'] = 'Steel'
            if defect == 'map': d['Sprite']['layers'][0]['map'] = ['different']
            if defect == 'zero': d['Stack']['count'] = 0
            if defect == 'boolean': d['Stack']['count'] = True
            if defect == 'negative': d['Stack']['count'] = -1
            if defect == 'count-overflow': d['Stack']['count'] = 2**31
            if defect == 'max': d['Stack']['maxCountOverride'] = 0
            if defect == 'missing-frames': states.pop()
            if defect == 'unordered-thresholds': d['StackLayerThreshold']['thresholds'] = list(reversed(CUTS))
            if defect == 'duplicate-thresholds': d['StackLayerThreshold']['thresholds'] = [10] * 7
            if defect == 'extra-owner': d['GenericVisualizer'] = {}
            with self.subTest(defect=defect):
                state, reason = saved_threshold_state(d, {}, d['Sprite']['layers'][0], states)
                self.assertIsNone(state)
                self.assertTrue(reason)

    def test_saved_sprite_pose_uses_count_not_inherited_base_state_and_rejects_appearance_overrides(self):
        model = dict(referenceRsi='cash.rsi', sourceSpriteOffset=[0, 0], sourceSpriteRotates=True,
                     spriteStates={state: {} for state in STATES})
        d = source()
        self.assertEqual(sprite_states.saved_pose(model, d, dict(Stack=dict(count=100)), scene.normalize_tint), ('spacecash_100', None))
        d['Stack']['count'] = 20
        self.assertEqual(sprite_states.saved_pose(model, d, {}, scene.normalize_tint), ('spacecash_20', None))
        state, reason = sprite_states.saved_pose(model, d, dict(Appearance=dict(data={'unknown': True})), scene.normalize_tint)
        self.assertIsNone(state)
        self.assertIn('live sprite owner', reason)

    def test_streaming_map_reader_keeps_saved_stack_contract_fields(self):
        data = '''meta:
  format: 7
entities:
- proto: RMCSpaceCash1000
  entities:
  - uid: 5
    components:
    - type: Transform
      parent: 1
    - type: Stack
      count: 5000
    - type: StackLayerThreshold
      thresholds: [10, 20]
    - type: GenericVisualizer
      visuals: {}
'''
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'map.yml'
            path.write_text(data)
            _, records = scene.read_map(path)
            self.assertEqual(records[5]['components']['Stack']['count'], 5000)
            self.assertEqual(records[5]['components']['StackLayerThreshold']['thresholds'], [10, 20])
            self.assertIn('GenericVisualizer', records[5]['components'])

    def material(self, stack_type='CMSteel'):
        prefix = 'metal' if stack_type == 'CMSteel' else 'plastic'
        levels = [prefix] + [prefix + '_' + str(i) for i in (2, 3, 4)]
        return dict(Stack=dict(stackType=stack_type, count=50, baseLayer='base', layerStates=levels),
                    Appearance={}, Sprite=dict(sprite=prefix+'.rsi', layers=[dict(state=levels[-1], map=['base'])]))

    def test_material_default_maximum_and_every_equal_level_boundary(self):
        for kind in ('CMSteel', 'RMCPlastic'):
            d = self.material(kind)
            self.assertEqual(stack_states.material_limit(kind), 50)
            for count, index in ((1,0),(10,0),(12,0),(13,1),(20,1),(24,1),(25,2),(30,2),(37,2),(38,3),(49,3),(50,3),(500,3),(2**31-1,3)):
                with self.subTest(kind=kind,count=count):
                    self.assertEqual(saved_threshold_state(d, dict(Stack=dict(count=count)), d['Sprite']['layers'][0], d['Stack']['layerStates']),
                                     (d['Stack']['layerStates'][index], None))

    def test_material_explicit_none_default_count_and_saved_maximum_override(self):
        d=self.material();d['Stack'].pop('count');d['Stack']['layerFunction']='None';levels=d['Stack']['layerStates']
        self.assertEqual(saved_threshold_state(d,{},d['Sprite']['layers'][0],levels),(levels[2],None))  # default30/50
        for count,maximum,index in ((1,1,3),(1,3,1),(2,3,2),(3,3,3),(24,100,0),(25,100,1),(50,100,2),(75,100,3),(2**31-2,2**31-1,3)):
            with self.subTest(count=count,maximum=maximum):
                self.assertEqual(saved_threshold_state(d,dict(Stack=dict(count=count,maxCountOverride=maximum)),d['Sprite']['layers'][0],levels),(levels[index],None))
        self.assertNotIn('count',d['Stack'])

    def test_material_unknown_owners_invalid_counts_and_changed_compositions_reject(self):
        for defect in ('zero','negative','boolean','fractional','overflow','maximum-zero','maximum-overflow','maximum-bool','maximum-fractional',
                       'other-owner','threshold','inert-threshold','composite','state-order','missing-state','wrong-map','no-appearance','item-counter','visualizer','random'):
            d=self.material();states=list(d['Stack']['layerStates'])
            if defect in ('zero','negative','boolean','fractional','overflow'):d['Stack']['count']={'zero':0,'negative':-1,'boolean':True,'fractional':2.5,'overflow':2**31}[defect]
            if defect.startswith('maximum-'):d['Stack']['maxCountOverride']={'maximum-zero':0,'maximum-overflow':2**31,'maximum-bool':True,'maximum-fractional':3.5}[defect]
            if defect=='other-owner':d['Stack']['stackType']='Steel'
            if defect=='threshold':d['Stack']['layerFunction']='Threshold'
            if defect=='inert-threshold':d['StackLayerThreshold']={}
            if defect=='composite':d['Stack']['composite']=True
            if defect=='state-order':d['Stack']['layerStates']=list(reversed(states))
            if defect=='missing-state':states.pop()
            if defect=='wrong-map':d['Sprite']['layers'][0]['map']=['other']
            if defect=='no-appearance':d.pop('Appearance')
            if defect=='item-counter':d['ItemCounter']={}
            if defect=='visualizer':d['GenericVisualizer']={}
            if defect=='random':d['RandomSprite']={}
            with self.subTest(defect=defect):
                result,reason=saved_threshold_state(d,{},d['Sprite']['layers'][0],states)
                self.assertIsNone(result);self.assertTrue(reason)

    def test_material_source_limit_refuses_changed_parent_missing_or_invalid_maximum(self):
        for record in ({'id':'CMSteel','type':'stack','parent':'Unverified','maxCount':50},
                       {'id':'CMSteel','type':'stack'}, {'id':'CMSteel','type':'stack','maxCount':False},
                       {'id':'CMSteel','type':'stack','maxCount':0}):
            stack_states.material_limit.cache_clear()
            with patch.object(stack_states.inventory,'load_yaml',return_value=[record]):
                with self.assertRaises(ValueError):stack_states.material_limit('CMSteel')
        stack_states.material_limit.cache_clear()
        with self.assertRaises(ValueError):stack_states.material_limit('Steel')

    def test_material_saved_pose_uses_count_over_misleading_sprite_state_and_appearance_falls_back(self):
        d=self.material();d['Stack']['count']=10;d['Sprite']['state']='metal_2'
        model=dict(referenceRsi='metal.rsi',sourceSpriteOffset=[0,0],sourceSpriteRotates=True,spriteStates={s:{} for s in d['Stack']['layerStates']})
        self.assertEqual(sprite_states.saved_pose(model,d,{},scene.normalize_tint),('metal',None))
        self.assertEqual(sprite_states.saved_pose(model,d,dict(Stack=dict(count=20)),scene.normalize_tint),('metal_2',None))
        result,reason=sprite_states.saved_pose(model,d,dict(Appearance=dict(data={'unknown':True})),scene.normalize_tint)
        self.assertIsNone(result);self.assertIn('live sprite owner',reason)


if __name__ == '__main__':
    unittest.main()
