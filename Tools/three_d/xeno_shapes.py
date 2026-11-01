"""CMU14: sculpted hive solids, in tile units with Z up and front at -Y.

Overlapping tapered solids form curved tissue without disconnected rod ends.
Surface ribs sit in the skin; hollow organs surround an open throat. These are
the same bounded primitives used by the live renderer and portable GLBs.
"""
import math
from author_redux_coverage import part


def polar(radius, angle, z, yscale=1):
    return [radius*math.cos(angle), radius*math.sin(angle)*yscale, z]


class Sculpt:
    def __init__(self, pale=False):
        self.parts = []
        self.dark, self.body, self.rib, self.light = (
            ('#393034', '#766A6C', '#A29993', '#C3BCB0') if pale else
            ('#0D141C', '#202D39', '#384B5A', '#576C77'))

    def add(self, label, lo, hi, color=None, shape='Box', **extra):
        self.parts.append(part(label, lo, hi, color or self.body, shape, **extra))

    def bulb(self, label, xyz, radius, color=None):
        self.add(label, [a-b for a, b in zip(xyz, radius)],
                 [a+b for a, b in zip(xyz, radius)], color, 'Ellipsoid')

    def vein(self, label, a, b, radius=.03, color=None, shape='Ellipsoid'):
        delta = [b[i]-a[i] for i in range(3)]
        length = math.sqrt(sum(d*d for d in delta))
        center = [(a[i]+b[i])/2 for i in range(3)]
        # Interpenetrating ends conceal joints without a sphere at every bend.
        width, depth = radius if isinstance(radius, tuple) else (radius, radius)
        half = length*.67+depth*.4 if shape == 'Ellipsoid' else length/2+depth*.35
        self.add(label, [center[0]-half, center[1]-width, center[2]-depth],
                 [center[0]+half, center[1]+width, center[2]+depth], color, shape,
                 yaw=math.degrees(math.atan2(delta[1], delta[0])),
                 pitch=math.degrees(math.atan2(delta[2], math.hypot(*delta[:2]))))

    def curve(self, label, points, radii, color=None, shape='Ellipsoid'):
        for i, (a, b) in enumerate(zip(points, points[1:])):
            radius = radii[i] if isinstance(radii, (tuple, list)) else radii
            self.vein(label, a, b, radius, color, shape)

    def fold(self, label, points, radius, color=None):
        """A continuous round fold: capped spans and rounded internal bends."""
        self.curve(label, points, radius, color, 'CylinderX')
        for p in points[1:-1]:
            self.bulb(label+' bend', p, [radius]*3, color)

    def roots(self, radius=.48, count=6, z=.035, color=None):
        for i in range(count):
            a = i*math.tau/count+.13
            bend = .26 if i%2 else -.31
            points = [polar(radius*.18, a, z+.08), polar(radius*.48, a+bend, z+.04),
                      polar(radius*.76, a+bend*.4, z), polar(radius, a+.13, z*.65)]
            self.curve('sinuous anchoring root', points, [.041, .026, .012], color, 'CylinderX')
            if i%2 == 0:
                self.vein('forked root tip', points[2], polar(radius*.93, a-.19, z*.65), .011, color, 'CylinderX')

    def ring(self, radius, z, thickness=.04, color=None, yscale=1, center=(0, 0), count=8):
        for i in range(count):
            p = polar(radius, i*math.tau/count, z, yscale)
            q = polar(radius, (i+1)*math.tau/count, z, yscale)
            self.vein('rolled organic lip', [p[0]+center[0], p[1]+center[1], z],
                      [q[0]+center[0], q[1]+center[1], z], thickness, color, 'CylinderX')


