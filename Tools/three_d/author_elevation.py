"""CMU14: bake reviewable Garrison landing regions from stair and retaining-edge topology.

The inferred short-flight rise is .39 tile, matching the existing metal platform cap.
Only connected, cardinal landings are fitted. Multi-Z shafts and contradictory links
are listed in the audit instead of flooding an arbitrary side of the map.
"""
from __future__ import annotations

import argparse
from collections import Counter, defaultdict
import json
import math
from pathlib import Path

import build_models as bm
import inventory
import scene as scene_tools
import yaml

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'Content.CMU/Resources/Prototypes/CMU14/ThreeD/Elevation'
REVIEW = ROOT / 'Tools/three_d/generated/elevation-review'
RISE = .39
CARDINALS = ((0, 1), (0, -1), (1, 0), (-1, 0))


def cell(position):
    return tuple(math.floor(v) for v in position[:2])


def turn(yaw, x, y):
    return round(x*math.cos(yaw)-y*math.sin(yaw)), round(x*math.sin(yaw)+y*math.cos(yaw))


def rectangles(heights):
    """Coalesce identical row runs; rectangles remain disjoint and preserve every tile."""
    rows = defaultdict(list)
    for (x, y), height in heights.items():
        if height:
            rows[y].append((x, round(height, 6)))
    active = {}
    result = []
    previous = None
    for y, values in sorted(rows.items()):
        runs = []
        for x, h in sorted(values):
            if runs and runs[-1][1] == x and runs[-1][2] == h:
                runs[-1] = (runs[-1][0], x+1, h)
            else:
                runs.append((x, x+1, h))
        following = {}
        for run in runs:
            x0, x1, h = run
            rect = active.pop(run, None) if previous == y-1 else None
            if rect:
                rect['bounds'][3] = y+1
            else:
                rect = {'bounds': [x0, y, x1, y+1], 'height': h}
            following[run] = rect
        result.extend(active.values())
        active = following
        previous = y
    result.extend(active.values())
    return [{**r, 'bounds': ', '.join(map(str, r['bounds']))} for r in result]


