"""CMU14: validate the bounded source-layer assemblies used by 3D vehicles."""
import json

from sprite_states import vector2


def validate(model, validate_model):
    layers = model.get('vehicleLayers')
    if not isinstance(layers, list) or not 1 <= len(layers) <= 128 or not model.get('useEntityRotation'):
        raise ValueError('Vehicle layers require 1..128 explicit source states and physical entity rotation')
    if any(model.get(key) for key in ('spriteStates', 'floorOpening', 'ceilingOpening', 'randomSpritePrototypes',
                                      'directionalModels', 'connectToNeighbours', 'wallMounted', 'equipmentOnly')):
        raise ValueError('Vehicle layer assemblies cannot compete with another placement/state adapter')
    template = {k: v for k, v in model.items() if k not in ('vehicleLayers', 'vehicleTurretPrototypes')}
    seen, result = set(), []
    for layer in layers:
        key = (layer.get('rsi'), layer.get('state'))
        if any(not isinstance(x, str) or not x for x in key) or key in seen:
            raise ValueError('Vehicle layers require unique RSI/state pairs')
        seen.add(key)
        result.append({**layer, 'parts': validate_model({**template, 'parts': layer['parts']})['parts'] if layer['parts'] else []})
    return {**model, 'vehicleLayers': result,
            'vehicleSpriteOffset': vector2(model.get('vehicleSpriteOffset', [0, 0])),
            'vehicleSpriteScale': vector2(model.get('vehicleSpriteScale', [1, 1]))}


def validate_source(model, resource_file):
    for layer in model['vehicleLayers']:
        folder = resource_file('/Textures/' + layer['rsi'])
        meta = json.loads((folder / 'meta.json').read_text(encoding='utf-8-sig'))
        if not any(s['name'] == layer['state'] for s in meta['states']):
            raise ValueError('Missing vehicle source state: ' + layer['state'])