def wall(pale=False, thick=False, membrane=False, reflective=False, weedbound=False):
    s = Sculpt(pale)
    rib = '#827D57' if reflective else s.rib
    # The sealed volume reaches neighbouring tiles; sculpting stays in the skin.
    s.add('continuous resin backing', [-.5, -.5, 0], [.5, .5, 2.4],
          s.body+'68' if membrane else s.body)
    for side in range(4):
        angle = side*math.pi/2
        def face(x, z, depth=.49):
            return [x*math.cos(angle)+depth*math.sin(angle),
                    x*math.sin(angle)-depth*math.cos(angle), z]
        for sign in (-1, 1):
            if not membrane:
                s.vein('broad exoskeletal fold', face(sign*.32, .28, .46),
                       face(sign*.19, 1.67, .46), .20 if thick else .155, s.body)
            s.curve('arching load-bearing tendon',
                    [face(sign*.43, .09), face(sign*.36, .68, .52), face(sign*.20, 1.38, .53),
                     face(sign*.10, 1.91, .52), face(sign*.36, 2.36)], [.058, .055, .044, .026], rib)
            s.curve('lower root buttress', [face(sign*.49, .12), face(sign*.31, .39, .54),
                    face(sign*.07, .61, .52)], [.06, .043], s.body if membrane else rib)
            s.curve('branching surface crease', [face(sign*.45, 1.14), face(sign*.31, 1.43, .53),
                    face(sign*.13, 1.53, .52)], [.028, .022], s.body if membrane else rib)
        s.curve('crown collar', [face(-.47, 2.29), face(-.23, 2.18, .53),
                face(.12, 2.13, .54), face(.47, 2.29)], .052, rib)
        if not membrane:
            s.curve('central interlocking seam', [face(-.02, .16, .505), face(.045, .66, .51),
                    face(-.035, 1.18, .51), face(.06, 1.60, .51)], .021, s.rib)
    for x in (-.32, 0, .32):
        s.curve('sealed roof fold', [[x, -.5, 2.38], [x-.08, -.06, 2.44],
                [x+.04, .5, 2.38]], .045, s.body)
    if weedbound:
        s.roots(.56, 4)
    return s.parts


def door(progress=0, pale=False, thick=False, weedbound=False):
    s = Sculpt(pale)
    depth = .22 if thick else .15
    for sign in (-1, 1):
        s.bulb('resin jamb tissue', [sign*.46, 0, 1.22], [.065, depth, 1.22], s.dark)
        for side in (-1, 1):
            s.curve('curved jamb tendon', [[sign*.47, side*depth, .06],
                    [sign*.43, side*(depth+.02), .81], [sign*.45, side*depth, 1.75],
                    [sign*.33, side*depth, 2.38]], [.05, .04, .035], s.rib)
    s.bulb('fleshy overhanging lintel', [0, 0, 2.39], [.51, depth, .10], s.body)
    for side in (-1, 1):
        s.curve('swept lintel fold', [[-.49, side*depth, 2.41], [-.22, side*depth, 2.32],
                [.12, side*depth, 2.35], [.49, side*depth, 2.43]], .04, s.rib)
    remaining = 1-progress
    if remaining > .006:
        for sign in (-1, 1):
            def leaf(x, y, z):
                return [sign*(.44-x*remaining), y, z]
            lo, hi = sorted((sign*.44, sign*(.44-.445*remaining)))
            s.add('closed contracting membrane', [lo, -depth*.80, .03], [hi, depth*.80, 2.32], s.body)
            for side in (-1, 1):
                for i in range(3):
                    z = .16+i*.67
                    s.curve('diagonal muscular leaf fold',
                            [leaf(.01, side*depth*.7, z), leaf(.14, side*depth, z+.19),
                             leaf(.34, side*depth*.85, z+.43), leaf(.44, side*depth*.65, z+.58)],
                            [(.10 if thick else .078)*remaining+.004, .082*remaining+.004,
                             .05*remaining+.004], s.rib)
                    s.curve('raised leaf seam', [leaf(.01, side*(depth+.012), z+.10),
                            leaf(.22, side*(depth+.014), z+.35), leaf(.44, side*depth, z+.62)],
                            [.023*remaining+.003, .018*remaining+.003], s.body)
    if weedbound:
        s.roots(.48, 4)
    return s.parts


