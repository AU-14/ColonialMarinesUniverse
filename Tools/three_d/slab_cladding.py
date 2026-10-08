"""Plain thin-cladding aperture fitting; caller proves exact source, anchoring and shared tile."""
from copy import deepcopy
import math

import slab_openings

TARGETS = frozenset(('CMCatwalk', 'CMCatwalkPrison', 'RMCCatwalkHybrisaElevator'))


def _vector(value, size):
    values = value.split(',') if isinstance(value, str) else value
    if not isinstance(values, (list, tuple)) or len(values) != size:
        raise ValueError('Invalid cladding vector')
    result = tuple(float(v) for v in values)
    if not all(math.isfinite(v) for v in result):
        raise ValueError('Nonfinite cladding vector')
    return result


def clip_parts(parts, target_offset_gridXY, relative_yaw, openings):
    """Return target-model-local parts; entity yaw/renderOffset must remain unchanged.

    Offset is the target pivot from tile center in grid XY; relative_yaw is target
    yaw minus grid yaw, in radians. The caller must also prove zero target Z offset.
    An unsupported part or aggregate fragment budget rejects the entire operation.
    Textured parts are deliberately unsupported: resizing their bounds would stretch UVs.
    """
    offset = _vector(target_offset_gridXY, 2)
    if not math.isfinite(relative_yaw):
        raise ValueError('Nonfinite cladding yaw')
    if not isinstance(parts, (list, tuple)) or not parts or len(parts) > slab_openings.MAX_FRAGMENTS:
        raise ValueError('Invalid cladding part count')
    # Validate even an empty/fully removed target against the entire opening contract.
    slab_openings.subtract((-.5, -.5, .5, .5), openings)
    c, s = math.cos(relative_yaw), math.sin(relative_yaw)
    result = []
    for part in parts:
        if part.get('shape', 'Box') != 'Box' or part.get('surface') or part.get('pitch', 0) != 0:
            raise ValueError('Cladding aperture requires untextured, untilted boxes')
        lo, hi = _vector(part['min'], 3), _vector(part['max'], 3)
        if any(a >= b for a, b in zip(lo, hi)) or lo[2] < -.1 or hi[2] > .04:
            raise ValueError('Cladding must be wholly inside Z [-.1,.04] with positive dimensions')
        yaw = float(part.get('yaw', 0))
        if not math.isfinite(yaw):
            raise ValueError('Nonfinite cladding part yaw')
        angle = math.remainder(relative_yaw + math.radians(yaw), math.tau)
        turn = round(angle / (math.pi / 2))
        if abs(angle - turn * (math.pi / 2)) > 1e-4:
            raise ValueError('Cladding must be cardinal relative to its grid')
        center = [(a+b)/2 for a, b in zip(lo, hi)]
        half = [(b-a)/2 for a, b in zip(lo, hi)]
        gx, gy = offset[0]+c*center[0]-s*center[1], offset[1]+s*center[0]+c*center[1]
        hx, hy = (half[1], half[0]) if turn % 2 else (half[0], half[1])
        slab = (gx-hx, gy-hy, gx+hx, gy+hy)
        rectangles = slab_openings.subtract(slab, openings)
        if rectangles == [slab]:
            unchanged = deepcopy(part)
            unchanged.update(min=list(lo), max=list(hi))
            result.append(unchanged)
        else:
            for x0, y0, x1, y1 in rectangles:
                dx, dy = (x0+x1)/2-offset[0], (y0+y1)/2-offset[1]
                mx, my = c*dx+s*dy, -s*dx+c*dy
                hx, hy = (x1-x0)/2, (y1-y0)/2
                fragment = deepcopy(part)
                fragment.update(min=[mx-hx, my-hy, lo[2]], max=[mx+hx, my+hy, hi[2]],
                                yaw=-math.degrees(math.remainder(relative_yaw, math.tau)))
                if not all(math.isfinite(v) for v in (*fragment['min'], *fragment['max'], fragment['yaw'])):
                    raise ValueError('Nonfinite clipped geometry')
                result.append(fragment)
        if len(result) > slab_openings.MAX_FRAGMENTS:
            raise ValueError('Cladding replacement exceeds the total part budget')
    return result
