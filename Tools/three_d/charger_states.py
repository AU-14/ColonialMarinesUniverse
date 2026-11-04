"""Bounded recharger layers, saved owner proof and portable source-frame compositions."""
from copy import deepcopy
import json
import math
import struct
from PIL import Image
from sprite_states import INCOMPATIBLE, vector2

RSI = '_RMC14/Structures/Power/recharger.rsi'
BASE = 'recharger'
LIGHTS = tuple('recharger-'+str(i) for i in range(6))
INSERTS = ('recharger-taser', 'recharger-baton')
BASE_MAP = 'enum.PowerChargerVisualLayers.Base'
LIGHT_MAP = 'enum.PowerChargerVisualLayers.Light'


def compose_parts(model, light='recharger-0', frame=0, inserted=()):
    definition = model['chargerAppearance']
    if (light not in (*LIGHTS, 'hidden') or type(frame) is not int or frame < 0 or
            any(s not in INSERTS for s in inserted) or len(inserted) != len(set(inserted))):
        raise ValueError('Unknown charger pose')
    result = list(definition['baseParts'])
    if light != 'hidden':
        frames = definition['lightStates'][light]['frames']
        if frame >= len(frames):
            raise ValueError('Unknown source indicator frame')
        result += frames[frame]['parts']
    elif frame != 0:
        raise ValueError('Hidden indicator has no portable clock')
    for state in INSERTS:
        if state in inserted:
            result += definition['insertedStates'][state]['parts']
    if not 1 <= len(result) <= 128:
        raise ValueError('Composed charger exceeds the source part budget')
    return result


def validate(model, validate_model):
    definition = model.get('chargerAppearance')
    if (not isinstance(definition, dict) or set(definition) != {'baseParts','lightStates','insertedStates'} or
            model.get('referenceRsi') != RSI or model.get('referenceState') != BASE or model.get('sourceDirections',1) != 1 or
            model.get('placement') != 'surface' or model.get('useEntityRotation',False) or
            model.get('bakedSpriteTint','#FFFFFF').upper() not in ('#FFFFFF','#FFFFFFFF') or
            any(model.get(k) for k in (*INCOMPATIBLE,'spriteStates','sourceSpriteOffset','sourceSpriteRotates','reagentTankAppearance','wallPaper','directionalModels')) or
            'doorState' in model or 'folded' in model or
            set(definition['lightStates']) != set(LIGHTS) or set(definition['insertedStates']) != set(INSERTS)):
        raise ValueError('Charger requires its independent snapped, one-direction source layer contract')
    template = {k:v for k,v in model.items() if k != 'chargerAppearance'}
    def parts(value):
        return validate_model({**template,'parts':value})['parts']
    checked = dict(baseParts=parts(definition['baseParts']), lightStates={}, insertedStates={})
    for state in LIGHTS:
        entry = definition['lightStates'][state]
        count, delays = (2,[.1,.1]) if state == 'recharger-5' else (1,[1])
        if (not isinstance(entry,dict) or set(entry) != {'frames','delays'} or entry['delays'] != delays or
                not isinstance(entry['frames'],list) or len(entry['frames']) != count):
            raise ValueError('Indicator frame count and delays must equal its source RSI')
        if any(not isinstance(f,dict) or set(f) != {'parts'} for f in entry['frames']):
            raise ValueError('Malformed indicator frame')
        checked['lightStates'][state] = dict(frames=[dict(parts=parts(f['parts'])) for f in entry['frames']],delays=delays)
    for state in INSERTS:
        if not isinstance(definition['insertedStates'][state],dict) or set(definition['insertedStates'][state]) != {'parts'}:
            raise ValueError('Inserted source must contain static parts only')
        checked['insertedStates'][state] = dict(parts=parts(definition['insertedStates'][state]['parts']))
    result = {**model,'chargerAppearance':checked}
    if result['parts'] != compose_parts(result):
        raise ValueError('Default charger must equal base plus source light-zero')
    for light in (*LIGHTS,'hidden'):
        for mask in range(4):
            for frame in range(2 if light == 'recharger-5' else 1):
                compose_parts(result,light,frame,[s for i,s in enumerate(INSERTS) if mask & (1<<i)])
    return result


