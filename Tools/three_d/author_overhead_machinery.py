"""Source-frame cabinet drafts; physical depth/elevation are explicit inferences.

The source image width describes the upright front, not a broad box across the
wall/aisle. Its alpha-envelope center plus Sprite.offset is a map-X placement
offset. All four direction slots and every animation frame remain explicit.
"""
import copy
import hashlib
import json
import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont
import yaml

import build_models

ROOT=Path(__file__).resolve().parents[2]
PROTOTYPES=ROOT/'Content.CMU/Resources/Prototypes/CMU14/ThreeD'
MODEL_FILE=PROTOTYPES/'garrison_overhead_machinery.yml'
ART_FILE=PROTOTYPES/'garrison_overhead_machinery_art.yml'
OLD_FILE=PROTOTYPES/'garrison_machinery_debris.yml'
RSI='_RMC14/Structures/hybrisa_machine_props.rsi'
SOURCE=ROOT/'Resources/Textures'/RSI
TEXTURES=ROOT/'Content.CMU/Resources/Textures/CMU14/ThreeD/Surfaces'
REVIEW=ROOT/'Tools/three_d/generated/review/overhead-machinery'
EVIDENCE=ROOT/'Tools/three_d/generated/overhead-machinery-source-audit.json'
Z_TOP=2.10
DIRECTIONS=('South','North','East','West')
FACINGS=(1,3,1,3)  # Physical cardinal facing per RSI S/N/E/W slot.


class SurfacePool:
    def __init__(self):
        self.by_hash={}
        self.art=[]
        self.evidence=[]

    def crop(self,frame,rect,context):
        pixels=frame.crop(rect)
        signature=hashlib.sha256(str(pixels.size).encode()+pixels.tobytes()).hexdigest()
        if signature not in self.by_hash:
            index=1100+len(self.art)
            assert index<1200,'Reserved overhead atlas range exhausted'
            uid=f'CMU3DOverheadSurface{index}'
            pixels.save(TEXTURES/(uid+'.png'))
            self.by_hash[signature]=uid
            self.art.append(dict(type='cmu3DSurface',id=uid,atlasIndex=index,
                texture=f'/Textures/CMU14/ThreeD/Surfaces/{uid}.png'))
        uid=self.by_hash[signature]
        actual=Image.open(TEXTURES/(uid+'.png')).convert('RGBA')
        assert actual.size==pixels.size and actual.tobytes()==pixels.tobytes()
        self.evidence.append(dict(**context,crop=list(rect),surface=uid,
            exactRgbaPixels=pixels.width*pixels.height,sha256=signature))
        return uid


def rsi_frame(state,direction,frame_index,meta):
    row=next(s for s in meta['states'] if s['name']==state)
    delays=row.get('delays') or [[1.0]]*row.get('directions',1)
    flat=sum(len(d) for d in delays[:direction])+frame_index
    sheet=Image.open(SOURCE/(state+'.png')).convert('RGBA')
    columns=sheet.width//64
    x,y=flat%columns*64,flat//columns*64
    return sheet.crop((x,y,x+64,y+64)),delays[direction]


