"""CMU14: authored tank and Blackfoot volumes, in the south-facing source frame.

Large vehicles need intentional cross-sections, not extruded sprite silhouettes.
All dimensions are map tiles; source panel crops retain the original paintwork.
"""
from collections import Counter


def part(label, low, high, color, shape='Box', **extra):
    return dict(label=label, min=low, max=high, color=color, shape=shape, **extra)


def paint(image):
    colors = Counter(c[:3] for c in image.get_flattened_data() if c[3] > 200 and 100 < sum(c[:3]) < 500)
    rgb = colors.most_common(1)[0][0] if colors else (77, 86, 80)
    return '#' + ''.join(f'{c:02X}' for c in rgb)


def panel(image, pool, rect, low, high, label, shape='Box'):
    if not image.crop(rect).getbbox():
        return []
    return [part(label, low, high, '#FFFFFF', shape, surface=pool.crop(image, rect), surfaceAxis='XY')]


def tank_hull(image, pool):
    x0, y0, x1, y1 = image.getbbox()
    half = (x1-x0)/64
    front, rear = (64-y1)/32, (64-y0)/32
    c = paint(image)
    p = [part('sealed belly', [-half+.26, front+.19, .22], [half-.26, rear-.2, .49], '#343D39'),
         part('lower armored hull', [-half+.2, front+.09, .45], [half-.2, rear-.12, .78], c),
         part('fighting compartment', [-half+.34, front+.52, .72], [half-.34, rear-.48, 1.05], c),
         part('sloped glacis', [-half+.23, front+.04, .72], [half-.23, front+.65, 1.05], c, 'WedgeY'),
         part('rear engine armor', [-half+.23, rear-.55, .72], [half-.23, rear-.08, 1.05], c, 'WedgeYReverse'),
         part('fixed turret race', [-.66, -.66, 1.02], [.66, .66, 1.12], '#303934', 'CylinderZ'),
         part('turret well', [-.56, -.56, 1.105], [.56, .56, 1.115], '#141C19', 'CylinderZ')]
    # Break the source deck into actual panels. The painted turret socket is not
    # pasted at a second pivot; the circular race above defines the real pivot.
    p += panel(image, pool, (35, 3, 93, 27), [-.9, rear-.79, 1.051], [.9, rear-.09, 1.058], 'engine deck')
    p += panel(image, pool, (35, 85, 93, 119), [-.9, front+.18, .79], [.9, front+.6, 1.055], 'glacis finish', 'WedgeY')
    for side in (-1, 1):
        a, b = sorted((side*(half-.36), side*(half-.07)))
        p.append(part('track fender', [a, front+.05, .77], [b, rear-.07, .87], c))
        for j in range(4):
            y = front+.19+j*(rear-front-.38)/4
            p.append(part('side skirt panel', [a, y, .42], [b, y+(rear-front-.5)/4, .77], c))
        x = side*(half-.4)
        p.append(part('headlamp housing', [x-.1, front-.01, .6], [x+.1, front+.11, .8], '#2D3634'))
        p.append(part('headlamp lens', [x-.07, front-.015, .65], [x+.07, front-.008, .75], '#C8CCA1'))
        p.append(part('rear towing lug', [x-.08, rear-.09, .42], [x+.08, rear+.05, .55], '#333B37'))
        p.append(part('exhaust outlet', [x-.075, rear-.04, .72], [x+.075, rear+.03, .9], '#252C29'))
    p.append(part('front towing beam', [-half+.14, front-.015, .36], [half-.14, front+.08, .49], '#404B43'))
    return p


