"""Source-guided sedimentation plant drafts; dimensions above the floor are inferred.

The source sprites define two different machines, not scaled copies: three narrow
vessels behind a wide front bed, versus one wide vessel beside a small rear bed.
Only the two authored files belong to this generator. Global exports are separate.
"""
import json
from pathlib import Path

import yaml

import build_models

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'Content.CMU/Resources/ThreeD/Prototypes/World/garrison_sedimentation_tanks.yml'
RSI = '_RMC14/Structures/Filtration/96x96.rsi'
# Exact RGB samples from sedimentation.png and sedimentation_A_1.png.
BLUE = '#2A4783'
BLUE_LIT = '#4E75C7'
BLUE_TOP = '#7593D3'
BLUE_DARK = '#1C2F57'
MINT = '#94C5A6'
GREEN = '#7CA36F'
STEEL = '#646D66'
EDGE = '#89938C'
DARK = '#0B1221'
RUST = '#733817'
BROWN = '#42352B'


def part(label, low, high, color, shape='Box'):
    value = dict(label=label, min=list(low), max=list(high), color=color)
    if shape != 'Box':
        value['shape'] = shape
    return value


def pipe(label, points, radius=.035, color=MINT):
    """Axis-aligned round tube with elbows at the path's actual corners."""
    result = []
    for a, b in zip(points, points[1:]):
        axes = [i for i in range(3) if a[i] != b[i]]
        assert len(axes) == 1, (label, a, b)
        axis = axes[0]
        low = [min(a[i], b[i]) - (radius if i != axis else 0) for i in range(3)]
        high = [max(a[i], b[i]) + (radius if i != axis else 0) for i in range(3)]
        result.append(part(label + ' tube', low, high, color, 'Cylinder' + 'XYZ'[axis]))
    for c in points[1:-1]:
        result.append(part(label + ' elbow', [v-radius for v in c], [v+radius for v in c], color, 'Ellipsoid'))
    return result


def vessel(label, x, y, radius, height):
    r = radius
    return [part(label+' pedestal foot ring', (x-r*.84, y-r*.84, 0), (x+r*.84, y+r*.84, .32), BLUE_DARK, 'CylinderZ'),
            part(label+' blue barrel', (x-r, y-r, .29), (x+r, y+r, height-.20), BLUE, 'CylinderZ'),
            part(label+' domed shoulder', (x-r, y-r, height-.37), (x+r, y+r, height), BLUE_LIT, 'Ellipsoid'),
            part(label+' top inspection lid', (x-r*.64, y-r*.64, height-.055), (x+r*.64, y+r*.64, height+.015), BLUE_TOP, 'CylinderZ'),
            part(label+' front left stiffener', (x-r*.68, y-r*.81, .39), (x-r*.58, y-r*.79, height-.30), BLUE_LIT)]


def trough(label, x0, x1, y0, y1, height, bars):
    """Open filter bed, pale perimeter and dark/cyan lower inspection gallery."""
    p = [part(label+' lower foundation', (x0, y0, 0), (x1, y1, .13), EDGE),
         part(label+' dark filter well', (x0+.07, y0+.07, .16), (x1-.07, y1-.07, height-.15), '#1A120C'),
         part(label+' front observation strip', (x0+.08, y0-.005, .20), (x1-.08, y0+.03, height*.48), '#457CB8'),
         part(label+' front sill', (x0, y0, .13), (x1, y0+.085, .23), STEEL),
         part(label+' front upper beam', (x0, y0, height-.14), (x1, y0+.085, height), EDGE),
         part(label+' rear rim', (x0, y1-.08, height-.13), (x1, y1, height), EDGE),
         part(label+' left rim', (x0, y0, .13), (x0+.08, y1, height), STEEL),
         part(label+' right rim', (x1-.08, y0, .13), (x1, y1, height), STEEL)]
    step = (x1-x0-.24)/bars
    for i in range(bars):
        x = x0+.12+step*(i+.5)
        p.append(part(label+' brown filter bar', (x-.027, y0+.065, height-.23), (x+.027, y1-.065, height-.09), '#5B4839'))
    # Source's large blue inverted-U transfer pipe crosses the near wall.
    x = x0+(x1-x0)*.28
    r = min(.085, (x1-x0)*.055)
    p.extend(pipe(label+' dark blue transfer', [(x-.19,y0-.02,.16), (x-.19,y0-.02,height+.09),
                                               (x+.19,y0-.02,height+.09), (x+.19,y0-.02,height-.12)], r, '#28496D'))
    return p


