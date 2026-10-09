"""Source-aware presentation rotation and cardinal connectivity.

Angles use Robust's ordinary counterclockwise RotateVec convention. Zero facing
is south. Sprite screen offsets are not ground translations: many compensate
for padded art, projected height, or direction-specific wall artwork.
"""
from __future__ import annotations

from collections import Counter, defaultdict
import math


CARDINALS = ((1, 0, 1, 1), (2, 0, -1, 3), (4, 1, 0, 0), (8, -1, 0, 2))
CORNER_NEIGHBOURS = tuple((flag, x, y) for flag, x, y, _ in CARDINALS) + ((16, 1, 1), (32, 1, -1), (64, -1, -1), (128, -1, 1))


def corner_states(mask):
    """IconSmooth CornerFill values in world-grid SE/NE/NW/SW order."""
    return [int(bool(mask & ccw)) | (2 if mask & diagonal else 0) | (4 if mask & cw else 0)
            for ccw, diagonal, cw in ((4, 32, 2), (1, 16, 4), (8, 128, 1), (2, 64, 8))]


def corner_parts(model, mask):
    return [{**part, 'surface': model['cornerSurfaces'][state*4+direction], 'surfaceAxis': 'XY'}
            for part, state, direction in zip(model['parts'], corner_states(mask), (0, 2, 1, 3))] + model['parts'][4:]


def render_yaw(yaw, directions=1, no_rotation=False, snap=False, offset=0, swap_east_west=False, cardinal_facings=None):
    if no_rotation:
        yaw = 0 if directions == 1 else round_direction(yaw, directions)
    elif directions == 1 and snap:
        yaw -= round_direction(yaw, 4)
    if directions == 4 and cardinal_facings:
        index = math.floor(yaw/(math.pi/2)+.5) % 4
        yaw += (cardinal_facings[index]-index)*math.pi/2
    if swap_east_west and directions == 4 and abs(math.sin(round_direction(yaw, 4))) > .5:
        yaw += math.pi
    return yaw + math.radians(offset)


def round_direction(yaw, directions):
    step = math.tau / directions
    return math.floor(yaw / step + .5) * step


def face_away_from_wall_yaw(yaw, grid_yaw, walls):
    local = yaw-grid_yaw
    for flag, dx, dy, _ in CARDINALS:
        if not walls & flag:
            continue
        opposite = {1: 2, 2: 1, 4: 8, 8: 4}[flag]
        facing = math.sin(local)*dx-math.cos(local)*dy
        if walls == flag or (not walls & opposite and facing > .99999):
            return grid_yaw+math.atan2(-dx, dy)
    return yaw


def wall_target_yaw(yaw, grid_yaw, on_wall, targets, walls):
    for flag, dx, dy, _ in CARDINALS:
        if targets != flag:
            continue
        if on_wall:
            return grid_yaw+math.atan2(dx, -dy)
        opposite = {1: 2, 2: 1, 4: 8, 8: 4}[flag]
        if walls & opposite:
            return grid_yaw+math.atan2(-dx, dy)
    return yaw


def opening_fixture_yaw(yaw, grid_yaw, walls, fixture_yaw):
    """Use a named fixture's source facing only when the saved opening faces a wall."""
    def flag(angle):
        turn=(angle-grid_yaw)/(math.pi/2)
        if abs(turn-round(turn)) > 1e-5:return 0
        return (2,4,1,8)[round(turn)%4]
    front,candidate=flag(yaw),flag(fixture_yaw)
    return fixture_yaw if front and candidate and walls & front and not walls & candidate else yaw


def offset_target_mask(delta, grid_yaw):
    """Resolve an offset fixture pivot only when it selects one clear grid side."""
    c, s = math.cos(grid_yaw), math.sin(grid_yaw)
    x, y = c*delta[0]+s*delta[1], -s*delta[0]+c*delta[1]
    if abs(x) >= .25 and abs(y) <= .125:
        return 4 if x > 0 else 8
    if abs(y) >= .25 and abs(x) <= .125:
        return 1 if y > 0 else 2
    return 0


def vector(value):
    return tuple(float(v) for v in (value.split(',') if isinstance(value, str) else value))


def wall_mount_offset(position, tile_center, yaw):
    """Remove only displacement normal to the wall; retain hand-authored along-wall spacing."""
    normal = (math.sin(yaw), -math.cos(yaw))
    distance = sum((position[i] - tile_center[i]) * normal[i] for i in range(2))
    return [-distance * normal[0], -distance * normal[1], 0]


def inside_wall_parts(parts):
    """Mount on the room side when the saved pivot is beside, not inside, a wall."""
    result = []
    for part in parts:
        low, high = list(vector(part['min'])), list(vector(part['max']))
        low[1], high[1] = -1-high[1], -1-low[1]
        mounted = {**part, 'min': low, 'max': high}
        if part.get('surface') and part.get('surfaceAxis', 'XZ') == 'XZ':
            mounted['surfaceFlipU'] = not part.get('surfaceFlipU', False)
        result.append(mounted)
    return result


def shutter_exterior_target(model_id, prototype):
    """Exact source contracts; arbitrary square objects are not shutter supports."""
    return model_id in ('CMU3DHybrisaWindowShutter', 'CMU3DHybrisaWindowShutterOpen') and prototype in (
        'RMCDoubleDoorGlassHybrisa', 'CMAirlockGlassHybrisa')


def shutter_exterior_axis(prototype, mask=0):
    if prototype == 'RMCWindowPrisonCell':
        # The thick square body has two observation faces. Connectivity, rather
        # than the square XY bounds, supplies the supported opening orientation.
        if mask & 3 and not mask & 12:
            return 0
        if mask & 12 and not mask & 3:
            return 1
        return None
    return 1 if prototype in ('RMCDoubleDoorGlassHybrisa', 'CMAirlockGlassHybrisa') else None


