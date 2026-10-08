"""Reproduce seven source-owned cash/cutlery drafts; no global build or export."""
from __future__ import annotations
import argparse
from collections import Counter
from copy import deepcopy
from io import BytesIO
import hashlib
import json
import math
from pathlib import Path
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont
import yaml

ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Tools/three_d'))
import build_models as bm
import surfaces
import sprite_states
import scene
from placement import resolve_placements
from author_wide_machinery import world_parts, contacts

GEN=ROOT/'Tools/three_d/generated'
REVIEW=GEN/'review/cash-cutlery'
MODELS=ROOT/'Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_cash_cutlery.yml'
ART=MODELS.with_name('garrison_cash_cutlery_art.yml')
TEXTURES=ROOT/'Content.CMU/Resources/Textures/CMU14/ThreeD/Surfaces'
NOTES=ROOT/'Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_CASH_CUTLERY.md'
BASELINE=ROOT/'.codex/model-batch-baseline950'
CASH='_RMC14/Objects/Misc/spacecash.rsi'
FORK='_RMC14/Objects/Tools/Kitchen/fork.rsi'
STATES=['spacecash']+['spacecash_'+str(n) for n in (10,20,50,100,200,500,1000)]
IDS=['RMCSpaceCash'+str(n) for n in (1,10,20,100,1000)]+['RMCFork','RMCForkPlastic']
CHECK=False
WRITTEN=[]

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def write(path,data):
    if CHECK: assert path.read_bytes()==data, f'Non-deterministic asset: {path}'
    else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
    WRITTEN.append(path)
def json_write(path,value):
    path.parent.mkdir(parents=True,exist_ok=True);path.write_text(json.dumps(value,indent=2)+'\n',encoding='utf-8')
def png(image):
    data=BytesIO();image.save(data,format='PNG');return data.getvalue()
def image(rsi,state):return Image.open(bm.resource_file(rsi)/(state+'.png')).convert('RGBA')

class Pool:
    def __init__(self):
        self.rows=[];self.evidence=[];self.cache={}
        for p in MODELS.parent.glob('*.yml'):
            if p==ART:continue
            for r in yaml.load(p.read_text(encoding='utf-8-sig'),Loader=yaml.CSafeLoader) or []:
                assert r.get('type')!='cmu3DSurface' or not 2200<=r['atlasIndex']<=2299,f'Atlas conflict: {p}'
    def crop(self,rsi,state,rect,purpose):
        pixels=image(rsi,state).crop(rect)
        signature=hashlib.sha256(str(pixels.size).encode()+pixels.tobytes()).hexdigest()
        if signature not in self.cache:
            index=2200+len(self.rows);assert index<2300
            uid=f'CMU3DCashCutlerySurface{index}'
            write(TEXTURES/(uid+'.png'),png(pixels))
            self.rows.append(dict(type='cmu3DSurface',id=uid,atlasIndex=index,texture=f'/Textures/CMU14/ThreeD/Surfaces/{uid}.png'))
            self.cache[signature]=uid
        uid=self.cache[signature]
        actual=Image.open(TEXTURES/(uid+'.png')).convert('RGBA')
        assert actual.size==pixels.size and actual.tobytes()==pixels.tobytes()
        self.evidence.append(dict(rsi=rsi,state=state,rect=list(rect),surface=uid,purpose=purpose,
                                 rgbaSha256=hashlib.sha256(pixels.tobytes()).hexdigest(),pixels=pixels.width*pixels.height,
                                 sourceSha256=sha(bm.resource_file(rsi)/(state+'.png'))))
        return uid

def box(label,rect,z0,z1,color='#FFFFFF',surface=None):
    x0,y0,x1,y1=rect
    part=dict(label=label,min=[(x0-16)/32,(16-y1)/32,z0],max=[(x1-16)/32,(16-y0)/32,z1],color=color)
    if surface:part.update(surface=surface,surfaceAxis='XY')
    return part

