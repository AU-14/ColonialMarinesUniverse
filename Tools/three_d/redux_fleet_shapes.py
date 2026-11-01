"""CMU14: closed, source-sized vehicle bodies and their independent hardpoints.

The source frame fixes the pivot and footprint; family profiles supply inferred
height. Details are structural volumes, with limited source crops for paintwork.
"""
from redux_vehicle_shapes import part, paint, panel


class Body:
    """Map a family's proportions onto the occupied south-facing RSI frame."""
    def __init__(self, image):
        self.image = image
        self.bounds = image.getbbox()
        x0, y0, x1, y1 = self.bounds
        self.width = (x1-x0)/32
        self.length = (y1-y0)/32
        self.cx = ((x0+x1-image.width)/2)/32
        self.front = (image.height/2-y1)/32
        self.color = paint(image)
        self.parts = []

    def add(self, label, lo, hi, color=None, shape='Box', **extra):
        def point(v):
            return [self.cx+v[0]*self.width, self.front+v[1]*self.length, v[2]]
        self.parts.append(part(label, point(lo), point(hi), color or self.color, shape, **extra))

    def finish(self, pool, x0, y0, x1, y1, z, label):
        # Only paint a real roof/deck panel, never the entire vehicle silhouette.
        bx, by, ex, ey = self.bounds
        rect = (round(bx+(x0+.5)*(ex-bx)), round(ey-y1*(ey-by)),
                round(bx+(x1+.5)*(ex-bx)), round(ey-y0*(ey-by)))
        self.parts += panel(self.image, pool, rect,
                            [self.cx+x0*self.width, self.front+y0*self.length, z],
                            [self.cx+x1*self.width, self.front+y1*self.length, z+.004], label)


def wheels(image, axles=2):
    b = Body(image)
    radius = min(.32, b.width*.21)
    for side in (-1, 1):
        x = side*.435
        for j in range(axles):
            y = .18+j*.65/(axles-1)
            dy = radius/b.length
            b.add('rubber road tire', [x-.065,y-dy,.015], [x+.065,y+dy,2*radius+.015], '#232A2B', 'CylinderX')
            b.add('recessed metal wheel hub', [x-.068,y-dy*.48,radius*.52+.015],
                  [x+.068,y+dy*.48,radius*1.48+.015], '#596261', 'CylinderX')
    return b.parts


def running_boards(b, roof, cabin=(.26,.5)):
    for side in (-1,1):
        x=side*.411
        b.add('cab door skin', [x-.012,cabin[0],.51], [x+.012,cabin[1],roof-.04])
        b.add('side window', [x-.014,cabin[0]+.015,roof-.36], [x+.014,cabin[1]-.025,roof-.08], '#263E44')
        b.add('door handle', [x-.025,cabin[1]-.06,roof-.46], [x+.025,cabin[1]-.025,roof-.43], '#BCC1B5')
        b.add('running board', [x-.055,cabin[0]-.02,.35], [x+.055,cabin[1]+.04,.41], '#303B3B')
        b.add('wing mirror stem', [x-.055,cabin[0]+.025,roof-.3], [x+.055,cabin[0]+.04,roof-.27], '#434D4D')
        b.add('wing mirror', [x+side*.045-.02,cabin[0]+.01,roof-.33],
              [x+side*.045+.02,cabin[0]+.07,roof-.2], '#303E40')


def road_details(b, roof, cabin=(.26,.5)):
    b.add('sealed chassis', [-.38,.025,.25], [.38,.97,.49], '#303A3B')
    for y in (.005,.975):
        b.add('bumper', [-.44,y,.35], [.44,y+.025,.46], '#454F50')
    b.add('front grille', [-.23,.011,.5], [.23,.024,.73], '#202C2D')
    for x in (-.17,-.085,0,.085,.17):
        b.add('grille rib', [x-.009,.008,.51], [x+.009,.026,.71], '#69736F')
    for side in (-1,1):
        x=side*.325
        b.add('headlamp', [x-.055,.012,.59], [x+.055,.025,.72], '#D2D2B2')
        b.add('rear lamp', [x-.035,.974,.6], [x+.035,.986,.72], '#AD3F36')
        for y in (.18,.83):
            b.add('wheel fender', [side*.44-.06,y-.09,.59], [side*.44+.06,y+.09,.66])
    running_boards(b,roof,cabin)