def validate_source(model, resource_file):
    folder = resource_file(RSI)
    meta = json.loads((folder/'meta.json').read_text())
    if meta['size'] != {'x':32,'y':32}:
        raise ValueError('Charger source dimensions changed')
    states = {s['name']:s for s in meta['states']}
    images = {}
    for name in (BASE,*LIGHTS,*INSERTS):
        count,delays = (2,[[.1,.1]]) if name == 'recharger-5' else (1,[[1]])
        entry = states.get(name,{})
        if entry.get('directions',1) != 1 or entry.get('delays',[[1]]) != delays:
            raise ValueError('Charger source state directions/timing changed')
        sheet = Image.open(folder/(name+'.png')).convert('RGBA')
        if sheet.size != (32,32*count) or not set(sheet.getchannel('A').tobytes()) <= {0,255}:
            raise ValueError('Charger source frames or binary-alpha proof changed')
        images[name] = [sheet.crop((0,32*i,32,32*(i+1))) for i in range(count)]
    return images


def pose_name(light, inserted):
    return light+'--'+('-'.join(s.removeprefix('recharger-') for s in INSERTS if s in inserted) or 'empty')


def portable_states(model):
    result = {}
    for light in (*LIGHTS,'hidden'):
        for mask in range(4):
            inserted = [s for i,s in enumerate(INSERTS) if mask & (1<<i)]
            delays = [.1,.1] if light == 'recharger-5' else [1]
            result[pose_name(light,inserted)] = dict(delays=delays,
                frames=[dict(parts=compose_parts(model,light,frame,inserted)) for frame in range(len(delays))])
    return result


def viewer_references(model, resource_file, png_bytes):
    images = validate_source(model,resource_file)
    outputs, references = {}, {}
    for light in (*LIGHTS,'hidden'):
        for mask in range(4):
            inserted = [s for i,s in enumerate(INSERTS) if mask & (1<<i)]
            name = pose_name(light,inserted);references[name] = []
            for frame in range(2 if light == 'recharger-5' else 1):
                image = images[BASE][0].copy()
                if light != 'hidden':image.alpha_composite(images[light][frame])
                for state in inserted:image.alpha_composite(images[state][0])
                file = f'references/{model["id"]}-charger-{name}-frame{frame}.png'
                outputs[file] = png_bytes(image);references[name].append('../generated/'+file)
    return outputs,references


def model_document(model, build_document):
    from sprite_states import model_document as sprite_document
    states = portable_states(model)
    virtual = {**model,'spriteStates':states,'referenceState':pose_name('recharger-0',()),'sourceSpriteOffset':[0,0]}
    document,binary = sprite_document(virtual,build_document)
    document['extras']['chargerAppearance'] = dict(sourceOwned=True,lightShader='unshaded',
        portablePoses=list(states),unshadedRenderingSupported=False,
        limitation='Exact source indicator colors and frames; per-part unshaded illumination is not implemented by the scene renderer.')
    return document,binary


def _resource(value):
    return str(value).removeprefix('/Textures/').removeprefix('Textures/')


def _owner(defaults,saved,name):
    return {**defaults.get(name,{}),**saved.get(name,{})}


def _containers(component):
    result = {}
    for name,value in component.get('containers',{}).items():
        if value == '':result[name]=[];continue  # source tagged empty container
        if not isinstance(value,dict):raise ValueError('Unknown container record')
        if value.get('ent') is not None and value.get('ents') not in (None,[]):raise ValueError('Ambiguous contained item')
        items = [value['ent']] if value.get('ent') is not None else value.get('ents',[])
        if not isinstance(items,list) or any(type(uid) is not int or uid <= 0 for uid in items):raise ValueError('Unknown container contents')
        result[name]=items
    return result