def tank_tracks(image):
    x0, y0, x1, y1 = image.getbbox()
    half = (x1-x0)/64
    front, rear = (64-y1)/32, (64-y0)/32
    p = []
    for side in (-1, 1):
        a, b = sorted((side*(half-.29), side*half))
        p.extend([part('lower track run', [a, front+.22, .015], [b, rear-.22, .13], '#252A27'),
                  part('upper track run', [a, front+.22, .59], [b, rear-.22, .7], '#303731')])
        for y in (front+.24, rear-.24):
            p.append(part('track return', [a, y-.24, .015], [b, y+.24, .7], '#29312B', 'CylinderX'))
        for j in range(5):
            y = front+.34+j*(rear-front-.68)/4
            p.append(part('road wheel', [a-.006, y-.23, .12], [b+.006, y+.23, .59], '#525B4F', 'CylinderX'))
            p.append(part('wheel hub', [a-.01, y-.09, .27], [b+.01, y+.09, .45], '#2C352D', 'CylinderX'))
        for j in range(7):
            y = front+.26+j*(rear-front-.52)/6
            p.append(part('track tread shoe', [a-.016, y-.035, .635], [b+.016, y+.035, .716], '#4C564A'))
    return p


def tank_turret(image, pool):
    c = paint(image)
    p = [part('rotating turret bearing', [-.61, -.61, 1.07], [.61, .61, 1.2], '#3C453E', 'CylinderZ'),
         part('turret lower casting', [-.81, -.74, 1.18], [.81, .7, 1.49], c, 'CylinderZ'),
         part('turret roof', [-.56, -.44, 1.4], [.56, .5, 1.68], c),
         part('sloped turret front', [-.67, -.9, 1.34], [.67, -.42, 1.68], c, 'WedgeY'),
         part('sloped turret rear', [-.67, .45, 1.34], [.67, .86, 1.68], c, 'WedgeYReverse'),
         part('left turret cheek', [-1.17, -.12, 1.34], [-.23, .18, 1.68], c, 'WedgeY', yaw=-90),
         part('right turret cheek', [.23, -.12, 1.34], [1.17, .18, 1.68], c, 'WedgeY', yaw=90),
         part('mantlet housing', [-.23, -1.0, 1.37], [.23, -.7, 1.64], '#48534A'),
         part('empty cannon socket', [-.095, -1.015, 1.43], [.095, -.995, 1.59], '#18201C', 'CylinderY'),
         part('rear turret bustle', [-.57, .7, 1.32], [.57, 1.04, 1.58], c),
         part('commander hatch rim', [-.5, -.13, 1.66], [-.07, .3, 1.715], '#343F37', 'CylinderZ'),
         part('commander hatch', [-.46, -.09, 1.711], [-.11, .26, 1.735], c, 'CylinderZ'),
         part('periscope', [.17, -.28, 1.68], [.4, -.15, 1.76], '#38463D'),
         part('periscope glass', [.19, -.287, 1.7], [.38, -.28, 1.745], '#8EAAA7')]
    # A roof panel and bustle markings retain CMU paint, while slopes and cheeks
    # provide the silhouette from a player's eye height.
    p += panel(image, pool, (44, 36, 84, 65), [-.55, -.43, 1.681], [.55, .48, 1.686], 'turret roof finish')
    p += panel(image, pool, (44, 15, 84, 30), [-.55, .72, 1.581], [.55, 1.02, 1.586], 'bustle finish')
    return p


def cannon(image):
    x0, y0, x1, y1 = image.getbbox()
    x = ((x0+x1)/2-64)/32
    front, rear = (64-y1)/32, (64-y0)/32
    return [part('gun barrel', [x-.055, front+.11, 1.465], [x+.055, rear-.14, 1.575], '#596459', 'CylinderY'),
            part('recoil sleeve', [x-.085, rear-.5, 1.435], [x+.085, rear, 1.605], '#3E4A40', 'CylinderY'),
            part('muzzle collar', [x-.075, front, 1.445], [x+.075, front+.16, 1.595], '#424C43', 'CylinderY'),
            part('dark bore', [x-.047, front-.004, 1.473], [x+.047, front, 1.567], '#111914', 'CylinderY')]


