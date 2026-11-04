"""Single visible RSI-layer compositions; native timing remains owned by the source sprite."""
from __future__ import annotations

import json
import math
import re

FIELDS = ('spriteStates', 'sourceSpriteOffset', 'sourceSpriteRotates')
INCOMPATIBLE = ('doorButtonStates', 'frameAnimations', 'doorSpriteStates', 'doorAnimationDurations',
                'poweredLightStates', 'barricadeDamageStates', 'barricadeWiredStates', 'barricadeAcidStates',
                'alternateDoorModel', 'alternateFoldModel', 'randomSpritePrototypes', 'randomSpriteLayer',
                'wallMounted', 'wallFacingTargets', 'backWallMountTargets', 'windowMountTargets',
                'panelEndTargets', 'openingFacingTargets', 'faceAwayFromWall', 'connectToNeighbours',
                'cornerSurfaces', 'terrainCutoutTargets', 'supportSurface', 'supportSurfaces')


def vector2(value):
    values = value.split(',') if isinstance(value, str) else value
    if not isinstance(values, (list, tuple)) or len(values) != 2:
        raise ValueError('Expected two finite sprite-offset coordinates')
    result = tuple(float(v) for v in values)
    if not all(math.isfinite(v) and abs(v) <= 32 for v in result):
        raise ValueError('Sprite offset is nonfinite or outside the model budget')
    return result


def validate(model, validate_model):
    states = model.get('spriteStates')
    directions = model.get('sourceDirections', 1)
    direction = model.get('referenceDirection', 0)
    rotating = model.get('sourceSpriteRotates', False)
    static_surface_mount = (rotating and model.get('placement') == 'surface' and isinstance(states, dict) and
        len(states) == 1 and all(isinstance(s, dict) and isinstance(s.get('frames'), list) and len(s['frames']) == 1
                                 for s in states.values()))
    incompatible = tuple(k for k in INCOMPATIBLE if not (static_surface_mount and k == 'backWallMountTargets'))
    if type(rotating) is not bool or rotating and (model.get('floorOpening') or model.get('ceilingOpening')):
        raise ValueError('sourceSpriteRotates requires an explicit boolean independent of slab openings')
    if (not isinstance(states, dict) or not 1 <= len(states) <= 32 or model.get('referenceState') not in states or
            not model.get('referenceRsi') or type(directions) is not int or directions not in (1, 4) or type(direction) is not int or
            not 0 <= direction < directions or 'sourceSpriteOffset' not in model or
            model.get('placement', 'floor') not in (('floor', 'surface') if rotating else ('floor',)) or any(model.get(k) for k in incompatible) or
            'doorState' in model or 'folded' in model or
            model.get('bakedSpriteTint', '#FFFFFF').upper() not in ('#FFFFFF', '#FFFFFFFF') or
            directions == 4 and (len(model.get('directionalModels', [])) != 4 or 'referenceDirection' not in model)):
        raise ValueError('Sprite states require an independent one/four-direction RSI model and explicit source offset')
    offset = vector2(model['sourceSpriteOffset'])
    template = {k: v for k, v in model.items() if k not in FIELDS}
    checked = {}
    for state, definition in states.items():
        if (not isinstance(state, str) or not re.fullmatch(r'[A-Za-z0-9_][A-Za-z0-9_.-]*', state) or
                not isinstance(definition, dict) or set(definition) != {'frames', 'delays'}):
            raise ValueError('Sprite state requires a safe source state, frames and delays')
        frames, delays = definition['frames'], definition['delays']
        if (not isinstance(frames, list) or not 1 <= len(frames) <= 64 or not isinstance(delays, list) or
                len(frames) != len(delays) or any(type(d) not in (int, float) or not math.isfinite(d) or d <= 0 for d in delays) or
                not math.isfinite(sum(delays))):
            raise ValueError('Sprite frames require matching finite positive source delays')
        validated = []
        for frame in frames:
            if not isinstance(frame, dict) or set(frame) != {'parts'}:
                raise ValueError('Each sprite frame must contain only parts')
            validated.append({'parts': validate_model({**template, 'parts': frame['parts']})['parts']})
        checked[state] = {'frames': validated, 'delays': delays[:]}
    if checked[model['referenceState']]['frames'][0]['parts'] != model['parts']:
        raise ValueError('Default sprite geometry must equal referenceState frame zero')
    return {**model, 'spriteStates': checked, 'sourceSpriteOffset': offset}


