"""Source-guided wardrobe and solid desk fan drafts; dedicated outputs only."""
import argparse
import copy
import hashlib
import json
import math
from collections import Counter
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageOps
import numpy as np
import yaml

import build_models
import inventory
import layout
import placement
import scene
import sprite_states
import surfaces
from author_wide_machinery import world_parts, contacts, box_bounds

ROOT = Path(__file__).resolve().parents[2]
GEN = ROOT/'Tools/three_d/generated'
BASE = ROOT/'.codex/model-batch-baseline950'
PROTOS = ROOT/'Content.CMU/Resources/ThreeD/Prototypes/World'
MODEL = PROTOS/'garrison_vendor_fans.yml'
ART = PROTOS/'garrison_vendor_fans_art.yml'
TEXTURES = ROOT/'Content.CMU/Resources/Textures/CMU14/ThreeD/Surfaces'
NOTES = ROOT/'Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_VENDOR_FANS.md'
REVIEW = GEN/'review/vendor-fans'
VENDOR_RSI = '_RMC14/Structures/Machines/VendingMachines/ColMarTech/corp_wardrobe.rsi'
FAN_RSI = '_RMC14/Objects/Misc/desk_fan.rsi'
IDS = ['AU14CivilianClothingVendor','RMCDeskFanTan']


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def source(rsi,state,direction=0):
    sheet=Image.open(ROOT/'Resources/Textures'/rsi/(state+'.png')).convert('RGBA')
    x=direction%(sheet.width//32)*32
    y=direction//(sheet.width//32)*32
    return sheet.crop((x,y,x+32,y+32))


class Pool:
    def __init__(self):
        self.entries,self.crops,self.hashes=[],[],{}
        used={e['atlasIndex'] for p in PROTOS.glob('*.yml') if p.name!=ART.name
              for e in (yaml.load(p.read_text(encoding='utf-8-sig'),Loader=yaml.CSafeLoader) or [])
              if e.get('type')=='cmu3DSurface'}
        assert not used.intersection(range(2100,2200))

    def store(self,image,record):
        digest=hashlib.sha256(str(image.size).encode()+image.tobytes()).hexdigest()
        if digest not in self.hashes:
            number=2100+len(self.entries)
            assert number<2200
            uid=f'CMU3DVendorFanSurface{number}'
            image.save(TEXTURES/(uid+'.png'))
            self.hashes[digest]=uid
            self.entries.append(dict(type='cmu3DSurface',id=uid,atlasIndex=number,
                texture=f'/Textures/CMU14/ThreeD/Surfaces/{uid}.png'))
        uid=self.hashes[digest]
        actual=Image.open(TEXTURES/(uid+'.png')).convert('RGBA')
        assert actual.size==image.size and actual.tobytes()==image.tobytes()
        self.crops.append(dict(**record,surface=uid,size=list(image.size),rgbaSha256=hashlib.sha256(image.tobytes()).hexdigest()))
        return uid


def vendor(pool):
    image=source(VENDOR_RSI,'base')
    parts=[]
    # Physical width/depth/height are inferred. A .92 tile width clears the
    # adjacent authored wall trim; every source texel remains in the same crop.
    scale=.05
    def box(label,rect,near,far,color='#584532',**extra):
        x0,y0,x1,y1=rect
        parts.append(dict(label=label,min=[(x0-16)/32*.92,near,(32-y1)*scale],
            max=[(x1-16)/32*.92,far,(32-y0)*scale],color=color,**extra))
    def face(label,rect,near,far=None,pixels=None,region=None):
        pixels=image.crop(rect) if pixels is None else pixels
        uid=pool.store(pixels,dict(design='wardrobe',direction=0,rect=list(rect),region=region or label))
        box(label,rect,near,near+.004 if far is None else far,'#FFFFFF',surface=uid,surfaceAxis='XZ')
    # Open cabinet: real side/back panels and shelves, never one full front box.
    box('left wooden cabinet side',(1,1,3,31),-.272,.28)
    box('right wooden cabinet side',(29,1,31,31),-.272,.28)
    box('rear wooden cabinet wall',(3,1,29,31),.25,.28,'#2D2A27')
    box('cabinet upper cap',(1,1,31,5),-.272,.25)
    box('folded garment upper shelf',(3,8,29,9),-.272,.25,'#8E643A')
    box('hanging garment shelf',(3,13,29,14),-.272,.25,'#8E643A')
    box('cabinet bottom shelf',(1,29,31,31),-.272,.25,'#8E643A')
    box('left foot',(0,31,3,32),-.26,.24,'#2D231A')
    box('right foot',(29,31,32,32),-.26,.24,'#2D231A')
    for label,rect in [('original header and upper rails',(0,1,32,9)),
                       ('original left front rail',(0,9,3,29)),
                       ('original right front rail',(29,9,32,29)),
                       ('original bottom rail and feet',(0,29,32,32)),
                       ('original center shelf edge',(3,13,29,14))]:
        face(label,rect,-.28)
    # Folded garments project above a genuine inset shelf.
    for label,rect in [('folded blue garments',(3,9,13,13)),
                       ('dark shelf recess',(13,9,20,13)),('folded brown garments',(20,9,29,13))]:
        face(label,rect,-.21,.15)
    # Separate six hanging cloth volumes from the dark cabinet recess. Every
    # original pixel is retained in one of the two complementary source layers.
    rect=(3,14,29,29)
    background=image.crop(rect)
    masks=[]
    for left,right in [(3,8),(8,12),(12,16),(16,20),(20,25),(25,29)]:
        piece=image.crop((left,14,right,29))
        for y in range(piece.height):
            for x in range(piece.width):
                pixel=piece.getpixel((x,y))
                # Source dark negative space, rather than an opaque black disc.
                if max(pixel[:3])<=45:
                    piece.putpixel((x,y),(0,0,0,0))
                else:
                    background.putpixel((left-3+x,y),(0,0,0,0))
        masks.append((left,right,piece))
    face('deep original dark recess',rect,.242,.246,pixels=background,region='hanging background')
    for index,(left,right,pixels) in enumerate(masks):
        face(f'hanging garment {index+1}',(left,14,right,29),-.17,.04,pixels=pixels,region='hanging foreground')
    return parts


def fan(pool):
    parts=[]
    brass='#99826A';light='#D2B48C';dark='#3D342A';metal='#999999'
    cx=-.015625
    cz=.515625
    def box(label,center,half,color,**extra):
        parts.append(dict(label=label,min=[v-h for v,h in zip(center,half)],
            max=[v+h for v,h in zip(center,half)],color=color,**extra))
    def pitch(value):
        return (value+90)%180-90
    # Rounded weighted base, original single pedestal and rear motor casing.
    box('weighted tan base',(cx,.055,.0375),(.16,.21,.0375),dark,shape='Ellipsoid')
    box('raised base upper casting',(cx,.035,.063),(.13,.17,.035),brass,shape='Ellipsoid')
    box('flat load-bearing foot',(cx,.045,.012),(.125,.165,.012),dark)
    box('pedestal stem',(cx,.065,.225),(.033,.04,.15),brass,shape='CylinderZ')
    box('pedestal top collar',(cx,.065,.400),(.055,.06,.045),light,shape='Ellipsoid')
    box('rear motor body',(cx,.115,cz),(.091,.13,.095),brass,shape='CylinderY')
    box('rear motor cover',(cx,.246,cz),(.075,.018,.078),light,shape='CylinderY')
    box('shaft housing',(cx,-.005,cz),(.052,.095,.052),dark,shape='CylinderY')
    # Two true annular cages, linked in depth. No filled plate behind the rotor.
    for label,yy,radius,thickness in [('front',-.117,.282,.012),('rear',-.043,.270,.010)]:
        for index in range(16):
            theta=math.tau*index/16
            color=light if math.sin(theta)>.3 else brass if math.sin(theta)>-.5 else dark
            box(f'{label} cage rim {index+1}',(cx+radius*math.cos(theta),yy,cz+radius*math.sin(theta)),
                (thickness,.012,radius*math.tan(math.pi/16)+.001),color,pitch=pitch(math.degrees(theta)))
    for index in range(4):
        theta=math.tau*index/4
        box(f'cage depth bridge {index+1}',(cx+.276*math.cos(theta),-.08,cz+.276*math.sin(theta)),
            (.013,.035,.013),brass)
    for index in range(8):
        theta=math.tau*index/8
        radius=.182
        box(f'front protective spoke {index+1}',(cx+radius*math.cos(theta),-.132,cz+radius*math.sin(theta)),
            (.004,.004,.099),dark,pitch=pitch(math.degrees(theta)-90))
    for index in range(4):
        theta=math.tau*index/4+math.pi/4
        radius=.176
        box(f'rear cage support {index+1}',(cx+radius*math.cos(theta),-.027,cz+radius*math.sin(theta)),
            (.006,.006,.1),dark,pitch=pitch(math.degrees(theta)-90))
    # Four source-visible stationary blades, with rounded oblique geometry.
    for index in range(4):
        theta=math.tau*index/4+.35
        box(f'stationary rotor blade {index+1}',(cx+.173*math.cos(theta),-.083,cz+.173*math.sin(theta)),
            (.053,.014,.105),metal,shape='Ellipsoid',pitch=pitch(math.degrees(theta)-65))
    box('front bronze rotor hub',(cx,-.115,cz),(.091,.025,.091),brass,shape='CylinderY')
    def detail(direction,rect,label,center,half,axis,flip=False,shape='Box'):
        crop=source(FAN_RSI,'deskfan',direction).crop(rect)
        if flip:crop=ImageOps.mirror(crop)
        uid=pool.store(crop,dict(design='fan',direction=direction,rect=list(rect),region=label,flip=flip))
        box(label,center,half,'#FFFFFF',shape=shape,surface=uid,surfaceAxis=axis)
    detail(0,(13,10,18,15),'original patterned front hub',(cx,-.142,cz),(.0625,.003,.0625),'XZ')
    detail(1,(14,10,21,16),'original rear motor vents',(cx,.266,cz),(.055,.003,.0475),'XZ',flip=True)
    detail(2,(9,10,19,14),'original left Weyland-Yutani motor label',(cx-.093,.135,cz+.008),(.003,.115,.044),'YZ')
    detail(3,(14,10,24,14),'original right Weyland-Yutani motor label',(cx+.093,.135,cz+.008),(.003,.115,.044),'YZ',flip=True)
    return parts


def serialize(data):
    if isinstance(data,list):return [serialize(v) for v in data]
    if isinstance(data,dict):return {k:', '.join(f'{n:.9f}' for n in v) if k in ('min','max') else serialize(v) for k,v in data.items()}
    return data


def author():
    REVIEW.mkdir(parents=True,exist_ok=True)
    pool=Pool()
    vp=vendor(pool)
    models=[dict(type='cmu3DModel',id='CMU3DCivilianClothingWardrobe',label='Civilian clothing wardrobe',status='draft',
        sourcePrototypes=[IDS[0]],referencePrototype=IDS[0],referenceRsi=VENDOR_RSI,referenceState='base',
        sourceDirections=1,faceAwayFromWall=True,faceAwayFromWindows=True,placement='floor',groundOffset='0,0',
        description='Open wooden wardrobe with real shelf and side construction, separate folded and hanging garments, exact source crops and inferred physical depth/height. Existing source-aware vendor aisle-facing rule; saved transforms and vending UI unchanged. Static base only. See SOURCES_VENDOR_FANS.md.',parts=vp)]
    fp=fan(pool)
    names=['South','North','East','West']
    ids=['CMU3DTanDeskFan'+n for n in names]
    for direction,name in enumerate(names):
        models.append(dict(type='cmu3DModel',id=ids[direction],label='Tan desk fan / '+name,status='draft',
            sourcePrototypes=[IDS[1]] if direction==0 else [],referencePrototype=IDS[1],referenceRsi=FAN_RSI,
            referenceState='deskfan',referenceDirection=direction,sourceDirections=4,directionalModels=ids,
            sourceSpriteOffset='0,0',sourceSpriteRotates=True,useEntityRotation=True,yawOffset=0,
            placement='surface',groundOffset='0,0',
            backWallMountTargets=['RMCWallHybrisa','RMCWallSPPGreyReinforced','RMCWallPrisonReinforced','RMCWallStrata'],
            description='Source-colored physical fan: two hollow segmented cages, protective spokes, four static curved blades, motor with original logos/vents, pedestal and weighted base. Depth/cage spacing and solid silhouette are inferred. No source spinning animation exists. Saved yaw and exact table support retained. See SOURCES_VENDOR_FANS.md.',
            parts=copy.deepcopy(fp),spriteStates={'deskfan':dict(frames=[dict(parts=copy.deepcopy(fp))],delays=[1])}))
    MODEL.write_text('# Source-guided wardrobe and desk fan drafts.\n'+yaml.safe_dump(serialize(models),sort_keys=False,width=110),encoding='utf-8')
    ART.write_text('# Original source crops, CC-BY-SA-3.0; see SOURCES_VENDOR_FANS.md.\n'+yaml.safe_dump(pool.entries,sort_keys=False),encoding='utf-8')
    surfaces.load_surfaces.cache_clear()
    loaded=build_models.load_models(MODEL)
    source_references=[]
    for rsi,state,count in [(VENDOR_RSI,'base',1),(FAN_RSI,'deskfan',4)]:
        for direction in range(count):
            image=source(rsi,state,direction)
            path=REVIEW/f'source-{state}-{direction}.png'
            image.save(path)
            assert Image.open(path).convert('RGBA').tobytes()==image.tobytes()
            source_references.append(dict(rsi=rsi,state=state,direction=direction,frames=1,
                sourcePng=path.relative_to(ROOT).as_posix(),rgbaSha256=hashlib.sha256(image.tobytes()).hexdigest()))
    # Reassemble the entire wardrobe from the actual on-disk source partitions.
    reconstructed=Image.new('RGBA',(32,32))
    for crop in pool.crops:
        if crop['design']!='wardrobe':continue
        image=Image.open(TEXTURES/(crop['surface']+'.png')).convert('RGBA')
        reconstructed.alpha_composite(image,crop['rect'][:2])
    assert reconstructed.tobytes()==source(VENDOR_RSI,'base').tobytes()
    palette={p[:3] for p in Image.open(ROOT/'Resources/Textures'/FAN_RSI/'deskfan.png').convert('RGBA').get_flattened_data() if p[3]}
    for p in fp:
        if p.get('surface'):continue
        assert tuple(bytes.fromhex(p['color'][1:])) in palette
    proof=dict(status='draft assets ready; focused context pending',schemaVersion=1,
        models=[dict(id=m['id'],parts=len(m['parts']),sourcePrototypes=m['sourcePrototypes']) for m in loaded],
        sourceReferences=source_references,sourceCrops=pool.crops,wardrobeWrittenRgbaReassembled=True,
        fanSourcePaletteVerified=True,fanFullReferencesExact=True,
        fanGeometryNotPixelIdentical='Curved blades and hollow cage interpret the source; dark interior pixels are not painted into an opaque disc.',
        textureCount=len(pool.entries),atlasIndices=[e['atlasIndex'] for e in pool.entries],
        sourceInputSha256={p.relative_to(ROOT).as_posix():sha(p) for p in [ROOT/'Resources/Textures'/rsi/file
            for rsi,files in [(VENDOR_RSI,['meta.json','base.png']),(FAN_RSI,['meta.json','deskfan.png'])] for file in files]})
    (GEN/'vendor-fans-proof.json').write_text(json.dumps(proof,indent=2)+'\n')
    print(json.dumps(dict(models=5,parts=[len(m['parts']) for m in loaded],textures=len(pool.entries))),flush=True)
    return loaded,proof


def cards(models):
    font=ImageFont.load_default(size=17)
    for m in models:
        c=Image.new('RGB',(1320,540),'#18232C');d=ImageDraw.Draw(c)
        d.text((12,10),m['label']+' / original source and four physical views',fill='white',font=font)
        d.text((12,37),'Draft: original source colors/details; depth and hidden geometry inferred. Static source, no invented animation.',fill='#BCD0DB',font=ImageFont.load_default(size=14))
        src=source(m['referenceRsi'],m['referenceState'],m.get('referenceDirection',0)).resize((230,230),Image.Resampling.NEAREST)
        c.paste(src,(10,130),src)
        scale=235 if m['sourceDirections']==4 else 190
        for i,yaw in enumerate([-math.pi/2,-math.pi/3,math.pi/3,math.pi]):
            p=build_models.render_model(m,(270,420),yaw,.25,pixels_per_unit=scale,screen_origin=(135,355))
            c.paste(p,(240+i*270,75))
        c.save(REVIEW/(m['id']+'.png'))


def occupied(part, points):
    """Native analytic solid and source-alpha witness, not an AABB verdict."""
    low,high=np.array(part['min']),np.array(part['max'])
    q=points-(low+high)/2
    yaw,pitch=map(math.radians,(part.get('yaw',0),part.get('pitch',0)))
    c,s=math.cos(yaw),math.sin(yaw)
    q=np.column_stack((c*q[:,0]+s*q[:,1],-s*q[:,0]+c*q[:,1],q[:,2]))
    c,s=math.cos(pitch),math.sin(pitch)
    q=np.column_stack((c*q[:,0]+s*q[:,2],q[:,1],-s*q[:,0]+c*q[:,2]))/(high-low)*2
    valid=np.all(np.abs(q)<1,axis=1)
    shape=part.get('shape','Box')
    if shape=='Ellipsoid':valid &= np.sum(q*q,axis=1)<1
    elif shape.startswith('Cylinder'):
        axis='XYZ'.index(shape[-1]);valid &= np.sum(q[:,[i for i in range(3) if i!=axis]]**2,axis=1)<1
    elif shape in ('WedgeY','WedgeYReverse'):valid &= q[:,2] <= q[:,1]*(-1 if shape.endswith('Reverse') else 1)
    elif shape!='Box':return None
    if part.get('surface'):
        im=surfaces.load_surfaces()[part['surface']]['image']
        axis=part.get('surfaceAxis','XZ')
        u=(q[:,0]+1)/2 if axis!='YZ' else (1-q[:,1])/2
        v=(1-q[:,1])/2 if axis=='XY' else (1-q[:,2])/2
        if part.get('surfaceFlipU'):u=1-u
        x=np.clip((u*im.width).astype(int),0,im.width-1);y=np.clip((v*im.height).astype(int),0,im.height-1)
        valid &= np.asarray(im)[y,x,3]>=128
    return valid


def contact_witnesses(hits, first, second):
    for hit in hits:
        a=next(p for p in first if p['label']==hit['part'])
        b=next(p for p in second if p['label']==hit['neighborPart'])
        lo,hi=box_bounds(a);blo,bhi=box_bounds(b)
        lo=np.maximum(lo,blo);hi=np.minimum(hi,bhi)
        xyz=np.meshgrid(*[np.linspace(l+(h-l)*.03,h-(h-l)*.03,9) for l,h in zip(lo,hi)],indexing='ij')
        points=np.column_stack([axis.ravel() for axis in xyz])
        av,bv=occupied(a,points),occupied(b,points)
        if av is None or bv is None:
            hit['solidTest']='unsupported neighbor shape; AABB only';continue
        both=av&bv
        hit['solidTest']='positive solid and source-alpha witness' if np.any(both) else 'no witness among 729 samples; not an exact separation proof'
        if np.any(both):hit['solidWitness']=points[np.flatnonzero(both)[0]].tolist()


def context(models,proof):
    index,_,_=inventory.load_prototypes(ROOT)
    resolver=inventory.Resolver(index['entity'])
    defaults={}
    def default(proto):
        if proto not in defaults:
            try:defaults[proto]=inventory.component_map(resolver.resolve(proto))
            except (ValueError,KeyError):defaults[proto]={}
        return defaults[proto]
    lib={m['id']:m for m in json.loads((BASE/'models.json').read_text())['models']}
    lib.update({m['id']:m for m in models})
    mappings={p:m for m in models for p in m['sourcePrototypes']}
    evidence=[];input_hashes={};font=ImageFont.load_default(size=16)
    for spec in json.loads((BASE/'scenes.json').read_text()):
        path=BASE/spec['file'];doc=json.loads(path.read_text())
        targets=[i for i in doc['instances'] if i['prototype'] in IDS]
        if not targets:continue
        input_hashes[path.relative_to(ROOT).as_posix()]=sha(path)
        map_path=ROOT/doc['map']['path']
        _,records=scene.read_map(map_path)
        for record in records.values():default(record['prototype'])
        transforms=scene.WorldTransforms(records,defaults)
        for entity in targets:
            m=mappings[entity['prototype']]
            direction=[0,2,1,3][round(entity['yaw']/(math.pi/2))%4] if m['sourceDirections']==4 else 0
            uid=m.get('directionalModels',[m['id']])[direction]
            entity.update(modelId=uid,matchKind='exact',renderYaw=entity['yaw'])
        # Existing production layout, restricted to our target instances. Baseline
        # neighboring geometry and its source transforms stay immutable.
        layout.resolve_layout(targets,list(lib.values()),records,defaults,transforms)
        placement.resolve_placements(doc['instances'],list(lib.values()),doc['geometryVariants'])
        for entity in targets:
            m=lib[entity['modelId']]
            raw=records[entity['id']]
            state_reason=(sprite_states.saved_pose(m,default(entity['prototype']),raw['components'],scene.normalize_tint)
                          if entity['prototype']==IDS[1] else ('base',None))
            assert state_reason[1] is None
            sprite={**default(entity['prototype']).get('Sprite',{}),**raw['components'].get('Sprite',{})}
            own=world_parts(m['parts'],entity['position'],entity['renderYaw'],entity.get('renderOffset',[0,0,0]))
            assembled=list(own);neighbors=[];unknown=[]
            for n in doc['instances']:
                if n['id']==entity['id'] or sum((a-b)**2 for a,b in zip(n['position'][:2],entity['position'][:2]))>1.55**2:continue
                nm=lib.get(n.get('modelId'))
                if nm is None:
                    unknown.append(dict(id=n['id'],prototype=n['prototype']));continue
                other=world_parts(doc['geometryVariants'].get(n.get('geometryKey'),nm['parts']),n['position'],
                    n.get('renderYaw',n['yaw']),n.get('renderOffset',[0,0,0]))
                hits,gap=contacts(own,other)
                contact_witnesses(hits,own,other)
                neighbors.append(dict(id=n['id'],prototype=n['prototype'],contacts=hits,minimumAabbSeparation=gap))
                assembled.extend(other)
            record=dict(variant=spec['variant'],level=spec['level'],id=entity['id'],prototype=entity['prototype'],
                modelId=m['id'],position=entity['position'],savedYaw=entity['yaw'],renderYaw=entity['renderYaw'],
                renderOffset=entity.get('renderOffset',[0,0,0]),support=entity.get('support'),sourceDirection=m.get('referenceDirection',0),
                sprite=sprite,savedComponents=raw['components'],sourceState=state_reason[0],sourceContractAccepted=True,
                modeledNeighbors=neighbors,unknownNeighbors=unknown,contactPairs=sum(len(n['contacts']) for n in neighbors))
            evidence.append(record)
            for p in assembled:
                for key in ('min','max'):
                    p[key][0]-=entity['position'][0];p[key][1]-=entity['position'][1]
            c=Image.new('RGB',(1200,620),'#18232C');d=ImageDraw.Draw(c)
            d.text((12,10),f'Redux {spec["level"]:+d} / UID {entity["id"]} / {entity["prototype"]}',fill='white',font=font)
            d.text((12,35),f'Saved yaw {math.degrees(entity["yaw"]):.0f}; render yaw {math.degrees(entity["renderYaw"]):.0f}; support {entity.get("support")}',fill='#BCD0DB',font=font)
            for i,yaw in enumerate([-math.pi/2+.25,math.pi/2+.25]):
                panel=build_models.render_model(dict(parts=assembled),(590,505),yaw,.55,pixels_per_unit=135,screen_origin=(295,425))
                c.paste(panel,(600*i,70))
            d.text((12,590),f'{record["contactPairs"]} conservative part contacts; {len(unknown)} unmodeled neighbors. Original pivots unchanged.',fill='#BCD0DB',font=font)
            c.save(REVIEW/f'context-{spec["level"]}-{entity["id"]}.png')
    assert len(evidence)==35
    fans=[e for e in evidence if e['prototype']==IDS[1]]
    assert len(fans)==16 and all(e['support'] for e in fans)
    proof.update(status='draft assets and focused context checked; parent global/native checks pending',contexts=evidence,
        placementCounts=dict(Counter(e['prototype'] for e in evidence)),inputScenesSha256=input_hashes,
        sources={p:dict(sprite=default(p).get('Sprite'),transform=default(p).get('Transform'),
                       source=resolver.resolve(p)['_source'],components=sorted(default(p))) for p in IDS})
    proof['writtenAssetSha256']={p.relative_to(ROOT).as_posix():sha(p) for p in [MODEL,ART,NOTES,
        *[TEXTURES/(e['id']+'.png') for e in yaml.safe_load(ART.read_text())]]}
    proof['generatorSha256']=sha(Path(__file__))
    (GEN/'vendor-fans-proof.json').write_text(json.dumps(proof,indent=2)+'\n')
    print(json.dumps(dict(placements=35,fansSupported=16,contactPairs=sum(e['contactPairs'] for e in evidence))),flush=True)


def notes():
    NOTES.write_text('''# Civilian wardrobe and tan desk fan drafts

Two exact source prototypes cover 35 Redux placements: AU14CivilianClothingVendor (19) and RMCDeskFanTan (16). No classic placements. These are source-guided drafts, not approved finished assets.

The wardrobe uses `_RMC14/Structures/Machines/VendingMachines/ColMarTech/corp_wardrobe.rsi`, static 32 x 32 `base`, one layer mapped to the existing vendor visual keys. Its Transform.noRot=true does not mean Sprite.noRot=true. It has anchored static machine physics and unchanged .9 x .9 tile fixtures. All saved wardrobe yaw values are zero. The existing vendor faceAwayFromWall/Windows presentation rule keeps its physical open front toward the aisle without moving or rotating the source entity. The original source spans 32 pixels. The physical width is .92 tiles, an explicit 8% context-fit inference that clears the existing Hybrisa wall trim while retaining all source texels. Physical height 1.55 and depth .56 tiles are also inferred; source pivots and fixtures are unchanged.

The wardrobe has real wooden side/back panels, cap, shelves and feet. Folded garments sit in an upper recess, and six separate thin hanging cloth volumes stand in front of the dark back. Written source partitions reconstruct every original wardrobe pixel exactly. This source layering is an inferred depth interpretation, not cloth simulation or arbitrary-camera pixel equivalence. All 19 wardrobes have no positive wall-volume intrusion; the two headers at surface UIDs116/118 meet an adjacent horizontal panel seam exactly, so these are flush contacts rather than positive-clearance claims.

CMAutomatedVendor owns the existing inventory and UI. SharedCMAutomatedVendorSystem attempts TryFlick on vending, but this resolved prototype supplies neither BaseSprite nor AnimationSprite. Both are nullable and SharedRMCAnimationSystem returns immediately when either is null. No vending, door-opening, power or clothing-removal animation is invented. AmbientOnPowered/AmbientSound remain existing gameplay components; no audio behavior is changed.

The fan uses `_RMC14/Objects/Misc/desk_fan.rsi`, `deskfan`, four static 32 x 32 directions in South/North/East/West order. The separate static `icon` resource is not the world state. There is no spinning source sequence and no fan-specific state/controller. Generic Animateable and Rotatable item components do not imply a rotor animation. The original item remains movable, with unchanged dynamic physics and .5 x .5 fixture. All current saved yaws are cardinal; the model uses existing rotating-source selection and exact source frame zero. Four reciprocal models retain those source references and a coherent body rotated once.

Fan geometry includes two genuine segmented annular cages, depth bridges and protective spokes, four rounded stationary blades, a cylindrical motor and hub, original side logo and rear vent pixels, pedestal, and a weighted base. Source palette and exact detail crops are checked. Full four-direction source-reference PNGs are preserved. Dark source interior pixels are interpreted as the negative space around blades and cage; they are not an opaque painted disc. Consequently the fan is deliberately not claimed to reassemble every source pixel as a flat 3D silhouette. Cage depth, spokes, hidden motor construction, blade curvature and base depth are inferred. All 16 saved fans use existing authored table support. The four directional records share the same bounded backWallMountTargets list for Hybrisa, SPP reinforced, prison reinforced and Strata walls. The existing rear-wall clearance rule shifts only presentation toward the saved front before support lookup; source pivots and source yaw remain unchanged. Its use is restricted by the source adapter to this static rotating-source surface composition. Unsupported future sprite effects or states remain subject to the existing source adapter guards.

Review and evidence: `Tools/three_d/generated/vendor-fans-proof.json` and `Tools/three_d/generated/review/vendor-fans/`. Context is based on the immutable 950-model snapshots in `.codex/model-batch-baseline950` plus this complete batch, using saved source transforms, actual model-local neighbor variants and offsets exactly once. Conservative AABB contacts include cage/curved-shape empty regions and texture alpha; they are not automatic claims of visible solid intersection. Unknown neighbors remain explicit. Parent work owns shared exports, native admission and runtime checks.

Both sources are CC-BY-SA-3.0. Fan attribution: Made by SharkSnake98 on GitHub. Wardrobe attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/a50253802b9d56391f7b857684e345119a207f43/icons/obj/structures/machinery/vending.dmi. Preserve these source attributions with redistributed derived artwork.

The rear motor cover and original vent crop sit .007 tile closer to the motor than the first provisional draft, clearing the SPP raised wall trim without changing the source crop, fan cage, pedestal or base. A Strata side seam still contacts fan UID4118 after its .862 table lift: the original rear-wall helper measures before the support elevation, and shortening the rear cap reduces its computed offset too. This exact mounting limitation is retained in the proof. Four co-centered wardrobe/window assemblies and the fan/TV, fan/shutter, fan/lamp and fan/window arrangements also remain explicit context defects; this batch does not alter those neighboring assets or source placements. Small paper, clipboard and table-relief contacts are recorded separately. A sampled analytic-solid/source-alpha witness proves an intersection when found; its absence is not an exact separation proof.

Reproduce dedicated assets with `Tools/three_d/author_vendor_fans.py`; `--reviews-only` leaves model/art/textures unchanged while rebuilding source notes and focused context. `--finalize-proof` rechecks the written YAML and textures and summarizes measured contacts. `--check-assets` regenerates only in `.codex/vendor-fans-regeneration-check` and requires byte-identical production YAML/art/PNG outputs. This tool does not run global exports, builds, the game or a server.
''',encoding='utf-8')


def finalize_proof():
    models=build_models.load_models(MODEL)
    proof=json.loads((GEN/'vendor-fans-proof.json').read_text())
    written=yaml.safe_load(MODEL.read_text())
    assert all(isinstance(p[k],str) for m in written for p in m['parts'] for k in ('min','max'))
    pose_checks=[]
    for m in models:
        meta=json.loads((ROOT/'Resources/Textures'/m['referenceRsi']/'meta.json').read_text())
        state=next(s for s in meta['states'] if s['name']==m['referenceState'])
        direction=m.get('referenceDirection',0)
        assert state.get('directions',1)==m['sourceDirections']
        assert len(state.get('delays',[[1]]*m['sourceDirections'])[direction])==1
        if m.get('spriteStates'):
            sprite_states.validate_source(m,lambda v:ROOT/'Resources/Textures'/v)
            assert m['spriteStates']['deskfan']['frames'][0]['parts']==m['parts']
        pose_checks.append(dict(modelId=m['id'],state=m['referenceState'],direction=direction,
            frames=1,parts=len(m['parts']),writtenStaticFrameVerified=True))
    for crop in proof['sourceCrops']:
        im=Image.open(TEXTURES/(crop['surface']+'.png')).convert('RGBA')
        assert hashlib.sha256(im.tobytes()).hexdigest()==crop['rgbaSha256']
    groups=[]
    for e in proof['contexts']:
        for n in e['modeledNeighbors']:
            if not n['contacts']:continue
            p=n['prototype'];confirmed=sum('solidWitness' in h for h in n['contacts'])
            if 'Carpet' in p:classification='minor floor covering / cabinet feet'
            elif 'Table' in p:classification='minor support surface grain or rivets'
            elif p in ('CMPaper','CMClipboard'):classification='small tabletop clutter contact'
            elif p=='CMUMonitorCameraColonyCMB':classification='small neighboring console detail contact'
            else:classification='unresolved context assembly intrusion' if confirmed else 'unresolved conservative contact'
            groups.append(dict(level=e['level'],id=e['id'],neighborId=n['id'],neighborPrototype=p,
                classification=classification,partPairs=len(n['contacts']),positiveSolidWitnesses=confirmed))
    proof.update(writtenFrameChecks=pose_checks,writtenScalarVectorsVerified=True,
        contactClassification=groups,solidWitnessMethod='9 x 9 x 9 interior samples in each intersecting AABB; inverse yaw/pitch, analytic Box/Cylinder/Ellipsoid/Wedge and original PNG alpha >=128. Positive witnesses establish native analytic overlap; no witness is not a separation proof.',
        contextFitInferences=dict(wardrobeWidth=.92,wardrobeSourceWidthCompressionPercent=8,fanRearCapDepthReduction=.007),
        wardrobeWallContacts=[dict(level=e['level'],id=e['id'],
            minimumAabbSeparation=min(n['minimumAabbSeparation'] for n in e['modeledNeighbors'] if 'Wall' in n['prototype']),
            remainingPartContacts=sum(len(n['contacts']) for n in e['modeledNeighbors'] if 'Wall' in n['prototype']))
            for e in proof['contexts'] if e['prototype']==IDS[0]],
        actualRearWallClearances=[dict(level=e['level'],id=e['id'],renderOffset=e['renderOffset'],
            neighborId=n['id'],minimumAabbSeparation=n['minimumAabbSeparation'],remainingPartContacts=len(n['contacts']))
            for e in proof['contexts'] if e['prototype']==IDS[1] and any(abs(v)>1e-6 for v in e['renderOffset'][:2])
            for n in e['modeledNeighbors'] if n['id'] in (23221,8407,56374,57156)],
        limitations=['All models remain drafts; depth and hidden geometry are inferred.',
            'Four co-centered wardrobe/window assemblies and fan TV/shutter/lamp/window arrangements remain unresolved; see contactClassification.',
            'No fan spinning or vending animation invented; unsupported live appearance remains subject to native source guards.',
            'Current context proof uses 950-model frozen neighbors plus this complete family; parent final exports/native admission checked separately.'])
    proof['allRearWallsClear']=all(e['remainingPartContacts']==0 and e['minimumAabbSeparation']>0 for e in proof['actualRearWallClearances'])
    proof['writtenAssetSha256']={p.relative_to(ROOT).as_posix():sha(p) for p in [MODEL,ART,NOTES,
        *[TEXTURES/(e['id']+'.png') for e in yaml.safe_load(ART.read_text())]]}
    proof['generatorSha256']=sha(Path(__file__))
    (GEN/'vendor-fans-proof.json').write_text(json.dumps(proof,indent=2)+'\n')
    print(json.dumps(dict(writtenFrames=len(pose_checks),contacts=len(groups),positiveSolidWitnesses=sum(g['positiveSolidWitnesses'] for g in groups),rearWallsClear=sum(e['remainingPartContacts']==0 for e in proof['actualRearWallClearances']))))


def check_assets():
    global MODEL,ART,TEXTURES,REVIEW,GEN
    originals=[MODEL,ART,*[TEXTURES/(e['id']+'.png') for e in yaml.safe_load(ART.read_text())]]
    stage=ROOT/'.codex/vendor-fans-regeneration-check'
    MODEL,ART=stage/MODEL.name,stage/ART.name
    TEXTURES=stage/'textures';REVIEW=stage/'review';GEN=stage/'proof'
    for p in (stage,TEXTURES,REVIEW,GEN):p.mkdir(parents=True,exist_ok=True)
    author()
    comparisons=[]
    for p in originals:
        candidate=(TEXTURES if p.suffix=='.png' else stage)/p.name
        assert candidate.read_bytes()==p.read_bytes(),p
        comparisons.append(dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p)))
    (stage/'comparison.json').write_text(json.dumps(dict(byteIdentical=len(comparisons),files=comparisons),indent=2)+'\n')
    print(json.dumps(dict(byteIdenticalAssets=len(comparisons))))