def shutter_mount_envelope(model, library):
    """Keep a door-mounted shutter still while either authored door pose changes."""
    models = [model]
    if alternate := model.get('alternateDoorModel'):
        other = library.get(alternate)
        if other is None:
            return None
        facing = ('yawOffset', 'sourceDirections', 'useEntityRotation', 'swapEastWest', 'sourceCardinalFacings', 'groundOffset')
        if any(model.get(key) != other.get(key) for key in facing):
            return None
        models.append(other)
    return [part for item in models for parts in [item['parts'],
        *[frame['parts'] for state in item.get('doorSpriteStates', {}).values() for frame in state['frames']]] for part in parts]


def prison_shutter_side(prototype, model_id, axis, yaw, window_yaw, delta, inside=False):
    """Negative/positive facade bits for an exact, co-pivot parallel shutter."""
    if (prototype not in ('RMCShutterHybrisaWindow', 'RMCShutterHybrisaWindowOpen') or
            model_id not in ('CMU3DHybrisaWindowShutter', 'CMU3DHybrisaWindowShutterOpen') or
            inside or axis not in (0, 1) or not all(math.isfinite(v) for v in (yaw, window_yaw, *delta[:2])) or
            math.hypot(*delta[:2]) > 1e-5):
        return 0
    normal = (math.sin(yaw-window_yaw), -math.cos(yaw-window_yaw))
    if abs(normal[1-axis]) > 1e-5:
        return 0
    return 2 if normal[axis] > 0 else 1


def prison_recess_parts(parts, axis, sides):
    """Reserve the shutter inside its wall frame, without changing either facade profile.

    .45 outer assembly - .125 shutter - .02 gap = .305 backing face.
    These are bounded context-fit depths, not dimensions recovered from the RSI.
    Only the exact untextured Box window draft is supported. Return None for an
    unknown construction rather than deforming its texels or translucent panes.
    """
    from build_models import rgba
    if (not parts or axis not in (0, 1) or sides not in (1, 2, 3) or
            any(p.get('shape', 'Box') != 'Box' or p.get('yaw') or p.get('pitch') or p.get('surface') for p in parts)):
        return None
    bounds = [(vector(p['min']), vector(p['max'])) for p in parts]
    if any(not all(math.isfinite(v) for v in (*lo, *hi)) or any(lo[i] >= hi[i] for i in range(3)) for lo, hi in bounds):
        return None
    glazing = [abs(v[axis]) for p, pair in zip(parts, bounds) if rgba(p.get('color', '#FFFFFF'))[3] < 1 for v in pair]
    if not glazing or abs(max(glazing)-.045) > 1e-5:
        return None
    faces = (-min(lo[axis] for lo, _ in bounds), max(hi[axis] for _, hi in bounds))
    if any(sides & (1 << i) and abs(face-.525) > 1e-5 for i, face in enumerate(faces)):
        return None
    def mapped(value):
        side = 1 if value > 0 else 0
        depth = abs(value)
        if depth <= .045 or not sides & (1 << side):
            return value
        return math.copysign(.045+(depth-.045)*(.305-.045)/(faces[side]-.045), value)
    result = []
    for part, (low, high) in zip(parts, bounds):
        a, b = list(low), list(high)
        a[axis], b[axis] = mapped(a[axis]), mapped(b[axis])
        result.append({**part, 'min': a, 'max': b})
    return result


def prison_wall_join_face(delta, wall_yaw, grid_yaw, window_mask):
    """A cardinal wall face touching the end of a supported window opening."""
    axis = shutter_exterior_axis('RMCWindowPrisonCell', window_mask)
    if axis is None or not all(math.isfinite(v) for v in (*delta[:2], wall_yaw, grid_yaw)):
        return 0
    c, s = math.cos(grid_yaw), math.sin(grid_yaw)
    local = (c*delta[0]+s*delta[1], -s*delta[0]+c*delta[1])
    if abs(local[axis]) > 1e-5 or abs(abs(local[1-axis])-1) > 1e-5:
        return 0
    c, s = math.cos(wall_yaw), math.sin(wall_yaw)
    local = (c*delta[0]+s*delta[1], -s*delta[0]+c*delta[1])
    for i in range(2):
        if abs(abs(local[i])-1) < 1e-5 and abs(local[1-i]) < 1e-5:
            return (1 if i == 0 else 4) << int(local[i] > 0)
    return 0


def prison_joined_relief_parts(parts, faces):
    """Hide inferred exposed-face trim inside a source-connected wall/window joint.

    The core remains exactly full-tile. Changed trim stays inside that original
    opaque core or is a subset of its original part, so no occupied volume is added.
    """
    from build_models import rgba
    if (not parts or faces not in range(1, 16) or any(p.get('shape', 'Box') != 'Box' or
            p.get('yaw') or p.get('pitch') or p.get('surface') or rgba(p.get('color', '#FFFFFF'))[3] != 1 for p in parts)):
        return None
    cores = [p for p in parts if p.get('label') == 'full tile wall core']
    if len(cores) != 1 or vector(cores[0]['min']) != (-.5, -.5, 0) or vector(cores[0]['max']) != (.5, .5, 2.8):
        return None
    core = cores[0]
    result = []
    for part in parts:
        low, high = list(vector(part['min'])), list(vector(part['max']))
        if not all(math.isfinite(v) for v in (*low, *high)) or any(low[i] >= high[i] for i in range(3)):
            return None
        if part is not core:
            for axis, sign, flag in ((0,-1,1),(0,1,2),(1,-1,4),(1,1,8)):
                if not faces & flag: continue
                a, b = sorted((sign*low[axis], sign*high[axis]))
                if b <= .5+1e-6: continue
                if b > .55 or low[2] < 0 or high[2] > 2.8: return None
                a, b = (a-(b-.49), .49) if a >= .5-1e-6 else (a, .49)
                low[axis], high[axis] = sorted((sign*a, sign*b))
            subset = all(vector(part['min'])[i] <= low[i] <= high[i] <= vector(part['max'])[i] for i in range(3))
            buried = all(vector(core['min'])[i] <= low[i] < high[i] <= vector(core['max'])[i] for i in range(3))
            if not (subset or buried): return None
        result.append({**part, 'min': low, 'max': high})
    return result


