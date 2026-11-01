"""Source-owned drinking vessels: static layered art, never a live chemistry simulator."""
from copy import deepcopy
from functools import cache
from pathlib import Path
import json
import math
import struct
from PIL import Image
from sprite_states import INCOMPATIBLE,vector2

ROOT=Path(__file__).resolve().parents[2]
CLEAR='Objects/Consumable/Drinks/glass_clear.rsi'
RMC='_RMC14/Objects/Consumable/Drinks/drink_glass.rsi'
ROLES=('Base','Fill','Overlay')
_kinds=None


def configure_source(kinds):
    """Reuse scene.py's parsed source registry; no secondary full scan."""
    global _kinds
    _kinds=kinds
    source_solution.cache_clear();source_reagent.cache_clear()


def source_kinds():
    global _kinds
    if _kinds is None:
        import inventory
        configure_source(inventory.load_prototypes(ROOT)[0])
    return _kinds


def parents(row):
    p=row.get('parent',[])
    return [p] if isinstance(p,str) else p


@cache
def source_solution(uid):
    """SolutionComponent.Solution is AlwaysPushInheritance, unlike ordinary fields."""
    import inventory
    row=source_kinds()['entity'][uid]
    result=deepcopy(inventory.component_map(row).get('Solution',{}).get('solution',{}))
    for parent in parents(row):
        for k,v in source_solution(parent).items():result.setdefault(k,deepcopy(v))
    return result


@cache
def source_reagent(uid):
    row=deepcopy(source_kinds()['reagent'][uid])
    for parent in parents(row):
        for k,v in source_reagent(parent).items():row.setdefault(k,deepcopy(v))
    return row


def resource(value):return str(value).removeprefix('/Textures/').removeprefix('Textures/')
def rgba(value):
    from build_models import rgba as parse
    result=parse(value)
    if not all(math.isfinite(v) and 0<=v<=1 for v in result):raise ValueError('Invalid layer color')
    return result
def color(value):return '#'+''.join(f'{max(0,min(255,round(v*255))):02X}' for v in value)
def f32(value):return struct.unpack('<f',struct.pack('<f',value))[0]
def pose(role,rsi,state,visible=True,tint='#FFFFFF'):return dict(role=role,rsi=rsi,state=state,visible=visible,color=tint)


def compose_parts(model,layers=None,sprite_tint='#FFFFFF'):
    definition=model['solutionAppearance'];layers=definition['defaultLayers'] if layers is None else layers
    if (not isinstance(layers,list) or len(layers)!=(3 if model['referenceRsi']==CLEAR else 2) or
        any(not isinstance(p,dict) or set(p)!={'role','rsi','state','visible','color'} or type(p['visible']) is not bool or
            any(not isinstance(p[k],str) or not p[k] for k in ('role','rsi','state','color')) for p in layers) or
        [p['role'] for p in layers]!=list(ROLES[:len(layers)]) or not layers[0]['visible']):
        raise ValueError('Solution layers require ordered visible Base, Fill and optional Overlay')
    overall=rgba(sprite_tint)
    if overall[3]!=1:raise ValueError('Overall sprite alpha needs original layered rendering')
    result=[]
    for p in layers:
        tint=rgba(p['color']);matches=[g for g in definition['layers'] if all(g[k]==p[k] for k in ('role','rsi','state'))]
        if len(matches)!=1:raise ValueError('Unmodeled source layer or metamorphic vessel')
        if not p['visible']:continue
        for part in matches[0]['parts']:
            result.append({**part,'color':color(tuple(a*b*c for a,b,c in zip(rgba(part.get('color','#FFFFFF')),tint,overall)))})
    if not 1<=len(result)<=128:raise ValueError('Solution composition part budget exceeded')
    return result


