"""Source-owned five-frame barricade acid compositions and portable loop exports."""
from copy import deepcopy

FIELDS = ('barricadeAcidStates', 'barricadeAcidRsi', 'barricadeAcidState', 'barricadeAcidDelays')
KEYS = ('0', '4', '8', '12')
PART_LIMIT = 160


def validate(model, validate_model, template):
    states = model.get('barricadeAcidStates', {})
    if not isinstance(states, dict):
        raise ValueError('Acid compositions must be a mapping')
    if not states:
        if any(model.get(field) for field in FIELDS[1:]):
            raise ValueError('Acid metadata requires frame compositions')
        return model
    if (set(states) != set(KEYS) or not model.get('barricadeWiredStates') or
            not isinstance(model.get('barricadeAcidRsi'), str) or not model['barricadeAcidRsi'] or
            model.get('barricadeAcidState') != 'acid' or model.get('barricadeAcidDelays') != [.1]*5):
        raise ValueError('Acid requires four damage/wire contexts and the five 0.1-second source frames')
    checked, overlay = {}, {}
    for key in KEYS:
        state = states[key]
        if not isinstance(state, dict) or set(state) != {'frames', 'wiredFrames'}:
            raise ValueError('Acid damage context needs frames and wiredFrames')
        checked[key] = {}
        for field, base_field in (('frames', 'barricadeDamageStates'), ('wiredFrames', 'barricadeWiredStates')):
            frames = state[field]
            if not isinstance(frames, list) or len(frames) != 5:
                raise ValueError('Acid source has exactly five frames')
            base = model[base_field][key]['parts']
            checked[key][field] = []
            for index, frame in enumerate(frames):
                if not isinstance(frame, dict) or set(frame) != {'parts'}:
                    raise ValueError('Acid frame must contain parts')
                parts = validate_model({**template, 'parts': frame['parts']}, part_limit=PART_LIMIT)['parts']
                if parts[:len(base)] != base or len(parts) <= len(base):
                    raise ValueError('Acid composition must preserve its complete damage/wire pose')
                extra = parts[len(base):]
                if index in overlay and overlay[index] != extra:
                    raise ValueError('Acid frames must be consistent across damage/wire contexts')
                overlay[index] = extra
                checked[key][field].append({'parts': parts})
    return {**model, 'barricadeAcidStates': checked}


def append_scenes(model, parts, scenes):
    groups, clips, indices, defaults = {}, [], {}, []
    for wired, collection in ((False, 'frames'), (True, 'wiredFrames')):
        for key in KEYS:
            context = key + (':wire' if wired else '')
            nodes = []
            indices[context] = len(scenes)
            for index, frame in enumerate(model['barricadeAcidStates'][key][collection]):
                name = context + ':acid:' + str(index)
                group = list(range(len(parts), len(parts)+len(frame['parts'])))
                groups[name] = group
                nodes.extend(group)
                parts.extend(frame['parts'])
                if index == 0:
                    defaults.append(name)
            changes, time = [], 0
            for index, delay in enumerate(model['barricadeAcidDelays']):
                changes.append({'time': round(time, 6), 'frame': context + ':acid:' + str(index)})
                time += delay
            changes.append({'time': round(time, 6), 'frame': context + ':acid:0'})
            name = 'Acid_damage' + key + ('_wired' if wired else '')
            clips.append({'name': name, 'trigger': 'SprayAcided visualizer; strip loops while the original layer is visible',
                          'status': 'draft acid loop; expiry/water removal remain owned by gameplay', 'changes': changes})
            scenes.append({'name': name, 'nodes': nodes})
    return groups, clips, indices, defaults


def add_animations(document, binary, acid):
    from frame_animation import add_clips
    groups, clips, indices, defaults = acid
    scales = [deepcopy(node['scale']) for node in document['nodes']]
    binary = add_clips(document, binary, groups, clips, defaults[0])
    # Each acid scene opens on its own first frame; the eight dry scenes retain their static scales.
    for key in defaults:
        for node in groups[key]:
            document['nodes'][node]['scale'] = scales[node]
    for clip in document['animations']:
        clip['extras'].update(loop=True, returnsToIdle=False, lifetimeControlledBy='Original SprayAcided layer visibility; not the 0.5-second strip')
    document['extras'].update(barricadeAcidScenes=indices, barricadeAcidFrameGroups=groups,
                             acidClipSceneSelection='Select the matching Acid scene before playing its named clip; default scene remains dry intact.')
    return binary
