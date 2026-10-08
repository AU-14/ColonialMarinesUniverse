"""Resolve authored surface attachments without changing saved gameplay coordinates."""
from __future__ import annotations

import math
from collections import defaultdict


def vector(value):
    return tuple(float(v) for v in (value.split(',') if isinstance(value, str) else value))


def surface(model):
    label = model.get('supportSurface')
    if not label or model.get('supportSurfaces'):
        return None
    return part_surface(model, label)


def part_surface(model, label):
    parts = [part for part in model.get('parts', []) if label and part.get('label') == label]
    if len(parts) != 1 or parts[0].get('shape', 'Box') != 'Box' or parts[0].get('yaw', 0) or parts[0].get('pitch', 0):
        return None
    low, high = vector(parts[0]['min']), vector(parts[0]['max'])
    if len(low) != 3 or len(high) != 3 or high[2] <= 0 or any(
            not math.isfinite(a) or not math.isfinite(b) or a >= b for a, b in zip(low, high)):
        return None
    return low, high


def surfaces(model):
    labels = model.get('supportSurfaces', [])
    if not labels:
        bounds = surface(model)
        return [bounds] if bounds else []
    if (not isinstance(labels, list) or model.get('supportSurface') or model.get('connectToNeighbours') or
            any(not isinstance(label, str) or not label for label in labels) or len(set(labels)) != len(labels)):
        return []
    bounds = [part_surface(model, label) for label in labels]
    return bounds if all(bound is not None for bound in bounds) else []


def resolve_placements(instances, models, geometry_variants=None):
    """Exact mapped surfaces only. Never invent a table or borrow one from another floor."""
    library = {model['id']: model for model in models}
    cells = defaultdict(list)
    def horizontal_position(entity):
        offset = entity.get('renderOffset', [0, 0])
        return entity['position'][0] + offset[0], entity['position'][1] + offset[1]

    for entity in instances:
        entity.pop('renderOffset', None)
        entity.pop('support', None)
        entity.pop('floorCladding', None)
        model = library.get(entity.get('modelId'), {})
        ground = vector(model.get('groundOffset', [0, 0]))
        offset = list(entity.get('layoutOffset', [0, 0, 0]))
        offset[0] += ground[0]
        offset[1] += ground[1]
        if any(offset):
            entity['renderOffset'] = offset
        parts = (geometry_variants or {}).get(entity.get('geometryKey'), model.get('parts', []))
        if entity.get('matchKind') != 'exact':
            continue
        x, y = horizontal_position(entity)
        z = entity['position'][2]
        yaw = entity.get('renderYaw', entity['yaw'])
        c, s = math.cos(yaw), math.sin(yaw)
        for low, high in surfaces({**model, 'parts': parts}):
            corners = [(x + u*c-v*s, y + u*s+v*c) for u in (low[0], high[0]) for v in (low[1], high[1])]
            for cx in range(math.floor(min(p[0] for p in corners)), math.floor(max(p[0] for p in corners)) + 1):
                for cy in range(math.floor(min(p[1] for p in corners)), math.floor(max(p[1] for p in corners)) + 1):
                    cells[(cx, cy, z)].append((entity, low, high))
    attached = missing = 0
    mounted_surfaces = unstable_mounts = 0
    def support_at(entity, model, x, y, allow_probe=True):
        z = entity['position'][2]
        choices = []
        for support, low, high in cells[(math.floor(x), math.floor(y), z)]:
            if support['id'] == entity['id']:
                continue
            sx, sy = horizontal_position(support)
            dx, dy = x - sx, y - sy
            yaw = support.get('renderYaw', support['yaw'])
            c, s = math.cos(yaw), math.sin(yaw)
            u, v = c*dx+s*dy, -s*dx+c*dy
            if low[0] <= u <= high[0] and low[1] <= v <= high[1] and high[2] > 0:
                choices.append((dx*dx+dy*dy, support['id'], -high[2]))
        if choices:
            _, uid, negative_height = min(choices)
            height = -negative_height
            from build_models import part_bounds
            minimum = min(part_bounds(part)[0][2] for part in model['parts'])
            return round(height - minimum + .002, 6), {'entity': uid, 'height': height, 'method': 'authored surface footprint'}
        if allow_probe and model.get('supportProbePart'):
            from support_probe import probe
            point = probe(model)
            if point is not None:
                yaw = entity.get('renderYaw', entity['yaw'])
                c, s = math.cos(yaw), math.sin(yaw)
                height, support = support_at(entity, model, x+c*point[0]-s*point[1], y+s*point[0]+c*point[1], False)
                if support:
                    support = {**support, 'method': 'authored bottom contact', 'part': model['supportProbePart']}
                return height, support
        return 0, None

    for entity in instances:
        walls = entity.pop('_surfaceMountWalls', [])
        model = library.get(entity.get('modelId'), {})
        if model.get('placement') != 'surface':
            continue
        x, y = horizontal_position(entity)
        height, support = support_at(entity, model, x, y)
        correction = [0, 0, 0]
        mounted_wall = None
        if walls:
            from layout import back_wall_mount_offset
            for attempt in range(4):
                candidates = []
                for wall in walls:
                    delta = back_wall_mount_offset(model['parts'], entity.get('renderYaw', entity['yaw']),
                        library[wall['modelId']]['parts'], wall['yaw'], wall['delta'], height_offset=height)
                    if delta is not None:
                        candidates.append((math.hypot(*delta[:2]), wall['id'], delta))
                _, mounted_wall, correction = max(candidates) if candidates else (0, None, [0, 0, 0])
                next_height, support = support_at(entity, model, x + correction[0], y + correction[1])
                if abs(next_height - height) <= 1e-5:
                    height = next_height
                    break
                height = next_height
            else:
                # A height/footprint cycle has no stable mount. Keep the original source fallback.
                entity.update(baseModelId=entity['modelId'], modelId=None, matchKind='unmapped',
                    unsupportedState='Surface rear-wall placement did not converge')
                entity.pop('geometryKey', None)
                entity.pop('renderOffset', None)
                unstable_mounts += 1
                continue
            if mounted_wall is not None:
                base = entity.get('layoutOffset', [0, 0, 0])
                entity.update(layoutOffset=[round(base[i] + correction[i], 6) for i in range(3)],
                    backWallMount=mounted_wall, layoutAlignment='rear clearance at supported height')
                mounted_surfaces += 1
        horizontal = entity.get('renderOffset', [0, 0])[:2]
        if support or walls and (any(correction) or height):
            entity['renderOffset'] = [round(horizontal[i] + correction[i], 6) for i in range(2)] + [height]
        if support:
            entity['support'] = support
            attached += 1
        else:
            missing += 1
    from reagent_tank_placement import resolve as resolve_tank_cladding
    cladding = resolve_tank_cladding(instances, library, geometry_variants or {})
    result = {'surfacePropsPlaced': attached, 'surfacePropsWithoutSupport': missing}
    if any(model.get('placement') == 'surface' and model.get('backWallMountTargets') for model in models):
        result.update(surfaceRearWallsPlaced=mounted_surfaces, surfaceRearWallFallbacks=unstable_mounts)
    if any(model.get('reagentTankAppearance') for model in models):
        result['reagentTanksOnFloorCladding'] = cladding
    return result
