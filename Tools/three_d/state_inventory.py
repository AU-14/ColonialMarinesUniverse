#!/usr/bin/env python3
"""Enumerate source state work separately from 3D geometry coverage.

Reads current prototypes and RSI metadata. A resource state is a candidate, not
proof that a gameplay system uses it. No completion is inferred from a matching
state name, a static model, or a valid GLB.
"""
from __future__ import annotations

import argparse
import json
import math
from pathlib import Path
import struct

import inventory as inv
from charger_states import portable_states as charger_states
from foam_wall_states import portable_states as foam_states
from solution_glass_states import portable_states as solution_states

ROOT = Path(__file__).resolve().parents[2]


def resource_states(root, reference):
    path = inv.resource_path(root, reference)
    result = {'reference': reference, 'states': [], 'issues': []}
    if path is None:
        result['issues'].append('Missing resource')
        return result
    result['source'] = path.relative_to(root).as_posix()
    if path.suffix.lower() != '.rsi':
        result['kind'] = 'texture'
        return result
    result['kind'] = 'rsi'
    try:
        meta = json.loads((path / 'meta.json').read_text(encoding='utf-8-sig'))
        result.update(size=meta['size'], license=meta.get('license'), copyright=meta.get('copyright'))
        for state in meta['states']:
            directions = state.get('directions', 1)
            rows = state.get('delays') or [[1.0] for _ in range(directions)]
            if (directions not in (1, 4, 8) or len(rows) != directions or
                    any(not row or any(type(t) not in (float, int) or not math.isfinite(t) or t <= 0 for t in row) for row in rows)):
                result['issues'].append(f"Invalid frame timing: {state['name']}")
                continue
            image = path / (state['name'] + '.png')
            if not image.is_file():
                result['issues'].append(f"Missing state image: {state['name']}")
            animated = any(len(row) > 1 for row in rows)
            result['states'].append({'name': state['name'], 'directions': directions,
                                     'framesPerDirection': [len(row) for row in rows],
                                     'delaysSeconds': rows if 'delays' in state else None,
                                     'durationSeconds': [round(sum(row), 8) for row in rows] if animated else None,
                                     'animated': animated, 'imageExists': image.is_file()})
    except (OSError, ValueError, KeyError, TypeError) as error:
        result['issues'].append(str(error))
    return result


def sprite_layers(sprite):
    if not sprite:
        return []
    # A top-level state/texture is the implicit layer only when no layer list is present.
    layers = sprite.get('layers')
    if layers is None:
        layers = [sprite] if sprite.get('state') is not None or sprite.get('texture') else []
    base = sprite.get('sprite') or sprite.get('rsi')
    result = []
    for index, layer in enumerate(layers):
        if not isinstance(layer, dict):
            continue
        reference = layer.get('rsi') or layer.get('sprite') or base
        texture = layer.get('texture')
        result.append({'index': index, 'keys': layer.get('map', []),
                       'rsi': inv.texture_reference(reference) if isinstance(reference, str) else None,
                       'texture': inv.texture_reference(texture) if isinstance(texture, str) else None,
                       'state': str(layer['state']) if layer.get('state') is not None else None,
                       'visibleByDefault': layer.get('visible', True),
                       'color': layer.get('color', '#FFFFFF'), 'spriteColor': sprite.get('color', '#FFFFFF')})
    return result


def state_fields(value, path=''):
    """Keep provenance for data fields that mention states; do not invent triggers."""
    result = []
    if isinstance(value, dict):
        for key, child in value.items():
            name = str(key)
            location = f'{path}.{name}' if path else name
            if 'state' in name.lower() and name != '__type_tag__':
                result.append({'field': location, 'value': child})
            elif isinstance(child, (dict, list)):
                result.extend(state_fields(child, location))
    elif isinstance(value, list):
        for index, child in enumerate(value):
            result.extend(state_fields(child, f'{path}[{index}]'))
    return result


def glb_animations(path):
    if not path.is_file():
        return {'exists': False, 'clips': [], 'issue': 'Missing exported model'}
    try:
        with path.open('rb') as stream:
            header = stream.read(20)
            magic, version, length, size, kind = struct.unpack('<5I', header)
            if magic != 0x46546C67 or version != 2 or kind != 0x4E4F534A or length != path.stat().st_size:
                raise ValueError('Invalid GLB header')
            document = json.loads(stream.read(size))
        return {'exists': True, 'clips': [{'name': clip.get('name'), 'channels': len(clip.get('channels', []))}
                                         for clip in document.get('animations', [])]}
    except (OSError, ValueError, struct.error) as error:
        return {'exists': True, 'clips': [], 'issue': str(error)}


