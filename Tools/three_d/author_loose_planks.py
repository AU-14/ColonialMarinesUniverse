"""Nine source-shaped cave-floor timber drafts; dedicated assets and evidence only."""
import argparse
import copy
import hashlib
import json
import math
from collections import Counter
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont
import yaml

import build_models as bm
import inventory
import layout
import placement
import scene
import surfaces
from author_cash_cutlery import rectangles
from author_wide_machinery import world_parts, contacts
from author_vendor_fans import contact_witnesses

ROOT=Path(__file__).resolve().parents[2]
GEN=ROOT/'Tools/three_d/generated'
BASE=ROOT/'.codex/model-batch-baseline978'
PROTOS=ROOT/'Content.CMU/Resources/ThreeD/Prototypes/World'
MODEL=PROTOS/'garrison_loose_planks.yml'
ART=PROTOS/'garrison_loose_planks_art.yml'
TEXTURES=ROOT/'Content.CMU/Resources/Textures/CMU14/ThreeD/Surfaces'
NOTE=ROOT/'Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_LOOSE_PLANKS.md'
REVIEW=GEN/'review/loose-planks'
RSI='CMU14/N14content/cave_decor.rsi'
SOURCE=ROOT/'Content.CMU/Resources/Textures'/RSI
STATES={3:'boards_drought_ns-3',4:'boards_drought_ns-4',8:'boards_drought_we-2',11:'boards_drought_we-5',
        17:'boards_mammoth_ns-5',18:'boards_mammoth_ns-6',19:'boards_mammoth_we-1',
        22:'boards_mammoth_we-4',23:'boards_mammoth_we-5'}
IDS=['DecorFloorBoard'+str(n) for n in STATES]


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def source(number):return Image.open(SOURCE/(STATES[number]+'.png')).convert('RGBA')


def masks(number):
    """Source-guided overlapping timber sections. Hidden height is inferred."""
    y,x=np.mgrid[0:32,0:32]
    configs={
        11:(x<22,(y>=12)&(y<25)),
        17:((y<11)|((x<20)&(y<22)),(x>=12)&(x<25)),
        18:((y<12)|((x>=.75*y+5)&(y<23)),(x>=8)&(x<21)),
        19:(x<23,(y>=12)&(y<25)),
        22:((x<10)|(x>=22),(y>=11)&(y<23)),
        23:((x<18)|(x>=23),(y>=11)&(y<23)),
        3:(y>=34-x,(x>=8)&(x<21)),
        4:((y<11)|((x>=.8*y+2)&(y<24)),(x>=8)&(x<21)),
        8:((x>=9)&(y<.333*x+12.8),(y>=11)&(y<23)&(x<24)),
    }
    alpha=np.array(source(number))[:,:,3]>0
    upper,envelope=configs[number]
    # Lower timber continues underneath the crossing section only inside the
    # existing alpha footprint. No transparent source hole becomes solid.
    return alpha&(~upper|envelope),alpha&upper,alpha&~upper


def box(label,rect,z0,z1,color,surface=None):
    x0,y0,x1,y1=rect
    p=dict(label=label,min=[(x0-16)/32,(16-y1)/32,z0],max=[(x1-16)/32,(16-y0)/32,z1],color=color)
    if surface:p.update(surface=surface,surfaceAxis='XY')
    return p


def serialize(value):
    if isinstance(value,list):return [serialize(v) for v in value]
    if isinstance(value,dict):return {k:', '.join(f'{a:.9f}' for a in v) if k in ('min','max') else serialize(v) for k,v in value.items()}
    return value