def validate_source(model, resource_file):
    """Require the native RSI frame index to identify the same time interval in every direction."""
    folder = resource_file(model['referenceRsi'])
    if folder is None or not (folder / 'meta.json').is_file():
        raise ValueError(f"{model['id']}: source RSI metadata was not found")
    meta = json.loads((folder / 'meta.json').read_text(encoding='utf-8-sig'))
    states = {s['name']: s for s in meta['states']}
    direction, directions = model.get('referenceDirection', 0), model.get('sourceDirections', 1)
    width, height = meta['size']['x'], meta['size']['y']
    if type(width) is not int or type(height) is not int or width <= 0 or height <= 0:
        raise ValueError('RSI frames need positive integer dimensions')
    checked = {}
    for name, definition in model['spriteStates'].items():
        source = states.get(name)
        if source is None or source.get('directions', 1) != directions:
            raise ValueError(f"{model['id']}: missing source state or incorrect directions: {name}")
        delays = source.get('delays', [[1]] * directions)
        if (not isinstance(delays, list) or len(delays) != directions or
                any(not isinstance(row, list) or not row or
                    any(type(d) not in (int, float) or not math.isfinite(d) or d <= 0 for d in row) for row in delays) or
                any(row != delays[0] for row in delays[1:]) or
                len(delays[direction]) != len(definition['frames']) or
                len(delays[direction]) != len(definition['delays']) or
                any(not math.isclose(a, b, rel_tol=1e-7, abs_tol=1e-8) for a, b in zip(delays[direction], definition['delays']))):
            raise ValueError(f"{model['id']}: source frame count or timing differs: {name}")
        checked[name] = delays
    return folder, (width, height), checked