def weeds(state, pale=False, wall_cover=False):
    s = Sculpt(pale)
    if wall_cover:
        for side in range(4):
            angle = side*math.pi/2
            def face(x, z):
                return [x*math.cos(angle)+.512*math.sin(angle),
                        x*math.sin(angle)-.512*math.cos(angle), z]
            for sign in (-1, 1):
                s.curve('climbing root', [face(sign*.27, .04), face(sign*.15, .56),
                        face(sign*.25, 1.1), face(sign*.12, 1.65), face(sign*.29, 2.35)],
                        [.027, .025, .022, .014], s.rib)
            for i in range(5):
                z = .21+i*.42
                s.curve('climbing root fork', [face(-.48, z), face(-.12, z+.14),
                        face(.14, z+.05), face(.48, z+.23)], [.016, .019, .012], s.body)
        return s.parts
    if state in ('constructionnode', 'weednode'):
        s.roots(.25, 4)
        for i in range(3):
            s.bulb('overlapping root node', [-.07+i*.066, .022*(i%2), .10+i*.018],
                   [.088, .11, .09], s.body if i != 1 else s.rib)
        return s.parts
    mask = int(state.split('dir')[-1]) if 'dir' in state else int(state[9:]) if state.startswith('hive_weed') else 15
    seed = sum(ord(c) for c in state) % 7
    # Endpoints remain exact; interior bends vary without changing cardinal connections.
    for i, (dx, dy, flag) in enumerate(((0, 1, 1), (0, -1, 2), (1, 0, 4), (-1, 0, 8))):
        reach = .5 if mask & flag else .23
        bend = (.04+seed*.006)*(-1 if i%2 else 1)
        def point(r, offset, z=.026):
            return [dx*r+dy*offset, dy*r-dx*offset, z]
        s.curve('connected ground rhizome', [point(-.16, .11+bend), point(.06, bend),
                point(reach*.65, -bend*1.6), point(reach, 0)], [.021, .018, .012], s.body, 'Box')
        for sign in (-1, 1):
            s.curve('forked ground filament', [point(.06, bend), point(reach*.45, sign*.21),
                    point(reach*.8, sign*.30)], [.011, .007], s.rib if pale else s.body, 'Box')
        s.curve('interwoven root loop', [point(.16, -.22), point(-.02, -.30),
                point(-.22, -.14)], .009, s.body, 'Box')
    return s.parts


def egg(state, frame=0, count=1, pale=False):
    s = Sculpt(pale)
    st = state.lower(); t = frame/max(1, count-1)
    destroyed = 'exploded' in st
    opening = t if 'opening' in st or 'exploding' in st else 1 if 'opened' in st or destroyed else 0
    size = .65 if 'item' in st else .72+.28*t if 'growing' in st else 1
    shell, ridge, flesh = '#697E86', '#9BABAF', '#95636C'
    s.roots(.36, 4, color='#46545D')
    s.bulb('leathery egg foot', [0, 0, .085], [.285, .265, .08], '#34434D')
    if destroyed:
        s.bulb('ruptured fleshy bed', [0, 0, .095], [.27, .26, .05], flesh)
        for i in range(6):
            a = i*math.tau/6
            s.curve('torn collapsed petal', [polar(.16, a, .12), polar(.32, a+.2, .17),
                    polar(.49, a+.1, .035)], [.078, .039], flesh if i%2 else shell)
            s.vein('exposed torn fibre', polar(.23, a, .16), polar(.46, a+.17, .09), .016, ridge)
    elif opening == 0:
        s.bulb('full leathery lower shell', [0, 0, .32], [.28, .265, .28], shell)
        s.bulb('tapered egg crown', [0, 0, .60], [.20, .19, .25], shell)
        for i in range(8):
            a = i*math.tau/8
            def skin(z):
                lower = .28*math.sqrt(max(0, 1-((z-.32)/.28)**2))
                upper = .20*math.sqrt(max(0, 1-((z-.60)/.25)**2))
                return max(lower, upper)+.005
            points = [polar(skin(z), a+(.13 if j%2 else -.08), z, .95)
                      for j, z in enumerate((.105, .26, .44, .62, .79))]
            s.curve('interlocking longitudinal shell fold',
                    points, [.014, .016, .015, .012], ridge if i%2 == 0 else '#84959A')
        for z, r in ((.22, .259), (.39, .264), (.56, .211)):
            s.ring(r, z, .011, '#50656E', count=8)
    else:
        # The four peeled lobes surround empty space, with no solid cap across the mouth.
        s.bulb('lower hatched shell', [0, 0, .235], [.28, .265, .19], shell)
        s.bulb('dark interior of egg', [0, 0, .38], [.222, .207, .025], '#231F2A')
        s.ring(.236, .41, .037, flesh)
        for i in range(4):
            a = i*math.tau/4
            points = [polar(.22, a, .29), polar(.25, a, .48),
                      polar(.12+.24*opening, a, .67-.12*opening),
                      polar(.025+.44*opening, a, .81-.41*opening)]
            s.curve('peeled leather petal', points, [.092, .086, .053], shell, 'Ellipsoid')
            s.curve('inner petal flesh', [[p[0]*.93, p[1]*.93, p[2]+.013] for p in points[1:]],
                    [.063, .035], flesh, 'Ellipsoid')
            s.curve('outer shell fold', [[p[0]*1.14, p[1]*1.14, p[2]] for p in points],
                    [.019, .016, .010], ridge)
    if size != 1:
        for p in s.parts:
            p['min'] = [v*size for v in p['min']]
            p['max'] = [v*size for v in p['max']]
    return s.parts


