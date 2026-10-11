"""CMU14: authored cabin solids for the fleet's actual walkable interior maps.

One unit is one tile. Large chassis use explicit structural regions rather than
pixel extrusion; floors, collision, storage and vehicle travel stay game-owned.
"""
import argparse
from collections import defaultdict, Counter
from copy import deepcopy
import json
import math
from pathlib import Path

import yaml
from PIL import Image, ImageDraw, ImageFont

import inventory
import build_models as bm
import redux_fleet_shapes
from author_redux_coverage import Sources, Pool, part, serialize, write, ROOT, WORLD

MODEL = WORLD / 'garrison_vehicle_interiors.yml'
ART = WORLD / 'garrison_vehicle_interiors_art.yml'
TEXTURES = ROOT / 'Content.CMU/Resources/Textures/CMU14/ThreeD/VehicleInteriors'
DELIVERY = ROOT / 'Content.CMU/Resources/Models/CMU14/Garrison'
REVIEW = DELIVERY / 'Reviews/VehicleInteriors'
NATIVE = {
    'CMEffectGrab': 'transient grab effect',
    'CMRandomPosterAny': 'random spawn marker',
    'RMCSpawnerRandomCrateLoot': 'random spawn marker',
    'RMCWallInvisibleBulletBlocker': 'invisible collision helper',
    'RMCWallInvisibleBulletLightBlocker': 'invisible collision helper',
    'VehicleInvisibleWall': 'invisible collision helper',
    'VehicleInvisibleWallNoVision': 'invisible collision helper',
    'VehicleExitArea': 'invisible exit trigger',
    'VehicleTankExitHatch': 'transparent exit trigger (the source hatch state has no pixels)',
}


def collect(kinds):
    resolver = inventory.Resolver(kinds['entity'])
    maps, locations = defaultdict(list), defaultdict(list)
    for uid, raw in kinds['entity'].items():
        if raw.get('abstract'):
            continue
        try:
            c = inventory.component_map(resolver.resolve(uid))
        except inventory.ResolutionError:
            continue
        if 'GridVehicleMover' in c and 'VehicleEnter' in c:
            maps[c['VehicleEnter']['interiorPath']].append(uid)
    placements = {}
    for path in sorted(maps):
        doc = inventory.load_yaml(inventory.resource_path(ROOT, path).read_text(encoding='utf-8-sig'))
        placements[path] = []
        for group in doc['entities']:
            uid = group['proto']
            if not uid:
                continue
            locations[uid].append(path)
            placements[path].extend(dict(prototype=uid, **e) for e in group['entities'])
    for uid in ('CMUFighterHull', 'CMUFighterCanopy', 'CMUFighterPilotSeat', 'CMUFighterObserverSeat', 'AU14VehicleRadioSet'):
        locations[uid].append('procedural fighter cockpit')
    refs = defaultdict(list)
    for m in kinds['cmu3DModel'].values():
        if Path(m.get('_source', '')).name == MODEL.name:
            continue
        for uid in m.get('sourcePrototypes', []):
            refs[uid].append(m['id'])
    rows = []
    for uid, where in sorted(locations.items()):
        c = inventory.component_map(resolver.resolve(uid))
        if 'Sprite' in c:
            rows.append(dict(id=uid, locations=where, components=c, sprite=c['Sprite'], models=refs[uid]))
    return dict(maps=dict(maps), placements=placements, rows=rows)


def paint(image):
    colors = Counter(rgb[:3] for rgb in image.getdata() if rgb[3] > 200 and 35 < max(rgb[:3]) < 180)
    rgb = colors.most_common(1)[0][0] if colors else (74, 83, 78)
    return '#%02X%02X%02X' % rgb


class Shape:
    def __init__(self, image, pool):
        self.image, self.pool, self.parts = image, pool, []
        self.color = paint(image)

    def add(self, label, low, high, color=None, shape='Box', **extra):
        self.parts.append(part(label, low, high, color or self.color, shape, **extra))

    def xy(self, rect):
        x0, y0, x1, y1 = rect
        w, h = self.image.size
        return [(x0-w/2)/32, (h/2-y1)/32], [(x1-w/2)/32, (h/2-y0)/32]

    def region(self, label, rect, low, high, printed=True):
        a, b = self.xy(rect)
        self.add(label, [*a, low], [*b, high])
        if printed and self.image.crop(rect).getbbox():
            self.add(label+' source finish', [*a, high], [*b, high+.003], '#FFFFFF',
                     surface=self.pool.crop(self.image, rect), surfaceAxis='XY')

    def face(self, label, rect, low, high, axis='XZ'):
        self.add(label, low, high, '#FFFFFF', surface=self.pool.crop(self.image, rect), surfaceAxis=axis)