def blackfoot_body(image, state, pool):
    c = paint(image)
    # Aircraft centerline is x=79 in a 160-pixel frame. Nose points toward -Y.
    p = [part('sealed lower fuselage', [-.38, -1.55, .41], [.32, .9, .85], c, 'CylinderY'),
         part('main cabin', [-.49, -.8, .7], [.43, .74, 1.4], c),
         part('nose lower fairing', [-.38, -2.02, .55], [.32, -1.25, 1.05], c, 'Ellipsoid'),
         part('sloping foredeck', [-.44, -1.55, .8], [.38, -.76, 1.4], c, 'WedgeY'),
         part('tail shoulder', [-.49, .72, .68], [.43, 1.2, 1.4], c, 'WedgeYReverse'),
         part('tail boom', [-.17, 1.02, .72], [.11, 1.82, 1.02], c),
         part('cockpit frame', [-.28, -1.72, .88], [.22, -1.01, 1.39], '#273732', 'WedgeY'),
         part('armored windshield', [-.23, -1.68, .97], [.17, -1.04, 1.42], '#89B7BE', 'WedgeY'),
         part('canopy rear frame', [-.28, -1.06, 1.1], [.22, -1.015, 1.43], c),
         part('canopy central mullion', [-.043, -1.67, .995], [-.018, -1.045, 1.445], '#3A4944', 'WedgeY'),
         part('nose sensor', [-.105, -1.99, .83], [.045, -1.85, 1.0], '#243732', 'Ellipsoid')]
    p += panel(image, pool, (67, 57, 91, 102), [-.405, -.69, 1.401], [.345, .72, 1.405], 'cabin dorsal panels')
    for side in (-1, 1):
        x = -.03 + side*.3
        # Canted stabilizers follow the source's forked tail, leaving its gap open.
        p.append(part('tail fork spar', [x-.115, 1.47, .79], [x+.115, 2.06, .92], c, yaw=-side*24))
        p.append(part('tail stabilizer fin', [x-.035, 1.51, .86], [x+.035, 2.07, 1.36], c, 'WedgeY', yaw=-side*24))
        wingx = -.03 + side*.68
        p.append(part('short wing', [wingx-.39, -.2, .89], [wingx+.39, .15, 1.04], c))
        p.append(part('red wing stripe', [wingx-.22, -.105, 1.041], [wingx+.22, -.065, 1.052], '#934540'))
        podx = -.03 + side*(1.015 if state=='vtol' else .94)
        if state == 'vtol':
            p.append(part('vertical engine nacelle', [podx-.21, -.44, .43], [podx+.21, .34, 1.3], c, 'CylinderZ'))
            p.append(part('vertical intake rim', [podx-.215, -.445, 1.22], [podx+.215, .345, 1.33], '#343F3A', 'CylinderZ'))
            p.append(part('recessed intake', [podx-.16, -.39, 1.331], [podx+.16, .29, 1.335], '#161F1A', 'CylinderZ'))
            p.append(part('downward nozzle', [podx-.16, -.39, .32], [podx+.16, .29, .55], '#2B3430', 'CylinderZ'))
        else:
            p.append(part('horizontal engine nacelle', [podx-.22, -.67, .72], [podx+.22, .9, 1.26], c, 'CylinderY'))
            p.append(part('engine intake rim', [podx-.235, -.7, .71], [podx+.235, -.57, 1.27], '#394640', 'CylinderY'))
            p.append(part('recessed intake', [podx-.18, -.706, .77], [podx+.18, -.699, 1.21], '#19271F', 'CylinderY'))
            p.append(part('exhaust collar', [podx-.18, .81, .78], [podx+.18, 1.01, 1.18], '#2B3830', 'CylinderY'))
        # Side doors, grab rails and glazing give the fuselage an authored side.
        doorx = -.03 + side*.467
        p.append(part('side cargo door', [doorx-.015, -.57, .77], [doorx+.015, .4, 1.32], '#45524B'))
        p.append(part('door window', [doorx-.019, -.25, 1.11], [doorx+.019, .27, 1.26], '#243F40'))
        p.append(part('side step', [doorx-.12, -.57, .55], [doorx+.12, .42, .6], '#303B33'))
        if state == 'stowed':
            p.append(part('main landing strut', [doorx-.03, -.35, .15], [doorx+.03, -.23, .77], '#768076'))
            p.append(part('main landing wheel', [doorx-.09, -.42, .02], [doorx+.09, -.17, .28], '#202923', 'CylinderX'))
    if state == 'stowed':
        p.extend([part('nose gear strut', [-.06, -1.57, .14], [0, -1.46, .73], '#657567'),
                  part('nose gear wheel', [-.105, -1.65, .015], [.045, -1.39, .27], '#222C24', 'CylinderX')])
    return p


