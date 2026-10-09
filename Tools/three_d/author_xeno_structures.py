"""CMU14: reproduce the xeno structure library from resolved entity prototypes.

Usage: python Tools/three_d/author_xeno_structures.py [--inventory cached-kinds.json]
Only non-mob hive descendants are eligible. Source artwork remains authoritative.
"""
import argparse
import json
import math
from pathlib import Path

import yaml
from PIL import Image, ImageDraw, ImageFont
import inventory
import build_models as bm
import xeno_shapes as shapes
from author_redux_coverage import ROOT, WORLD, Sources, Pool, part, serialize, write

MODEL = WORLD / 'garrison_xeno_structures.yml'
DELIVERY = ROOT / 'Content.CMU/Resources/Models/CMU14/Garrison'
REVIEW = DELIVERY / 'Reviews/XenoStructures'
ART = WORLD / 'garrison_xeno_skin.yml'
TEXTURES = ROOT / 'Content.CMU/Resources/Textures/CMU14/ThreeD/XenoSkin'
MAINTENANCE = {'XenoTunnelMaint', 'XenoTunnelMaintNoXenoDesc', 'XenoTunnelMaintHybrisa', 'XenoTunnelMaintHybrisaNoXenoDesc'}


def collect(kinds):
    resolver = inventory.Resolver(kinds['entity'])
    rows = []
    for uid, raw in kinds['entity'].items():
        if raw.get('abstract'):
            continue
        try:
            entity = resolver.resolve(uid)
        except inventory.ResolutionError:
            continue
        if not any('/Structures/Xeno/' in kinds['entity'][p].get('_source', '').replace('\\', '/') for p in [uid, *entity['_ancestors']]):
            continue
        c = inventory.component_map(entity)
        if 'Sprite' not in c or 'MobState' in c or 'Xeno' in c:
            continue
        native = 'transient construction effect' if uid.startswith('CMU14Effect') else None
        if uid in MAINTENANCE:
            native = 'existing maintenance-cover model'
        rows.append(dict(id=uid, name=entity.get('name', uid), components=c, source=raw['_source'], native=native))
    return rows


def classify(row):
    uid = row['id'].lower(); c = row['components']
    if 'Door' in c:return 'door'
    if uid.endswith('weedswall'):return 'wallweeds'
    if 'IconSmooth' in c and c['IconSmooth'].get('key') == 'walls':return 'wall'
    for token, kind in [('fruit','fruit'),('egg','egg'),('nest','nest'),('spikes','spikes'),('sticky','sticky'),('fastresin','fast'),('collapse','collapse'),('resinhole','hole'),('tunnel','tunnel'),('trap','trap'),('sporesac','sac'),('sporecaster','sporecaster'),('cocoon','cocoon'),('plasma','plasma'),('recovery','recovery'),('pylon','pylon'),('acidpillar','acid'),('cluster','cluster'),('core','core'),('weed','weeds')]:
        if token in uid:
            return 'morpher' if 'morpher' in uid else kind
    raise ValueError(row['id'])


def metadata(rsi):
    path = inventory.resource_path(ROOT, inventory.texture_reference(rsi))
    return json.loads((path / 'meta.json').read_text(encoding='utf-8-sig'))