def export_parity(manifest_name):
    proof=json.loads((GEN/'vendor-fans-proof.json').read_text())
    manifest=GEN/manifest_name
    models={m['id']:m for m in build_models.load_models(MODEL)}
    library={m['id']:m for m in json.loads((GEN/'models.json').read_text())['models']}
    expected={(e['variant'],e['level'],e['id']):e for e in proof['contexts']}
    checks=[];inputs={}
    def same(a,b):
        if isinstance(a,(int,float)) and isinstance(b,(int,float)):return abs(a-b)<1e-7
        if isinstance(a,(list,tuple)) and isinstance(b,(list,tuple)):return len(a)==len(b) and all(same(x,y) for x,y in zip(a,b))
        if isinstance(a,dict) and isinstance(b,dict):return set(a)==set(b) and all(same(a[k],b[k]) for k in a)
        return a==b
    for spec in json.loads(manifest.read_text()):
        path=GEN/spec['file'];doc=json.loads(path.read_text());inputs[spec['file']]=sha(path)
        for entity in doc['instances']:
            if entity['prototype'] not in IDS:continue
            e=expected[(spec['variant'],spec['level'],entity['id'])]
            for key in ('position','modelId','renderYaw','support'):
                assert same(entity.get(key),e.get(key)),(entity['id'],key,entity.get(key),e.get(key))
            assert same(entity['yaw'],e['savedYaw'])
            assert same(entity.get('renderOffset',[0,0,0]),e['renderOffset'])
            assert entity['matchKind']=='exact'
            m=models[entity['modelId']]
            actual=doc['geometryVariants'].get(entity.get('geometryKey'),library[m['id']]['parts'])
            assert same(actual,m['parts']),(entity['id'],'geometry')
            if entity['prototype']==IDS[1]:
                assert entity['spriteState']=='deskfan' and entity['spriteFrame']==0
                assert entity['sourceDirection']==e['sourceDirection']
            checks.append(dict(variant=spec['variant'],level=spec['level'],id=e['id'],modelId=m['id'],
                sourceTransformUnchanged=True,renderYawOffsetSupportEqual=True,exportedGeometryEqual=True,
                sourceDirection=e['sourceDirection'],staticFrame=0))
    assert len(checks)==len(expected)==35
    for uid,m in models.items():assert same(library[uid]['parts'],m['parts'])
    regeneration=json.loads((ROOT/'.codex/vendor-fans-regeneration-check/comparison.json').read_text())
    assert all(sha(ROOT/e['path'])==e['sha256'] for e in regeneration['files'])
    proof.update(finalSceneParity=checks,finalSceneInputsSha256=inputs,
        finalSceneManifest=dict(path=manifest.relative_to(ROOT).as_posix(),sha256=sha(manifest)),
        deterministicRegeneration=regeneration,
        finalExportedModelCount=len(library),status='written assets, all 35 source/support placements and final exports verified; recorded context defects remain')
    proof['generatorSha256']=sha(Path(__file__))
    (GEN/'vendor-fans-proof.json').write_text(json.dumps(proof,indent=2)+'\n')
    print(json.dumps(dict(finalSceneParity=len(checks),byteIdenticalAssets=regeneration['byteIdentical'],proofSha256=sha(GEN/'vendor-fans-proof.json'))))


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--reviews-only',action='store_true')
    parser.add_argument('--finalize-proof',action='store_true');parser.add_argument('--check-assets',action='store_true')
    parser.add_argument('--export-parity');args=parser.parse_args()
    if args.export_parity:export_parity(args.export_parity);return
    if args.check_assets:check_assets();return
    if args.finalize_proof:notes();finalize_proof();return
    if args.reviews_only:
        models=build_models.load_models(MODEL);proof=json.loads((GEN/'vendor-fans-proof.json').read_text())
    else:models,proof=author()
    notes();cards(models);context(models,proof);finalize_proof()


if __name__=='__main__':main()
