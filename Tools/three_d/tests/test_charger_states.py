from copy import deepcopy
from pathlib import Path
import sys
import unittest

sys.path[:0]=[str(Path(__file__).resolve().parents[1])]
import build_models as bm
import charger_states as cs
import scene

def part(label):
    return dict(label=label,min=[0,0,0],max=[.1,.1,.1],color='#FFFFFF')

def model():
    definition=dict(baseParts=[part('body')],lightStates={},insertedStates={s:dict(parts=[part(s)]) for s in cs.INSERTS})
    for level,state in enumerate(cs.LIGHTS):
        definition['lightStates'][state]=dict(frames=[dict(parts=[part(f'{state}-{f}')]) for f in range(2 if level==5 else 1)],delays=[.1,.1] if level==5 else [1])
    result=dict(id='Test',label='Test',status='draft',sourcePrototypes=['RMCRecharger'],referenceRsi=cs.RSI,
        referenceState=cs.BASE,sourceDirections=1,placement='surface',chargerAppearance=definition)
    result['parts']=cs.compose_parts(result)
    return cs.validate(bm.validate_model(result),bm.validate_model)

def source():
    return dict(Sprite=dict(sprite=cs.RSI,snapCardinals=True,layers=[dict(state=cs.BASE,map=[cs.BASE_MAP]),
        dict(state='recharger-0',map=[cs.LIGHT_MAP],shader='unshaded')]),Appearance={},
        Charger=dict(slotId='charger_slot',chargeLevelSteps=6),
        PowerChargerVisuals=dict(emptyState=cs.BASE,occupiedState=cs.BASE,chargeLevelState='recharger-{0}'),
        ItemMapper=dict(sprite=cs.RSI,mapLayers={state:dict(whitelist=dict(tags=[tag])) for state,tag in zip(cs.INSERTS,('Taser','Stunbaton'))}),
        ItemSlots=dict(slots={'charger_slot':{}}),
        ContainerContainer=dict(containers={'charger_slot':'','machine_board':'','machine_parts':''}))

