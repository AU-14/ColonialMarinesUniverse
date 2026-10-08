"""Author source-shaped reagent carts; stage first, then apply dedicated reviewed assets."""
import argparse
from collections import Counter
from copy import deepcopy
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import re
import shutil
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont
import yaml

ROOT = next(p for p in Path(__file__).resolve().parents if (p/'Tools/three_d/build_models.py').is_file())
sys.path.insert(0,str(ROOT/'Tools/three_d'))
import build_models
import inventory
import scene
import surfaces
from author_wide_machinery import world_parts, contacts

STAGE = ROOT/'.codex/reagent-tanks-staged'
BASELINE = ROOT/'.codex/captured-scene-baseline867'
SOURCE = ROOT/'Resources/Textures/_RMC14/Structures/Storage/reagent_tank.rsi'
MODELS = Path('Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_reagent_tanks.yml')
ART = MODELS.with_name('garrison_reagent_tanks_art.yml')
UTILITIES = MODELS.with_name('garrison_utilities.yml')
TEXTURES = Path('Content.CMU/Resources/Textures/CMU14/ThreeD/Surfaces')
NOTES = Path('Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_REAGENT_TANKS.md')
VARIANTS = [('Empty',None,None),('Dermaline','CMDermaline','#E2972E'),('Meralyne','CMMeralyne','#B40000'),
            ('Phoron','RMCPhoron','#E71B00'),('SulphuricAcid','RMCSulphuricAcid','#DB5008'),('Water','Water','#0064C8')]
VESSEL = ['upper vessel','lower vessel']
TANK_IDS = {'RMCTankReagent'+v[0] for v in VARIANTS}
CLADDING = ('CMCatwalk','CMCatwalkPrison','RMCCatwalkHybrisaElevator')


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def model_id(proto):
    return 'CMU3DWaterTank' if proto=='RMCTankReagentWater' else 'CMU3D'+proto


class Pool:
    def __init__(self):
        self.entries,self.crops,self.cache = [],[],{}
        paths = [p for p in (ROOT/MODELS.parent).glob('*.yml') if p.name!=ART.name]
        for stage in (ROOT/'.codex').glob('*staged*'):
            if stage!=STAGE:
                paths.extend(stage.rglob('*.yml'))
        used = set()
        for p in paths:
            for item in yaml.load(p.read_text(encoding='utf-8-sig'),Loader=yaml.CSafeLoader) or []:
                if item.get('type')=='cmu3DSurface': used.add(item['atlasIndex'])
        assert not set(range(1420,1480)) & used,'Reserved tank atlas range has a conflict.'

    def crop(self,state,rect):
        pixels=Image.open(SOURCE/(state+'.png')).convert('RGBA').crop(rect)
        digest=hashlib.sha256(str(pixels.size).encode()+pixels.tobytes()).hexdigest()
        if digest not in self.cache:
            index=1420+len(self.entries)
            assert index<1480
            uid=f'CMU3DReagentTankSurface{index}'
            path=STAGE/TEXTURES/(uid+'.png');path.parent.mkdir(parents=True,exist_ok=True);pixels.save(path)
            self.entries.append({'type':'cmu3DSurface','id':uid,'atlasIndex':index,
                                 'texture':f'/Textures/CMU14/ThreeD/Surfaces/{uid}.png'})
            self.cache[digest]=uid
        uid=self.cache[digest]
        assert Image.open(STAGE/TEXTURES/(uid+'.png')).convert('RGBA').tobytes()==pixels.tobytes()
        self.crops.append({'state':state,'rect':list(rect),'surface':uid,'rgbaSha256':hashlib.sha256(pixels.tobytes()).hexdigest()})
        return uid