def build_report(root, saved_inventory, kinds, load_issues):
    resolver = inv.Resolver(kinds['entity'])
    models = sorted(kinds.get('cmu3DModel', {}).values(), key=lambda m: m['id'])
    source_ids = {p['id'] for p in saved_inventory['prototypes'] if p['classification'] == 'visual'}
    for model in models:
        source_ids.update(model.get('sourcePrototypes', []))
        source_ids.update(model.get('randomSpritePrototypes', []))
        if model.get('referencePrototype'):
            source_ids.add(model['referencePrototype'])
    saved = {p['id']: p for p in saved_inventory['prototypes']}
    resources, prototypes, by_reference = {}, {}, {}
    def inspect(reference):
        if reference and reference not in resources:
            resources[reference] = resource_states(root, reference)
    for prototype_id in sorted(source_ids):
        previous = saved.get(prototype_id, {})
        entry = {'id': prototype_id, 'instanceCounts': previous.get('instanceCounts', {}),
                 'classification': previous.get('classification', 'off_map'), 'issues': []}
        try:
            effective = resolver.resolve(prototype_id)
            components = inv.component_map(effective)
            layers = sprite_layers(components.get('Sprite'))
            references = inv.sprite_resources(components.get('Sprite'))
            # GenericVisualizer may switch to another RSI or a plain texture.
            def extra_resources(value):
                if isinstance(value, dict):
                    for key, child in value.items():
                        if key in ('rsi', 'sprite', 'texture') and isinstance(child, str):
                            references.append(inv.texture_reference(child))
                        elif isinstance(child, (dict, list)):
                            extra_resources(child)
                elif isinstance(value, list):
                    for child in value:
                        extra_resources(child)
            extra_resources(components.get('GenericVisualizer', {}))
            references = sorted(set(references))
            for ref in references:
                inspect(ref)
            entry.update(source=effective['_source'], components=sorted(components), layers=layers,
                         resources=references, componentStateFields=state_fields(components),
                         visualizer=components.get('GenericVisualizer'), randomSprite=components.get('RandomSprite'),
                         runtimeTriggerReview='pending')
            for layer in layers:
                if layer['rsi'] and layer['state'] and layer['state'] not in {
                        s['name'] for s in resources.get(layer['rsi'], {}).get('states', [])}:
                    entry['issues'].append(f"Layer {layer['index']}: unresolved default state {layer['state']}")
        except inv.ResolutionError as error:
            entry['issues'].append(str(error))
        prototypes[prototype_id] = entry
    model_entries = []
    for model in models:
        refs = sorted(set(model.get('sourcePrototypes', []) + model.get('randomSpritePrototypes', []) +
                          ([model['referencePrototype']] if model.get('referencePrototype') else [])))
        for reference in refs:
            by_reference.setdefault(reference, []).append(model['id'])
        references = {r for ref in refs for r in prototypes[ref].get('resources', [])}
        if model.get('referenceRsi'):
            references.add(inv.texture_reference(model['referenceRsi']))
        references.update(inv.texture_reference(layer['rsi'])
                          for layer in model.get('solutionAppearance', {}).get('layers', []))
        for reference in references:
            inspect(reference)
        export = glb_animations(root / 'Content.CMU/Resources/Models/CMU14/Garrison' / (model['id'] + '.glb'))
        model_entries.append({'id': model['id'], 'status': model.get('status', 'draft'), 'source': model['_source'],
                              'sourcePrototypes': refs, 'resources': sorted(references),
                              'authoredPoseMetadata': {k: model[k] for k in (
                                  'referenceRsi', 'referenceState', 'referenceDirection', 'directionalModels',
                                  'doorState', 'alternateDoorModel', 'folded', 'alternateFoldModel',
                                  'randomSpriteLayer', 'cornerSurfaces', 'sourceSpriteOffset') if k in model},
                              'authoredSourceFrames': {state: {'frames': len(definition['frames']),
                                                               'unpoweredFrames': len(definition['unpoweredFrames'])}
                                                       for state, definition in model.get('doorButtonStates', {}).items()},
                              'authoredBarricadeDamageStates': sorted(model.get('barricadeDamageStates', {})),
                              'authoredReagentTankAppearance': model.get('reagentTankAppearance'),
                              'authoredChargerCompositions': sum(len(s['frames']) for s in
                                  charger_states(model).values()) if model.get('chargerAppearance') else 0,
                              'authoredFoamCompositions': len(foam_states(model)) if model.get('foamAppearance') else 0,
                              'authoredSolutionCompositions': len(solution_states(model)) if model.get('solutionAppearance') else 0,
                              'authoredPoweredLightStates': sorted(model.get('poweredLightStates', {})),
                              'authoredDoorSpriteFrames': sum(len(s['frames']) for s in model.get('doorSpriteStates', {}).values()),
                              'authoredSpriteStates': {state: {'frames': len(data['frames']), 'delays': data['delays'],
                                                              'loopSeconds': sum(data['delays']), 'animated': len(data['frames']) > 1}
                                                       for state, data in model.get('spriteStates', {}).items()},
                              'authoredBarricadeWiredStates': sorted(model.get('barricadeWiredStates', {})),
                              'authoredBarricadeAcidFrames': sum(len(s[k]) for s in model.get('barricadeAcidStates', {}).values() for k in ('frames', 'wiredFrames')),
                              'export': export, 'allStatesVerified': False,
                              'runtimeTriggerReview': 'source owner reviewed; native interactive review pending'
                              if model.get('solutionAppearance') or model.get('foamAppearance') or model.get('chargerAppearance') or model.get('reagentTankAppearance') or model.get('doorButtonStates') or model.get('barricadeDamageStates') or model.get('poweredLightStates') or model.get('doorSpriteStates') or model.get('spriteStates') else 'pending'})
    for entry in prototypes.values():
        entry['modelIds'] = by_reference.get(entry['id'], [])
        entry['resourceStateCount'] = sum(len(resources[r]['states']) for r in entry.get('resources', []))
        entry['animatedResourceStateCount'] = sum(s['animated'] for r in entry.get('resources', []) for s in resources[r]['states'])
    redux = [p for p in prototypes.values() if p['instanceCounts'].get('redux', 0) and p['classification'] == 'visual']
    redux_resources = {r for p in redux for r in p.get('resources', [])}
    summary = {'modelCount': len(models), 'modelExportsFound': sum(m['export']['exists'] for m in model_entries),
               'exportedAnimationClips': sum(len(m['export']['clips']) for m in model_entries),
               'modelsWithSourceOwnedReagentFill': sum(bool(m['authoredReagentTankAppearance']) for m in model_entries),
               'modelsWithSourceOwnedCharger': sum(bool(m['authoredChargerCompositions']) for m in model_entries),
               'authoredChargerCompositions': sum(m['authoredChargerCompositions'] for m in model_entries),
               'modelsWithSourceOwnedFoam': sum(bool(m['authoredFoamCompositions']) for m in model_entries),
               'authoredFoamCompositions': sum(m['authoredFoamCompositions'] for m in model_entries),
               'modelsWithSourceOwnedSolutionGlass': sum(bool(m['authoredSolutionCompositions']) for m in model_entries),
               'authoredSolutionCompositions': sum(m['authoredSolutionCompositions'] for m in model_entries),
               'modelsWithAuthoredSourceFrames': sum(bool(m['authoredSolutionCompositions']) or bool(m['authoredFoamCompositions']) or bool(m['authoredChargerCompositions']) or bool(m['authoredSourceFrames']) or bool(m['authoredBarricadeAcidFrames']) or bool(m['authoredDoorSpriteFrames']) or bool(m['authoredSpriteStates']) for m in model_entries),
               'authoredSourceFrameCompositions': sum(sum(s['frames'] + s['unpoweredFrames'] for s in m['authoredSourceFrames'].values())
                                                       + m['authoredBarricadeAcidFrames'] + m['authoredDoorSpriteFrames'] + m['authoredChargerCompositions'] + m['authoredFoamCompositions'] + m['authoredSolutionCompositions']
                                                       + sum(s['frames'] for s in m['authoredSpriteStates'].values()) for m in model_entries),
               'authoredGenericSpriteStates': sum(len(m['authoredSpriteStates']) for m in model_entries),
               'authoredGenericSpriteCompositions': sum(s['frames'] for m in model_entries for s in m['authoredSpriteStates'].values()),
               'authoredGenericSpriteLoops': sum(s['animated'] for m in model_entries for s in m['authoredSpriteStates'].values()),
               'authoredDoorSpriteCompositions': sum(m['authoredDoorSpriteFrames'] for m in model_entries),
               'authoredDoorControlFrameCompositions': sum(s['frames'] + s['unpoweredFrames'] for m in model_entries
                                                           for s in m['authoredSourceFrames'].values()),
               'authoredBarricadeDamageCompositions': sum(len(m['authoredBarricadeDamageStates']) for m in model_entries),
               'authoredPoweredLightCompositions': sum(len(m['authoredPoweredLightStates']) for m in model_entries),
               'authoredBarricadeWiredCompositions': sum(len(m['authoredBarricadeWiredStates']) for m in model_entries),
               'authoredBarricadeAcidCompositions': sum(m['authoredBarricadeAcidFrames'] for m in model_entries),
               'modelsWithAllStatesVerified': 0, 'reduxVisualPrototypes': len(redux),
               'reduxVisualPrototypesWithAnimatedResources': sum(p['animatedResourceStateCount'] > 0 for p in redux),
               'reduxResources': len(redux_resources),
               'reduxResourceStates': sum(len(resources[r]['states']) for r in redux_resources),
               'reduxAnimatedResourceStates': sum(s['animated'] for r in redux_resources for s in resources[r]['states']),
               'sourceResources': len(resources), 'resourceIssues': sum(len(r['issues']) for r in resources.values()),
               'prototypeIssues': sum(len(p['issues']) for p in prototypes.values()),
               'exportIssues': sum('issue' in m['export'] for m in model_entries), 'prototypeLoadIssues': len(load_issues)}
    return {'schemaVersion': 1, 'primaryMap': 'Stable Garrison Redux',
            'scope': {'staticResourceEnumeration': True, 'fullSourceStateInventoryComplete': False,
                      'limitations': ['Resource states include unused art; runtime reachability and component code defaults need review.',
                                      'Prototype fields use the inventory resolver; custom component inheritance is not simulated.',
                                      'Saved instance overrides, procedural overlays, runtime spawns and character equipment are not exhaustive.',
                                      'Static poses and clip presence do not certify state, animation or gameplay completeness.']},
            'summary': summary, 'models': model_entries, 'prototypes': list(prototypes.values()),
            'resources': dict(sorted(resources.items())), 'loadIssues': load_issues,
            'reduxAnimatedQueue': [p['id'] for p in sorted(redux, key=lambda p: (-p['instanceCounts'].get('redux', 0), p['id']))
                                   if p['animatedResourceStateCount']]}


