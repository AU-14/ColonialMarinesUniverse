"""Explicit opaque bottom contact for static rotating source props; never move source XY."""
import math


def _point(model):
    label = model.get('supportProbePart')
    if label is None:
        return None
    states = model.get('spriteStates', {})
    state = states.get(model.get('referenceState'), {}) if isinstance(states, dict) else {}
    frames, delays, parts = state.get('frames', []), state.get('delays', []), model.get('parts', [])
    if (not isinstance(label, str) or not label.strip() or model.get('placement') != 'surface' or
            model.get('sourceDirections', 1) != 1 or model.get('sourceSpriteRotates') is not True or
            model.get('useEntityRotation') is not True or model.get('connectToNeighbours') or model.get('frameAnimations') or
            len(states) != 1 or len(frames) != 1 or len(delays) != 1 or
            type(delays[0]) not in (int, float) or not math.isfinite(delays[0]) or delays[0] <= 0 or not parts or
            frames[0].get('parts') != parts):
        raise ValueError('supportProbePart requires one static rotating surface sprite pose')
    from build_models import part_bounds, vector, rgba
    selected = [p for p in parts if p.get('label', '') == label]
    if len(selected) != 1:
        raise ValueError('supportProbePart must name one unique physical part')
    part = selected[0]
    color = rgba(part.get('color', '#FFFFFF'))
    if (part.get('shape', 'Box') != 'Box' or part.get('yaw', 0) or part.get('pitch', 0) or
            part.get('surface') is not None or part.get('omitWhenConnected', 0) or
            color[3] != 1):
        raise ValueError('supportProbePart must be an opaque unrotated untextured Box')
    for p in parts:
        low, high = vector(p['min']), vector(p['max'])
        if len(low) != 3 or len(high) != 3 or any(not math.isfinite(a) or not math.isfinite(b) or a >= b for a,b in zip(low,high)):
            raise ValueError('supportProbePart requires valid finite model geometry')
        yaw, pitch = p.get('yaw', 0), p.get('pitch', 0)
        shape, axis = p.get('shape', 'Box'), p.get('surfaceAxis', 'XZ')
        if (not math.isfinite(yaw) or abs(yaw) > 360 or not math.isfinite(pitch) or abs(pitch) > 90 or
                not 0 <= p.get('omitWhenConnected', 0) <= 15 or
                shape not in ('Box', 'Ellipsoid', 'CylinderX', 'CylinderY', 'CylinderZ', 'WedgeY', 'WedgeYReverse',
                              'SlantedX', 'SlantedXReverse', 'SlantedY', 'SlantedYReverse', 'Foliage') or
                p.get('surface') is not None and (pitch != 0 or shape not in ('Box', 'WedgeY', 'WedgeYReverse') or axis not in ('XY', 'XZ', 'YZ'))):
            raise ValueError('supportProbePart requires valid part shapes and angles')
    low, high = vector(part['min']), vector(part['max'])
    bottom = min(part_bounds(p)[0][2] for p in parts)
    if abs(low[2]-bottom) > .000001:
        raise ValueError('supportProbePart must reach the actual model bottom')
    return (low[0]+high[0])/2, (low[1]+high[1])/2


def validate(model):
    _point(model)


def probe(model):
    try:
        return _point(model)
    except (ValueError, TypeError, KeyError, IndexError, AttributeError):
        return None
