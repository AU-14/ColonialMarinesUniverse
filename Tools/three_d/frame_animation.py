"""Core glTF frame-switching clips for explicit source-frame assemblies."""
import struct


def model_document(model, build_document):
    parts, groups = [], {}
    for state, definition in model['doorButtonStates'].items():
        for collection, suffix in (('frames', 'powered'), ('unpoweredFrames', 'unpowered')):
            for index, frame in enumerate(definition[collection]):
                key = f'{state}:{index}:{suffix}'
                groups[key] = list(range(len(parts), len(parts) + len(frame['parts'])))
                parts.extend(frame['parts'])
    default = f"{model['referenceState']}:0:powered"
    clips = [{'name': name, 'trigger': 'Existing door-control sprite visualizer', 'status': 'draft source-frame geometry',
              'changes': [{'time': key['time'], 'frame': f"{key['state']}:{key.get('frame', 0)}:{'unpowered' if key.get('unpowered') else 'powered'}"}
                          for key in keys]} for name, keys in model['frameAnimations'].items()]
    document, binary = build_document({**model, 'parts': parts})
    binary = add_clips(document, binary, groups, clips, default)
    document['extras'].update(defaultFrame=default, frameGroups=groups,
                             statePlayback='Live preview follows Sprite layers; offline map regions remain static snapshots.')
    return document, binary

def add_clips(document, binary, groups, clips, default):
    """Portable core glTF STEP-scale tracks; only one full source frame is visible."""
    scales = [node['scale'][:] for node in document['nodes']]
    for key, nodes in groups.items():
        for node in nodes:
            document['nodes'][node]['scale'] = scales[node] if key == default else [0, 0, 0]
    def accessor(values, kind):
        nonlocal binary
        flat = values if kind == 'SCALAR' else [v for row in values for v in row]
        data = struct.pack(f'<{len(flat)}f', *flat)
        document['bufferViews'].append({'buffer': 0, 'byteOffset': len(binary), 'byteLength': len(data)})
        index = len(document['accessors'])
        entry = {'bufferView': len(document['bufferViews']) - 1, 'componentType': 5126,
                 'count': len(values), 'type': kind}
        if kind == 'SCALAR':
            entry.update(min=[min(values)], max=[max(values)])
        document['accessors'].append(entry)
        binary += data
        return index
    document['animations'] = []
    # Include every node in every clip so switching clips cannot leave a previous frame visible.
    for clip in clips:
        times = [change['time'] for change in clip['changes']]
        input_id = accessor(times, 'SCALAR')
        animation = {'name': clip['name'], 'samplers': [], 'channels': [],
                     'extras': {'sourceTrigger': clip['trigger'], 'durationSeconds': times[-1],
                                'returnsToIdle': clip['changes'][-1]['frame'] == default, 'status': clip.get('status', 'study; runtime integration pending')}}
        output_ids = {}
        for key, nodes in groups.items():
            for node in nodes:
                values = [scales[node] if change['frame'] == key else [0, 0, 0] for change in clip['changes']]
                signature = tuple(tuple(v) for v in values)
                if signature not in output_ids:
                    output_ids[signature] = accessor(values, 'VEC3')
                sampler = len(animation['samplers'])
                animation['samplers'].append({'input': input_id, 'output': output_ids[signature], 'interpolation': 'STEP'})
                animation['channels'].append({'sampler': sampler, 'target': {'node': node, 'path': 'scale'}})
        document['animations'].append(animation)
    document['buffers'][0]['byteLength'] = len(binary)
    return binary