def motor_skid(x0, x1, y0, y1, cyan_display=False):
    """The shared right-hand open rack has blue controls over a silver motor."""
    z = .11
    p = [part('pump rack bottom', (x0,y0,z), (x1,y1,z+.08), DARK),
         part('pump rack left upright', (x0,y0,z), (x0+.055,y1,.95), DARK),
         part('pump rack right upright', (x1-.055,y0,z), (x1,y1,.95), DARK),
         part('pump rack motor shelf', (x0,y0,.40), (x1,y1,.46), '#383D39'),
         part('blue controller housing', (x0,y0,.83), (x1,y1,1.07), BLUE_DARK),
         part('blue controller front', (x0+.035,y0-.007,.89), (x1-.035,y0+.017,1.035), BLUE_LIT),
         part('controller status strip', (x0+.055,y0-.011,.935), (x1-.16,y0-.007,.962), '#48C9EC' if cyan_display else STEEL),
         part('controller red pilot', (x0+.035,y0-.012,1.035), (x0+.085,y0+.018,1.085), '#8C2629'),
         part('controller pale end switch', (x1-.075,y0-.013,.91), (x1-.035,y0-.007,.98), '#BAB7B8')]
    cy = (y0+y1)/2
    p.append(part('silver horizontal pump motor', (x0+.06,cy-.11,.49), (x1-.09,cy+.11,.72), '#989898', 'CylinderX'))
    for x in (x0+.085, x1-.18):
        p.append(part('dark motor winding band', (x,cy-.118,.483), (x+.065,cy+.118,.727), '#493C3B', 'CylinderX'))
    return p


def pressure_canister(x, y):
    return [part('small red pressure bottle', (x-.10,y-.10,.16), (x+.10,y+.10,.65), '#8C2629', 'CylinderZ'),
            part('pressure bottle dark shoulder', (x-.10,y-.10,.58), (x+.10,y+.10,.72), '#3E1613', 'Ellipsoid'),
            part('pressure bottle valve', (x-.035,y-.035,.70), (x+.035,y+.035,.82), '#636B64')]


def main_machine():
    p = []
    centers = (-1.08, -.29, .50)
    for i, x in enumerate(centers):
        p.extend(vessel(f'vessel {i+1}', x, .86, .35, 1.99))
        # Distinct source lower mint elbows join the forward collection header.
        p.extend(pipe(f'vessel {i+1} outlet', [(x,.51,.95),(x,.34,.95),(x,.34,.58),(x+.15,.34,.58)], .055))
        p.extend(pipe(f'vessel {i+1} upper feed', [(x,.86,2.005),(x,.86,2.09),(x,.45,2.09)], .032))
    p.extend(pipe('shared upper mint header', [(-1.08,.45,2.09),(.81,.45,2.09),(.81,.45,.58)], .032))
    p.extend(pipe('lower mint collection header', [(-.93,.34,.58),(.81,.34,.58),(.81,.48,.58)], .047))
    p.extend(trough('wide front filter bed', -1.34, .84, -1.375, -.54, .80, 7))
    p.extend(motor_skid(.79,1.40,-.15,.44))
    p.extend(pressure_canister(.10,.08))
    # Tall ochre bottle to the left of the center vessel is separate from the red bottle.
    p.extend([part('ochre separator bottle', (-.80,.20,.17), (-.65,.35,1.04), '#2B2823','CylinderZ'),
              part('ochre separator brass collar', (-.80,.20,.98), (-.65,.35,1.04), '#966A31','CylinderZ'),
              part('brown front service cabinet', (.89,-1.375,.13), (1.47,-.54,.84), BROWN),
              part('service cabinet recessed door', (.96,-1.381,.26), (1.40,-1.375,.76), '#5B4839'),
              part('service cabinet pale latch', (1.16,-1.389,.42), (1.22,-1.381,.45), '#BAB7B8')])
    p.extend(pipe('rust drain descending into bed', [(.40,.05,1.13),(.40,.05,.87),(.58,.05,.87),(.58,-.73,.87),(.58,-.73,.49)], .041, RUST))
    p.extend(pipe('rear red supply riser', [(-1.40,1.28,.05),(-1.40,1.28,1.30),(-1.13,1.28,1.30)], .035, '#25070D'))
    p.extend(pipe('front green service header', [(-1.10,-.37,.15),(-1.10,-.37,.47),(.90,-.37,.47)], .032, GREEN))
    p.append(part('small grey controller on lower header', (-.17,-.413,.39), (.10,-.365,.53), EDGE))
    return p