def geometry(pool):
    parts=[]
    im=Image.open(SOURCE/'tank_normal.png').convert('RGBA')
    color=lambda x,y:'#'+''.join(f'{v:02X}' for v in im.getpixel((x,y))[:3])
    def box(label,low,high,tint,**rest):
        parts.append({'label':label,'min':list(low),'max':list(high),'color':tint,**rest})
    def front(label,state,rect,y):
        box(label,((rect[0]-16)/32,y,(30-rect[3])/32),((rect[2]-16)/32,y+.003,(30-rect[1])/32),
            '#FFFFFF',surface=pool.crop(state,rect),surfaceAxis='XZ')

    box('left cabinet core',(-.40625,-.235,.0625),(0,.225,.78125),color(9,17))
    box('stepped cabinet crown',(-.40625,-.235,.78125),(0,.225,.8125),color(6,8))
    front('original cabinet face','tank_normal',(2,4,17,28),-.246)
    box('low wheeled chassis',(-.375,-.20,.0625),(.28125,.20,.125),color(10,27))
    for x,label in [(-.375,'left'),(.15625,'right')]:
        for y,side in [(-.20,'front'),(.125,'rear')]:
            box(label+' '+side+' wheel',(x,y,0),(x+.09375,y+.075,.09375),color(5,29),shape='CylinderY')
    front('left wheel source edge','tank_normal',(4,28,7,30),-.246)
    front('right wheel source edge','tank_normal',(21,28,24,30),-.246)
    box('right upright frame',(.25,-.225,.125),(.3125,.20,.71875),color(24,20))
    box('push handle grip',(.375,-.20,.50),(.4375,.20,.8125),color(28,8))
    box('push handle lower return',(.3125,-.20,.4375),(.40625,.20,.50),color(26,15))
    box('upper vessel frame',(.03125,-.225,.71875),(.4375,.20,.75),color(20,6))
    box('fixed middle frame bar',(.03125,-.239,.21875),(.3125,.20,.28125),color(20,21))
    front('original right frame and handle','tank_normal',(17,4,30,28),-.246)
    box('upper vessel',(.03125,-.232,.28125),(.25,.20,.71875),'#FFFFFF',
        surface=pool.crop('tn_color-1',(17,7,24,21)),surfaceAxis='XZ')
    box('lower vessel',(.03125,-.232,.125),(.25,.20,.21875),'#FFFFFF',
        surface=pool.crop('tn_color-1',(17,23,24,26)),surfaceAxis='XZ')
    front('static green source indicator','t_inactive',(5,18,6,26),-.250)
    return parts


def state_parts(parts,visible,tint):
    result=deepcopy(parts)
    rgba=build_models.rgba(tint or '#FFFFFF00')
    factor=[1-rgba[3]+rgba[3]*c for c in rgba[:3]] if visible else [1,1,1]
    color='#'+''.join(f'{round(c*255):02X}' for c in factor)
    for part in result:
        if part['label'] in VESSEL:
            assert part['color']=='#FFFFFF'
            part['color']=color
    return result


def source_composite(visible,tint):
    body=Image.open(SOURCE/'tank_normal.png').convert('RGBA')
    vessel=Image.open(SOURCE/'tn_color-1.png').convert('RGBA')
    body.alpha_composite(vessel)
    if visible:
        data=np.asarray(vessel).astype(np.float64)/255
        rgba=np.array(build_models.rgba(tint))
        over=np.round(data*rgba*255).astype(np.uint8)
        body.alpha_composite(Image.fromarray(over,'RGBA'))
    body.alpha_composite(Image.open(SOURCE/'t_inactive.png').convert('RGBA'))
    return body


def source_proof(pool):
    proof=[]
    for state in ('tank_normal','tn_color-1','t_inactive'):
        canvas=Image.new('RGBA',(32,32))
        for crop in pool.crops:
            if crop['state']!=state: continue
            image=Image.open(STAGE/TEXTURES/(crop['surface']+'.png')).convert('RGBA')
            canvas.alpha_composite(image,tuple(crop['rect'][:2]))
        original=Image.open(SOURCE/(state+'.png')).convert('RGBA')
        assert canvas.tobytes()==original.tobytes(),state
        proof.append({'state':state,'writtenCropReassemblyExact':True,'rgbaSha256':hashlib.sha256(canvas.tobytes()).hexdigest()})
    one=Image.open(SOURCE/'tn_color-1.png').convert('RGBA');two=Image.open(SOURCE/'tn_color-2.png').convert('RGBA')
    assert one.tobytes()==two.tobytes()
    data=np.asarray(one).astype(np.float64)/255
    assert set(np.asarray(one)[:,:,3].flatten())=={0,255}
    maximum_error=0
    for alpha in range(256):
        a=alpha/255
        for rgb in [(0,0,0),(1,1,1),(.5,.2,.8),(226/255,151/255,46/255),(0,100/255,200/255)]:
            expected=data[:,:,:3]*(1-a)+data[:,:,:3]*np.array(rgb)*a
            equivalent=data[:,:,:3]*((1-a)+np.array(rgb)*a)
            maximum_error=max(maximum_error,float(np.max(np.abs(expected-equivalent))))
    assert maximum_error<1e-12
    return {'states':proof,'identicalFillStates':True,'binarySourceFillAlpha':True,
            'floatingCompositeCases':256*5,'maximumRgbError':maximum_error,
            'note':'RGBA layer compositing equivalence is proved before display quantization. Review PNGs quantize channels to bytes and are not native GPU screenshots.'}


