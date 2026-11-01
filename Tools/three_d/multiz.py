"""Physical presentation of logical map levels and the game's sticky stair curves.

Logical depth is retained separately from physical Z. Three tiles provides the
clearance required by the existing 2.8-tile walls; the 2D sprite offset is not a
storey height. Keep this projection aligned with CMU3DZProjection.cs.
"""
from copy import deepcopy
import math

STORY_HEIGHT = 3.0


def curve_height(curve, t):
    t = max(0, min(1, t)) * (len(curve) - 1)
    i = min(int(t), len(curve) - 1)
    return curve[i] + (curve[min(i + 1, len(curve) - 1)] - curve[i]) * (t - i)


def stair_parts(curve, yaw, corner=False, lower=0, upper=0):
    # Source physics uses cardinal world rotation with tile-local coordinates.
    direction = round(yaw / (math.pi / 2)) % 4
    nx, ny = (12, 12) if corner else (12, 1) if direction % 2 else (1, 12)
    result = []
    for y in range(ny):
        for x in range(nx):
            u, v = (x + .5) / nx, (y + .5) / ny
            t = (1-v, u, v, 1-u)[direction] if not corner else (
                (2-u-v)/2, (u+1-v)/2, (u+v)/2, (1-u+v)/2)[direction]
            half_span = .5/nx if corner else .5/max(nx, ny)
            phase = max(curve_height(curve, t-half_span), curve_height(curve, t), curve_height(curve, t+half_span))
            height = STORY_HEIGHT * phase + lower + min(1, max(0, phase)) * (upper-lower)
            result.append({'min': [x/nx-.5, y/ny-.5, min(lower, height)],
                           'max': [(x+1)/nx-.5, (y+1)/ny-.5, height],
                           'color': '#69716F'})
    return result


def stack_scenes(scenes, default_level=0):
    scenes = sorted(scenes, key=lambda s: s['map']['level'])
    if not scenes:
        raise ValueError('A stack needs at least one map')
    result = {'schemaVersion': 2, 'modelsUrl': scenes[0].get('modelsUrl', '../generated/models.json'),
              'coordinates': {**scenes[0].get('coordinates', {}), 'z': 'physical height in game tiles',
                              'level': 'logical map index', 'storyHeight': STORY_HEIGHT},
              'instances': [], 'tiles': [], 'tilePalette': {}, 'geometryVariants': {}, 'sourceReferences': {},
              'diagnostics': {'levels': {}}}
    levels = []
    for original in scenes:
        scene = deepcopy(original)
        level = scene['map']['level']
        levels.append({'z': level, 'height': level * STORY_HEIGHT,
                       'label': 'Ground level' if level == 0 else f'Level {level:+d}'})
        prefix = f'{level}:'
        for entity in scene['instances']:
            entity['level'] = level
            entity['sceneKey'] = prefix + str(entity['id'])
            entity['position'][2] = level * STORY_HEIGHT
            if 'geometryKey' in entity:
                entity['geometryKey'] = prefix + entity['geometryKey']
        for tile in scene['tiles']:
            tile['level'] = level
            tile['z'] = level * STORY_HEIGHT
            tile['grid'] = prefix + str(tile['grid'])
            tile['palette'] = prefix + str(tile['palette'])
            # A floor is visible from the level below, including its underside.
            tile['solid'] = True
        for name in ('tilePalette', 'geometryVariants'):
            result[name].update({prefix + str(k): v for k, v in scene.get(name, {}).items()})
        result['sourceReferences'].update(scene.get('sourceReferences', {}))
        result['instances'].extend(scene['instances'])
        result['tiles'].extend(scene['tiles'])
        result['diagnostics']['levels'][str(level)] = scene.get('diagnostics', {})
    primary = next((s for s in scenes if s['map']['level'] == default_level), scenes[0])
    bounds = [min(s['map']['bounds'][i] for s in scenes) if i < 2 else
              max(s['map']['bounds'][i] for s in scenes) for i in range(4)]
    result['map'] = {**primary['map'], 'levels': levels, 'bounds': bounds,
                     'defaultLevel': primary['map']['level'], 'storyHeight': STORY_HEIGHT}
    # defaultFocus retains a logical level for navigation; geometry uses physical Z.
    floors = {(t['level'], math.floor(t['x']+.001), math.floor(t['y']+.001)): t for t in result['tiles']}
    stair_count = 0
    for entity in result['instances']:
        stair = entity.get('zStair')
        if not stair:
            continue
        level = entity['level']
        x, y = map(math.floor, entity['position'][:2])
        lower = floors.get((level, x, y), {}).get('elevation', 0)
        upper = floors.get((level+1, x, y), {}).get('elevation', 0)
        key = 'z-stair:' + entity['sceneKey']
        result['geometryVariants'][key] = stair_parts(stair['heightCurve'], entity.get('yaw', 0),
                                                      stair.get('corner', False), lower, upper)
        if (ramp := entity.get('elevationRamp')) and ramp.get('physicsCurve'):
            dx, dy = ramp['direction']
            result['geometryVariants'][key] = stair_parts([ramp['top']/STORY_HEIGHT, ramp['bottom']/STORY_HEIGHT],
                                                         math.atan2(-dx, dy))
        entity.update(geometryKey=key, renderYaw=stair.get('gridYaw', 0), renderOffset=[0, 0, 0],
                      modelId='CMU3DTopologyStairsFlightStudyCloud', matchKind='exact')
        entity.pop('unsupportedState', None)
        stair_count += 1
    result['diagnostics']['multiZStairs'] = stair_count
    return result
