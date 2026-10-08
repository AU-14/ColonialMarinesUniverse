"""Consolidate the existing corner into source-textured beams without changing its union.

Only CMU3DHybrisaPlatformThreeCorner is rewritten. The eleven original colored
cap strips and the clipped east support's fractional edge remain explicit solids.
"""
from copy import deepcopy
import hashlib
import itertools
import json
import math
from pathlib import Path
import re

from PIL import Image, ImageDraw, ImageFont, ImageOps
import yaml
import build_models
import surfaces
from author_hybrisa_platform_three import decoded_parts, rotated_bounds, union_proof, contains, rotate

ROOT=Path(__file__).resolve().parents[2]
PROTOTYPES=ROOT/'Content.CMU/Resources/Prototypes/CMU14/ThreeD'
MODEL_FILE=PROTOTYPES/'garrison_environment.yml'
ART_FILE=PROTOTYPES/'garrison_hybrisa_platform_three_corner_art.yml'
MODEL='CMU3DHybrisaPlatformThreeCorner'
STRAIGHT='CMU3DHybrisaPlatformThree'
SURFACE='CMU3DPlatformThreeCornerTrimmedSupport'
SLOT=1204
TEXTURES=ROOT/'Content.CMU/Resources/Textures/CMU14/ThreeD/Surfaces'
BASE=ROOT/'.codex/platform-three-corner-baseline'
GENERATED=ROOT/'Tools/three_d/generated'
REVIEW=GENERATED/'review/platform-three-corner'
RSI=ROOT/'Resources/Textures/_RMC14/Structures/platforms.rsi'
SOURCE=RSI/'hybrisaplatform3_corner.png'


def digest(data):
    return hashlib.sha256(data).hexdigest()


def sample(point,part,images):
    if not part.get('surface'):
        rgba=bytes.fromhex(part['color'][1:])
        return tuple(rgba)+(255,) if len(rgba)==3 else tuple(rgba)
    local=[(point[i]-part['min'][i])/(part['max'][i]-part['min'][i])-.5 for i in range(3)]
    u,v=surfaces.uv(local,part['surfaceAxis'])
    if part.get('surfaceFlipU'):u=1-u
    image=images[part['surface']]
    return image.getpixel((min(image.width-1,max(0,math.floor(u*image.width))),
                           min(image.height-1,max(0,math.floor(v*image.height)))))


def color_proof(before,after,images,turn):
    """Partition actual rotated bounds; pull sample positions back into source UV space."""
    old=rotated_bounds(before,turn);new=rotated_bounds(after,turn)
    cuts=[sorted({p[key][axis] for p in old+new for key in ('min','max')}) for axis in range(3)]
    centers=[[(a+b)/2 for a,b in zip(cut,cut[1:])] for cut in cuts]
    occupied={};partition_count=0
    def rgba(world,part):return sample(rotate(world,-turn),part,images)
    for cell in itertools.product(*(range(len(c)) for c in centers)):
        point=[centers[i][cell[i]] for i in range(3)]
        previous=[i for i,p in enumerate(old) if contains(point,p)]
        current=[i for i,p in enumerate(new) if contains(point,p)]
        assert bool(previous)==bool(current),(turn,point)
        if previous:
            assert len(previous)==len(current)==1
            oi,ni=previous[0],current[0]
            assert rgba(point,before[oi])==rgba(point,after[ni]),(turn,point)
            occupied[cell]=(oi,ni)
        partition_count+=1
    faces={}
    for cell,(oi,ni) in occupied.items():
        for axis in range(3):
            for side in (-1,1):
                neighbor=list(cell);neighbor[axis]+=side
                if tuple(neighbor) in occupied:continue
                point=[centers[i][cell[i]] for i in range(3)]
                point[axis]=cuts[axis][cell[axis]+(1 if side>0 else 0)]-side*1e-9
                assert rgba(point,before[oi])==rgba(point,after[ni]),(turn,cell,axis,side)
                key='XYZ'[axis]+('+' if side>0 else '-')
                faces[key]=faces.get(key,0)+1
    return dict(turn=turn,partitionCells=partition_count,occupiedPartitionCellSamples=len(occupied),allRgbaEqual=True,
                exposedFaceSamples=faces,totalExposedFaceSamples=sum(faces.values()),
                method='Compare actual written texture RGBA through XZ/YZ UV projection and surfaceFlipU against the frozen colored solids, at every occupied rotated partition center and just inside every exposed axis face.')