def geometry(row, rsi, state, frame=0, count=1):
    kind = classify(row); pale = 'Pathogen' in rsi or 'pathogen' in rsi; lower=state.lower()
    if 'weeds.rsi' in rsi or state in ('constructionnode','weednode'):
        if state=='nest_overlay':return shapes.nest(pale)
        return shapes.weeds(state,pale,kind=='wallweeds')
    if 'xeno_structure_node_indicator' == state:
        return shapes.organ('node',pale=pale)
    if lower=='closed_unlit':return []
    if kind=='wall':
        return shapes.wall(pale,'thick' in lower,'membrane' in lower,'bone' in lower,'weedbound' in lower)
    if kind=='door':
        progress=frame/max(1,count-1)
        if 'closing' in lower:progress=1-progress
        elif 'opening' not in lower:progress=1 if 'open' in lower else 0
        return shapes.door(progress,pale,'thick' in lower,'weedbound' in lower)
    if kind=='egg':return shapes.egg(state,frame,count,pale)
    if kind=='morpher' and state.startswith('eggmorph_'):
        # Source overlays add pairs of small eggs around the cradle, not one
        # oversized central egg. Keep the full eight-egg composition below 128.
        s=shapes.Sculpt(pale)
        for i in range(2+int(state[-1])*2):
            a=i*2.4;x,y,_=shapes.polar(.215,a,0);z=.49+(i%3)*.08
            s.bulb('cradled egg shell',[x,y,z],[.055,.053,.085],'#7D9299')
            s.bulb('cradled egg crown',[x,y,z+.062],[.037,.035,.045],'#9BA9AC')
            for j in range(4):
                b=j*math.tau/4
                s.vein('small egg seam',[x+.053*math.cos(b),y+.05*math.sin(b),z-.035],
                       [x+.031*math.cos(b),y+.03*math.sin(b),z+.074],.006,'#BCC4BC','CylinderX')
        return s.parts
    if state.endswith('_underlay'):
        s=shapes.Sculpt(pale);s.bulb('luminous spore vent',[0,0,.76],[.09,.09,.016],'#B3B284');return s.parts
    return shapes.organ(kind,state,frame,count,pale)


def source_layers(row):
    sprite=row['components']['Sprite'];result=[]
    for layer in sprite.get('layers') or [sprite]:
        state=layer.get('state');rsi=layer.get('sprite',sprite.get('sprite'))
        if state and rsi:result.append((rsi,state,layer.get('visible',True),layer))
    if not result:
        icon=row['components'].get('Icon',{})
        result.append((sprite.get('sprite',icon.get('sprite')),icon.get('state','weedwall'),True,{}))
    return result


def state_names(row,rsi,default):
    kind=classify(row);names=[s['name'] for s in metadata(rsi)['states']]
    if 'weeds.rsi' in rsi:
        return [s for s in names if s.startswith(('weed_dir','hive_weed')) or s.startswith('weed') and s[4:].isdigit() or s==default]
    if 'landmarks.rsi' in rsi or 'xeno_hud.rsi' in rsi:return [default]
    if kind=='door':
        c=row['components']['Door'];return [c[f'{st}SpriteState'] for st in ('closed','open','opening','closing')]+['closed_unlit']
    if kind=='egg':return [s for s in names if s.lower().startswith('egg')]
    if kind=='fruit':return list(dict.fromkeys([default,*[v for k,v in row['components']['XenoFruit'].items() if k.endswith('State')]]))
    if kind in ('hole','acid','cocoon','sac'):return names
    if kind=='morpher':return [s for s in names if s.startswith('eggmorph')]
    return [default]


def build(rows):
    models=[];sources=Sources();groups={};coverage=[]
    for row in rows:
        if row['native']:
            coverage.append(dict(id=row['id'],native=row['native']));continue
        c=row['components'];kind=classify(row);layers=source_layers(row)
        signature=json.dumps([kind,c['Sprite'],c.get('Door'),c.get('IconSmooth'),c.get('XenoFruit')],sort_keys=True)
        if signature in groups:
            model=groups[signature];model['sourcePrototypes'].append(row['id'])
            coverage.append(dict(id=row['id'],model=model['id']));continue
        # A composed default retains the roots, main organ and construction bud.
        parts=[]
        for rsi,state,visible,layer in layers:
            if visible:parts.extend(geometry(row,rsi,state))
        rsi,state,_,_=next((l for l in layers if 'weed' not in l[1] and 'indicator' not in l[1]),layers[0])
        model=dict(type='cmu3DModel',id='CMU3DHive'+row['id'],label=str(row['name']),status='draft',
                   sourcePrototypes=[row['id']],referencePrototype=row['id'],referenceRsi=inventory.texture_reference(rsi),
                   referenceState=state,placement='floor',groundOffset=[0,0],useEntityRotation=True,parts=parts,
                   description='Solid hive organ with authored depth and finished reverse faces. See SOURCES_XENO_STRUCTURES.md.')
        # Wall solids tile continuously. Construction node HUD glyphs are review-only,
        # not a separate source of gameplay geometry or markers made visible to humans.
        if kind not in ('wall','wallweeds','nest','spikes') and 'HiveConstructionNode' not in c:
            table={}
            work=list(layers)
            if kind=='egg' and 'XenoEgg' in c:
                for name in ('xeno_egg_fragile','xeno_egg_fragile_eggsac'):
                    work.append(('_RMC14/Structures/Xenos/'+name+'.rsi','egg_item',False,{}))
            for layer_rsi,default,_,_ in work:
                definitions=table.setdefault(inventory.texture_reference(layer_rsi),{})
                entries={s['name']:s for s in metadata(layer_rsi)['states']}
                for name in state_names(row,layer_rsi,default):
                    delays=entries[name].get('delays',[[1]])[0]
                    definitions[name]=dict(delays=delays,frames=[dict(parts=geometry(row,layer_rsi,name,i,len(delays))) for i in range(len(delays))])
            model.update(xenoStates=table,xenoSpriteOffset=c['Sprite'].get('offset','0, 0'))
        groups[signature]=model;models.append(model)
        coverage.append(dict(id=row['id'],model=model['id']))
        sources.frame(rsi,state)
    return models,coverage,sources