def window_mount_offset(parts, yaw, window_parts, window_yaw, delta, inside=False, exterior_axis=None):
    """Fit the shutter behind its front-facing plane against parallel glazing, with a .02-tile assembly gap."""
    from build_models import part_bounds
    if not parts or not window_parts:
        return None
    bounds = [part_bounds(p) for p in window_parts]
    low = [min(a[i] for a, _ in bounds) for i in range(2)]
    high = [max(b[i] for _, b in bounds) for i in range(2)]
    width, depth = high[0]-low[0], high[1]-low[1]
    side = -1 if inside else 1
    normal = (side*math.sin(yaw), -side*math.cos(yaw))
    local = (side*math.sin(yaw-window_yaw), -side*math.cos(yaw-window_yaw))
    if exterior_axis is not None:
        if (inside or exterior_axis not in (0, 1) or math.hypot(*delta[:2]) > 1e-5 or
                abs(local[1-exterior_axis]) > 1e-5):
            return None
    elif abs(width-depth) < 1e-5 or abs(local[0 if width > depth else 1]) > 1e-5:
        return None  # A corner/crossing or perpendicular pane has no unambiguous parallel face.
    face = sum(delta[i]*normal[i] + max(low[i]*local[i], high[i]*local[i]) for i in range(2))
    back = -min(part_bounds(p)[0][1] for p in parts) if inside else max(part_bounds(p)[1][1] for p in parts)
    distance = max(0, face+back+.02)
    return [distance*normal[0], distance*normal[1], 0]


def panel_end_limits(parts, yaw, obstacles, obstacle_yaw, delta):
    """Conservative horizontal opening clearance against explicitly selected solid trim.

    Tall wall trim defines the opening at every elevation, including when a pane
    rests on a counter. Callers select the joining pane once, in grid coordinates,
    so perpendicular panes form a butt joint instead of both shortening.
    """
    from build_models import part_bounds
    left, right = min(vector(p['min'])[0] for p in parts), max(vector(p['max'])[0] for p in parts)
    low_y, high_y = min(vector(p['min'])[1] for p in parts), max(vector(p['max'])[1] for p in parts)
    turn = (obstacle_yaw-yaw)/(math.pi/2)
    if abs(turn-round(turn)) > 1e-5:
        return left, right
    c, s = math.cos(yaw), math.sin(yaw)
    dx, dy = c*delta[0]+s*delta[1], -s*delta[0]+c*delta[1]
    c, s = math.cos(obstacle_yaw-yaw), math.sin(obstacle_yaw-yaw)
    bounds = []
    for part in obstacles:
        if part.get('shape', 'Box') != 'Box':
            continue
        low, high = part_bounds(part)
        corners = [(c*x-s*y+dx, s*x+c*y+dy) for x in (low[0],high[0]) for y in (low[1],high[1])]
        lx, hx = min(x for x,y in corners), max(x for x,y in corners)
        ly, hy = min(y for x,y in corners), max(y for x,y in corners)
        bounds.append((lx,hx,ly,hy))
    # A wall spanning the panel's pivot is behind/in front of it, not an end
    # support. Its separate trim members must not squeeze both panel ends.
    if not bounds or min(b[0] for b in bounds) <= 0 <= max(b[1] for b in bounds):
        return left,right
    for lx,hx,ly,hy in bounds:
        if min(hy,high_y)-max(ly,low_y) <= 1e-5:
            continue
        if lx > 0: right = min(right,lx-.01)
        if hx < 0: left = max(left,hx+.01)
    return left, right


def fit_panel_ends(parts, left, right):
    """Fit the complete frame, clips and pane proportionally; never delete an end cap."""
    if not parts or any(p.get('shape','Box') != 'Box' or p.get('yaw',0) or p.get('pitch',0) for p in parts):
        return parts
    lo, hi = min(vector(p['min'])[0] for p in parts), max(vector(p['max'])[0] for p in parts)
    if not (lo <= left <= lo+.18 and hi-.18 <= right <= hi and right-left >= (hi-lo)*.7):
        return parts
    if abs(left-lo)+abs(right-hi) < 1e-5:
        return parts
    scale = (right-left)/(hi-lo)
    result = []
    for part in parts:
        a, b = list(vector(part['min'])), list(vector(part['max']))
        a[0], b[0] = left+(a[0]-lo)*scale, left+(b[0]-lo)*scale
        result.append({**part,'min':a,'max':b})
    return result


def back_wall_mount_offset(parts, yaw, wall_parts, wall_yaw, delta, room_side=False, height_offset=0):
    """Clear a rear wall along the saved front, using only solid parts at the fixture's height and frontage."""
    from build_models import part_bounds
    if room_side:
        yaw += math.pi
    c, s = math.cos(yaw), math.sin(yaw)
    dx, dy = c*delta[0]+s*delta[1], -s*delta[0]+c*delta[1]
    turn = (wall_yaw-yaw)/(math.pi/2)
    if not parts or not wall_parts or not -1e-5 <= dy <= 1.01 or abs(turn-round(turn)) > 1e-5:
        return None
    turn = round(turn) % 4
    distance = 0
    for wall in wall_parts:
        if wall.get('shape', 'Box') != 'Box' or wall.get('surface'):
            continue
        low, high = part_bounds(wall)
        for _ in range(turn):
            low, high = (-high[1],low[0],low[2]), (-low[1],high[0],high[2])
        low, high = (low[0]+dx,low[1]+dy,low[2]), (high[0]+dx,high[1]+dy,high[2])
        for part in parts:
            a, b = part_bounds(part)
            a, b = (*a[:2], a[2] + height_offset), (*b[:2], b[2] + height_offset)
            if room_side:
                a, b = (-b[0], 1+a[1], a[2]), (-a[0], 1+b[1], b[2])
            if min(b[0],high[0])-max(a[0],low[0]) <= 1e-5 or min(b[2],high[2])-max(a[2],low[2]) <= 1e-5:
                continue
            distance = max(distance,b[1]-low[1]+.01)
    if not 1e-5 < distance <= .75:
        return None
    return [distance*s,-distance*c,0]


