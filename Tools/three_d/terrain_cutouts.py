"""Source-authorized visual alcoves in terrain; gameplay collision is never changed."""
from __future__ import annotations

import math
from collections import Counter

EPSILON = 1e-5
MAXIMUM_PARTS = 128
MAXIMUM_CUTS = 16
MAXIMUM_REACH = 8
MINIMUM_SLAB_SIZE = .001
UNSUPPORTED_CUTTER_FIELDS = ('connectToNeighbours', 'wallMounted', 'faceAwayFromWall', 'cornerSurfaces',
                            'backWallMountTargets', 'windowMountTargets', 'panelEndTargets',
                            'openingFacingTargets', 'directionalModels')


def vector(value):
    return tuple(float(x) for x in (value.split(',') if isinstance(value, str) else value))


def valid_bounds(low, high):
    return len(low) == len(high) == 3 and all(math.isfinite(a) and math.isfinite(b) and a < b for a, b in zip(low, high))


def validate_contract(model, parse_vector):
    fields = ('terrainCutoutTargets', 'terrainCutoutMin', 'terrainCutoutMax')
    if not any(field in model for field in fields):
        return
    targets = model.get('terrainCutoutTargets')
    if (not isinstance(targets, list) or not targets or any(not isinstance(p, str) or not p for p in targets)
            or len(set(targets)) != len(targets) or any(field not in model for field in fields)):
        raise ValueError(f"{model['id']}: terrain cutouts require unique exact source targets and both bounds")
    low, high = parse_vector(model['terrainCutoutMin']), parse_vector(model['terrainCutoutMax'])
    if (not valid_bounds(low, high) or any(abs(v) > MAXIMUM_REACH for v in (*low, *high))
            or model.get('placement', 'floor') != 'floor' or any(model.get(k) for k in UNSUPPORTED_CUTTER_FIELDS)):
        raise ValueError(f"{model['id']}: terrain cutout requires independent floor geometry and ordered local bounds within eight tiles")
    model['terrainCutoutMin'], model['terrainCutoutMax'] = low, high


def turn(yaw):
    if not math.isfinite(yaw):
        return None
    angle = math.remainder(yaw, math.tau)
    result = round(angle / (math.pi / 2))
    return result if abs(angle - result * math.pi / 2) <= EPSILON else None


def rotate(point, quarter):
    x, y, z = point
    return ((x, y, z), (-y, x, z), (-x, -y, z), (y, -x, z))[quarter % 4]


def rotate_bounds(low, high, quarter):
    a, b = rotate(low, quarter), rotate(high, quarter)
    return tuple(min(x, y) for x, y in zip(a, b)), tuple(max(x, y) for x, y in zip(a, b))


def world_bounds(low, high, position, yaw):
    quarter = turn(yaw)
    if quarter is None or not valid_bounds(low, high) or not all(math.isfinite(v) for v in position):
        return None
    low, high = rotate_bounds(low, high, quarter)
    return tuple(low[i] + (position[i] if i < 2 else 0) for i in range(3)), tuple(high[i] + (position[i] if i < 2 else 0) for i in range(3))


def overlaps(low, high, other_low, other_high):
    return all(min(b, d) - max(a, c) > EPSILON for a, b, c, d in zip(low, high, other_low, other_high))


def part_bounds(part):
    low, high = vector(part['min']), vector(part['max'])
    center = tuple((a + b) / 2 for a, b in zip(low, high))
    half = tuple((b - a) / 2 for a, b in zip(low, high))
    yaw, pitch = math.radians(part.get('yaw', 0)), math.radians(part.get('pitch', 0))
    cp, sp = abs(math.cos(pitch)), abs(math.sin(pitch))
    x, y, z = half[0] * cp + half[2] * sp, half[1], half[0] * sp + half[2] * cp
    c, s = abs(math.cos(yaw)), abs(math.sin(yaw))
    extent = (x * c + y * s, x * s + y * c, z)
    return tuple(v - e for v, e in zip(center, extent)), tuple(v + e for v, e in zip(center, extent))