def nest(pale=False):
    s = Sculpt(pale)
    s.bulb('recessed resin nest bed', [0, 0, .065], [.34, .48, .065], s.dark)
    for i in range(6):
        y = -.39+i*.148
        for sign in (-1, 1):
            s.curve('cupped nest rib', [[sign*.32, y, .055], [sign*.22, y-.015, .12],
                    [sign*.05, y+.045, .085]], [.031, .02], s.rib if pale else s.body)
    for sign in (-1, 1):
        s.curve('curling restraint', [[sign*.23, -.40, .04], [sign*.34, -.19, .13],
                [sign*.34, .13, .20], [sign*.22, .38, .25], [sign*.12, .34, .23]],
                [.042, .044, .033, .019], s.rib)
        s.curve('outer nest root', [[sign*.23, .3, .055], [sign*.44, .15, .035],
                [sign*.43, -.17, .024], [sign*.49, -.35, .022]], [.027, .019, .012], s.body)
    return s.parts


def core(s):
    s.roots(.76, 6)
    s.bulb('low vaulted core', [0, 0, .355], [.62, .5, .345], s.body)
    for i in range(6):
        x = (i-2.5)*.17
        reach = .5*math.sqrt(1-(x/.62)**2)
        points = [[x, reach*math.sin(a), .355+.353*math.sqrt(1-(x/.62)**2)*math.cos(a)]
                  for a in (-1.4, -.85, -.28, .3, .88, 1.4)]
        s.fold('sweeping vaulted chitin plate', points, .036, s.rib)
        if i in (1, 4):
            s.curve('inset crest seam', [[p[0]-.02, p[1], p[2]+.025] for p in points[1:4]], .009, s.light)
    for sign in (-1, 1):
        for y in (-.24, .28):
            s.curve('lateral shoulder buttress', [[sign*.41, y, .28], [sign*.6, y-.1, .17],
                    [sign*.69, y-.13, .06]], [.06, .03], s.body)


def cocoon(s, state, t):
    opened = 1 if state == 'hatched' else t if state == 'hatching' else 0
    s.roots(.96, 8)
    s.bulb('cocoon basal tissue', [0, 0, .12], [.79, .63, .115], s.dark)
    if opened == 0:
        s.bulb('curled inner cocoon', [-.025, .01, .43], [.78, .64, .42], s.body)
    for i in range(9):
        a = i*math.tau/9
        points = [polar(.79, a, .1, .82), polar(.72+opened*.07, a+.08, .31-opened*.12, .82),
                  polar(.47+opened*.43, a+.35, .76-opened*.52, .82),
                  polar(.17+opened*.79, a+.83, .94-opened*.8, .82)]
        s.curve('curled overlapping cocoon plate', points, [.085, .10, .060], s.body, 'Ellipsoid')
        s.curve('carved cocoon ridge', [[p[0]*1.04, p[1]*1.04, p[2]+.035] for p in points],
                [.033, .032, .018], s.rib)
    for i in range(7):
        a = i*2.4; x, y, _ = polar(.88, a, .1, .83); size = .09+(i%3)*.03
        s.bulb('peripheral nutrient blister', [x, y, size*.6], [size, size*.85, size*.6], '#82985F')
        s.bulb('blister highlight', [x-.018, y-.02, size*1.04], [size*.52, size*.4, size*.16], '#B1BE86')