def cross(b, z, x=0, y=.69, color='#CEDCD2'):
    b.add('medical cross horizontal', [x-.14,y-.024,z], [x+.14,y+.024,z+.008], color)
    b.add('medical cross vertical', [x-.036,y-.09,z], [x+.036,y+.09,z+.009], color)


def humvee(image, rsi, pool):
    b=Body(image);medical='medical' in rsi;transport='transport' in rsi
    b.add('armored lower body', [-.4,.03,.45], [.4,.965,.82])
    b.add('sloped engine hood', [-.395,.02,.69], [.395,.265,1.05], shape='WedgeY')
    b.add('cab armored bulkhead', [-.405,.338,.75], [.405,.56,1.38])
    b.add('raked windshield frame', [-.39,.235,.91], [.39,.335,1.4], shape='WedgeY')
    b.add('windshield glazing', [-.35,.24,.98], [.35,.336,1.415], '#304D50', 'WedgeY')
    b.add('windshield mullion', [-.018,.239,.993], [.018,.339,1.43], shape='WedgeY')
    if transport:
        b.add('open transport bed', [-.35,.565,.73], [.35,.94,.81], '#4C5146')
        for x in (-.385,.35):
            b.add('cargo side wall', [x,.56,.79], [x+.035,.95,1.09])
            b.add('troop bench', [x+.035 if x<0 else x-.12,.59,.85], [x+.16 if x<0 else x,.93,.93], '#6C6453')
        b.add('tailgate', [-.36,.925,.8], [.36,.955,1.08])
    else:
        roof=1.52 if medical else 1.38
        b.add('rear enclosed cabin', [-.4,.51,.74], [.4,.95,roof])
        b.finish(pool,-.34,.39,.34,.92,roof+.001,'source cabin paint')
        if not medical:
            b.add('roof hatch rim', [-.17,.57,roof+.005], [.17,.75,roof+.06], '#2D3A36','CylinderZ')
            b.add('hatch opening', [-.13,.592,roof+.06], [.13,.728,roof+.063], '#15221C','CylinderZ')
        else:
            cross(b,roof+.01)
    road_details(b,1.38,(.34,.55))
    b.add('rear access panel',[-.32,.966,.53],[.32,.982,1.25 if not transport else 1.0])
    for side in (-1,1):
        x=side*.405
        b.add('rear door seam',[x-.007,.73,.74],[x+.007,.744,1.06 if transport else 1.34],'#222F2B')
        if medical:
            b.add('side medical cross horizontal',[x-.01,.65,1.1],[x+.01,.84,1.17],'#CFDAD1')
            b.add('side medical cross vertical',[x-.01,.718,.99],[x+.01,.771,1.28],'#CFDAD1')
    for x in (-.22,.07):
        b.add('hood ventilation', [x,.13,.875], [x+.15,.22,1.017], '#34443E','WedgeY')
    return b.parts


def apc(image, rsi, pool):
    b=Body(image);spp='sppapc' in rsi
    b.add('sealed armored belly', [-.37,.045,.25], [.37,.955,.62], '#303936')
    b.add('lower personnel hull', [-.4,.04,.55], [.4,.96,1.05])
    b.add('upper troop compartment', [-.34,.24,.94], [.34,.925,1.52])
    b.add('sloped front armor', [-.4,.025,.81], [.4,.26,1.52], shape='WedgeY')
    b.add('rear sloped armor', [-.4,.89,.89], [.4,.974,1.52], shape='WedgeYReverse')
    b.finish(pool,-.32,.32,.32,.9,1.525,'source compartment roof')
    for side in (-1,1):
        x=side*.42
        b.add('side sponson', [x-.075,.09,.55], [x+.075,.93,1.08])
        b.add('upper fender', [x-.078,.09,1.065], [x+.078,.94,1.12], '#46534C')
        for j in range(4):
            b.add('side armor plate', [x-.081,.14+j*.18,.74], [x+.081,.3+j*.18,1.03])
        b.add('driver vision glass', [side*.15-.095,.135,1.16], [side*.15+.095,.225,1.44], '#6C9B9F','WedgeY')
        b.add('armored lamp', [side*.31-.035,.012,.86], [side*.31+.035,.035,.95], '#D9D7B2')
        b.add('rear marker', [side*.3-.022,.979,.93], [side*.3+.022,.985,1.06], '#A5352E')
        b.add('roof handrail', [side*.29-.012,.58,1.54], [side*.29+.012,.87,1.59], '#738071')
    b.add('rear access ramp', [-.25,.977,.48], [.25,.997,1.39], '#46514B')
    b.add('ramp center seam', [-.009,.998,.5], [.009,1.001,1.36], '#26322D')
    b.add('ramp hinge', [-.27,.975,.48], [.27,1.013,.55], '#758073','CylinderX')
    b.add('exhaust stack', [.23,.83,1.3], [.31,.9,1.7], '#37433B','CylinderZ')
    if spp:
        b.add('turret bearing', [-.25,.43,1.51], [.25,.68,1.61], '#46553C','CylinderZ')
    else:
        b.add('rear service hatch', [-.19,.73,1.532], [.06,.88,1.59], '#35423D','CylinderZ')
    return b.parts


