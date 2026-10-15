"""CMU14: apply authored grid-local heights without changing saved or logical-Z coordinates."""
from __future__ import annotations

import math
from pathlib import Path

from inventory import load_yaml

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'Content.CMU/Resources/Prototypes/CMU14/ThreeD/Elevation'


def vector(value):
    return tuple(float(v) for v in (value.split(',') if isinstance(value, str) else value))


class Field:
    def __init__(self, profile):
        self.heights = {}
        self.ramps = {}
        self.suppressed = {(tuple(map(int, vector(tile))), prototype) for prototype, tiles in profile.get('suppressedStairs', {}).items() for tile in tiles}
        self.openings = {tuple(map(int, vector(tile))) for tile in profile.get('stairOpenings', [])}
        for region in profile.get('regions', []):
            x0, y0, x1, y1 = vector(region['bounds'])
            h = float(region['height'])
            if (not all(math.isfinite(v) for v in (x0, y0, x1, y1, h)) or
                    any(v != int(v) for v in (x0, y0, x1, y1)) or abs(h) > 16 or
                    x1 <= x0 or y1 <= y0 or (x1-x0)*(y1-y0) > 100000):
                raise ValueError('Elevation requires bounded, integer tile rectangles and finite heights')
            for y in range(int(y0), int(y1)):
                for x in range(int(x0), int(x1)):
                    if (x, y) in self.heights:
                        raise ValueError('Overlapping elevation rectangles')
                    self.heights[x, y] = h
        for ramp in profile.get('ramps', []):
            tile = tuple(int(v) for v in vector(ramp['tile']))
            direction = vector(ramp['direction'])
            if (tile in self.ramps or direction not in ((1, 0), (-1, 0), (0, 1), (0, -1)) or
                    not all(math.isfinite(ramp[k]) for k in ('bottom', 'top')) or
                    not 0 < ramp['top']-ramp['bottom'] <= 4):
                raise ValueError('Elevation ramps require unique tiles and a finite cardinal rise')
            self.ramps[tile] = {**ramp, 'direction': direction}

    def floor(self, tile):
        return self.heights.get(tuple(tile), 0)

    def height(self, x, y):
        tile = math.floor(x), math.floor(y)
        ramp = self.ramps.get(tile)
        if not ramp:
            return self.floor(tile)
        dx, dy = ramp['direction']
        t = min(1, max(0, (x-tile[0]-.5)*dx + (y-tile[1]-.5)*dy + .5))
        return ramp['bottom'] + t * (ramp['top']-ramp['bottom'])


def load_profiles():
    return {p['id']: p for path in sorted(SOURCE.glob('*.yml'))
            for p in load_yaml(path.read_text(encoding='utf-8')) or [] if p.get('type') == 'cmu3DElevation'}


def apply_scene(instances, tiles, records, transforms, library, variants, profiles=None):
    profiles = load_profiles() if profiles is None else profiles
    fields = {}
    for uid, record in records.items():
        if 'CMU3DElevation' not in record['components']:
            continue
        name = record['components']['CMU3DElevation']['profile']
        fields[uid] = Field(profiles[name])
    if not fields:
        return {}

    def local(grid, x, y):
        gx, gy, yaw, _ = transforms.resolve(grid)
        c, s = math.cos(yaw), math.sin(yaw)
        return c*(x-gx)+s*(y-gy), -s*(x-gx)+c*(y-gy)

    raised_tiles = raised_entities = fitted_stairs = 0
    for tile in tiles:
        field = fields.get(tile['grid'])
        if field is None:
            continue
        x, y = local(tile['grid'], tile['x'], tile['y'])
        cell = round(x), round(y)
        height = field.floor(cell)
        if cell in field.openings:
            tile['floorFragments'] = []
        tile['elevation'] = height
        if (ramp := field.ramps.get(cell)) and not ramp.get('sourcePrototype'):
            tile['elevationRamp'] = ramp
        neighbors = [field.floor((cell[0]+dx, cell[1]+dy)) for dx, dy in ((0,-1),(1,0),(0,1),(-1,0))]
        tile['foundationDepth'] = round(height - min(height, *neighbors) + .07, 6)
        raised_tiles += height != 0

    hidden = []
    for entity in instances:
        parent = entity['id']
        seen = set()
        while parent not in fields and parent in records and parent not in seen:
            seen.add(parent)
            parent = transforms.local(parent).get('parent', 0)
        if parent not in fields:
            continue
        field = fields[parent]
        x, y = local(parent, *entity['position'][:2])
        ramp = field.ramps.get((math.floor(x), math.floor(y)))
        if ((math.floor(x), math.floor(y)), entity['prototype']) in field.suppressed:
            hidden.append(entity)
            continue
        model = library.get(entity.get('modelId'), {})
        height = field.height(x, y)
        if ramp and ramp.get('geometry', True) and ramp['sourcePrototype'] == entity['prototype'] and model.get('parts'):
            height = ramp['bottom']
            parts = variants.get(entity.get('geometryKey'), model['parts'])
            top = max(vector(p['max'])[2] for p in parts)
            scale = (ramp['top']-ramp['bottom'])/top
            key = f"elevation:{parent}:{entity['id']}"
            variants[key] = [{**p, 'min': [*vector(p['min'])[:2], vector(p['min'])[2]*scale],
                             'max': [*vector(p['max'])[:2], vector(p['max'])[2]*scale]} for p in parts]
            entity['geometryKey'] = key
            entity['elevationRamp'] = ramp
            entity['renderYaw'] = transforms.resolve(parent)[2] + math.atan2(-ramp['direction'][0], ramp['direction'][1])
            fitted_stairs += 1
        if entity.get('zStair') and ramp and ramp.get('physicsCurve'):
            entity['elevationRamp'] = ramp
        offset = list(entity.get('renderOffset', [0, 0, 0]))
        offset[2] += height
        entity['renderOffset'] = offset
        entity['elevation'] = round(height, 6)
        raised_entities += height != 0
    hidden_ids = {id(e) for e in hidden}
    instances[:] = [e for e in instances if id(e) not in hidden_ids]
    return {'suppressedStairProjections': len(hidden), 'profiles': len(fields), 'elevatedTiles': raised_tiles, 'elevatedEntities': raised_entities,
            'fittedStairTiles': fitted_stairs}
