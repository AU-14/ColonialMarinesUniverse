"""Exact, source-gated companion geometry for reviewed ladder/decoration assemblies."""
from __future__ import annotations

from functools import lru_cache
import json
import math

FIELD = 'floorOpeningCompanions'
FIELDS = {'prototype','model','referenceRsi','referenceState','referenceDirection','sourceDirections',
          'sourceFrame','sourceSpriteOffset','sourceNoRotation','sourceSnapCardinals',
          'sourceYaw','renderYaw','renderOffset','parts'}


def vector(value, count):
    values = value.split(',') if isinstance(value,str) else value
    if not isinstance(values,(list,tuple)) or len(values)!=count:
        raise ValueError('Compound requires a complete finite vector')
    result = tuple(float(v) for v in values)
    if not all(math.isfinite(v) for v in result):
        raise ValueError('Compound vector is not finite')
    return result


def resource(value):
    return str(value).lstrip('/').removeprefix('Textures/')


def validate(model, validate_model):
    companions=model.get(FIELD)
    if not isinstance(companions,list) or not 1<=len(companions)<=4 or not model.get('floorOpening'):
        raise ValueError('Companion compositions require a floor opening and one to four source contracts')
    seen=set()
    checked=[]
    for entry in companions:
        if not isinstance(entry,dict) or set(entry)!=FIELDS:
            raise ValueError('Incomplete or unknown compound source contract fields')
        for field in ('prototype','model','referenceRsi','referenceState'):
            if not isinstance(entry[field],str) or not entry[field]:
                raise ValueError('Compound source identifiers must be nonempty strings')
        if entry['prototype'] in seen:
            raise ValueError('Duplicate compound prototype contract')
        seen.add(entry['prototype'])
        directions,direction=entry['sourceDirections'],entry['referenceDirection']
        if (type(directions) is not int or directions not in (1,4,8) or type(direction) is not int or
                not 0<=direction<directions or type(entry['sourceFrame']) is not int or entry['sourceFrame']!=0 or
                type(entry['sourceNoRotation']) is not bool or type(entry['sourceSnapCardinals']) is not bool):
            raise ValueError('Compound requires a known static RSI direction and explicit source rotation flags')
        for field in ('sourceYaw','renderYaw'):
            if type(entry[field]) not in (int,float) or not math.isfinite(entry[field]) or abs(entry[field])>360:
                raise ValueError('Compound angles must be finite degrees within one turn')
        offset=vector(entry['renderOffset'],3)
        sprite_offset=vector(entry['sourceSpriteOffset'],2)
        if any(abs(v)>4 for v in (*offset,*sprite_offset)):
            raise ValueError('Compound offsets exceed the bounded local assembly')
        template={'type':'cmu3DModel','id':model['id']+'Companion','label':'Compound companion',
                  'sourcePrototypes':[],'parts':entry['parts']}
        parts=validate_model(template)['parts']
        if not 1<=len(parts)<=128:
            raise ValueError('Compound companion exceeds its part budget')
        checked.append({**entry,'parts':parts,'renderOffset':offset,'sourceSpriteOffset':sprite_offset})
    return {**model,FIELD:checked}


@lru_cache(maxsize=32)
def source_metadata(rsi):
    from build_models import resource_file
    folder=resource_file(rsi)
    if folder is None or not (folder/'meta.json').is_file():
        raise ValueError('Compound RSI metadata is missing')
    return json.loads((folder/'meta.json').read_text(encoding='utf-8-sig'))


def validate_reference(entry):
    metadata=source_metadata(resource(entry['referenceRsi']))
    state=next((s for s in metadata.get('states',[]) if s['name']==entry['referenceState']),None)
    if state is None or state.get('directions',1)!=entry['sourceDirections']:
        raise ValueError('Compound source RSI state/direction count changed')
    delays=state.get('delays',[[1]]*entry['sourceDirections'])
    if (len(delays)!=entry['sourceDirections'] or any(len(row)!=1 or not math.isfinite(row[0]) or row[0]<=0 for row in delays)):
        raise ValueError('Compound source must have exactly one static frame per direction')


def source_direction(yaw,directions):
    if directions==1:return 0
    step=math.floor(yaw/(math.tau/directions)+.5)%directions
    return ((0,2,1,3) if directions==4 else (0,4,2,6,1,7,3,5))[step]


