"""Bounded presentation apertures; never change collision, destinations or map tiles."""
from __future__ import annotations

import math

FIELDS = ('floorOpening', 'ceilingOpening')
MAX_OPENINGS, MAX_FRAGMENTS, EPSILON = 16, 128, 1e-5


def rect(value):
    def pair(v):
        values = v.split(',') if isinstance(v, str) else v
        if not isinstance(values, (list, tuple)) or len(values) != 2:
            raise ValueError('Opening requires two coordinates')
        return tuple(float(x) for x in values)
    if not isinstance(value, dict) or set(value) != {'min', 'max'}:
        raise ValueError('Opening requires only min/max bounds')
    result = (*pair(value['min']), *pair(value['max']))
    if (not all(math.isfinite(x) and -.5 <= x <= .5 for x in result) or
            result[0] >= result[2] or result[1] >= result[3]):
        raise ValueError('Opening must be a positive rectangle within the source tile')
    return result


def validate(model):
    result = dict(model)
    if 'preserveSlabCladding' in model and (type(model['preserveSlabCladding']) is not bool or
            'floorOpening' not in model):
        raise ValueError('preserveSlabCladding requires a boolean and a floor opening')
    for field in FIELDS:
        if field in model:
            x0, y0, x1, y1 = rect(model[field])
            result[field] = {'min': [x0, y0], 'max': [x1, y1]}
    return result


def has_opening(model):
    return any(field in model for field in FIELDS)


def transform(bounds, offset, entity_yaw, grid_yaw):
    """Return grid axes relative to tile center; reject unsupported geometry atomically."""
    values = (*offset, entity_yaw, grid_yaw)
    if not all(math.isfinite(v) for v in values):
        raise ValueError('Nonfinite opening transform')
    relative = math.remainder(entity_yaw - grid_yaw, math.tau)
    turn = relative / (math.pi / 2)
    if abs(relative - round(turn)*(math.pi/2)) > 1e-4:
        raise ValueError('Opening requires cardinal facing relative to its grid')
    x0, y0, x1, y1 = rect(bounds)
    c, s = round(math.cos(round(turn)*math.pi/2)), round(math.sin(round(turn)*math.pi/2))
    points = [(offset[0]+c*x-s*y, offset[1]+s*x+c*y) for x in (x0,x1) for y in (y0,y1)]
    result = (min(p[0] for p in points), min(p[1] for p in points),
              max(p[0] for p in points), max(p[1] for p in points))
    if any(v < -.5 or v > .5 for v in result):
        raise ValueError('Opening extends outside its source tile')
    return result


def subtract(slab, openings):
    if len(openings) > MAX_OPENINGS:
        raise ValueError('Opening count exceeds the bounded slab budget')
    def valid(r):
        return len(r) == 4 and all(math.isfinite(v) for v in r) and r[0] < r[2] and r[1] < r[3]
    if not valid(slab) or any(not valid(r) for r in openings):
        raise ValueError('Invalid slab/opening rectangle')
    fragments = [tuple(slab)]
    for cut in openings:
        next_fragments = []
        for a,b,c,d in fragments:
            x0,y0,x1,y1 = max(a,cut[0]),max(b,cut[1]),min(c,cut[2]),min(d,cut[3])
            if x0 >= x1 or y0 >= y1:
                next_fragments.append((a,b,c,d))
                continue
            for candidate in ((a,b,x0,d),(x1,b,c,d),(x0,b,x1,y0),(x0,y1,x1,d)):
                if candidate[0] < candidate[2] and candidate[1] < candidate[3]:
                    next_fragments.append(candidate)
        if len(next_fragments) > MAX_FRAGMENTS:
            raise ValueError('Fragment count exceeds the bounded slab budget')
        fragments = next_fragments
    return fragments


