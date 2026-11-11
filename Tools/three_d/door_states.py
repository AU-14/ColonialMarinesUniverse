"""Door sprite frames owned by the existing DoorSystem, with portable preview clips."""
import math

FIELDS = ('doorSpriteStates', 'doorAnimationDurations')
STATES = ('closed', 'open', 'opening', 'closing')


def validate(model, validate_model):
    states, durations = (model.get(k) for k in FIELDS)
    if (not isinstance(states, dict) or set(states) != set(STATES) or
            not isinstance(durations, dict) or set(durations) != {'opening', 'closing'} or
            model.get('referenceState') not in ('closed', 'open') or not model.get('referenceRsi') or
            model.get('sourceDirections') != 4 or model.get('placement', 'floor') != 'floor' or
            any(model.get(k) for k in ('doorButtonStates', 'frameAnimations', 'poweredLightStates',
                                      'barricadeDamageStates', 'directionalModels', 'wallMounted',
                                      'connectToNeighbours', 'cornerSurfaces', 'faceAwayFromWall'))):
        raise ValueError('Door sprite frames require four known states and independent four-direction geometry')
    template = {k: v for k, v in model.items() if k not in FIELDS}
    checked = {}
    for state in STATES:
        definition = states[state]
        if not isinstance(definition, dict) or set(definition) != {'frames', 'delays'}:
            raise ValueError('Door state requires frames and source delays')
        frames, delays = definition['frames'], definition['delays']
        if (not isinstance(frames, list) or not 1 <= len(frames) <= 32 or
                not isinstance(delays, list) or len(delays) != len(frames) or
                any(type(d) not in (int, float) or not math.isfinite(d) or d <= 0 for d in delays) or
                state in ('closed', 'open') and len(frames) != 1):
            raise ValueError('Door frames need matching finite positive delays; stable states have one frame')
        parts = []
        for frame in frames:
            if not isinstance(frame, dict) or set(frame) != {'parts'}:
                raise ValueError('Door frame requires parts')
            parts.append({'parts': validate_model({**template, 'parts': frame['parts']})['parts']})
        checked[state] = {'frames': parts, 'delays': delays}
        if state in durations:
            duration = durations[state]
            if type(duration) not in (int, float) or not math.isfinite(duration) or duration < sum(delays) - 1e-7:
                raise ValueError('Door animation duration must include its complete source strip')
    if checked[model['referenceState']]['frames'][0]['parts'] != model['parts']:
        raise ValueError('Default door geometry must match its stable reference frame')
    # Frame-dependent mounting must not move a shutter through its glazing.
    depth = lambda parts: (min(p['min'][1] for p in parts), max(p['max'][1] for p in parts))
    if model.get('windowMountTargets') and any(depth(f['parts']) != depth(model['parts'])
            for state in checked.values() for f in state['frames']):
        raise ValueError('Mounted door frames must preserve the full mounting depth')
    return {**model, 'doorSpriteStates': checked}


def timeline(model, state):
    definition = model['doorSpriteStates'][state]
    changes, time = [], 0
    for index, delay in enumerate(definition['delays']):
        changes.append({'time': round(time, 7), 'frame': f'{state}:{index}'})
        time += delay
    changes.append({'time': model['doorAnimationDurations'][state],
                    'frame': ('open' if state == 'opening' else 'closed') + ':0'})
    return changes


def model_document(model, build_document):
    from frame_animation import add_clips
    parts, groups = [], {}
    for state, definition in model['doorSpriteStates'].items():
        for index, frame in enumerate(definition['frames']):
            groups[f'{state}:{index}'] = list(range(len(parts), len(parts) + len(frame['parts'])))
            parts.extend(frame['parts'])
    clips = [dict(name=state.capitalize(), changes=timeline(model, state),
                  trigger='DoorSystem Base sprite flick, clamped through animation completion',
                  status='draft source-frame geometry; native interaction unverified') for state in ('opening', 'closing')]
    document, binary = build_document({**model, 'parts': parts})
    default = model['referenceState'] + ':0'
    binary = add_clips(document, binary, groups, clips, default)
    document['extras'].update(doorSpriteFrameGroups=groups, defaultDoorSpriteFrame=default,
                             doorAnimationDurations=model['doorAnimationDurations'],
                             statePlayback='Live preview reads the original Base RSI frame, including completion and interruption. Map exports remain saved stable snapshots.')
    return document, binary


def viewer_references(model, resource_file, png_bytes):
    """RSI packs frames consecutively within each direction, then into sheet rows."""
    import json
    from PIL import Image
    folder = resource_file(model['referenceRsi'])
    meta = json.loads((folder / 'meta.json').read_text(encoding='utf-8-sig'))
    metadata = {s['name']: s for s in meta['states']}
    width, height = meta['size']['x'], meta['size']['y']
    outputs, references = {}, {}
    for state, definition in model['doorSpriteStates'].items():
        source = metadata[state]
        if source.get('directions', 1) != 4:
            raise ValueError('Door source must supply all four directions')
        delays = source.get('delays', [[1]] * 4)
        if any(row != definition['delays'] for row in delays):
            raise ValueError('Door source timing differs from the authored frame timing')
        sheet = Image.open(folder / (state + '.png')).convert('RGBA')
        references[state] = []
        for frame in range(len(definition['frames'])):
            urls = []
            for direction in range(4):
                index = sum(len(row) for row in delays[:direction]) + frame
                x, y = index % (sheet.width // width) * width, index // (sheet.width // width) * height
                image = sheet.crop((x, y, x + width, y + height))
                name = f'references/{model["id"]}-{state}-frame{frame}-dir{direction}.png'
                outputs[name] = png_bytes(image)
                urls.append('../generated/' + name)
            references[state].append(urls)
    return outputs, references