def rectangles(field):
    """Disjoint maximal rectangles by authored height, never by similar colors."""
    work=field.copy();out=[]
    for y in range(32):
        for x in range(32):
            value=work[y,x]
            if not value:continue
            right=x+1
            while right<32 and work[y,right]==value:right+=1
            bottom=y+1
            while bottom<32 and np.all(work[bottom,x:right]==value):bottom+=1
            out.append(((x,y,right,bottom),int(value)));work[y:bottom,x:right]=0
    return out

def cash_geometry(state,pool):
    """Exact printed plan footprint with real paper plies and stepped bundle edges.

    The 2D bundle's side stripes become shallow descending sheet steps in plan.
    This explicit height interpretation is inferred; it is not camera reconstruction.
    """
    im=image(CASH,state);alpha=np.array(im)[:,:,3]>0
    field=np.ones((32,32),dtype=int)*8
    if state=='spacecash_200':field[10:18,10:26]=16
    if state=='spacecash_500':
        field[12:20,7:23]=56;field[20:22,7:23]=40;field[22:24,7:23]=24
        field[22:30,10:26]=8
    if state=='spacecash_1000':
        field[7:15,8:24]=56;field[15:17,8:24]=40
        field[17:25,11:27]=32
        field[12:20,3:19]=80;field[20:22,3:19]=64;field[22:24,3:19]=48
        field[22:30,6:22]=16
    field[~alpha]=0
    parts=[];proof=[]
    for i,(rect,mm) in enumerate(rectangles(field)):
        height=mm/1000
        # Continuous touching plies retain a closed solid; alternating pale and
        # shaded source palette colors expose the layered paper on real sides.
        cuts=[0,height*.36,height*.71,height-.002] if mm>=16 else [0,height-.002]
        for layer,(a,b) in enumerate(zip(cuts,cuts[1:])):
            parts.append(box(f'paper section {i+1} ply {layer+1}',rect,a,b,('#CCC7B4','#9D9380','#D6CEB7')[layer%3]))
        surface=pool.crop(CASH,state,rect,'Exact opaque printed source patch; projected in XY at its inferred paper tier.')
        parts.append(box(f'paper section {i+1} original print',rect,height-.002,height,surface=surface))
        proof.append(dict(rect=list(rect),height=height,surface=surface))
    assert sum((r['rect'][2]-r['rect'][0])*(r['rect'][3]-r['rect'][1]) for r in proof)==int(alpha.sum())
    # Reassemble written surfaces, including transparent margins, independently
    # of the geometry rasterizer. No interpolation/color approximation permitted.
    reconstructed=Image.new('RGBA',(32,32))
    for r in proof:
        crop=Image.open(TEXTURES/(r['surface']+'.png')).convert('RGBA');reconstructed.paste(crop,r['rect'][:2])
    assert reconstructed.tobytes()==im.tobytes()
    return parts,dict(state=state,opaquePixels=int(alpha.sum()),parts=len(parts),rectangles=proof,
                      originalRgbaSha256=hashlib.sha256(im.tobytes()).hexdigest(),exactPrintedPlanReconstruction=True)