def infer_profile(data, models):
    if any(abs(t.get('yaw', 0)) > 1e-6 for t in data['tiles']) or len({t['grid'] for t in data['tiles']}) != 1:
        raise ValueError('Authoring requires one cardinal grid; runtime supports transformed profiles')
    tiles = {(round(t['x']), round(t['y'])) for t in data['tiles']}
    objects = defaultdict(list)
    stairs, blocked, edges = [], set(), set()
    edge = lambda a, b: tuple(sorted((a, b)))
    for entity in data['instances']:
        p = cell(entity['position'])
        objects[p].append(entity)
        name = entity['prototype']
        model = models.get(entity.get('modelId'), {})
        yaw = entity.get('renderYaw', entity['yaw'])
        if 'stairs' in name.lower():
            blocked.add(p)
            if 'MultiZ' not in name and model.get('parts'):
                stairs.append(entity)
        elif (any(s in name.lower() for s in ('wall', 'window', 'door', 'airlock', 'railing', 'fence')) and
              not model.get('wallMounted') and not any(s in name.lower() for s in
                  ('walllight', 'poster', 'decal', 'button', 'wallmount', 'wallcabinet', 'wallflag'))):
            blocked.add(p)
            c, s = math.cos(yaw), math.sin(yaw)
            for part in model.get('parts', []):
                low, high = bm.part_bounds(part)
                if high[2] < .6:
                    continue
                points = [(entity['position'][0]+x*c-y*s, entity['position'][1]+x*s+y*c)
                          for x in (low[0], high[0]) for y in (low[1], high[1])]
                for x in range(math.floor(min(v[0] for v in points)+.08), math.ceil(max(v[0] for v in points)-.08)):
                    for y in range(math.floor(min(v[1] for v in points)+.08), math.ceil(max(v[1] for v in points)-.08)):
                        blocked.add((x, y))
        if 'Platform' in name:
            directions = [] if 'CornerSmall' in name else [(0, -1)]
            # The round end cap is U-shaped: its side returns close the channel
            # as well as its south crossmember. Ignoring them joins both landings.
            if 'Round' in name:
                directions.extend([(-1, 0), (1, 0)])
            if 'Corner' in name and 'CornerSmall' not in name:
                directions.append((1, 0))
            for direction in directions:
                dx, dy = turn(yaw, *direction)
                edges.add(edge(p, (p[0]+dx, p[1]+dy)))

    regions, groups = {}, []
    # The warehouse loading deck has open conveyor/central discharge gaps in
    # its south fascia. Those gaps do not lower the deck to the surrounding aisle.
    if data['map']['level'] == 0:
        for y in range(-79, -52):
            edges.add(edge((210, y), (211, y)))
            edges.add(edge((225, y), (226, y)))
        for x in range(211, 226):
            edges.add(edge((x, -80), (x, -79)))
    for tile in sorted(tiles):
        if tile in regions or tile in blocked:
            continue
        rid = len(groups)
        queue = [tile]
        regions[tile] = rid
        group = []
        while queue:
            a = queue.pop()
            group.append(a)
            for dx, dy in CARDINALS:
                b = a[0]+dx, a[1]+dy
                if b in tiles and b not in regions and b not in blocked and edge(a, b) not in edges:
                    regions[b] = rid
                    queue.append(b)
        groups.append(group)

    def landing(p, direction, sign):
        chain = []
        for distance in range(1, 8):
            target = p[0]+direction[0]*sign*distance, p[1]+direction[1]*sign*distance
            peers = objects.get(target, [])
            if any('MultiZStairs' in e['prototype'] for e in peers):
                return None, target, chain, 'multi-Z transition'
            if target in regions:
                return regions[target], target, chain, None
            if any('stairs' in e['prototype'].lower() for e in peers):
                chain.append(target)
            elif not any(any(n in e['prototype'].lower() for n in ('door', 'airlock')) for e in peers):
                return None, target, chain, 'blocked landing'
        return None, target, chain, 'landing beyond seven tiles'

    constraints = Counter()
    links = []
    for e in stairs:
        p = cell(e['position'])
        direction = turn(e.get('renderYaw', e['yaw']), 0, 1)
        lo, lp, before, le = landing(p, direction, -1)
        hi, hp, after, he = landing(p, direction, 1)
        link = {'id': e['id'], 'prototype': e['prototype'], 'tile': p, 'direction': direction,
                'low': lo, 'high': hi, 'before': before, 'after': after,
                'issue': ('multi-Z transition' if any('MultiZStairs' in other['prototype'] for other in objects[p])
                          else le or he or ('same region at both ends' if lo == hi else None))}
        links.append(link)
        if link['issue'] is None:
            constraints[lo, hi, 1] += 100
    for e in data['instances']:
        name = e['prototype']
        if 'Platform' not in name or any(s in name for s in ('CornerSmall', 'Stair', 'Round')):
            continue
        p = cell(e['position'])
        for direction in [(0, -1)]+([(1, 0)] if 'Corner' in name else []):
            dx, dy = turn(e.get('renderYaw', e['yaw']), *direction)
            outside, inside = regions.get(p), regions.get((p[0]+dx, p[1]+dy))
            # The retaining fascia sits on the lower tile, touching the raised tile's edge.
            if outside is not None and inside is not None and outside != inside:
                constraints[outside, inside, 1] += 1
    doors = []
    for e in data['instances']:
        if not any(s in e['prototype'].lower() for s in ('airlock', 'doubledoor', 'windoor')):
            continue
        p = cell(e['position'])
        direction = turn(e.get('renderYaw', e['yaw']), 0, 1)
        lo, _, before, le = landing(p, direction, -1)
        hi, _, after, he = landing(p, direction, 1)
        if not le and not he and not before and not after and lo != hi:
            # A door propagates an established room height, but cannot overrule a stair.
            constraints[min(lo, hi), max(lo, hi), 0] += .01
            doors.append((p, direction, lo, hi, e['id']))

    parents = list(range(len(groups)))
    offsets = [0]*len(groups)

    def find(i):
        if parents[i] != i:
            root, offset = find(parents[i])
            offsets[i] += offset
            parents[i] = root
        return parents[i], offsets[i]

    conflicts = []
    for (lo, hi, height), weight in sorted(constraints.items(), key=lambda v: (-v[1], v[0])):
        a, da = find(lo)
        b, db = find(hi)
        if a == b:
            if db-da != height:
                conflicts.append({'lowRegion': lo, 'highRegion': hi, 'requestedRise': height*RISE,
                                  'resolvedRise': (db-da)*RISE, 'votes': weight})
        else:
            parents[b] = a
            offsets[b] = da+height-db
    anchors = {}
    for i, group in enumerate(groups):
        root, offset = find(i)
        if root not in anchors or len(group) > anchors[root][0]:
            anchors[root] = len(group), offset
    heights = {i: round((find(i)[1]-anchors[find(i)[0]][1])*RISE, 6) for i in range(len(groups))}
    floor = {p: heights[rid] for p, rid in regions.items()}
    ramps = []
    for link in links:
        if link['issue']:
            continue
        lo, hi = heights[link['low']], heights[link['high']]
        if hi <= lo:
            link['issue'] = 'ascent contradicts connected landings'
            continue
        count = len(link['before'])+len(link['after'])+1
        rise = (hi-lo)/count
        bottom = lo+len(link['before'])*rise
        ramp = {'tile': ', '.join(map(str, link['tile'])),
                      'direction': ', '.join(map(str, link['direction'])),
                      'bottom': round(bottom, 6), 'top': round(bottom+rise, 6),
                      'sourcePrototype': link['prototype']}
        previous = next((r for r in ramps if r['tile'] == ramp['tile']), None)
        if previous is not None:
            link['issue'] = 'duplicate saved stair' if previous == ramp else 'overlapping stair flights'
            continue
        ramps.append(ramp)
        floor[link['tile']] = round(bottom, 6)
    thresholds = []
    for p, direction, lo, hi, uid in doors:
        low, high = heights[lo], heights[hi]
        if low == high:
            continue
        dx, dy = direction if high > low else (-direction[0], -direction[1])
        q = p[0]-dx, p[1]-dy
        key = ', '.join(map(str, q))
        if q not in regions or any(r['tile'] == key for r in ramps):
            continue
        ramps.append({'tile': key, 'direction': f'{dx}, {dy}', 'bottom': min(low, high),
                      'top': max(low, high), 'sourcePrototype': ''})
        floor[q] = min(low, high)
        thresholds.append({'door': uid, 'tile': q, 'bottom': min(low, high), 'top': max(low, high)})
    # Carry the highest adjoining landing through walls/door footprints. Do not let
    # inferred structural heights spread through open terrain or across stair cells.
    stair_cells = {cell(e['position']) for e in stairs}
    for _ in range(2):
        updates = {}
        for p in blocked - stair_cells:
            if p in floor:
                continue
            adjoining = [floor[q] for dx, dy in CARDINALS if (q := (p[0]+dx, p[1]+dy)) in floor]
            if adjoining:
                updates[p] = max(adjoining)
        floor.update(updates)
    # An unresolved decorative/transition flight must not punch a flat hole into
    # an already raised floor. Its existing shape stays on the adjoining ground.
    for p in stair_cells:
        if p not in floor:
            floor[p] = max((floor.get((p[0]+dx, p[1]+dy), 0) for dx, dy in CARDINALS), default=0)
    name = 'CMU3DStableGarrisonRedux' + str(data['map']['level']).replace('-', 'Minus')
    profile = {'type': 'cmu3DElevation', 'id': name, 'regions': rectangles(floor), 'ramps': ramps}
    report = {'level': data['map']['level'], 'regions': len(groups), 'elevatedTiles': sum(h != 0 for h in floor.values()),
              'heightCounts': dict(Counter(floor.values())),
              'fittedStairTiles': sum(bool(r['sourcePrototype']) for r in ramps), 'thresholdRamps': thresholds,
              'stairs': links, 'conflictingLinks': conflicts, 'inferredRise': RISE,
              'scope': 'Presentation inference from cardinal saved stairs and platform enclosures; multi-Z transitions remain separate.'}
    return profile, report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--levels', nargs='+', type=int, default=[-2, -1, 0, 1, 2, 3, 4])
    args = parser.parse_args()
    REVIEW.mkdir(parents=True, exist_ok=True)
    OUTPUT.mkdir(parents=True, exist_ok=True)
    models = bm.load_models()
    library = {m['id']: m for m in models}
    catalog = json.loads((ROOT/'Tools/three_d/generated/inventory.json').read_text(encoding='utf-8'))
    kinds, issues, _ = inventory.load_prototypes(ROOT)
    from solution_glass_states import configure_source
    configure_source(kinds)
    resolver = inventory.Resolver(kinds['entity'])
    for level in args.levels:
        path, definition = scene_tools.configured_map(ROOT, 'redux', level)
        header, records = scene_tools.read_map(path)
        defaults, errors = {}, []
        for name in sorted({r['prototype'] for r in records.values()} - {''}):
            try:
                defaults[name] = inventory.component_map(resolver.resolve(name))
            except inventory.ResolutionError as error:
                errors.append({'prototype': name, 'error': str(error)})
        # Re-author from the original two-dimensional evidence, not our last baked heights.
        for record in records.values():
            record['components'].pop('CMU3DElevation', None)
        data = scene_tools.build_scene(header, records, catalog, models, defaults, level=level)
        profile, report = infer_profile(data, library)
        (OUTPUT/f'redux_{level}.yml').write_text(
            '# Inferred presentation heights; regenerate with Tools/three_d/author_elevation.py.\n'+
            yaml.safe_dump([profile], sort_keys=False, width=120), encoding='utf-8', newline='\n')
        grid = next(uid for uid, r in records.items() if 'MapGrid' in r['components'])
        text = path.read_text(encoding='utf-8')
        marker = '    - type: MapGrid\n'
        component = '    - type: CMU3DElevation\n      profile: '+profile['id']+'\n'
        if component not in text:
            if text.count(marker) != 1:
                raise ValueError('Expected a single map grid')
            path.write_text(text.replace(marker, component+marker, 1), encoding='utf-8', newline='\n')
        records[grid]['components']['CMU3DElevation'] = {'profile': profile['id']}
        from elevation import apply_scene
        stats = apply_scene(data['instances'], data['tiles'], records, scene_tools.WorldTransforms(records, defaults),
                            library, data['geometryVariants'], {profile['id']: profile})
        data['diagnostics']['elevation'] = stats
        data['diagnostics']['prototypeResolutionErrors'] = errors
        data['diagnostics']['prototypeLoadIssues'] = issues
        data['map'].update({'path': path.relative_to(ROOT).as_posix(), 'variant': 'redux',
                            'name': definition.get('mapName', 'Garrison Redux')})
        scene_tools.enrich_materials(data, ROOT)
        (REVIEW/f'redux-{level}.json').write_text(json.dumps(data, separators=(',', ':'))+'\n', encoding='utf-8', newline='\n')
        (REVIEW/f'audit-{level}.json').write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8', newline='\n')
        print(json.dumps({'level': level, **stats, 'unresolvedStairs': sum(bool(s['issue']) for s in report['stairs']),
                          'conflictingLinks': len(report['conflictingLinks'])}), flush=True)


if __name__ == '__main__':
    main()
