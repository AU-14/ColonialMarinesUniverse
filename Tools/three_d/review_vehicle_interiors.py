"""CMU14: render fleet cabins at saved map transforms, plus a labeled cutaway."""
import argparse
from copy import deepcopy
import json
import math
import re
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

import build_models as bm
import inventory
import scene
from author_vehicle_interiors import ROOT, REVIEW, collect, NATIVE
from author_redux_coverage import part, write


def transform_parts(parts,x,y,yaw):
    result=[];c,s=math.cos(yaw),math.sin(yaw)
    for p in parts:
        q=deepcopy(p)
        cx,cy=[(p['min'][i]+p['max'][i])/2 for i in range(2)]
        dx,dy=c*cx-s*cy+x-cx,s*cx+c*cy+y-cy
        for key in ('min','max'):
            q[key]=list(q[key]);q[key][0]+=dx;q[key][1]+=dy
        q['yaw']=q.get('yaw',0)+math.degrees(yaw)
        result.append(q)
    return result


def cutaway(parts):
    result=[]
    for p in parts:
        structural=any(t in p.get('label','') for t in ('bulkhead','hull wall','hull panel','door panel','window pillar','roof','overhead','armor trim','Hull','hull sill'))
        if structural and p['min'][2]>.9:continue
        q=deepcopy(p)
        if structural and q['max'][2]>.9:q['max'][2]=.9
        result.append(q)
    return result


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cache',type=Path)
    args=parser.parse_args()
    camera=(ROOT/'Content.CMU/Client/ThreeD/CMU3DFirstPersonCamera.cs').read_text()
    eye_height=float(re.search(r'const float EyeHeight = ([\d.]+)f;',camera).group(1))
    if args.cache:kinds=json.loads(args.cache.read_text())
    else:
        kinds,issues,_=inventory.load_prototypes(ROOT);assert not issues,issues
    report=collect(kinds);resolver=inventory.Resolver(kinds['entity'])
    models=bm.load_models(bm.SOURCE);library={m['id']:m for m in models}
    refs={uid:m for m in models for uid in m.get('sourcePrototypes',[])}
    font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',16)
    overview=Image.new('RGB',(1440,400*7),'#17212B');od=ImageDraw.Draw(overview)
    checks=[]
    for index,(path,vehicles) in enumerate(sorted(report['maps'].items())):
        header,records=scene.read_map(inventory.resource_path(ROOT,path))
        world=scene.WorldTransforms(records);hidden=scene.hidden_container_entities(records)
        parts=[];unmodeled=[];seats=[];objects=[]
        for uid,record in records.items():
            proto=record['prototype']
            if uid in hidden:continue
            effective=inventory.component_map(resolver.resolve(proto)) if proto else {}
            for key, value in record['components'].items():
                effective[key]={**effective.get(key,{}),**value}
            x,y,yaw,_=world.resolve(uid)
            grid=effective.get('MapGrid')
            if grid:
                for chunk in grid.get('chunks',{}).values():
                    for tile in scene.decode_chunk(chunk):
                        tid=header['tilemap'][tile['palette']]
                        if tid in ('Space','RMCVoid'):continue
                        shade='#47524B' if (tile['x']+tile['y'])%2 else '#414C46'
                        parts+=transform_parts([part('native floor',[tile['x'],tile['y'],-.04],[tile['x']+1,tile['y']+1,0],shade)],x,y,yaw)
            if not proto or proto in NATIVE or 'Sprite' not in effective:continue
            if proto not in refs:unmodeled.append(proto);continue
            m=refs[proto]
            if 'Foldable' in effective and m.get('folded',False)!=effective['Foldable'].get('folded',False):m=library[m['alternateFoldModel']]
            sprite=effective['Sprite']
            if not m.get('useEntityRotation') and sprite.get('noRot'):yaw=0
            yaw+=math.radians(m.get('yawOffset',0))
            offset=m.get('groundOffset',(0,0));offset=scene.finite_vector(offset)
            if m.get('wallMounted'):
                # Cabin art has no IconSmooth tile walls; mirror the live cabin anchor correction.
                offset=(offset[0]-.5*math.sin(yaw),offset[1]+.5*math.cos(yaw))
            moved=transform_parts(m['parts'],x+offset[0],y+offset[1],yaw)
            parts+=moved;objects.append(dict(prototype=proto,parts=moved,x=x,y=y))
            if 'Strap' in effective:seats.append((proto,x,y))
        assert not unmodeled,(path,unmodeled)
        canvas=Image.new('RGB',(1440,660),'#17212B');d=ImageDraw.Draw(canvas)
        d.text((12,8),path+' | '+', '.join(vehicles),font=font,fill='white')
        for j,(label,geometry,yaw,pitch) in enumerate((('Assembled cabin',parts,-1.15,.75),('Cutaway: upper walls and roof removed',cutaway(parts),-1.15,1.0),('Cutaway: reverse view',cutaway(parts),1.9,.75))):
            d.text((j*480+10,42),label,font=font,fill='#CBD7CC')
            canvas.paste(bm.render_model({'parts':geometry},(480,580),yaw,pitch),(j*480,70))
        name=f'layout-{index+1:02}.png';canvas.save(REVIEW/name)
        row,col=divmod(index,4)
        thumb=bm.render_model({'parts':cutaway(parts)},(360,340),-1.15,1.0)
        overview.paste(thumb,(col*360,row*400+50));od.text((col*360+7,row*400+5),f'{index+1:02} '+path.split('/')[-1],font=font,fill='white')
        # Flag another entity's structure crossing a seated eye, not the seat's
        # own backrest. Inspect these against source placement before changing art.
        crossings=[]
        for seat,sx,sy in seats:
            for obj in objects:
                if obj['prototype']==seat and abs(obj['x']-sx)<.001 and abs(obj['y']-sy)<.001:continue
                for p in obj['parts']:
                    if p['min'][2]<=eye_height<=p['max'][2]:
                        center=[(a+b)/2 for a,b in zip(p['min'],p['max'])]
                        angle=math.radians(p.get('yaw',0));c,s=math.cos(angle),math.sin(angle)
                        dx,dy=sx-center[0],sy-center[1]
                        lx,ly=c*dx+s*dy,-s*dx+c*dy
                        if abs(lx)<(p['max'][0]-p['min'][0])/2 and abs(ly)<(p['max'][1]-p['min'][1])/2:
                            crossings.append(dict(seat=seat,object=obj['prototype'],part=p.get('label')))
                            break
        checks.append(dict(map=path,review=name,parts=len(parts),eyeHeight=eye_height,seatedEyeCrossings=crossings))
        print(path,'parts',len(parts),'eye crossings',crossings,flush=True)
    overview.save(REVIEW/'overview.png')
    write(REVIEW/'layout-review.json',json.dumps(checks,indent=2)+'\n')
    # Fighter positions come from FighterSystem.CreateAircraft, all facing north.
    parts=[]
    for uid,x,y,rot in [('CMUFighterHull',.5,0,math.pi),('CMUFighterCanopy',.5,0,math.pi),('CMUFighterPilotSeat',.5,4.2,math.pi),('CMUFighterObserverSeat',.5,2.1,math.pi),('AU14VehicleRadioSet',1.5,3.5,0)]:
        parts+=transform_parts(refs[uid]['parts'],x,y,rot)
    bm.render_model({'parts':parts},(1100,800),-1.2,.65).save(REVIEW/'fighter-cockpit.png')


if __name__=='__main__':main()