def morpher(s):
    s.roots(.51, 6)
    s.bulb('morpher basin floor', [0, 0, .12], [.30, .29, .105], s.dark)
    for i in range(8):
        a = i*math.tau/8
        s.curve('fluted egg cradle', [polar(.30, a, .1), polar(.32, a+.12, .33),
                polar(.245, a+.22, .57), polar(.18, a+.25, .72)], [.064, .075, .059], s.body)
        s.curve('cradle rib highlight', [polar(.34, a+.12, .23), polar(.30, a+.18, .46),
                polar(.20, a+.25, .72)], [.019, .014], s.rib)
    s.ring(.185, .72, .031, s.rib)


def sporecaster(s):
    s.roots(.73, 6)
    s.bulb('flattened fungal skirt', [0, 0, .25], [.57, .50, .24], s.body)
    s.bulb('fleshy vent neck', [0, 0, .52], [.33, .3, .18], s.body)
    for i in range(10):
        a = i*math.tau/10
        points = [polar(.52, a, .16, .9), polar(.46, a+.07, .37, .9),
                  polar(.3, a+.10, .61, .9), polar(.15, a+.08, .69)]
        s.curve('radial sporecaster cap lobe', points, [.08, .07, .045], s.rib if i%3 == 0 else s.body, 'Ellipsoid')
        if i%2 == 0:
            s.curve('cap seam', [[p[0]*1.025, p[1]*1.025, p[2]+.018] for p in points[:3]], .021, s.light)
    s.bulb('recessed spore vent', [0, 0, .68], [.125, .125, .025], s.dark)
    s.ring(.14, .728, .03, s.light)
    s.ring(.49, .19, .025, s.rib, yscale=.9)


def spire(s, kind, state, t):
    s.roots(.54, 6)
    if kind == 'acid':
        for i in range(3):
            a = i*math.tau/3+.2
            s.curve('forked acid pillar arm', [polar(.30, a, .07), polar(.13, a+.3, .47),
                    polar(.14, a+.1, .87), polar(.34, a, 1.21), polar(.32, a-.15, 1.47)],
                    [.078, .095, .060, .024], s.body)
            s.curve('acid arm crest', [polar(.18, a+.3, .44), polar(.20, a+.1, .86),
                    polar(.37, a, 1.20)], [.024, .018], s.rib)
            s.vein('backward pointing barb', polar(.17, a, .61), polar(.39, a-.16, .78), .042, s.rib)
        active = 'fire' in state or 'attack' in state
        s.bulb('acid gland', [0, 0, .66], [.12, .13, .22*(1+.06*math.sin(t*math.tau))],
               '#759061' if active else '#475451')
        s.ring(.17, .89, .025, s.rib, count=6)
        s.bulb('acid funnel throat', [0, 0, .85], [.137, .137, .025], s.dark)
    else:
        s.curve('crooked pylon spine', [[0, 0, .09], [-.06, .015, .51], [.06, -.015, .93],
                [-.04, .01, 1.38], [0, 0, 1.69]], [.145, .115, .088, .060], s.body)
        for i in range(3):
            a = i*math.tau/3
            s.curve('twisted pylon buttress', [polar(.32, a, .05), polar(.13, a+.35, .42),
                    polar(.1, a+1, .96), polar(.14, a+1.5, 1.43), polar(.22, a+1.7, 1.69)],
                    [.058, .049, .04, .025], s.rib)
        for i in range(8):
            z = .35+i*.14
            s.curve('overlapping vertebral ridge', [[-.12, .005, z], [-.075, -.105, z+.04],
                    [.07, -.09, z+.08], [.12, .018, z+.06]], .018, s.light)
        s.bulb('pylon sensory crown', [0, 0, 1.70], [.083, .082, .145], '#668E9D')