def saved_pose(model, default_components, saved_components, normalize_tint, records=None, defaults_by_proto=None):
    """Prove source MapInit appearance. Current gameplay charge still belongs to the live owner.

    Resolved saved contents may supply a direct or slotted battery's startingCharge;
    missing records, unresolved starting items and unrecognized owners keep fallback.
    """
    d,s = default_components,saved_components
    try:
        sprite = _owner(d,s,'Sprite')
        if (sprite.get('noRot',False) or sprite.get('snapCardinals') is not True or sprite.get('visible',True) is not True or
                sprite.get('granularLayersRendering') or sprite.get('postShader') or sprite.get('postShaders') or
                vector2(sprite.get('offset',[0,0])) != (0,0) or vector2(sprite.get('scale',[1,1])) != (1,1) or
                float(str(sprite.get('rotation',0)).removesuffix('rad')) != 0 or _resource(sprite.get('sprite')) != RSI):
            raise ValueError('Charger Sprite transform/source differs')
        tint = normalize_tint(sprite.get('color','#FFFFFF'))
        from build_models import rgba
        if rgba(tint)[3] != 1:raise ValueError('Layered charger requires opaque overall tint')
        layers = sprite.get('layers')
        if not isinstance(layers,list) or len(layers) != 2:raise ValueError('Saved charger requires its two original layers')
        for i,layer in enumerate(layers):
            if (layer.get('state') != (BASE,'recharger-0')[i] or layer.get('map') != [[BASE_MAP],[LIGHT_MAP]][i] or
                    layer.get('visible',True) is not True or normalize_tint(layer.get('color','#FFFFFF')) != '#FFFFFF' or
                    layer.get('shader') != (None,'unshaded')[i] or layer.get('shaderPrototype') or layer.get('texture') or
                    layer.get('copyToShaderParameters') is not None or
                    layer.get('dirOffset',layer.get('directionOffset')) not in (None,0,'None') or
                    _resource(layer.get('rsi',sprite['sprite'])) != RSI or vector2(layer.get('offset',[0,0])) != (0,0) or
                    vector2(layer.get('scale',[1,1])) != (1,1) or float(str(layer.get('rotation',0)).removesuffix('rad')) != 0):
                raise ValueError('Saved charger layer contract differs')
        for name in ('Appearance','Charger','PowerChargerVisuals','ItemMapper','ItemSlots','ContainerContainer'):
            if name not in d and name not in s:raise ValueError('Missing source visual owner '+name)
        if _owner(d,s,'Appearance').get('data') or any(name in d or name in s for name in ('GenericVisualizer','RandomSprite','ItemCounter')):
            raise ValueError('Unknown saved visual owner/data')
        charger = _owner(d,s,'Charger');visuals = _owner(d,s,'PowerChargerVisuals');mapper = _owner(d,s,'ItemMapper')
        if charger.get('slotId') != 'charger_slot' or charger.get('chargeLevelSteps') != 6:
            raise ValueError('Unknown Charger source contract')
        if any(visuals.get(k) != v for k,v in {'emptyState':BASE,'occupiedState':BASE,'chargeLevelState':'recharger-{0}'}.items()):
            raise ValueError('Unknown PowerChargerVisuals source contract')
        if (_resource(mapper.get('sprite')) != RSI or mapper.get('containerWhitelist') is not None or mapper.get('spriteLayers') or
                list(mapper.get('mapLayers',{})) != list(INSERTS)):
            raise ValueError('Unknown ItemMapper source contract/order')
        for state,tag in zip(INSERTS,('Taser','Stunbaton')):
            layer = mapper['mapLayers'][state]
            if (layer.get('whitelist') != {'tags':[tag]} or layer.get('minCount',1) != 1 or layer.get('maxCount',2**31-1) != 2**31-1):
                raise ValueError('Unknown ItemMapper whitelist/count owner')
        containers = _containers(_owner(d,s,'ContainerContainer'))
        if 'charger_slot' not in containers or len(containers['charger_slot']) > 1:raise ValueError('Unknown charging slot')
        slots = _owner(d,s,'ItemSlots').get('slots',{})
        if any(slot.get('startingItem') for slot in slots.values()):raise ValueError('Starting items require resolved source content')
        def components(uid):
            if records is None or defaults_by_proto is None or uid not in records:raise ValueError('Nonempty container needs resolved source records')
            record=records[uid];proto=record['prototype']
            if proto not in defaults_by_proto:raise ValueError('Contained prototype is unresolved')
            defaults=defaults_by_proto[proto];saved=record['components']
            return {k:{**defaults.get(k,{}),**saved.get(k,{})} for k in set(defaults)|set(saved)}
        all_contents = [components(uid) for values in containers.values() for uid in values]
        inserted = [state for state,tag in zip(INSERTS,('Taser','Stunbaton')) if any(tag in c.get('Tag',{}).get('tags',[]) for c in all_contents)]
        level = 0
        if containers['charger_slot']:
            item = components(containers['charger_slot'][0]);battery = item.get('Battery')
            if battery is None and 'PowerCellSlot' in item:
                slot=item['PowerCellSlot'].get('cellSlotId');cells=_containers(item.get('ContainerContainer',{})).get(slot,[])
                if len(cells)>1:raise ValueError('Ambiguous power cell slot')
                if cells:battery=components(cells[0]).get('Battery')
                elif any(v.get('startingItem') for v in item.get('ItemSlots',{}).get('slots',{}).values()):raise ValueError('Unresolved starting battery')
            if battery is not None:
                maximum,charge=battery.get('maxCharge',0),battery.get('startingCharge',0)
                if any(type(v) not in (int,float) or not math.isfinite(v) or v<0 for v in (maximum,charge)):
                    raise ValueError('Invalid source battery charge')
                # Match the owner's float ratio at MapInit, not a second runtime clock.
                f32=lambda x:struct.unpack('<f',struct.pack('<f',x))[0]
                ratio=f32(f32(min(charge,maximum))/f32(maximum)) if maximum>0 else 0
                level=5 if ratio>=1 else max(1,min(4,math.ceil(f32(ratio*4)))) if ratio>0 else 0
        pose=dict(light=LIGHTS[level],frame=0,inserted=inserted,spriteTint=tint,
                  source='source-owned MapInit charge and contained-item tags; live states may change after loading')
        compose_parts(model,pose['light'],0,inserted)
        return pose,None
    except (ValueError,TypeError,KeyError,AttributeError,OverflowError) as error:
        return None,str(error)


def resolve_scene_pose(instance, model, defaults, saved, normalize_tint, records=None, defaults_by_proto=None):
    pose,reason=saved_pose(model,defaults,saved,normalize_tint,records,defaults_by_proto)
    if pose is None:
        instance.update(baseModelId=model['id'],modelId=None,modelStatus=None,matchKind='unmapped',unsupportedState=reason)
        return False
    instance['chargerPose']=pose
    return True


def scene_variants(instances, library, variants):
    from reagent_tank_states import channels, color_hex
    for entity in instances:
        model=library.get(entity.get('modelId'));pose=entity.get('chargerPose')
        if not model or not pose:continue
        key=f"{model['id']}:charger:{pose_name(pose['light'],pose['inserted'])}:{pose['frame']}:{pose['spriteTint']}"
        parts=compose_parts(model,pose['light'],pose['frame'],pose['inserted'])
        if pose['spriteTint'] != '#FFFFFF':
            tint=channels(pose['spriteTint'])
            parts=[{**p,'color':color_hex([a*b for a,b in zip(channels(p.get('color','#FFFFFF')),tint)])} for p in parts]
        variants[key]=parts;entity['geometryKey']=key