def composition(design,state,direction,frame_index,meta,pool):
    frame,delays=rsi_frame(state,direction,frame_index,meta)
    bounds=frame.getbbox()
    assert bounds and bounds[1:4:2]==(0,64)
    width=bounds[2]-bounds[0]
    mirrored=direction in (1,3)
    center=(bounds[0]+bounds[2])/2
    on,_=rsi_frame(f'buildingventbig{design}',direction,0,meta)
    on_bounds=on.getbbox()
    center_shift=(center-(on_bounds[0]+on_bounds[2])/2)/32
    # A physical East/West facing maps canonical depth onto map X.
    depth_shift=-center_shift/(1 if FACINGS[direction]==1 else -1)
    parts=[]
    reconstructed=Image.new('RGBA',(64,64))
    context=dict(design=design,state=state,direction=direction,frame=frame_index)

    def source_rect(rect):
        x0,y0,x1,y1=rect
        if mirrored:x0,x1=width-x1,width-x0
        return bounds[0]+x0,y0,bounds[0]+x1,y1

    def solid(label,rect,color,near=-.115,far=.140,shape='Box'):
        x0,y0,x1,y1=source_rect(rect)
        # The 33-pixel design meets a south wall in two actual corner placements.
        # A 7% inferred width reduction also clears protruding rock face geometry.
        width_scale=.93 if design==11 else 1.0
        # Compress inferred case depth around its own center, never its real
        # source-state translation. The shifted North off pose clears the platform cap.
        depth_scale=.54 if design==11 else 1.0
        low=[(x0-center)/32*width_scale,near*depth_scale+depth_shift,Z_TOP-y1/32]
        high=[(x1-center)/32*width_scale,far*depth_scale+depth_shift,Z_TOP-y0/32]
        item=dict(label=label,min=low,max=high,color=color)
        if shape!='Box':item['shape']=shape
        parts.append(item)

    def surface(label,rect,depth):
        crop=source_rect(rect)
        uid=pool.crop(frame,crop,dict(**context,region=label))
        reconstructed.paste(frame.crop(crop),(crop[0],crop[1]))
        solid(label,rect,'#FFFFFF',depth,depth+.003)
        parts[-1].update(surface=uid,surfaceAxis='XZ')

    if design==11:
        assert width==33
        solid('main cast cabinet',(0,1,19,63),'#595B58')
        solid('narrow upper cap',(1,0,17,1),'#595B58')
        solid('dark rear service spine',(19,3,25,64),'#1E1F1E',.035,.14)
        solid('separate narrow control pod',(25,12,33,55),'#332F2E',-.100,.13)
        solid('pod horizontal connector',(18,39,26,41),'#1E1F1E',-.034,.034,'CylinderX')
        solid('lower round control housing',(1,40,18,62),'#363835',-.148,-.107,'CylinderY')
        for label,rect,depth in [
            ('top cast cap',(0,0,19,6),-.140),
            ('upper recessed grille',(0,6,19,15),-.131),
            ('middle cast divider',(0,15,19,21),-.140),
            ('second recessed grille',(0,21,19,30),-.131),
            ('paired status sockets',(0,30,19,39),-.142),
            ('round lower control artwork',(0,39,19,64),-.151),
            ('rear spine source pixels',(19,0,25,64),.028),
            ('separate side control artwork',(25,0,33,64),-.112),
        ]:surface(label,rect,depth)
    else:
        assert width==28
        if design==12:
            solid('continuous tall cabinet',(4,1,23,63),'#454744')
            solid('upper side casing',(23,4,27,29),'#393B38',.010,.14)
        else:
            solid('upper cabinet above side notch',(4,1,23,20),'#454744')
            solid('narrowed mid-case at source notch',(4,20,21,26),'#454744')
            solid('lower cabinet below side notch',(4,26,23,63),'#454744')
            solid('short upper side shoulder',(23,4,25,18),'#393B38',.010,.14)
            solid('small lower side shoulder',(23,26,27,30),'#393B38',.010,.14)
        solid('cast top shoulder',(4,0,21,1),'#595B58')
        solid('dark rear mounting spine',(0,0,4,64),'#1E1F1E',.032,.14)
        solid('exposed side conduit',(24,30,28,62),'#595B58',.012,.137,'CylinderZ')
        for x0,x1 in [(10,14),(17,21)]:
            solid('copper vertical cartridge',(x0,15,x1,24),'#4E3A3A',-.151,-.072,'CylinderZ')
        for label,rect,depth in [
            ('upper perforated vent',(4,0,23,14),-.133),
            ('paired copper cartridge backing',(4,14,23,24),-.120),
            ('animated paired control bank',(4,24,23,38),-.144),
            ('upper lower-case grille',(4,38,23,50),-.131),
            ('bottom grille',(4,50,23,64),-.131),
            ('rear mounting spine artwork',(0,0,4,64),.027),
            ('source side-depth details',(23,0,28,64),.005),
        ]:surface(label,rect,depth)
    assert reconstructed.tobytes()==frame.tobytes(),context
    opaque_colors={p[:3] for p in frame.get_flattened_data() if p[3]}
    for p in parts:
        if not p.get('surface'):assert tuple(bytes.fromhex(p['color'][1:])) in opaque_colors,(context,p)
        for key in ('min','max'):p[key]=', '.join(f'{v:.7f}' for v in p[key])
    return parts,dict(**context,sourceAlphaBounds=list(bounds),sourceRgbaPreserved=True,
        fullCanvasPixels=4096,alphaEnvelopeCenterShiftWorldX=center_shift,
        canonicalDepthShift=depth_shift,parts=len(parts)),delays