def clip_parts(parts, position, yaw, world_cuts):
    """Return original list on no change or unsupported/budget failure; subtract at most six slabs per input box."""
    quarter = turn(yaw)
    if quarter is None or len(parts) > MAXIMUM_PARTS:
        return parts, 'unsupported'
    cuts = []
    for low, high in world_cuts:
        if not valid_bounds(low, high):
            return parts, 'unsupported'
        shifted = [tuple(v[i] - (position[i] if i < 2 else 0) for i in range(3)) for v in (low, high)]
        cut = rotate_bounds(*shifted, -quarter)
        if cut not in cuts:
            cuts.append(cut)
    if len(cuts) > MAXIMUM_CUTS:
        return parts, 'budget'
    working, changed = list(parts), False
    for cut_low, cut_high in cuts:
        following = []
        for part in working:
            low, high = part_bounds(part)
            if not overlaps(low, high, cut_low, cut_high):
                following.append(part)
                continue
            part_turn = turn(math.radians(part.get('yaw', 0)))
            if part.get('shape', 'Box') != 'Box' or part.get('pitch', 0) or part.get('surface') or part_turn is None:
                return parts, 'unsupported'
            low, high = vector(part['min']), vector(part['max'])
            center = tuple((a + b) / 2 for a, b in zip(low, high))
            half = tuple((b - a) / 2 for a, b in zip(low, high))
            a, b = rotate_bounds(tuple(v - c for v, c in zip(cut_low, center)), tuple(v - c for v, c in zip(cut_high, center)), -part_turn)
            cursor_low, cursor_high = list(-v for v in half), list(half)
            if not overlaps(cursor_low, cursor_high, a, b):
                following.append(part)
                continue
            inside_low = [max(x, y) for x, y in zip(cursor_low, a)]
            inside_high = [min(x, y) for x, y in zip(cursor_high, b)]
            slabs = []
            for axis in range(3):
                if inside_low[axis] - cursor_low[axis] > EPSILON:
                    slab_high = list(cursor_high)
                    slab_high[axis] = inside_low[axis]
                    slabs.append((list(cursor_low), slab_high))
                if cursor_high[axis] - inside_high[axis] > EPSILON:
                    slab_low = list(cursor_low)
                    slab_low[axis] = inside_high[axis]
                    slabs.append((slab_low, list(cursor_high)))
                cursor_low[axis], cursor_high[axis] = inside_low[axis], inside_high[axis]
            for a, b in slabs:
                if any(y - x < MINIMUM_SLAB_SIZE for x, y in zip(a, b)):
                    return parts, 'unsupported'
                rotated = rotate(tuple((x + y) / 2 for x, y in zip(a, b)), part_turn)
                new_center = tuple(x + y for x, y in zip(center, rotated))
                new_half = tuple((y - x) / 2 for x, y in zip(a, b))
                following.append({**part, 'min': tuple(x - y for x, y in zip(new_center, new_half)),
                                  'max': tuple(x + y for x, y in zip(new_center, new_half))})
            changed = True
            if len(following) > MAXIMUM_PARTS:
                return parts, 'budget'
        if len(following) > MAXIMUM_PARTS:
            return parts, 'budget'
        working = following
    return (working, 'clipped') if changed else (parts, 'unchanged')


def apply_cutouts(instances, library, variants, records, component, transforms, render_yaw, excluded_sources=()):
    """Discover exact authored sources in all map records, including outside a cropped export."""
    references = {}
    for model in library.values():
        if model.get('terrainCutoutTargets'):
            for prototype in model.get('sourcePrototypes', []):
                references[prototype] = model
    if not references:
        return {}
    cutters = []
    for uid, record in sorted(records.items()):
        model = references.get(record['prototype'])
        if not model or uid in excluded_sources:
            continue
        sprite = component(uid, 'Sprite')
        if (sprite.get('visible') is False or vector(sprite.get('scale', (1, 1))) != (1, 1)
                or vector(sprite.get('offset', (0, 0))) != (0, 0)):
            continue
        try:
            x, y, yaw, map_root = transforms.resolve(uid)
            yaw = (yaw + math.radians(model.get('yawOffset', 0)) if model.get('useEntityRotation') else
                   render_yaw(yaw, model.get('sourceDirections', 1), sprite.get('noRot', False), sprite.get('snapCardinals', False),
                              model.get('yawOffset', 0), model.get('swapEastWest', False), model.get('sourceCardinalFacings')))
            rotation = str(sprite.get('rotation', 0)).strip()
            yaw += float(rotation[:-3]) if rotation.endswith('rad') else math.radians(float(rotation))
            ground = vector(model.get('groundOffset', (0, 0)))
            volume = world_bounds(vector(model['terrainCutoutMin']), vector(model['terrainCutoutMax']), (x + ground[0], y + ground[1]), yaw)
        except (ValueError, KeyError, TypeError):
            continue
        if volume:
            cutters.append((uid, map_root, model['terrainCutoutTargets'], volume))
    stats = Counter()
    target_prototypes = {prototype for _, _, targets, _ in cutters for prototype in targets}
    for entity in instances:
        entity.pop('terrainCutoutSources', None)
        entity.pop('terrainCutoutRejected', None)
        model = library.get(entity.get('modelId'))
        if entity.get('matchKind') != 'exact' or model is None or entity['prototype'] not in target_prototypes:
            continue
        _, _, _, map_root = transforms.resolve(entity['id'])
        ground = vector(model.get('groundOffset', (0, 0)))
        offset = entity.get('layoutOffset', (0, 0, 0))
        if offset[2] or model.get('placement', 'floor') != 'floor':
            continue
        position = tuple(entity['position'][i] + ground[i] + offset[i] for i in range(2))
        parts = variants.get(entity.get('geometryKey'), model['parts'])
        if not parts:
            continue
        bounds = [part_bounds(p) for p in parts]
        volume = world_bounds(tuple(min(b[0][i] for b in bounds) for i in range(3)),
                              tuple(max(b[1][i] for b in bounds) for i in range(3)), position, entity['renderYaw'])
        if volume is None:
            continue
        applicable = [(uid, cut) for uid, root, targets, cut in cutters if uid != entity['id'] and root == map_root
                      and entity['prototype'] in targets and overlaps(*volume, *cut)]
        if not applicable:
            continue
        clipped, status = clip_parts(parts, position, entity['renderYaw'], [cut for _, cut in applicable])
        if status == 'clipped':
            key = f"{model['id']}:terrain:{entity['id']}"
            variants[key] = clipped
            entity.update(geometryKey=key, terrainCutoutSources=sorted({uid for uid, _ in applicable}))
            stats['terrainCutoutEntities'] += 1
        elif status != 'unchanged':
            entity['terrainCutoutRejected'] = status
            stats['terrainCutoutRejected'] += 1
    return dict(stats)