def seat(image, pool, uid):
    b = Shape(image, pool)
    fighter = 'Fighter' in uid
    w = .31 if not fighter else .37
    b.add('bolted pedestal', [-.16, -.17, .02], [.16, .17, .42], '#363D3C')
    b.add('seat pan', [-w, -.3, .4], [w, .29, .51])
    b.add('cushion', [-w+.045, -.275, .51], [w-.045, .24, .61], '#333C3B')
    b.add('reinforced back', [-w, .19, .49], [w, .31, 1.32])
    b.add('back cushion', [-w+.045, .16, .63], [w-.045, .192, 1.2], '#293231')
    b.add('headrest', [-w*.76, .145, 1.2], [w*.76, .32, 1.43])
    for x in (-w, w-.035):
        b.add('arm support', [x, -.2, .48], [x+.035, -.12, .82], '#59625B')
        b.add('arm rest', [x-.025, -.27, .81], [x+.06, .2, .87], '#373E3C')
    x0,y0,x1,y1 = image.getbbox()
    b.face('source seat upholstery', (x0,y0,x1,max(y0+1,(y0+y1)//2)), [-w+.04,.151,.7], [w-.04,.159,1.25])
    if fighter:
        for x in (-.17,.12):
            b.add('restraint webbing', [x,.132,.68], [x+.045,.15,1.27], '#B3A158')
        b.add('harness release', [-.07,-.1,.614], [.07,.03,.634], '#929689')
    if 'DoorGunner' in uid:
        b.add('gunner pedestal', [.36,-.2,0], [.55,.2,.83], '#414B48')
        b.add('weapon receiver', [.29,-.48,.8], [.59,.04,1.0])
        b.add('barrel', [.39,-1.02,.865], [.48,-.45,.955], '#343D3B', 'CylinderY')
        b.add('muzzle bore', [.41,-1.025,.885], [.46,-1.02,.935], '#131B19', 'CylinderY')
    return b.parts


def chassis(image, pool, uid, components):
    b = Shape(image, pool)
    if 'Blackfoot' in uid:
        b.region('ribbed cabin deck', (16,8,80,147), .006, .025)
        for label, fixture in components['Fixtures']['fixtures'].items():
            x0,y0,x1,y1 = map(float,fixture['shape']['bounds'].split(','))
            height = 1.05 if label == 'nose' else 2.5
            b.add(label, [x0,y0,0], [x1,y1,height])
            b.add(label+' top trim', [x0,y0,height], [x1,y1,height+.04], '#58625E')
        # Narrow rails bridge the doors at head clearance, leaving the aisles open.
        for x in (-1.5,1.4):
            b.add('cabin roof rail', [x,-1.55,2.5], [x+.1,2.55,2.62])
        for y in (-1.3,0,1.5,2.55):
            b.add('roof cross rib', [-1.4,y,2.55], [1.4,y+.07,2.63])
        b.add('cabin roof',[-1.5,-1.55,2.63],[1.5,2.62,2.71])
        b.face('pilot instruments', (31,154,67,176), [-.64,-1.64,.69], [.64,-1.625,1.12])
    elif 'Humvee' in uid:
        end = 126 if any(t in uid for t in ('Medical','Transport')) else 102
        b.region('cabin deck', (8,18,end,88), .005,.026)
        for rect in ((0,24,8,82),(8,12,76,18),(8,88,76,94),(76,18,96,86)):
            b.region('armored cabin sill', rect, .025,.74)
        b.region('rear bulkhead', (4,23,10,82), .03,2.22)
        b.region('dashboard', (66,27,78,76), .74,1.16)
        for rect in ((8,12,76,16),(8,90,76,94),(76,18,82,86)):
            b.region('overhead window rail', rect, 2.2,2.32)
        for rect in ((8,16,11,90),(72,16,76,90)):
            # Pillars occupy only the four corners, not a panel across the crew.
            x0,y0,x1,y1=rect
            for y in (y0,y1-4): b.region('window pillar',(x0,y,x1,y+4),.74,2.22,False)
        if 'Medical' in uid:
            for y in (32,64): b.region('medical bay pad',(80,y,124,y+22),.04,.52)
        b.region('cabin roof',(8,12,82,94),2.32,2.4,False)
    elif 'SPPAPC' in uid:
        for rect in ((2,10,10,150),(10,0,140,8),(174,0,232,8),(10,152,140,160),(174,152,231,160),(242,35,260,121)):
            b.region('armored hull wall',rect,0,2.45)
        for rect in ((12,34,68,62),(114,34,144,62),(174,34,217,62)):
            b.region('built-in troop bench',rect,0,.55)
        b.region('driver instrument cluster',(231,48,245,111),.55,1.22)
        for y in (8,149): b.region('roof edge',(10,y,240,y+3),2.45,2.55,False)
        b.region('cabin roof',(10,8,242,152),2.55,2.64,False)
    elif 'SPPTank' in uid:
        right = 160 if uid.endswith('Alt') else 129
        for rect in ((9,18,15,96),(15,94,right,101),(15,16,70,22),(70,2,109,8),(right-4,49,right+4,94)):
            b.region('stepped hull bulkhead',rect,0,2.42)
        for rect in ((15,35,47,66),(72,12,98,39),(103,43,right,79)):
            b.region('armored equipment console',rect,.025,1.12)
        b.region('overhead structural rail',(15,93,right,97),2.42,2.53,False)
        b.region('cabin roof',(15,22,right,94),2.53,2.61,False)
        b.region('raised station roof',(70,8,109,22),2.53,2.61,False)
    else:
        for rect in ((11,6,17,100),(17,95,148,101),(148,18,165,83),(17,2,87,7),(114,2,149,7)):
            b.region('van hull panel',rect,0,2.34)
        b.region('driver console',(141,32,151,78),.04,1.12)
        for rect in ((17,19,87,31),(115,19,142,31)):
            b.region('window sill',rect,0,.68)
        b.region('upper frame',(17,2,148,6),2.34,2.46,False)
        b.region('cabin roof',(17,7,148,95),2.46,2.54,False)
    return b.parts


def fighter(image, pool, canopy=False):
    b=Shape(image,pool)
    if canopy:
        for x in (-.72,.67):
            b.add('canopy side rail',[x,-5.6,1.05],[x+.05,-.55,1.14],'#687472')
        for y in (-5.6,-3.2,-.6):
            for x in (-.72,.67):
                b.add('canopy upright',[x,y,1.05],[x+.05,y+.06,2.15],'#5A6562')
            b.add('canopy arch',[-.72,y,2.15],[.72,y+.06,2.22],'#69746D')
        # Glass remains transparent instead of an opaque dome across the crew's view.
        b.add('transparent canopy roof',[-.67,-5.6,2.14],[.67,-.54,2.155],'#89BCCB18')
        for x in (-.675,.665):
            b.add('transparent canopy side',[x,-5.6,1.14],[x+.01,-.54,2.14],'#89BCCB18')
    else:
        exterior=redux_fleet_shapes.fighter(image,'jetfighter',pool)
        for p in exterior:
            if any(t in p['label'] for t in ('canopy','cockpit','upper fuselage','lower fuselage','source spine')):
                continue
            p=deepcopy(p)
            center=[(a+b)/2 for a,b in zip(p['min'],p['max'])]
            half=[(b-a)/2 for a,b in zip(p['min'],p['max'])]
            # Pitched wing prisms exchange X and Z. Scale their local extents
            # accordingly so the wings still meet the fuselage after flattening.
            extent_scale=(.55,2,2) if abs(p.get('pitch',0))==90 else (2,2,.55)
            p['min']=[center[i]*(2,2,.55)[i]-half[i]*extent_scale[i] for i in range(3)]
            p['max']=[center[i]*(2,2,.55)[i]+half[i]*extent_scale[i] for i in range(3)]
            b.parts.append(p)
        b.add('rear fuselage',[-.78,-.5,.035],[.78,5.65,.76],'#465350')
        b.add('forward keel',[-.66,-7.4,.035],[.66,-5.7,.62],'#465350','WedgeY')
        b.add('cockpit floor',[-.72,-5.7,.006],[.72,-.5,.045],'#333F3D')
        for x in (-.87,.72):
            b.add('cockpit side consoles',[x,-5.65,.04],[x+.15,-.48,.94])
        b.add('rear cockpit bulkhead',[-.87,-.56,.04],[.87,-.4,1.35])
        for y in (-5.42,-3.29):
            b.add('instrument coaming',[-.66,y,.56],[.66,y+.23,.92])
            b.add('display',[-.24,y+.23,.69],[.24,y+.24,.9],'#2B8793')
            b.add('control stick',[-.035,y+.47,.24],[.035,y+.55,.64],'#222C29')
            for x in (-.28,.12): b.add('rudder pedal',[x,y+.15,.085],[x+.16,y+.32,.12],'#737B70')
    return b.parts


def structure(image, pool, uid, components):
    b=Shape(image,pool);rect=image.getbbox();x0,y0,x1,y1=rect
    if 'Prop' in uid and 'Tank' in uid:
        # These quarter-tile overlays surround the tank's walkable turret station.
        # Reconstruct edge consoles, not the large flat shadow painted behind them.
        edges={'0':(0,0,32,10),'2':(0,0,8,32),'3':(0,24,32,32),'4':(0,0,32,10),
               '5':(0,0,10,32),'6':(0,23,32,32),'7':(0,25,20,32),'8Extra':(0,27,32,32)}
        key=uid.split('Prop')[1];r=edges[key]
        b.region('turret station equipment',r,.015,.91)
        b.region('console top',r,.91,.96)
    elif (any(t in uid for t in ('Roof','Windshield','WheelFrontTop')) or
          any(t in uid for t in ('Exterior','Background')) and 'Fixtures' not in components or
          any('roof' in l.get('map',[]) for l in components['Sprite'].get('layers',[]))) and 'Door' not in uid:
        b.region('overhead armor',rect,2.45,2.58)
        a,c=b.xy(rect)
        b.add('inner roof rib',[a[0],a[1],2.37],[c[0],min(c[1],a[1]+.06),2.45],'#626E67')
    elif 'Door' in uid or 'Exit' in uid:
        # Sprite art supplies the door position, including narrow side-facing panels.
        a,c=b.xy(rect);wx,wy=c[0]-a[0],c[1]-a[1]
        if 'BlackfootRear' in uid: a,c=[-.98,-.08],[.98,.08];wx,wy=1.96,.16
        if wx<wy*.6:
            cx=(a[0]+c[0])/2; a[0]=cx-.045;c[0]=cx+.045
            b.add('door panel', [*a,0],[*c,2.28])
            b.face('source door face',rect,[c[0],a[1],.12],[c[0]+.006,c[1],2.13],'YZ')
            b.face('inner door face',rect,[a[0]-.006,a[1],.12],[a[0],c[1],2.13],'YZ')
            b.add('door header',[a[0]-.025,a[1],2.28],[c[0]+.025,c[1],2.38],'#7F8981')
        else:
            # Van door artwork overlaps the driver's tile in the top-down map.
            # The physical side panel belongs at the cabin edge, not through the seat.
            cy=-.46 if 'ExteriorDoor' in uid else .46 if 'InteriorDoor' in uid else (a[1]+c[1])/2
            a[1]=cy-.05;c[1]=cy+.05
            b.add('door panel',[*a,0],[*c,2.28])
            b.face('source door face',rect,[a[0],a[1]-.006,.12],[c[0],a[1],2.13])
            b.face('inner door face',rect,[a[0],c[1],.12],[c[0],c[1]+.006,2.13])
            b.add('door header',[a[0],a[1]-.025,2.28],[c[0],c[1]+.025,2.38],'#7F8981')
    else:
        # Narrow sprite outlines stay narrow in the floor plane. Full wall tiles
        # receive closed inner, outer and end faces at ordinary room height.
        b.region('cabin bulkhead',rect,0,2.4)
        a,c=b.xy(rect)
        b.add('lower armor trim',[*a,.16],[*c,.21],'#606B64')
        b.add('upper armor trim',[*a,2.27],[*c,2.34],'#717D75')
    return b.parts


def furnishing(image,pool,uid,components):
    b=Shape(image,pool);rect=image.getbbox();x0,y0,x1,y1=rect
    w=max(.12,(x1-x0)/32);h=max(.18,(y1-y0)/32)
    low=1.0 if 'WallMount' in components or any(t in uid for t in ('Camera','Button','Viewport','Extinguisher','Cassette')) else 0
    if 'AmmoLoader' in uid: low=0
    top=low+min(1.65,h*1.25)
    depth=.15 if low else .58
    b.add('closed equipment casing',[-w/2,-depth/2,low],[w/2,depth/2,top])
    b.face('source equipment controls',rect,[-w/2+.008,-depth/2-.006,low+.008],[w/2-.008,-depth/2,top-.008])
    b.add('reinforced top',[-w/2-.015,-depth/2-.01,top],[w/2+.015,depth/2+.01,top+.035],'#778279')
    b.add('rear service plate',[-w/2+.02,depth/2,low+.025],[w/2-.02,depth/2+.018,top-.025],'#3A4540')
    if 'Locker' in uid or 'Crate' in uid:
        b.add('door handle',[w*.22,-depth/2-.055,low+(top-low)*.43],[w*.28,-depth/2-.025,low+(top-low)*.68],'#9CA598')
    if 'AmmoLoader' in uid:
        b.add('loading tray',[-w*.45,-depth*.83,.33],[w*.45,-depth/2,.42],'#727F70')
        for x in (-w*.27,0,w*.27):b.add('feed roller',[x-.025,-depth*.8,.41],[x+.025,-depth*.4,.46],'#9A9D81','CylinderY')
    if 'Console' in uid or 'Sensors' in uid:
        b.add('operator keyboard tray',[-w*.47,-depth/2-.24,max(.15,top-.52)],[w*.47,-depth/2,max(.2,top-.46)],'#606D63')
    if 'MapTable' in uid or 'TableTOC' in uid:
        b.parts=[]
        w=max(.8,w);d=max(.65,h)
        b.add('tabletop',[-w/2,-d/2,.84],[w/2,d/2,.94])
        b.face('source table surface',rect,[-w/2,-d/2,.94],[w/2,d/2,.944],'XY')
        for x in (-w/2+.08,w/2-.14):
            for y in (-d/2+.08,d/2-.14): b.add('table leg',[x,y,0],[x+.06,y+.06,.84],'#4A5650')
    if uid=='AU14IVFoldable':
        b.parts=[]
        b.add('IV pole',[-.022,-.022,.04],[.022,.022,1.72],'#929E98','CylinderZ')
        for yaw in (0,120,240):b.add('castor base',[-.025,-.26,.025],[.025,.05,.06],'#4D5751',yaw=yaw)
        b.add('bag hook',[-.02,-.025,1.68],[.21,.025,1.73],'#AFB4A3')
        b.add('fluid bag',[.12,-.04,1.14],[.28,.04,1.59],'#C0C9B6')
        b.add('drip tube',[.19,-.012,.47],[.206,.012,1.15],'#959F93')
    if uid=='CMStasisBag':
        b.parts=[]
        b.add('sealed patient bag',[-.35,-.76,.015],[.35,.76,.21],'#3C7778')
        b.face('source stasis cover',rect,[-.35,-.76,.21],[.35,.76,.214],'XY')
    if uid in ('CMDefibrillator','RMCSheetCardboard25'):
        b.parts=[]
        b.add('portable body',[-w/2,-h/2,.012],[w/2,h/2,.16])
        b.face('source upper face',rect,[-w/2,-h/2,.16],[w/2,h/2,.164],'XY')
        if uid=='CMDefibrillator':
            for x in (-w*.4,w*.23):
                b.add('removable paddle',[x,-h*.32,.164],[x+w*.17,h*.26,.21],'#B0B9A1')
                b.add('paddle grip',[x+w*.04,-h*.19,.21],[x+w*.13,h*.15,.245],'#465548')
    if uid.startswith('VehicleAPCM56FPWInterior'):
        b.parts=[]
        b.add('gun mounting pedestal',[-.09,-.12,.02],[.09,.1,.8],'#3E4C47')
        b.add('gun receiver',[-.14,-.38,.82],[.14,.13,1.04],'#53615B')
        b.add('gun barrel',[-.047,-1.05,.9],[.047,-.35,.994],'#3B4843','CylinderY')
        b.add('muzzle bore',[-.025,-1.054,.922],[.025,-1.05,.972],'#111D16','CylinderY')
        for x in (-.22,.16):
            b.add('gunner handgrip',[x,.025,.77],[x+.06,.095,.98],'#283831')
        b.face('source receiver finish',rect,[-.14,-.38,1.04],[.14,.13,1.043],'XY')
    return b.parts


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cache',type=Path,help='Previously parsed inventory for iterative authoring')
    args=parser.parse_args()
    if args.cache: kinds=json.loads(args.cache.read_text())
    else:
        kinds,issues,_=inventory.load_prototypes(ROOT)
        assert not issues,issues
    report=collect(kinds);sources=Sources()
    pool=Pool(ART,TEXTURES,'CMU3DInteriorSurface','/Textures/CMU14/ThreeD/VehicleInteriors')
    resolver=inventory.Resolver(kinds['entity']);donors={}
    for m in kinds['cmu3DModel'].values():
        if Path(m.get('_source','')).name==MODEL.name:continue
        for uid in m.get('sourcePrototypes',[]):
            try:c=inventory.component_map(resolver.resolve(uid))
            except inventory.ResolutionError:continue
            if 'Sprite' in c:donors.setdefault(json.dumps(c['Sprite'],sort_keys=True),m)
    models=[];groups={};images={};categories={}
    for row in report['rows']:
        uid=row['id'];c=row['components'];sprite=row['sprite']
        if row['models'] or uid in NATIVE:continue
        image=sources.compose(sprite)
        signature=json.dumps(sprite,sort_keys=True)
        # Only equivalent seats share geometry; their gameplay roles remain separate.
        if ('Seat' in uid or uid=='VehicleInteriorArmorChair') and signature in groups:
            groups[signature]['sourcePrototypes'].append(uid);continue
        name='CMU3DInterior'+uid
        if signature in donors:
            m=deepcopy(donors[signature]);m.pop('_source',None)
            m.update(id=name,label=uid,sourcePrototypes=[uid],referencePrototype=uid)
            for field in ('alternateFoldModel','alternateDoorModel'):
                if m.get(field):
                    other=deepcopy(kinds['cmu3DModel'][m[field]]);other.pop('_source',None)
                    other.update(id=name+'Alternate',sourcePrototypes=[],referencePrototype=uid)
                    other[field]=name;m[field]=other['id'];models.append(other)
            category='existing source-equivalent model'
        else:
            if 'Chassis' in uid: parts=chassis(image,pool,uid,c);category='hollow chassis'
            elif uid in ('CMUFighterHull','CMUFighterCanopy'):parts=fighter(image,pool,uid.endswith('Canopy'));category='fighter cockpit'
            elif 'Seat' in uid or uid=='VehicleInteriorArmorChair':parts=seat(image,pool,uid);category='crew seat'
            elif any(t in uid for t in ('Wall','Front','Back','Left','Right','Corner','Roof','Exterior','InteriorDoor','ExitDoor','Windshield','Wheel','TankProp','ExitHatch','SideExit','RearExit','DoorBack')) and not any(t in uid for t in ('Viewport','Button','Ammo','Phone','Camera')):
                parts=structure(image,pool,uid,c);category='cabin structure'
            elif uid in ('VehicleAPCApcRight3','VehicleTankTankRight2','VehicleAPCWallViewport'):
                parts=structure(image,pool,uid,c);category='cabin structure'
            else:parts=furnishing(image,pool,uid,c);category='equipment'
            m=dict(type='cmu3DModel',id=name,label=uid,status='draft',sourcePrototypes=[uid],referencePrototype=uid,
                   useEntityRotation=True,parts=parts,placement='floor',groundOffset=[0,0],
                   description='Source-referenced cabin solids with inferred depth; see SOURCES_VEHICLE_INTERIORS.md.')
            if uid in ('CMDefibrillator','RMCSheetCardboard25'):m['placement']='surface'
            facings={'CMUBlackfootAmmoLoader':90,'CMUBlackfootAmmoLoaderRight':-90,
                     'CMUBlackfootCassettePlayer':90,'CMUBlackfootExtinguisherCabinet':-90,
                     'VehicleHumveeViewportFrontNorth':-90,'VehicleAPCM56FPWInteriorNorth':180}
            if uid in facings:m['yawOffset']=facings[uid]
            if 'Foldable' in c:
                # Provide a compact stored pose rather than hiding the model when folded.
                unfolded = next(l['state'] for l in sprite['layers'] if 'unfoldedLayer' in l.get('map', []))
                folded = next(l['state'] for l in sprite['layers'] if 'foldedLayer' in l.get('map', []))
                m.update(referenceRsi=inventory.texture_reference(sprite['sprite']), referenceState=unfolded)
                other=deepcopy(m);other.update(id=name+'Folded',sourcePrototypes=[],folded=True,alternateFoldModel=name)
                other['referenceState']=folded
                im=sources.frame(sprite['sprite'],folded);b=Shape(im,pool)
                b.region('folded equipment',im.getbbox(),.01,.12)
                other['parts']=b.parts
                m['alternateFoldModel']=other['id'];models.append(other)
        models.append(m);images[name]=image;categories[name]=category
        if category=='crew seat':groups[signature]=m
        # Review the default unfolded model for deployed furniture, even when an
        # identical donor's original binding was a folded spawn variant.
        if m.get('folded') and not c.get('Foldable',{}).get('folded',False):
            images.pop(name)
            images[m['alternateFoldModel']]=image
    write(ART,'# CMU14: original RSI crops; licenses in SOURCES_VEHICLE_INTERIORS.md.\n'+yaml.safe_dump(pool.entries,sort_keys=False,width=115))
    write(MODEL,'# CMU14: generated by Tools/three_d/author_vehicle_interiors.py; X/Y horizontal, Z up.\n'+yaml.safe_dump(serialize(models),sort_keys=False,width=115))
    validated=bm.load_models(MODEL)
    REVIEW.mkdir(parents=True,exist_ok=True)
    byid={m['id']:m for m in validated};ordered=list(images)
    font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',14)
    for start in range(0,len(ordered),12):
        ids=ordered[start:start+12];canvas=Image.new('RGB',(1080,190*len(ids)),'#19242B');d=ImageDraw.Draw(canvas)
        for i,mid in enumerate(ids):
            y=i*190;d.text((8,y+2),byid[mid]['label']+' | source / front / rear / underside',font=font,fill='white')
            im=images[mid].copy();im.thumbnail((165,154),Image.Resampling.NEAREST)
            if max(im.size)<75:im=im.resize((im.width*2,im.height*2),Image.Resampling.NEAREST)
            canvas.paste(im,(10,y+29),im)
            for j,(yaw,pitch) in enumerate(((-1.1,.55),(1.1,.4),(2.8,-.32))):
                canvas.paste(bm.render_model(byid[mid],(292,162),yaw,pitch),(190+j*294,y+25))
        canvas.save(REVIEW/f'assets-{start//12+1:02}.png')
    refs={uid:m['id'] for m in models for uid in m['sourcePrototypes']}
    report['coverage']=[dict(id=r['id'],models=r['models'] or ([refs[r['id']]] if r['id'] in refs else []),native=NATIVE.get(r['id'])) for r in report['rows']]
    assert all(r['models'] or r['native'] for r in report['coverage'])
    report['models']=[dict(id=m['id'],category=categories.get(m['id'],'alternate pose'),parts=len(m['parts']),triangles=bm.triangle_count(m['parts']),sourcePrototypes=m['sourcePrototypes']) for m in validated]
    report.pop('rows');report.pop('placements')
    write(REVIEW/'coverage.json',json.dumps(report,indent=2)+'\n')
    references=sorted(sources.metadata)
    doc='# Vehicle interior model sources\n\n'
    doc+='Editable CMU cabin drafts for all 27 VehicleEnter maps and the procedural fighter cockpit. One tile equals one model unit. Depth and hidden faces are authored interpretations. Models do not change vehicle collision, contents or travel.\n\n'
    doc+='Geometry uses structural boxes, rounded seats, rails and closed fittings, not sprite-pixel extrusion. Roof pieces are elevated above occupants; large chassis leave the playable cabin hollow. Optional assets are loaded only by the 3D client.\n\n'
    doc+='## Source artwork\n\nOriginal RSI metadata and licenses remain authoritative. Generated PNGs are crops of these source sprites.\n\n'
    for rsi in references:
        meta=sources.metadata[rsi]
        doc+=f'- `{rsi}` — {meta.get("license","see source meta.json")}; {meta.get("copyright","see source meta.json")}\n'
    doc+='\n## Rebuild and review\n\nRun `python Tools/three_d/author_vehicle_interiors.py`, then export `garrison_vehicle_interiors.yml` with `build_models.py`. `Reviews/VehicleInteriors/coverage.json` records exact bindings and intentional invisible helpers. Review sheets include the source, front, rear and underside.\n'
    write(DELIVERY/'SOURCES_VEHICLE_INTERIORS.md',doc)
    print(json.dumps(dict(maps=len(report['maps']),models=len(models),bindings=len(refs),surfaces=len(pool.entries),native=len(NATIVE),maxParts=max(len(m['parts']) for m in models))))


if __name__=='__main__':main()