def viewer_references(model, resource_file, png_bytes):
    from PIL import Image
    folder, (width, height), source = validate_source(model, resource_file)
    direction = model.get('referenceDirection', 0)
    outputs, references = {}, {}
    for state, definition in model['spriteStates'].items():
        sheet = Image.open(folder / (state + '.png')).convert('RGBA')
        if sheet.width % width or sheet.height % height or sheet.width // width * (sheet.height // height) < sum(map(len, source[state])):
            raise ValueError(f"{model['id']}: source sheet does not contain all frames: {state}")
        references[state] = []
        for frame in range(len(definition['frames'])):
            index = sum(len(row) for row in source[state][:direction]) + frame
            x, y = index % (sheet.width // width) * width, index // (sheet.width // width) * height
            image = sheet.crop((x, y, x + width, y + height))
            name = f'references/{model["id"]}-{state}-frame{frame}-dir{direction}.png'
            outputs[name] = png_bytes(image)
            references[state].append('../generated/' + name)
    return outputs, references


def timeline(model, state):
    time, changes = 0., []
    for frame, delay in enumerate(model['spriteStates'][state]['delays']):
        changes.append({'time': time, 'frame': f'{state}:{frame}'})
        time += delay
    changes.append({'time': time, 'frame': f'{state}:0'})
    return changes


def model_document(model, build_document):
    from frame_animation import add_clips
    parts, groups = [], {}
    for state, definition in model['spriteStates'].items():
        for frame, composition in enumerate(definition['frames']):
            groups[f'{state}:{frame}'] = list(range(len(parts), len(parts) + len(composition['parts'])))
            parts.extend(composition['parts'])
    clips = [dict(name=state, changes=timeline(model, state), trigger='Source RSI sprite clock (loop)',
                  status='draft source-frame composition; native interaction unverified')
             for state, definition in model['spriteStates'].items() if len(definition['frames']) > 1]
    document, binary = build_document({**model, 'parts': parts})
    original_scales = [node['scale'][:] for node in document['nodes']]
    default = model['referenceState'] + ':0'
    binary = add_clips(document, binary, groups, clips, default)
    # A static state remains selectable without an invented animation clip. Its nodes
    # are separate from animated groups so off geometry cannot leak into a loop.
    static_scenes = {}
    for state, definition in model['spriteStates'].items():
        if len(definition['frames']) != 1:
            continue
        static_nodes = []
        for index in groups[state + ':0']:
            static_nodes.append(len(document['nodes']))
            document['nodes'].append({**document['nodes'][index], 'scale': original_scales[index]})
        static_scenes[state] = len(document['scenes'])
        document['scenes'].append({'name': state, 'nodes': static_nodes})
    for animation in document.get('animations', []):
        animation['extras'].update(loop=True, sourceState=animation['name'])
    if not clips:
        document.pop('animations', None)
    document['extras'].update(spriteFrameGroups=groups, defaultSpriteFrame=default, spriteStaticScenes=static_scenes,
                             sourceSpriteOffset=model['sourceSpriteOffset'],
                             spriteStatePeriods={state: sum(s['delays']) for state, s in model['spriteStates'].items()},
                             statePlayback='Library clips loop the authored RSI delays. Native playback follows the original visible layer frame; saved map exports use frame zero only.')
    return document, binary


def empty_webbing_layer(layer, default_components, saved_components):
    """Prove that the clothing owner's reserved layer has nothing to render.

    Native playback reads the actual layer. A saved map must also exclude the
    owner's attached/starting item and its container before using the bare icon.
    Other empty layer owners are deliberately not inferred here.
    """
    if (set(layer) - {'map', 'visible', 'state', 'texture'} or
            layer.get('map') != ['enum.WebbingVisualLayers.Base'] or
            layer.get('state') is not None or layer.get('texture') is not None or
            'WebbingClothing' not in default_components and 'WebbingClothing' not in saved_components):
        return False
    owner = {**default_components.get('WebbingClothing', {}), **saved_components.get('WebbingClothing', {})}
    if owner.get('webbing') is not None or owner.get('startingWebbing') is not None:
        return False
    if default_components.get('GenericVisualizer') or saved_components.get('GenericVisualizer'):
        return False
    slot = owner.get('container', 'cm_clothing_webbing_slot')
    if not isinstance(slot, str) or not slot:
        return False
    containers = {**default_components.get('ContainerContainer', {}).get('containers', {}),
                  **saved_components.get('ContainerContainer', {}).get('containers', {})}
    contents = containers.get(slot, {})
    return (isinstance(contents, dict) and contents.get('ent') is None and
            contents.get('ents') in (None, []))


def saved_pose(model, default_components, saved_components, normalize_tint):
    """Prove a saved/default single visible layer; return an explicit fallback reason otherwise."""
    sprite = {**default_components.get('Sprite', {}), **saved_components.get('Sprite', {})}
    try:
        stain = {**default_components.get('CMUItemStain', {}), **saved_components.get('CMUItemStain', {})}
        if stain.get('color') is not None:
            return None, 'Item stain requires the live layered sprite owner'
        if 'ToggleableVisuals' in default_components or 'ToggleableVisuals' in saved_components:
            for component in ('HandheldLight', 'ItemToggle'):
                owner = {**default_components.get(component, {}), **saved_components.get(component, {})}
                if owner.get('activated', False) is not False:
                    return None, 'Activated item requires the live toggleable sprite owner'
        normalize_tint(sprite.get('color', '#FFFFFF'))
        from slab_openings import has_opening
        opening = has_opening(model)
        rotating = model.get('sourceSpriteRotates', False)
        no_rotation = sprite.get('noRot', False)
        if (rotating and (no_rotation or opening or sprite.get('snapCardinals', False) or
                          sprite.get('granularLayersRendering', False) or sprite.get('postShader') or sprite.get('postShaders'))):
            return None, 'Rotating source contract requires ordinary unsnapped rendering'
        if (no_rotation is not True and not opening and not rotating or sprite.get('visible') is False or vector2(sprite.get('scale', (1, 1))) != (1, 1) or
                vector2(sprite.get('offset', (0, 0))) != vector2(model['sourceSpriteOffset'])):
            return None, 'Sprite requires its authored rotation contract, unit scale, visibility and proven source offset'
        if opening and (sprite.get('noRot', False) or sprite.get('snapCardinals', False) or
                        sprite.get('granularLayersRendering', False) or sprite.get('postShader') or sprite.get('postShaders') or
                        normalize_tint(sprite.get('color', '#FFFFFF')) != '#FFFFFF' or
                        model.get('sourceDirections', 1) != 1 or len(model['spriteStates']) != 1 or
                        any(len(s['frames']) != 1 for s in model['spriteStates'].values())):
            return None, 'Opening requires the opaque, static, rotating source appearance'
        def zero_rotation(value):
            value = str(value).strip()
            return float(value[:-3] if value.endswith('rad') else value) == 0
        if not zero_rotation(sprite.get('rotation', 0)):
            return None, 'Sprite rotation differs from the authored source'
        layers = sprite.get('layers', [])
        if not isinstance(layers, list) or any(not isinstance(layer, dict) for layer in layers):
            return None, 'Sprite layers are malformed'
        # SpriteComponent.AfterDeserialization creates the top-level state/texture
        # layer when the list is empty, including an explicit `layers: []` override.
        if not layers and (sprite.get('state') is not None or sprite.get('texture') is not None):
            layers = [{'state': sprite.get('state'), 'texture': sprite.get('texture')}]
        if (any(layer.get('map') == ['enum.WebbingVisualLayers.Base'] for layer in layers) and
                not empty_webbing_layer({'map': ['enum.WebbingVisualLayers.Base']}, default_components, saved_components)):
            return None, 'Webbing layer requires an empty source owner and container'
        for layer in layers:
            if (layer.get('map') == ['enum.WebbingVisualLayers.Base'] and
                    layer.get('state') is None and layer.get('texture') is None and
                    not empty_webbing_layer(layer, default_components, saved_components)):
                return None, 'Webbing placeholder requires an empty source owner and container'
        visible = [layer for layer in layers if layer.get('visible', True) and
                   not empty_webbing_layer(layer, default_components, saved_components)]
        if len(visible) != 1:
            return None, 'Sprite does not have exactly one visible layer'
        layer = visible[0]
        if (layer.get('texture') or layer.get('shader') or layer.get('shaderPrototype') or layer.get('copyToShaderParameters') is not None or
                layer.get('dirOffset') not in (None, 0, 'None') or layer.get('directionOffset') not in (None, 0, 'None') or
                vector2(layer.get('scale', (1, 1))) != (1, 1) or vector2(layer.get('offset', (0, 0))) != (0, 0) or
                not zero_rotation(layer.get('rotation', 0)) or normalize_tint(layer.get('color', '#FFFFFF')) != '#FFFFFF'):
            return None, 'Sprite layer has unsupported texture, transform, shader or tint'
        resource = lambda s: str(s).removeprefix('/Textures/').removeprefix('Textures/')
        if resource(layer.get('rsi', sprite.get('sprite', ''))) != resource(model['referenceRsi']):
            return None, 'Sprite layer RSI differs from the authored source'
        state = layer.get('state')
        if 'Stack' in default_components or 'Stack' in saved_components:
            from stack_states import saved_threshold_state
            state, reason = saved_threshold_state(default_components, saved_components, layer, model['spriteStates'])
            if state is None:
                return None, reason
        if state not in model['spriteStates']:
            return None, 'Sprite state has no authored composition'
        # Saved appearance data can drive GenericVisualizer after map loading. Its
        # layer result is not serialized here, so do not infer a default geometry.
        if saved_components.get('Appearance', {}).get('data'):
            return None, 'Saved appearance data requires the live sprite owner'
        return state, None
    except (ValueError, TypeError, KeyError, OverflowError, AttributeError):
        return None, 'Sprite appearance cannot be matched to the authored source'


def resolve_scene_pose(instance, model, default_components, saved_components, normalize_tint):
    state, reason = saved_pose(model, default_components, saved_components, normalize_tint)
    if state is None:
        instance.update(baseModelId=model['id'], modelId=None, modelStatus=None, matchKind='unmapped', unsupportedState=reason)
        return False
    sprite = {**default_components.get('Sprite', {}), **saved_components.get('Sprite', {})}
    instance.update(spriteState=state, spriteFrame=0, referenceState=state,
                    spriteStateTint=normalize_tint(sprite.get('color', '#FFFFFF')))
    return True


def scene_variants(instances, library, variants):
    for entity in instances:
        model = library.get(entity.get('modelId'))
        if not model or 'spriteState' not in entity:
            continue
        state = entity['spriteState']
        tint = entity.get('spriteStateTint', '#FFFFFF')
        key = f"{model['id']}:sprite:{state}:0:{tint}"
        parts = model['spriteStates'][state]['frames'][0]['parts']
        if tint != '#FFFFFF':
            def channels(color):
                color = color.lstrip('#')
                return [int(color[i:i+2], 16) for i in (0, 2, 4)] + [int(color[6:8], 16) if len(color) == 8 else 255]
            multiplier = channels(tint)
            parts = [{**p, 'color': '#' + ''.join(f'{round(a*b/255):02X}' for a, b in zip(channels(p.get('color', '#FFFFFF')), multiplier))}
                     for p in parts]
        variants[key] = parts
        entity['geometryKey'] = key
