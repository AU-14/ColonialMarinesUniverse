"""Source-guided static drafts for the two vessel assemblies in Redux water treatment.

The original sprites constrain the visible vessel count, pipe routing and palette.
Their hidden elevation, pipe bore and vertical dimensions remain reconstruction
assumptions. Fixtures, rather than the 96-pixel artwork padding, fix the footprint.
"""
import json
from pathlib import Path

import yaml

import build_models

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'Content.CMU/Resources/ThreeD/Prototypes/World/garrison_filtration_vessels.yml'
RSI = '_RMC14/Structures/Filtration/96x96.rsi'


def solid(label, low, high, color, shape='Box', **extra):
    result = dict(label=label, min=list(low), max=list(high), color=color, **extra)
    if shape != 'Box':
        result['shape'] = shape
    return result


def drum(label, x, y, radius, bottom, top, color):
    return solid(label, (x-radius, y-radius, bottom), (x+radius, y+radius, top), color, 'CylinderZ')


def filtration():
    parts = [
        solid('long left pressure vessel', (-1.39, -.99, .32), (-.39, 1.03, 1.38), '#4D4C4B', 'CylinderY'),
        solid('rounded pressure vessel rear head', (-1.39, .88, .32), (-.39, 1.25, 1.38), '#43413D', 'Ellipsoid'),
        solid('rounded pressure vessel front head', (-1.39, -1.21, .32), (-.39, -.87, 1.38), '#2B2A28', 'Ellipsoid'),
        solid('front head circular flange', (-1.31, -1.24, .40), (-.47, -1.17, 1.30), '#969594', 'CylinderY'),
        solid('front head inset', (-1.23, -1.252, .48), (-.55, -1.238, 1.22), '#484743', 'CylinderY'),
        solid('front outlet coupling', (-1.055, -1.30, .64), (-.725, -1.23, .99), '#8D8572', 'CylinderY'),
        solid('ochre front outlet', (-1.0, -1.46, .68), (-.78, -1.27, .94), '#3E3417', 'CylinderY'),
        solid('front outlet rim', (-1.025, -1.48, .655), (-.755, -1.435, .965), '#8D8572', 'CylinderY'),
        solid('front outlet bore', (-.985, -1.482, .702), (-.795, -1.479, .918), '#25200E', 'CylinderY'),
    ]
    for y in (-.63, .77):
        parts.append(solid('pressure drum saddle foot', (-1.33, y-.13, 0), (-.45, y+.13, .43), '#2B2A28'))
    # Thin tangent patches retain the source's battered steel rather than making
    # the complete drum copper. They do not alter the vessel's overall silhouette.
    for x, y, z, dx, dy, dz in [(-1.395, .2, .6, .026, .33, .25),
                               (-1.395, -.50, .81, .026, .20, .21),
                               (-.985, -.05, 1.36, .11, .4, .018)]:
        parts.append(solid('source rust patch on pressure shell', (x,y,z), (x+dx,y+dy,z+dz), '#6E3D26'))
    for y, name in ((.69, 'rear'), (-.59, 'front')):
        x = .66
        parts.extend([
            drum(name+' upright dark filter vessel', x,y,.445,.19,1.29,'#2B2A28'),
            drum(name+' lower cast flange', x,y,.48,.18,.28,'#191919'),
            drum(name+' upper cast flange', x,y,.48,1.24,1.37,'#43413D'),
            drum(name+' pale lid rim', x,y,.42,1.365,1.44,'#8D8572'),
            drum(name+' recessed lid', x,y,.355,1.436,1.45,'#43413D'),
            drum(name+' concentric lid ridge', x,y,.285,1.447,1.482,'#8D8572'),
            drum(name+' inner lid', x,y,.245,1.48,1.489,'#4D4C4B'),
        ])
        # Sparse physical longitudinal flutes, on both visible and unseen sides.
        for dx, dy in ((-.34,-.24),(-.12,-.43),(.14,-.42),(.35,-.22),(-.35,.22),(.34,.24)):
            parts.append(solid(name+' filter housing rib', (x+dx-.025,y+dy-.025,.29),
                               (x+dx+.025,y+dy+.025,1.23),'#484743'))
        for dx, dz in ((-.22,.44),(.15,.86)):
            parts.append(solid(name+' casing rust stain',(x+dx,y-.425,dz),
                               (x+dx+.07,y-.411,dz+.22),'#6E3D26'))
    for y, shade, name in ((.69, '#276855', 'green rear'), (-.59, '#685627', 'ochre front')):
        # Raised, round manifold with two curved elbows and flanged downpipes.
        parts.append(solid(name+' transverse manifold',(-.90,y-.10,1.67),(.66,y+.10,1.87),shade,'CylinderX'))
        for x in (-.90,.66):
            parts.extend([
                solid(name+' elbow',(x-.105,y-.105,1.63),(x+.105,y+.105,1.88),shade,'Ellipsoid'),
                drum(name+' downpipe',x,y,.10,1.38,1.76,shade),
                drum(name+' connection collar',x,y,.155,1.40,1.49,'#969594'),
            ])
    # The source's open right-hand framework is not an enclosing solid cabinet.
    for x,y in ((.19,-1.21),(1.30,-1.21),(1.30,1.17)):
        parts.append(solid('exposed support frame post',(x-.045,y-.045,0),(x+.045,y+.045,1.12),'#43413D'))
    for z in (.12,1.08):
        parts.append(solid('right longitudinal frame rail',(1.255,-1.25,z),(1.345,1.215,z+.065),'#4D4C4B'))
    for y in (-1.07,-.70,-.31,.08,.48,.88):
        parts.append(solid('right frame narrow crossbar',(1.23,y-.028,.29),(1.38,y+.028,.35),'#969594'))
    parts.extend([
        solid('front instrument plinth',(.20,-1.25,.05),(1.12,-1.08,.30),'#2B2A28'),
        solid('front instrument enclosure',(.24,-1.23,.30),(1.08,-1.08,.65),'#43413D'),
        solid('source front panel dark recess',(.34,-1.244,.38),(.98,-1.228,.53),'#191919'),
        solid('front panel left rust edge',(.29,-1.247,.30),(.36,-1.232,.63),'#6E3D26'),
        solid('front panel right rust edge',(.89,-1.247,.30),(.96,-1.232,.63),'#6E3D26'),
    ])
    return parts