def source_reviews(model,inventory):
    overview=Image.new('RGB',(1200,470),'#101820');draw=ImageDraw.Draw(overview)
    font=ImageFont.load_default(size=16);small=ImageFont.load_default(size=13)
    draw.text((16,12),'PLATFORM THREE CORNER / preserved geometry / actual source facings / DRAFT',fill='#F0E3BE',font=font)
    draw.text((16,38),'73 solids to 22. Eleven original cap strips retained; east clipped support keeps its fractional edge pixel.',fill='#AAC0CD',font=small)
    sheet=Image.open(SOURCE).convert('RGBA');meta=json.loads((RSI/'meta.json').read_text())
    state=next(s for s in meta['states'] if s['name']=='hybrisaplatform3_corner')
    assert state.get('directions',1)==4 and 'delays' not in state
    w,h=meta['size']['x'],meta['size']['y'];references=[];posed_models=[]
    for turn,name in enumerate(('South','East','North','West')):
        direction=(0,2,1,3)[turn]
        x,y=direction%(sheet.width//w)*w,direction//(sheet.width//w)*h
        expected=sheet.crop((x,y,x+w,y+h))
        posed=deepcopy(model);posed.update(id=MODEL+'Review'+name,label='Platform Three corner / '+name,
                                          referenceState='hybrisaplatform3_corner',referenceDirection=direction)
        for part in posed['parts']:
            center=[(a+b)/2 for a,b in zip(part['min'],part['max'])];half=[(b-a)/2 for a,b in zip(part['min'],part['max'])]
            center=rotate(center,turn)
            part.update(min=[c-r for c,r in zip(center,half)],max=[c+r for c,r in zip(center,half)],yaw=turn*90)
        reference,origin=build_models.reference_frame(posed,inventory)
        assert reference is not None and reference.size==expected.size and reference.tobytes()==expected.tobytes()
        reference_path=REVIEW/('source-'+name.lower()+'.png');reference.save(reference_path)
        references.append(dict(sourceFacing=name,rsiDirection=direction,savedYaw=turn*math.pi/2,
                               source=SOURCE.relative_to(ROOT).as_posix(),state='hybrisaplatform3_corner',
                               frameRect=[x,y,w,h],file=reference_path.relative_to(GENERATED).as_posix(),
                               rgbaSha256=digest(expected.tobytes())))
        column=turn*300
        draw.text((column+20,70),name+' / saved yaw '+str(turn*90),fill='#F0E3BE',font=font)
        scaled=reference.resize((192,192),Image.Resampling.NEAREST);overview.paste(scaled,(column+54,100),scaled)
        top=build_models.render_model(posed,(280,160),yaw=-math.pi/2,pitch=math.pi/2,pixels_per_unit=120,screen_origin=(140,80))
        overview.paste(top,(column+10,300));posed_models.append(posed)
    build_models.write_reviews(posed_models,REVIEW/'source-facings',inventory)
    overview.save(REVIEW/'source-facing-overview.png')
    (REVIEW/'source-facing-references.json').write_text(json.dumps(references,indent=2)+'\n')
    return references


def main():
    baseline=yaml.safe_load((BASE/'target-record.yml').read_text())[0]
    assert baseline['id']==MODEL and len(baseline['parts'])==73
    raw=MODEL_FILE.read_text();blocks=re.split(r'(?=^- type: cmu3DModel\s*$)',raw,flags=re.M)
    matches=[i for i,b in enumerate(blocks) if re.search(r'^  id: '+MODEL+r'$',b,re.M)]
    assert len(matches)==1;index=matches[0];model=yaml.safe_load(blocks[index])[0]
    metadata={k:v for k,v in baseline.items() if k!='parts'}
    assert {k:v for k,v in model.items() if k!='parts'}==metadata and len(model['parts']) in (73,22)
    straight=next(p for p in yaml.safe_load(raw) if p.get('id')==STRAIGHT)
    frozen_straight=next(p for p in yaml.safe_load((BASE/'garrison_environment.yml').read_text()) if p.get('id')==STRAIGHT)
    assert straight==frozen_straight and len(straight['parts'])==6
    before=decoded_parts(baseline);front=decoded_parts(straight)[:5]
    cap_raw=[deepcopy(p) for p in baseline['parts'] if p['label'].startswith('source cap')]
    caps=[deepcopy(p) for p in before if p['label'].startswith('source cap')];assert len(caps)==11
    images={name:Image.open(TEXTURES/(name+'.png')).convert('RGBA') for name in {p['surface'] for p in front}}
    support=images['CMU3DPlatformThreeSupport'];assert support.size==(9,4)
    # Prove the shared support itself still matches its source RSI crop.
    straight_sheet=Image.open(RSI/'hybrisaplatform3.png').convert('RGBA')
    assert support.tobytes()==ImageOps.mirror(straight_sheet.crop((51,10,60,14))).tobytes()
    trimmed=support.crop((1,0,9,4));assert trimmed.size==(8,4) and trimmed.getchannel('A').getextrema()==(255,255)
    existing=[s for p in PROTOTYPES.glob('*.yml') for s in (yaml.load(p.read_text(),Loader=yaml.CSafeLoader) or []) if s.get('type')=='cmu3DSurface']
    assert not any(s['atlasIndex']==SLOT and s['id']!=SURFACE for s in existing)
    assert not any(s['id']==SURFACE and s['atlasIndex']!=SLOT for s in existing)
    texture_path=TEXTURES/(SURFACE+'.png');trimmed.save(texture_path)
    written=Image.open(texture_path).convert('RGBA');assert written.size==trimmed.size and written.tobytes()==trimmed.tobytes();images[SURFACE]=written
    east=[];fractional=None
    for original in front:
        part=deepcopy(original);part.update(min=[-original['max'][1],max(-.36,original['min'][0]),original['min'][2]],
                                            max=[-original['min'][1],original['max'][0],original['max'][2]],
                                            surfaceAxis='YZ',surfaceFlipU=True,label='east '+original['label'])
        if original['min'][0]==-.375:
            colors={support.getpixel((0,y)) for y in range(4)};assert len(colors)==1
            color,=colors;assert color[3]==255
            strip={k:deepcopy(v) for k,v in part.items() if k not in ('surface','surfaceAxis','surfaceFlipU')}
            strip['max'][1]=-.34375;strip['color']='#'+bytes(color[:3]).hex().upper();strip['label']+=' fractional edge pixel'
            east.append(strip);fractional=deepcopy(strip)
            part['min'][1]=-.34375;part['surface']=SURFACE
        east.append(part)
    after=front+east+caps;assert len(after)==22 and fractional is not None
    assert after[-11:]==caps
    proposal=json.loads((BASE/'proposal.json').read_text());expected=deepcopy(proposal['candidateParts'])
    for p in expected:
        if p.get('surface')=='PROPOSAL_PlatformThreeCornerTrimmedSupport':p['surface']=SURFACE
    assert after==expected
    art=[dict(type='cmu3DSurface',id=SURFACE,atlasIndex=SLOT,texture='/Textures/CMU14/ThreeD/Surfaces/'+SURFACE+'.png')]
    ART_FILE.write_text('# Exact CC-BY-SA-3.0 crop; see SOURCES_PLATFORM_THREE_CORNER.md.\n'+yaml.safe_dump(art,sort_keys=False))
    rotations=[]
    for turn in range(4):
        rotations.append(dict(turn=turn,yaw=turn*math.pi/2,sourceDirection=(0,2,1,3)[turn],
                              union=union_proof(rotated_bounds(before,turn),rotated_bounds(after,turn)),
                              colors=color_proof(before,after,images,turn)))
    model['parts']=[{**p,'min':', '.join(f'{v:.7f}' for v in p['min']),'max':', '.join(f'{v:.7f}' for v in p['max'])} for p in after[:-11]]+cap_raw
    blocks[index]=yaml.safe_dump([model],sort_keys=False,width=112)+'\n';updated=''.join(blocks)
    # Do not replace another writer's simultaneous changes outside this record.
    assert MODEL_FILE.read_text()==raw,'Model file changed during generation; rerun without replacing it'
    MODEL_FILE.write_text(updated)
    written_blocks=re.split(r'(?=^- type: cmu3DModel\s*$)',MODEL_FILE.read_text(),flags=re.M)
    original_blocks=re.split(r'(?=^- type: cmu3DModel\s*$)',raw,flags=re.M)
    assert len(written_blocks)==len(original_blocks) and all(a==b for i,(a,b) in enumerate(zip(written_blocks,original_blocks)) if i!=index)
    written_model=yaml.safe_load(written_blocks[index])[0]
    assert {k:v for k,v in written_model.items() if k!='parts'}==metadata
    assert written_model['parts'][-11:]==cap_raw
    surfaces.load_surfaces.cache_clear();loaded=build_models.validate_model(written_model)
    actual=decoded_parts(loaded);assert actual==after
    # Repeat against serialized YAML and re-opened output textures, not only the candidate.
    for turn in range(4):assert color_proof(before,actual,images,turn)==rotations[turn]['colors']
    inv={e['id']:e for e in json.loads((GENERATED/'inventory.json').read_text())['prototypes']}
    REVIEW.mkdir(parents=True,exist_ok=True);build_models.write_reviews([loaded],REVIEW,inv)
    references=source_reviews(loaded,inv)
    meta=json.loads((RSI/'meta.json').read_text())
    report=dict(schemaVersion=1,model=MODEL,previousParts=73,parts=22,partsRemoved=51,baseline='Frozen target-record.yml in .codex/platform-three-corner-baseline',
                originalTargetSha256=digest((BASE/'target-record.yml').read_bytes()),metadataUnchanged=list(metadata),otherModelRecordsUnchanged=True,
                straightPlatformRecordUnchanged=True,capStripsPreserved=11,capPartRecordsExactlyPreserved=True,fractionalEastSupportEdge=fractional,
                newTexture=dict(surface=SURFACE,atlasIndex=SLOT,size=list(written.size),path=texture_path.relative_to(ROOT).as_posix(),
                                rgbaSha256=digest(written.tobytes()),sourceSurface='CMU3DPlatformThreeSupport',crop=[1,0,9,4],allOpaque=True),
                reusedSurfaces=sorted({p['surface'] for p in front}),sourceReferences=references,rotations=rotations,
                proofTotals=dict(rotationCount=4,partitionCells=sum(r['union']['partitionCells'] for r in rotations),
                                 occupiedColorSamples=sum(r['colors']['occupiedPartitionCellSamples'] for r in rotations),
                                 exposedFaceColorSamples=sum(r['colors']['totalExposedFaceSamples'] for r in rotations)),
                sourceFiles={p.relative_to(ROOT).as_posix():digest(p.read_bytes()) for p in (SOURCE,RSI/'hybrisaplatform3.png',RSI/'meta.json',MODEL_FILE,ART_FILE,texture_path,Path(__file__))},
                license=meta['license'],copyright=meta['copyright'],savedMapEdits=0,
                limitations=['Physical construction remains the existing inferred draft; this reduction preserves shape, source projection and exposed colors rather than approving new fidelity.',
                             'Source-facing references are the four actual hybrisaplatform3_corner RSI frames; their original directional shading differs from one rotated physical assembly.',
                             'No global model/scene/GLB exports, native admission audit, build, game or server launch is performed. The parent coordinates those checks.',
                             'Other platform variants and broken/construction states remain outside this consolidation.'])
    (GENERATED/'platform-three-corner-verification.json').write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps({k:report[k] for k in ('model','previousParts','parts','capStripsPreserved','proofTotals')},indent=2))

if __name__=='__main__':main()