def cargo_load(b, kind):
    if kind in ('crates','barrels','mixed'):
        columns=(.12,) if kind in ('barrels','mixed') else (-.18,.18)
        for x in columns:
            b.add('wooden cargo crate', [x-.14,.54,.79], [x+.14,.925,1.24], '#846B50')
            for y in (.61,.83):
                b.add('cargo securing strap', [x-.145,y,1.24], [x+.145,y+.015,1.258], '#CAA635')
            for z in (.87,1.14):
                b.add('crate side batten', [x-.147,.535,z], [x+.147,.93,z+.035], '#A48965')
    if kind in ('barrels','mixed'):
        for y,color in ((.64,'#80362D'),(.84,'#394F60')):
            b.add('cargo drum', [-.32,y-.086,.79], [-.025,y+.086,1.31], color,'CylinderZ')
            for z in (.84,1.22):
                b.add('drum rolling hoop', [-.328,y-.09,z], [-.017,y+.09,z+.035], '#383F40','CylinderZ')
            b.add('drum lid', [-.32,y-.086,1.31], [-.025,y+.086,1.33], '#676B65','CylinderZ')


def van_or_truck(image, state, rsi, pool):
    b=Body(image)
    military=any(t in rsi for t in ('sppvan','/CLF_van','Vehicles/van.rsi')) and 'hybrisa' not in rsi
    truck='truck' in rsi or state in ('truck_base','armored_base')
    # Cargo occupies most of a loaded truck's top view. It must not determine
    # the paint of the cab. These colors follow the corresponding empty skin.
    if truck:
        if 'blue' in rsi:b.color='#415864'
        elif 'brown' in rsi:b.color='#655246'
        elif 'turquoise' in rsi:b.color='#497A76'
        elif state in ('civtruck_2','civtruck_3'):b.color='#AFB6B0'
        elif 'garbage' in rsi or 'medical' in rsi:b.color='#747C78'
        elif state in ('truck_base','armored_base'):b.color='#4E6159'
    spp='sppvan' in rsi
    roof=1.45 if military or 'box_van' in rsi else 1.3
    cabend=.49 if truck else .43
    b.add('lower vehicle body', [-.41,.035,.42], [.41,.97,.78])
    b.add('front hood', [-.39,.02,.62], [.39,.17,.93], shape='WedgeY')
    b.add('cab bulkhead', [-.405,.28,.72], [.405,cabend,roof])
    b.add('sloping cab frame', [-.39,.155,.79], [.39,.275,roof], shape='WedgeY')
    if spp:
        for side in (-1,1):
            b.add('armored vision window',[side*.18-.12,.21,1.111],[side*.18+.12,.252,1.339],'#527B7E','WedgeY')
    else:
        b.add('windshield', [-.343,.17,.89], [.343,.277,roof+.014], '#284349','WedgeY')
    if military:
        b.add('windshield center frame', [-.02,.169,.9], [.02,.28,roof+.024], shape='WedgeY')
    if truck:
        b.add('cargo bed floor', [-.37,.49,.67], [.37,.955,.8], '#414C49')
        for x in (-.4,.366):
            b.add('bed side wall', [x,.495,.8], [x+.034,.96,1.05])
        b.add('tailgate', [-.38,.935,.8], [.38,.968,1.055])
        if 'garbage' in rsi:
            b.add('refuse compactor', [-.35,.51,.8], [.35,.94,1.57], '#6A7E4B')
            b.add('sloped refuse hopper', [-.36,.86,1.0], [.36,1.0,1.54], '#5B6F43','WedgeYReverse')
            for y in (.56,.73):
                b.add('compactor access panel', [-.31,y,1.572], [.31,y+.13,1.598], '#879363')
        elif 'medical' in rsi:
            b.add('medical supply chest', [-.33,.525,.8], [.33,.92,1.32], '#B4C2B8')
            for x in (-.28,.24):
                b.add('medical case clamp', [x,.52,1.323], [x+.035,.925,1.345], '#456C5D')
            cross(b,1.328,color='#488471')
        else:
            kind='barrels' if ('barrels' in rsi or state=='civtruck_3') else 'mixed' if state=='armored_base' else 'crates' if ('cargo' in rsi or state=='civtruck_2') else None
            cargo_load(b,kind)
    else:
        b.add('enclosed rear body', [-.4,.405,.72], [.4,.965,roof])
        b.add('rear double doors', [-.365,.966,.52], [.365,.98,roof-.055], '#505B56' if military else b.color)
        b.add('rear door seam', [-.007,.981,.54], [.007,.985,roof-.05], '#2F3837')
        for x in (-.065,.045):
            b.add('rear door handle', [x,.984,.79], [x+.02,.996,.93], '#B2BBB1')
        b.finish(pool,-.34,.46,.34,.91,roof+.001,'source roof livery')
        if spp:
            for side in (-1,1):
                x=side*.427
                b.add('armored side storage', [x-.045,.46,.65], [x+.045,.87,1.22], '#B68F4E' if 'logistics' in rsi else b.color)
            if state=='sppvant_base':
                b.add('roof gun mount', [-.12,.55,roof+.005], [.12,.68,roof+.08], '#303A27','CylinderZ')
            elif state=='sppvan_medical':
                cross(b,roof+.015)
        if 'CLF' in rsi:
            # Canvas hoops wrap over a lower solid cargo enclosure.
            for y in (.52,.71,.89):
                b.add('canvas roof hoop', [-.412,y,roof-.035], [.412,y+.02,roof+.04], '#777562')
        if any(t in rsi for t in ('cop_car','ambulance','marshalpaddywagon')):
            b.add('emergency lightbar base', [-.3,.365,roof+.015], [.3,.4,roof+.055], '#2D383B')
            b.add('red warning lamp', [-.27,.368,roof+.054], [-.045,.398,roof+.115], '#C23932')
            b.add('blue warning lamp', [.045,.368,roof+.054], [.27,.398,roof+.115], '#367AB0')
        stripe = '#A73D35' if any(t in rsi for t in ('ambulance','pizza_van')) else '#28343D' if 'cop_car' in rsi else '#9D4734' if state=='sppvan_prisoner' else None
        for side in (-1,1):
            x=side*(.474 if spp else .402)
            if stripe:
                b.add('side livery stripe',[x-.007,.46,.89],[x+.007,.92,.98],stripe)
            if state=='sppvan_medical' or 'ambulance' in rsi:
                b.add('side medical cross horizontal',[x-.01,.62,1.08],[x+.01,.81,1.14],'#D1E0D8')
                b.add('side medical cross vertical',[x-.01,.691,.99],[x+.01,.739,1.23],'#D1E0D8')
            if state=='sppvan_prisoner':
                b.add('prisoner compartment window',[x-.006,.62,1.02],[x+.006,.86,1.18],'#1D302B')
                for y in (.65,.7,.75,.8,.85):
                    b.add('security window bar',[x-.01,y,1.015],[x+.01,y+.009,1.185],'#8B9776')
        if 'cop_car' in rsi:
            b.add('black police hood',[-.385,.022,.637],[.385,.167,.936],'#273235','WedgeY')
    road_details(b,roof,(.275,cabend))
    return b.parts


