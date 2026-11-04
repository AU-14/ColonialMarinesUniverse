"""CMU14: source-owned, independently visible hive layers and portable state scenes."""
import json
import math


def validate(model, validate_model, resource_file):
    states = model['xenoStates']
    if not isinstance(states, dict) or not 1 <= len(states) <= 8:
        raise ValueError('Hive appearances require one to eight audited RSI sources')
    if any(model.get(k) for k in ('spriteStates', 'vehicleLayers', 'doorSpriteStates', 'cornerSurfaces', 'connectToNeighbours')):
        raise ValueError('Hive layers cannot combine with another geometry state owner')
    template = {k: v for k, v in model.items() if k not in ('xenoStates', 'xenoSpriteOffset')}
    checked = {}
    for rsi, definitions in states.items():
        folder = resource_file(rsi)
        metadata = {s['name']: s for s in json.loads((folder / 'meta.json').read_text(encoding='utf-8-sig'))['states']}
        checked[rsi] = {}
        for state, definition in definitions.items():
            source = metadata[state]
            delays = source.get('delays', [[1]])[0]
            if source.get('directions', 1) not in (1, 4) or any(row != delays for row in source.get('delays', [delays])):
                raise ValueError(f'{rsi}/{state}: hive layers require consistent cardinal source frames')
            frames = definition['frames']
            if definition['delays'] != delays or len(frames) != len(delays) or len(frames) > 64:
                raise ValueError(f'{rsi}/{state}: source frame timing mismatch')
            if any(not math.isfinite(d) or d <= 0 for d in delays):
                raise ValueError('Invalid source frame duration')
            checked[rsi][state] = dict(delays=delays, frames=[dict(parts=validate_model({**template, 'parts': f['parts']})['parts'] if f['parts'] else []) for f in frames])
    return {**model, 'xenoStates': checked}


def model_document(model, build_document):
    parts = list(model['parts'])
    scenes = [dict(name='Default composition', nodes=list(range(len(parts))))]
    timings = {}
    for rsi, definitions in model['xenoStates'].items():
        for state, definition in definitions.items():
            name = rsi.rsplit('/', 1)[-1] + '/' + state
            timings[name] = definition['delays']
            for i, frame in enumerate(definition['frames']):
                start = len(parts)
                parts.extend(frame['parts'])
                scene = dict(name=f'{name}:{i}')
                if len(parts) > start:
                    scene['nodes'] = list(range(start, len(parts)))
                scenes.append(scene)
    document, binary = build_document({**model, 'parts': parts})
    document['scenes'] = scenes
    document['scene'] = 0
    document.setdefault('extras', {}).update(xenoLayerDelays=timings,
        statePlayback='Default scene is the composed object. Other scenes expose individual source-layer frames; native playback uses the existing RSI owner.')
    return document, binary