def markdown(report):
    summary = report['summary']
    lines = ['# Redux source-state inventory', '', 'Generated with `python Tools/three_d/state_inventory.py`.', '',
             f"{summary['modelCount']} models inspected; {summary['exportedAnimationClips']} animation clips in their library GLBs. "
             f"{summary['modelsWithAllStatesVerified']} models have every state verified.", '',
             f"Redux has {summary['reduxVisualPrototypes']} saved visual prototype types. Their referenced resources contain "
             f"{summary['reduxResourceStates']} distinct RSI states, including {summary['reduxAnimatedResourceStates']} animated states. "
             'Resource counts include unused art and are not a gameplay completion denominator.', '',
             f"Generic sprite-state models contain {summary['authoredGenericSpriteStates']} authored states, "
             f"{summary['authoredGenericSpriteCompositions']} frame compositions and {summary['authoredGenericSpriteLoops']} portable loops. "
             'These counts describe authored exports, not verified live gameplay.', '',
             'The JSON includes every model, resolved source layers, layer visibility, visualizer rules, candidate state fields, '
             'RSI directions and frame delays. Trigger review remains pending.', '',
             '| Check | Count |', '| --- | ---: |']
    for key in ('resourceIssues', 'prototypeIssues', 'exportIssues', 'prototypeLoadIssues'):
        lines.append(f'| {key} | {summary[key]} |')
    lines += ['', '## Animated source work queue', '', 'Sorted by Redux placements, not modeling effort.', '',
              '| Prototype | Redux instances | Resource states | Animated resource states | Mapped assemblies |',
              '| --- | ---: | ---: | ---: | ---: |']
    entries = {p['id']: p for p in report['prototypes']}
    for uid in report['reduxAnimatedQueue'][:60]:
        p = entries[uid]
        lines.append(f"| {uid} | {p['instanceCounts']['redux']} | {p['resourceStateCount']} | {p['animatedResourceStateCount']} | {len(p['modelIds'])} |")
    lines += ['', '## Limits', ''] + ['- ' + s for s in report['scope']['limitations']]
    return '\n'.join(lines) + '\n'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--inventory', type=Path, default=ROOT / 'Tools/three_d/generated/inventory.json')
    parser.add_argument('--output', type=Path, default=ROOT / 'Tools/three_d/generated')
    args = parser.parse_args()
    kinds, issues, _ = inv.load_prototypes(ROOT)
    report = build_report(ROOT, json.loads(args.inventory.read_text(encoding='utf-8')), kinds, issues)
    args.output.mkdir(parents=True, exist_ok=True)
    (args.output / 'state-inventory.json').write_text(json.dumps(report, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
    (args.output / 'state-inventory.md').write_text(markdown(report), encoding='utf-8')
    print(json.dumps(report['summary'], indent=2))


if __name__ == '__main__':
    main()