def stage_registry(pool):
    registry=dict(surfaces.load_surfaces())
    for item in pool.entries:
        path=STAGE/TEXTURES/(item['id']+'.png')
        registry[item['id']]={**item,'file':path,'image':Image.open(path).convert('RGBA')}
    surfaces.load_surfaces=lambda:registry


def fixtures(parts):
    records=[{'id':'empty','fillVisible':False,'fillState':'tn_color-1','fillColor':'#FFFFFF00','volume':0}]
    for name,_,color in VARIANTS:
        if color:
            records.append({'id':name.lower()+'-full','fillVisible':True,'fillState':'tn_color-2','fillColor':color,'volume':1000})
    records.extend([
        {'id':'dermaline-partial','fillVisible':True,'fillState':'tn_color-1','fillColor':'#E2972E','volume':500},
        {'id':'equal-water-dermaline-mix','fillVisible':True,'fillState':'tn_color-1','fillColor':'#717E7B','volume':500,
         'explanation':'Quantized 50/50 quantity-weighted color of Water and Dermaline; runtime float color remains authoritative.'},
        {'id':'partial-alpha-blue','fillVisible':True,'fillState':'tn_color-1','fillColor':'#0064C880','volume':500},
        {'id':'transparent-fill','fillVisible':True,'fillState':'tn_color-1','fillColor':'#0064C800','volume':500},
    ])
    template=[{k:v for k,v in p.items() if k!='color'} for p in parts]
    for record in records:
        record['parts']=state_parts(parts,record['fillVisible'],record['fillColor'])
        assert [{k:v for k,v in p.items() if k!='color'} for p in record['parts']]==template
    return records


def remove_old_water():
    before=(ROOT/UTILITIES).read_text(encoding='utf-8')
    blocks=re.split(r'(?=^- type: cmu3DModel\s*$)',before,flags=re.MULTILINE)
    selected=[b for b in blocks if re.search(r'^  id: CMU3DWaterTank\s*$',b,re.MULTILINE)]
    if not selected:
        assert (ROOT/MODELS).is_file(),'Missing Water model without dedicated replacement.'
        return before
    assert len(selected)==1
    return ''.join(b for b in blocks if b is not selected[0])


