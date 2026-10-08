"""Static source-ordered wall paper. Offsets apply after wall fitting; source transforms stay intact."""
from functools import cache
import json
import math
from pathlib import Path
import re

LIMIT, GAP, MAX_OFFSET = 16, .004, .25
ROOT = Path(__file__).resolve().parents[2]


def vector(value):
    return [float(v) for v in (value.split(',') if isinstance(value, str) else value)]


def validate(model):
    if type(model.get('wallPaper', False)) is not bool:
        raise ValueError('wallPaper must be boolean')
    if not model.get('wallPaper'):
        return model
    invalid = ('connectToNeighbours', 'faceAwayFromWall', 'directionalModels', 'spriteStates', 'windowMountTargets',
               'doorSpriteStates', 'doorButtonStates', 'poweredLightStates', 'barricadeDamageStates', 'reagentTankAppearance',
               'terrainCutoutTargets', 'floorOpening', 'ceilingOpening', 'alternateFoldModel', 'alternateDoorModel', 'panelEndTargets')
    parts = model.get('parts', [])
    if (not model.get('wallMounted') or not model.get('fitInsideWall') or not model.get('backWallMountTargets') or
            model.get('sourceDirections', 1) != 1 or vector(model.get('groundOffset', [0, 0])) != [0, 0] or
            model.get('bakedSpriteTint', '#FFFFFF').upper() not in ('#FFFFFF', '#FFFFFFFF') or
            model.get('placement', 'floor') != 'floor' or any(model.get(k) for k in invalid) or
            not 1 <= len(parts) <= 16 or not model.get('referenceRsi') or not model.get('referenceState')):
        raise ValueError('wallPaper requires an independent static thin wall-mounted model')
    for part in parts:
        if part.get('shape', 'Box') != 'Box' or any(part.get(k, 0) != 0 for k in ('yaw', 'pitch', 'omitWhenConnected')):
            raise ValueError('wallPaper supports unrotated boxes only')
    low, high = bounds(parts)
    if (high[0]-low[0] > 1 or high[2]-low[2] > 1 or high[1]-low[1] > .02 or
            low[0] < -.5 or high[0] > .5 or low[1] < -.55 or high[1] > -.45):
        raise ValueError('wallPaper bounds exceed the one-tile thin-paper contract')
    return model


def bounds(parts):
    return ([min(vector(p['min'])[i] for p in parts) for i in range(3)],
            [max(vector(p['max'])[i] for p in parts) for i in range(3)])


@cache
def source_meta(rsi):
    from build_models import resource_file
    return json.loads(resource_file('Textures/' + rsi).joinpath('meta.json').read_text())


def validate_source(model, resource_file):
    meta = json.loads(resource_file('Textures/' + model['referenceRsi']).joinpath('meta.json').read_text())
    if not valid_meta(model, meta):
        raise ValueError('wallPaper requires an actual static single-direction 32x32 RSI reference')


def valid_meta(model, meta):
    state = next((s for s in meta.get('states', []) if s['name'] == model['referenceState']), {})
    return (meta.get('size') == {'x': 32, 'y': 32} and bool(state) and state.get('directions', 1) == 1 and
            len(state.get('delays', [[1]])) == 1 and len(state.get('delays', [[1]])[0]) == 1)


@cache
def draw_depths():
    text = (ROOT / 'Content.Shared/DrawDepth/DrawDepth.cs').read_text()
    return {name: (int(n) * (-1 if op == '-' else 1) if n else 0)
            for name, op, n in re.findall(r'(\w+)\s*=\s*DrawDepthTag.Default(?:\s*([+-])\s*(\d+))?', text)}


