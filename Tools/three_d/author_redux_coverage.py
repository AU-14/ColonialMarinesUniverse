"""CMU14: source-referenced Redux round-setup and infrastructure model drafts.

Geometry is authored by object family, never one primitive per sprite pixel.
The original game continues to own visibility, connection and anchor states.
"""
from collections import Counter, defaultdict
from copy import deepcopy
from io import BytesIO
import argparse
import hashlib
import json
import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont
import yaml

import build_models as bm
import inventory
import redux_coverage
import redux_vehicle_shapes
import surfaces

ROOT = Path(__file__).resolve().parents[2]
WORLD = ROOT / 'Content.CMU/Resources/ThreeD/Prototypes/World'
MODEL = WORLD / 'garrison_redux_missing.yml'
ART = WORLD / 'garrison_redux_missing_art.yml'
TEXTURES = ROOT / 'Content.CMU/Resources/Textures/CMU14/ThreeD/ReduxCoverage'
REVIEW = ROOT / 'Tools/three_d/generated/review/redux-coverage'
DOC = ROOT / 'Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_REDUX_COVERAGE.md'


def write(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data.encode('utf-8') if isinstance(data, str) else data)


def vector(value):
    return ', '.join(f'{x:.6f}'.rstrip('0').rstrip('.') if x else '0' for x in value)


def serialize(value):
    if isinstance(value, dict):
        return {k: vector(v) if k in ('min', 'max', 'groundOffset', 'sourceSpriteOffset', 'vehicleSpriteOffset', 'vehicleSpriteScale') and isinstance(v, (list, tuple))
                else serialize(v) for k, v in value.items()}
    if isinstance(value, (tuple, list)):
        return [serialize(v) for v in value]
    return value