def remove_superseded_record():
    text=OLD_FILE.read_text(encoding='utf-8')
    marker='- type: cmu3DModel\n  id: CMU3DRMCMachinePropBig11\n'
    if marker not in text:return
    start=text.index(marker)
    end=text.find('\n- type:',start+len(marker))
    if end<0:end=len(text)
    else:end+=1
    assert yaml.safe_load(text[start:end])[0]['id']=='CMU3DRMCMachinePropBig11'
    OLD_FILE.write_text(text[:start]+text[end:],encoding='utf-8')


def main():
    meta=json.loads((SOURCE/'meta.json').read_text())
    pool=SurfacePool()
    current={s['id']:s['atlasIndex'] for path in PROTOTYPES.glob('*.yml')
             for s in (yaml.load(path.read_text(),Loader=yaml.CSafeLoader) or []) if s['type']=='cmu3DSurface'}
    assert not [(uid,n) for uid,n in current.items() if 1100<=n<1200 and not uid.startswith('CMU3DOverheadSurface')]
    variants={}
    frame_evidence=[]
    for design in (11,12,13):
        for direction in range(4):
            states={}
            for state in (f'buildingventbig{design}',f'buildingventbig{design}_off'):
                _,delays=rsi_frame(state,direction,0,meta)
                frames=[]
                for frame_index in range(len(delays)):
                    parts,evidence,_=composition(design,state,direction,frame_index,meta,pool)
                    frames.append(dict(parts=parts));frame_evidence.append(evidence)
                states[state]=dict(frames=frames,delays=delays)
            variants[(design,direction)]=states
    ART_FILE.write_text('# Exact CC-BY-SA-3.0 source crops; see SOURCES_OVERHEAD_MACHINERY.md.\n'+
        yaml.safe_dump(pool.art,sort_keys=False,width=110),encoding='utf-8')
    models=[]
    for design in (11,12,13):
        for flipped in (False,True):
            suffix='Flipped' if flipped else ''
            proto=f'RMCMachinePropBig{design}{suffix}'
            root_id='CMU3D'+proto
            ids=[root_id]+[root_id+d for d in DIRECTIONS[1:]]
            offset=-.25 if flipped else .25
            for direction in range(4):
                frame,_=rsi_frame(f'buildingventbig{design}',direction,0,meta)
                bounds=frame.getbbox()
                ground_x=((bounds[0]+bounds[2])/2-32)/32+offset
                refs=([proto] if direction==0 else [])
                if design==11 and flipped and direction==0:refs.append('RMCMachinePropBaseOverheadFlipped')
                states=copy.deepcopy(variants[(design,direction)])
                models.append(dict(type='cmu3DModel',id=ids[direction],
                    label=f'Hybrisa cabinet {design} {suffix or "normal"} / {DIRECTIONS[direction]} source',
                    status='draft',sourcePrototypes=refs,referencePrototype=proto,
                    referenceRsi=RSI,referenceState=f'buildingventbig{design}',referenceDirection=direction,
                    sourceDirections=4,directionalModels=ids,sourceCardinalFacings=[1,1,3,3],
                    sourceSpriteOffset=f'{offset},0.5',groundOffset=f'{ground_x},0',
                    description='Shallow source-specific upright cabinet with solid case, separate vent/control '
                    'surfaces and exact source animation frames. Source S/E faces physically east; N/W west. '
                    'Ground X is the source alpha-envelope center plus authored horizontal Sprite offset; '
                    'screen-Y offset is not a ground-Y displacement. 2-tile height above Z .10, depth, back '
                    'and mounting construction are inferred. Design11 face width is scaled .93 to .9590625 tiles '
                    'to clear source wall corners and protruding rock without changing pixel crops or saved positions. '
                    'Its inferred case depth is .54 of the initial construction (.15714 tiles), preserving the '
                    'full source off-state shift while clearing neighboring platform fascia and cap. '
                    'Off resource frames are represented without '
                    'inventing a power controller. SpriteFade and live frame selection require native integration. '
                    'CC-BY-SA-3.0 cmss13 source; see SOURCES_OVERHEAD_MACHINERY.md.',
                    parts=copy.deepcopy(states[f'buildingventbig{design}']['frames'][0]['parts']),spriteStates=states))
    MODEL_FILE.write_text('# Source animation and placement variants. Original artwork CC-BY-SA-3.0.\n'+
        yaml.safe_dump(models,sort_keys=False,width=110),encoding='utf-8')
    remove_superseded_record()
    # Validate every frame independently even while the shared native contract is being integrated.
    loaded=[]
    for model in models:
        for state,definition in model['spriteStates'].items():
            assert len(definition['frames'])==len(definition['delays'])
            for frame in definition['frames']:
                build_models.validate_model({**{k:v for k,v in model.items() if k not in ('spriteStates','sourceSpriteOffset')},'parts':frame['parts']})
        loaded.append(build_models.validate_model(model))
    inventory=json.loads((ROOT/'Tools/three_d/generated/inventory.json').read_text())
    build_models.write_reviews(loaded,REVIEW,{e['id']:e for e in inventory['prototypes']})
    write_off_reviews(loaded,{e['id']:e for e in inventory['prototypes']})
    write_context_audit(loaded)
    EVIDENCE.write_text(json.dumps(dict(sourceRsi=RSI,license=meta['license'],copyright=meta['copyright'],
        modelCount=len(models),directMappings=sum(len(m['sourcePrototypes']) for m in models),
        uniqueTextureCount=len(pool.art),atlasIndices=[s['atlasIndex'] for s in pool.art],
        originalSourceFrameCompositions=len(frame_evidence),frameCompositions=frame_evidence,crops=pool.evidence,
        physicalFacingByRsiDirection=['East','West','East','West'],
        groundOffsetRule='Source alpha-envelope center in pixels plus Sprite.offset.X; never Sprite.offset.Y',
        inferredDimensions=dict(bottomZ=.10,topZ=2.10,maximumDepth=.291,big11FaceWidthScale=.93,
            big11FaceWidth=.9590625,big11DepthScale=.54,big11Depth=.15714),
        liveIntegrationStatus='Asset evidence only; native source-clock, alpha and packing verification is reported separately by the parent task'),indent=2)+'\n')
    print('Authored',len(models),'directional model records;',len(pool.art),'exact source surfaces;',
          len(frame_evidence),'source frame compositions;',sorted({len(m['parts']) for m in models}),'parts per design')