def validate(model,validate_model):
    definition=model.get('solutionAppearance')
    if (not isinstance(definition,dict) or set(definition)!={'layers','defaultLayers'} or
        model.get('referenceRsi') not in (CLEAR,RMC) or model.get('referenceState')!='icon' or model.get('sourceDirections',1)!=1 or
        model.get('placement')!='surface' or model.get('useEntityRotation') is not True or
        model.get('bakedSpriteTint','#FFFFFF').upper() not in ('#FFFFFF','#FFFFFFFF') or
        any(model.get(k) for k in (*INCOMPATIBLE,'spriteStates','sourceSpriteOffset','sourceSpriteRotates','chargerAppearance','reagentTankAppearance','foamAppearance','wallPaper','directionalModels'))):
        raise ValueError('Solution glass requires independent source-rotating layered appearance')
    if not isinstance(definition['layers'],list) or not 2<=len(definition['layers'])<=64:raise ValueError('Invalid solution layer catalog')
    checked=[];keys=set();template={k:v for k,v in model.items() if k!='solutionAppearance'}
    for layer in definition['layers']:
        if not isinstance(layer,dict) or set(layer)!={'role','rsi','state','parts'} or layer['role'] not in ROLES:
            raise ValueError('Invalid source layer definition')
        key=tuple(layer[k] for k in ('role','rsi','state'))
        if key in keys or any(not isinstance(s,str) or not s for s in key):raise ValueError('Duplicate/invalid source layer key')
        keys.add(key);checked.append({**layer,'parts':validate_model({**template,'parts':layer['parts']})['parts']})
    result={**model,'solutionAppearance':{**definition,'layers':checked}}
    expected=compose_parts(result)
    # Generic color normalization omits an opaque FF suffix. Compare numeric RGBA.
    def equivalent(parts):return [{**p,'color':rgba(p['color'])} for p in parts]
    if equivalent(result['parts'])!=equivalent(expected):raise ValueError('Default parts differ from source default composition')
    for state in portable_states(result).values():compose_parts(result,state['layers'])
    return result


def validate_source(model,resource_file):
    images={}
    for g in model['solutionAppearance']['layers']:
        folder=resource_file(g['rsi']);meta=json.loads((folder/'meta.json').read_text());states={s['name']:s for s in meta['states']};s=states.get(g['state'],{})
        if meta['size']!={'x':32,'y':32} or s.get('name')!=g['state'] or s.get('directions',1)!=1 or s.get('delays',[[1]])!=[[1]]:
            raise ValueError('Glass layer requires its static 32px one-direction source')
        image=Image.open(folder/(g['state']+'.png')).convert('RGBA')
        if image.size!=(32,32) or not image.getbbox():raise ValueError('Missing glass source frame')
        images[(g['rsi'],g['state'])]=image
    return images


def portable_states(model):
    """Static source studies, including each authored vessel's complete fill range."""
    d=model['solutionAppearance'];result={'saved-default':dict(layers=d['defaultLayers'])}
    original=d['defaultLayers'];catalog=d['layers'];overlay=next((g for g in catalog if g['role']=='Overlay'),None)
    for base in [g for g in catalog if g['role']=='Base']:
        fills=[g for g in catalog if g['role']=='Fill' and g['rsi']==base['rsi']]
        if not fills:raise ValueError('Vessel needs every source fill layer')
        tint=next((p['color'] for p in original if p['role']=='Fill' and p['rsi']==base['rsi']),'#FFFFFF')
        for fill in [None,*fills]:
            layers=[pose('Base',base['rsi'],base['state']),pose('Fill',base['rsi'],(fill or fills[0])['state'],fill is not None,tint)]
            if overlay:layers.append(pose('Overlay',overlay['rsi'],overlay['state'],base['rsi']==CLEAR))
            key=Path(base['rsi']).stem+'--'+(fill['state'] if fill else 'empty')
            result[key]=dict(layers=layers)
    for s in result.values():s.update(delays=[1],frames=[dict(parts=compose_parts(model,s['layers']))])
    return result


def owner(defaults,saved,key):return {**defaults.get(key,{}),**saved.get(key,{})}