def fork_geometry(state,pool):
    parts=[];proof=[]
    def piece(label,rect,z0,z1,physical=None):
        surface=pool.crop(FORK,state,rect,'Verbatim fork source pixels on a solid handle/head/tine.')
        parts.append(box(label,physical or rect,z0,z1,surface=surface));proof.append(dict(label=label,sourceRect=list(rect),physicalPixelRect=list(physical or rect)))
    # The black sprite contours around neighboring tines touch at 1px scale.
    # Three separate metal/plastic tines narrow those contours by .25px per
    # side to retain the real slots in 3D; the source reference stays unchanged.
    for n,x in enumerate((13,15,17)):
        piece(f'tine {n+1}',(x,5,x+1,10),.009,.021,(x-.25,5,x+1.25,10))
    piece('head bridge',(12,10,19,12),.005,.021)
    piece('tapered shoulder',(13,12,18,13),.003,.018)
    piece('narrow neck',(14,13,17,19),.001,.015)
    piece('solid handle',(13,19,18,28),0,.020)
    piece('rounded end step',(14,28,17,29),.002,.017)
    # Positive empty plan slots between tines, not texture-only transparency.
    for x in (14.5,16.5):
        wx,wy=(x-16)/32,(16-7)/32
        assert not any(p['min'][0]<wx<p['max'][0] and p['min'][1]<wy<p['max'][1] for p in parts)
    return parts,dict(state=state,parts=len(parts),solidTines=3,clearSlots=2,slotWidth=.5/32,
                      sourceAlphaBounds=list(image(FORK,state).getbbox()),pieces=proof,
                      inference='Narrowed touching outline pixels into physical tine slots; Z thickness and neck taper are inferred.')

def configure(pool):
    registry=surfaces.load_surfaces()
    for r in pool.rows:
        path=TEXTURES/(r['id']+'.png');registry[r['id']]={**r,'file':path,'image':Image.open(path).convert('RGBA')}
    surfaces.load_surfaces=lambda:registry

def serialize(models):
    data=deepcopy(models)
    for model in data:
        for key in ('groundOffset','sourceSpriteOffset'):model[key]=', '.join(str(v) for v in model[key])
        lists=[model['parts']]+[f['parts'] for s in model['spriteStates'].values() for f in s['frames']]
        for parts in lists:
            for p in parts:
                for key in ('min','max'):p[key]=', '.join(f'{v:.8g}' for v in p[key])
    return data

def reviews(models,compositions):
    REVIEW.mkdir(parents=True,exist_ok=True)
    font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',15)
    montage=Image.new('RGB',(1300,math.ceil(len(compositions)/4)*265+40),'#17212B');draw=ImageDraw.Draw(montage)
    draw.text((12,8),'Original source / physical cash plies and solid cutlery; inferred depth',font=font,fill='white')
    for n,(state,(rsi,parts)) in enumerate(compositions.items()):
        x=n%4*325;y=n//4*265+40;original=image(rsi,state)
        ref=Image.new('RGBA',(96,96),'#60717A');ref.alpha_composite(original.resize((96,96),Image.Resampling.NEAREST))
        montage.paste(ref.convert('RGB'),(x+8,y+36))
        rendered=bm.render_model({'parts':parts},(220,210),-math.pi/2+.24,.74,pixels_per_unit=235,screen_origin=(110,125))
        montage.paste(rendered,(x+105,y+5));draw.text((x+8,y+230),state,font=font,fill='white')
    montage.save(REVIEW/'source-model-montage.png')
    for model in models:
        card=Image.new('RGB',(1100,350),'#17212B');d=ImageDraw.Draw(card);d.text((12,8),model['id'],font=font,fill='white')
        ref=Image.new('RGBA',(224,224),'#60717A');ref.alpha_composite(image(model['referenceRsi'],model['referenceState']).resize((224,224),Image.Resampling.NEAREST));card.paste(ref.convert('RGB'),(12,60))
        for i,(yaw,pitch) in enumerate(((-math.pi/2,.9),(-.3,.7),(math.pi/2,.35))):
            card.paste(bm.render_model(model,(280,270),yaw,pitch,pixels_per_unit=280,screen_origin=(140,150)),(250+i*280,45))
        card.save(REVIEW/(model['id']+'.png'))

