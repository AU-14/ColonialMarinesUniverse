"""Rebuild saved review scenes once per model batch and export shared local contexts.

Existing GLBs are checked by hashes, while newly authored GLBs and the clustered
context exports are listed for targeted external glTF validation.
"""
from collections import Counter
import argparse
import hashlib
import json
from pathlib import Path

import build_models as bm
import inventory as inv
import scene
from export_scene import export_region

ROOT = bm.ROOT
OUT = ROOT / 'Tools/three_d/generated'
BASE = ROOT / '.codex/ladder-install-baseline872'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def rejected_inherited_candidate(before, after, library, new_ids):
    """A new ancestor can improve fallback diagnostics without replacing an unmapped sprite."""
    base = after.get('baseModelId')
    if (base not in new_ids or before.get('modelId') is not None or after.get('modelId') is not None or
            before.get('matchKind') != 'unmapped' or after.get('matchKind') != 'unmapped' or
            not isinstance(after.get('unsupportedState'), str) or not after['unsupportedState'] or
            after.get('prototype') in library[base]['sourcePrototypes'] or
            after.get('matchedPrototype') not in library[base]['sourcePrototypes'] or
            after.get('candidateModelIds') != [base]):
        return False
    diagnostic_fields = {'baseModelId', 'candidateModelIds', 'matchedPrototype', 'unsupportedState'}
    return all(before.get(key) == after.get(key) for key in (set(before) | set(after)) - diagnostic_fields)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--baseline', type=Path, default=BASE)
    parser.add_argument('--prefix', default='fast-batch')
    parser.add_argument('--include-model-prefix', action='append', default=[])
    args = parser.parse_args()
    if not args.prefix or any(ch not in 'abcdefghijklmnopqrstuvwxyz0123456789-' for ch in args.prefix):
        parser.error('prefix must contain lowercase letters, numbers and hyphens only')
    baseline, prefix = args.baseline, args.prefix
    models = bm.load_models()
    library = {m['id']: m for m in models}
    previous = json.loads((baseline / 'models.json').read_text())
    old_ids = {m['id'] for m in previous['models']}
    new_ids = set(library) - old_ids
    checked_ids = new_ids | {uid for uid in library if any(uid.startswith(p) for p in args.include_model_prefix)}
    assert old_ids <= set(library), 'Batch removed an existing model'
    vector_fields = {'min', 'max', 'groundOffset', 'sourceSpriteOffset', 'renderOffset',
                     'terrainCutoutMin', 'terrainCutoutMax'}
    def native_vectors(value, location):
        if isinstance(value, dict):
            for key, child in value.items():
                if key in vector_fields:
                    assert isinstance(child, str), f'Native vector must be a scalar at {location}.{key}'
                native_vectors(child, f'{location}.{key}')
        elif isinstance(value, list):
            for index, child in enumerate(value):
                native_vectors(child, f'{location}[{index}]')
    for path in bm.SOURCE.glob('*.yml'):
        for model in inv.load_yaml(path.read_text(encoding='utf-8')) or []:
            if model.get('type') == 'cmu3DModel' and model['id'] in checked_ids:
                native_vectors(model, model['id'])
    inventory = json.loads((OUT / 'inventory.json').read_text())
    kinds, issues, _ = inv.load_prototypes(ROOT)
    from solution_glass_states import configure_source
    configure_source(kinds)
    resolver = inv.Resolver(kinds['entity'])
    defaults, errors = {}, {}
    specs = json.loads((baseline / 'scenes.json').read_text())
    contexts = OUT / f'{prefix}-contexts'
    contexts.mkdir(exist_ok=True)
    manifest, checks, exports = [], [], []
    for spec in specs:
        variant, level = spec['variant'], spec['level']
        name = f'{variant}-' + ('surface' if level == 0 else f'{"plus" if level > 0 else "minus"}{abs(level)}')
        path, definition = scene.configured_map(ROOT, variant, level)
        header, records = scene.read_map(path)
        for uid in sorted({r['prototype'] for r in records.values()} - {''}):
            if uid in defaults or uid in errors:
                continue
            try:
                defaults[uid] = inv.component_map(resolver.resolve(uid))
            except inv.ResolutionError as error:
                errors[uid] = str(error)
        current = scene.build_scene(header, records, inventory, models, defaults, level=level)
        current['map'].update(path=path.relative_to(ROOT).as_posix(), variant=variant,
                              name=definition.get('mapName', variant))
        current['diagnostics']['prototypeLoadIssues'] = issues
        current['diagnostics']['prototypeResolutionErrors'] = [dict(prototype=p, error=errors[p])
            for p in sorted({r['prototype'] for r in records.values()}) if p in errors]
        scene.enrich_materials(current, ROOT)
        before = json.loads((baseline / spec['file']).read_text())
        old = {e['id']: e for e in before['instances']}
        current_ids = {entity['id'] for entity in current['instances']}
        assert current_ids <= set(old), (name, 'Unexpected added source instances')
        removed = set(old) - current_ids
        hidden = {entry['id']: entry for entry in current['diagnostics'].get('sourceSubfloorHidden', [])}
        assert removed <= set(hidden), (name, 'Unexplained removed source instances', removed - set(hidden))
        from subfloor_visibility import PROTOTYPES as subfloor_prototypes
        assert all(old[uid]['prototype'] in subfloor_prototypes and
                   hidden[uid]['prototype'] == old[uid]['prototype'] and
                   hidden[uid]['reason'] == 'Covered by source floor tile' for uid in removed), name
        target = [e for e in current['instances'] if e.get('modelId') in checked_ids and e['matchKind'] == 'exact']
        candidates = [e for e in current['instances'] if e.get('modelId') in new_ids and e['matchKind'] == 'inherited']
        target_ids = {e['id'] for e in target}
        candidate_ids = {e['id'] for e in candidates}
        changed_neighbors, rejected_candidates = [], []
        for entity in current['instances']:
            original = old[entity['id']]
            assert entity['position'] == original['position'] and entity['yaw'] == original['yaw'], (name, entity['id'])
            if entity != original and rejected_inherited_candidate(original, entity, library, new_ids):
                rejected_candidates.append(dict(id=entity['id'], prototype=entity['prototype'],
                    candidate=entity['baseModelId'], reason=entity['unsupportedState']))
                continue
            if entity != original and entity['id'] not in target_ids | candidate_ids:
                changed_neighbors.append(dict(id=entity['id'], prototype=entity['prototype'],
                                              fields=sorted(k for k in set(entity) | set(original)
                                                            if entity.get(k) != original.get(k))))
        file = OUT / f'{prefix}-{name}-scene.json'
        file.write_text(json.dumps(current, ensure_ascii=False, separators=(',', ':')) + '\n', encoding='utf-8')
        manifest.append(dict(variant=variant, level=level, file=file.name,
                             newModelInstances=sum(e['modelId'] in new_ids for e in target), targetModelInstances=len(target)))
        # One local context may cover several nearby new props, avoiding duplicate exports.
        pending = sorted(target, key=lambda e: e['id'])
        while pending:
            center = pending[0]['position']
            covered = [e for e in pending if max(abs(e['position'][i] - center[i]) for i in (0, 1)) <= 2]
            covered_ids = {e['id'] for e in covered}
            binary, report = export_region(current, models, center, 3, False, True)
            filename = contexts / f'{name}-{pending[0]["id"]}.glb'
            filename.write_bytes(binary)
            filename.with_suffix('.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
            exports.append(dict(file=str(filename.relative_to(ROOT)).replace('\\', '/'),
                                scene=file.name, targetIds=sorted(covered_ids), sha256=digest(filename)))
            pending = [e for e in pending if e['id'] not in covered_ids]
        checks.append(dict(variant=variant, level=level, mapSha256=digest(path), sceneSha256=digest(file),
                           sourceTransformsPreserved=len(current['instances']),
                           checkedPlacements=[dict(id=e['id'], prototype=e['prototype'], modelId=e['modelId'],
                                               position=e['position'], yaw=e['yaw'], renderYaw=e.get('renderYaw'),
                                               renderOffset=e.get('renderOffset'), support=e.get('support'),
                                               geometryKey=e.get('geometryKey'), cornerStates=e.get('cornerStates')) for e in target],
                           matchKinds=dict(Counter(e['matchKind'] for e in target)),
                           newInheritedCandidates=[dict(id=e['id'], prototype=e['prototype'], modelId=e['modelId']) for e in candidates],
                           changedExistingNeighbors=changed_neighbors,
                           rejectedInheritedCandidates=rejected_candidates,
                           sourceVisibilityRemovals=[hidden[uid] for uid in sorted(removed)],
                           slabOpenings=current['diagnostics']['slabOpenings']))
        print(f'{name}: {len(target)} checked placements, {len(changed_neighbors)} other existing neighbors adjusted.', flush=True)
    (OUT / f'{prefix}-scenes.json').write_text(json.dumps(manifest, indent=2) + '\n')
    report = dict(previousModels=len(old_ids), modelCount=len(models), newModelIds=sorted(new_ids),
                  targetModelIds=sorted(checked_ids), baseline=str(baseline),
                  nativeVectorSerializationChecked=True,
                  scenes=checks, contextExports=exports,
                  libraryExports=[dict(file=str((bm.OUTPUT / (uid + '.glb')).relative_to(ROOT)).replace('\\', '/'))
                                  for uid in sorted(checked_ids)],
                  validation='Source transforms checked; listed GLBs require external validation.',
                  limitations=['Saved default appearances, not live gameplay acceptance.',
                               'Contexts omit unmapped and unapproved inherited candidates.',
                               'Conventional GLB contexts export floors; native ceiling admission is checked separately.'])
    (OUT / f'{prefix}-export-audit.json').write_text(json.dumps(report, indent=2) + '\n')
    print(f'Batch scenes ready: {len(new_ids)} new models, {len(exports)} shared context exports.', flush=True)


if __name__ == '__main__':
    main()