def connected_parts(parts, mask, support_label=None, end_inset=0):
    """Join half panels from the pivot to each occupied cardinal edge.

Keep the complete original panel for straight runs, including their asymmetry.
Junctions use clipped halves, never overlapping whole perpendicular panels.
"""
    if support_label:
        result = []
        for part in parts:
            if part.get('omitWhenConnected', 0) & mask:
                continue
            low, high = list(vector(part['min'])), list(vector(part['max']))
            if part.get('label') == support_label:
                if mask & 8: low[0] = -.5
                if mask & 4: high[0] = .5
                if mask & 2: low[1] = -.5
                if mask & 1: high[1] = .5
            result.append({**part, 'min': low, 'max': high})
        return result
    turns = ([0] if mask in (0, 4, 8, 12) else [1] if mask in (1, 2, 3) else
             [turn for flag, _, _, turn in CARDINALS if mask & flag])
    half = mask not in (0, 1, 2, 3, 4, 8, 12)
    result = []
    seen = set()
    for turn in turns:
        for part in parts:
            omit = part.get('omitWhenConnected', 0)
            for _ in range(turn):
                omit = ((omit & 1) << 3) | ((omit & 2) << 1) | ((omit & 4) >> 2) | ((omit & 8) >> 2)
            if omit & mask:
                continue
            low, high = list(vector(part['min'])), list(vector(part['max']))
            if end_inset:
                if mask & (8, 2, 4, 1)[turn] and abs(low[0] - (-.5 + end_inset)) < 1e-5:
                    low[0] = -.5
                if mask & (4, 1, 8, 2)[turn] and abs(high[0] - (.5 - end_inset)) < 1e-5:
                    high[0] = .5
            if half:
                low[0] = max(0, low[0])
                if high[0] <= low[0]:
                    continue
            corners = [(x, y) for x in (low[0], high[0]) for y in (low[1], high[1])]
            for _ in range(turn):
                corners = [(-y, x) for x, y in corners]
            a = [min(v[0] for v in corners), min(v[1] for v in corners), low[2]]
            b = [max(v[0] for v in corners), max(v[1] for v in corners), high[2]]
            key = (*a, *b, part.get('color'))
            if key in seen:
                continue
            seen.add(key)
            result.append({**part, 'min': a, 'max': b})
    return result


def resolve_layout(instances, models, records, defaults, transforms, *, excluded_terrain_sources=()):
    from wall_paper import resolve_layout as paper_layout
    return paper_layout(instances, models, records, defaults, transforms, _resolve_layout, excluded_terrain_sources)