def context_checks(models,render=True):
    library=json.loads((BASELINE/'models.json').read_text())['models'];by_id={m['id']:m for m in library+models}
    by_proto={p:m for m in models for p in m['sourcePrototypes']}
    raw=json.loads((GEN/'cash-cutlery-source-audit.json').read_text())
    source={(r['map'],r['uid']):r for r in raw['records']}
    records=[];hashes={};cards=0
    for spec in json.loads((BASELINE/'scenes.json').read_text()):
        path=BASELINE/spec['file'];doc=json.loads(path.read_text());targets=[e for e in doc['instances'] if e['prototype'] in by_proto]
        if not targets:continue
        hashes[path.relative_to(ROOT).as_posix()]=sha(path)
        for e in targets:
            model=by_proto[e['prototype']];saved=source[(doc['map']['path'],e['id'])]
            assert set(saved['savedComponents'])=={'Transform'}
            assert saved['sourceOwnerState']==model['referenceState']
            e.update(modelId=model['id'],matchKind='exact',renderYaw=e['yaw'])
            e.pop('geometryKey',None)
        # Resolve the entire local scene so support choices use its authored
        # table footprints, existing offsets, and actual geometry variants.
        resolve_placements(doc['instances'],list(by_id.values()),doc.get('geometryVariants',{}))
        for e in targets:
            model=by_proto[e['prototype']];parts=world_parts(model['parts'],e['position'],e['renderYaw'],e.get('renderOffset',[0,0,0]))
            near=[];context=deepcopy(parts)
            for other in doc['instances']:
                if other['id']==e['id'] or max(abs(other['position'][i]-e['position'][i]) for i in (0,1))>.8:continue
                target=by_id.get(other.get('modelId'))
                if not target:
                    near.append(dict(id=other['id'],prototype=other['prototype'],mapped=False));continue
                actual=doc.get('geometryVariants',{}).get(other.get('geometryKey'),target['parts'])
                placed=world_parts(actual,other['position'],other.get('renderYaw',other['yaw']),other.get('renderOffset',[0,0,0]))
                hits,_=contacts(parts,placed)
                near.append(dict(id=other['id'],prototype=other['prototype'],mapped=True,conservativeContactPairs=len(hits),contacts=hits[:8],selectedSupport=other['id']==e.get('support',{}).get('entity')))
                context.extend(placed)
            records.append(dict(variant=spec['variant'],level=spec['level'],uid=e['id'],prototype=e['prototype'],sourceState=model['referenceState'],position=e['position'],yaw=e['yaw'],renderYaw=e['renderYaw'],renderOffset=e.get('renderOffset',[0,0,0]),support=e.get('support'),neighbors=near))
            if render and (cards<9 or any(n.get('conservativeContactPairs') for n in near)):
                local=[]
                for p in context:
                    for bound in ('min','max'):
                        for axis in (0,1):p[bound][axis]-=e['position'][axis]
                    if p['min'][2]>=1.5:continue
                    p['max'][2]=min(p['max'][2],1.5);local.append(p)
                bm.render_model({'parts':local},(760,610),-math.pi/2+.28,.83,pixels_per_unit=270,screen_origin=(380,410)).save(REVIEW/f'context-{spec["variant"]}-{spec["level"]}-{e["id"]}.png');cards+=1
    assert Counter(r['variant'] for r in records)=={'redux':22,'classic':11}
    return records,hashes

