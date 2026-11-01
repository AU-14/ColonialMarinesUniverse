"""Audit CMU starting equipment, optional loadouts and recursively filled items.

This is a conservative catalogue: optional and hidden roles are included, not a
claim that every item appears in each round. No gameplay prototypes are changed.
"""
from __future__ import annotations

import argparse
from collections import Counter, defaultdict, deque
import json
from pathlib import Path

import inventory as inv

ROOT = Path(__file__).resolve().parents[2]


def owned(row):
    return row.get('_source', '').startswith('Content.CMU/')


def strings(value):
    if isinstance(value, str):
        yield value
    elif isinstance(value, dict):
        for child in value.values():
            yield from strings(child)
    elif isinstance(value, list):
        for child in value:
            yield from strings(child)


def catalogue(kinds):
    entities = kinds['entity']
    resolver = inv.Resolver(entities)
    origins = defaultdict(set)
    slots = defaultdict(set)
    errors = []
    gears = kinds['startingGear']
    loadouts = kinds['loadout']
    selected_gears = {uid for uid, row in gears.items() if owned(row)}
    selected_loadouts = {uid for uid, row in loadouts.items() if owned(row)}
    roles = {uid for uid, row in kinds['roleLoadout'].items() if owned(row)}
    jobs = inv.Resolver(kinds['job'])
    for uid, row in kinds['job'].items():
        if not owned(row):
            continue
        job = jobs.resolve(uid)
        for field in ('startingGear', 'dummyStartingGear'):
            if job.get(field):
                selected_gears.add(job[field])
        if 'Job' + uid in kinds['roleLoadout']:
            roles.add('Job' + uid)
    # Spawn equipment can also be selected by ghost roles and mob Loadout data.
    for row in entities.values():
        if not owned(row):
            continue
        for value in strings(inv.component_map(resolver.resolve(row['id'])).get('Loadout', {})):
            if value in gears:
                selected_gears.add(value)
    for uid in sorted(roles):
        for group in kinds['roleLoadout'][uid].get('groups', []):
            for loadout in kinds['loadoutGroup'][group].get('loadouts', []):
                selected_loadouts.add(loadout)
    for uid in selected_loadouts:
        row = loadouts[uid]
        if row.get('startingGear'):
            selected_gears.add(row['startingGear'])
    # AlwaysPushInheritance merges startingGear collections. For an art catalogue
    # include every possible parent's item, including slots overridden by children.
    queue = deque(selected_gears)
    while queue:
        uid = queue.popleft()
        for parent in inv.parent_ids(gears[uid]):
            if parent not in selected_gears:
                selected_gears.add(parent)
                queue.append(parent)
    for kind, ids in (('startingGear', selected_gears), ('loadout', selected_loadouts)):
        for uid in sorted(ids):
            row = kinds[kind][uid]
            for slot, item in (row.get('equipment') or {}).items():
                origins[item].add(kind + ':' + uid)
                slots[item].add(slot)
            for item in row.get('inhand') or []:
                origins[item].add(kind + ':' + uid)
                slots[item].add('hand')
            for slot, items in (row.get('storage') or {}).items():
                for item in items:
                    origins[item].add(kind + ':' + uid + '/storage:' + slot)
    direct = set(origins)
    queue = deque(sorted(direct))
    seen = set()
    while queue:
        uid = queue.popleft()
        if uid in seen:
            continue
        seen.add(uid)
        if uid not in entities:
            errors.append({'prototype': uid, 'error': 'Missing equipment prototype'})
            continue
        components = inv.component_map(resolver.resolve(uid))
        children = set()
        for name in ('StorageFill', 'ContainerFill', 'EntityTableContainerFill',
                     'RandomSpawner', 'UniqueRandomSpawner', 'RMCRandomSpawner'):
            children.update(value for value in strings(components.get(name, {})) if value in entities)
        for slot in components.get('AttachableHolder', {}).get('slots', {}).values():
            if slot.get('startingAttachable'):
                children.add(slot['startingAttachable'])
            children.update(slot.get('random') or [])
        def tables(value, visited=()):
            if isinstance(value, dict):
                table = value.get('tableId')
                if table and table not in visited:
                    yield from tables(kinds['entityTable'][table]['table'], (*visited, table))
                if value.get('id') in entities:
                    yield value['id']
                for child in value.values():
                    yield from tables(child, visited)
            elif isinstance(value, list):
                for child in value:
                    yield from tables(child, visited)
        children.update(tables(components.get('EntityTableContainerFill', {})))
        for name in ('CMItemSlots', 'ItemSlots'):
            def starting_items(value):
                if isinstance(value, dict):
                    for key, child in value.items():
                        if key == 'startingItem' and child:
                            yield child
                        else:
                            yield from starting_items(child)
            children.update(starting_items(components.get(name, {})))
        for name in ('BallisticAmmoProvider', 'RevolverAmmoProvider'):
            component = components.get(name, {})
            if component.get('proto') and component.get('capacity', 1) > 0:
                children.add(component['proto'])
        for child in children:
            origins[child].add('contents:' + uid)
            if child not in seen:
                queue.append(child)
    models = kinds['cmu3DModel']
    attachments = defaultdict(lambda: defaultdict(list))
    for pose in kinds.get('cmu3DEquipmentPose', {}).values():
        for uid in pose.get('sourcePrototypes', []):
            attachments[uid][pose['slot']].append(pose['id'])
    exact = defaultdict(list)
    for model in models.values():
        for uid in model.get('sourcePrototypes', []):
            exact[uid].append(model['id'])
    rows = []
    for uid in sorted(seen):
        if uid not in entities:
            continue
        resolved = resolver.resolve(uid)
        components = inv.component_map(resolved)
        sprite = components.get('Sprite')
        ancestors = resolved['_ancestors']
        candidates = sorted({mid for parent in ancestors for mid in exact[parent]})
        compatible = []
        for mid in candidates:
            model = models[mid]
            for parent in model.get('sourcePrototypes', []):
                if parent in ancestors and sprite == inv.component_map(resolver.resolve(parent)).get('Sprite'):
                    compatible.append(mid)
                    break
        rows.append({'prototype': uid, 'source': resolved['_source'], 'slots': sorted(slots[uid]),
                     'origins': sorted(origins[uid]), 'direct': uid in direct,
                     'models': sorted(exact[uid]), 'inheritedCandidates': candidates,
                     'equipmentPoses': dict(attachments[uid]),
                     'identicalSpriteCandidates': compatible, 'sprite': sprite,
                     'clothing': components.get('Clothing'), 'item': components.get('Item'),
                     'rigidEquipment': sorted(set(components) & {'Gun', 'MeleeWeapon', 'Tool', 'FireExtinguisher',
                         'Clipboard', 'ForensicScanner', 'Instrument', 'Stunbaton', 'WeaponWand'}),
                     'visualComponents': {name: value for name, value in components.items()
                                          if 'Visual' in name or name in ('Appearance', 'ItemToggle', 'Foldable', 'RandomSprite')},
                     'status': 'model' if exact[uid] else 'no-sprite' if not sprite else 'missing-model'})
    return {'scope': 'All CMU startingGear/loadout definitions, CMU job parents and role loadout choices, recursively filled equipment. Includes optional and hidden roles; not round spawn frequencies.',
            'counts': {'startingGear': len(selected_gears), 'loadouts': len(selected_loadouts),
                       'roleLoadouts': len(roles), 'directItems': len(direct), 'allItems': len(rows),
                       'withHeldPose': sum('hand' in r['equipmentPoses'] for r in rows),
                       'withWornPose': sum(any(s != 'hand' for s in r['equipmentPoses']) for r in rows),
                       **dict(Counter(r['status'] for r in rows))},
            'errors': errors, 'equipment': rows}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=ROOT / 'Tools/three_d/generated/equipment-inventory.json')
    parser.add_argument('--cache', type=Path, help='Optional previously read prototype catalogue for local iteration')
    args = parser.parse_args()
    if args.cache:
        kinds = json.loads(args.cache.read_text(encoding='utf-8'))
    else:
        kinds, issues, _ = inv.load_prototypes(ROOT)
        if issues:
            raise ValueError(issues)
    result = catalogue(kinds)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(result['counts']))
    print('Missing references:', len(result['errors']))


if __name__ == '__main__':
    main()