def apply_scene(instances, tiles, library, records, defaults, transforms, geometry_variants=None):
    """Preserve original tiles/transforms; attach clipped fragments only to matched sources."""
    if not any(has_opening(library.get(e.get('modelId'), {})) for e in instances):
        return {'applied': [], 'rejected': [], 'limitations': []}
    lookup = {}
    for tile in tiles:
        gx,gy,yaw,_ = transforms.resolve(tile['grid'])
        c,s = math.cos(yaw),math.sin(yaw)
        dx,dy = tile['x']-gx,tile['y']-gy
        lookup[tile['grid'],round(c*dx+s*dy),round(-s*dx+c*dy)] = tile
    def grid_context(entity):
        uid, visited = entity['id'], set()
        while uid in records and uid not in visited and 'MapGrid' not in records[uid]['components']:
            visited.add(uid)
            uid = int(transforms.local(uid).get('parent',0))
        if uid not in records or 'MapGrid' not in records[uid]['components']:
            raise ValueError('Opening source has no grid')
        gx,gy,grid_yaw,_ = transforms.resolve(uid)
        wx,wy,_,_ = transforms.resolve(entity['id'])
        c,s = math.cos(grid_yaw),math.sin(grid_yaw)
        x,y = c*(wx-gx)+s*(wy-gy),-s*(wx-gx)+c*(wy-gy)
        return uid,grid_yaw,x,y,math.floor(round(x,6)),math.floor(round(y,6))
    plans, rejected = [], []
    for entity in instances:
        model = library.get(entity.get('modelId'))
        if not model or not has_opening(model):
            continue
        try:
            if entity.get('matchKind') != 'exact' or 'spriteState' not in entity:
                raise ValueError('Opening needs a proven exact source appearance')
            local = transforms.local(entity['id'])
            if not local.get('anchored', False):
                raise ValueError('Opening source must be anchored')
            uid,grid_yaw,x,y,ix,iy = grid_context(entity)
            tile = lookup.get((uid,ix,iy))
            if tile is None:
                raise ValueError('Opening source tile was not exported')
            ground = model.get('groundOffset', (0,0))
            ground = tuple(float(v) for v in (ground.split(',') if isinstance(ground,str) else ground))
            if ground != (0,0) or model.get('placement','floor') != 'floor':
                raise ValueError('Opening source has an unsupported placement offset')
            cuts = {field: transform(model[field], (x-ix-.5,y-iy-.5),
                                    entity.get('renderYaw',entity['yaw']),grid_yaw)
                    for field in FIELDS if field in model}
            plans.append((entity,tile,cuts))
        except (ValueError,TypeError,KeyError) as error:
            rejected.append({'id':entity['id'],'reason':str(error)})
            entity.update(baseModelId=model['id'],modelId=None,modelStatus=None,matchKind='unmapped',unsupportedState=str(error))
    grouped = {}
    for entity,tile,cuts in plans:
        for field,cut in cuts.items():
            grouped.setdefault((id(tile),field),[tile,field,[]])[2].append((entity,cut))
    applied, operations = [], []
    # Evaluate all operations before mutating any tile: over-budget unions remain whole.
    for tile,field,sources in grouped.values():
        try:
            fragments = subtract((-.5,-.5,.5,.5),[cut for _,cut in sources])
        except ValueError as error:
            for entity,_,_ in plans:
                rejected.append({'id':entity['id'],'reason':str(error)})
                entity.update(baseModelId=entity['modelId'],modelId=None,modelStatus=None,matchKind='unmapped',unsupportedState=str(error))
            operations = []
            break
        operations.append((tile,field,sources,fragments))
    cladding_operations = []
    floor_cuts = {id(tile):(sources,fragments) for tile,field,sources,fragments in operations if field=='floorOpening'}
    if geometry_variants is not None and floor_cuts:
        from slab_cladding import clip_parts
        try:
            for entity in instances:
                if (entity.get('prototype') not in ('CMCatwalk','CMCatwalkPrison','RMCCatwalkHybrisaElevator') or
                        entity.get('matchKind') != 'exact' or entity.get('modelId') not in library or
                        not transforms.local(entity['id']).get('anchored',False)):
                    continue
                uid,grid_yaw,x,y,ix,iy = grid_context(entity)
                tile = lookup.get((uid,ix,iy))
                if tile is None or id(tile) not in floor_cuts or abs(x-ix-.5)>1e-4 or abs(y-iy-.5)>1e-4:
                    continue
                sources,_ = floor_cuts[id(tile)]
                sources = [(source, cut) for source, cut in sources
                           if not library[source['modelId']].get('preserveSlabCladding', False)]
                if not sources:
                    continue
                offset = entity.get('renderOffset',[0,0,0])
                if any(abs(v)>1e-6 for v in offset):
                    raise ValueError('Cladding aperture requires its unchanged tile-center pivot')
                model = library[entity['modelId']]
                parts = geometry_variants.get(entity.get('geometryKey'),model['parts'])
                clipped = clip_parts(parts,(x-ix-.5,y-iy-.5),entity.get('renderYaw',entity['yaw'])-grid_yaw,
                                     [cut for _,cut in sources])
                cladding_operations.append((entity,clipped,[e['id'] for e,_ in sources]))
        except (ValueError,TypeError,KeyError) as error:
            for entity,_,_ in plans:
                rejected.append({'id':entity['id'],'reason':str(error)})
                entity.update(baseModelId=entity['modelId'],modelId=None,modelStatus=None,matchKind='unmapped',unsupportedState=str(error))
            operations,cladding_operations = [],[]
    for tile,field,sources,fragments in operations:
        tile[field.replace('Opening','Fragments')] = [[v+.5 for v in r] for r in fragments]
        tile.setdefault('openingSources',[]).extend({'id':e['id'],'kind':field} for e,_ in sources)
        applied.extend({'id':e['id'],'kind':field,'bounds':list(cut),'grid':tile['grid']}
                       for e,cut in sources)
    for entity,parts,sources in cladding_operations:
        key = entity.get('geometryKey',entity['modelId'])+':floor-opening:'+','.join(map(str,sorted(sources)))+':target:'+str(entity['id'])
        geometry_variants[key] = parts
        entity.update(geometryKey=key,slabCladdingSources=sources)
    return {'applied':applied,'rejected':rejected,
            'cladding':[{'id':e['id'],'sources':sources,'parts':len(parts)} for e,parts,sources in cladding_operations],
            'limitations':['Inferred presentation apertures only; no collision or destination changes.',
                            'Only three exact thin grating prototypes are clipped; platforms and other entities are retained.',
                            'Ceiling fragments describe the native slab; offline terrain has no ceiling renderer.']}
