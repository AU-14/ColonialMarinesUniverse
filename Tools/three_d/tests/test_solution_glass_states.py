from copy import deepcopy
from pathlib import Path
import sys
import unittest

import numpy as np

sys.path[:0] = [str(Path(__file__).resolve().parents[1])]
import author_solution_glasses as author
import build_models as bm
import inventory
import scene
import solution_glass_states as sg
from author_vendor_fans import occupied


class SolutionGlassStateTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        kinds, issues, _ = inventory.load_prototypes(author.ROOT)
        assert not issues
        sg.configure_source(kinds)
        resolver = inventory.Resolver(kinds['entity'])
        cls.models = {m['referencePrototype']:m for m in bm.load_models(author.MODEL)}
        cls.defaults = {uid:inventory.component_map(resolver.resolve(uid)) for uid in author.IDS}

    def test_written_defaults_follow_nested_source_capacity_and_metamorphic_layers(self):
        self.assertEqual(len(self.models), 16)
        for uid, model in self.models.items():
            with self.subTest(prototype=uid):
                self.assertEqual(sg.source_solution(uid)['maxVol'], 50)
                before = deepcopy(self.defaults[uid])
                pose, reason = sg.saved_pose(model, self.defaults[uid], {}, scene.normalize_tint)
                self.assertIsNone(reason)
                self.assertEqual(pose['layers'], model['solutionAppearance']['defaultLayers'])
                self.assertEqual(pose['volume'], 0 if uid == 'RMCDrinkGlass' else 30)
                self.assertEqual(pose['maxVolume'], 50)
                self.assertEqual(self.defaults[uid], before)
                self.assertEqual(sg.compose_parts(model), model['parts'])
        coffee = self.models['DrinkCoffee']['solutionAppearance']['defaultLayers']
        self.assertTrue(coffee[0]['rsi'].endswith('/coffeeglass.rsi'))
        self.assertEqual(coffee[1]['state'], 'fill-2')
        self.assertFalse(coffee[2]['visible'])
        grape = self.models['DrinkGrapeSodaGlass']['solutionAppearance']['defaultLayers']
        self.assertEqual(grape[1]['state'], 'fill-5')
        self.assertTrue(grape[2]['visible'])

    def test_source_level_thresholds_and_layer_tint_are_preserved(self):
        model = self.models['RMCDrinkGlass']; default = self.defaults['RMCDrinkGlass']
        for amount, level in ((0,0), (.01,1), (12.5,1), (12.51,2), (25,2), (25.01,3), (37.5,3), (49.99,4), (50,5)):
            pose, error = sg.saved_pose(model, default, {'Solution':{'solution':{'reagents':[{'ReagentId':'Water','Quantity':amount}]}}}, scene.normalize_tint)
            self.assertIsNone(error); self.assertEqual(pose['fillLevel'],level)
            self.assertEqual(pose['layers'][1]['visible'], bool(level))
        model = self.models['DrinkVodkaRedBool']
        default = model['solutionAppearance']['defaultLayers']
        self.assertEqual(default[1]['color'], '#C4C27655')
        parts = sg.compose_parts(model)
        self.assertTrue(any(bm.rgba(p['color'])[3] <= 85/255 for p in parts if p['label'].startswith('liquid volume')))

    def test_every_state_has_physical_volume_and_open_mouth_within_budget(self):
        count = 0
        for model in self.models.values():
            for name, state in sg.portable_states(model).items():
                count += 1; parts = state['frames'][0]['parts']; layers = state['layers']
                with self.subTest(model=model['id'],state=name):
                    self.assertLessEqual(len(parts),128)
                    self.assertTrue(any(p['label'].startswith('hollow wall') for p in parts))
                    self.assertTrue(any(p['label'].startswith('open rim') for p in parts))
                    rsi = Path(layers[0]['rsi']).stem
                    cx,bottom,rim,*_ = author.PROFILE[rsi]
                    mouth = np.array([[(cx-16)/32,0,(bottom-rim)/32+.004]])
                    self.assertFalse(any(occupied(p,mouth)[0] for p in parts))
                    expected_liquid = layers[1]['visible'] and rsi != 'iceglass'
                    self.assertEqual(any(p['label'].startswith('liquid volume') for p in parts), expected_liquid)
                    self.assertEqual(sg.composite(model,layers,bm.resource_file).size,(32,32))
        self.assertEqual(count,241)

    def test_portable_glb_exposes_static_studies_without_new_animation_clock(self):
        for uid in ('DrinkCoffee','DrinkGrapeSodaGlass','RMCDrinkGlass'):
            model = self.models[uid]
            doc, _ = sg.model_document(model,bm.glb_document)
            self.assertFalse(doc.get('animations'))
            self.assertEqual(len(doc['extras']['spriteStaticScenes']),len(sg.portable_states(model)))
            self.assertTrue(doc['extras']['solutionAppearance']['sourceOwned'])

    def test_unknown_saved_owners_mixtures_and_transforms_keep_fallback(self):
        model = self.models['DrinkCoffee']; default = self.defaults['DrinkCoffee']
        cases = [dict(Sprite=dict(offset='.1,0')),dict(Sprite=dict(noRot=True)),dict(Sprite=dict(color='#FFFFFF80')),
            dict(Appearance=dict(data={'unknown':1})),dict(GenericVisualizer={}),
            dict(Solution=dict(solution=dict(reagents=[{'ReagentId':'Coffee','Quantity':10},{'ReagentId':'Water','Quantity':10}]))),
            dict(Solution=dict(solution=dict(reagents=[{'ReagentId':'Sake','Quantity':30}]))),
            dict(Solution=dict(solution=dict(maxVol=float('nan'))))]
        for saved in cases:
            with self.subTest(saved=saved):
                pose,error = sg.saved_pose(model,default,saved,scene.normalize_tint)
                self.assertIsNone(pose); self.assertTrue(error)

    def test_pose_schema_unknown_layers_and_duplicate_catalog_are_rejected(self):
        model = self.models['DrinkCoffee']
        for defect in ('visible-type','extra-key','order','rsi','base-hidden','invalid-tint','duplicate','foam'):
            m = deepcopy(model); poses = m['solutionAppearance']['defaultLayers']
            if defect == 'visible-type': poses[1]['visible']='false'
            if defect == 'extra-key': poses[1]['frame']=1
            if defect == 'order': poses.reverse()
            if defect == 'rsi': poses[0]['rsi']='unknown.rsi'
            if defect == 'base-hidden': poses[0]['visible']=False
            if defect == 'invalid-tint': poses[1]['color']='#INVALID'
            if defect == 'duplicate': m['solutionAppearance']['layers'].append(deepcopy(m['solutionAppearance']['layers'][0]))
            if defect == 'foam': m['foamAppearance']={'baseParts':[]}
            with self.subTest(defect=defect), self.assertRaises(ValueError):
                sg.validate(m,bm.validate_model)

    def test_scene_composition_is_source_pose_specific_and_does_not_mutate_model(self):
        model = self.models['DrinkCoffee']; before = deepcopy(model)
        pose, _ = sg.saved_pose(model,self.defaults['DrinkCoffee'],{},scene.normalize_tint)
        entity = dict(modelId=model['id'],solutionPose=pose); variants = {}
        sg.scene_variants([entity],{model['id']:model},variants)
        self.assertEqual(variants[entity['geometryKey']],model['parts'])
        self.assertEqual(model,before)


if __name__ == '__main__':
    unittest.main()