def angle_matches(actual,expected):
    return math.isfinite(actual) and abs(math.remainder(actual-expected,math.tau))<=1e-4


def source_matches(entry,default,saved,yaw,normalize_tint):
    sprite={**default.get('Sprite',{}),**saved.get('Sprite',{})}
    def zero(value):
        value=str(value).strip()
        return float(value[:-3] if value.endswith('rad') else value)==0
    if (sprite.get('visible',True) is not True or sprite.get('noRot',False)!=entry['sourceNoRotation'] or
            sprite.get('snapCardinals',False)!=entry['sourceSnapCardinals'] or
            sprite.get('granularLayersRendering',False) or sprite.get('postShader') or sprite.get('postShaders') or
            vector(sprite.get('offset',(0,0)),2)!=vector(entry['sourceSpriteOffset'],2) or
            vector(sprite.get('scale',(1,1)),2)!=(1,1) or not zero(sprite.get('rotation',0)) or
            normalize_tint(sprite.get('color','#FFFFFF'))!='#FFFFFF' or saved.get('Appearance',{}).get('data')):
        return False
    layers=sprite.get('layers',[{'state':sprite.get('state')}])
    if not isinstance(layers,list) or any(not isinstance(layer,dict) for layer in layers):return False
    visible=[layer for layer in layers if layer.get('visible',True)]
    if len(visible)!=1:return False
    layer=visible[0]
    if (layer.get('texture') or layer.get('shader') or layer.get('shaderPrototype') or layer.get('copyToShaderParameters') or
            layer.get('dirOffset') not in (None,0,'None') or layer.get('directionOffset') not in (None,0,'None') or
            vector(layer.get('scale',(1,1)),2)!=(1,1) or vector(layer.get('offset',(0,0)),2)!=(0,0) or
            not zero(layer.get('rotation',0)) or normalize_tint(layer.get('color','#FFFFFF'))!='#FFFFFF' or
            resource(layer.get('rsi',sprite.get('sprite','')))!=resource(entry['referenceRsi']) or
            layer.get('state')!=entry['referenceState'] or
            source_direction(yaw,entry['sourceDirections'])!=entry['referenceDirection']):
        return False
    validate_reference(entry)
    return True


def select(source,model,instances,records,defaults,transforms,grid_context,library,normalize_tint):
    """Return proposed replacements only after the complete composition is proven."""
    contracts=model.get(FIELD,[])
    if not contracts:return []
    index={e['id']:e for e in instances}
    from scene import hidden_container_entities
    hidden=hidden_container_entities(records)
    grid,_,x,y,_,_=grid_context(source)
    ladder_yaw=source.get('renderYaw',source['yaw'])
    operations=[]
    for entry in contracts:
        candidates=[]
        for uid,record in records.items():
            if record['prototype']!=entry['prototype'] or uid in hidden:continue
            try:g,_,tx,ty,_,_=grid_context({'id':uid})
            except (ValueError,TypeError,KeyError):continue
            if g==grid and abs(tx-x)<=1e-4 and abs(ty-y)<=1e-4:candidates.append(uid)
        if len(candidates)!=1:
            raise ValueError('Compound requires one matching companion of each exact prototype')
        uid=candidates[0]
        target=index.get(uid)
        if (target is None or target.get('matchKind')!='exact' or target.get('modelId')!=entry['model'] or
                target['modelId'] not in library or not transforms.local(uid).get('anchored',False)):
            raise ValueError('Compound companion was omitted, unanchored or has an unsupported model')
        if (not angle_matches(target['yaw'],ladder_yaw+math.radians(entry['sourceYaw'])) or
                not angle_matches(target.get('renderYaw',target['yaw']),ladder_yaw+math.radians(entry['renderYaw'])) or
                any(abs(a-b)>1e-5 for a,b in zip(target.get('renderOffset',(0,0,0)),vector(entry['renderOffset'],3)))):
            raise ValueError('Compound companion orientation or support offset differs from the reviewed assembly')
        if not source_matches(entry,defaults.get(entry['prototype'],{}),records[uid]['components'],target['yaw'],normalize_tint):
            raise ValueError('Compound companion source appearance differs from the reviewed assembly')
        operations.append((target,entry['parts'],source['id']))
    return operations