def source_supported(model, sprite):
    from scene import radians, normalize_tint
    try:
        validate(model)
        layers = sprite.get('layers')
        layers = layers if layers else [dict(state=sprite.get('state'))]
        def transparent(color):
            tint = normalize_tint(color)
            return len(tint) == 9 and tint.endswith('00')
        layers = [l for l in layers if l.get('visible', True) and not transparent(l.get('color', '#FFFFFF'))]
        if len(layers) != 1:
            return False
        layer = layers[0]
        tint = normalize_tint(sprite.get('color', '#FFFFFF'))
        return (sprite.get('visible', True) and not sprite.get('noRot', False) and sprite.get('snapCardinals', False) and
                vector(sprite.get('offset', [0, 0])) == [0, 0] and vector(sprite.get('scale', [1, 1])) == [1, 1] and
                radians(sprite.get('rotation', 0)) == 0 and not sprite.get('granularLayersRendering') and
                not sprite.get('postShaders') and not transparent(tint) and
                not any(layer.get(k) for k in ('texture', 'shader', 'shaderPrototype', 'copyToShaderParameters', 'dirOffset')) and
                vector(layer.get('offset', [0, 0])) == [0, 0] and vector(layer.get('scale', [1, 1])) == [1, 1] and
                radians(layer.get('rotation', 0)) == 0 and normalize_tint(layer.get('color', '#FFFFFF')) == '#FFFFFF' and
                layer.get('sprite', sprite.get('sprite')) == model['referenceRsi'] and layer.get('state') == model['referenceState'] and
                valid_meta(model, source_meta(model['referenceRsi'])) and
                type(sprite.get('renderOrder', 0)) is int and 0 <= sprite.get('renderOrder', 0) <= 4294967295 and
                (type(sprite.get('drawdepth', 0)) is int or sprite.get('drawdepth') in draw_depths()))
    except (ValueError, TypeError, KeyError, OSError):
        return False


def pose(uid, model, source_position, mounted_position, yaw, inside, depth=0, order=0, grid=0):
    from layout import inside_wall_parts
    low, high = bounds(inside_wall_parts(model['parts']) if inside else model['parts'])
    front = yaw + (math.pi if inside else 0)
    c, s = math.cos(front), math.sin(front)
    x, d = c*mounted_position[0]+s*mounted_position[1], s*mounted_position[0]-c*mounted_position[1]
    return dict(id=uid, sourcePosition=source_position, drawDepth=depth, renderOrder=order, frontYaw=front, grid=grid,
                face=[x-high[0] if inside else x+low[0], low[2], x-low[0] if inside else x+high[0], high[2]],
                rear=d+(low[1] if inside else -high[1]), front=d+(high[1] if inside else -low[1]))


def overlaps(a, b):
    return (a['grid'] == b['grid'] and abs(math.sin((a['frontYaw']-b['frontYaw'])/2)) < 1e-5 and
            abs(a['rear']-b['rear']) < .025 and
            min(a['face'][2], b['face'][2])-max(a['face'][0], b['face'][0]) > 1e-5 and
            min(a['face'][3], b['face'][3])-max(a['face'][1], b['face'][1]) > 1e-5)


def offsets(papers):
    if not 1 <= len(papers) <= LIMIT or len({p['id'] for p in papers}) != len(papers):
        raise ValueError('wall paper cluster budget/identity')
    for p in papers:
        if (not all(math.isfinite(v) for v in [p['frontYaw'],p['sourcePosition'][1],p['rear'],p['front'],*p['face']]) or
                not 0 < p['front']-p['rear'] <= .02001 or p['face'][2] <= p['face'][0] or p['face'][3] <= p['face'][1]):
            raise ValueError('invalid wall paper pose')
    ordered = sorted(papers, key=lambda p: (p['drawDepth'], p['renderOrder'], -p['sourcePosition'][1], p['id']))
    distances = [0.] * len(ordered)
    for i, paper in enumerate(ordered):
        for j, other in enumerate(ordered[:i]):
            if overlaps(paper, other):
                distances[i] = max(distances[i], other['front']+distances[j]+GAP-paper['rear'])
        if not math.isfinite(distances[i]) or distances[i] > MAX_OFFSET:
            raise ValueError('wall paper depth budget')
    return {p['id']: [d*math.sin(p['frontYaw']), -d*math.cos(p['frontYaw']), 0] for p, d in zip(ordered, distances)}