def skin_surfaces(models,rows,sources):
    """Source relief on solid wall faces and retracting leaves, never billboards."""
    pool=Pool(art=ART,textures=TEXTURES,prefix='CMU3DXenoSkin',texture_root='/Textures/CMU14/ThreeD/XenoSkin')
    refs={r['id']:r for r in rows}
    provenance={}
    def crop(pixels,rect,rsi,state):
        uid=pool.crop(pixels,rect)
        source=dict(rsi=inventory.texture_reference(rsi),state=state,rect=rect,
                    **{k:v for k,v in metadata(rsi).items() if k in ('license','copyright')})
        if source not in provenance.setdefault(uid,[]):
            provenance[uid].append(source)
        return uid
    for m in models:
        row=refs[m['referencePrototype']];kind=classify(row)
        if kind not in ('wall','door'):
            continue
        rsi=m['referenceRsi'];state=m['referenceState']
        if kind=='wall':
            if 'membrane' in state.lower():
                continue
            pixels=sources.frame(rsi,state)
            surface=crop(pixels,(0,0,pixels.width,pixels.height),rsi,state)
            for side in (-1,1):
                y=side*.502;x=side*.502
                m['parts'].append(part('source resin skin front and rear',[-.5,y-.003,.01],[.5,y+.003,2.39],
                                       '#FFFFFF',surface=surface,surfaceAxis='XZ'))
                m['parts'].append(part('source resin skin sides',[x-.003,-.5,.01],[x+.003,.5,2.39],
                                       '#FFFFFF',surface=surface,surfaceAxis='YZ'))
        else:
            state=row['components']['Door']['closedSpriteState'];pixels=sources.frame(rsi,state)
            halves=[crop(pixels,(i*pixels.width//2,0,(i+1)*pixels.width//2,pixels.height),rsi,state) for i in range(2)]
            frames=[m['parts']]+[f['parts'] for states in m.get('xenoStates',{}).values()
                                      for definition in states.values() for f in definition['frames']]
            for parts in frames:
                for p in parts:
                    if p['label']=='closed contracting membrane':
                        p.update(surface=halves[0 if sum((p['min'][0],p['max'][0]))<0 else 1],
                                 surfaceAxis='XZ',color='#FFFFFF')
    write(ART,'# CMU14: original resin skin detail on solid authored geometry.\n'+yaml.safe_dump(pool.entries,sort_keys=False))
    write(TEXTURES/'sources.json',json.dumps(provenance,indent=2)+'\n')


def review(models,sources):
    font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',15)
    for page in range((len(models)+7)//8):
        entries=models[page*8:page*8+8]
        sheet=Image.new('RGB',(1160,len(entries)*210+40),'#242D36');d=ImageDraw.Draw(sheet)
        d.text((15,8),'Original sprite                  Front / side                         Rear / side                         Underside',font=font,fill='white')
        for index,m in enumerate(entries):
            y=40+index*210
            source=sources.frame(m['referenceRsi'],m['referenceState']);source.thumbnail((140,140),Image.Resampling.NEAREST)
            n=max(1,min(3,140//max(source.size)));source=source.resize((source.width*n,source.height*n),Image.Resampling.NEAREST)
            sheet.paste(source,(65,y+15),source)
            for j,(yaw,pitch) in enumerate(((-math.pi/3,.55),(math.pi*.65,.48),(-math.pi/3,-.55))):
                pic=bm.render_model(m,size=(290,175),yaw=yaw,pitch=pitch)
                sheet.paste(pic,(260+j*295,y))
            d.text((12,y+176),m['label']+'  |  '+m['id'],font=font,fill='white')
        sheet.save(REVIEW/f'assets-{page+1:02}.png')


def highlights(models):
    refs={p:m for m in models for p in m['sourcePrototypes']}
    font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',15)
    picks=['DoorXenoResin','WallXenoResin','WallXenoMembrane','XenoEgg','XenoNest','XenoTunnel',
           'XenoResinHole','XenoWeedsSource','HiveCoreXeno','HiveAcidPillarXeno','HivePlasmaTreeXeno',
           'HiveRecoveryNodeXeno','CMU14XenoMyceliumDoor','CMU14PathogenSporecaster','XenoFruitSpeed','RMCHiveCocoonKing']
    canvas=Image.new('RGB',(1120,1000),'#18232D');draw=ImageDraw.Draw(canvas)
    for i,uid in enumerate(picks):
        m=refs[uid];x=i%4*280;y=i//4*250
        canvas.paste(bm.render_model(m,size=(280,215),yaw=-math.pi/3,pitch=.48),(x,y))
        draw.text((x+8,y+222),m['label'],font=font,fill='white')
    canvas.save(REVIEW/'overview.png')
    canvas=Image.new('RGB',(1120,780),'#18232D');draw=ImageDraw.Draw(canvas)
    for row,(uid,states) in enumerate([
        ('DoorXenoResin',['resin','resinopening','resinopen','resinclosing']),
        ('XenoEgg',['egg','egg_opening','egg_opened','egg_exploded']),
        ('XenoWeeds',['weed_dir1','weed_dir4','weed_dir5','weed_dir15'])]):
        m=refs[uid];table=m['xenoStates'][m['referenceRsi']]
        for col,name in enumerate(states):
            definition=table[name];frame=len(definition['frames'])//2 if 'ing' in name else 0
            pic=bm.render_model({**m,'parts':definition['frames'][frame]['parts']},size=(280,220),yaw=-math.pi/3,pitch=.5)
            x=col*280;y=row*260;canvas.paste(pic,(x,y))
            draw.text((x+10,y+231),name+' / frame '+str(frame),font=font,fill='white')
    canvas.save(REVIEW/'states.png')


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--inventory',type=Path);args=parser.parse_args()
    kinds=json.loads(args.inventory.read_text()) if args.inventory else inventory.load_prototypes(ROOT)[0]
    rows=collect(kinds);models,coverage,sources=build(rows);skin_surfaces(models,rows,sources)
    write(MODEL,'# CMU14: authored hive structures; mobs are deliberately excluded.\n'+yaml.safe_dump(serialize(models),sort_keys=False,width=120))
    checked=bm.load_models(MODEL);REVIEW.mkdir(parents=True,exist_ok=True);review(checked,sources);highlights(checked)
    rsis={m['referenceRsi'] for m in models} | {rsi for m in models for rsi in m.get('xenoStates',{})}
    licenses={rsi:{k:v for k,v in metadata(rsi).items() if k in ('license','copyright')} for rsi in sorted(rsis)}
    write(REVIEW/'coverage.json',json.dumps(dict(models=len(models),coverage=coverage,sources=licenses),indent=2)+'\n')
    print(json.dumps(dict(models=len(models),bindings=sum(len(m['sourcePrototypes']) for m in models),parts=max(len(m['parts']) for m in models))))


if __name__=='__main__':main()
