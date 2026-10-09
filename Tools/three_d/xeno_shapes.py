"""CMU14: low-part-count organic solids, in tile units with Z up.

Silhouettes are hand-authored by organ; no pixel-per-box extrusion or billboards.
"""
import math
from author_redux_coverage import part


class Sculpt:
    def __init__(self, pale=False):
        self.parts = []
        self.dark, self.body, self.rib = ('#494346', '#817A78', '#B5AFA8') if pale else ('#111921', '#293846', '#536571')

    def add(self, label, lo, hi, color=None, shape='Box', **extra):
        self.parts.append(part(label, lo, hi, color or self.body, shape, **extra))

    def bulb(self, label, xyz, radius, color=None):
        self.add(label, [a-b for a,b in zip(xyz,radius)], [a+b for a,b in zip(xyz,radius)], color, 'Ellipsoid')

    def vein(self, label, a, b, radius=.03, color=None, shape='CylinderX'):
        delta = [b[i]-a[i] for i in range(3)]
        length = math.sqrt(sum(d*d for d in delta)); center = [(a[i]+b[i])/2 for i in range(3)]
        self.add(label, [center[0]-length/2,center[1]-radius,center[2]-radius],
                 [center[0]+length/2,center[1]+radius,center[2]+radius], color,
                 shape, yaw=math.degrees(math.atan2(delta[1],delta[0])),
                 pitch=math.degrees(math.atan2(delta[2],math.hypot(*delta[:2]))))

    def roots(self, radius=.48, count=8, z=.045, color=None):
        for i in range(count):
            a=i*math.tau/count
            self.vein('anchoring root', [.06*math.cos(a),.06*math.sin(a),z+.06],
                      [radius*math.cos(a),radius*math.sin(a),z], .028, color, 'Box')

    def ring(self, radius, z, thickness=.04, color=None, yscale=1):
        for i in range(8):
            a=i*math.tau/8;b=(i+1)*math.tau/8
            self.vein('continuous fleshy rim', [radius*math.cos(a),radius*math.sin(a)*yscale,z],
                      [radius*math.cos(b),radius*math.sin(b)*yscale,z],thickness,color)


def wall(pale=False, thick=False, membrane=False, reflective=False, weedbound=False):
    s=Sculpt(pale); rim=s.rib if not reflective else '#8B8860'
    if membrane:
        s.add('translucent resin membrane',[-.47,-.46,.03],[.47,.46,2.36],s.body+'70')
    else:
        s.add('continuous sealed resin core',[-.5,-.5,0],[.5,.5,2.4],s.dark)
    # Ribs meet the full tile edge, so adjacent wall tiles have no open seam.
    for side in (-1,1):
        y=side*.49
        for x in (-.42,0,.42):
            s.vein('vertical exoskeletal rib',[x,y,.03],[x*.65,y,2.35],.045 if thick else .03,rim)
        for z in (.23,.84,1.48,2.15):
            s.vein('front and rear sweeping rib',[-.49,y,z],[0,y,z+.19],.038,rim)
            s.vein('front and rear sweeping rib',[0,y,z+.19],[.49,y,z],.038,rim)
        for z in (.35,1.12,1.9):
            s.vein('finished side rib',[side*.49,-.49,z],[side*.49,.49,z+.2],.035,rim)
    for x in (-.3,.05,.35):
        s.vein('roof rib',[x,-.49,2.42],[x+.07,.49,2.42],.033,rim)
    if weedbound:
        s.roots(.53,6,.045)
    return s.parts


def door(progress=0, pale=False, thick=False, weedbound=False):
    s=Sculpt(pale); depth=.25 if thick else .18
    for sign in (-1,1):
        s.add('solid resin jamb',[sign*.44-.06,-depth,0],[sign*.44+.06,depth,2.45],s.dark)
        s.vein('jamb tendon',[sign*.43,-depth-.018,.05],[sign*.42,-depth-.018,2.38],.04,s.rib)
    s.add('resin lintel',[-.5,-depth,2.28],[.5,depth,2.48],s.body)
    for y in (-depth-.012,depth+.012):
        s.vein('lintel rib',[-.48,y,2.35],[.48,y,2.4],.035,s.rib)
    width=.435*(1-progress)
    if width>.003:
        for sign in (-1,1):
            lo,hi=(-.435,-.435+width) if sign<0 else (.435-width,.435)
            s.add('retracting organic leaf',[lo,-depth+.025,.02],[hi,depth-.025,2.28],s.body)
            for z in (.25,.8,1.35,1.9):
                for y in (-depth,depth):
                    s.vein('leaf sweeping tendon',[lo,y,z],[hi,y,z+.37*(1-progress)],.042,s.rib)
            for y in (-depth,depth):
                s.vein('branching membrane seam',[lo,y,1.4],[hi,y,.72],.045,s.rib)
    if weedbound:s.roots(.48,6,.04)
    return s.parts