def cluster(s):
    s.roots(.54, 4)
    for x, y, h, radius in ((-.23, -.13, .77, .16), (.20, -.07, .94, .17),
                             (-.025, .22, 1.17, .18)):
        for i in range(6):
            a = i*math.tau/6
            def at(r, z):
                p = polar(r, a, z)
                return [x*(.45+z/h*.55)+p[0], y*(.45+z/h*.55)+p[1], z]
            s.vein('tapered chimney trunk', at(radius*.50, .09), at(radius*.56, h*.59),
                   (.09, .042), s.body, 'Ellipsoid')
            s.vein('flared chimney bell', at(radius*.56, h*.45), at(radius, h),
                   (.15, .034), s.body, 'Ellipsoid')
        s.ring(radius, h-.008, .030, s.rib, center=(x, y), count=6)
        s.bulb('recessed chimney throat', [x, y, h-.075], [radius*.81, radius*.81, .023], s.dark)


def nutrient(s, kind, pale):
    s.roots(.49, 6)
    blue = kind == 'plasma'
    color, highlight = ('#426F80', '#67A2B4') if blue else ('#735D83', '#AA85B6')
    if pale:
        s.bulb('nutrient basal sac', [0, 0, .24], [.32, .28, .16 if blue else .22], color)
        for i in range(6):
            a = i*math.tau/6
            s.curve('mycelial clasp', [polar(.34, a, .08), polar(.30, a+.10, .26),
                    polar(.16, a+.24, .46 if blue else .64)], [.045, .03], s.rib)
            s.bulb('lobed nutrient tissue', polar(.16, a, .31 if blue else .38),
                   [.12, .11, .115 if blue else .19], color)
        s.bulb('soft nutrient crown', [0, 0, .35 if blue else .5], [.18, .16, .1 if blue else .19], highlight)
    else:
        s.curve('twisting nutrient trunk', [[0, 0, .07], [-.04, .025, .36], [.07, .01, .72],
                [.03, .025, 1.13]], [.11, .085, .045], s.body)
        for i in range(5):
            a = i*2.4+.3; h = .43+i*.15; p = polar(.29 if i<4 else .12, a, h)
            s.curve('arched nutrient branch', [[0, 0, h-.26], [p[0]*.6, p[1]*.6, h-.06],
                    [p[0], p[1], h+.1]], [.037, .027], s.rib)
            if blue:
                s.bulb('plasma sac shoulder', p, [.12, .11, .115], color)
                s.vein('hanging tapered plasma frond', [p[0], p[1], h-.01],
                       [p[0]*1.27, p[1]*1.27, h-.25], .077, highlight)
            else:
                for j in range(3):
                    b = a+j*math.tau/3
                    s.bulb('clustered recovery petal', [p[0]+.055*math.cos(b), p[1]+.055*math.sin(b), h+.025],
                           [.072, .069, .096], highlight if j == 1 else color)
            s.curve('nutrient cup calyx', [[p[0]-.10, p[1], h-.06], [p[0], p[1]-.09, h-.12],
                    [p[0]+.10, p[1], h-.06]], .024, s.body)


def fruit(s, state):
    spent, growing = 'spent' in state, 'immature' in state
    size = .6 if growing else .45 if spent else 1
    color = next((v for k, v in {'lesser': '#48803C', 'greater': '#376F35', 'unstable': '#3E8B77',
                  'spore': '#A57237', 'speed': '#71518C', 'plasma': '#3F8399'}.items() if k in state), '#48803C')
    s.roots(.33, 4)
    for i in range(5):
        a = i*math.tau/5
        s.curve('curved fruit stalk', [polar(.23, a, .03), polar(.15, a+.15, .16*size),
                polar(.1, a+.3, .27*size)], [.025, .018], s.body)
    for i in range(4):
        a = i*math.tau/4; x, y, _ = polar(.105*size, a, 0)
        s.bulb('shrivelled fruit' if spent else 'overlapping nutrient fruit lobe', [x, y, .31*size],
               [.12*size, .105*size, .15*size], s.dark if spent else color)
        s.curve('fruit longitudinal crease', [[x*.7, y*.7, .43*size], [x*1.62, y*1.62, .32*size],
                [x*1.2, y*1.2, .19*size]], .008, s.rib)
    s.bulb('fruit stalk crown', [0, 0, .46*size], [.033, .033, .038], s.body)