def disinfection():
    parts = []
    # Source stagger: three yellow-topped rear columns, two blue-topped front ones.
    # The saved collision footprint is 3x2 with its centre half a tile SOUTH.
    for x,y,medium,name in [(-.99,.035,'#B6BD1E','rear west'),
                            (0,.035,'#B6BD1E','rear middle'),
                            (.99,.035,'#B6BD1E','rear east'),
                            (-.64,-.99,'#4A8CBD','front west'),
                            (.64,-.99,'#4A8CBD','front east')]:
        parts.extend([
            drum(name+' copper filter vessel',x,y,.385,.28,1.61,'#725B49'),
            drum(name+' dark base collar',x,y,.402,.27,.39,'#30271F'),
            drum(name+' thick copper lid rim',x,y,.423,1.58,1.71,'#47392E'),
            drum(name+' open top dark recess',x,y,.338,1.705,1.717,'#271F19'),
            drum(name+' source colored filter surface',x,y,.285,1.716,1.72,medium),
        ])
        for z,color in ((.54,'#AD5525'),(.91,'#783A18'),(1.23,'#604D3E')):
            parts.append(drum(name+' worn circumferential band',x,y,.389,z,z+.07,color))
        for dx, z, width, height, color in ((-.11,.39,.11,.25,'#AC5525'),
                                           (.065,1.02,.13,.33,'#5D2C11'),
                                           (-.16,.73,.17,.13,'#AD5525')):
            parts.append(solid(name+' vertical copper patina',(x+dx,y-.386,z),
                               (x+dx+width,y-.371,z+height),color))
        for dx in (-.27,.27):
            parts.append(solid(name+' open stand foot',(x+dx-.045,y-.17,0),
                               (x+dx+.045,y+.17,.32),'#271F19'))
    # Source dark pipework ties the staggered rows without filling their gaps.
    for x in (-1.42,1.42):
        parts.extend([
            solid('outer return riser',(x-.05,-.11,.07),(x+.05,-.01,1.01),'#30271F','CylinderZ'),
            solid('outer pipe top elbow',(x-.06,-.11,.92),(x+.06,.18,1.07),'#47392E','Ellipsoid'),
        ])
        lo,hi = sorted((x, 1.13 if x > 0 else -1.13))
        parts.append(solid('outer return connection',(lo,-.11,.93),(hi,-.01,1.03),'#47392E','CylinderX'))
    parts.append(solid('rear inter-vessel crosspipe',(-1.10,-.06,1.00),(1.10,.07,1.13),'#47392E','CylinderX'))
    for x in (-.99,0,.99):
        parts.append(solid('rear lower outlet pipe',(x-.065,-.34,.28),(x+.065,-.10,.41),'#30271F','CylinderY'))
    for x in (-.64,.64):
        parts.extend([
            solid('front return elbow',(x-.15,-1.45,.05),(x+.15,-1.16,.27),'#30271F','CylinderY'),
            solid('front outlet pale coupling',(x-.17,-1.455,.10),(x+.17,-1.39,.32),'#9C9C9C','CylinderY'),
            solid('front open stand tie',(x-.31,-1.22,.12),(x+.31,-1.15,.18),'#47392E'),
        ])
    return parts