def weeds(state, pale=False, wall_cover=False):
    s=Sculpt(pale)
    if wall_cover:
        for side in (-1,1):
            for swap in (False,True):
                def face(v,z):return [v,side*.509,z] if swap else [side*.509,v,z]
                for v in (-.24,.25):
                    s.vein('climbing wall root',face(v,.03),face(v+.1,2.2),.017,s.body,'Box')
                for i,z in enumerate((.14,.46,.81,1.16,1.52,1.91)):
                    s.vein('branching wall web',face(-.46,z),face(.46,z+(.23 if i%2 else -.1)),.014,s.body,'Box')
        return s.parts
    if state in ('constructionnode','weednode'):
        s.bulb('root nodule',[0,0,.13],[.16,.15,.12]);s.roots(.24,4)
        return s.parts
    mask=int(state.split('dir')[-1]) if 'dir' in state else int(state[9:]) if state.startswith('hive_weed') else 15
    seed=sum(ord(c) for c in state)%7
    s.vein('central ground rhizome',[-.12,-.18,.038],[.12,.18,.038],.025,s.dark,'Box')
    for i,(dx,dy,flag) in enumerate(((0,1,1),(0,-1,2),(1,0,4),(-1,0,8))):
        reach=.5 if mask&flag else .25
        s.vein('tile-joining root',[0,0,.04],[dx*reach,dy*reach,.04],.022,s.body,'Box')
        for sign in (-1,1):
            root=.17+seed*.01
            s.vein('branching ground filament',[dx*root,dy*root,.04],
                   [dx*(reach-.06)+dy*sign*.13,dy*(reach-.06)+dx*sign*.13,.03],.015,s.body,'Box')
    return s.parts


def egg(state, frame=0, count=1, pale=False):
    s=Sculpt(pale); st=state.lower();opened='opened' in st;destroyed='exploded' in st
    t=frame/max(1,count-1)
    opening=t if 'opening' in st or 'exploding' in st else 1 if opened or destroyed else 0
    size=.65 if 'item' in st else .72+.28*t if 'growing' in st else 1
    shell='#788B90'; seam='#BBC4AC'; flesh='#9A7479'
    s.roots(.35*size,6,.035)
    if destroyed:
        s.bulb('torn egg base',[0,0,.075],[.28,.26,.075],s.dark)
        for i in range(6):
            a=i*math.tau/6
            s.bulb('broken shell and flesh',[.25*math.cos(a),.25*math.sin(a),.105],[.13,.085,.095],flesh if i%2 else shell)
        return s.parts
    if not opening:
        s.bulb('closed ovoid shell',[0,0,.43*size],[.29*size,.28*size,.42*size],shell)
        for i in range(8):
            a=i*math.tau/8
            points=[]
            for tilt in (-1.15,-.4,.35,1.12):
                points.append([.296*math.cos(tilt)*math.cos(a)*size,.286*math.cos(tilt)*math.sin(a)*size,(.43+.424*math.sin(tilt))*size])
            for p,q in zip(points,points[1:]):s.vein('raised shell ridge',p,q,.014*size,'#A1ADB0')
        return s.parts
    s.bulb('lower egg shell',[0,0,.26*size],[.29*size,.28*size,.25*size],shell)
    if opening:
        s.bulb('dark hollow opening',[0,0,.57*size],[.24*size,.24*size,.035],s.dark)
    for i in range(4):
        a=i*math.tau/4
        x,y=(.12+.23*opening)*math.cos(a)*size,(.12+.23*opening)*math.sin(a)*size
        s.bulb('four opening shell petals',[x,y,(.60-.12*opening)*size],[.16*size,.14*size,(.26-.09*opening)*size],shell)
        s.vein('raised egg seam',[.24*math.cos(a)*size,.24*math.sin(a)*size,.18*size],
               [x,y,(.84-.28*opening)*size],.018,seam)
    return s.parts