def author():
    REVIEW.mkdir(parents=True,exist_ok=True)
    used={r['atlasIndex'] for p in PROTOS.glob('*.yml') if p.name!=ART.name
          for r in yaml.load(p.read_text(encoding='utf-8-sig'),Loader=yaml.CSafeLoader) or [] if r.get('type')=='cmu3DSurface'}
    assert not used.intersection(range(2500,2600))
    meta=json.loads((SOURCE/'meta.json').read_text());states={s['name']:s for s in meta['states']}
    models=[];art=[];proof=[]
    for number,state in STATES.items():
        assert states[state].get('directions',1)==1 and states[state].get('delays',[[1]])==[[1]]
        im=source(number);rgba=np.array(im);alpha=rgba[:,:,3]>0
        base,upper,lower=masks(number)
        assert np.array_equal(base|upper,alpha)
        colors=Counter(tuple(p[:3]) for p in rgba[alpha]);palette=sorted(colors,key=lambda p:sum(p))
        edge='#'+''.join(f'{c:02X}' for c in palette[len(palette)//3])
        parts=[];patches=[];supports=[]
        for name,mask,z0,z1 in [('lower timber',base,.004,.04),('crossing timber',upper,.04,.076)]:
            for i,(rect,_) in enumerate(rectangles(mask.astype(int))):
                label=f'{name} solid section {i+1}'
                parts.append(box(label,rect,z0,z1,edge));supports.append(label)
        for name,mask,height in [('lower timber',lower,.04),('crossing timber',upper,.076)]:
            ys,xs=np.where(mask);rect=(int(xs.min()),int(ys.min()),int(xs.max()+1),int(ys.max()+1))
            pixels=rgba.copy();pixels[~mask]=0
            patch=Image.fromarray(pixels).crop(rect)
            index=2500+len(art);assert index<2600
            uid=f'CMU3DLoosePlankSurface{index}'
            patch.save(TEXTURES/(uid+'.png'))
            assert Image.open(TEXTURES/(uid+'.png')).convert('RGBA').tobytes()==patch.tobytes()
            art.append(dict(type='cmu3DSurface',id=uid,atlasIndex=index,texture=f'/Textures/CMU14/ThreeD/Surfaces/{uid}.png'))
            parts.append(box(name+' original grain',rect,height-.001,height,'#FFFFFF',uid))
            patches.append(dict(surface=uid,rect=list(rect),height=height,rgbaSha256=hashlib.sha256(patch.tobytes()).hexdigest()))
        record=dict(type='cmu3DModel',id=f'CMU3DLooseFloorBoard{number}',label=f'Loose cave floor planks {number}',status='draft',
            sourcePrototypes=[f'DecorFloorBoard{number}'],referencePrototype=f'DecorFloorBoard{number}',referenceRsi=RSI,
            referenceState=state,sourceDirections=1,useEntityRotation=False,yawOffset=0,groundOffset='0,0',
            placement='surface' if number in (3,8) else 'floor',
            description='Source-shaped solid timber sections with original drought/mammoth grain, separate raised crossing sections and exact alpha gaps. Static fixed noRot visual orientation; saved source yaw/position unchanged. Heights .004-.076 tiles and hidden continuation are inferred. Bedroll/platform compound support remains a documented draft limitation. See SOURCES_LOOSE_PLANKS.md.',parts=parts)
        if number in (11,17):record['supportSurfaces']=supports
        models.append(record)
        reference=REVIEW/(f'source-{number}.png');im.save(reference)
        proof.append(dict(prototype=f'DecorFloorBoard{number}',modelId=record['id'],state=state,sourceDirections=1,frames=1,
            parts=len(parts),opaquePixels=int(alpha.sum()),transparentPixels=int((~alpha).sum()),sourceBounds=list(im.getbbox()),
            sourceSha256=sha(SOURCE/(state+'.png')),patches=patches,sideColor=edge,
            lowerSolidPixels=int(base.sum()),crossingSolidPixels=int(upper.sum()),supportLabels=record.get('supportSurfaces',[])))
    MODEL.write_text('# Exact source pixels and inferred shallow timber geometry; all drafts.\n'+yaml.safe_dump(serialize(models),sort_keys=False,width=110),encoding='utf-8')
    ART.write_text('# CC-BY-NC-SA-3.0 original cave_decor crops; see SOURCES_LOOSE_PLANKS.md.\n'+yaml.safe_dump(art,sort_keys=False),encoding='utf-8')
    surfaces.load_surfaces.cache_clear()
    loaded=bm.load_models(MODEL)
    document=dict(status='draft assets written; context pending',schemaVersion=1,models=proof,
        sourceMetaSha256=sha(SOURCE/'meta.json'),license=meta['license'],copyright=meta['copyright'],
        atlasIndices=[a['atlasIndex'] for a in art],textureCount=len(art))
    (GEN/'loose-planks-proof.json').write_text(json.dumps(document,indent=2)+'\n')
    print(json.dumps(dict(models=len(loaded),parts=[len(m['parts']) for m in loaded],textures=len(art))),flush=True)
    return loaded,document


def written_proof(models,proof):
    library={m['id']:m for m in models}
    raw=yaml.safe_load(MODEL.read_text())
    assert all(isinstance(p[k],str) for m in raw for p in m['parts'] for k in ('min','max'))
    for entry in proof['models']:
        number=int(entry['prototype'].replace('DecorFloorBoard',''));im=source(number)
        reconstructed=Image.new('RGBA',(32,32))
        for patch in entry['patches']:
            png=Image.open(TEXTURES/(patch['surface']+'.png')).convert('RGBA')
            assert hashlib.sha256(png.tobytes()).hexdigest()==patch['rgbaSha256']
            reconstructed.alpha_composite(png,patch['rect'][:2])
        assert reconstructed.tobytes()==im.tobytes()
        physical=np.zeros((32,32),bool);heights=np.zeros((32,32))
        m=library[entry['modelId']]
        for part in m['parts']:
            if part.get('surface'):continue
            x0=round(part['min'][0]*32+16);x1=round(part['max'][0]*32+16)
            y0=round(16-part['max'][1]*32);y1=round(16-part['min'][1]*32)
            physical[y0:y1,x0:x1]=True
            heights[y0:y1,x0:x1]=np.maximum(heights[y0:y1,x0:x1],part['max'][2])
        assert np.array_equal(physical,np.array(im)[:,:,3]>0)
        _,upper,_=masks(number)
        assert np.allclose(heights[physical],np.where(upper,.076,.04)[physical])
        assert not m['useEntityRotation']
        palette={tuple(p[:3]) for p in np.array(im)[np.array(im)[:,:,3]>0]}
        assert all(tuple(bytes.fromhex(p['color'][1:])) in palette for p in m['parts'] if not p.get('surface'))
        entry.update(writtenRgbaReconstructed=True,writtenSolidProjectionExact=True,transparentGapsPreserved=True,
            writtenHeightFieldVerified=True,sourceColorsOnAllTopPixels=True,writtenSidePaletteVerified=True)
    proof['writtenScalarVectorsVerified']=True


def cards(models):
    font=ImageFont.load_default(size=16)
    for m in models:
        number=int(m['referencePrototype'].replace('DecorFloorBoard',''))
        card=Image.new('RGB',(1420,470),'#182C38');d=ImageDraw.Draw(card)
        d.text((12,10),m['label']+' / original and four solid views',fill='white',font=font)
        d.text((12,35),'Draft: exact original top pixels and alpha; crossing height and hidden wood inferred. Source noRot remains fixed.',fill='#BBD4DF',font=font)
        src=source(number).resize((230,230),Image.Resampling.NEAREST);card.paste(src,(10,140),src)
        for i,yaw in enumerate([-math.pi/2,-math.pi/3,math.pi/3,math.pi]):
            panel=bm.render_model(m,(290,360),yaw,.62,pixels_per_unit=240,screen_origin=(145,220))
            card.paste(panel,(250+i*290,85))
        card.save(REVIEW/(m['id']+'.png'))


def context(models,proof):
    index,_,_=inventory.load_prototypes(ROOT);resolver=inventory.Resolver(index['entity']);defaults={}
    def default(proto):
        if proto not in defaults:
            try:defaults[proto]=inventory.component_map(resolver.resolve(proto))
            except (KeyError,ValueError):defaults[proto]={}
        return defaults[proto]
    library={m['id']:m for m in json.loads((BASE/'models.json').read_text())['models']}
    library.update({m['id']:m for m in models});mapping={p:m for m in models for p in m['sourcePrototypes']}
    evidence=[];source_inputs={};font=ImageFont.load_default(size=15)
    for spec in json.loads((BASE/'scenes.json').read_text()):
        path=BASE/spec['file'];doc=json.loads(path.read_text());targets=[e for e in doc['instances'] if e['prototype'] in IDS]
        if not targets:continue
        source_inputs[path.relative_to(ROOT).as_posix()]=sha(path)
        map_path=ROOT/doc['map']['path'];_,records=scene.read_map(map_path)
        for raw in records.values():default(raw['prototype'])
        transforms=scene.WorldTransforms(records,defaults)
        ordinal={uid:i for i,uid in enumerate(records)}
        for e in targets:
            m=mapping[e['prototype']];raw=records[e['id']]
            sprite={**default(e['prototype'])['Sprite'],**raw['components'].get('Sprite',{})}
            assert sprite['noRot'] and sprite['state']==m['referenceState']
            assert sprite.get('visible',True) and sprite.get('offset','0,0').replace(' ','')=='0,0'
            assert not sprite.get('layers') and not sprite.get('rotation')
            e.update(modelId=m['id'],matchKind='exact',renderYaw=0)
        layout.resolve_layout(targets,list(library.values()),records,defaults,transforms)
        # Apply only existing support metadata, with exact source entities present.
        # Neighbors without this family's metadata retain their old geometry.
        placement.resolve_placements(doc['instances'],list(library.values()),doc['geometryVariants'])
        for e in targets:
            m=library[e['modelId']];raw=records[e['id']]
            assert e['renderYaw']==0
            own=world_parts(m['parts'],e['position'],0,e.get('renderOffset',[0,0,0]))
            assembled=copy.deepcopy(own);neighbors=[];unknown=[]
            for n in doc['instances']:
                if n['id']==e['id'] or sum((a-b)**2 for a,b in zip(n['position'][:2],e['position'][:2]))>1.55**2:continue
                nm=library.get(n.get('modelId'))
                if nm is None:unknown.append(dict(id=n['id'],prototype=n['prototype']));continue
                other=world_parts(doc['geometryVariants'].get(n.get('geometryKey'),nm['parts']),n['position'],n.get('renderYaw',n['yaw']),n.get('renderOffset',[0,0,0]))
                hits,gap=contacts(own,other);contact_witnesses(hits,own,other)
                neighbors.append(dict(id=n['id'],prototype=n['prototype'],position=n['position'],contacts=hits,minimumAabbSeparation=gap))
                assembled.extend(other)
            record=dict(variant=spec['variant'],level=spec['level'],id=e['id'],prototype=e['prototype'],modelId=m['id'],
                position=e['position'],savedYaw=e['yaw'],renderYaw=e['renderYaw'],renderOffset=e.get('renderOffset',[0,0,0]),support=e.get('support'),
                rawMapOrdinal=ordinal[e['id']],sourceState=m['referenceState'],sourceFrame=0,sourceVisible=True,
                savedComponents=raw['components'],sourceContractAccepted=True,modeledNeighbors=neighbors,unknownNeighbors=unknown,
                contactPairs=sum(len(n['contacts']) for n in neighbors))
            evidence.append(record)
            # Flat floor below Z=0 is explicitly a review underlay; actual saved
            # floor tile/material metadata remains in parent scene exports.
            for p in assembled:
                for key in ('min','max'):
                    p[key][0]-=e['position'][0];p[key][1]-=e['position'][1]
            assembled.append(dict(label='flat reference floor',min=[-1.35,-1.35,-.055],max=[1.35,1.35,-.005],color='#53504A'))
            c=Image.new('RGB',(1280,670),'#182C38');d=ImageDraw.Draw(c)
            d.text((12,10),f'{spec["variant"].title()} {spec["level"]:+d} / UID {e["id"]} / {e["prototype"]}',fill='white',font=font)
            d.text((12,35),f'Saved yaw {math.degrees(e["yaw"]):.0f}; fixed render yaw0; support {e.get("support")}',fill='#BBD4DF',font=font)
            for i,yaw in enumerate([-math.pi/3,math.pi/2+.15]):
                panel=bm.render_model(dict(parts=assembled),(635,540),yaw,.75,pixels_per_unit=150,screen_origin=(315,440));c.paste(panel,(640*i,75))
            d.text((12,637),f'{record["contactPairs"]} conservative part contacts; flat reference floor only. Source pivots unchanged.',fill='#BBD4DF',font=font)
            c.save(REVIEW/f'context-{spec["variant"]}-{spec["level"]}-{e["id"]}.png')
    assert len(evidence)==14 and Counter(e['variant'] for e in evidence)=={'redux':11,'classic':3}
    proof.update(status='written draft assets and all14 source/context placements checked; final parent export pending',contexts=evidence,
        placementCounts=dict(Counter(e['variant'] for e in evidence)),inputScenesSha256=source_inputs,
        sources={p:dict(components=default(p),file=resolver.resolve(p)['_source']) for p in IDS})
    proof['stackedPairs']=[dict(variant=e['variant'],level=e['level'],upperId=e['id'],lowerId=e['support']['entity'],height=e['support']['height'],
        partContacts=next(n['contacts'] for n in e['modeledNeighbors'] if n['id']==e['support']['entity'])) for e in evidence if e['support']]
    assert [(e['upperId'],e['lowerId']) for e in proof['stackedPairs']]==[(10187,10185),(10190,10189)]
    assert all(not e['partContacts'] for e in proof['stackedPairs'])
    print(json.dumps(dict(placements=len(evidence),stacks=len(proof['stackedPairs']),partContacts=sum(e['contactPairs'] for e in evidence))),flush=True)


def notes():
    NOTE.write_text('''# Loose cave-floor timber drafts

Nine exact prototypes, DecorFloorBoard3/4/8/11/17/18/19/22/23, cover11 Redux and3 classic saved placements. The nine original32×32 one-direction source states in `CMU14/N14content/cave_decor.rsi` are static. Source Sprite.noRot=true, no offset, no extra layer, FloorObjects depth, anchored static noncolliding Physics and no fixture. Saved0/90/180 degree transforms are preserved while visual render yaw remains0. Destructible owns destruction and wood sound at50 damage; it does not supply a plank animation. No movement, damage animation or controller is invented. Minecart and other unused RSI states are outside this batch.

Original drought/mammoth grain, nail/brace shading and every source alpha gap are retained. The solids follow the exact opaque source-pixel footprint at32pixels/tile. Visible timber sections are separated into lower and crossing layers, with source-shaped overhanging edges and inferred hidden continuation only beneath existing opaque pixels. The lower sections occupy Z.004–.04 and the crossing sections Z.04–.076 tiles. These heights, underside color and the choice of crossing boundary are inferred from the artwork; this is not a recovered three-dimensional scan. Source edge stepping is intentional. Written textures reconstruct every RGBA source pixel exactly in top projection, and the union of physical solid footprints equals the opaque source mask. Side colors are from the original palette.

Two saved same-pivot pairs overlap201/241 opaque source pixels. Existing exact support metadata on lower11/17 and surface placement on upper3/8 interpret these as stacks without modifying saved positions. Exposed real opaque body rectangles supply support; no transparent bounding-box area is invented as a support. The standalone Board3 remains on the floor. Original sources share FloorObjects depth, default render order and32×32 bounds; Clyde.Sprite.cs sorts equal depth/order/Y by entity UID. Saved10187 follows10185, and10190 follows10189. This is an inferred saved-stack interpretation, not a claim that arbitrary live render-order changes are reproduced by support metadata.

Context limitations are explicit: nearby bedrolls currently retain their floor-based underside atZ.007, so plausible raised wood needs later bedroll support adaptation. Board18 at Redux11951/classic7113 is co-centered with PlatformThree, whose existing model has no support metadata; this platform/board assembly needs a bounded shared follow-up, including whether the plank belongs beneath its deck or on it. Two exact source tile edges enter the existing Strata wall foot course by.020tile: Board23 UID214/wall292 and stacked Board3 UID10187/wall4080. These have positive solid witnesses, not merely AABB contacts; wall cores remain clear. These issues are not hidden by flattening/clipping source wood or changing neighbors. Existing cave structures, debris, gaps and source pivots are retained. Original source visibility/components and every14placement context are checked against `.codex/model-batch-baseline978`. Context review floors are labeled flat reference underlays; parent scene exports retain original tile/material data.

Source attribution: CC-BY-NC-SA-3.0, taken from mojave-sun-13 at https://github.com/Mojave-Sun/mojave-sun-13/blob/0cbeda29e69293cd3a637fe67576b30b7693d5f6/mojave/icons/structure/cave_decor.dmi. Preserve the source license and attribution when redistributing derived artwork.

Reproduce dedicated model/art/textures, source/four-view cards and proof with `Tools/three_d/author_loose_planks.py`. `--reviews-only` preserves model/art/textures. `--finalize-proof` checks the written source partitions and the two source-composite stacks without regenerating all contexts. `--check-assets` writes only an isolated `.codex/loose-planks-regeneration-check` copy and compares bytes. `--export-parity MANIFEST.json` compares final parent exports to the independent saved context. No shared builds, global exports, game or server process operations are performed.
''',encoding='utf-8')


def save_proof(proof):
    proof['writtenAssetSha256']={p.relative_to(ROOT).as_posix():sha(p) for p in [MODEL,ART,NOTE,
        *[TEXTURES/(e['id']+'.png') for e in yaml.safe_load(ART.read_text())]]}
    proof['generatorSha256']=sha(Path(__file__))
    (GEN/'loose-planks-proof.json').write_text(json.dumps(proof,indent=2)+'\n')


def finalize_proof(models,proof):
    library={m['id']:m for m in models};instances={e['id']:e for e in proof['contexts'] if e['variant']=='redux' and e['level']==-1}
    for stack in proof['stackedPairs']:
        lower,upper=instances[stack['lowerId']],instances[stack['upperId']]
        assert lower['rawMapOrdinal']<upper['rawMapOrdinal'] and lower['id']<upper['id']
        composed=Image.new('RGBA',(32,32));projected=Image.new('RGBA',(32,32));zbuffer=np.zeros((32,32))
        assembled=[]
        for e in (lower,upper):
            number=int(e['prototype'].replace('DecorFloorBoard',''));composed.alpha_composite(source(number))
            m=library[e['modelId']]
            assembled.extend(world_parts(m['parts'],[0,0,0],0,e['renderOffset']))
            for p in m['parts']:
                if not p.get('surface'):continue
                x0=round(p['min'][0]*32+16);y0=round(16-p['max'][1]*32)
                texture=Image.open(surfaces.load_surfaces()[p['surface']]['file']).convert('RGBA')
                height=p['max'][2]+e['renderOffset'][2]
                for y in range(texture.height):
                    for x in range(texture.width):
                        pixel=texture.getpixel((x,y))
                        if pixel[3] and height>zbuffer[y0+y,x0+x]:
                            projected.putpixel((x0+x,y0+y),pixel);zbuffer[y0+y,x0+x]=height
        assert composed.tobytes()==projected.tobytes()
        stack.update(originalCompositeRgbaExact=True,originalMapOrderConfirmed=True,
            upperMapOrdinal=upper['rawMapOrdinal'],lowerMapOrdinal=lower['rawMapOrdinal'],
            positiveVerticalSeparation=.002)
        c=Image.new('RGB',(1150,460),'#182C38');d=ImageDraw.Draw(c)
        d.text((12,10),f'Saved plank stack {lower["id"]} + {upper["id"]}: original source composite and physical views',fill='white',font=ImageFont.load_default(size=16))
        image=composed.resize((256,256),Image.Resampling.NEAREST);c.paste(image,(20,110),image)
        for i,yaw in enumerate([-math.pi/3,math.pi*.8]):
            panel=bm.render_model(dict(parts=assembled),(420,370),yaw,.65,pixels_per_unit=250,screen_origin=(210,240));c.paste(panel,(290+i*425,65))
        d.text((12,430),'Exact source top-color composite; .002 tile separation; source transforms unchanged. Bedroll support remains pending.',fill='#BBD4DF',font=ImageFont.load_default(size=14))
        c.save(REVIEW/f'stack-{lower["id"]}-{upper["id"]}.png')
    groups=[]
    for e in proof['contexts']:
        for n in e['modeledNeighbors']:
            if not n['contacts']:continue
            kind=('bedroll support pending' if n['prototype']=='Bedroll' else
                  'platform assembly support pending' if n['prototype']=='RMCPlatformHybrisaThree' else
                  'source-edge wall foot-course contact' if n['prototype']=='RMCWallStrata' else 'unresolved other contact')
            groups.append(dict(variant=e['variant'],level=e['level'],id=e['id'],neighborId=n['id'],prototype=n['prototype'],
                classification=kind,partPairs=len(n['contacts']),positiveSolidWitnesses=sum('solidWitness' in h for h in n['contacts'])))
    proof.update(contactClassification=groups,
        sourceGuardAudit=dict(visibleStaticSources=14,onlyTransformSavedOverrides=all(set(e['savedComponents'])=={'Transform'} for e in proof['contexts']),
            noRot=True,sourceDirections=1,offset=[0,0],scale=[1,1],defaultWhiteTint=True,extraLayers=False),
        sourceDefinitionSha256={v['file']:sha(ROOT/v['file']) for v in proof['sources'].values()},
        limitations=['All models remain drafts; crossing height, layer boundary and underside are inferred.',
            'Bedroll, platform and two wall-foot contacts are retained in contactClassification; this asset batch does not modify those assemblies.',
            'Positive solid/source-alpha witnesses establish intersections; absence from729 samples is not an exact separation proof.',
            'Source-map stack order is evidenced; arbitrary runtime changes to Sprite.RenderOrder are not modeled.'])
    save_proof(proof)
    print(json.dumps(dict(writtenModels=len(models),contexts=len(proof['contexts']),contactNeighbors=len(groups),solidWitnesses=sum(g['positiveSolidWitnesses'] for g in groups))),flush=True)


def check_assets():
    global MODEL,ART,TEXTURES,REVIEW,GEN
    originals=[MODEL,ART,*[TEXTURES/(e['id']+'.png') for e in yaml.safe_load(ART.read_text())]]
    stage=ROOT/'.codex/loose-planks-regeneration-check';MODEL=stage/MODEL.name;ART=stage/ART.name
    TEXTURES=stage/'textures';REVIEW=stage/'review';GEN=stage/'proof'
    for p in (stage,TEXTURES,REVIEW,GEN):p.mkdir(parents=True,exist_ok=True)
    author();checks=[]
    for path in originals:
        candidate=(TEXTURES if path.suffix=='.png' else stage)/path.name
        assert candidate.read_bytes()==path.read_bytes(),path
        checks.append(dict(path=path.relative_to(ROOT).as_posix(),sha256=sha(path)))
    (stage/'comparison.json').write_text(json.dumps(dict(byteIdentical=len(checks),files=checks),indent=2)+'\n')
    print(json.dumps(dict(byteIdentical=len(checks))))


def export_parity(manifest):
    proof=json.loads((GEN/'loose-planks-proof.json').read_text());expected={(e['variant'],e['level'],e['id']):e for e in proof['contexts']}
    models={m['id']:m for m in bm.load_models(MODEL)};lib={m['id']:m for m in json.loads((GEN/'models.json').read_text())['models']}
    checks=[];inputs={};new_neighbors=[]
    baseline_ids={m['id'] for m in json.loads((BASE/'models.json').read_text())['models']}
    def same(a,b):
        if isinstance(a,(int,float)) and isinstance(b,(int,float)):return abs(a-b)<1e-7
        if isinstance(a,(list,tuple)) and isinstance(b,(list,tuple)):return len(a)==len(b) and all(same(x,y) for x,y in zip(a,b))
        if isinstance(a,dict) and isinstance(b,dict):return set(a)==set(b) and all(same(a[k],b[k]) for k in a)
        return a==b
    for spec in json.loads((GEN/manifest).read_text()):
        path=GEN/spec['file'];doc=json.loads(path.read_text());inputs[spec['file']]=sha(path)
        for entity in doc['instances']:
            if entity['prototype'] not in IDS:continue
            e=expected[(spec['variant'],spec['level'],entity['id'])]
            for key in ('position','modelId','renderYaw','support'):assert same(entity.get(key),e.get(key)),(entity['id'],key)
            assert same(entity['yaw'],e['savedYaw']) and same(entity.get('renderOffset',[0,0,0]),e['renderOffset'])
            assert entity['matchKind']=='exact'
            m=models[entity['modelId']];parts=doc['geometryVariants'].get(entity.get('geometryKey'),lib[m['id']]['parts'])
            assert same(parts,m['parts'])
            own=world_parts(parts,entity['position'],entity['renderYaw'],entity.get('renderOffset',[0,0,0]))
            for n in doc['instances']:
                if n.get('modelId') in baseline_ids or n.get('modelId') in models or n.get('modelId') not in lib:continue
                if sum((a-b)**2 for a,b in zip(n['position'][:2],entity['position'][:2]))>1.55**2:continue
                nm=lib[n['modelId']];np=doc['geometryVariants'].get(n.get('geometryKey'),nm['parts'])
                other=world_parts(np,n['position'],n.get('renderYaw',n['yaw']),n.get('renderOffset',[0,0,0]))
                hits,gap=contacts(own,other);contact_witnesses(hits,own,other)
                new_neighbors.append(dict(variant=spec['variant'],level=spec['level'],id=e['id'],neighborId=n['id'],prototype=n['prototype'],contacts=hits,minimumAabbSeparation=gap))
            checks.append(dict(variant=spec['variant'],level=spec['level'],id=e['id'],modelId=m['id'],sourceTransformUnchanged=True,
                renderYawOffsetSupportEqual=True,geometryEqual=True))
    assert len(checks)==14
    proof.update(finalSceneParity=checks,finalSceneInputsSha256=inputs,newFamilyNeighborChecks=new_neighbors,
        newFamilyNeighborScope='All other newly mapped model IDs versus978 baseline within1.55 tiles of every target in final eight scenes; same-family planks already included in complete context.',
        status='written assets and all14 final source/render/support placements verified; recorded support limitations remain')
    regeneration=json.loads((ROOT/'.codex/loose-planks-regeneration-check/comparison.json').read_text())
    for e in regeneration['files']:assert sha(ROOT/e['path'])==e['sha256']
    proof['deterministicRegeneration']=regeneration;save_proof(proof)
    print(json.dumps(dict(finalPlacements=14,proofSha256=sha(GEN/'loose-planks-proof.json'))))


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--reviews-only',action='store_true');parser.add_argument('--check-assets',action='store_true');parser.add_argument('--export-parity');parser.add_argument('--finalize-proof',action='store_true');args=parser.parse_args()
    if args.check_assets:check_assets();return
    if args.export_parity:export_parity(args.export_parity);return
    if args.reviews_only or args.finalize_proof:models=bm.load_models(MODEL);proof=json.loads((GEN/'loose-planks-proof.json').read_text())
    else:models,proof=author()
    notes();written_proof(models,proof)
    if not args.finalize_proof:cards(models);context(models,proof)
    finalize_proof(models,proof)


if __name__=='__main__':main()
