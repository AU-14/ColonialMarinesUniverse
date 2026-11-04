"""Bounded four-layer reagent-tank presentation; never computes chemistry or activation state."""
from __future__ import annotations

import json
from PIL import Image
from sprite_states import INCOMPATIBLE, vector2

RSI = '_RMC14/Structures/Storage/reagent_tank.rsi'
STATES = ('tank_normal', 'tn_color-1', 'tn_color-2', 't_inactive')
FILL_MAP = 'enum.SolutionContainerLayers.Fill'


def validate(model):
    definition = model.get('reagentTankAppearance')
    if (not isinstance(definition, dict) or set(definition) != {'vesselParts'} or
            model.get('referenceRsi') != RSI or model.get('referenceState') != 'tank_normal' or
            model.get('sourceDirections', 1) != 1 or model.get('placement', 'floor') != 'floor' or
            model.get('useEntityRotation') or model.get('bakedSpriteTint', '#FFFFFF').upper() not in ('#FFFFFF', '#FFFFFFFF') or
            any(model.get(k) for k in (*INCOMPATIBLE, 'spriteStates', 'sourceSpriteOffset', 'directionalModels')) or
            'doorState' in model or 'folded' in model):
        raise ValueError('Reagent tank appearance requires the independent four-layer source and floor geometry')
    labels = definition['vesselParts']
    if (not isinstance(labels, list) or not 1 <= len(labels) <= 128 or
            any(not isinstance(v, str) or not v for v in labels) or len(labels) != len(set(labels))):
        raise ValueError('Vessel parts must be unique nonempty labels')
    for label in labels:
        parts = [part for part in model['parts'] if part.get('label') == label]
        if len(parts) != 1 or parts[0].get('color', '#FFFFFF').upper() not in ('#FFFFFF', '#FFFFFFFF'):
            raise ValueError('Each vessel label must identify exactly one white-multiplier part')
    return model


def validate_source(model, resource_file):
    folder = resource_file(model['referenceRsi'])
    if folder is None:
        raise ValueError('Missing reagent tank source RSI')
    meta = json.loads((folder / 'meta.json').read_text(encoding='utf-8-sig'))
    states = {entry['name']: entry for entry in meta['states']}
    size = meta['size']['x'], meta['size']['y']
    images = {}
    for name in STATES:
        state = states.get(name, {})
        if state.get('name') != name or state.get('directions', 1) != 1 or state.get('delays', [[1]]) != [[1]]:
            raise ValueError('Tank layers must be the known static single-direction source states')
        image = Image.open(folder / (name + '.png')).convert('RGBA')
        if image.size != size:
            raise ValueError('Tank layer sheet is not one source frame')
        images[name] = image
    mask = images['tn_color-1']
    if (mask.tobytes() != images['tn_color-2'].tobytes() or
            not set(mask.getchannel('A').tobytes()) <= {0, 255} or mask.getbbox() is None):
        raise ValueError('Permanent and fill masks must be identical with binary alpha')
    return images


def channels(color):
    from build_models import rgba
    return rgba(color)


def color_hex(values):
    return '#' + ''.join(f'{round(v*255):02X}' for v in values)


def compose_parts(model, *, visible=False, color='#FFFFFF', sprite_tint='#FFFFFF'):
    """Source-over the identical opaque vessel, then multiply overall Sprite RGB exactly once."""
    rgba, overall = channels(color), channels(sprite_tint)
    if overall[3] != 1:
        raise ValueError('Non-opaque overall Sprite tint requires original per-layer blending')
    alpha = rgba[3] if visible else 0
    vessel = tuple(1 - alpha + alpha * c for c in rgba[:3]) + (1,)
    labels = set(model['reagentTankAppearance']['vesselParts'])
    return [{**part, 'color': color_hex(tuple(a*b*c for a, b, c in zip(
        channels(part.get('color', '#FFFFFF')), vessel if part['label'] in labels else (1, 1, 1, 1), overall)))}
        for part in model['parts']]


