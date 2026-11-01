"""Fit decorative approaches to real multi-Z stairs and remove duplicate 2D projections.

Run after author_elevation.py, before exporting the complete scene. The physics
curve remains authoritative; only presentation profiles are authored here.
"""
from collections import defaultdict
from copy import deepcopy
import json
import math

import yaml
from author_elevation import ROOT, REVIEW, OUTPUT, cell, rectangles
from elevation import Field
from multiz import STORY_HEIGHT


def fit_approaches(scenes, profiles):
    fields = {level: Field(profile) for level, profile in profiles.items()}
    objects = {}
    tiles = {}
    for level, scene in scenes.items():
        objects[level] = defaultdict(list)
        for entity in scene['instances']:
            objects[level][cell(entity['position'])].append(entity)
        tiles[level] = {cell((t['x'], t['y'])) for t in scene['tiles']}
    report = []
    for level, scene in scenes.items():
        if level + 1 not in scenes:
            continue
        fitted = set()
        for entity in scene['instances']:
            source = entity.get('zStair', {})
            curve = source.get('heightCurve', [])
            if len(curve) < 2 or max(curve)-min(curve) <= .01 or source.get('corner'):
                continue
            p = cell(entity['position'])
            if p in fitted:
                continue
            fitted.add(p)
            direction = round(entity['yaw']/(math.pi/2)) % 4
            # The source default curve descends as t increases.
            dx, dy = ((0, 1), (-1, 0), (0, -1), (1, 0))[direction]
            if curve[-1] > curve[0]:
                dx, dy = -dx, -dy
            low_phase, high_phase = min(curve[0], curve[-1]), max(curve[0], curve[-1])
            if not 0 <= low_phase < 1 < high_phase < 2:
                continue

            def ordinary(at_level, q):
                return [e for e in objects[at_level].get(q, []) if
                        'stairs' in e['prototype'].lower() and 'MultiZ' not in e['prototype'] and e.get('modelId') and
                        abs(-math.sin(e.get('yaw', 0))*dx + math.cos(e.get('yaw', 0))*dy) > .9]

            def chain(at_level, sign):
                result = []
                for distance in range(1, 8):
                    q = p[0]+dx*distance*sign, p[1]+dy*distance*sign
                    peers = ordinary(at_level, q)
                    if not peers:
                        break
                    result.append((q, peers))
                return result

            before, after = chain(level, -1), chain(level+1, 1)
            # Fit the entire drawn flight, including the decorative approach and
            # exit. Physics changes maps inside the middle cell; presentation keeps
            # one continuous ascent across both copies of that cell.
            run = [(q, peers, level) for q, peers in reversed(before)] + [(p, [entity], level)] + [
                (q, peers, level+1) for q, peers in after]
            first = (run[0][0][0]-dx, run[0][0][1]-dy)
            last = (run[-1][0][0]+dx, run[-1][0][1]+dy)
            start = fields[level].floor(first)
            end = STORY_HEIGHT + fields[level+1].floor(last)
            if end <= start:
                continue
            ascending_curve = curve if curve[-1] > curve[0] else list(reversed(curve))
            for index, (q, peers, owner) in enumerate(run):
                for at_level in (level, level+1):
                    offset = (at_level-level)*STORY_HEIGHT
                    bottom = start+(end-start)*index/len(run)-offset
                    top = start+(end-start)*(index+1)/len(run)-offset
                    ramp = {'tile': ', '.join(map(str, q)), 'direction': (dx, dy),
                            'bottom': round(bottom, 6), 'top': round(top, 6),
                            'sourcePrototype': peers[0]['prototype'], 'geometry': at_level == owner,
                            'physicsCurve': ascending_curve if q == p else [0, 0],
                            'physicsOffset': (level if q == p else owner)-at_level}
                    fields[at_level].ramps[q] = ramp
                    if at_level == owner:
                        fields[at_level].heights[q] = ramp['bottom']
                    if at_level == level+1 and owner == level and q in tiles[at_level]:
                        openings = profiles[at_level].setdefault('stairOpenings', [])
                        tile_string = ', '.join(map(str, q))
                        if tile_string not in openings:
                            openings.append(tile_string)
            hidden = []
            for q, peers in after:
                for projected in ordinary(level, q):
                    if any(e['prototype'] == projected['prototype'] for e in peers):
                        targets = profiles[level].setdefault('suppressedStairs', {}).setdefault(projected['prototype'], [])
                        tile_string = ', '.join(map(str, q))
                        if tile_string not in targets:
                            targets.append(tile_string)
                        hidden.append(projected['id'])
            if p in tiles[level+1]:
                openings = profiles[level+1].setdefault('stairOpenings', [])
                tile_string = ', '.join(map(str, p))
                if tile_string not in openings:
                    openings.append(tile_string)
            report.append({'level': level, 'stair': entity['id'], 'tile': p, 'approachTiles': len(before),
                           'landingTiles': len(after), 'suppressedProjections': hidden,
                           'footHeight': start, 'upperExitHeight': end-STORY_HEIGHT, 'flightTiles': len(run)})
    for level, field in fields.items():
        profiles[level]['regions'] = rectangles(field.heights)
        profiles[level]['ramps'] = [{**ramp, 'direction': ', '.join(str(int(v)) for v in ramp['direction'])}
                                    for ramp in field.ramps.values()]
    return report


def main():
    scenes = {level: json.loads((REVIEW/f'redux-{level}.json').read_text(encoding='utf-8')) for level in range(-2, 5)}
    profiles = {level: yaml.safe_load((OUTPUT/f'redux_{level}.yml').read_text(encoding='utf-8'))[0] for level in scenes}
    originals = deepcopy(profiles)
    report = fit_approaches(scenes, profiles)
    for level, profile in profiles.items():
        if profile == originals[level]:
            continue
        (OUTPUT/f'redux_{level}.yml').write_text(
            '# Generate with author_elevation.py followed by author_multiz_elevation.py.\n'+
            yaml.safe_dump([profile], sort_keys=False, width=120), encoding='utf-8', newline='\n')
    target = ROOT/'Tools/three_d/art-batch-9/multiz-stair-audit.json'
    target.write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8', newline='\n')
    print(f'Fitted {len(report)} multi-Z assemblies; {sum(len(r["suppressedProjections"]) for r in report)} duplicate projections removed.')


if __name__ == '__main__':
    main()
