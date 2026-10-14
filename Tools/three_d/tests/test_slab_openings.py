from copy import deepcopy
import json
import math
from pathlib import Path
import struct
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import slab_openings as so
from export_scene import export_region
from scene import WorldTransforms, normalize_tint
from sprite_states import saved_pose


def fixture(yaw=0):
    bounds = {'min':[-.28125,-.4375],'max':[.28125,-.03125]}
    part = {'min':[-.28,-.09,-.8],'max':[-.18,-.03,1],'color':'#AAAAAA','label':'left rail'}
    model = {'id':'Ladder','label':'Ladder','status':'draft','sourcePrototypes':['Ladder'],
             'referenceRsi':'ladder.rsi','referenceState':'down','sourceSpriteOffset':[0,0],
             'parts':[part],'spriteStates':{'down':{'frames':[{'parts':[part]}],'delays':[1]}},'floorOpening':bounds}
    records = {1:{'prototype':'','components':{'Map':{},'Transform':{}}},
               2:{'prototype':'','components':{'MapGrid':{},'Transform':{'parent':1,'pos':'10,20','rot':str(yaw)+'rad'}}},
               3:{'prototype':'Ladder','components':{'Transform':{'parent':2,'pos':'.5,.5','anchored':True}}}}
    transforms = WorldTransforms(records,{})
    x,y,angle,_ = transforms.resolve(3)
    entity = {'id':3,'prototype':'Ladder','position':[x,y,0],'yaw':angle,'renderYaw':angle,
              'modelId':'Ladder','matchKind':'exact','spriteState':'down','spriteFrame':0,'geometryKey':'posed'}
    tile = {'x':10,'y':20,'z':0,'grid':2,'yaw':yaw,'palette':1}
    return model,records,transforms,entity,tile