def carrier(image,state,pool):
    b=Body(image)
    b.add('carrier chassis',[-.35,.035,.19],[.35,.965,.49],'#4A5356')
    b.add('remote drive unit',[-.34,.02,.44],[.34,.3,.78],shape='WedgeY')
    b.add('cargo bay floor',[-.33,.3,.42],[.33,.92,.49],'#272F30')
    for side in (-1,1):
        x=side*.411
        b.add('track belt',[x-.085,.09,.015],[x+.085,.91,.39],'#242C2D')
        for y in (.17,.39,.61,.83):
            b.add('drive wheel',[x-.09,y-.075,.045],[x+.09,y+.075,.345],'#707C7C','CylinderX')
        b.add('bay side wall',[side*.332-.017,.3,.46],[side*.332+.017,.94,.83])
    b.add('rear bay door',[-.33,.915,.46],[.33,.945,.83])
    if state=='cargo_closed':
        b.add('cargo lid',[-.335,.31,.81],[.335,.938,.89])
        b.finish(pool,-.29,.34,.29,.9,.892,'source cargo lid')
    else:
        b.add('raised cargo lid',[-.335,.922,.79],[.335,.957,1.47])
        b.add('open lid reinforcement',[-.26,.914,.86],[.26,.922,1.37],'#3F494D')
    b.add('lid hinge',[-.33,.917,.8],[.33,.963,.89],'#798480','CylinderX')
    b.add('control indicator',[-.09,.14,.64],[.09,.2,.746],'#A99948','WedgeY')
    return b.parts