def contexts(models,parts,review_dir):
    """Revalidate all 42 map records and compare against unchanged 867-model neighbors."""
    spec=importlib.util.spec_from_file_location('tank_audit_helpers',ROOT/'.codex/audit_reagent_tanks.py')
    helpers=importlib.util.module_from_spec(spec);spec.loader.exec_module(helpers)
    kinds,issues,_=inventory.load_prototypes(ROOT)
    assert not issues
    resolver=inventory.Resolver(kinds['entity'])
    defaults={}
    def comps(proto):
        if proto not in defaults:
            defaults[proto]=inventory.component_map(resolver.resolve(proto)) if proto else {}
        return defaults[proto]
    generated=BASELINE/'Tools/three_d/generated'
    library={m['id']:m for m in json.loads((generated/'models.json').read_text())['models']}
    library.update({m['id']:m for m in models})
    specifications=json.loads((generated/'interior-scenes.json').read_text())
    cases=[];input_hashes={}
    font=ImageFont.load_default(size=15);small=ImageFont.load_default(size=12)
    for spec in specifications:
        scene_path=generated/spec['file'];doc=json.loads(scene_path.read_text());input_hashes[scene_path.relative_to(ROOT).as_posix()]=sha(scene_path)
        selected=[i for i in doc['instances'] if i['prototype'] in TANK_IDS]
        if not selected: continue
        map_path,_=scene.configured_map(ROOT,spec['variant'],spec['level']);input_hashes[map_path.relative_to(ROOT).as_posix()]=sha(map_path)
        records=helpers.helper.read_selected(map_path,{1,*(i['id'] for i in selected)})
        for record in records.values():comps(record['prototype'])
        transforms=scene.WorldTransforms(records,defaults)
        for entity in selected:
            saved=records[entity['id']];world=transforms.resolve(entity['id'])
            assert [round(world[0],6),round(world[1],6),spec['level']]==entity['position']
            assert round(world[2],9)==entity['yaw']
            assert saved['componentTypes']==['Transform'],(spec,entity['id'],saved['components'])
            source=comps(entity['prototype']);assert source['Sprite']['noRot'] is True
            solution=helpers.solution_fields(entity['prototype'],kinds)
            volume=sum(r['Quantity'] for r in solution.get('reagents',[]));reagents=solution.get('reagents',[])
            assert len(reagents)<=1 and solution['maxVol']==1000
            color=kinds['reagent'][reagents[0]['ReagentId']]['color'].upper() if reagents else None
            visible=volume>0
            nearby=[n for n in doc['instances'] if n['id']!=entity['id'] and
                    abs(n['position'][0]-entity['position'][0])<=2 and abs(n['position'][1]-entity['position'][1])<=2]
            offsets=[]
            for n in nearby:
                if n['prototype'] not in CLADDING or n.get('matchKind')!='exact': continue
                if max(abs(n['position'][i]-entity['position'][i]) for i in range(2))>1e-6: continue
                model=library[n['modelId']];np=doc['geometryVariants'].get(n.get('geometryKey'),model['parts'])
                top=max(build_models.part_bounds(p)[1][2] for p in np)+n.get('renderOffset',[0,0,0])[2]
                assert 0<top<=.1
                offsets.append((top,n['id']))
            floor_top,cladding_id=max(offsets,default=(0,None))
            lift=floor_top+.002 if cladding_id else 0
            candidate=world_parts(state_parts(parts,visible,color),entity['position'],0,(0,0,lift))
            assembled=list(candidate);neighbors=[];unknown=[]
            for n in nearby:
                model=library.get(n.get('modelId'))
                if n['prototype'] in TANK_IDS:
                    model=library[model_id(n['prototype'])]
                    nsol=helpers.solution_fields(n['prototype'],kinds);rs=nsol.get('reagents',[])
                    nc=kinds['reagent'][rs[0]['ReagentId']]['color'].upper() if rs else None
                    np=state_parts(parts,bool(rs),nc)
                    nlift=0
                    for x in doc['instances']:
                        if x['prototype'] in CLADDING and x.get('matchKind')=='exact' and x['position']==n['position']:
                            nlift=max(nlift,max(build_models.part_bounds(p)[1][2] for p in library[x['modelId']]['parts'])+.002)
                    other=world_parts(np,n['position'],0,(0,0,nlift))
                elif model:
                    np=doc['geometryVariants'].get(n.get('geometryKey'),model['parts'])
                    other=world_parts(np,n['position'],n.get('renderYaw',n['yaw']),n.get('renderOffset',[0,0,0]))
                else:
                    unknown.append({'id':n['id'],'prototype':n['prototype']});continue
                hits,gap=contacts(candidate,other)
                neighbors.append({'id':n['id'],'prototype':n['prototype'],'matchKind':'exact' if n['prototype'] in TANK_IDS else n['matchKind'],
                                  'contacts':hits,'minimumAabbSeparation':gap})
                if max(abs(n['position'][i]-entity['position'][i]) for i in range(2))<=1.3:
                    for p in other:
                        # Explicit review cutaway exposes the target; contacts above use full solids.
                        if 'Wall' in n['prototype'] or 'Window' in n['prototype'] or 'Shutter' in n['prototype']:
                            if p['min'][2]>=.38:continue
                            p['max'][2]=min(p['max'][2],.38)
                        if p['min'][2]>=1.5:continue
                        assembled.append(p)
            total=sum(len(n['contacts']) for n in neighbors)
            case={'variant':spec['variant'],'level':spec['level'],'id':entity['id'],'prototype':entity['prototype'],
                  'saved':saved,'position':entity['position'],'yaw':entity['yaw'],'renderYaw':0,
                  'solution':solution,'fixtureAppearance':{'fillVisible':visible,'fillTint':color},
                  'claddingEntity':cladding_id,'claddingTop':floor_top,'renderOffset':[0,0,lift],
                  'wheelMinimumZ':lift,'clearanceAboveCladding':lift-floor_top,
                  'neighbors':neighbors,'unknownNeighbors':unknown,'contactPairs':total}
            cases.append(case)
            for p in assembled:
                for key in ('min','max'):
                    p[key][0]-=entity['position'][0];p[key][1]-=entity['position'][1]
            card=Image.new('RGB',(800,500),'#17212B');draw=ImageDraw.Draw(card)
            draw.text((14,9),f'{spec["variant"]} {spec["level"]:+d} / UID {entity["id"]} / {entity["prototype"]}',font=font,fill='white')
            draw.text((14,32),f'Post-MapInit color fixture; cladding {cladding_id}, lift {lift:.3f}; {total} conservative contact pairs',font=small,fill='#B8CDD8')
            panel=build_models.render_model({'parts':assembled},(620,400),-math.pi/2+.32,.65,pixels_per_unit=155,screen_origin=(310,325))
            card.paste(panel,(175,65))
            ref=source_composite(visible,color).resize((160,160),Image.Resampling.NEAREST);card.paste(ref,(8,145),ref)
            draw.text((14,470),'Source inset; modeled neighbors. Wall/window/shutter cutaway at .38 for review only.',font=small,fill='#B8CDD8')
            card.save(review_dir/f'context-{spec["variant"]}-{spec["level"]}-{entity["id"]}.png')
            print(f'Context {len(cases)}/42: {spec["variant"]} {spec["level"]:+d} UID {entity["id"]}, contacts {total}',flush=True)
    assert len(cases)==42
    return cases,input_hashes


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--apply',action='store_true');args=parser.parse_args()
    if args.apply:
        report=json.loads((STAGE/'reagent-tanks-proof.json').read_text())
        assert report['passed'],'Staged proof must pass before applying.'
        assert sha(Path(__file__))==report['generatorSha256'],'Generator changed after staged verification.'
        for path,digest in report['writtenAssetsSha256'].items(): assert sha(STAGE/path)==digest,path
        for path,digest in report['immutableInputSha256'].items(): assert sha(ROOT/path)==digest,path
        current=sha(ROOT/UTILITIES)
        assert current in (report['utilitiesBeforeSha256'],report['writtenAssetsSha256'][UTILITIES.as_posix()])
        for path in report['writtenAssetsSha256']:
            source=STAGE/path;target=ROOT/path;target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(source,target)
        shutil.copyfile(Path(__file__),ROOT/'Tools/three_d/author_reagent_tanks.py')
        target=ROOT/'Tools/three_d/generated/reagent-tanks-review';target.mkdir(parents=True,exist_ok=True)
        for source in (STAGE/'review').glob('*.png'):shutil.copyfile(source,target/source.name)
        shutil.copyfile(STAGE/'runtime-fixtures.json',ROOT/'Tools/three_d/generated/reagent-tank-runtime-fixtures.json')
        report['status']='applied draft asset files';report['generatorSha256']=sha(ROOT/'Tools/three_d/author_reagent_tanks.py')
        (ROOT/'Tools/three_d/generated/reagent-tanks-proof.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
        for path,digest in report['writtenAssetsSha256'].items():assert sha(ROOT/path)==digest,path
        print(json.dumps({'applied':True,'models':6,'netNewModels':5,'parts':report['partCount'],'textures':len(report['atlasIndices'])}));return

    pool=Pool();parts=geometry(pool);stage_registry(pool);models=[]
    for name,reagent,tint in VARIANTS:
        proto='RMCTankReagent'+name
        model={'type':'cmu3DModel','id':model_id(proto),'label':('Empty' if not reagent else name)+' reagent cart','status':'draft',
               'sourcePrototypes':[proto],'referencePrototype':proto,'referenceRsi':'_RMC14/Structures/Storage/reagent_tank.rsi',
               'referenceState':'tank_normal','referenceDirection':0,'sourceDirections':1,'useEntityRotation':False,'yawOffset':0,
               'placement':'floor','groundOffset':'0,0','reagentTankAppearance':{'vesselParts':VESSEL},
               'description':'Source-shaped asymmetric wheeled reagent cart: equipment cabinet, narrow vessel, frame, handle and low chassis. Original body/vessel/status crops retained. Empty white vessel is the authored base; the bounded appearance adapter owns live fill tint over it. Source noRot retained. Hidden depth and rear wheels are inferred; no falling-liquid or pumping animation. See SOURCES_REAGENT_TANKS.md.',
               'parts':deepcopy(parts)}
        models.append(build_models.validate_model(model))
    serial=deepcopy(models)
    for model in serial:
        model['groundOffset'] = ', '.join(str(v) for v in model['groundOffset'])
        for p in model['parts']:
            for key in ('min','max'):p[key]=', '.join(f'{v:.7f}' for v in p[key])
    for relative,data in [(MODELS,serial),(ART,pool.entries)]:
        path=STAGE/relative;path.parent.mkdir(parents=True,exist_ok=True)
        path.write_text('# Generated source-shaped reagent cart drafts; see SOURCES_REAGENT_TANKS.md.\n'+yaml.safe_dump(data,sort_keys=False,width=110),encoding='utf-8')
    before_hash=sha(ROOT/UTILITIES);(STAGE/UTILITIES).write_text(remove_old_water(),encoding='utf-8')
    loaded=build_models.load_models(STAGE/MODELS)
    assert len(loaded)==6 and all(m['parts']==loaded[0]['parts'] for m in loaded)
    proof=source_proof(pool);runtime=fixtures(parts)
    review_dir=STAGE/'review';review_dir.mkdir(parents=True,exist_ok=True)
    font=ImageFont.load_default(size=17);small=ImageFont.load_default(size=13)
    for fixture in runtime:
        card=Image.new('RGB',(1000,460),'#17212B');draw=ImageDraw.Draw(card)
        draw.text((15,10),'Runtime appearance fixture / '+fixture['id'],font=font,fill='white')
        draw.text((15,36),'Actual source layer composition; inferred solid depth. Offline fixture, not a captured game state.',font=small,fill='#B8CDD8')
        image=source_composite(fixture['fillVisible'],fixture['fillColor']).resize((256,256),Image.Resampling.NEAREST);card.paste(image,(10,85),image)
        for i,(yaw,pitch) in enumerate([(-math.pi/2,.18),(-math.pi/3,.5),(math.pi/3,.5)]):
            panel=build_models.render_model({'parts':fixture['parts']},(238,330),yaw,pitch,pixels_per_unit=265,screen_origin=(115,265))
            card.paste(panel,(280+i*240,75))
        draw.text((15,426),'Body and indicator remain unchanged; only vessel color varies. Wheel bottoms Z 0; no invented fill-height animation.',font=small,fill='#B8CDD8')
        card.save(review_dir/(fixture['id']+'.png'))
    (STAGE/'runtime-fixtures.json').write_text(json.dumps({'schemaVersion':1,'modelIds':[m['id'] for m in models],
        'vesselParts':VESSEL,'fixtures':runtime,'orderedGeometryInvariant':True,
        'description':'Explicit post-startup/default-color and synthetic appearance fixtures. Saved map exports may preserve pre-startup white layers.'},indent=2)+'\n')
    cases,hashes=contexts(loaded,parts,review_dir)
    notes='''# Source-matched reagent cart drafts

Six exact model records cover 41 Redux placements and 1 classic placement. Five records are new (Empty, Dermaline, Meralyne, Phoron, SulphuricAcid); CMU3DWaterTank retains its existing ID and moves from garrison_utilities.yml into the dedicated reagent library. Fuel replaces its Sprite layers with weldtank, so its separate draft remains unchanged.

The source has an asymmetric left equipment cabinet, right vessel, frame/handle, low chassis and wheels. Original tank_normal, tn_color-1 and t_inactive pixels are retained as exact crops. Source width is 28 pixels / .875 tile and image height is 26 pixels / .8125 tile. Cabinet/vessel depth (.47 tile), rear construction and rear wheels are inferred. These are drafts; front artwork preservation does not establish exact arbitrary-camera fidelity.

The authored assembly is the permanent white vessel before fill appearance. The mapped Fill layer may hide or show its tinted overlay; the permanent white vessel remains visible underneath. tn_color-1 and tn_color-2 are byte-identical, with only binary alpha; their colors composite over the identical white background as factor 1-alpha+alpha*RGB. The two named vessel parts are white with grayscale source textures. Runtime fixtures cover every default reagent color, empty, partial, a mixed color and partial/zero-alpha tint. Shapes, masks and bounds remain invariant. Full/partial state names do not imply a falling liquid surface. The static t_inactive strip is preserved; unused active/boom artwork is not animated.

All saved transforms remain unchanged and source noRot fixes presentation yaw. Wheel bottoms are at zero. Exact co-located catwalk cladding uses its actual top plus .002 presentation clearance; bare floor stays at zero. Context proofs use the frozen 867-model baseline, full neighboring solids for contacts and clearly labeled low wall/window/shutter cutaways only in the review pictures. Source composition fixtures predict the audited post-MapInit appearance and are not screenshots of a running client.

Artwork: CC-BY-SA-3.0, taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/obj/structures/liquid_tanks.dmi, as recorded by the original RSI metadata. Derived crops retain original pixels. Preserve source attribution when redistributing.

Generator: Tools/three_d/author_reagent_tanks.py (stages by default, --apply verifies the staged proof and writes dedicated resources). Source/context proof: Tools/three_d/generated/reagent-tanks-proof.json; appearance fixtures: reagent-tank-runtime-fixtures.json; review cards: reagent-tanks-review/. Native adapter, renderer capacity, global deterministic exports and game acceptance are coordinated separately.
'''
    (STAGE/NOTES).parent.mkdir(parents=True,exist_ok=True);(STAGE/NOTES).write_text(notes,encoding='utf-8')
    files=[MODELS,ART,UTILITIES,NOTES,*[TEXTURES/(e['id']+'.png') for e in pool.entries]]
    hashes.update({p.relative_to(ROOT).as_posix():sha(p) for p in SOURCE.glob('*') if p.is_file()})
    hashes['.codex/captured-scene-baseline867/Tools/three_d/generated/models.json']=sha(BASELINE/'Tools/three_d/generated/models.json')
    hashes['Resources/Prototypes/_RMC14/Entities/Structures/Storage/reagent_tank.yml']=sha(ROOT/'Resources/Prototypes/_RMC14/Entities/Structures/Storage/reagent_tank.yml')
    report={'schemaVersion':1,'status':'staged drafts','passed':all(c['contactPairs']==0 for c in cases),
            'models':[m['id'] for m in loaded],'partCount':len(parts),'atlasIndices':[e['atlasIndex'] for e in pool.entries],
            'sourceProof':proof,'runtimeFixtureCount':len(runtime),'orderedStateGeometryInvariant':True,
            'contexts':cases,'contextCount':len(cases),'contactPairs':sum(c['contactPairs'] for c in cases),
            'claddingTargets':list(CLADDING),'claddingContextCount':sum(c['claddingEntity'] is not None for c in cases),
            'preservedFirstPass':{'report':'.codex/reagent-tanks-staged/first-pass-proof.json','sha256':sha(STAGE/'first-pass-proof.json'),
                                  'finding':'Water 5265/5266 initially intersected their co-located Hybrisa elevator grate; only the exact floor-cladding allowlist was extended, preserving geometry and source appearance.'},
            'utilitiesBeforeSha256':before_hash,'immutableInputSha256':hashes,
            'writtenAssetsSha256':{p.as_posix():sha(STAGE/p) for p in files},
            'generatorSha256':sha(Path(__file__)),
            'limitations':['Depth and hidden faces are inferred.','Conservative AABB contacts are distinct from gameplay collision.','Runtime fixtures are offline source-composition cases, not a launched client.','No global export, native admission or game acceptance run by this author.']}
    (STAGE/'reagent-tanks-proof.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({'models':6,'parts':len(parts),'textures':len(pool.entries),'contexts':len(cases),'contacts':report['contactPairs'],'passed':report['passed']}),flush=True)


if __name__=='__main__':main()