def write_off_reviews(models,inventory):
    """Review-only copies retain library IDs; no prototype/schema changes are written."""
    reviews=[]
    for original in models:
        if 'Flipped' in original['referencePrototype']:continue
        model=copy.deepcopy(original)
        state=original['referenceState']+'_off'
        model['parts']=copy.deepcopy(original['spriteStates'][state]['frames'][0]['parts'])
        model['referenceState']=state
        model['label']+=' / static OFF resource'
        reviews.append(model)
    assert len(reviews)==12
    build_models.write_reviews(reviews,REVIEW/'off-states',inventory)


def write_context_audit(models):
    """Use saved source neighbors and frozen exported geometry; contacts are conservative AABBs."""
    baseline=ROOT/'.codex/overhead-baseline'
    audit=json.loads((ROOT/'Tools/three_d/generated/next-overhead-machinery-audit.json').read_text())
    source_audit=json.loads((ROOT/'.codex/overhead-source-state-audit.json').read_text())
    placements=audit['placements']+source_audit['existingBig11Placements']['placements']
    library={m['id']:m for m in json.loads((baseline/'models.json').read_text())['models']}
    library.update({m['id']:m for m in models})
    direct={p:m for m in models for p in m['sourcePrototypes']}
    files={('classic',0):'scene.json',('redux',0):'redux-scene.json',
           ('redux',-1):'biomass-redux-minus1-scene.json',
           ('redux',-2):'containment-redux-minus2-scene.json',
           ('redux',1):'biomass-redux-plus1-scene.json'}
    scenes={k:json.loads((baseline/name).read_text()) for k,name in files.items()}
    by_uid={k:{i['id']:i for i in s['instances']} for k,s in scenes.items()}

    def own_model(record):
        turn=round(record['yaw']/(math.pi/2))%4
        direction=(0,2,1,3)[turn]
        model=library[direct[record['prototype']]['directionalModels'][direction]]
        return model,math.pi/2*(1,1,3,3)[turn]

    def transformed(parts,position,yaw,offset):
        c,s=math.cos(yaw),math.sin(yaw)
        result=[]
        for original in parts:
            p=copy.deepcopy(original)
            lo,hi=build_models.vector(p['min']),build_models.vector(p['max'])
            center=[(a+b)/2 for a,b in zip(lo,hi)]
            half=[(b-a)/2 for a,b in zip(lo,hi)]
            x,y=c*center[0]-s*center[1],s*center[0]+c*center[1]
            center=[x+position[0]+offset[0],y+position[1]+offset[1],center[2]+offset[2]]
            p.update(min=[v-h for v,h in zip(center,half)],max=[v+h for v,h in zip(center,half)],
                     yaw=p.get('yaw',0)+math.degrees(yaw))
            result.append(p)
        return result

    def own_parts(record,state=None):
        model,yaw=own_model(record)
        parts=model['spriteStates'][state]['frames'][0]['parts'] if state else model['parts']
        offset=list(model['groundOffset'])+[0]
        return transformed(parts,record['position'],yaw,offset)

    def neighbor_parts(record,key):
        if record['prototype'] in direct:return own_parts(record)
        model=library.get(record.get('modelId'))
        if model is None:return []
        parts=scenes[key]['geometryVariants'].get(record.get('geometryKey'),model['parts'])
        return transformed(parts,record['position'],record.get('renderYaw',record['yaw']),record.get('renderOffset',[0,0,0]))

    def contacts(first,second):
        def world_bounds(p):
            # build_models.part_bounds intentionally limits local model coordinates
            # to +/-32; these temporary audit parts are in saved world coordinates.
            lo,hi=p['min'],p['max']
            center=[(a+b)/2 for a,b in zip(lo,hi)]
            half=[(b-a)/2 for a,b in zip(lo,hi)]
            pitch=math.radians(p.get('pitch',0));cp,sp=abs(math.cos(pitch)),abs(math.sin(pitch))
            half=[half[0]*cp+half[2]*sp,half[1],half[0]*sp+half[2]*cp]
            yaw=math.radians(p.get('yaw',0));c,s=abs(math.cos(yaw)),abs(math.sin(yaw))
            extent=[half[0]*c+half[1]*s,half[0]*s+half[1]*c,half[2]]
            return [center[i]-extent[i] for i in range(3)],[center[i]+extent[i] for i in range(3)]
        hits=[]
        clearance=math.inf
        boxes=[(p,world_bounds(p)) for p in second]
        for a in first:
            lo,hi=world_bounds(a)
            for b,(blo,bhi) in boxes:
                overlap=[min(hi[i],bhi[i])-max(lo[i],blo[i]) for i in range(3)]
                gaps=[max(blo[i]-hi[i],lo[i]-bhi[i],0) for i in range(3)]
                clearance=min(clearance,math.sqrt(sum(g*g for g in gaps)))
                if all(v>1e-6 for v in overlap):
                    hits.append(dict(cabinetPart=a['label'],neighborPart=b['label'],overlap=overlap))
        return hits,clearance

    evidence=[]
    for record in placements:
        key=(record['variant'],record['level'])
        model,yaw=own_model(record)
        row=dict(variant=record['variant'],level=record['level'],id=record['id'],prototype=record['prototype'],
                 savedPosition=record['position'],savedYaw=record['yaw'],model=model['id'],
                 physicalFacing='East' if math.sin(yaw)>0 else 'West',groundOffset=model['groundOffset'],
                 stateContacts={},stateClearances={},unmodeledNeighbors=[])
        for state in model['spriteStates']:
            parts=own_parts(record,state)
            found=[]
            separations=[]
            for neighbor in record['neighbors']:
                if neighbor['id']==record['id']:continue
                exported=by_uid[key].get(neighbor['id'])
                if not exported or not exported.get('modelId') and exported['prototype'] not in direct:
                    if state==model['referenceState'] and abs(neighbor['delta'][0])<=1 and abs(neighbor['delta'][1])<=1:
                        row['unmodeledNeighbors'].append(dict(id=neighbor['id'],prototype=neighbor['prototype']))
                    continue
                other=neighbor_parts(exported,key)
                hits,clearance=contacts(parts,other)
                if math.isfinite(clearance):
                    separations.append(dict(id=neighbor['id'],prototype=neighbor['prototype'],
                        sameCenter=all(abs(d)<1e-6 for d in neighbor['delta']),minimumPartAabbSeparation=clearance))
                if hits:found.append(dict(id=neighbor['id'],prototype=neighbor['prototype'],
                    sameCenter=all(abs(d)<1e-6 for d in neighbor['delta']),conservativePartPairs=len(hits),
                    samples=hits[:8],assessment='Unresolved conservative contact; source co-location alone does not prove clearance'))
            row['stateContacts'][state]=found
            row['stateClearances'][state]=sorted(separations,key=lambda n:n['minimumPartAabbSeparation'])
        evidence.append(row)
    result=dict(placementCount=len(placements),newPlacements=35,existingBig11Companions=8,
        frameScope='Each default on-frame zero and off-frame zero; on geometry bounds remain constant across all five frames',
        method='Current cabinet parts vs frozen resolved scene geometry; positive 3D part AABB overlaps are candidates, not exact curved/alpha intersections',
        baselineSceneFiles=files_to_json(files),placements=evidence,
        limitations=['All neighbor geometry retains existing draft limitations',
          'Cylinder/ellipsoid and transparent texture AABBs can report contacts through empty space',
          'Unknown or unmapped neighbors are listed rather than claimed clear',
          'Native cell-budget, animated runtime and live SpriteFade validation belong to parent'])
    (REVIEW/'context-audit.json').write_text(json.dumps(result,indent=2)+'\n')
    pair_checks=0
    pair_contacts=[]
    closest=None
    for index,first in enumerate(placements):
        for second in placements[index+1:]:
            if (first['variant'],first['level'])!=(second['variant'],second['level']):continue
            for first_state in own_model(first)[0]['spriteStates']:
                for second_state in own_model(second)[0]['spriteStates']:
                    hits,clearance=contacts(own_parts(first,first_state),own_parts(second,second_state))
                    pair_checks+=1
                    pair=dict(variant=first['variant'],level=first['level'],firstId=first['id'],
                              firstState=first_state,secondId=second['id'],secondState=second_state,
                              minimumPartAabbSeparation=clearance)
                    if closest is None or clearance<closest['minimumPartAabbSeparation']:closest=pair
                    if hits:pair_contacts.append(dict(**pair,conservativePartPairs=len(hits),samples=hits[:8]))
    (REVIEW/'family-state-pair-audit.json').write_text(json.dumps(dict(
        placementCount=len(placements),pairStateChecks=pair_checks,contacts=pair_contacts,closest=closest,
        method='Every same-map/level family pair in every on/off state combination; geometry is constant across on frames',
        limitations='Part AABBs conservatively enclose curved and transparent geometry; this is not live runtime proof'),indent=2)+'\n')
    # Each review area contains separate local views when its targets are far apart.
    for area_index,area in enumerate(audit['reviewAreas']):
        targets=[p for p in placements if p['variant']=='redux' and p['level']==area['level'] and
                 p['id'] in area['targetSavedIds']+area.get('companionSavedIds',[])]
        sheet=Image.new('RGB',(1000,80+len(targets)*350),'#101820')
        draw=ImageDraw.Draw(sheet);font=ImageFont.load_default(size=16)
        draw.text((15,12),f'Saved overhead context area {area_index+1} / level {area["level"]} / OFFLINE DRAFT',fill='#F0E3BE',font=font)
        draw.text((15,39),'Actual saved XY/yaw + existing resolved neighbor geometry. Floor is a flat reference underlay.',fill='#AAC0CD',font=ImageFont.load_default(size=13))
        for index,record in enumerate(targets):
            key=('redux',record['level']);model,yaw=own_model(record)
            px,py=record['position'][:2]
            parts=[dict(label='reference floor',min=[px-1.5,py-1.5,-.008],max=[px+1.5,py+1.5,-.004],color='#3B4144')]
            for instance in scenes[key]['instances']:
                if abs(instance['position'][0]-px)>1.25 or abs(instance['position'][1]-py)>1.25:continue
                parts.extend(neighbor_parts(instance,key))
            if record['id'] not in by_uid[key]:
                parts.extend(own_parts(record))
            for p in parts:
                for k in ('min','max'):p[k]=[p[k][0]-px,p[k][1]-py,p[k][2]]
            assembled=dict(id='OverheadContextReview',label='Saved context',parts=parts)
            south_wall=any('Wall' in n['prototype'] and abs(n['delta'][0])<.1 and -.01>n['delta'][1]>-1.1 for n in record['neighbors'])
            oblique=.25 if (math.sin(yaw)>0)==south_wall else -.25
            panel=build_models.render_model(assembled,size=(740,320),yaw=(0 if math.sin(yaw)>0 else math.pi)+oblique,pitch=.65,
                pixels_per_unit=85,screen_origin=(370,240))
            y=75+index*350;sheet.paste(panel,(245,y))
            source,_=rsi_frame(model['referenceState'],model['referenceDirection'],0,json.loads((SOURCE/'meta.json').read_text()))
            source=source.resize((192,192),Image.Resampling.NEAREST)
            sheet.paste(source,(25,y+50),source)
            draw.text((15,y+5),f'UID {record["id"]} / {model["referenceDirection"]} source / '+('East' if math.sin(yaw)>0 else 'West')+' face',fill='#EEE4CE',font=font)
            draw.text((15,y+260),f'XY {px}, {py}',fill='#AAC0CD',font=font)
        sheet.save(REVIEW/f'context-area-{area_index+1}.png')
    print('Context audit:',len(evidence),'placements;',sum(bool(v) for e in evidence for v in e['stateContacts'].values()),
          'state/placement records have conservative contacts',flush=True)


def files_to_json(files):
    return [{'variant':variant,'level':level,'file':name} for (variant,level),name in files.items()]


if __name__=='__main__':main()