def main():
    global CHECK
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true');parser.add_argument('--skip-reviews',action='store_true');parser.add_argument('--skip-context',action='store_true');args=parser.parse_args();CHECK=args.check
    pool=Pool();cash={};compositions={};proof=[]
    for state in STATES:
        parts,record=cash_geometry(state,pool);cash[state]=dict(frames=[dict(parts=parts)],delays=[1]);compositions[state]=(CASH,parts);proof.append(record)
    forks={}
    for state in ('icon','plastic'):
        parts,record=fork_geometry(state,pool);forks[state]=dict(frames=[dict(parts=parts)],delays=[1]);compositions[state]=(FORK,parts);proof.append(record)
    configure(pool);models=[]
    for uid in IDS:
        is_cash=uid.startswith('RMCSpaceCash');rsi=CASH if is_cash else FORK
        state=('spacecash' if uid=='RMCSpaceCash1' else 'spacecash_'+uid.removeprefix('RMCSpaceCash')) if is_cash else ('plastic' if uid.endswith('Plastic') else 'icon')
        states=cash if is_cash else {state:forks[state]}
        model=dict(type='cmu3DModel',id='CMU3D'+uid,label=uid.removeprefix('RMC'),status='draft',sourcePrototypes=[uid],referencePrototype=uid,
            referenceRsi=rsi,referenceState=state,referenceTint='#FFFFFF',sourceDirections=1,useEntityRotation=True,placement='surface',groundOffset='0, 0',
            sourceSpriteOffset='0, 0',sourceSpriteRotates=True,
            description='Source-owned cash stack states with printed physical paper plies.' if is_cash else 'Separate solid fork tines, bridge, neck and handle retaining original source palette/crops.',
            parts=deepcopy(states[state]['frames'][0]['parts']),spriteStates=deepcopy(states))
        model['description']+=' XY source origin and saved yaw are preserved; thickness and unseen sides are inferred. See SOURCES_CASH_CUTLERY.md.'
        model=bm.validate_model(model);sprite_states.validate_source(model,bm.resource_file);models.append(model)
    for path,rows in ((MODELS,serialize(models)),(ART,pool.rows)):
        write(path,('# Generated by Tools/three_d/author_cash_cutlery.py; dedicated physical drafts.\n'+yaml.safe_dump(rows,sort_keys=False,width=110)).encode())
    actual=bm.load_models(MODELS);assert len(actual)==7
    for model in actual:
        assert len(model['parts'])<=128 and model['useEntityRotation'] and model['sourceSpriteRotates']
        for definition in model['spriteStates'].values():
            assert min(p['min'][2] for p in definition['frames'][0]['parts'])==0
    source_audit=json.loads((GEN/'cash-cutlery-source-audit.json').read_text())
    by_proto={p:m for m in models for p in m['sourcePrototypes']};state_checks=[]
    for saved in source_audit['records']:
        actual_state,reason=sprite_states.saved_pose(by_proto[saved['prototype']],source_audit['defaults'][saved['prototype']],saved['savedComponents'],scene.normalize_tint)
        assert actual_state==saved['sourceOwnerState'] and reason is None,(saved['uid'],reason)
        state_checks.append(dict(uid=saved['uid'],variant=saved['variant'],level=saved['level'],count=saved['effectiveCount'],state=actual_state,exported=saved['exported']))
    if not args.skip_reviews:reviews(models,compositions)
    contexts,inputs=([],{}) if args.skip_context else context_checks(models,not args.skip_reviews)
    copyright_lines=[]
    for rsi in (CASH,FORK):
        meta=json.loads((bm.resource_file(rsi)/'meta.json').read_text());copyright_lines.append(rsi+': '+meta['copyright'])
    notes='''# Cash and cutlery source drafts

Seven exact mappings: RMCSpaceCash1/10/20/100/1000, RMCFork and RMCForkPlastic. Original world source RSI references are retained. Each rotating single-direction model preserves source origin, zero Sprite offset and arbitrary saved yaw. All are surface props; the existing exact authored table-footprint resolver supplies support height plus 0.002. It never invents a table when none is modeled.

Cash uses all eight original static world states. Content.Client/Stack/StackSystem.cs owns the threshold selection [10,20,50,100,200,500,1000], then ItemCounterSystem.ProcessOpaqueSprite selects the mapped base layer through ContentHelpers.RoundToEqualLevels. Existing spriteStates consumes that actual layer; no chemistry, cash count, merge/split or new animation controller is invented. Unknown states, extra visible layers or unsupported transforms retain the existing source fallback. Offline exports require the corresponding source-owned stack resolver. Native gameplay validation is separate from these asset proofs.

Cash printed surfaces preserve every original RGBA pixel and transparent margin in an exact plan reconstruction. Separate pale/shaded paper plies and stepped bundle heights make closed solids. The source bundle side stripes become shallow descending sheet steps. This is an explicit depth interpretation, not proof of a recovered perspective camera. Hidden backs, paper thickness and bundle tier heights are inferred. No denomination is rendered as a floating image card.

Both forks have three actual solid tines and two empty 0.015625-wide slots, a head bridge, shoulder, neck, handle and end. One-pixel tine-center crops and full bridge/neck/handle crops are verbatim. Touching dark sprite outlines are narrowed into genuine 3D slots; the cropped original source reference is never modified. Z thickness, bevel/shoulder interpretation and unobserved underside are inferred. In-hand four-direction art is not a world-state model. RMCForkPlastic inherits RMCFork, not upstream ForkPlastic. UtensilSystem consumes food directly and deletes a broken utensil; there is no authored food-on-fork or invented broken animation.

Raw map inventory covers 46 saved records: cash16 Redux/9 classic and forks15 Redux/6 classic. Only33 are exported world props: cash7 Redux/5 classic, forks15 Redux/6 classic. Thirteen cash entries are hidden containers and stay hidden. The only saved count override is hidden Redux surface UID18033 count5000, selecting the existing1000 state. All visible props have only Transform overrides. Fork poses include -90,0,+90 degrees; no offset recentering is applied.

Reproduce dedicated assets with `python Tools/three_d/author_cash_cutlery.py`; use `--check --skip-reviews` for byte equality. No global library/scene build or game launch is performed. The cached raw map evidence is generated/cash-cutlery-source-audit.json; generated/cash-cutlery-proof.json records crops, physical slots, part counts and saved-context checks against the frozen950 baseline. Conservative AABB contact counts are not live gameplay collision claims.

## Attribution

Original source sheets and derived crops use CC-BY-SA-3.0. Preserve original attribution on redistribution.

'''+ '\n\n'.join(copyright_lines)+'\n'
    write(NOTES,notes.encode())
    report=dict(status='dedicated-assets-ready',models=7,ids=[m['id'] for m in models],exactMappings=IDS,
        counts={'savedRedux':31,'savedClassic':15,'visibleRedux':22,'visibleClassic':11,'hiddenCash':13},
        surfaces=len(pool.rows),atlasIndices=[r['atlasIndex'] for r in pool.rows],sourceCompositions=proof,
        crops=pool.evidence,contexts=contexts,contextInputSha256=inputs,
        supportedSourceStates={m['id']:list(m['spriteStates']) for m in models},
        actualOfflineSourceOwnerChecks=state_checks,
        writtenAssetsSha256={p.relative_to(ROOT).as_posix():sha(p) for p in WRITTEN},
        sourceAuditSha256=sha(GEN/'cash-cutlery-source-audit.json'),generatorSha256=sha(Path(__file__)),
        limitations=['Depth and unobserved construction are inferred, not recovered 3D truth.',
          'Fork outline narrowing is an explicit physical interpretation; crops preserve retained source RGBA, not every outline pixel.',
          'Before final shared exports, source-owned cash count resolution is assumed from the recorded controller; root owns offline adapter validation.',
          'Neighbor contact checks use frozen950 modeled geometry; unmapped neighbors and newly authored simultaneous families need final shared-context review.',
          'No game, server, global export, common code or native build is changed by this generator.'])
    if not args.skip_context:json_write(GEN/'cash-cutlery-proof.json',report)
    else:json_write(GEN/'cash-cutlery-geometry-proof.json',report)
    print(json.dumps({'models':7,'surfaces':len(pool.rows),'parts':{s:len(p[1]) for s,p in compositions.items()},'contexts':len(contexts),
                      'contacts':sum(n.get('conservativeContactPairs',0) for r in contexts for n in r['neighbors'])}))

if __name__=='__main__':main()
