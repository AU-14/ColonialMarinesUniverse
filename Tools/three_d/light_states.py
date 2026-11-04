"""Powered-light poses selected by the existing base sprite layer, without a second timer."""


def validate(model, validate_model):
    states = model.get('poweredLightStates')
    reference = model.get('referenceState', '')
    prefixes = ('bulb', 'bbulb', 'tube', 'btube', 'ptube', 'bptube')
    suffixes = ('1', '0', '-empty', '-broken', '-burned')
    prefix = next((p for p in prefixes if reference in {p + s for s in suffixes}), None)
    expected = {prefix + suffix for suffix in suffixes} if prefix else set()
    if (not isinstance(states, dict) or set(states) != expected or model.get('referenceState') not in states or
            not model.get('referenceRsi') or model.get('sourceDirections') != 4 or not model.get('wallMounted') or
            any(model.get(k) for k in ('doorButtonStates', 'frameAnimations', 'barricadeDamageStates',
                                      'directionalModels', 'connectToNeighbours', 'cornerSurfaces'))):
        raise ValueError('Powered light requires five known static light poses, four directions and a wall mount')
    template = {k: v for k, v in model.items() if k != 'poweredLightStates'}
    checked = {}
    for key, frame in states.items():
        if not isinstance(frame, dict) or set(frame) != {'parts'}:
            raise ValueError('Each powered-light state must contain parts')
        checked[key] = {'parts': validate_model({**template, 'parts': frame['parts']})['parts']}
    if checked[model['referenceState']]['parts'] != model['parts']:
        raise ValueError('Default light state must match default parts')
    return {**model, 'poweredLightStates': checked}


def model_document(model, build_document):
    parts, scenes = [], []
    for key, frame in model['poweredLightStates'].items():
        scenes.append({'name': key, 'nodes': list(range(len(parts), len(parts) + len(frame['parts'])))})
        parts.extend(frame['parts'])
    document, binary = build_document({**model, 'parts': parts})
    document.update(scene=list(model['poweredLightStates']).index(model['referenceState']), scenes=scenes)
    document['extras'].update(poweredLightScenes={s['name']: i for i, s in enumerate(scenes)},
                             statePlayback='Static bulb poses. Live preview follows PoweredLightLayers.Base, including owner-driven blinking. No illumination or independent blink timer.')
    return document, binary