def _resolve_layout(instances, models, records, defaults, transforms, *, excluded_terrain_sources=()):
    library = {m['id']: m for m in models}
    contexts = {}
    cells = defaultdict(list)
    target_positions = defaultdict(list)
    target_prototypes = {p for m in models for p in m.get('wallFacingTargets', [])}
    window_targets = {p for m in models for p in m.get('windowMountTargets', [])}
    window_models = {p: m for m in models for p in m.get('sourcePrototypes', []) if p in window_targets}
    window_cells = defaultdict(list)
    prison_shutter_models = {p: m for m in models for p in m.get('sourcePrototypes', [])
        if p in ('RMCShutterHybrisaWindow', 'RMCShutterHybrisaWindowOpen') and
        m['id'] in ('CMU3DHybrisaWindowShutter', 'CMU3DHybrisaWindowShutterOpen')}
    prison_shutter_cells = defaultdict(list)
    back_targets = {p for m in models for p in m.get('backWallMountTargets', [])}
    back_models = {p: m for m in models for p in m.get('sourcePrototypes', []) if p in back_targets}
    back_cells = defaultdict(list)
    panel_targets = {p for m in models for p in m.get('panelEndTargets', [])}
    panel_models = {p:m for m in models for p in m.get('sourcePrototypes', []) if p in panel_targets}
    panel_cells = defaultdict(list)
    opening_targets = {p for m in models for p in m.get('openingFacingTargets', [])}
    opening_cells = defaultdict(list)

    def component(uid, name):
        record = records[uid]
        return {**defaults.get(record['prototype'], {}).get(name, {}),
                **record['components'].get(name, {})}

    def grid_context(uid):
        if uid in contexts:
            return contexts[uid]
        original = uid
        visited = set()
        while uid in records and uid not in visited:
            visited.add(uid)
            if 'MapGrid' in records[uid]['components']:
                gx, gy, angle, _ = transforms.resolve(uid)
                x, y, _, _ = transforms.resolve(original)
                dx, dy = x-gx, y-gy
                c, s = math.cos(angle), math.sin(angle)
                value = uid, math.floor(round(c*dx+s*dy, 6)), math.floor(round(-s*dx+c*dy, 6)), angle
                contexts[original] = value
                return value
            parent = transforms.local(uid).get('parent', 0)
            try: uid = int(parent)
            except (TypeError, ValueError): break
        contexts[original] = None
        return None

    # Smoothing uses every anchored neighbour, even outside a cropped export and
    # even when that neighbour has no 3D model.
    for uid in records:
        prototype = records[uid]['prototype']
        if prototype in prison_shutter_models and component(uid, 'Transform').get('anchored', False):
            try: shutter_context = grid_context(uid)
            except ValueError: shutter_context = None
            if shutter_context: prison_shutter_cells[shutter_context[:3]].append(uid)
        # Named decorative showers are static map fixtures but are not anchored.
        if prototype in opening_targets:
            try: opening_context=grid_context(uid)
            except ValueError: opening_context=None
            if opening_context: opening_cells[opening_context[:3]].append(uid)
        if prototype in panel_models and component(uid, 'Transform').get('anchored', False):
            try: panel_context = grid_context(uid)
            except ValueError: panel_context = None
            if panel_context: panel_cells[panel_context[:3]].append(uid)
        if prototype in back_models and component(uid, 'Transform').get('anchored', False):
            try: back_context = grid_context(uid)
            except ValueError: back_context = None
            if back_context:
                back_cells[back_context[:3]].append(uid)
        if prototype in window_models and component(uid, 'Transform').get('anchored', False):
            try: window_context = grid_context(uid)
            except ValueError: window_context = None
            if window_context:
                window_cells[window_context[:3]].append(uid)
        if prototype in target_prototypes:
            try: target_context = grid_context(uid)
            except ValueError: target_context = None
            if target_context:
                tx, ty, _, _ = transforms.resolve(uid)
                target_positions[target_context[:3]].append((prototype, tx, ty))
        smooth = component(uid, 'IconSmooth')
        if not smooth or smooth.get('enabled') is False or not component(uid, 'Transform').get('anchored', False):
            continue
        try: context = grid_context(uid)
        except ValueError: continue
        if context:
            cells[context[:3]].append(smooth.get('key'))

    stats = Counter()
    variants = {}
    prison_variants = {}

    def prison_mount_parts(uid, model, mask, window_yaw, parts):
        # Read the whole saved source cell, never a partially visited export list.
        # The per-call plan disappears on removal/unanchoring; only immutable
        # geometry shared by a model/mask/side combination is reused.
        axis = shutter_exterior_axis('RMCWindowPrisonCell', mask)
        if (records[uid]['prototype'] != 'RMCWindowPrisonCell' or
                model['id'] != 'CMU3DPrisonCellObservationWindow' or axis is None):
            return parts, 0
        context = grid_context(uid)
        if context is None or not component(uid, 'Transform').get('anchored', False):
            return parts, 0
        wx, wy, _, _ = transforms.resolve(uid)
        ground = vector(model.get('groundOffset', [0, 0]))
        sides = 0
        for other in prison_shutter_cells[context[:3]]:
            shutter = prison_shutter_models[records[other]['prototype']]
            if 'RMCWindowPrisonCell' not in shutter.get('windowMountTargets', []): continue
            sprite = component(other, 'Sprite')
            if sprite.get('visible') is False: continue
            sx, sy, yaw, _ = transforms.resolve(other)
            yaw = (yaw+math.radians(shutter.get('yawOffset', 0)) if shutter.get('useEntityRotation') else
                render_yaw(yaw, shutter.get('sourceDirections', 1), sprite.get('noRot', False),
                    sprite.get('snapCardinals', False), shutter.get('yawOffset', 0), shutter.get('swapEastWest', False), shutter.get('sourceCardinalFacings')))
            rotation = str(sprite.get('rotation', 0)).strip()
            yaw += float(rotation[:-3]) if rotation.endswith('rad') else math.radians(float(rotation))
            own = vector(shutter.get('groundOffset', [0, 0]))
            sides |= prison_shutter_side(records[other]['prototype'], shutter['id'], axis, yaw, window_yaw,
                [sx+own[0]-wx-ground[0], sy+own[1]-wy-ground[1]], shutter.get('windowMountInside', False))
        if not sides: return parts, 0
        key = (model['id'], mask, sides)
        if key not in prison_variants:
            prison_variants[key] = prison_recess_parts(parts, axis, sides)
        recessed = prison_variants[key]
        return (recessed, sides) if recessed is not None else (parts, 0)

    def prison_wall_faces(uid, model, yaw):
        smooth = component(uid, 'IconSmooth')
        context = grid_context(uid)
        if (records[uid]['prototype'] != 'RMCWallPrisonReinforced' or model['id'] != 'CMU3DReinforcedPrisonWall' or
                not context or not component(uid, 'Transform').get('anchored', False) or
                smooth.get('enabled') is False or smooth.get('mode', 'Corners') != 'Corners' or
                'windows' not in smooth.get('additionalKeys', [])):
            return 0
        grid, x, y, grid_yaw = context
        sx, sy, _, _ = transforms.resolve(uid)
        own = vector(model.get('groundOffset', [0,0]))
        faces = 0
        for _, dx, dy, _ in CARDINALS:
            for other in window_cells[(grid,x+dx,y+dy)]:
                if records[other]['prototype'] != 'RMCWindowPrisonCell': continue
                window = window_models['RMCWindowPrisonCell']
                ws = component(other, 'IconSmooth')
                if not window.get('connectToNeighbours') or ws.get('enabled') is False or component(other,'Sprite').get('visible') is False: continue
                keys = {ws.get('key'), *ws.get('additionalKeys', [])} - {None}
                mask = sum(flag for flag, cx, cy, _ in CARDINALS if keys.intersection(cells[(grid,x+dx+cx,y+dy+cy)]))
                if not mask:
                    walls = sum(flag for flag, cx, cy, _ in CARDINALS if 'walls' in cells[(grid,x+dx+cx,y+dy+cy)])
                    if walls and not (walls & 3 and walls & 12): mask = walls
                base = connected_parts(window['parts'], mask, window.get('supportSurface'), window.get('connectionEndInset',0))
                _, sides = prison_mount_parts(other, window, mask, grid_yaw, base)
                if not sides: continue
                wx, wy, _, _ = transforms.resolve(other)
                ground = vector(window.get('groundOffset', [0,0]))
                faces |= prison_wall_join_face((wx+ground[0]-sx-own[0], wy+ground[1]-sy-own[1]), yaw, grid_yaw, mask)
        return faces

    for entity in instances:
        model = library.get(entity.get('modelId'))
        if model is None:
            continue
        uid = entity['id']
        entity.pop('_surfaceMountWalls', None)
        for field in ('geometryKey', 'connectionMask', 'layoutMask', 'layoutAlignment', 'connectionState', 'layoutOffset', 'cornerStates', 'cornerStateBase', 'windowMount', 'backWallMount', 'panelEndFit', 'openingFacingSource', 'prisonShutterSides', 'prisonJoinFaces', '_wallPaperAttached'):
            entity.pop(field, None)
        sprite = component(uid, 'Sprite')
        yaw = (entity['yaw'] + math.radians(model.get('yawOffset', 0)) if model.get('useEntityRotation') else
               render_yaw(entity['yaw'], model.get('sourceDirections', 1), sprite.get('noRot', False),
                          sprite.get('snapCardinals', False), model.get('yawOffset', 0), model.get('swapEastWest', False), model.get('sourceCardinalFacings')))
        rotation = str(sprite.get('rotation', 0)).strip()
        yaw += float(rotation[:-3]) if rotation.endswith('rad') else math.radians(float(rotation))
        smooth = component(uid, 'IconSmooth')
        context = grid_context(uid)
        if model.get('openingFacingTargets') and context and component(uid,'Transform').get('anchored',False):
            grid,x,y,grid_yaw=context
            walls=sum(flag for flag,dx,dy,_ in CARDINALS if 'walls' in cells[(grid,x+dx,y+dy)])
            choices=[]
            for other in opening_cells[context[:3]]:
                if other==uid or records[other]['prototype'] not in model['openingFacingTargets']:continue
                ox,oy,source_yaw,_=transforms.resolve(other)
                if math.hypot(ox-entity['position'][0],oy-entity['position'][1]) > .125:continue
                adjusted=opening_fixture_yaw(yaw,grid_yaw,walls,source_yaw)
                if not math.isclose(math.remainder(adjusted-yaw,math.tau),0,abs_tol=1e-5):choices.append((other,adjusted))
            if choices and all(abs(math.remainder(a-choices[0][1],math.tau)) < 1e-5 for _,a in choices):
                other,yaw=min(choices)
                entity.update(openingFacingSource=other,layoutAlignment='opening faces away from its backing wall')
                stats['openingFacingCorrections'] += 1
        if model.get('cornerSurfaces') and context and smooth.get('enabled') is not False and smooth.get('mode', 'Corners') == 'Corners' and component(uid, 'Transform').get('anchored', False):
            grid, x, y, grid_yaw = context
            keys = {smooth.get('key'), *smooth.get('additionalKeys', [])} - {None}
            mask = sum(flag for flag, dx, dy in CORNER_NEIGHBOURS if keys.intersection(cells[(grid, x+dx, y+dy)]))
            key = f"{model['id']}:corners:{mask}"
            variants.setdefault(key, corner_parts(model, mask))
            entity.update(geometryKey=key, connectionMask=mask, cornerStates=corner_states(mask), cornerStateBase=smooth.get('base', ''), layoutAlignment='source corner smoothing')
            yaw = grid_yaw
            stats['cornerSurfaceInstances'] += 1
        elif model.get('connectToNeighbours') and context and smooth.get('enabled') is not False and component(uid, 'Transform').get('anchored', False):
            grid, x, y, grid_yaw = context
            keys = {smooth.get('key'), *smooth.get('additionalKeys', [])} - {None}
            mask = 0
            for flag, dx, dy, _ in CARDINALS:
                if keys.intersection(cells[(grid, x+dx, y+dy)]):
                    mask |= flag
            layout_mask = mask
            if not mask and not model.get('supportSurface'):
                # An isolated panel in a wall opening has no same-key neighbour.
                # Infer its axis only when wall neighbours select a single axis.
                walls = sum(flag for flag, dx, dy, _ in CARDINALS if 'walls' in cells[(grid, x+dx, y+dy)])
                if walls and not (walls & 3 and walls & 12):
                    layout_mask = walls
                    entity['layoutAlignment'] = 'adjacent wall opening'
                    stats['wallAlignedInstances'] += 1
            key = f"{model['id']}:{layout_mask}"
            variants.setdefault(key, connected_parts(model['parts'], layout_mask, model.get('supportSurface'), model.get('connectionEndInset', 0)))
            recessed, sides = prison_mount_parts(uid, model, layout_mask, grid_yaw, variants[key])
            if sides:
                key += f':prison-shutter:{sides}'
                variants.setdefault(key, recessed)
                entity.update(prisonShutterSides=sides, layoutAlignment='recessed prison window and shutter assembly')
                stats['prisonWindowAssemblies'] += 1
            entity['geometryKey'] = key
            entity['connectionMask'] = mask
            entity['layoutMask'] = layout_mask
            if smooth.get('base') and not model.get('supportSurface'):
                entity['connectionState'] = f"{smooth['base']}{mask}"
            yaw = grid_yaw
            stats['connectedSurfaceInstances' if model.get('supportSurface') else 'connectedInstances'] += 1
            stats[f'connectionMask{mask}'] += 1
        elif model.get('wallMounted') and context:
            grid, x, y, grid_yaw = context
            on_wall = 'walls' in cells[(grid, x, y)]
            targets = set(model.get('wallFacingTargets', []))
            if targets:
                target_mask = 0
                for flag, dx, dy in ((0, 0, 0), *((f, dx, dy) for f, dx, dy, _ in CARDINALS)):
                    for prototype, tx, ty in target_positions[(grid, x+dx, y+dy)]:
                        if prototype in targets:
                            # Tiny offsets across a tile boundary must not turn a basin below a mirror into an east/west target.
                            target_mask |= offset_target_mask((tx-entity['position'][0], ty-entity['position'][1]), grid_yaw) or flag
                wall_mask = sum(flag for flag, dx, dy, _ in CARDINALS if 'walls' in cells[(grid, x+dx, y+dy)])
                adjusted = wall_target_yaw(yaw, grid_yaw, on_wall, target_mask, wall_mask)
                if not math.isclose(math.remainder(adjusted-yaw, math.tau), 0, abs_tol=1e-6):
                    stats['workstationDisplayFacings'] += 1
                yaw = adjusted
            dx, dy = math.sin(yaw-grid_yaw), -math.cos(yaw-grid_yaw)
            cardinal = abs(dx-round(dx)) < 1e-5 and abs(dy-round(dy)) < 1e-5
            facing_wall = cardinal and 'walls' in cells[(grid, x+round(dx), y+round(dy))]
            if cardinal and not on_wall and facing_wall:
                key = f"{model['id']}:inside-wall"
                variants.setdefault(key, inside_wall_parts(model['parts']))
                entity['geometryKey'] = key
                entity['layoutAlignment'] = 'room side of adjacent wall'
                stats['adjacentWallFixtures'] += 1
            if cardinal and (on_wall or facing_wall):
                if model.get('wallPaper'):
                    entity['_wallPaperAttached'] = True
                gx, gy, _, _ = transforms.resolve(grid)
                c, s = math.cos(grid_yaw), math.sin(grid_yaw)
                center = (gx+c*(x+.5)-s*(y+.5), gy+s*(x+.5)+c*(y+.5))
                offset = wall_mount_offset(entity['position'], center, yaw)
                if math.hypot(*offset[:2]) > 1e-6:
                    entity['layoutOffset'] = [round(v, 6) for v in offset]
                    entity.setdefault('layoutAlignment', 'wall face at tile boundary')
                    stats['wallDepthCorrections'] += 1
        elif model.get('faceAwayFromWall') and context:
            grid, x, y, grid_yaw = context
            backing = {'walls', 'windows'} if model.get('faceAwayFromWindows') else {'walls'}
            walls = sum(flag for flag, dx, dy, _ in CARDINALS if backing.intersection(cells[(grid, x+dx, y+dy)]))
            adjusted = face_away_from_wall_yaw(yaw, grid_yaw, walls)
            if not math.isclose(math.remainder(adjusted-yaw, math.tau), 0, abs_tol=1e-6):
                entity['layoutAlignment'] = 'appliance facing away from adjacent wall'
                stats['applianceWallFacings'] += 1
            yaw = adjusted
        # Co-located wall fixtures first normalize their mount pivot, then clear actual trim.
        # Adjacent-room mirrored geometry fits its opposite face only with explicit opt-in.
        room_side = entity.get('geometryKey') == f"{model['id']}:inside-wall"
        if model.get('backWallMountTargets') and context and (not room_side or model.get('fitInsideWall')):
            grid, x, y, _ = context
            base_offset = entity.get('layoutOffset', [0, 0, 0])
            mounted = []
            surface_walls = []
            for ox in range(-1, 2):
                for oy in range(-1, 2):
                    for other in back_cells[(grid,x+ox,y+oy)]:
                        if other == uid or records[other]['prototype'] not in model['backWallMountTargets']:
                            continue
                        wall = back_models[records[other]['prototype']]
                        if any(wall.get(k) for k in ('connectToNeighbours','wallMounted','windowMountTargets','backWallMountTargets','faceAwayFromWall')):
                            continue
                        wx, wy, wall_yaw, _ = transforms.resolve(other)
                        ws = component(other, 'Sprite')
                        wall_yaw = (wall_yaw+math.radians(wall.get('yawOffset',0)) if wall.get('useEntityRotation') else
                            render_yaw(wall_yaw,wall.get('sourceDirections',1),ws.get('noRot',False),ws.get('snapCardinals',False),wall.get('yawOffset',0),wall.get('swapEastWest',False),wall.get('sourceCardinalFacings')))
                        wr = str(ws.get('rotation',0)).strip()
                        wall_yaw += float(wr[:-3]) if wr.endswith('rad') else math.radians(float(wr))
                        smooth_wall = component(other, 'IconSmooth')
                        if wall.get('cornerSurfaces') and smooth_wall and smooth_wall.get('enabled') is not False and smooth_wall.get('mode', 'Corners') == 'Corners':
                            # Corner selection changes artwork only; the support solids keep their authored bounds.
                            wall_yaw = transforms.resolve(grid)[2]
                        ground, own = vector(wall.get('groundOffset',[0,0])), vector(model.get('groundOffset',[0,0]))
                        delta = [v+ground[i]-entity['position'][i]-own[i]-base_offset[i] for i,v in enumerate((wx,wy))]
                        if model.get('placement') == 'surface':
                            surface_walls.append(dict(id=other, modelId=wall['id'], yaw=wall_yaw, delta=delta))
                            continue
                        offset = back_wall_mount_offset(model['parts'],yaw,wall['parts'],wall_yaw,delta,room_side)
                        if offset is not None: mounted.append((math.hypot(*offset[:2]),other,offset))
            if mounted:
                _, other, offset = max(mounted)
                entity.update(layoutOffset=[round(v+base_offset[i],6) for i,v in enumerate(offset)],backWallMount=other,layoutAlignment='rear clearance against modeled wall')
                if room_side:
                    entity['layoutAlignment'] = 'room-side clearance against modeled wall'
                stats['backWallMountedFixtures'] += 1
            if surface_walls:
                # Consumed after all exact support surfaces have been collected.
                entity['_surfaceMountWalls'] = surface_walls
        elif model.get('windowMountTargets') and context and component(uid, 'Transform').get('anchored', False):
            mounted = []
            for other in window_cells[context[:3]]:
                if other == uid or records[other]['prototype'] not in model['windowMountTargets']:
                    continue
                window_model = window_models[records[other]['prototype']]
                exterior = shutter_exterior_target(model['id'], records[other]['prototype'])
                mask = 0
                window_smooth = component(other, 'IconSmooth')
                grid, x, y, grid_yaw = context
                wx, wy, window_yaw, _ = transforms.resolve(other)
                if window_model.get('connectToNeighbours'):
                    if window_smooth.get('enabled') is False: continue
                    keys = {window_smooth.get('key'), *window_smooth.get('additionalKeys', [])} - {None}
                    mask = sum(flag for flag, dx, dy, _ in CARDINALS if keys.intersection(cells[(grid,x+dx,y+dy)]))
                    if not mask and not window_model.get('supportSurface'):
                        walls = sum(flag for flag, dx, dy, _ in CARDINALS if 'walls' in cells[(grid,x+dx,y+dy)])
                        if walls and not (walls & 3 and walls & 12): mask = walls
                    window_parts = connected_parts(window_model['parts'], mask, window_model.get('supportSurface'), window_model.get('connectionEndInset', 0))
                    window_yaw = grid_yaw
                else:
                    ws = component(other, 'Sprite')
                    window_yaw = (window_yaw+math.radians(window_model.get('yawOffset',0)) if window_model.get('useEntityRotation') else
                        render_yaw(window_yaw,window_model.get('sourceDirections',1),ws.get('noRot',False),ws.get('snapCardinals',False),window_model.get('yawOffset',0),window_model.get('swapEastWest',False),window_model.get('sourceCardinalFacings')))
                    wr = str(ws.get('rotation',0)).strip()
                    window_yaw += float(wr[:-3]) if wr.endswith('rad') else math.radians(float(wr))
                    window_parts = window_model['parts']
                exterior_axis = None
                if records[other]['prototype'] == 'RMCWindowPrisonCell':
                    window_parts, sides = prison_mount_parts(other, window_model, mask, window_yaw, window_parts)
                    exterior_axis = shutter_exterior_axis('RMCWindowPrisonCell', mask)
                    ground, own = vector(window_model.get('groundOffset', [0,0])), vector(model.get('groundOffset', [0,0]))
                    own_side = prison_shutter_side(records[uid]['prototype'], model['id'], exterior_axis, yaw, window_yaw,
                        [entity['position'][0]+own[0]-wx-ground[0], entity['position'][1]+own[1]-wy-ground[1]], model.get('windowMountInside', False))
                    if not own_side or not sides & own_side: continue
                elif exterior:
                    exterior_axis = shutter_exterior_axis(records[other]['prototype'], mask)
                    if exterior_axis is None:
                        continue
                    if not window_model.get('connectToNeighbours'):
                        window_parts = shutter_mount_envelope(window_model, library)
                        if window_parts is None:
                            continue
                ground = vector(window_model.get('groundOffset', [0,0]))
                own_ground = vector(model.get('groundOffset', [0,0]))
                delta = [v+ground[i]-entity['position'][i]-own_ground[i] for i,v in enumerate((wx,wy))]
                offset = window_mount_offset(model['parts'], yaw, window_parts, window_yaw, delta,
                    model.get('windowMountInside',False), exterior_axis)
                if offset is not None: mounted.append((math.hypot(*offset[:2]),other,offset))
            if mounted:
                _, other, offset = max(mounted)
                entity.update(layoutOffset=[round(v,6) for v in offset], layoutAlignment='shutter mounted outside co-located glazing', windowMount=other)
                if model.get('windowMountInside'): entity['layoutAlignment'] = 'curtain mounted inside co-located glazing'
                stats['windowMountedShutters'] += 1
        faces = prison_wall_faces(uid, model, yaw)
        if faces:
            joined = prison_joined_relief_parts(variants.get(entity.get('geometryKey'), model['parts']), faces)
            if joined is not None:
                key = f"{model['id']}:prison-join:{faces}"
                variants.setdefault(key, joined)
                entity.update(geometryKey=key, prisonJoinFaces=faces, layoutAlignment='source-connected prison wall relief')
                stats['prisonWallJoins'] += 1
        if model.get('panelEndTargets') and context and component(uid, 'Transform').get('anchored',False):
            grid,x,y,grid_yaw = context
            left,right = min(vector(p['min'])[0] for p in model['parts']),max(vector(p['max'])[0] for p in model['parts'])
            obstacles = []
            for ox in range(-1,2):
                for oy in range(-1,2):
                    for other in panel_cells[(grid,x+ox,y+oy)]:
                        proto = records[other]['prototype']
                        if other == uid or proto not in model['panelEndTargets']: continue
                        obstacle = panel_models[proto]
                        if any(obstacle.get(k) for k in ('connectToNeighbours','wallMounted','faceAwayFromWall','windowMountTargets','backWallMountTargets')): continue
                        wx,wy,oyaw,_ = transforms.resolve(other);ws = component(other,'Sprite')
                        oyaw = (oyaw+math.radians(obstacle.get('yawOffset',0)) if obstacle.get('useEntityRotation') else render_yaw(oyaw,obstacle.get('sourceDirections',1),ws.get('noRot',False),ws.get('snapCardinals',False),obstacle.get('yawOffset',0),obstacle.get('swapEastWest',False),obstacle.get('sourceCardinalFacings')))
                        wr = str(ws.get('rotation',0)).strip();oyaw += float(wr[:-3]) if wr.endswith('rad') else math.radians(float(wr))
                        if obstacle.get('cornerSurfaces'): oyaw = grid_yaw
                        if obstacle.get('panelEndTargets') and (abs(math.sin(oyaw-yaw)) < .99999 or math.floor((yaw-grid_yaw)/(math.pi/2)+.5)%2 == 0): continue
                        ground,own = vector(obstacle.get('groundOffset',[0,0])),vector(model.get('groundOffset',[0,0]))
                        mounted_offset = entity.get('layoutOffset',[0,0,0])
                        delta = [v+ground[i]-entity['position'][i]-own[i]-mounted_offset[i] for i,v in enumerate((wx,wy))]
                        a,b = panel_end_limits(model['parts'],yaw,obstacle['parts'],oyaw,delta)
                        if a>left+1e-5 or b<right-1e-5: obstacles.append(other)
                        left,right = max(left,a),min(right,b)
            parts = fit_panel_ends(model['parts'],left,right)
            if parts is not model['parts']:
                key = f"{model['id']}:ends:{left:.6f}:{right:.6f}"
                variants.setdefault(key,parts)
                entity.update(geometryKey=key,panelEndFit=dict(left=round(left,6),right=round(right,6),obstacles=sorted(obstacles)),layoutAlignment='panel fitted between modeled end trim')
                stats['fittedPanelEnds'] += 1
        if not math.isclose(math.remainder(yaw-entity['yaw'], math.tau), 0, abs_tol=1e-6):
            stats['reorientedInstances'] += 1
        entity['renderYaw'] = round(yaw, 9)
    from terrain_cutouts import apply_cutouts
    stats.update(apply_cutouts(instances, library, variants, records, component, transforms, render_yaw, excluded_terrain_sources))
    return variants, dict(stats)