class Sources:
    def __init__(self):
        self.metadata = {}
        self.references = []

    def frame(self, rsi, state, direction=0, frame=0):
        path = inventory.resource_path(ROOT, inventory.texture_reference(rsi))
        meta = json.loads((path / 'meta.json').read_text(encoding='utf-8-sig'))
        self.metadata[rsi] = meta
        entry = next(s for s in meta['states'] if s['name'] == state)
        w, h = meta['size']['x'], meta['size']['y']
        sheet = Image.open(path / (state + '.png')).convert('RGBA')
        rows = entry.get('delays', [[1]] * entry.get('directions', 1))
        index = sum(len(row) for row in rows[:direction]) + frame
        x, y = index % (sheet.width // w) * w, index // (sheet.width // w) * h
        result = sheet.crop((x, y, x + w, y + h))
        self.references.append(dict(rsi=rsi, state=state, direction=direction, frame=frame,
                                    rgbaSha256=hashlib.sha256(result.tobytes()).hexdigest()))
        return result

    def compose(self, sprite):
        result = None
        # Source-reference sheets show the exposed artwork even if the entity
        # is initially hidden under a floor. Actual layer visibility still applies.
        for layer in sprite.get('layers') or [{k:v for k,v in sprite.items() if k!='visible'}]:
            if layer.get('visible') is False or not layer.get('state'):
                continue
            image = self.frame(layer.get('sprite', sprite.get('sprite')), layer['state'])
            if result is None:
                result = Image.new('RGBA', image.size)
            assert result.size == image.size
            result.alpha_composite(image)
        assert result is not None
        return result


class Pool:
    def __init__(self, art=ART, textures=TEXTURES, prefix='CMU3DReduxSurface',
                 texture_root='/Textures/CMU14/ThreeD/ReduxCoverage'):
        self.textures, self.prefix, self.texture_root = textures, prefix, texture_root
        self.entries, self.cache = [], {}
        self.previous = {}
        used = set()
        for path in WORLD.glob('*.yml'):
            if path == art:
                continue
            for entry in yaml.load(path.read_text(encoding='utf-8-sig'), Loader=yaml.CSafeLoader) or []:
                if entry['type'] == 'cmu3DSurface':
                    used.add(entry['atlasIndex'])
        # Retain unchanged crops and atlas slots when refining one model family.
        # Reassigning every later slot needlessly rewrites unrelated exports.
        if art.exists():
            for entry in yaml.safe_load(art.read_text(encoding='utf-8')):
                pixels = Image.open(textures / Path(entry['texture']).name).convert('RGBA')
                signature = hashlib.sha256(str(pixels.size).encode() + pixels.tobytes()).hexdigest()
                self.previous[signature] = entry
                used.add(entry['atlasIndex'])
        self.free = iter(i for i in range(3600, surfaces.MAX_SURFACES + 1) if i not in used)

    def crop(self, image, rect):
        pixels = image.crop(rect)
        assert pixels.getbbox(), rect
        signature = hashlib.sha256(str(pixels.size).encode() + pixels.tobytes()).hexdigest()
        if signature not in self.cache:
            if signature in self.previous:
                entry = self.previous[signature]
                self.entries.append(entry)
                self.cache[signature] = entry['id']
                return entry['id']
            index = next(self.free)
            uid = f'{self.prefix}{index}'
            stream = BytesIO()
            pixels.save(stream, format='PNG')
            write(self.textures / (uid + '.png'), stream.getvalue())
            self.entries.append(dict(type='cmu3DSurface', id=uid, atlasIndex=index,
                                     texture=f'{self.texture_root}/{uid}.png'))
            self.cache[signature] = uid
        return self.cache[signature]


def part(label, low, high, color, shape='Box', **extra):
    return dict(label=label, min=low, max=high, color=color, shape=shape, **extra)


def model(uid, refs, parts, **extra):
    return dict(type='cmu3DModel', id='CMU3DRedux' + uid, label=uid, status='draft',
                sourcePrototypes=refs, parts=parts, placement='floor', groundOffset=[0, 0],
                description='Source-referenced solid draft; inferred depth and hidden surfaces. See SOURCES_REDUX_COVERAGE.md.', **extra)


def vendor_geometry(image, pool, recessed=False):
    # Original source is an upright cabinet elevation, not a top-down footprint.
    width, height = image.size
    bounds = image.getbbox()
    left, top, right, bottom = bounds
    scale = 1 / 32
    x0, x1 = (left - width / 2) * scale, (right - width / 2) * scale
    z1 = (bottom - top) * scale * 1.7
    front, back = -.24, .23
    parts = [part('closed cabinet back', [x0, back-.05, 0], [x1, back, z1], '#3C4143'),
             part('left casing', [x0, front, 0], [x0+.035, back, z1], '#666B6B'),
             part('right casing', [x1-.035, front, 0], [x1, back, z1], '#666B6B'),
             part('closed plinth and underside', [x0, front, 0], [x1, back, .09], '#44484A'),
             part('closed roof', [x0, front, z1-.05], [x1, back, z1], '#737879')]
    # Shelves remain deep solid trays. Source stock appears on shallow separate
    # shelf volumes, so oblique views retain gaps and the cabinet's interior.
    cuts = [top, min(top+5, bottom), min(top+12, bottom), bottom-3, bottom]
    if recessed:
        cuts = [top, min(top+13, bottom), min(top+20, bottom), bottom-3, bottom]
    cuts = sorted(set(cuts))
    for i, (a, b) in enumerate(zip(cuts, cuts[1:])):
        z0, ztop = (bottom-b)*scale*1.7, (bottom-a)*scale*1.7
        y = front-.004 if i in (0, len(cuts)-2) else -.12
        rect = (left, a, right, b)
        parts.append(part('source cabinet panel '+str(i), [x0, y, z0], [x1, y+.025, ztop], '#FFFFFF',
                          surface=pool.crop(image, rect), surfaceAxis='XZ'))
        if i > 0:
            parts.append(part('shelf tray '+str(i), [x0+.03, front, max(.04,z0)],
                              [x1-.03, back-.05, max(.04,z0)+.025], '#676D6E'))
    for side in (-1, 1):
        x = x0 if side == -1 else x1-.035
        parts.append(part('front upright', [x, front-.008, .08], [x+.035, front+.015, z1-.04], '#8B9090'))
    return parts


def vendors(rows, kinds, sources, pool):
    relevant = [r for r in rows if any(o.startswith('platoon vendor') for o in r['origins']) and not r['models']]
    keys = ('Sprite', 'Appearance', 'GenericVisualizer', 'VendingMachineVisuals', 'AnimationPlayer', 'LitOnPowered', 'DamageVisuals')
    def signature(components):
        return json.dumps({key: components.get(key) for key in keys}, sort_keys=True)
    resolver = inventory.Resolver(kinds['entity'])
    donors = {}
    for entry in sorted(kinds['cmu3DModel'].values(), key=lambda m:m['id']):
        for uid in entry.get('sourcePrototypes', []):
            try:
                components = inventory.component_map(resolver.resolve(uid))
            except inventory.ResolutionError:
                continue
            if 'CMAutomatedVendor' in components:
                donors.setdefault(signature(components), entry)
    groups = defaultdict(list)
    for row in relevant:
        groups[signature(row['components'])].append(row)
    result, images = [], {}
    for group in sorted(groups.values(), key=lambda g:g[0]['id']):
        first = group[0]
        if signature(first['components']) in donors:
            entry = deepcopy(donors[signature(first['components'])])
            entry.pop('_source', None)
            entry.update(id='CMU3DRedux' + first['id'], sourcePrototypes=[r['id'] for r in group])
        else:
            image = sources.compose(first['sprite'])
            parts = vendor_geometry(image, pool, 'requisitions_wall' in first['sprite']['sprite'])
            entry = model(first['id'], [r['id'] for r in group], parts, useEntityRotation=True,
                          referencePrototype=first['id'])
        result.append(entry)
        images[entry['id']] = sources.compose(first['sprite'])
    return result, images


def cable_geometry(image, mask, heavy, pool):
    cx, cy = (0, 0) if heavy else (-.1875, .1875)
    radius = .12 if heavy else .029
    z = radius + .006
    color = '#393D40' if heavy else '#B7B7B7'
    parts = []
    # Rounded central junction with four independently connected cable arms.
    parts.append(part('insulated cable junction', [cx-radius,cy-radius,.006],
                      [cx+radius,cy+radius,2*radius+.006], color, 'Box' if heavy else 'Ellipsoid'))
    for flag, axis, end in ((1,'Y',.5),(2,'Y',-.5),(4,'X',.5),(8,'X',-.5)):
        if not mask & flag:
            continue
        if axis == 'Y':
            low, high = [cx-radius,min(cy,end),z-radius], [cx+radius,max(cy,end),z+radius]
        else:
            low, high = [min(cx,end),cy-radius,z-radius], [max(cx,end),cy+radius,z+radius]
        parts.append(part('connected '+str(flag), low, high, color, 'Box' if heavy else 'Cylinder'+axis))
    # One crown print retains the original route, highlights and junction colors.
    # The round closed cores supply sides and underside without pixel geometry.
    x0,y0,x1,y1=image.getbbox()
    if heavy:
        # Cable sprites describe a rectangular armored conduit. Its printed cover
        # sits on the casing, with no floating decal above a sphere.
        for shape in parts:
            shape['max'][2]=.08
        if mask==0:
            parts[0]['min']=[(x0-16)/32,(16-y1)/32,.006]
            parts[0]['max']=[(x1-16)/32,(16-y0)/32,.08]
        parts.append(part('source conduit cover', [(x0-16)/32,(16-y1)/32,.079],
                          [(x1-16)/32,(16-y0)/32,.081], '#FFFFFF',
                          surface=pool.crop(image,(x0,y0,x1,y1)),surfaceAxis='XY'))
    return parts


def cables(rows, sources, pool):
    result, images = [], {}
    for row in rows:
        if row['id'] not in ('CableApcExtension','RMCCableHeavy'):
            continue
        rsi=row['sprite']['sprite'];prefix=row['components']['CableVisualizer']['statePrefix']
        poses={}
        for mask in range(16):
            state=prefix+str(mask);image=sources.frame(rsi,state)
            poses[state]=dict(frames=[dict(parts=cable_geometry(image,mask,row['id']=='RMCCableHeavy',pool))],delays=[1])
        entry=model(row['id'],[row['id']],deepcopy(poses[prefix+'0']['frames'][0]['parts']),
                    referencePrototype=row['id'],referenceRsi=rsi,referenceState=prefix+'0',
                    sourceSpriteOffset=[0,0],sourceSpriteRotates=True,sourceDirections=1,
                    useEntityRotation=True,spriteStates=poses)
        result.append(entry);images[entry['id']]=sources.frame(rsi,prefix+'0')
    return result,images


PIPE_KINDS = {'DisposalPipe':'s','DisposalBend':'c','DisposalRouter':'j1s',
              'DisposalRouterFlipped':'j2s','DisposalTrunk':'t','DisposalYJunction':'y'}


def pipe_geometry(kind, anchored, image, pool):
    radius=.25 if anchored else .225
    center=-radius-.02 if anchored else radius
    end=.5 if anchored else .4375
    parts=[]
    def arm(axis,a,b,label):
        low,high=([-radius,a,center-radius],[radius,b,center+radius]) if axis=='Y' else ([a,-radius,center-radius],[b,radius,center+radius])
        parts.append(part(label,low,high,'#545757','Cylinder'+axis))
    exits={'s':'NS','c':'SW','j1s':'NSW','j2s':'NSE','t':'S','y':'SWE'}[kind]
    for direction in exits:
        axis='Y' if direction in 'NS' else 'X';sign=1 if direction in 'NE' else -1
        arm(axis,min(0,sign*end),max(0,sign*end),'round '+direction+' barrel')
        a,b=sorted([sign*(end-.07),sign*end])
        low,high=([-radius-.018,a,center-radius-.018],[radius+.018,b,center+radius+.018]) if axis=='Y' else ([a,-radius-.018,center-radius-.018],[b,radius+.018,center+radius+.018])
        parts.append(part(direction+' coupling',low,high,'#747777','Cylinder'+axis))
    parts.append(part('curved closed junction',[-radius,-radius,center-radius],[radius,radius,center+radius],'#656868','Ellipsoid'))
    if kind=='t':
        parts.append(part('upright trunk throat',[-radius,-radius,center],[radius,radius,center+radius],'#555959','CylinderZ'))
        parts.append(part('dark throat recess',[-radius*.78,-radius*.78,center+radius-.02],
                          [radius*.78,radius*.78,center+radius+.001],'#171C1D','CylinderZ'))
    x0,y0,x1,y1=image.getbbox()
    # Retain the routing arrow as a small physical label, not a flat full-pipe
    # picture protruding over the round castings at oblique angles.
    colored=[(x,y) for y in range(image.height) for x in range(image.width)
             if (lambda c:c[3]>0 and c[0]>c[2]*1.3 and c[1]>c[2]*1.2 and c[0]>45)(image.getpixel((x,y)))]
    if colored:
        ax0,ay0=min(x for x,y in colored),min(y for x,y in colored)
        ax1,ay1=max(x for x,y in colored)+1,max(y for x,y in colored)+1
        parts.append(part('source routing plate',[(ax0-16)/32,(16-ay1)/32,center+radius-.015],
                          [(ax1-16)/32,(16-ay0)/32,center+radius-.001],'#FFFFFF',
                          surface=pool.crop(image,(ax0,ay0,ax1,ay1)),surfaceAxis='XY'))
    opening=None
    if anchored:
        left,bottom,right,top=(x0-16)/32,(16-y1)/32,(x1-16)/32,(16-y0)/32
        # Finite service trough; intact source cladding is retained by the adapter.
        opening=dict(min=[left,bottom],max=[right,top])
        parts.append(part('finite channel bottom',[left,bottom,-.61],[right,top,-.58],'#242A2C'))
    return parts,opening


def pipes(sources,pool):
    result,images=[],{}
    rsi='Structures/Piping/disposal.rsi'
    for uid,kind in PIPE_KINDS.items():
        for anchored in (True,False):
            state=('pipe-' if anchored else 'conpipe-')+kind
            image=sources.frame(rsi,state);parts,opening=pipe_geometry(kind,anchored,image,pool)
            extra=dict(floorOpening=opening,preserveSlabCladding=True) if anchored else dict(sourceSpriteRotates=True)
            entry=model(uid+('' if anchored else 'Loose'),[uid] if anchored else [],parts,
                        referencePrototype=uid,referenceRsi=rsi,referenceState=state,
                        referenceDirection=0,sourceDirections=1,sourceSpriteOffset=[0,0],
                        useEntityRotation=True,anchored=anchored,
                        alternateAnchorModel='CMU3DRedux'+uid+('Loose' if anchored else ''),
                        spriteStates={state:dict(frames=[dict(parts=deepcopy(parts))],delays=[1])},**extra)
            result.append(entry);images[entry['id']]=image
    return result,images


def dominant(image):
    colors=Counter(c[:3] for c in image.get_flattened_data() if c[3]>200 and sum(c[:3])>120)
    return '#'+''.join(f'{c:02X}' for c in (colors.most_common(1)[0][0] if colors else (81,91,83)))


def silhouette_sections(image, pool, bottom, top, scale=1, sections=8, label='casting'):
    """A handful of closed longitudinal castings, not an extruded pixel mesh.

    Keep disconnected mounts separate and leave the transparent space between
    them empty. This matters for aircraft wings, dozer arms and flame nozzles.
    """
    w,h=image.size;x0,y0,x1,y1=image.getbbox();result=[]
    for band in range(sections):
        a=y0+(y1-y0)*band//sections;b=y0+(y1-y0)*(band+1)//sections
        if b<=a:continue
        occupied=[x for x in range(x0,x1) if any(image.getpixel((x,y))[3]>128 for y in range(a,b))]
        runs=[]
        for x in occupied:
            if runs and x-runs[-1][1]<=2:runs[-1][1]=x+1
            else:runs.append([x,x+1])
        for left,right in runs:
            rect=(left,a,right,b);lo=[(left-w/2)/32*scale,(h/2-b)/32*scale,bottom];hi=[(right-w/2)/32*scale,(h/2-a)/32*scale,top]
            result.append(part(label+' section',lo,hi,dominant(image.crop(rect))))
    # A shared alpha-cut print sits directly on the segmented solid crown.
    # Avoid consuming an atlas slot for every small longitudinal partition.
    for xa in range(x0,x1,240):
        for ya in range(y0,y1,240):
            xb,yb=min(x1,xa+240),min(y1,ya+240);rect=(xa,ya,xb,yb)
            if not image.crop(rect).getbbox():continue
            result.append(part(label+' original finish',[(xa-w/2)/32*scale,(h/2-yb)/32*scale,top-.001],
                               [(xb-w/2)/32*scale,(h/2-ya)/32*scale,top+.002],
                               '#FFFFFF',surface=pool.crop(image,rect),surfaceAxis='XY'))
    return result


def vehicle_geometry(image, state, rsi, pool, scale=1, body=False, frame=None):
    bounds=image.getbbox()
    if bounds is None:
        return []
    rebuilt = redux_vehicle_shapes.rebuild(image, state, rsi, pool, body, frame)
    if rebuilt is not None:
        if scale != 1:
            for piece in rebuilt:
                for key in ('min', 'max'):
                    piece[key] = [v * scale for v in piece[key]]
        return rebuilt
    w,h=image.size;x0,y0,x1,y1=bounds
    left,right=(x0-w/2)/32*scale,(x1-w/2)/32*scale
    rear,front=(h/2-y0)/32*scale,(h/2-y1)/32*scale
    width,length=right-left,rear-front
    color=dominant(image);parts=[]
    tracked='tank' in rsi or 'fv150' in rsi
    aircraft='Blackfoot' in rsi or 'Fighter' in rsi
    ztop=1.2 if tracked else 1.5
    if aircraft:ztop=1.3
    if body and aircraft:
        # Match the narrow fuselage and separated wing/engine silhouette. A
        # full bounding-box chassis would fill most of the aircraft's empty air.
        parts=silhouette_sections(image,pool,.72,1.08,scale,sections=8,label='airframe')
        mid=(left+right)/2
        parts.append(part('rounded fuselage keel',[mid-width*.095,front+length*.05,.43],
                          [mid+width*.095,rear-length*.1,1.16],color,'CylinderY'))
        parts.append(part('cockpit canopy',[mid-width*.07,front+length*.17,1.07],
                          [mid+width*.07,front+length*.34,1.28],'#28464E','Ellipsoid'))
        for side in (-1,1):
            x=mid+side*width*.09
            parts.append(part('landing gear strut',[x-.025,front+length*.42,.12],[x+.025,front+length*.42+.12,.75],'#454A4C'))
            parts.append(part('landing skid',[x-.05,front+length*.35,.03],[x+.05,rear-length*.25,.13],'#303A3B'))
        return parts
    if any(word in state for word in ('lights','damage','shadow','thrust','downwash','fan-overlay')):
        # These RSI layers contain isolated lights, scorch marks or translucent
        # effects. Giving their whole bounding box an opaque casing would cover
        # the hull (and bridge empty air between disconnected navigation lights).
        z=.018 if 'shadow' in state or 'downwash' in state else 1.085 if aircraft else ztop+.015
        for xa in range(x0,x1,240):
            for ya in range(y0,y1,240):
                xb,yb=min(x1,xa+240),min(y1,ya+240);rect=(xa,ya,xb,yb)
                if not image.crop(rect).getbbox():continue
                parts.append(part('source surface effect',[(xa-w/2)/32*scale,(h/2-yb)/32*scale,z],
                                  [(xb-w/2)/32*scale,(h/2-ya)/32*scale,z+.002],'#FFFFFF',
                                  surface=pool.crop(image,rect),surfaceAxis='XY'))
        return parts
    if body:
        # Chassis is a closed structure; its underside and cabin remain visible from all sides.
        parts.append(part('closed lower chassis',[left+.07,front+.09,.3],[right-.07,rear-.09,.72],color))
        parts.append(part('upper hull',[left+.12,front+.17,.7],[right-.12,rear-.16,ztop],color))
        if not tracked and not aircraft:
            cabin_back=front+length*.55
            parts.append(part('cab windscreen',[left+.17,front+.16,1.03],[right-.17,front+.19,ztop-.08],'#253C42'))
            for x in (left+.1,right-.12):
                parts.append(part('cab side window',[x,front+.22,1.02],[x+.02,cabin_back,ztop-.09],'#30484E'))
            if 'truck' in state:
                # Open cargo bed is lower than the cab, with a closed deck and tailgate.
                parts[1]['max'][1]=cabin_back
                parts.append(part('cargo deck',[left+.12,cabin_back,.73],[right-.12,rear-.12,.83],color))
                for x in (left+.08,right-.13):
                    parts.append(part('cargo side wall',[x,cabin_back,.8],[x+.05,rear-.1,1.12],color))
                parts.append(part('tailgate',[left+.08,rear-.15,.8],[right-.08,rear-.1,1.12],color))
        if aircraft:
            for side in (-1,1):
                x=left if side<0 else right-.3
                parts.append(part('engine nacelle',[x,front+length*.22,.45],[x+.3,rear-length*.12,1.1],color,'CylinderY'))
                parts.append(part('landing strut',[x+.1,front+length*.35,.08],[x+.2,front+length*.35+.2,.55],'#454A4C'))
            parts.append(part('cockpit canopy',[left+width*.35,front+length*.06,ztop],
                              [right-width*.35,front+length*.28,ztop+.2],'#28464E','Ellipsoid'))
        for x in (left+.12,right-.26):
            parts.append(part('front light',[x,front+.045,.65],[x+.14,front+.07,.78],'#D7D6AC'))
            parts.append(part('rear marker',[x,rear-.07,.65],[x+.14,rear-.045,.77],'#6E2626'))
        parts.append(part('front bumper',[left+.04,front,.35],[right-.04,front+.065,.47],'#353C3D'))
        parts.append(part('rear bumper',[left+.04,rear-.065,.35],[right-.04,rear,.47],'#353C3D'))
        # Roof print is partitioned at major source panels; no per-pixel cubes.
        cuts={y0,y0+(y1-y0)//3,y0+2*(y1-y0)//3,y1}
        if 'truck' in state:
            cuts.add(round(h/2-cabin_back/scale*32))
        cuts=sorted(cuts)
        for a,b in zip(cuts,cuts[1:]):
            crop=(x0,a,x1,b)
            if not image.crop(crop).getbbox():continue
            for xa in range(x0,x1,240):
                xb=min(x1,xa+240);rect=(xa,a,xb,b)
                if not image.crop(rect).getbbox():continue
                panel_z=.835 if 'truck' in state and (h/2-(a+b)/2)/32*scale>cabin_back else ztop
                parts.append(part('source hull roof panel',[(xa-w/2)/32*scale,(h/2-b)/32*scale,panel_z],
                                  [(xb-w/2)/32*scale,(h/2-a)/32*scale,panel_z+.012],'#FFFFFF',
                                  surface=pool.crop(image,rect),surfaceAxis='XY'))
        return parts
    if state.startswith('wheels_'):
        for side in (-1,1):
            x=left if side<0 else right-.2
            if tracked:
                parts.append(part('continuous track',[x,front,.04],[x+.2,rear,.49],'#292D2E'))
                for i in range(5):
                    y=front+(i+.5)*length/5
                    parts.append(part('track road wheel',[x-.01,y-.16,.08],[x+.21,y+.16,.4],'#494F4C','CylinderX'))
            else:
                for fraction in (.2,.8):
                    y=front+length*fraction
                    parts.append(part('road tire',[x,y-.24,.02],[x+.2,y+.24,.5],'#25292B','CylinderX'))
                    parts.append(part('wheel hub',[x-.004,y-.12,.14],[x+.204,y+.12,.38],'#686D6B','CylinderX'))
        return parts
    turret='turret' in state or 'cupola' in state
    weapon=any(word in state for word in ('cannon','gun','flamer','launcher','p17702','t60p3m','railgun','electrolaser'))
    z=ztop+(.15 if turret else .38 if weapon else .025)
    if turret or weapon or any(word in state for word in ('snowplow','hj35','t60p3m','mountedflag')):
        if 'snowplow' in state:
            return silhouette_sections(image,pool,.12,.45,scale,sections=5,label='dozer structure')
        parts=silhouette_sections(image,pool,z,z+(.32 if turret else .15),scale,sections=5,label='turret' if turret else 'weapon')
        if turret:
            parts.append(part('turret bearing',[left+width*.27,front+length*.3,ztop-.07],
                              [right-width*.27,rear-length*.3,ztop+.16],color,'CylinderZ'))
        return parts
    if 'downwash' in state or 'shadow' in state or 'thrust' in state:
        # Atmospheric artwork remains a flat alpha projection at its physical plane.
        z=.015 if 'thrust' not in state else .35
    else:
        parts.append(part('installed equipment casing',[left,front,z],[right,rear,z+.12],color))
    z+=.125
    for xa in range(x0,x1,240):
        for ya in range(y0,y1,240):
            xb,yb=min(x1,xa+240),min(y1,ya+240);rect=(xa,ya,xb,yb)
            if not image.crop(rect).getbbox():continue
            parts.append(part('source equipment markings',[(xa-w/2)/32*scale,(h/2-yb)/32*scale,z],
                              [(xb-w/2)/32*scale,(h/2-ya)/32*scale,z+.006],'#FFFFFF',
                              surface=pool.crop(image,rect),surfaceAxis='XY'))
    return parts


def vehicles(rows,kinds,sources,pool):
    result,images=[],{}
    previous = yaml.safe_load(MODEL.read_text(encoding='utf-8')) if MODEL.exists() else []
    references = {m.get('referencePrototype') for m in previous if m.get('vehicleLayers') and m.get('sourcePrototypes')}
    groups=defaultdict(list)
    for row in rows:
        if 'GridVehicleMover' in row['components']:
            groups[json.dumps(row['sprite'],sort_keys=True)].append(row)
    for group in groups.values():
        # Keep published asset IDs when another prototype shares an existing skin.
        row=next((r for r in group if r['id'] in references), group[0]);sprite=row['sprite'];rsi=sprite['sprite']
        base=(sprite.get('layers') or [sprite])[0]
        scale=float(str(sprite.get('scale','1, 1')).split(',')[0])
        base_rsi=base.get('sprite',rsi);base_state=base['state']
        all_rsis={rsi,base_rsi}
        all_rsis.update(layer['sprite'] for layer in sprite.get('layers',[]) if layer.get('sprite'))
        layers=[];default=[]
        for source in sorted(all_rsis):
            folder=inventory.resource_path(ROOT,inventory.texture_reference(source));meta=json.loads((folder/'meta.json').read_text(encoding='utf-8-sig'))
            for entry in meta['states']:
                state=entry['name']
                if entry.get('directions',1)==1 and state.isupper():continue
                image=sources.frame(source,state)
                is_body=(source==base_rsi and state==base_state) or source==rsi and state in ('stowed','flight','vtol','folded','jetfighter','vtolmode','cargo_closed','cargo_open')
                # Other chassis skins in this shared RSI are not runtime hardpoints.
                if ('base' in state or state=='hull_wy') and not is_body:continue
                if not is_body and (state in ('sppvan_medical','sppvan_prisoner','civtruck_1','civtruck_2','civtruck_3') or state.startswith('cargo_debris')):continue
                # Some stationary wheel overlays are blank because the 2D base
                # already contains tires. The physical wheel volume still exists.
                wheel_image=sources.frame(base_rsi,base_state) if state.startswith('wheels_') else image
                parts=vehicle_geometry(wheel_image,state,source,pool,scale,is_body,
                                       lambda pose: sources.frame(source,pose) if pose else sources.frame(base_rsi,base_state))
                layers.append(dict(rsi=source,state=state,parts=parts))
                if source==base_rsi and state==base_state or source==rsi and state=='wheels_1':default.extend(deepcopy(parts))
        entry=model(row['id'],[r['id'] for r in group],default,referencePrototype=row['id'],
                    referenceRsi=base_rsi,referenceState=base_state,
                    useEntityRotation=True,vehicleLayers=layers,
                    vehicleSpriteScale=[scale,scale],vehicleSpriteOffset=[float(v) for v in str(sprite.get('offset','0, 0')).split(',')])
        result.append(entry);images[entry['id']]=sources.compose(sprite)
    resolver=inventory.Resolver(kinds['entity']);turrets=defaultdict(list)
    for uid,raw in kinds['entity'].items():
        if raw.get('abstract'):continue
        try:components=inventory.component_map(resolver.resolve(uid))
        except inventory.ResolutionError:continue
        turret=components.get('VehicleTurret',{})
        if turret.get('showOverlay') and turret.get('overlayRsi') and turret.get('overlayState'):
            turrets[(turret['overlayRsi'],turret['overlayState'])].append((uid,turret))
    for (rsi,state),group in sorted(turrets.items()):
        states={state}
        for _,turret in group:
            if turret.get('overlayDamagedState'):states.add(turret['overlayDamagedState'])
        layers=[]
        for pose in sorted(states):
            image=sources.frame(rsi,pose);parts=vehicle_geometry(image,pose,rsi,pool,frame=lambda pose: sources.frame(rsi,pose))
            layers.append(dict(rsi=rsi,state=pose,parts=parts))
        uid=group[0][0]+'Mounted'
        entry=model(uid,[],deepcopy(next(l['parts'] for l in layers if l['state']==state)),
                    referencePrototype=group[0][0],referenceRsi=rsi,referenceState=state,
                    useEntityRotation=True,vehicleTurretPrototypes=[item[0] for item in group],vehicleLayers=layers)
        result.append(entry);images[entry['id']]=sources.frame(rsi,state)
    return result,images


def props(rows,kinds,sources,pool):
    result,images=[],{}
    resolver=inventory.Resolver(kinds['entity'])
    library=kinds['cmu3DModel'];donors=defaultdict(list)
    for entry in sorted(library.values(),key=lambda m:m['id']):
        for uid in entry.get('sourcePrototypes',[]):
            try:sprite=inventory.component_map(resolver.resolve(uid)).get('Sprite')
            except inventory.ResolutionError:continue
            if sprite:donors[json.dumps(sprite,sort_keys=True)].append(entry)
    by_signature=defaultdict(list)
    for row in rows:
        if (row['models'] or row['conditional'] or row['native'] or
                'GridVehicleMover' in row['components'] or
                any(o.startswith('platoon') for o in row['origins']) or
                row['id'] in PIPE_KINDS or row['id'] in ('RMCCableHeavy','CableApcExtension')):continue
        by_signature[json.dumps(row['sprite'],sort_keys=True)].append(row)
    for signature,group in by_signature.items():
        row=group[0];uid=row['id'];sprite=row['sprite']
        if donors[signature]:
            original=donors[signature][0];pending=[original['id']];visited=set();copies=[]
            def renamed(value):return 'CMU3DRedux'+uid+value.removeprefix('CMU3D')
            while pending:
                original_id=pending.pop()
                if original_id in visited:continue
                visited.add(original_id);entry=deepcopy(library[original_id]);entry.pop('_source',None)
                entry['id']=renamed(original_id);entry['sourcePrototypes']=[r['id'] for r in group] if original_id==original['id'] else []
                entry['referencePrototype']=uid
                for key in ('directionalModels','alternateDoorModel','alternateFoldModel'):
                    if not entry.get(key):continue
                    links=entry[key] if isinstance(entry[key],list) else [entry[key]]
                    pending.extend(links);entry[key]=[renamed(link) for link in links] if isinstance(entry[key],list) else renamed(entry[key])
                copies.append(entry)
            result.extend(copies)
            images[renamed(original['id'])]=sources.compose(sprite)
            continue
        rsi=sprite.get('sprite') or sprite['layers'][0]['sprite']
        state=sprite.get('state') or (sprite.get('layers') or [{}])[0].get('state')
        if not state:
            state='wall0' if uid=='RMCWallElevator' else None
        image=sources.frame(rsi,state)
        w,h=image.size;x0,y0,x1,y1=image.getbbox();width=(x1-x0)/32;length=(y1-y0)/32
        color=dominant(image);p=[]
        upright=False;top=.14
        if 'Elevator' in uid or uid=='VehicleLift':
            if 'DoorBroken' in uid:
                upright=True;top=2.4
                p=[part('closed steel door backing',[-width/2,-.12,0],[width/2,.12,top],color)]
                for x in (-width/2,width/2-.06):p.append(part('door frame',[x,-.2,0],[x+.06,.18,top+.05],'#4A4E50'))
            elif 'Wall' in uid:
                upright=True;top=2.5
                p=[part('elevator shaft wall',[-.5,-.5,0],[.5,.5,top],color)]
                for x in (-.42,.34):p.append(part('lift guide rail',[x,-.54,.05],[x+.08,-.5,top],'#303437'))
            else:
                top=.06
                p=[part('closed lift platform',[-width/2,-length/2,0],[width/2,length/2,top],'#323B3D')]
                for x in (-width/2,width/2-.04):p.append(part('lift edge rail',[x,-length/2,.06],[x+.04,length/2,.13],'#5C6364'))
        elif 'Ladder' in uid or 'Hatch' in uid:
            hatch='Hatch' in uid
            if hatch:
                top=.075
                for x in (-width/2,width/2-.04):p.append(part('hatch frame',[x,-length/2,0],[x+.04,length/2,top],'#696F70'))
                for y in (-length/2,length/2-.04):p.append(part('hatch cross frame',[-width/2,y,0],[width/2,y+.04,top],'#696F70'))
                p.append(part('hatch plate',[-width/2+.04,-length/2+.04,.025],[width/2-.04,length/2-.04,.055],'#454C4D'))
            else:
                upright=True;top=2.3
                for x in (-.27,.23):p.append(part('ladder rail',[x,-.07,0],[x+.04,.07,top],'#656C70'))
                for index in range(8):
                    z=.16+index*.28;p.append(part('round ladder rung',[-.23,-.06,z],[.23,.06,z+.07],'#686F71','CylinderX'))
        elif 'ConstructionFrame' in uid or uid=='RMCWindowFrameStrata':
            upright=True;top=2.1
            for x in (-.45,.38):p.append(part('structural upright',[x,-.12,0],[x+.07,.12,top],color))
            for z in (.03,.8,1.85):p.append(part('structural crossbar',[-.45,-.12,z],[.45,.12,z+.07],color))
        elif uid=='RMCDrill':
            upright=True;top=2.4
            p=[part('drill base',[-.24,-.25,0],[.24,.25,.22],color),
               part('upright column',[-.1,-.05,.2],[.12,.13,top],color),
               part('drill motor',[-.24,-.16,1.45],[.25,.19,2.15],color),
               part('bore shaft',[-.06,-.24,.25],[.06,-.12,1.5],'#7A8080','CylinderZ')]
        elif uid=='CMUCanisterOxygen':
            upright=True;top=1.25
            p=[part('pressure cylinder',[-.26,-.26,.05],[.26,.26,1.12],'#377CC0','CylinderZ'),
               part('rounded shoulder',[-.25,-.25,.94],[.25,.25,1.22],'#387ABC','Ellipsoid'),
               part('valve housing',[-.1,-.1,1.1],[.1,.1,top],'#333D42'),
               part('cylinder foot',[-.27,-.27,0],[.27,.27,.09],'#333B44','CylinderZ')]
            for z in (.3,.85):p.append(part('reinforcing hoop',[-.266,-.266,z],[.266,.266,z+.04],'#5D6878','CylinderZ'))
        elif 'Tripod' in uid:
            upright=True;top=1.7
            p=[part('lamp mast',[-.035,-.035,.13],[.035,.035,1.5],'#777B7A'),
               part('lamp head',[-.2,-.14,1.42],[.2,.14,top],'#626A6C'),
               part('lamp lens',[-.16,-.15,1.47],[.16,-.14,1.64],'#C5D0D1')]
            for yaw in (0,120,240):p.append(part('tripod foot',[-.025,-.34,0],[.025,.06,.08],'#4E5555',yaw=yaw))
        elif 'Handrail' in uid:
            upright=True;top=1.05
            for x in (-.43,.35):p.append(part('rail post',[x,-.06,0],[x+.08,.06,top],color))
            for z in (.3,.87):p.append(part('rail crossbar',[-.43,-.055,z],[.43,.055,z+.075],color))
        elif 'Hardhat' in uid:
            top=.28
            p=[part('helmet dome',[-.24,-.25,.025],[.24,.25,top],'#C1C0AD','Ellipsoid'),
               part('helmet brim',[-.29,-.3,.02],[.29,.3,.045],'#848B85','CylinderZ'),
               part('headlamp',[-.07,-.31,.14],[.07,-.24,.24],'#E2DDAB')]
        elif 'Cable' in uid:
            top=.055
            for i in range(12):
                angle=i*math.tau/12;cx=math.cos(angle)*.17;cy=math.sin(angle)*.17
                p.append(part('coiled insulation',[cx-.06,cy-.022,0],[cx+.06,cy+.022,top],'#205C22','CylinderX',yaw=math.degrees(angle)+90 if angle<math.pi*1.5 else math.degrees(angle)-270))
        elif 'Belt' in uid:
            top=.12
            for x in (-width/2,width/2-.08):p.append(part('belt side',[x,-length/2,0],[x+.08,length/2,top],color))
            for y in (-length/2,length/2-.06):p.append(part('belt strap',[-width/2,y,0],[width/2,y+.06,top],color))
            p.append(part('holster pouch',[-.11,-.18,0],[.15,.17,.18],color))
        elif 'Grenade' in uid:
            top=.28
            p=[part('smoke canister',[-.08,-.08,0],[.08,.08,.25],'#7B7D75','CylinderZ'),
               part('fuse cap',[-.065,-.065,.24],[.065,.065,top],'#AEB2A6','CylinderZ'),
               part('safety lever',[.055,-.025,.04],[.08,.025,.27],'#49504B')]
        elif uid=='RMCFlashlight':
            top=.11
            p=[part('torch barrel',[-.06,-.23,.005],[.06,.12,top],'#4D585B','CylinderY'),
               part('torch head',[-.1,-.29,.005],[.1,-.2,.16],'#646F6D','CylinderY'),
               part('torch lens',[-.075,-.294,.03],[.075,-.29,.13],'#D2D8BC','CylinderY')]
        elif uid=='DisposalPipeBroken':
            top=.18
            p=[part('broken coupling',[-.25,-.37,0],[.25,-.22,.16],'#5B6061','CylinderY')]
            for x,z in ((-.18,.25),(-.08,.19),(.1,.22)):
                p.append(part('fractured casting',[x,-.27,.09],[x+.065,-.2,z],'#474D4D'))
        elif 'Shard' in uid or uid=='SteelOre1' or 'RockCorner' in uid:
            top=.09 if uid!='RMCPlatformHybrisaRockCornerSmall' else .4
            p=[part('solid fractured material',[-width*.38,-length*.4,.002],[width*.4,length*.4,top],color,'WedgeY')]
        elif uid=='CMHandsInsulated':
            top=.055
            for x in (-.12,.02):
                p.append(part('glove palm',[x,-.13,0],[x+.11,.08,.055],'#AAA235'))
                for i in range(4):p.append(part('glove finger',[x+i*.026,.06,0],[x+i*.026+.021,.17-i*.008,.035],'#AAA235'))
        else:
            top=.035 if 'Bedsheet' in uid else .08 if uid in ('DoorElectronics','Igniter','ProximitySensor','RemoteSignaller') else .16
            p=[part('closed object body',[-width/2,-length/2,0],[width/2,length/2,top],color)]
            if 'Cell' in uid:
                for x in (-width*.3,width*.2):p.append(part('battery terminal',[x,length/2-.04,top],[x+.04,length/2,top+.025],'#A8A784'))
            if 'Bedsheet' in uid:p.append(part('folded cloth edge',[-width/2,-length/2,0],[width/2,-length/2+.035,.05],color))
        # Source artwork belongs on the physical face or footprint, not a floating upright sprite.
        if upright:
            # Open ladder/frame/rail volumes must keep their real holes.
            if uid=='CMUCanisterOxygen':
                rect=(x0+2,y0+(y1-y0)//2,x1-2,y1-4)
                p.append(part('source pressure-cylinder label',[-.16,-.255,.32],[.16,-.245,.7],'#FFFFFF',
                              surface=pool.crop(image,rect),surfaceAxis='XZ'))
            elif not any(word in uid for word in ('Ladder','ConstructionFrame','WindowFrame','Handrail','Tripod')):
                depth=-.267 if uid=='CMUCanisterOxygen' else -.245 if uid=='RMCDrill' else -.542 if 'WallElevator' in uid else -.245
                p.append(part('source face',[-width/2,depth,.01],[width/2,depth+.008,top], '#FFFFFF',
                              surface=pool.crop(image,(x0,y0,x1,y1)),surfaceAxis='XZ'))
        elif uid not in ('DisposalPipeBroken',):
            for xa in range(x0,x1,240):
                for ya in range(y0,y1,240):
                    xb,yb=min(x1,xa+240),min(y1,ya+240);rect=(xa,ya,xb,yb)
                    if image.crop(rect).getbbox():p.append(part('source upper face',[(xa-(x0+x1)/2)/32,((y0+y1)/2-yb)/32,top],
                        [(xb-(x0+x1)/2)/32,((y0+y1)/2-ya)/32,top+.003],'#FFFFFF',surface=pool.crop(image,rect),surfaceAxis='XY'))
        entry=model(uid,[r['id'] for r in group],p,referencePrototype=uid,referenceRsi=rsi,referenceState=state,useEntityRotation=True)
        result.append(entry);images[entry['id']]=image
    return result,images


def review(models, images):
    REVIEW.mkdir(parents=True,exist_ok=True)
    font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',14)
    for page in range((len(models)+5)//6):
        selected=models[page*6:(page+1)*6]
        canvas=Image.new('RGB',(1200,len(selected)*240),'#17212b');draw=ImageDraw.Draw(canvas)
        for row,entry in enumerate(selected):
            y=row*240;draw.text((8,y+3),entry['id']+f" — {len(entry['parts'])} parts",font=font,fill='white')
            if entry['id'] in images:
                source=images[entry['id']].copy();source.thumbnail((190,190),Image.Resampling.NEAREST)
                scale=max(1,min(190//source.width,190//source.height));source=source.resize((source.width*scale,source.height*scale),Image.Resampling.NEAREST)
                canvas.paste(source,(10,y+35),source)
            for col,(yaw,pitch) in enumerate(((-1.1,.55),(1.2,.4),(2.7,-.25))):
                canvas.paste(bm.render_model(entry,(320,210),yaw,pitch),(220+col*325,y+25))
        canvas.save(REVIEW/f'redux-{page+1:02}.png')


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--skip-reviews',action='store_true')
    args=parser.parse_args()
    kinds,issues,_=inventory.load_prototypes(ROOT);assert not issues,issues
    # Excluding our own output keeps repeated generation deterministic.
    kinds['cmu3DModel']={uid:m for uid,m in kinds['cmu3DModel'].items() if not uid.startswith('CMU3DRedux')}
    saved=json.loads((ROOT/'Tools/three_d/generated/inventory.json').read_text())
    rows=redux_coverage.collect(kinds,saved)['targets']
    pool,sources=Pool(),Sources();models=[];images={}
    for entries,refs in (vendors(rows,kinds,sources,pool),cables(rows,sources,pool),pipes(sources,pool),vehicles(rows,kinds,sources,pool),props(rows,kinds,sources,pool)):
        models.extend(entries);images.update(refs)
    write(MODEL,'# CMU14: generated by Tools/three_d/author_redux_coverage.py.\n'+yaml.safe_dump(serialize(models),sort_keys=False,width=115))
    write(ART,'# CMU14: original RSI crops; licenses and attribution in SOURCES_REDUX_COVERAGE.md.\n'+yaml.safe_dump(sorted(pool.entries,key=lambda e:e['atlasIndex']),sort_keys=False))
    surfaces.load_surfaces.cache_clear()
    actual=bm.load_models(MODEL)
    note='# Redux missing-model drafts\n\nGenerated with `Tools/three_d/author_redux_coverage.py`. '+str(len(actual))+' solid models, '+str(sum(len(m['sourcePrototypes']) for m in actual))+' exact prototype bindings.\n\n'
    note+='Closed cabinets have backs, sides, plinths, roofs and deep shelves. Original stock art is printed on recessed shelf volumes. Both cable families cover all 16 source connection masks: LV has round insulated cores, while HV follows the armored rectangular conduit artwork. Disposal barrels, collars and junctions have closed sides and undersides, with source routing marks and reciprocal installed/construction poses. Installed channels retain intact floor cladding and obey the original SubFloorHide owner. Geometry height, depth and unseen surfaces are inferred and remain draft. No gameplay prototype, collision, AI or mob equipment is changed.\n\n'
    note+='All concrete GridVehicleMover variants are included, including civilian and admin-spawnable vehicles outside supply catalogs. Vehicle hulls, wheels and installed hardpoints are separate source-layer assemblies. Mounted turrets bind their installed item through VehicleTurretVisual, follow physical entity yaw, and disappear with the source layer; they do not add equipment to mobs. APCs, Humvees, vans, cargo trucks and the tracked carrier use distinct family profiles with source-sized footprints, closed bellies, cab glazing and rear access panels. Cargo crates and drums have their own volumes. The carrier has closed/open bay poses. The fighter has a tandem cockpit, swept solid wings, forked tail, engine nozzles and folded/flight/VTOL poses at the original sprite scale. Blackfoot uses authored fuselage, cockpit, tail and engine volumes with separate stowed, hover and flight poses. Its full-airframe equipment overlays contribute only changed hardware, including the parachute variant. Tank hulls, tracks, rotating turrets and cannon barrels are separate shaped assemblies; the engineering hull has no turret race. Assemblies remain under the 128-part runtime limit. Vehicle state selection follows the source owner; wheel texture animation and aircraft effect animation are currently represented by static solid poses.\n\n'
    note+='Small props rest on their modeled footprint; cylinders, rails, ladders, cabinets and machinery have closed back/side/underside volumes. Lift platforms, gear walls and layered small props are static drafts; this pass does not add a lift travel animation or reproduce every charge/light/stock overlay. Cloned bindings retain the original models and their existing state contracts and artwork licenses.\n\n'
    note+='The audit includes hidden placements, possible round-setup vendor/vehicle outputs and all concrete drivable vehicle prototypes. Exact binding coverage is not proof of live state coverage or final visual approval. It reports unresolved saved-map prototype references separately and never invents gameplay definitions for them. Unrecognized vehicle layers retain their original sprite fallback.\n\n## Source artwork and licenses\n\n'
    for rsi,meta in sorted(sources.metadata.items()):
        note+=f"- `{rsi}` — {meta.get('license','see original metadata')}. {meta.get('copyright','See original metadata.')}\n"
    write(DOC,note)
    write(REVIEW/'source-references.json',json.dumps(sources.references,indent=2)+'\n')
    if not args.skip_reviews:
        review(actual,images)
    print(json.dumps(dict(models=len(actual),bindings=sum(len(m['sourcePrototypes']) for m in actual),surfaces=len(pool.entries),maxParts=max(len(m['parts']) for m in actual))))


if __name__=='__main__':
    main()