def saved_pose(model, default_components, saved_components, normalize_tint):
    """Read serialized/default sprite layers, explicitly not their future solution-driven MapInit result."""
    sprite = {**default_components.get('Sprite', {}), **saved_components.get('Sprite', {})}
    try:
        def zero(value):
            return float(str(value).removesuffix('rad').strip()) == 0
        if (sprite.get('noRot') is not True or sprite.get('visible') is False or sprite.get('granularLayersRendering') or
                sprite.get('postShaders') or sprite.get('postShader') or
                vector2(sprite.get('offset', (0, 0))) != (0, 0) or vector2(sprite.get('scale', (1, 1))) != (1, 1) or
                not zero(sprite.get('rotation', 0))):
            raise ValueError('Tank Sprite transform or visibility differs from its source')
        if saved_components.get('Appearance', {}).get('data'):
            raise ValueError('Saved appearance data requires the live solution visualizer')
        overall = normalize_tint(sprite.get('color', '#FFFFFF'))
        if channels(overall)[3] != 1:
            raise ValueError('Non-opaque overall Sprite tint requires original per-layer blending')
        layers = sprite.get('layers')
        if not isinstance(layers, list) or len(layers) != 4:
            raise ValueError('Tank requires exactly four ordered source layers')
        resource = lambda s: str(s).removeprefix('/Textures/').removeprefix('Textures/')
        for index, layer in enumerate(layers):
            if (not isinstance(layer, dict) or layer.get('texture') or layer.get('shader') or layer.get('shaderPrototype') or
                    layer.get('copyToShaderParameters') or
                    layer.get('dirOffset', layer.get('directionOffset')) not in (None, 0, 'None') or
                    vector2(layer.get('scale', (1, 1))) != (1, 1) or vector2(layer.get('offset', (0, 0))) != (0, 0) or
                    not zero(layer.get('rotation', 0)) or resource(layer.get('rsi', sprite.get('sprite', ''))) != RSI):
                raise ValueError('Tank layer source, transform or shader differs')
            tint = normalize_tint(layer.get('color', '#FFFFFF'))
            state = layer.get('state')
            if index == 2:
                if state not in ('tn_color-1', 'tn_color-2') or layer.get('map') != [FILL_MAP]:
                    raise ValueError('Tank Fill layer state or map is unsupported')
            elif (state != ('tank_normal', 'tn_color-1', None, 't_inactive')[index] or
                    not layer.get('visible', True) or tint != '#FFFFFF' or layer.get('map')):
                raise ValueError('Tank fixed layers must remain visible, white and in source order')
        fill = layers[2]
        return {'state': fill['state'], 'visible': fill.get('visible', True),
                'color': normalize_tint(fill.get('color', '#FFFFFF')), 'spriteTint': overall,
                'source': 'serialized/default sprite layers before live solution visualizer'}, None
    except (ValueError, TypeError, KeyError, OverflowError) as error:
        return None, str(error)


def resolve_scene_pose(instance, model, defaults, saved, normalize_tint):
    pose, reason = saved_pose(model, defaults, saved, normalize_tint)
    if pose is None:
        instance.update(baseModelId=model['id'], modelId=None, modelStatus=None, matchKind='unmapped', unsupportedState=reason)
        return False
    instance['reagentTankPose'] = pose
    return True


def scene_variants(instances, library, variants):
    for entity in instances:
        model = library.get(entity.get('modelId'))
        if not model or 'reagentTankPose' not in entity:
            continue
        pose = entity['reagentTankPose']
        key = f"{model['id']}:tank:{pose['state']}:{pose['visible']}:{pose['color']}:{pose['spriteTint']}"
        variants[key] = compose_parts(model, visible=pose['visible'], color=pose['color'], sprite_tint=pose['spriteTint'])
        entity['geometryKey'] = key


def viewer_references(model, resource_file, png_bytes):
    images = validate_source(model, resource_file)
    output, references = {}, {}
    for state, image in images.items():
        name = f"references/{model['id']}-tank-{state}.png"
        output[name] = png_bytes(image)
        references[state] = '../generated/' + name
    return output, references


def model_document(model, build_document):
    document, binary = build_document(model)
    document['extras']['reagentTankAppearance'] = model['reagentTankAppearance']
    document['extras']['reagentTankState'] = 'Permanent white vessel and t_inactive; live Fill RGBA is source-owned. No invented activation clips.'
    return document, binary