def blackfoot_layer(image, state, pool, frame):
    mode = state.rsplit('_', 1)[-1]
    # Door-gun/recon/radar states include a complete copy of the airframe. Only
    # their changed hardware belongs to the attachment, not another hull volume.
    if state.startswith(('doorgun_', 'recon_', 'radar_', 'medevac_')):
        base = frame(mode)
        image = image.copy()
        for y in range(image.height):
            for x in range(image.width):
                if image.getpixel((x,y)) == base.getpixel((x,y)):
                    image.putpixel((x,y), (0,0,0,0))
    if not image.getbbox():
        return []
    p = []
    if state.startswith('engines_'):
        for side in (-1,1):
            x = -.03+side*(1.08 if mode=='vtol' else .94)
            p.append(part('thruster module', [x-.1, -.85 if mode=='vtol' else .6, .58],
                          [x+.1, -.42 if mode=='vtol' else 1.07, .86], '#444F47', 'CylinderY'))
        return p
    if state.startswith('launchers_'):
        for side in (-1,1):
            x=-.03+side*.56
            p.append(part('launcher housing', [x-.08,-1.125,.76], [x+.08,-.4,1.0], '#404C43'))
            for z in (.82,.94):
                p.append(part('launcher tube', [x-.043,-1.131,z-.04], [x+.043,-1.125,z+.04], '#18231B', 'CylinderY'))
        return p
    if state.startswith(('doorgun_', 'recon_', 'radar_', 'medevac_')):
        # The changed pixels are small dorsal modules. Group separate vertical
        # regions so recon's nose camera cannot become a fuselage-length slab.
        x0,y0,x1,y1=image.getbbox()
        for a,b in ((y0,min(y1,83)),(max(y0,83),min(y1,105)),(max(y0,105),y1)):
            if b<=a or not image.crop((x0,a,x1,b)).getbbox():continue
            rect=(x0,a,x1,b);top=1.46 if a<105 else 1.03
            lo=[(x0-80)/32,(80-b)/32,top-.06];hi=[(x1-80)/32,(80-a)/32,top]
            p.append(part('dorsal equipment module',lo,hi,'#3B4840'))
            p+=panel(image,pool,rect,[lo[0],lo[1],top],[hi[0],hi[1],top+.003],'source module finish')
        return p
    if state.endswith('_lights'):
        for x in (-.98,.92):
            p.append(part('navigation light', [x-.035,-.11,1.045], [x+.035,-.055,1.08], '#AB5349'))
        return p
    if state == 'fan-overlay':
        # The source fan strip is an aircraft-sized overlay, not another airframe.
        for x in (-.97,.91):
            p.append(part('intake fan hub', [x-.07,-.711,.92], [x+.07,-.704,1.06], '#6C7C71','CylinderY'))
        return p
    if state in ('flight_thrust','vtol_thrust'):
        for x in (-1.045,.985):
            p.append(part('engine glow', [x-.095,-.26,.28], [x+.095,.06,.32], '#6DA5B6','CylinderZ'))
        return p
    if 'shadow' in state or state == 'downwash':
        return []
    if state == 'damage':
        return [part('scorched fuselage panel', [-.2,-.45,1.405], [.15,.15,1.408], '#202921')]
    return None


def rebuild(image, state, rsi, pool, body, frame):
    if 'Blackfoot/blackfoot.rsi' in rsi:
        return blackfoot_body(image,state,pool) if body else blackfoot_layer(image,state,pool,frame)
    if rsi.endswith(('/tank.rsi','/wytank.rsi','/spptank.rsi','/fv150/exterior.rsi')):
        if body:
            return tank_hull(image,pool)
        if state.startswith('wheels_'):
            return tank_tracks(image)
        if state.startswith('tank_turret_'):
            return tank_turret(image,pool)
        if state.startswith('ltb_cannon_'):
            return cannon(image)
    return None
