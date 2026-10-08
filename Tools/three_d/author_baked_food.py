"""Reproduce the placed, previously unmapped bread/pie/cake source-specific drafts.

Dedicated assets only: no global export, engine build, game or server launch.
"""
import argparse
from collections import Counter
from copy import deepcopy
import hashlib
from io import BytesIO
import json
import math
from pathlib import Path
import re
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont
import yaml

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT/'Tools/three_d'))
import build_models as bm
import inventory
import scene
import surfaces
from author_wide_machinery import world_parts, contacts

MODEL_FILE = ROOT/'Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_baked_food.yml'
ART_FILE = MODEL_FILE.with_name('garrison_baked_food_art.yml')
TEXTURES = ROOT/'Content.CMU/Resources/Textures/CMU14/ThreeD/Surfaces'
REFERENCES = ROOT/'Content.CMU/Resources/Textures/CMU14/ThreeD/BakedFoodReferences.rsi'
NOTES = ROOT/'Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_BAKED_FOOD.md'
GENERATED = ROOT/'Tools/three_d/generated'
REVIEW = GENERATED/'review/baked-food'
SOURCE = ROOT/'Resources/Textures/Objects/Consumable/Food/Baked'
PROTOTYPES = (
    'FoodBreadBaguette','FoodBreadBaguetteSlice','FoodBreadBanana','FoodBreadGarlicSlice','FoodBreadMeat','FoodBreadPlain',
    'FoodCakeApple','FoodCakeBirthday','FoodCakeBirthdaySlice','FoodCakeBlueberry','FoodCakeCarrot','FoodCakeCheese',
    'FoodCakeChocolate','FoodCakePlain','FoodPieApple','FoodPieBaklava','FoodPieBananaCream','FoodPieCherry',
    'FoodPieCherrySlice','FoodPieClafoutis','FoodPieFrosty','FoodPieMeat','FoodPiePumpkin','WeaponBaguette')
CHECK = False
WRITTEN = []


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def json_write(path,value):
    path.parent.mkdir(parents=True,exist_ok=True)
    path.write_text(json.dumps(value,indent=2)+'\n',encoding='utf-8')


def asset_write(path,data):
    if CHECK:
        assert path.is_file() and path.read_bytes()==data, f'Generated asset differs: {path}'
    else:
        path.parent.mkdir(parents=True,exist_ok=True)
        path.write_bytes(data)
    WRITTEN.append(path)


def png(image):
    stream=BytesIO(); image.save(stream,format='PNG'); return stream.getvalue()


def source_frame(prototype):
    sprite=prototype['sprite']
    assert not sprite.get('noRot') and sprite.get('offset','0,0').replace(' ','')=='0,0'
    assert not sprite.get('rotation',0) and sprite.get('scale','1,1').replace(' ','')=='1,1'
    layers=sprite.get('layers') or [{'state':sprite['state']}]
    composite=Image.new('RGBA',(32,32)); evidence=[]
    for layer in layers:
        assert layer.get('visible',True) and not any(k in layer for k in ('shader','scale','offset','rotation'))
        resource=layer.get('sprite',layer.get('rsi',sprite['sprite']))
        folder=bm.resource_file(resource)
        meta=json.loads((folder/'meta.json').read_text())
        state=next(s for s in meta['states'] if s['name']==layer['state'])
        assert meta['size']=={'x':32,'y':32} and state.get('directions',1)==1
        assert state.get('delays',[[1]])==[[1]]
        frame=Image.open(folder/(layer['state']+'.png')).convert('RGBA')
        assert frame.size==(32,32)
        tint=scene.normalize_tint(layer.get('color','#FFFFFF'))
        multiplier=np.array(bm.rgba(tint))
        tinted=Image.fromarray(np.rint(np.asarray(frame)*multiplier).astype(np.uint8))
        composite.alpha_composite(tinted)
        evidence.append({'rsi':resource,'state':layer['state'],'tint':tint,'directions':1,'frames':1,
                         'rawRgbaSha256':hashlib.sha256(frame.tobytes()).hexdigest(),
                         'tintedRgbaSha256':hashlib.sha256(tinted.tobytes()).hexdigest(),
                         'sourceSha256':sha(folder/(layer['state']+'.png')),'metaSha256':sha(folder/'meta.json'),
                         'license':meta.get('license'),'copyright':meta.get('copyright')})
    overall=scene.normalize_tint(sprite.get('color','#FFFFFF'))
    assert overall in ('#FFFFFF','#FFFFFFFF')
    return composite,{'prototype':prototype['id'],'layers':evidence,'sourceNoRot':False,'sourceOffset':[0,0],
                      'sourceRotation':0,'sourceOverallTint':overall,'size':[32,32],
                      'alphaBounds':list(composite.getbbox()),'composedRgbaSha256':hashlib.sha256(composite.tobytes()).hexdigest(),
                      'instanceCounts':prototype['instanceCounts']}