class ChargerStateTests(unittest.TestCase):
    def test_source_composition_independence_static_hidden_and_blink_timeline(self):
        m=model();states=cs.portable_states(m)
        self.assertEqual(len(states),28)
        self.assertEqual(sum(len(s['frames']) for s in states.values()),32)
        self.assertEqual(sum(len(s['frames'])>1 for s in states.values()),4)
        for light in (*cs.LIGHTS,'hidden'):
            for mask in range(4):
                inserted=[s for i,s in enumerate(cs.INSERTS) if mask&(1<<i)]
                for frame in range(2 if light=='recharger-5' else 1):
                    parts=cs.compose_parts(m,light,frame,inserted)
                    expected=['body']+([] if light=='hidden' else [f'{light}-{frame}'])+inserted
                    self.assertEqual([p['label'] for p in parts],expected)
        self.assertEqual(cs.validate_source(m,bm.resource_file)['recharger-5'][0].size,(32,32))

    def test_glb_uses_four_source_loops_and_static_pose_scenes_without_extra_clock(self):
        doc,_=cs.model_document(model(),bm.glb_document)
        self.assertEqual(len(doc['animations']),4)
        self.assertEqual(len(doc['extras']['spriteStaticScenes']),24)
        self.assertTrue(all(a['extras']['loop'] for a in doc['animations']))
        self.assertEqual(doc['extras']['spriteStatePeriods']['recharger-5--empty'],.2)
        self.assertFalse(doc['extras']['chargerAppearance']['unshadedRenderingSupported'])

    def test_saved_empty_initializes_level_zero_and_preserves_source(self):
        d=source();before=deepcopy(d)
        pose,error=cs.saved_pose(model(),d,{},scene.normalize_tint)
        self.assertIsNone(error);self.assertEqual(pose['light'],'recharger-0');self.assertEqual(pose['inserted'],[])
        self.assertEqual(d,before)

    def test_resolved_direct_battery_defaults_cover_every_charge_level_and_mapper_tags(self):
        d=source();d['ContainerContainer']['containers']['charger_slot']={'ent':10}
        records={10:dict(prototype='Contained',components={})}
        for charge,expected in ((0,0),(.01,1),(25,1),(25.01,2),(50,2),(50.01,3),(75,3),(75.01,4),(99.9,4),(100,5),(120,5)):
            defaults={'Contained':dict(Battery=dict(maxCharge=100,startingCharge=charge),Tag=dict(tags=['Taser','Stunbaton']))}
            with self.subTest(charge=charge):
                pose,error=cs.saved_pose(model(),d,{},scene.normalize_tint,records,defaults)
                self.assertIsNone(error);self.assertEqual(pose['light'],f'recharger-{expected}')
                self.assertEqual(pose['inserted'],list(cs.INSERTS))

    def test_slotted_battery_and_mapper_all_containers_follow_source_owners(self):
        d=source();d['ContainerContainer']['containers']['charger_slot']={'ent':10};d['ContainerContainer']['containers']['machine_parts']={'ents':[12]}
        records={10:dict(prototype='Taser',components={}),11:dict(prototype='Cell',components={}),12:dict(prototype='Baton',components={})}
        defaults={'Taser':dict(Tag=dict(tags=['Taser']),PowerCellSlot=dict(cellSlotId='cell'),ContainerContainer=dict(containers={'cell':{'ent':11}})),
            'Cell':dict(Battery=dict(maxCharge=100,startingCharge=51)), 'Baton':dict(Tag=dict(tags=['Stunbaton']))}
        pose,error=cs.saved_pose(model(),d,{},scene.normalize_tint,records,defaults)
        self.assertIsNone(error);self.assertEqual(pose['light'],'recharger-3');self.assertEqual(pose['inserted'],list(cs.INSERTS))
        defaults['Cell']['Battery']['maxCharge']=0
        pose,error=cs.saved_pose(model(),d,{},scene.normalize_tint,records,defaults)
        self.assertIsNone(error);self.assertEqual(pose['light'],'recharger-0')

    def test_unresolved_nonempty_default_or_starting_item_keeps_fallback(self):
        for defect in ('nonempty','starting','unresolved-slot'):
            d=source();records=None;defaults=None
            if defect=='nonempty':d['ContainerContainer']['containers']['charger_slot']={'ent':10}
            if defect=='starting':d['ItemSlots']['slots']['charger_slot']['startingItem']='Taser'
            if defect=='unresolved-slot':
                d['ContainerContainer']['containers']['charger_slot']={'ent':10}
                records={10:dict(prototype='Taser',components={})}
                defaults={'Taser':dict(PowerCellSlot=dict(cellSlotId='cell'),ItemSlots=dict(slots={'cell':{'startingItem':'UnknownCell'}}))}
            with self.subTest(defect=defect):
                pose,error=cs.saved_pose(model(),d,{},scene.normalize_tint,records,defaults)
                self.assertIsNone(pose);self.assertTrue(error)

    def test_unknown_visual_owner_layer_data_and_transform_fail_conservatively(self):
        for defect in ('mapper','mapper-order','mapper-owner','level-count','visual-owner','appearance','layer-color','shader','extra-layer',
                       'offset','no-rotation','snap','overall-alpha','missing-owner','unknown-container','fractional-id'):
            d=source()
            if defect=='mapper':d['ItemMapper']['mapLayers']['unknown']={}
            if defect=='mapper-order':d['ItemMapper']['mapLayers']=dict(reversed(list(d['ItemMapper']['mapLayers'].items())))
            if defect=='mapper-owner':d['ItemMapper']['containerWhitelist']=['charger_slot']
            if defect=='level-count':d['Charger']['chargeLevelSteps']=7
            if defect=='visual-owner':d['PowerChargerVisuals']['occupiedState']='full'
            if defect=='appearance':d['Appearance']['data']={'unknown':1}
            if defect=='layer-color':d['Sprite']['layers'][1]['color']='#FF0000'
            if defect=='shader':d['Sprite']['layers'][1]['shader']='custom'
            if defect=='extra-layer':d['Sprite']['layers'].append(dict(state='unknown'))
            if defect=='offset':d['Sprite']['offset']=[.1,0]
            if defect=='no-rotation':d['Sprite']['noRot']=True
            if defect=='snap':d['Sprite']['snapCardinals']=False
            if defect=='overall-alpha':d['Sprite']['color']='#FFFFFF80'
            if defect=='missing-owner':d.pop('Charger')
            if defect=='unknown-container':d['ContainerContainer']['containers']['charger_slot']='unknown'
            if defect=='fractional-id':d['ContainerContainer']['containers']['charger_slot']={'ent':1.5}
            with self.subTest(defect=defect):
                pose,error=cs.saved_pose(model(),d,{},scene.normalize_tint)
                self.assertIsNone(pose);self.assertTrue(error)

    def test_schema_and_composed_part_budgets_reject_bad_states_atomically(self):
        for defect in ('default','delay','missing','rotation','budget'):
            m=model()
            if defect=='default':m['parts']=m['parts'][:1]
            if defect=='delay':m['chargerAppearance']['lightStates']['recharger-5']['delays']=[.2,.2]
            if defect=='missing':m['chargerAppearance']['insertedStates'].pop(cs.INSERTS[0])
            if defect=='rotation':m['useEntityRotation']=True
            if defect=='budget':
                m['chargerAppearance']['baseParts']=[part(str(n)) for n in range(128)]
                m['parts']=m['chargerAppearance']['baseParts']
            with self.subTest(defect=defect):
                with self.assertRaises(ValueError):cs.validate(m,bm.validate_model)

if __name__=='__main__':unittest.main()