def fighter(image,state,pool):
    # The 266x335 RSI has one stable center. Its occupied bounds change when the
    # wings fold; scaling the whole hull to each pose's bounds would stretch it.
    c=paint(image);p=[]
    def add(label,lo,hi,color=None,shape='Box',**extra):
        p.append(part(label,lo,hi,color or c,shape,**extra))
    add('lower fuselage keel',[-.39,-3.8,.45],[.39,2.75,1.15],shape='CylinderY')
    add('tapered nose',[-.22,-4.72,.54],[.22,-2.6,1.09],shape='Ellipsoid')
    add('pointed nose cap',[-.085,-5.14,.67],[.085,-4.39,.91],shape='Ellipsoid')
    add('upper fuselage spine',[-.38,-1.65,.87],[.38,2.45,1.47])
    add('canopy fairing',[-.33,-3.02,1.03],[.33,-1.22,1.56],'#394343','Ellipsoid')
    add('tandem cockpit glass',[-.267,-2.94,1.18],[.267,-1.32,1.68],'#23464E','Ellipsoid')
    add('cockpit dividing frame',[-.28,-2.14,1.39],[.28,-2.06,1.67])
    add('spine engine fairing',[-.4,1.4,.92],[.4,3.96,1.45],shape='WedgeYReverse')
    for side in (-1,1):
        x=side*.61
        add('engine nacelle',[x-.21,-.55,.57],[x+.21,2.87,1.18],shape='CylinderY')
        add('engine intake',[x-.17,-.59,.65],[x+.17,-.55,1.1],'#172B2D','CylinderY')
        add('exhaust nozzle',[x-.2,2.71,.62],[x+.2,3.04,1.13],'#343E3E','CylinderY')
        add('dark exhaust throat',[x-.135,3.038,.69],[x+.135,3.046,1.055],'#122223','CylinderY')
        if state=='folded':
            # Vertical outer wings, with a real hinge along the fixed wing root.
            add('folded outer wing',[side*.95-.06,-.78,.93],[side*.95+.06,1.8,2.0],shape='WedgeY')
            add('wing hinge',[side*.84-.035,-.76,.96],[side*.84+.035,1.76,1.08])
        else:
            # Pitch a triangular prism into the XY plane. This gives a closed
            # swept wing from the root to the tip, without disconnected bars.
            add('swept triangular wing',[side*2.37-.065,.3,-.73],[side*2.37+.065,2.4,2.87],shape='WedgeY',pitch=-side*90)
            add('red wing edge marking',[side*2.35-.012,-.63,1.139],[side*2.35+.012,3.31,1.149],'#874B45',yaw=-side*60)
        add('fixed wing root',[side*.68-.34,-.6,.83],[side*.68+.34,1.85,1.045],shape='WedgeY')
        add('forked tail plane',[side*1.31-.045,2.35,.32],[side*1.31+.045,5.2,2.2],shape='WedgeY',pitch=-side*90)
        add('tail fin',[side*1.0-.035,3.3,1.2],[side*1.0+.035,4.84,1.83],shape='WedgeY')
        add('red tail edge marking',[side*1.29-.009,2.33,1.309],[side*1.29+.009,5.24,1.32],'#874B45',yaw=-side*32)
        if state=='vtolmode':
            add('downward vectoring engine',[x-.23,.39,.4],[x+.23,1.05,1.3],shape='CylinderZ')
            add('vertical thrust nozzle',[x-.175,.45,.32],[x+.175,.99,.47],'#1C3234','CylinderZ')
        if state=='folded':
            add('main landing strut',[side*.62-.035,.88,.15],[side*.62+.035,1.02,.76],'#919A95')
            add('landing tire',[side*.62-.12,.7,.015],[side*.62+.12,1.16,.475],'#26302C','CylinderX')
    if state=='folded':
        add('nose gear leg',[-.035,-2.97,.12],[.035,-2.86,.84],'#8C9491')
        add('nose tire',[-.1,-3.1,.015],[.1,-2.73,.385],'#252E2B','CylinderX')
    p+=panel(image,pool,(123,105,143,183),[-.29,-.55,1.472],[.29,1.75,1.476],'source spine finish')
    return p