def alternate_machine():
    p = vessel('large single vessel', -.59, -.41, .77, 2.08)
    p.extend(trough('small rear right filter bed', .23,1.47,-.08,.47,.94,5))
    p.extend(motor_skid(.57,1.43,-1.34,-.70, cyan_display=True))
    p.extend(pressure_canister(.19,-.73))
    p.extend(pipe('large vessel mint outlet', [(-.66,-1.08,1.23),(-.66,-1.22,1.23),(-.66,-1.22,.79),
                                               (-.13,-1.22,.79),(-.13,-1.22,.39)], .060))
    p.extend(pipe('large vessel top return', [(-.59,-.41,2.10),(.22,-.41,2.10),(.22,-.41,.53),
                                              (.44,-.41,.53),(.44,-1.22,.53)], .033))
    p.extend(pipe('green base manifold', [(-1.25,-1.33,.14),(-1.25,-1.33,.38),(.49,-1.33,.38),(.49,-1.33,.92)], .037, GREEN))
    p.extend(pipe('rear red supply riser', [(-1.39,-.25,.05),(-1.39,-.25,1.25),(-1.16,-.25,1.25)], .039, '#25070D'))
    p.extend(pipe('rust drain by pressure bottle', [(.40,-.53,1.01),(.40,-.53,.20),(.26,-.53,.20)], .034, RUST))
    for x in (-.95,-.41,.23):
        p.append(part('manifold blue clamp', (x-.035,-1.39,.34), (x+.035,-1.28,.42), BLUE_LIT))
    p.extend([part('small grey controller on lower header', (-.17,-1.386,.16), (.10,-1.34,.30), EDGE),
              part('header controller twin display left', (-.14,-1.392,.21), (-.09,-1.386,.27), '#C0C0C0'),
              part('header controller twin display right', (-.055,-1.392,.21), (-.005,-1.386,.27), '#C0C0C0')])
    return p


def main():
    models = []
    for suffix, state, parts, footprint in [('', 'sedimentation', main_machine(), (-1.49,-1.49,1.49,1.49)),
                                             ('Alt', 'sedimentation_A_1', alternate_machine(), (-1.49,-1.49,1.49,.49))]:
        for p in parts:
            assert footprint[0] <= p['min'][0] < p['max'][0] <= footprint[2], p
            assert footprint[1] <= p['min'][1] < p['max'][1] <= footprint[3], p
            p['min'] = ', '.join(f'{v:.6f}' for v in p['min'])
            p['max'] = ', '.join(f'{v:.6f}' for v in p['max'])
        assert len(parts) < 100, (suffix, len(parts))
        proto = 'RMCFiltrationSedimentation'+suffix
        models.append(dict(type='cmu3DModel', id='CMU3DFiltrationSedimentation'+suffix,
                           label='Single-vessel sedimentation filter' if suffix else 'Three-vessel sedimentation filter',
                           status='draft', sourcePrototypes=[proto], referencePrototype=proto,
                           referenceRsi=RSI, referenceState=state, sourceDirections=1, useEntityRotation=True,
                           description=('Source-palette blue pressure vessels, mint feed and collection pipes, pale framed '
                                        'brown filter bed, blue transfer elbow and exposed right-hand motor/control skid. '
                                        + ('One wide left vessel and small rear-right filter bed, south-shifted footprint. ' if suffix else
                                           'Three narrow rear vessels and wide front filter bed with a right service cabinet. ')
                                        + 'Static source sprite; original collider footprint preserved. Height, rear elevations and '
                                        'pipe depths inferred; draft needs in-map fidelity review. Source pixels CC-BY-SA-3.0; '
                                        'see 96x96.rsi/meta.json and SOURCES_FILTRATION.md.'), parts=parts))
    OUTPUT.write_text('# Source-guided sedimentation assemblies; dimensions above the footprint remain inferred.\n' +
                      yaml.safe_dump(models, sort_keys=False, width=110))
    library = build_models.load_models(OUTPUT)
    inventory = json.loads((ROOT / 'Tools/three_d/generated/inventory.json').read_text())
    build_models.write_reviews(library, ROOT / 'Tools/three_d/generated/review/sedimentation-tanks',
                               {e['id']:e for e in inventory['prototypes']})
    print('Sedimentation models:', [(m['id'],len(m['parts'])) for m in library])


if __name__ == '__main__':
    main()