def nest(pale=False):
    s=Sculpt(pale)
    s.bulb('sealed nest bed',[0,0,.12],[.36,.48,.12],s.dark)
    for y in (-.37,-.2,0,.2,.38):
        z=.19+.075*(1-abs(y)/.48)
        s.vein('nest rib left',[-.32,y,.15],[0,y+.04,z],.028,s.body)
        s.vein('nest rib right',[0,y+.04,z],[.32,y,.15],.028,s.body)
    for sign in (-1,1):
        s.vein('restraint tendril',[sign*.32,-.3,.13],[sign*.25,.33,.43],.038,s.rib)
    return s.parts


def organ(kind, state='', frame=0, count=1, pale=False):
    s=Sculpt(pale); t=frame/max(1,count-1); pulse=1+.025*math.sin(t*math.tau)
    if kind=='nest':return nest(pale)
    if kind=='spikes':
        for x,y in ((-.3,-.3),(.3,-.3),(-.3,.3),(.3,.3),(0,0)):
            s.bulb('spike socket',[x,y,.05],[.085,.085,.045],s.dark)
            s.add('tapered resin barb',[x-.04,y-.04,.07],[x+.04,y+.04,.32],'#7E263C','WedgeY')
        return s.parts
    if kind in ('sticky','fast','collapse'):
        return [{**p,'color':('#705F7B' if 'weak' in state else '#3C2F49')} for p in weeds('weed0')]
    if kind in ('tunnel','hole','trap'):
        radius=.44 if kind=='tunnel' else .22
        s.bulb('recessed dark entrance',[0,0,.019],[radius,radius,.019],s.dark)
        s.ring(radius,.11 if kind=='tunnel' else .045,.055 if kind=='tunnel' else .025,s.rib)
        if kind=='tunnel':
            for i in range(8):
                a=i*math.tau/8;x=radius*math.cos(a);y=radius*math.sin(a)
                s.add('jagged tunnel lip',[x-.045,y-.055,.08],[x+.045,y+.055,.24+(i%3)*.035],s.body,'WedgeY',yaw=math.degrees(a)-90)
        if kind=='trap':
            s.bulb('acid reservoir',[0,0,.075],[.19,.18,.06],'#B09732')
            s.roots(.44,7,.035,'#827342')
        elif kind=='hole' and any(k in state for k in ('acid','gas','hugger')):
            s.bulb('visible trap contents',[0,0,.06],[.15,.15,.045],'#8BA654' if 'acid' in state else '#62596D')
        return s.parts
    s.roots(.62 if kind in ('core','cocoon','sporecaster') else .45,8)
    if kind=='fruit':
        spent='spent' in state; growing='immature' in state
        color=next((v for k,v in {'lesser':'#589E42','greater':'#398535','unstable':'#43AC93','spore':'#C28B3A','speed':'#8260A1','plasma':'#4A9DAC'}.items() if k in state),'#579644')
        for i in range(3):
            a=i*math.tau/3;x=.14*math.cos(a);y=.14*math.sin(a);h=.13 if spent else .25 if growing else .43
            s.vein('fruit stalk',[0,0,.09],[x,y,h],.027,s.body)
            s.bulb('spent husk' if spent else 'nutrient fruit',[x,y,h],[.09,.09,.035 if spent else .07 if growing else .13],s.dark if spent else color)
    elif kind in ('core','cocoon','morpher','sporecaster'):
        radius=.8 if kind=='cocoon' else .57 if kind=='core' else .42
        height=1.1 if kind=='cocoon' else .8 if kind=='core' else .6
        if kind=='cocoon' and state in ('hatching','hatched'):
            opened=1 if state=='hatched' else t
            s.bulb('hollow hatched cocoon',[0,0,.11],[radius,radius*.82,.09],s.dark)
            for i in range(8):
                a=i*math.tau/8
                s.vein('opening cocoon rib',[radius*.85*math.cos(a),radius*.7*math.sin(a),.13],
                       [(radius*.2+opened*.85)*math.cos(a),(radius*.2+opened*.65)*math.sin(a),height*(1-opened)+.2],.08,s.rib)
            return s.parts
        s.bulb('complete organ body',[0,0,height/2+.1],[radius*.94,radius*.77,height/2*pulse],s.body)
        for i in range(8):
            a=i*math.tau/8
            points=[]
            for tilt in (-1.15,-.4,.35,1.12):
                points.append([radius*1.06*math.cos(tilt)*math.cos(a),radius*.87*math.cos(tilt)*math.sin(a),height/2+.1+(height*.57)*math.sin(tilt)])
            for p,q in zip(points,points[1:]):s.vein('raised curved organ rib',p,q,.038,s.rib)
        if kind=='sporecaster':
            s.ring(.14,.74,.04,s.rib);s.bulb('spore vent hollow',[0,0,.75],[.12,.12,.024],s.dark)
        if kind=='cocoon':
            for x,y in ((-.69,-.4),(.7,.3),(.47,-.66),(-.52,.58)):
                s.bulb('nutrient sac',[x,y,.2],[.13,.13,.12],'#A4B877')
    elif kind in ('pylon','acid'):
        h=1.95 if kind=='pylon' else 1.45
        s.bulb('central resin stalk',[0,0,h*.42],[.17,.17,h*.41],s.dark)
        for i in range(4):
            a=i*math.tau/4
            s.vein('curved lower prong',[.32*math.cos(a),.32*math.sin(a),.08],[.13*math.cos(a),.13*math.sin(a),h*.65],.06,s.rib)
            s.vein('upper prong',[.13*math.cos(a),.13*math.sin(a),h*.65],[.27*math.cos(a),.27*math.sin(a),h],.045,s.body)
        if kind=='acid':s.bulb('acid firing sac',[0,0,h*.68],[.14,.14,.22],'#718E48' if 'fire' in state else s.rib)
        else:
            s.bulb('pylon crown',[0,0,h-.08],[.12,.12,.17],s.rib if pale else '#4888A4')
            for i in range(12):
                a=i*math.tau/6;b=(i+1)*math.tau/6;z=.17+i*.12
                s.vein('spiral pylon ridge',[.18*math.cos(a),.18*math.sin(a),z],[.18*math.cos(b),.18*math.sin(b),z+.12],.026,s.rib)
    elif kind in ('recovery','plasma','cluster'):
        if pale and kind!='cluster':
            s.bulb('central nutrient pod',[0,0,.4],[.33,.3,.31],'#4894A9' if kind=='plasma' else '#976FAB')
            for i in range(6):
                a=i*math.tau/6
                points=[[.35*math.cos(t)*math.cos(a),.32*math.cos(t)*math.sin(a),.4+.35*math.sin(t)] for t in (-1.1,-.3,.5,1.2)]
                for p,q in zip(points,points[1:]):s.vein('curved pod cage rib',p,q,.025,s.rib)
        else:
            s.vein('central branching trunk',[0,0,.07],[0,0,1.05],.095,s.body)
            for i in range(5):
                a=i*2.4;x=.3*math.cos(a);y=.3*math.sin(a);z=.5+i*.15
                s.vein('raised organ branch',[0,0,z-.3],[x,y,z],.04,s.rib)
                if kind=='cluster':
                    s.bulb('fluted hive chimney',[x,y,z],[.17,.17,.09],s.body)
                    s.bulb('dark chimney throat',[x,y,z+.071],[.13,.13,.021],s.dark)
                else:
                    s.bulb('nutrient sac',[x,y,z],[.14,.13,.17],'#44879D' if kind=='plasma' else '#816697')
    elif kind=='sac':
        opened=state=='open'
        s.bulb('spore sac',[0,0,.16],[.18,.18,.14],s.dark if opened else '#766963')
        for i in range(5):
            a=i*math.tau/5
            s.vein('split sac petal',[0,0,.08],[.27*math.cos(a),.27*math.sin(a),.13 if opened else .32],.035,s.rib)
    elif kind=='node':s.bulb('construction bud',[0,0,.15],[.15,.15,.14],s.rib)
    else:raise ValueError(kind)
    return s.parts