def mount(image,state,rsi):
    """Use the overlay's actual pivot/footprint for independently aimed parts."""
    b=Body(image);c=b.color
    top=1.4 if 'humvee' in rsi else 1.46 if 'sppvan' in rsi else 1.55
    turret=any(t in state for t in ('turret','cupola','hatch'))
    weapon=any(t in state for t in ('cannon','gun','flamer','launcher','railgun','electrolaser','p17702','t60p3m','hj35'))
    if turret:
        b.add('rotating bearing',[-.38,.12,top],[.38,.86,top+.1],'#39473E','CylinderZ')
        b.add('turret casting',[-.47,.09,top+.08],[.47,.88,top+.35],c,'CylinderZ')
        b.add('sloped turret face',[-.36,0,top+.14],[.36,.35,top+.49],c,'WedgeY')
        b.add('turret roof',[-.31,.3,top+.24],[.31,.79,top+.49])
        b.add('commander hatch',[-.15,.49,top+.49],[.15,.74,top+.54],'#37463A','CylinderZ')
        return b.parts
    if weapon:
        b.add('weapon mounting yoke',[-.23,.71,top-.07],[.23,.92,top+.25],'#3E4D42')
        top+=.21
        if 'launcher' in state or 'hj35' in state:
            b.add('launcher casing',[-.45,.16,top-.06],[.45,.9,top+.15])
            for x in (-.22,.22):
                b.add('rocket tube',[x-.17,0,top-.07],[x+.17,.7,top+.16],'#344438','CylinderY')
                b.add('rocket tube mouth',[x-.125,-.003,top-.025],[x+.125,.002,top+.115],'#132019','CylinderY')
        else:
            b.add('gun breech',[-.35,.67,top-.055],[.35,1,top+.15])
            centers=(-.22,.22) if 'dual' in state else (0,)
            for x in centers:
                b.add('cylindrical gun barrel',[x-.08,.015,top],[x+.08,.79,top+.095],'#546359','CylinderY')
                b.add('muzzle collar',[x-.115,0,top-.017],[x+.115,.085,top+.112],'#35473B','CylinderY')
                b.add('muzzle bore',[x-.06,-.003,top+.019],[x+.06,0,top+.076],'#101B14','CylinderY')
        return b.parts
    return None


def rebuild(image,state,rsi,pool,body,frame):
    if 'jetfighter.rsi' in rsi:
        return fighter(image,state,pool) if body else []
    if 'cargo_carrier.rsi' in rsi:
        return carrier(image,state,pool) if body else None
    apcfamily=any(t in rsi for t in ('/apc.rsi','/apc_pmc.rsi','/sppapc.rsi'))
    humveefamily='humvee' in rsi
    road=apcfamily or humveefamily or any(t in rsi.lower() for t in ('van','truck','cop_car','ambulance','marshalpaddywagon'))
    if body and road:
        if apcfamily:return apc(image,rsi,pool)
        if humveefamily:return humvee(image,rsi,pool)
        return van_or_truck(image,state,rsi,pool)
    if road and state.startswith('wheels_'):
        return wheels(image,4 if apcfamily else 2)
    if road and ('damage' in state) and 'hdpt' not in state and 'turret' not in state:
        # Sparse damage overlays must not become an opaque box over the vehicle.
        b=Body(frame(None))
        roof=1.52 if apcfamily else 1.38 if humveefamily else 1.45 if any(t in rsi for t in ('sppvan','/CLF_van','Vehicles/van.rsi','box_van')) and 'hybrisa' not in rsi else 1.3
        b.add('cab roof damage scar',[-.16,.36,roof+.005],[.11,.41,roof+.009],'#27312B')
        return b.parts
    return mount(image,state,rsi)