def source_montage(images):
    REVIEW.mkdir(parents=True,exist_ok=True)
    font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',14)
    card=Image.new('RGB',(1200,math.ceil(len(images)/6)*210+45),'#17212B'); draw=ImageDraw.Draw(card)
    draw.text((16,10),'Original static layers composed with prototype tint; full 32 × 32 frame, 4× nearest.',font=font,fill='white')
    for index,(uid,image) in enumerate(images.items()):
        x=(index%6)*200; y=(index//6)*210+45
        panel=Image.new('RGBA',(128,128),'#60717A'); panel.alpha_composite(image.resize((128,128),Image.Resampling.NEAREST))
        card.paste(panel.convert('RGB'),(x+35,y+8))
        draw.text((x+8,y+145),uid.removeprefix('Food'),font=font,fill='white')
        source_path=REVIEW/'sources'/(uid+'.png');source_path.parent.mkdir(exist_ok=True);image.save(source_path)
    card.save(REVIEW/'source-montage.png')


class Pool:
    def __init__(self):
        self.entries=[]; self.crops=[]; self.cache={}
        used={}
        paths=[p for p in MODEL_FILE.parent.glob('*.yml') if p!=ART_FILE]
        for folder in (ROOT/'.codex').glob('*staged*'):
            paths.extend(folder.rglob('*.yml'))
        for path in paths:
            for row in yaml.load(path.read_text(encoding='utf-8-sig'),Loader=yaml.CSafeLoader) or []:
                if row.get('type')=='cmu3DSurface': used[row['atlasIndex']]=row['id']
        assert not set(range(1520,1600)) & used.keys(), 'Reserved baked-food atlas range has a conflict'

    def crop(self,uid,image,rect,purpose,ellipse=False):
        original=image.crop(rect)
        pixels=original.copy()
        removed=0
        if ellipse:
            # The source top is foreshortened. Project its original RGB onto a
            # round top in plan; discard only corners outside that solid cap.
            data=np.array(pixels)
            for y in range(pixels.height):
                for x in range(pixels.width):
                    if ((x+.5)/pixels.width*2-1)**2+((y+.5)/pixels.height*2-1)**2>1:
                        removed+=int(data[y,x,3]>0);data[y,x,3]=0
            pixels=Image.fromarray(data)
        signature=hashlib.sha256(str(pixels.size).encode()+pixels.tobytes()).hexdigest()
        if signature not in self.cache:
            index=1520+len(self.entries); assert index<1600
            surface=f'CMU3DBakedFoodSurface{index}'
            path=TEXTURES/(surface+'.png');asset_write(path,png(pixels))
            self.entries.append({'type':'cmu3DSurface','id':surface,'atlasIndex':index,
                                 'texture':f'/Textures/CMU14/ThreeD/Surfaces/{surface}.png'})
            self.cache[signature]=surface
        surface=self.cache[signature]
        actual=Image.open(TEXTURES/(surface+'.png')).convert('RGBA')
        assert actual.size==pixels.size and actual.tobytes()==pixels.tobytes()
        a,b=np.array(actual),np.array(original)
        visible=a[:,:,3]>0
        assert np.array_equal(a[visible],b[visible])
        self.crops.append({'prototype':uid,'rect':list(rect),'purpose':purpose,'surface':surface,
                           'capMask':'elliptical alpha crop' if ellipse else None,'discardedCornerPixels':removed,
                           'retainedRgbaExact':True,'retainedPixels':int(visible.sum()),
                           'rgbaSha256':hashlib.sha256(actual.tobytes()).hexdigest()})
        return surface


def geometry(uid,image,pool):
    parts=[]; pixels=np.array(image)
    def sample(x,y):
        yy,xx=np.where(pixels[:,:,3]>0)
        index=np.argmin((xx-x)**2+(yy-y)**2)
        return '#'+''.join(f'{v:02X}' for v in pixels[yy[index],xx[index],:3])
    def palette_color(test,region=None):
        data=np.array(image.crop(region)) if region else pixels
        colors=Counter(tuple(int(v) for v in pixel[:3]) for pixel in data.reshape(-1,4) if pixel[3] and test(*pixel[:3].astype(int)))
        assert colors,'Required source palette class is absent'
        rgb=max(colors,key=lambda rgb:(colors[rgb],sum(rgb)))
        return '#'+''.join(f'{v:02X}' for v in rgb)
    def solid(label,center,size,color,shape='Box',yaw=0,pitch=0):
        p={'label':label,'min':[c-s/2 for c,s in zip(center,size)],'max':[c+s/2 for c,s in zip(center,size)],'color':color}
        if shape!='Box':p['shape']=shape
        if yaw:p['yaw']=(yaw+180)%360-180
        if pitch:p['pitch']=pitch
        parts.append(p);return p
    def disc(label,diameter,z0,z1,color):
        return solid(label,[0,0,(z0+z1)/2],[diameter,diameter,z1-z0],color,'CylinderZ')
    def top(label,rect,width,depth,z,ellipse=True):
        p=solid(label,[0,0,z+.001],[width,depth,.002],'#FFFFFF')
        p.update(surface=pool.crop(uid,image,rect,'Source top decoration projected onto its actual solid cap.',ellipse),surfaceAxis='XY')
    def triangle(label,width,depth,z0,z1,color):
        # Rotate an actual triangular prism into a horizontal slice. The raw X
        # extent becomes height, raw Z becomes width, preserving real cut faces.
        solid(label,[0,0,(z0+z1)/2],[z1-z0,depth,width],color,'WedgeY',yaw=45,pitch=90)
    def candle(label,x,y,z):
        gold=sample(16,10); bright=sample(15,9)
        solid(label+' wax',[x,y,z+.055],[.025,.025,.11],gold,'CylinderZ')
        solid(label+' static source flame',[x,y,z+.13],[.04,.035,.065],bright,'Ellipsoid')

    if uid=='FoodBreadBaguette':
        # The saved sprite's diagonal loaf becomes a diagonal rounded solid,
        # with five pale score openings rather than a textured rectangular bar.
        angle=35
        solid('long rounded baked crust',[0,0,.085],[1.00,.205,.17],sample(17,18),'Ellipsoid',yaw=angle)
        solid('rounded lower crust',[0,0,.029],[.92,.155,.055],sample(10,24),'Ellipsoid',yaw=angle)
        c,s=math.cos(math.radians(angle)),math.sin(math.radians(angle))
        pale=palette_color(lambda r,g,b:r>210 and g>175 and b>90)
        for n,x in enumerate((-.32,-.16,0,.16,.32)):
            crown=.085+.085*math.sqrt(1-(x/.50)**2)
            solid(f'expanded pale score {n+1}',[x*c,x*s,crown-.024],[.050,.132,.052],pale,'Ellipsoid',yaw=angle-20)
    elif uid in ('FoodBreadPlain','FoodBreadBanana','FoodBreadMeat'):
        # A cut loaf has a rounded crust vault, flat sole, actual end face and a
        # separate original crumb/spiral crop. Its hidden depth is inferred.
        height=.35 if uid!='FoodBreadBanana' else .30
        solid('flat crust sole',[0,.01,.027],[.56,.56,.054],sample(24,23))
        solid('rounded loaf crust vault',[0,.025,height/2],[.60,.60,height],sample(23,11),'CylinderY')
        solid('rounded rear heel',[0,.30,height/2],[.57,.11,height*.95],sample(23,13),'Ellipsoid')
        face={'FoodBreadPlain':(2,8,17,25),'FoodBreadBanana':(3,11,17,25),'FoodBreadMeat':(1,9,18,26)}[uid]
        crop=image.crop(face)
        solid('solid cut crumb end',[0,-.277,height/2],[.55,.016,height*.96],sample(face[0]+6,face[1]+8),'CylinderY')
        p=solid('original cut crumb and filling',[0,-.287,height/2],[.59,.003,height],'#FFFFFF')
        p.update(surface=pool.crop(uid,image,face,'Original loaf cut-face colors and, for meat bread, spiral filling.'),surfaceAxis='XZ')
        if uid=='FoodBreadBanana':
            solid('pale banana icing crown',[0,.19,height-.012],[.56,.22,.036],sample(22,12),'Ellipsoid')
            for n,(x,y,z) in enumerate(((-.20,-.06,.045),(.06,-.16,.06),(.22,.09,.075))):
                solid(f'icing drip {n+1}',[x,y,height-z/2],[.04,.05,z],sample(22,12),'Ellipsoid')
    elif uid in ('FoodBreadBaguetteSlice','FoodBreadGarlicSlice'):
        garlic=uid.endswith('GarlicSlice')
        w,d,h=(.59,.44,.078) if garlic else (.42,.32,.065)
        solid('browned slice crust',[0,0,h/2],[w,d,h],sample(10,20),'CylinderZ')
        solid('exposed crumb',[0,0,h],[w*.90,d*.89,.02],sample(16,15),'CylinderZ')
        rect=(5,10,27,22) if garlic else (9,13,23,20)
        top('original garlic butter and herb top' if garlic else 'original cut crumb top',rect,w*.98,d*.98,h+.011)
    elif uid=='FoodCakeBirthdaySlice':
        triangle('white sponge slice',.38,.38,0,.17,sample(13,19))
        triangle('red frosting slice',.40,.40,.17,.202,sample(18,14))
        triangle('white lower icing edge',.40,.40,.005,.032,sample(11,18))
        candle('single birthday candle',0,.045,.202)
    elif uid=='FoodPieCherrySlice':
        triangle('golden bottom crust',.40,.40,0,.03,sample(12,19))
        triangle('red cherry filling',.38,.38,.03,.104,sample(20,17))
        triangle('baked upper pastry',.40,.40,.104,.14,sample(12,14))
        triangle('thin pale cut crumb',.385,.385,.10,.116,sample(11,15))
    elif uid.startswith('FoodCake'):
        birthday=uid=='FoodCakeBirthday'
        diameter=.74
        disc('whole cake crumb body',diameter*.97,0,.235,sample(16,23))
        disc('lower baked edge',diameter*.96,0,.032,sample(9,24))
        if birthday:
            disc('white icing side',diameter,.024,.226,sample(11,23))
            disc('red icing crown',diameter,.223,.264,sample(16,17))
            for n in range(16):
                a=math.tau*n/16
                solid(f'pink piped side stripe {n+1}',[.370*math.cos(a),.370*math.sin(a),.127],[.028,.016,.135],sample(12,22),yaw=math.degrees(a)+90)
            for n,(x,y) in enumerate(((-.17,-.055),(0,.12),(.17,-.055))):candle(f'birthday candle {n+1}',x,y,.264)
        else:
            z=.245
            disc('frosted top edge',diameter,.22,z,sample(9,13))
            top('original top filling and decoration',(4,9,28,21),diameter,diameter,z)
            if uid in ('FoodCakeApple','FoodCakeBlueberry'):
                # The actual colored filling is baked in the two-layer texture;
                # the raised pale outer frosting retains real thickness.
                for n in range(16):
                    a=math.tau*n/16
                    solid(f'piped frosting edge {n+1}',[.345*math.cos(a),.345*math.sin(a),.252],[.044,.042,.03],sample(6,14),'Ellipsoid')
    elif uid.startswith('FoodPie'):
        diameter=.56 if uid!='FoodPiePumpkin' else .60
        tin=sample(15,22)
        crust=palette_color(lambda r,g,b:r>g*1.06 and g>b*1.1 and r>130)
        disc('metal pie pan',diameter*.88,0,.045,tin)
        disc('rolled pan lip',diameter*.98,.035,.060,sample(10,20))
        disc('solid pastry shell',diameter*.93,.052,.105,crust)
        disc('baked filling',diameter*.82,.088,.127,sample(16,15))
        for n in range(16):
            a=math.tau*n/16
            solid(f'crimped crust edge {n+1}',[diameter*.445*math.cos(a),diameter*.445*math.sin(a),.111],
                  [.046,.044,.035],crust,'Ellipsoid')
        rect=(7,11,25,20) if uid!='FoodPiePumpkin' else (6,12,26,20)
        top('original pastry lattice and filling',rect,diameter*.92,diameter*.92,.130)
        if uid=='FoodPiePumpkin':
            for n,(x,y,z,s) in enumerate(((0,0,.156,.12),(-.025,.008,.183,.075),(.008,.014,.206,.035))):
                solid(f'whipped cream lobe {n+1}',[x,y,z],[s,s*.84,s*.57],sample(15,15),'Ellipsoid')
        elif uid=='FoodPieBananaCream':
            solid('soft cream dome',[0,0,.13],[diameter*.69,diameter*.69,.15],sample(15,13),'Ellipsoid')
    else:
        raise ValueError(uid)
    assert len(parts)<=128
    assert min(bm.part_bounds(p)[0][2] for p in parts)>=-1e-8
    return parts


def configure_surfaces(pool):
    original=yaml.safe_load
    try:
        yaml.safe_load=lambda value:yaml.load(value,Loader=yaml.CSafeLoader)
        registry=dict(surfaces.load_surfaces())
    finally:
        yaml.safe_load=original
    for row in pool.entries:
        path=TEXTURES/(row['id']+'.png')
        registry[row['id']]={**row,'file':path,'image':Image.open(path).convert('RGBA')}
    surfaces.load_surfaces=lambda:registry


def model_reviews(models,images):
    font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',15)
    montage=Image.new('RGB',(1400,math.ceil(len(models)/4)*245+50),'#17212B');draw=ImageDraw.Draw(montage)
    draw.text((16,12),'BAKED FOOD / original default composite and solid 3D draft; depth and underside inferred',font=font,fill='white')
    for index,model in enumerate(models):
        uid=model['sourcePrototypes'][0]
        card=Image.new('RGB',(1100,330),'#17212B');d=ImageDraw.Draw(card)
        d.text((12,8),uid+' / static draft / '+str(len(model['parts']))+' parts',font=font,fill='white')
        ref=images[uid].resize((192,192),Image.Resampling.NEAREST)
        background=Image.new('RGBA',ref.size,'#60717A');background.alpha_composite(ref);card.paste(background.convert('RGB'),(12,55))
        for j,(yaw,pitch) in enumerate(((-math.pi/2,.6),(-.4,.7),(math.pi/2,.6))):
            card.paste(bm.render_model(model,(285,270),yaw,pitch),(220+285*j,45))
        card.save(REVIEW/(model['id']+'.png'))
        x=index%4*350;y=index//4*245+50
        small_ref=images[uid].resize((96,96),Image.Resampling.NEAREST)
        montage.paste(small_ref,(x+6,y+62),small_ref)
        montage.paste(bm.render_model(model,(235,200),-math.pi/2+.25,.70),(x+110,y+10))
        draw.text((x+8,y+213),uid.removeprefix('Food'),font=font,fill='white')
    montage.save(REVIEW/'comparison-montage.png')


def saved_food_records(path):
    """Read only the selected format-7 entity blocks, not the whole map AST."""
    prototype=None; block=[]; result={}
    def finish():
        if block:
            value=inventory.load_yaml(''.join(block))[0]
            result[value['uid']]={'prototype':prototype,**value}
    with path.open(encoding='utf-8-sig') as stream:
        for line in stream:
            if line.startswith('- proto:'):
                finish();block=[]
                prototype=line.split(':',1)[1].strip().strip('"\'')
            elif prototype in PROTOTYPES:
                if line.startswith('  - uid:'):
                    finish();block=[line]
                elif block: block.append(line)
    finish()
    return result


def context_checks(models):
    from placement import resolve_placements
    baseline=ROOT/'.codex/ladder-install-baseline872'
    library=json.loads((baseline/'models.json').read_text())['models']
    index={m['id']:m for m in library}; index.update({m['id']:m for m in models})
    by_proto={p:m for m in models for p in m['sourcePrototypes']}
    records=[]; input_hashes={}; rendered=0
    for spec in json.loads((baseline/'scenes.json').read_text()):
        source_path=baseline/spec['file']; doc=json.loads(source_path.read_text())
        food=[e for e in doc['instances'] if e['prototype'] in by_proto]
        if not food:continue
        map_path=ROOT/doc['map']['path']; saved=saved_food_records(map_path)
        input_hashes[doc['map']['path']]=sha(map_path)
        input_hashes[str(source_path.relative_to(ROOT)).replace('\\','/')]=sha(source_path)
        for item in food:
            model=by_proto[item['prototype']]
            item.update(modelId=model['id'],matchKind='exact',renderYaw=item['yaw'])
        resolve_placements(doc['instances'],list(index.values()),doc.get('geometryVariants',{}))
        for item in food:
            raw=saved[item['id']]
            overrides={c['type']:c for c in raw.get('components',[]) if c['type'] not in ('Transform','Fixtures')}
            assert not overrides.get('Sprite'), 'A saved Sprite override needs its own source review'
            assert item['yaw']==0, 'New saved rotation needs visual orientation review'
            model=by_proto[item['prototype']]
            own=world_parts(model['parts'],item['position'],item['renderYaw'],item.get('renderOffset',[0,0,0]))
            nearby=[]
            for other in doc['instances']:
                if other['id']==item['id'] or other['position'][2]!=item['position'][2]:continue
                if max(abs(other['position'][i]-item['position'][i]) for i in (0,1))>.65:continue
                target=index.get(other.get('modelId'))
                if not target:
                    nearby.append({'id':other['id'],'prototype':other['prototype'],'mapped':False});continue
                actual=doc.get('geometryVariants',{}).get(other.get('geometryKey'),target['parts'])
                placed=world_parts(actual,other['position'],other.get('renderYaw',other['yaw']),other.get('renderOffset',[0,0,0]))
                hits,_=contacts(own,placed)
                nearby.append({'id':other['id'],'prototype':other['prototype'],'mapped':True,
                               'conservativeContactPairs':len(hits),'isChosenSupport':other['id']==item.get('support',{}).get('entity'),
                               'firstContacts':hits[:3]})
            records.append({'variant':spec['variant'],'level':spec['level'],'id':item['id'],'prototype':item['prototype'],
                            'position':item['position'],'yaw':item['yaw'],'modelId':model['id'],
                            'renderOffset':item.get('renderOffset',[0,0,0]),'support':item.get('support'),
                            'savedComponents':raw.get('components',[]),'neighborsWithin0_65':nearby})
            if rendered<6 and (not item.get('support') or item['prototype'] in ('FoodCakeBirthday','FoodBreadPlain','FoodPieClafoutis')):
                pieces=deepcopy(own)
                for other in doc['instances']:
                    if other['id']==item['id'] or other['position'][2]!=item['position'][2]:continue
                    if max(abs(other['position'][i]-item['position'][i]) for i in (0,1))>.8:continue
                    target=index.get(other.get('modelId'))
                    if target:
                        actual=doc.get('geometryVariants',{}).get(other.get('geometryKey'),target['parts'])
                        pieces.extend(world_parts(actual,other['position'],other.get('renderYaw',other['yaw']),other.get('renderOffset',[0,0,0])))
                local=[]
                for piece in pieces:
                    p=deepcopy(piece)
                    for bound in ('min','max'):
                        for axis in (0,1):p[bound][axis]-=item['position'][axis]
                    if p['min'][2]>1.45:continue
                    p['max'][2]=min(p['max'][2],1.45)
                    local.append(p)
                bm.render_model({'parts':local},(800,640),-math.pi/2+.4,.7,pixels_per_unit=260,screen_origin=(400,440)).save(REVIEW/f'context-{spec["variant"]}-{spec["level"]}-{item["id"]}.png')
                rendered+=1
    assert sum(r['variant']=='redux' for r in records)==76 and sum(r['variant']=='classic' for r in records)==56
    return records,input_hashes


def main():
    global CHECK
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-only',action='store_true')
    parser.add_argument('--check',action='store_true',help='Compare dedicated generated asset bytes without writing assets.')
    parser.add_argument('--skip-context',action='store_true',help='Defer saved-context review to root batch export.')
    parser.add_argument('--skip-reviews',action='store_true',help='Skip picture rasterization while checking dedicated assets.')
    args=parser.parse_args()
    CHECK=args.check
    indexed={p['id']:p for p in json.loads((GENERATED/'inventory.json').read_text())['prototypes']}
    coverage={p['id']:p for p in json.loads((GENERATED/'coverage.json').read_text())['prototypes']}
    images={}; evidence=[]
    for uid in PROTOTYPES:
        assert indexed[uid]['instanceCounts']['redux']>0
        assert coverage[uid]['category']=='unmapped' or all(m.get('modelId','').startswith('CMU3DBaked') for m in coverage[uid]['matches'])
        image,record=source_frame(indexed[uid]);images[uid]=image;evidence.append(record)
    if not CHECK and not args.skip_reviews:
        source_montage(images)
    if args.source_only:
        if not CHECK:
            json_write(GENERATED/'baked-food-source-audit.json',{'schemaVersion':1,'scope':'Placed static source layers only; no consumed, sliced-result or in-hand state claim.',
                       'prototypes':evidence,'counts':{'types':len(PROTOTYPES),'redux':76,'classic':56}})
        print(json.dumps({'sourceTypes':len(PROTOTYPES),'composedStaticSources':len(images)}));return
    reference_prototypes=[uid for uid in PROTOTYPES if uid!='WeaponBaguette']
    for uid in reference_prototypes:
        asset_write(REFERENCES/(uid+'.png'),png(images[uid]))
        assert Image.open(REFERENCES/(uid+'.png')).convert('RGBA').tobytes()==images[uid].tobytes()
    copyrights=[]
    for family in ('bread','pie','cake'):
        meta=json.loads((SOURCE/(family+'.rsi')/'meta.json').read_text())
        assert meta['license']=='CC-BY-SA-3.0'
        copyrights.append(f'{family}.rsi: '+meta.get('copyright',''))
    reference_meta={'version':1,'license':'CC-BY-SA-3.0','copyright':'Generated default-layer/tint reference composites, preserving original RGBA pixels after declared prototype tint. Original attribution: '+' | '.join(copyrights),
                    'size':{'x':32,'y':32},'states':[{'name':uid} for uid in reference_prototypes]}
    asset_write(REFERENCES/'meta.json',(json.dumps(reference_meta,indent=2)+'\n').encode('utf-8'))
    pool=Pool();models=[]
    for uid in PROTOTYPES:
        if uid=='WeaponBaguette':continue
        layers=evidence[PROTOTYPES.index(uid)]['layers']
        model={'type':'cmu3DModel','id':'CMU3DBaked'+uid.removeprefix('Food'),'label':uid.removeprefix('Food'),
               'status':'draft','sourcePrototypes':[uid]+(['WeaponBaguette'] if uid=='FoodBreadBaguette' else []),
               'referencePrototype':uid,'referenceRsi':'CMU14/ThreeD/BakedFoodReferences.rsi','referenceState':uid,
               'referenceTint':'#FFFFFF','sourceDirections':1,'useEntityRotation':True,'yawOffset':0,
               'placement':'surface','groundOffset':'0, 0',
               'description':'Source-shaped static baked food with actual solid crust, crumb/filling and source-specific decoration. Default source layers/tints are baked into dedicated color/crop materials. Hidden depth and underside are inferred. No consumed, slicing, in-hand or runtime appearance state is claimed. See SOURCES_BAKED_FOOD.md.',
               'parts':geometry(uid,images[uid],pool)}
        models.append(model)
    assert images['WeaponBaguette'].tobytes()==images['FoodBreadBaguette'].tobytes()
    configure_surfaces(pool)
    models=[bm.validate_model(m) for m in models]
    serialized=deepcopy(models)
    vector_count=0
    for model in serialized:
        assert isinstance(model['groundOffset'],(tuple,list))
        model['groundOffset']=', '.join(str(v) for v in model['groundOffset'])
        for p in model['parts']:
            for bound in ('min','max'):
                p[bound]=', '.join(f'{v:.7f}' for v in p[bound]);vector_count+=1
    for path,rows in ((MODEL_FILE,serialized),(ART_FILE,pool.entries)):
        asset_write(path,('# Generated by Tools/three_d/author_baked_food.py; source-specific static drafts.\n'+yaml.safe_dump(rows,sort_keys=False,width=110)).encode('utf-8'))
    actual=bm.load_models(MODEL_FILE)
    for model in actual:
        assert model['placement']=='surface' and model['useEntityRotation']
        bm.glb_bytes(model)  # In-memory structural export only; root owns final global assets.
    note='''# Baked-food source drafts

This batch covers 24 previously unmapped placed prototypes: 76 Redux and 56 classic instances. The 23 solid models share the identical baguette art between FoodBreadBaguette and edible WeaponBaguette. Scope is the selected bread.rsi, pie.rsi and cake.rsi world states only. Every selected source state is one direction and one static frame, with noRot false, zero Sprite offset/rotation and ordinary entity yaw. Models retain entity rotation and use the existing authored surface-placement resolver; no table or support is invented.

Rounded bread crusts have actual volume, flat soles and cut crumb faces. The meat loaf keeps its original spiral cut face; banana bread keeps its pale icing. Baguette scoring is separate pale solid detail on a long rounded loaf. Crostini and garlic bread have crust/crumb thickness and original top decoration. Whole cakes use thick round crumb bodies, frosting and original top filling; birthday cake/slice keep the actual three/one static candles. Pie pans, pastry shells, fillings and scalloped edges are solid parts. Slices use horizontal triangular prisms with separate pastry/filling layers. Pumpkin cream and banana-cream height are explicit inferred 3D forms.

Default Sprite layers are composed in source order with actual named/hex layer tints, including pink clafoutis tin, red cherry filling, blue cake filling and chocolate tint. Dedicated top/cut-face textures preserve every retained RGBA pixel from that default composite. Round cap projection discards only corners outside the physical cap; those discarded pixels and retained hashes are recorded. Original RSI files are not edited. Model reference fields point to CMU14/ThreeD/BakedFoodReferences.rsi, a dedicated generated RSI containing the exact composed 32 by 32 default frames. Its metadata retains all original attribution. These references and the comparisons under generated/review/baked-food show the full default source, not an isolated layer.

Dimensions in Z, hidden crust/crumb depth, undersides, round cap projection and the conversion of visible scoring/candles to solids are inferred. These are drafts, not exact reconstruction proof. Only default world appearance is authored: no food consumption, slicing transition, secret-stash, thrown pie effect, equipped/in-hand animation, or dynamic layer-tint behavior is claimed. Slicing creates separate prototypes; unmodeled result prototypes remain outside this batch. Named source states in exported evidence do not imply a new controller or animation.

All three original RSI metadata files declare CC-BY-SA-3.0. Their full copyright strings and file hashes are in baked-food-source-audit.json. Derived crops and sampled palettes retain that attribution. Preserve original attribution when redistributing these assets.

Regenerate dedicated assets with `python Tools/three_d/author_baked_food.py`; use `--check --skip-context` for a read-only deterministic asset comparison, or `--source-only` for original composite review images. The generator does not run a global exporter, native build, game or server. Context contacts are conservative bounding-box candidates, not proof of actual curved-surface intersections; final assembled/native validation is separate.
'''
    asset_write(NOTES,note.encode('utf-8'))
    if CHECK:
        print(json.dumps({'models':len(actual),'exactSourceMappings':24,'assetFilesChecked':len(WRITTEN),'byteIdentical':True}));return
    if not args.skip_reviews:
        model_reviews(actual,images)
    contexts,context_inputs=([],{}) if args.skip_context else context_checks(actual)
    json_write(GENERATED/'baked-food-source-audit.json',{'schemaVersion':1,'scope':'Default static source layers and saved contexts; no runtime food-state claim.',
               'prototypes':evidence,'counts':{'types':24,'redux':76,'classic':56},'contexts':contexts,'contextInputSha256':context_inputs})
    report={'schemaVersion':1,'assetChecksPass':True,'models':[{'id':m['id'],'sourcePrototypes':m['sourcePrototypes'],'parts':len(m['parts'])} for m in actual],
            'counts':{'models':len(actual),'exactPrototypeMappings':24,'reduxInstances':76,'classicInstances':56,'textures':len(pool.entries),'composedReferenceFrames':len(reference_prototypes)},
            'atlasIndices':[r['atlasIndex'] for r in pool.entries],'sourceCrops':pool.crops,'nativeScalarVector3Count':vector_count,
            'nativeScalarVector2Count':len(actual),'allStaticOneFrameOneDirection':True,'defaultCompositeOnly':True,
            'contextChecks':{'placements':len(contexts),'withSupport':sum(bool(c['support']) for c in contexts),
                             'withoutSupport':sum(not c['support'] for c in contexts),
                             'conservativeContactNeighborCount':sum(bool(n.get('conservativeContactPairs')) for c in contexts for n in c['neighborsWithin0_65'])},
            'writtenAssetsSha256':{p.relative_to(ROOT).as_posix():sha(p) for p in WRITTEN},'generatorSha256':sha(Path(__file__)),
            'sourceAuditSha256':sha(GENERATED/'baked-food-source-audit.json'),
            'limitations':['Hidden depth and undersides are inferred; round-cap UV projection preserves retained pixels, not source screen-space geometry.',
                           'Only default static world appearance is authored; runtime food states, equipped art and dynamic layer colors are outside this batch.',
                           'Conservative context contacts may include curved-shape false positives; native/global scene export validation belongs to the root batch.']}
    json_write(GENERATED/'baked-food-proof.json',report)
    print(json.dumps(report['counts']))


if __name__=='__main__':
    main()