def main():
    models = []
    for proto, state, label, parts, bounds, details in [
        ('RMCFiltration','filtration','Water filtration pressure-vessel assembly',filtration(),
         (-1.49,-1.49,1.49,1.49),
         'One long fore/aft pressure drum, two ribbed upright filters, round green and ochre bridge pipes, '
         'front outlet and open support gantry follow the source silhouette. Height 1.88 tiles.'),
        ('RMCFiltrationDisinfection','disinfection','Five-vessel disinfection filter',disinfection(),
         (-1.49,-1.49,1.49,.49),
         'Three yellow-topped rear vessels and two blue-topped front vessels retain the staggered source '
         'layout, copper patina, exposed legs and return pipes. Height 1.72 tiles. The 3x2 footprint is '
         'centred half a tile south, matching its asymmetric fixture rather than recentring the sprite.'),
    ]:
        for part in parts:
            lo, hi = part['min'], part['max']
            assert bounds[0] <= lo[0] < hi[0] <= bounds[2], part
            assert bounds[1] <= lo[1] < hi[1] <= bounds[3], part
            for key in ('min','max'):
                part[key] = ', '.join(f'{v:.6f}' for v in part[key])
        models.append(dict(type='cmu3DModel',id='CMU3D'+proto[3:],label=label,status='draft',
                           sourcePrototypes=[proto],referencePrototype=proto,referenceRsi=RSI,
                           referenceState=state,sourceDirections=1,useEntityRotation=True,
                           description=details+' Vertical dimensions, hidden back surfaces, bore depths and '
                           'pipe elevations remain inferred. Original source is one static direction; '
                           'no operating animation is invented. Source CC-BY-SA-3.0, cmss13 commit '
                           '46d1d000640006d99b3ea99475fee4ba96890702 icons/obj/structures/props/96x96.dmi.',
                           parts=parts))
    OUTPUT.write_text('# Source-guided curved machinery drafts; original RSI metadata provides attribution.\n'+
                      yaml.safe_dump(models,sort_keys=False,width=110),encoding='utf-8')
    library = build_models.load_models(OUTPUT)
    inventory = json.loads((ROOT/'Tools/three_d/generated/inventory.json').read_text())
    build_models.write_reviews(library,ROOT/'Tools/three_d/generated/review/filtration-vessels',
                               {entry['id']:entry for entry in inventory['prototypes']})
    print(json.dumps([dict(id=m['id'],parts=len(m['parts'])) for m in library],indent=2))


if __name__ == '__main__':
    main()