def saved_pose(model,defaults,saved,normalize_tint):
    """Bounded source MapInit selection; native sampling always uses actual live layers."""
    try:
        sprite=owner(defaults,saved,'Sprite');rsi=resource(sprite.get('sprite'))
        zero=lambda v:float(str(v).removesuffix('rad').strip())==0
        if (rsi!=model['referenceRsi'] or sprite.get('noRot',False) or sprite.get('snapCardinals',False) or
            sprite.get('visible',True) is not True or vector2(sprite.get('offset',[0,0]))!=(0,0) or
            vector2(sprite.get('scale',[1,1]))!=(1,1) or not zero(sprite.get('rotation',0)) or
            sprite.get('granularLayersRendering') or sprite.get('postShaders') or sprite.get('postShader')):
            raise ValueError('Unsupported solution sprite transform/render path')
        tint=normalize_tint(sprite.get('color','#FFFFFF'))
        if rgba(tint)[3]!=1:raise ValueError('Overall sprite alpha requires original layered renderer')
        layers=sprite.get('layers');expected=3 if rsi==CLEAR else 2
        if not isinstance(layers,list) or len(layers)!=expected:raise ValueError('Unsupported solution layer count')
        for i,l in enumerate(layers):
            if (l.get('map')!=['enum.SolutionContainerLayers.'+ROLES[i]] or l.get('state')!=('icon','fill-1','icon-front')[i] or
                l.get('visible',True)!=(i!=1) or normalize_tint(l.get('color','#FFFFFF'))!='#FFFFFF' or
                resource(l.get('rsi',rsi))!=rsi or l.get('texture') or l.get('shader') or l.get('shaderPrototype') or
                l.get('copyToShaderParameters') or l.get('dirOffset',l.get('directionOffset')) not in (None,0,'None') or
                vector2(l.get('offset',[0,0]))!=(0,0) or vector2(l.get('scale',[1,1]))!=(1,1) or not zero(l.get('rotation',0))):
                raise ValueError('Unsupported saved solution layer')
        if ('Appearance' not in defaults and 'Appearance' not in saved or owner(defaults,saved,'Appearance').get('data') or
            any(k in defaults or k in saved for k in ('GenericVisualizer','RandomSprite','ItemMapper','SolutionContainerManager'))):
            raise ValueError('Unsupported source appearance owner')
        visuals=owner(defaults,saved,'SolutionContainerVisuals');solution=owner(defaults,saved,'Solution')
        if (solution.get('id')!='drink' or visuals.get('fillBaseName')!='fill-' or
            visuals.get('maxFillLevels')!=(9 if rsi==CLEAR else 5) or bool(visuals.get('metamorphic',False))!=(rsi==CLEAR) or
            visuals.get('solutionName') not in (None,'drink') or visuals.get('changeColor',True) is not True or
            visuals.get('emptySpriteName') or any(visuals.get(k,k[0].upper()+k[1:])!=v for k,v in [('layer','Fill'),('baseLayer','Base'),('overlayLayer','Overlay')] if k in visuals)):
            raise ValueError('Unknown solution visualizer contract')
        if rsi==CLEAR and visuals.get('metamorphicDefaultSprite')!={'sprite':CLEAR,'state':'icon'}:raise ValueError('Unknown metamorphic default')
        data={**source_solution(model['referencePrototype']),**saved.get('Solution',{}).get('solution',{})}
        contents=data.get('reagents',[])
        # Source default single reagents are proven. Unresolved mixtures/reactions
        # remain live-owner territory instead of a second chemistry simulation.
        if not isinstance(contents,list) or len(contents)>1:raise ValueError('Saved mixture requires live solution owner')
        amount=0.;reagent=None
        if contents:
            item=contents[0]
            if set(item)!={'ReagentId','Quantity'} or not isinstance(item['ReagentId'],str):raise ValueError('Unsupported saved reagent identity/data')
            amount=float(item['Quantity']);reagent=source_reagent(item['ReagentId'])
        maximum=float(data.get('maxVol',0))
        if not math.isfinite(amount) or amount<0 or not math.isfinite(maximum) or maximum<0:raise ValueError('Invalid source volume')
        if maximum==0:maximum=amount
        fraction=min(1.,f32(f32(amount)/f32(maximum))) if maximum else 1.
        base_rsi=rsi;base_state='icon';fill_rsi=rsi;maximum_levels=visuals['maxFillLevels'];change_color=True;overlay=rsi==CLEAR;fill_visible=True
        if rsi==CLEAR and reagent is not None and amount>0:
            metamorphic=reagent.get('metamorphicSprite')
            if metamorphic:
                if set(metamorphic)!={'sprite','state'}:raise ValueError('Unsupported metamorphic texture')
                base_rsi=metamorphic['sprite'];base_state=metamorphic['state'];overlay=False
            levels=reagent.get('metamorphicMaxFillLevels',0)
            if levels>0:
                maximum_levels=levels;fill_rsi=base_rsi;change_color=reagent.get('metamorphicChangeColor',True)
                if reagent.get('metamorphicFillBaseName')!='fill-':raise ValueError('Unsupported metamorphic fill prefix')
            elif metamorphic:fill_visible=False
        level=maximum_levels if fraction>=1 else 0 if fraction<=0 else math.ceil(fraction*(maximum_levels-1))
        fill_state='fill-'+str(level) if level else 'fill-1'
        fill_color=normalize_tint(reagent.get('color','#FFFFFF')) if reagent is not None and change_color else '#FFFFFF'
        result=[pose('Base',base_rsi,base_state),pose('Fill',fill_rsi,fill_state,fill_visible and level>0,fill_color)]
        if rsi==CLEAR:result.append(pose('Overlay',CLEAR,'icon-front',overlay))
        compose_parts(model,result,tint)
        return dict(layers=result,spriteTint=tint,source='source Solution MapInit, including AlwaysPushInheritance; no reaction simulation',
                    volume=amount,maxVolume=maximum,fillLevel=level),None
    except (ValueError,KeyError,TypeError,OverflowError,ZeroDivisionError) as e:return None,str(e)