class SlabOpeningTests(unittest.TestCase):
    def test_rotated_grid_and_each_cardinal_keep_area_and_bounds(self):
        bounds = fixture()[0]['floorOpening']
        for grid in (0,.37,2.19):
            for quarter in range(4):
                cut = so.transform(bounds,(0,0),grid+quarter*math.pi/2,grid)
                fragments = so.subtract((-.5,-.5,.5,.5),[cut,cut])
                self.assertAlmostEqual(sum((r[2]-r[0])*(r[3]-r[1]) for r in fragments),1-.5625*.40625)
                for x,y in ((-.49,-.49),(.49,-.49),(-.49,.49),(.49,.49)):
                    self.assertEqual(sum(a<=x<=c and b<=y<=d for a,b,c,d in fragments),1)
                for i,a in enumerate(fragments):
                    for b in fragments[i+1:]:
                        self.assertFalse(max(a[0],b[0])<min(a[2],b[2]) and max(a[1],b[1])<min(a[3],b[3]))

    def test_invalid_and_out_of_tile_requests_do_not_return_partial_geometry(self):
        bounds = fixture()[0]['floorOpening']
        for args in ((bounds,(0,0),.3,0),(bounds,(.5,0),0,0),(bounds,(0,0),float('nan'),0)):
            with self.assertRaises(ValueError): so.transform(*args)
        with self.assertRaises(ValueError): so.subtract((-.5,-.5,.5,.5),[(0,0,.1,.1)]*17)
        for invalid in ({'min':[0,0],'max':[0,.1]},{'min':[-.6,0],'max':[.1,.1]}, {'min':[0,0]}):
            with self.assertRaises(ValueError): so.rect(invalid)

    def test_source_layer_changes_and_transparency_preserve_sprite_fallback(self):
        model,*_ = fixture()
        sprite = {'sprite':'ladder.rsi','state':'down'}
        self.assertEqual(saved_pose(model,{'Sprite':sprite},{},normalize_tint),('down',None))
        for change in ({'noRot':True},{'color':'#FFFFFF80'},{'rotation':10},{'visible':False},
                       {'snapCardinals':True},{'postShader':'test'}, {'state':'unknown'},
                       {'layers':[{'state':'down','copyToShaderParameters':{'x':1}}]}):
            pose,reason = saved_pose(model,{'Sprite':{**sprite,**change}},{},normalize_tint)
            self.assertIsNone(pose,change)
            self.assertTrue(reason)

    def test_scene_uses_grid_axes_retains_source_and_all_other_geometry(self):
        for yaw in (0,.37,math.pi/2):
            model,records,transforms,entity,tile = fixture(yaw)
            original = deepcopy(entity)
            neighbor = {'id':4,'prototype':'grating','modelId':None,'position':entity['position'][:],'yaw':0}
            old_neighbor = deepcopy(neighbor)
            result = so.apply_scene([entity,neighbor],[tile],{'Ladder':model},records,{},transforms)
            self.assertFalse(result['rejected'])
            self.assertEqual(len(result['applied']),1)
            self.assertEqual(entity,original)
            self.assertEqual(neighbor,old_neighbor)
            self.assertEqual([[round(v,8) for v in r] for r in tile['floorFragments']],[[0,0,.21875,1],[.78125,0,1,1],
                                                      [.21875,0,.78125,.0625],[.21875,.46875,.78125,1]])

    def test_unsupported_source_transform_does_not_cut_any_tile(self):
        model,records,transforms,entity,tile = fixture()
        entity['renderYaw'] = .123
        result = so.apply_scene([entity],[tile],{'Ladder':model},records,{},transforms)
        self.assertEqual(len(result['rejected']),1)
        self.assertNotIn('floorFragments',tile)
        self.assertIsNone(entity['modelId'])

    def test_grating_is_clipped_with_floor_without_changing_source_pivots(self):
        for unsupported in (False,True):
            model,records,transforms,entity,tile = fixture(.37)
            records[4]=deepcopy(records[3])
            records[4]['prototype']='CMCatwalk'
            grate={'id':'Grate','parts':[{'min':[-.5,-.5,-.01],'max':[.5,.5,.035], 'label':'underplate','color':'#444444'}]}
            if unsupported: grate['parts'][0]['surface']='UnexpectedTexture'
            target={'id':4,'prototype':'CMCatwalk','position':entity['position'][:],'yaw':entity['yaw'],
                    'renderYaw':entity['yaw'],'modelId':'Grate','matchKind':'exact'}
            original=deepcopy(target)
            variants={}
            result=so.apply_scene([entity,target],[tile],{'Ladder':model,'Grate':grate},records,{},transforms,variants)
            if unsupported:
                self.assertTrue(result['rejected'])
                self.assertNotIn('floorFragments',tile)
                self.assertEqual(target,original)
                self.assertIsNone(entity['modelId'])
            else:
                self.assertFalse(result['rejected'])
                self.assertEqual(target['position'],original['position'])
                self.assertEqual(target['yaw'],original['yaw'])
                self.assertEqual(target['slabCladdingSources'],[3])
                self.assertEqual(len(variants[target['geometryKey']]),4)
                self.assertEqual(result['cladding'],[{'id':4,'sources':[3],'parts':4}])

    def test_export_has_a_real_hole_and_preserves_source_picking_metadata(self):
        model,records,transforms,entity,tile = fixture()
        so.apply_scene([entity],[tile],{'Ladder':model},records,{},transforms)
        scene = {'map':{'path':'fixture'},'instances':[entity],'tiles':[tile], 'tilePalette':{},
                 'geometryVariants':{'posed':model['parts']}}
        data,report = export_region(scene,[model],entity['position'],1)
        length = struct.unpack_from('<I',data,12)[0]
        doc = json.loads(data[20:20+length])
        self.assertEqual(report['floorParts'],4)
        self.assertEqual(report['solidParts'],5)
        source = next(n for n in doc['nodes'] if n.get('extras',{}).get('savedUid')==3)
        self.assertEqual(source['extras']['sourcePosition'],entity['position'])
        floor = next(n for n in doc['nodes'] if n.get('extras',{}).get('openingSources'))
        hole_x,hole_y=.5,.25
        for index in floor['children']:
            node=doc['nodes'][index]
            x,_,negative_y = node['translation']
            width,_,depth = node['scale']
            self.assertFalse(abs(x-hole_x)<width/2 and abs(-negative_y-hole_y)<depth/2)

    def test_recessed_service_channel_preserves_grates_above_floor_cut(self):
        model,records,transforms,entity,tile = fixture()
        model['preserveSlabCladding'] = True
        records[4] = deepcopy(records[3])
        records[4]['prototype'] = 'CMCatwalk'
        grate = {'id':'Grate','parts':[{'min':[-.5,-.5,0],'max':[.5,.5,.035],
            'label':'existing grating','color':'#444444','surface':'ExistingGrateArt'}]}
        target = dict(id=4,prototype='CMCatwalk',position=entity['position'][:],yaw=0,
            renderYaw=0,renderOffset=[0,0,.1],modelId='Grate',matchKind='exact')
        original = deepcopy(target)
        variants = {}
        result = so.apply_scene([entity,target],[tile],{'Ladder':model,'Grate':grate},records,{},transforms,variants)
        self.assertFalse(result['rejected'])
        self.assertEqual(len(result['applied']),1)
        self.assertIn('floorFragments',tile)
        self.assertEqual(target,original)
        self.assertEqual(variants,{})
        self.assertEqual(result['cladding'],[])

    def test_fully_cut_floor_keeps_metadata_without_invalid_empty_children(self):
        model,records,transforms,entity,tile = fixture()
        model['floorOpening'] = {'min':[-.5,-.5],'max':[.5,.5]}
        so.apply_scene([entity],[tile],{'Ladder':model},records,{},transforms)
        data,report = export_region({'map':{'path':'test'},'instances':[entity],'tiles':[tile],
            'tilePalette':{},'geometryVariants':{'posed':model['parts']}},[model],entity['position'],1)
        doc = json.loads(data[20:20+struct.unpack_from('<I',data,12)[0]])
        self.assertEqual(report['floorParts'],0)
        root = next(n for n in doc['nodes'] if n.get('extras',{}).get('openingSources'))
        self.assertNotIn('children',root)
        self.assertEqual(root['extras']['floorFragments'],[])

    def test_colocated_grates_with_different_facing_do_not_overwrite_each_other(self):
        model,records,transforms,entity,tile=fixture()
        grate={'id':'Grate','parts':[{'min':[-.5,-.25,-.01],'max':[.5,.25,.035],'label':'bar','color':'#444444'}]}
        targets=[]
        for uid,yaw in ((4,0),(5,math.pi/2)):
            records[uid]=deepcopy(records[3])
            records[uid]['prototype']='CMCatwalk'
            targets.append({'id':uid,'prototype':'CMCatwalk','position':entity['position'][:],'yaw':yaw,
                            'renderYaw':yaw,'modelId':'Grate','matchKind':'exact'})
        variants={}
        result=so.apply_scene([entity,*targets],[tile],{'Ladder':model,'Grate':grate},records,{},transforms,variants)
        self.assertFalse(result['rejected'])
        self.assertNotEqual(targets[0]['geometryKey'],targets[1]['geometryKey'])
        self.assertNotEqual(variants[targets[0]['geometryKey']],variants[targets[1]['geometryKey']])


if __name__=='__main__': unittest.main()
