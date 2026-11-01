"""Barricade damage/wire poses and optional source-owned acid frames."""
import barricade_acid

KEYS = ('0', '4', '8', '12')
FIELDS = ('barricadeDamageStates', 'barricadeReinforcementRsi', 'barricadeWiredStates',
          'barricadeWireRsi', 'barricadeWireState') + barricade_acid.FIELDS


def validate(model, validate_model):
    states = model.get('barricadeDamageStates', {})
    if not isinstance(states, dict):
        raise ValueError('barricadeDamageStates must be a mapping')
    if not states:
        if any(model.get(field) for field in FIELDS[1:]):
            raise ValueError('Barricade layer metadata requires damage poses')
        return model
    if (set(states) != {'0', '4', '8', '12'} or model.get('sourceDirections') != 4 or
            model.get('referenceState') != 'DamageOverlay_0' or not model.get('referenceRsi') or
            not model.get('barricadeReinforcementRsi') or
            any(model.get(field) for field in ('doorButtonStates', 'frameAnimations', 'directionalModels',
                                             'connectToNeighbours', 'cornerSurfaces', 'wallMounted'))):
        raise ValueError('Barricade poses require the four known static damage states and explicit body/reinforcement RSIs')
    template = {k: v for k, v in model.items() if k not in FIELDS}
    checked = {}
    for key, frame in states.items():
        if not isinstance(frame, dict) or set(frame) != {'parts'}:
            raise ValueError('Each barricade pose must contain parts')
        checked[key] = {'parts': validate_model({**template, 'parts': frame['parts']})['parts']}
    if checked['0']['parts'] != model['parts']:
        raise ValueError('Intact barricade pose must match default parts')
    wired = model.get('barricadeWiredStates', {})
    if not isinstance(wired, dict):
        raise ValueError('barricadeWiredStates must be a mapping')
    if not wired:
        if model.get('barricadeWireRsi') or model.get('barricadeWireState'):
            raise ValueError('Wire reference requires all four wired compositions')
        return barricade_acid.validate({**model, 'barricadeDamageStates': checked}, validate_model, template)
    if set(wired) != set(KEYS) or not all(isinstance(model.get(field), str) and model[field]
                                        for field in ('barricadeWireRsi', 'barricadeWireState')):
        raise ValueError('Wired poses require all four damage suffixes and an explicit wire reference')
    wired_checked, wire_parts = {}, None
    for key, frame in wired.items():
        if not isinstance(frame, dict) or set(frame) != {'parts'}:
            raise ValueError('Each wired composition must contain parts')
        parts = validate_model({**template, 'parts': frame['parts']})['parts']
        count = len(checked[key]['parts'])
        if len(parts) <= count or parts[:count] != checked[key]['parts']:
            raise ValueError('Wiring must retain the complete corresponding damage pose')
        if wire_parts is not None and parts[count:] != wire_parts:
            raise ValueError('The same wire overlay must be retained across damage poses')
        wire_parts = parts[count:]
        wired_checked[key] = {'parts': parts}
    return barricade_acid.validate({**model, 'barricadeDamageStates': checked, 'barricadeWiredStates': wired_checked}, validate_model, template)


def model_document(model, build_document):
    parts, scenes = [], []
    for field, suffix in (('barricadeDamageStates', ''), ('barricadeWiredStates', '_wired')):
        if not model.get(field):
            continue
        for key in KEYS:
            frame = model[field][key]['parts']
            scenes.append({'name': f'DamageOverlay_{key}{suffix}', 'nodes': list(range(len(parts), len(parts) + len(frame)))})
            parts.extend(frame)
    acid = barricade_acid.append_scenes(model, parts, scenes) if model.get('barricadeAcidStates') else None
    document, binary = build_document({**model, 'parts': parts})
    document.update(scene=0, scenes=scenes)
    document['extras'].update(barricadeDamageScenes={key: index for index, key in enumerate(('0', '4', '8', '12'))},
                             statePlayback='Static damage/wire glTF scenes, not animation clips. Live preview reads the original sprite layers.')
    if model.get('barricadeWiredStates'):
        document['extras']['barricadeWiredScenes'] = {key: index + 4 for index, key in enumerate(KEYS)}
    if acid:
        binary = barricade_acid.add_animations(document, binary, acid)
        document['extras']['statePlayback'] = 'Eight static damage/wire scenes and eight acid loop scenes. Live preview follows original sprite frame and visibility.'
    return document, binary


def saved_pose(model, defaults, saved):
    """Only the unmodified, unbarbed dry snapshot is currently established offline.

    Runtime damage comes from the client's actual layers. Do not derive a saved
    damage pose from stale Appearance data or silently ignore damage/body state.
    """
    relevant = {'Sprite', 'Appearance', 'Damageable', 'Injurable', 'DamageVisuals', 'Barbed',
                'SprayAcided', 'Corrodible', 'Flammable'}
    if relevant & saved.keys():
        return None
    damage = defaults.get('DamageVisuals', {})
    body = defaults.get('Sprite', {})
    if (defaults.get('Barbed', {}).get('isBarbed', False) or 'SprayAcided' in defaults or
            defaults.get('Damageable', {}).get('damage') or defaults.get('Injurable', {}).get('damage') or
            not damage.get('trackAllDamage') or damage.get('overlay') or damage.get('hideIfZero') or
            damage.get('thresholds') != [4, 8, 12] or damage.get('damageDivisor') != 56.25 or
            body.get('color', '#FFFFFF').upper() not in ('#FFFFFF', '#FFFFFFFF', 'WHITE')):
        return None
    layers = body.get('layers', [])
    if len(layers) != 3 or layers[2].get('map') != ['acided'] or layers[2].get('state') or layers[2].get('texture'):
        return None
    for layer, rsi, state, key in zip(layers[:2], (model['referenceRsi'], model['barricadeReinforcementRsi']),
                                    ('DamageOverlay_0', 'AdditionalDamageOverlay_0'),
                                    ('enum.RMCDamageOverlayVisuals.DamageOverlay', 'enum.RMCDamageOverlayVisuals.AdditionalDamageOverlay')):
        if (layer.get('sprite') != rsi or layer.get('state') != state or layer.get('map') != [key] or
                not layer.get('visible', True) or layer.get('color', '#FFFFFF').upper() not in ('#FFFFFF', '#FFFFFFFF', 'WHITE') or
                any(layer.get(field) for field in ('texture', 'offset', 'rotation', 'scale', 'shader'))):
            return None
    return '0'