def resolve_scene_pose(instance,model,defaults,saved,normalize_tint):
    p,reason=saved_pose(model,defaults,saved,normalize_tint)
    if p is None:
        instance.update(baseModelId=model['id'],modelId=None,modelStatus=None,matchKind='unmapped',unsupportedState=reason);return False
    instance['solutionPose']=p;return True


def scene_variants(instances,library,variants):
    for e in instances:
        model=library.get(e.get('modelId'))
        if not model or 'solutionPose' not in e:continue
        p=e['solutionPose'];key=model['id']+':solution:'+json.dumps(p['layers'],sort_keys=True)+':'+p['spriteTint']
        variants[key]=compose_parts(model,p['layers'],p['spriteTint']);e['geometryKey']=key


def composite(model,layers,resource_file):
    images=validate_source(model,resource_file);result=Image.new('RGBA',(32,32))
    for p in layers:
        if not p['visible']:continue
        im=images[(p['rsi'],p['state'])].copy();tint=rgba(p['color'])
        im.putdata([tuple(round(c*t) for c,t in zip(pixel,tint)) for pixel in im.getdata()]);result.alpha_composite(im)
    return result


def default_source_composite(model,resource_file):
    return composite(model,model['solutionAppearance']['defaultLayers'],resource_file)


def viewer_references(model,resource_file,png_bytes):
    files,refs={},{}
    for name,p in portable_states(model).items():
        path=f'references/{model["id"]}-solution-{name}.png';files[path]=png_bytes(composite(model,p['layers'],resource_file));refs[name]=['../generated/'+path]
    return files,refs


def model_document(model,build_document):
    from sprite_states import model_document as sprite_document
    states=portable_states(model);virtual={**model,'spriteStates':{k:{'frames':v['frames'],'delays':v['delays']} for k,v in states.items()},
        'referenceState':'saved-default','sourceSpriteOffset':[0,0]}
    doc,binary=sprite_document(virtual,build_document)
    doc['extras']['solutionAppearance']=dict(sourceOwned=True,staticStudies=True,sourceAlphaPreserved=True,
        limitation='Physical wall depth and unseen construction inferred; native transparency uses the current renderer approximation.')
    return doc,binary
