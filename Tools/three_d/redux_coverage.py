"""CMU14: inventory Redux's physical placements and round-setup spawn choices.

This deliberately follows marker/spawner outputs, not arbitrary item inventories.
Hidden subfloor objects still need models when their existing owner reveals them.
"""
from collections import defaultdict
import argparse
import json
from pathlib import Path

import inventory

ROOT = Path(__file__).resolve().parents[2]


def collect(kinds, saved_inventory):
    resolver = inventory.Resolver(kinds['entity'])
    targets, unresolved, marker_classes = defaultdict(set), {}, set()
    placed = {p['id']: p for p in saved_inventory['prototypes'] if p['instanceCounts'].get('redux')}

    def visit(uid, origin, path=()):
        # This engine-owned, sprite-less entity stores map chunks; it is not a prop.
        if uid == 'ChunkEntity':
            return
        if uid in path:
            unresolved[uid] = 'Spawner cycle: ' + ' -> '.join((*path, uid))
            return
        try:
            entity = resolver.resolve(uid)
        except inventory.ResolutionError as error:
            unresolved[uid] = str(error)
            return
        components = inventory.component_map(entity)
        if 'VendorMarker' in components:
            marker_classes.add(components['VendorMarker'].get('class'))
        spawner = False
        for kind in ('RandomSpawner', 'ConditionalSpawner'):
            if kind not in components:
                continue
            spawner = True
            for field in ('prototypes', 'rarePrototypes'):
                for output in components[kind].get(field, []):
                    visit(output, 'map ' + kind, (*path, uid))
        if (spawner or 'Sprite' not in components or
                any(c in components for c in ('MobState', 'Action', 'Marker', 'Area', 'VendorMarker'))):
            return
        targets[uid].add(origin)

    for uid, entry in placed.items():
        visit(uid, 'saved ' + entry['classification'])

    def vendor_set(uid, path=()):
        if uid in path:
            raise ValueError('Vendor inheritance cycle: ' + uid)
        entry = kinds['platoonVendorSet'][uid]
        values = {}
        # The source field uses AlwaysPushInheritance; a child overrides each key.
        for parent in reversed(inventory.parent_ids(entry)):
            values.update(vendor_set(parent, (*path, uid)))
        values.update(entry.get('vendors', {}))
        return values

    for platoon in kinds.get('platoon', {}).values():
        if platoon.get('abstract'):
            continue
        vendors = vendor_set(platoon['vendorSet']) if platoon.get('vendorSet') else {}
        vendors.update(platoon.get('VendorToMarker', {}))
        vendors.update(platoon.get('vendorOverrides', {}))
        for role, uid in vendors.items():
            if role in marker_classes:
                visit(uid, 'platoon vendor ' + role)
        for uid in platoon.get('vehicleSupplyCatalog', []):
            visit(uid, 'platoon vehicle supply')
    for uid in ('CMUHospitalEmergencyComputerColony', 'CMUHospitalEmergencyComputerGovfor',
                'CMUHospitalEmergencyComputerOpfor', 'CMUResearchDataTerminalGovfor', 'CMUResearchDataTerminalOpfor'):
        if uid in kinds['entity']:
            visit(uid, 'faction terminal')

    exact, conditional = defaultdict(list), defaultdict(list)
    for model in kinds.get('cmu3DModel', {}).values():
        for uid in model.get('sourcePrototypes', []):
            exact[uid].append(model['id'])
        for uid in model.get('randomSpritePrototypes', []):
            conditional[uid].append(model['id'])
    rows = []
    for uid, origins in sorted(targets.items()):
        entity = resolver.resolve(uid)
        components = inventory.component_map(entity)
        native = 'multi-z stair' if uid.startswith('CMUMultiZStairs') else None
        # These are effects / void markers, not a solid prop to invent.
        if 'CMUFogWall' in uid:
            native = 'atmospheric sprite'
        if uid == 'FloorChasmEntity':
            native = 'void floor'
        rows.append(dict(id=uid, origins=sorted(origins), models=exact[uid],
                         conditional=conditional[uid], native=native,
                         inherited=[m for ancestor in entity['_ancestors'] for m in exact[ancestor]],
                         source=entity['_source'], sprite=components['Sprite'], components=components,
                         reduxSaved=placed.get(uid, {}).get('instanceCounts', {}).get('redux', 0)))
    return dict(targets=rows, unresolved=unresolved, markerClasses=sorted(marker_classes))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--inventory', type=Path, default=ROOT / 'Tools/three_d/generated/inventory.json')
    parser.add_argument('--output', type=Path, default=ROOT / 'Tools/three_d/generated/redux-coverage.json')
    args = parser.parse_args()
    kinds, issues, _ = inventory.load_prototypes(ROOT)
    if issues:
        raise ValueError(issues)
    report = collect(kinds, json.loads(args.inventory.read_text(encoding='utf-8')))
    rows = report['targets']
    missing = [r for r in rows if not (r['models'] or r['conditional'] or r['native'])]
    report['summary'] = dict(physicalTypes=len(rows), exact=sum(bool(r['models']) for r in rows),
                             conditional=sum(bool(r['conditional']) for r in rows),
                             native=sum(bool(r['native']) for r in rows), missing=len(missing))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(report['summary']))
    for row in missing:
        print(row['id'], ', '.join(row['origins']))


if __name__ == '__main__':
    main()