def resolve_layout(instances, models, records, defaults, transforms, core, excluded=()):
    """Include exact source neighbors beyond crop, then layer only proven mounts on the same grid face."""
    from collections import Counter
    from scene import normalize_tint
    library = {m['id']: m for m in models}
    by_source = {p: m for m in models if m.get('wallPaper') for p in m.get('sourcePrototypes', [])}
    if not by_source:
        return core(instances, models, records, defaults, transforms, excluded_terrain_sources=excluded)
    selected = {e['id']: e for e in instances}
    work = list(instances)
    paper_entities, contexts, unsupported = {}, {}, set()
    def component(uid, key):
        r = records[uid]
        return {**defaults.get(r['prototype'], {}).get(key, {}), **r['components'].get(key, {})}
    def grid(uid):
        seen = set()
        while uid in records and uid not in seen:
            if 'MapGrid' in records[uid]['components']:
                return uid
            seen.add(uid)
            uid = int(transforms.local(uid).get('parent', 0))
        return None
    for uid, record in records.items():
        model = by_source.get(record['prototype'])
        if model is None or uid in excluded:
            continue
        sprite = component(uid, 'Sprite')
        tint = normalize_tint(sprite.get('color', '#FFFFFF'))
        if not sprite.get('visible', True) or len(tint) == 9 and tint.endswith('00'):
            continue
        x, y, yaw, _ = transforms.resolve(uid)
        grid_uid = grid(uid)
        e = selected.get(uid)
        if e is None:
            e = dict(id=uid, prototype=record['prototype'], modelId=model['id'], position=[x,y,0], yaw=yaw, matchKind='exact')
            work.append(e)
        e.pop('wallPaperOffset', None)
        if (e.get('modelId') != model['id'] or not source_supported(model, sprite) or grid_uid is None or
                not component(uid, 'Transform').get('anchored', False)):
            unsupported.add(uid)
        contexts[uid] = dict(model=model, sprite=sprite, grid=grid_uid, sourcePosition=[x,y])
        paper_entities[uid] = e
    variants, stats = core(work, models, records, defaults, transforms, excluded_terrain_sources=excluded)
    poses = {}
    for uid, e in paper_entities.items():
        data = contexts[uid]; model = data['model']; sprite = data['sprite']
        yaw = e.get('renderYaw', e['yaw'])
        if uid in unsupported:
            continue
        grid_yaw = transforms.resolve(data['grid'])[2]
        if abs(math.sin(2*(yaw-grid_yaw))) > 1e-5 or not e.pop('_wallPaperAttached', False):
            unsupported.add(uid)
            continue
        offset = e.get('layoutOffset', [0,0,0])
        depth = sprite.get('drawdepth', 0)
        depth = depth if type(depth) is int else draw_depths()[depth]
        poses[uid] = pose(uid, model, data['sourcePosition'], [e['position'][i]+offset[i] for i in range(2)], yaw,
                          e.get('geometryKey') == model['id']+':inside-wall', depth, int(sprite.get('renderOrder', 0)), data['grid'])
    done, changed = set(), Counter()
    for uid in poses:
        if uid in done: continue
        group, pending = [], [uid]
        while pending:
            current = pending.pop()
            if current in done: continue
            done.add(current); group.append(poses[current])
            pending.extend(other for other in poses if other not in done and overlaps(poses[current], poses[other]))
        # Mirror the native conservative unknown-neighbor guard within its two-tile query.
        bad = any(contexts[other]['grid'] == p['grid'] and
                  all(abs(contexts[other]['sourcePosition'][i]-p['sourcePosition'][i]) <= 2 for i in range(2))
                  for p in group for other in unsupported)
        try:
            if bad: raise ValueError('unsupported adjacent source paper')
            result = offsets(group)
        except ValueError:
            unsupported.update(p['id'] for p in group)
            continue
        for key, offset in result.items():
            if any(abs(v) > 1e-7 for v in offset):
                e = paper_entities[key]
                e['wallPaperOffset'] = [round(v, 6) for v in offset]
                e['layoutOffset'] = [round(v+e.get('layoutOffset', [0,0,0])[i],6) for i,v in enumerate(offset)]
                e['layoutAlignment'] = 'source-ordered separated wall paper'
                if key in selected: changed['stackedWallPapers'] += 1
    for uid in unsupported:
        e = paper_entities[uid]
        e.update(baseModelId=contexts[uid]['model']['id'], modelId=None, matchKind='unmapped',
                 unsupportedState='Unsupported wall paper source, attachment or stacking budget')
        for key in ('geometryKey', 'layoutOffset', 'wallPaperOffset', 'backWallMount', '_wallPaperAttached'):
            e.pop(key, None)
        if uid in selected: changed['unsupportedWallPapers'] += 1
    stats.update(changed)
    return variants, stats