def organ(kind, state='', frame=0, count=1, pale=False):
    s = Sculpt(pale); t = frame/max(1, count-1)
    if kind == 'nest':
        return nest(pale)
    if kind == 'spikes':
        for x, y in ((-.3, -.3), (.3, -.3), (-.3, .3), (.3, .3), (0, 0)):
            s.bulb('flared chitin spike socket', [x, y, .045], [.069, .071, .045], s.body)
            s.vein('tapered dark barb', [x, y, .06], [x+.018, y, .24], .038, s.dark)
            s.vein('red barbed tip', [x+.015, y, .20], [x+.028, y-.018, .33], .016, '#8F2949')
    elif kind in ('sticky', 'fast', 'collapse'):
        return [{**p, 'color': '#705F7B' if 'weak' in state else '#3C2F49'} for p in weeds('weed0')]
    elif kind in ('tunnel', 'hole', 'trap'):
        radius = .42 if kind == 'tunnel' else .20
        s.bulb('recessed dark entrance', [0, 0, .018], [radius, radius*.87, .017], s.dark)
        s.ring(radius, .065, .036, s.body, yscale=.87)
        if kind == 'tunnel':
            for i in range(10):
                a = i*math.tau/10
                s.curve('torn tunnel collar', [polar(.50, a, .025, .87), polar(.45, a+.04, .16, .87),
                        polar(.41, a+.09, .28+(i%3)*.06, .87)], [.052, .029], s.rib if i%3 == 0 else s.body)
            s.roots(.64, 4)
        elif kind == 'trap':
            s.bulb('viscous yellow acid trap', [0, 0, .061], [.23, .20, .047], '#AA8A2D')
            for i in range(7):
                a = i*math.tau/7
                s.curve('upturned sticky trap tendril', [polar(.14, a, .08), polar(.30, a+.10, .09),
                        polar(.45, a+.22, .15+(i%2)*.1)], [.027, .017], '#907530')
        elif any(k in state for k in ('acid', 'gas', 'hugger')):
            s.bulb('visible trap contents', [0, 0, .05], [.15, .14, .025], '#8BA654' if 'acid' in state else '#62596D')
    elif kind == 'core':
        core(s)
    elif kind == 'cocoon':
        cocoon(s, state, t)
    elif kind == 'morpher':
        morpher(s)
    elif kind == 'sporecaster':
        sporecaster(s)
    elif kind in ('pylon', 'acid'):
        spire(s, kind, state, t)
    elif kind == 'cluster':
        cluster(s)
    elif kind in ('recovery', 'plasma'):
        nutrient(s, kind, pale)
    elif kind == 'fruit':
        fruit(s, state)
    elif kind == 'sac':
        opened = state == 'open'
        s.bulb('spore sac interior', [0, 0, .06], [.14, .14, .05], s.dark)
        for i in range(5):
            a = i*math.tau/5
            s.curve('split spore petal', [polar(.15, a, .03), polar(.18, a+.1, .13),
                    polar(.31 if opened else .055, a+.24, .1 if opened else .29)], [.046, .038], s.body)
            s.vein('petal seam', polar(.17, a+.1, .11),
                   polar(.30 if opened else .065, a+.24, .12 if opened else .30), .012, s.rib)
    elif kind == 'node':
        s.bulb('construction bud', [0, 0, .12], [.14, .13, .11], s.body)
        s.curve('folded construction bud ridge', [[-.12, 0, .10], [-.03, -.06, .21], [.09, 0, .12]], .018, s.rib)
    else:
        raise ValueError(kind)
    return s.parts
