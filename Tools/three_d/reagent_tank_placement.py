"""Presentation-only lift for tanks resting on three proven thin floor grates."""
import math
from placement import vector

TARGETS = ('CMCatwalk', 'CMCatwalkPrison', 'RMCCatwalkHybrisaElevator')


def cladding_offset(model, support, prototype, delta, yaw):
    if (not model.get('reagentTankAppearance') or prototype not in TARGETS or
            prototype not in support.get('sourcePrototypes', []) or model.get('placement', 'floor') != 'floor' or
            support.get('placement', 'floor') != 'floor' or any(not math.isfinite(v) or abs(v) > .0001 for v in delta) or
            not math.isfinite(yaw) or abs(math.sin(yaw * 2)) > .0001 or
            vector(support.get('groundOffset', (0, 0))) != (0, 0) or
            any(support.get(k) for k in ('wallMounted', 'connectToNeighbours', 'backWallMountTargets', 'windowMountTargets',
                                          'spriteStates', 'reagentTankAppearance', 'terrainCutoutTargets'))):
        return 0
    parts = support.get('parts', [])
    if not parts or not model.get('parts'):
        return 0
    for part in parts:
        low, high = vector(part['min']), vector(part['max'])
        if (part.get('shape', 'Box') != 'Box' or part.get('yaw', 0) or part.get('pitch', 0) or
                any(not math.isfinite(v) for v in (*low, *high)) or any(a >= b for a, b in zip(low, high)) or
                low[0] < -.50001 or low[1] < -.50001 or high[0] > .50001 or high[1] > .50001 or
                low[2] < 0 or high[2] > .05):
            return 0
    from build_models import part_bounds
    bounds = [part_bounds(p) for p in model['parts']]
    if abs(min(lo[2] for lo, hi in bounds)) > .00001 or any(
            lo[0] < -.50001 or lo[1] < -.50001 or hi[0] > .50001 or hi[1] > .50001 for lo, hi in bounds):
        return 0
    return max(vector(p['max'])[2] for p in parts) + .002


def resolve(instances, library, variants):
    cells = {}
    for entity in instances:
        if entity.get('prototype') in TARGETS and entity.get('matchKind') == 'exact':
            point = entity['position']
            cells.setdefault((math.floor(point[0]), math.floor(point[1]), point[2]), []).append(entity)
    count = 0
    for entity in instances:
        model = library.get(entity.get('modelId'), {})
        if not model.get('reagentTankAppearance') or entity.get('matchKind') != 'exact':
            continue
        point = entity['position']
        choices = []
        for support in cells.get((math.floor(point[0]), math.floor(point[1]), point[2]), []):
            definition = library.get(support.get('modelId'), {})
            # Unexpected layout/vertical adjustments are not a known floor cladding.
            if any(support.get('renderOffset', (0, 0, 0))) or support.get('unsupportedState'):
                continue
            parts = variants.get(support.get('geometryKey'), definition.get('parts', []))
            offset = cladding_offset(model, {**definition, 'parts': parts}, support['prototype'],
                                     [point[i] - support['position'][i] for i in (0, 1)], support.get('renderYaw', support['yaw']))
            if offset:
                choices.append((offset, support['id']))
        if choices:
            offset, uid = min(choices)
            entity['renderOffset'] = [*entity.get('renderOffset', [0, 0])[:2], round(offset, 6)]
            entity['floorCladding'] = {'entity': uid, 'height': round(offset - .002, 6), 'method': 'exact co-located thin catwalk'}
            count += 1
    return count
