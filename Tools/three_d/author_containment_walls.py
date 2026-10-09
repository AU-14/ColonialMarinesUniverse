"""Author the seven fixed containment wall pieces used by Redux's level -2 cells."""
import json
from pathlib import Path

from PIL import Image
import yaml

import build_models

ROOT = Path(__file__).resolve().parents[2]
PROTOTYPES = ROOT / 'Content.CMU/Resources/ThreeD/Prototypes/World'
MODEL_FILE = PROTOTYPES / 'garrison_containment_walls.yml'
SURFACE_FILE = PROTOTYPES / 'garrison_containment_art.yml'
RSI = '_RMC14/Structures/Walls/containment.rsi'
SURFACE = 'CMU3DContainmentVentFace'
# Local faces use the game's cardinal convention: south=0, east=1, north=2, west=3.
# Interior sides were checked against all three saved cell enclosures, not camera facing.
PIECES = [
    ('South', 'RMCWallContainment', 'south', (2,)),
    ('North', 'RMCWallContainmentNorth', 'n', (0,)),
    ('West', 'RMCWallContainmentWest', 'w', (1,)),
    ('East', 'RMCWallContainmentEast', 'e', (3,)),
    ('Corner', 'RMCWallContainmentCorner', 'corner', (2, 3)),
    ('NorthWestJunction', 'RMCWallContainmentConnect4', 'connect_w2', (0, 1)),
    ('NorthEastJunction', 'RMCWallContainmentConnect5', 'connect_e2', (0, 3)),
]


def box(label, low, high, color, **extra):
    return dict(label=label, min=list(low), max=list(high), color=color, **extra)


def white_face():
    parts = [box('outer pale recessed backing', (-.48, -.47, .12), (.48, -.448, 2.29), '#AAA798'),
             box('outer lower sill', (-.5, -.5, 0), (.5, -.45, .12), '#898478'),
             box('outer upper rail', (-.5, -.5, 2.20), (.5, -.45, 2.30), '#C4C3BE')]
    # Shared stiles/rails form the same two recessed columns without four separate
    # overlapping frame bars per panel. Cell budgets include adjacent walls and roofs.
    for left, right in ((-.5, -.44375), (-.040625, .040625), (.44375, .5)):
        parts.append(box('outer shared panel stile', (left, -.495, .12), (right, -.45, 2.20), '#C4C3BE'))
    parts.append(box('outer course separator', (-.5, -.499, 1.73), (.5, -.46, 1.87), '#898478'))
    return parts


def brown_face():
    # The continuous brown core supplies panel beds. Only the raised construction
    # needs separate solids; overlapping backing/jambs waste cell entries.
    parts = [box('inner lower sill', (-.5, -.5, 0), (.5, -.45, .12), '#3A2E21')]
    for left, right in ((-.43, -.403), (-.067, .067), (.403, .43)):
        parts.append(box('inner shared lower panel stile', (left, -.495, .18), (right, -.478, 1.39), '#4C3D2E'))
    for bottom, top in ((.18, .23), (1.34, 1.39)):
        parts.append(box('inner shared lower panel rail', (-.403, -.49, bottom), (.403, -.478, top), '#4C3D2E'))
    for z in (1.45, 1.61):
        parts.append(box('inner horizontal equipment rail', (-.48, -.495, z), (.48, -.46, z+.07), '#4C3D2E'))
    parts.append(box('inner shared upper cabinet flange', (-.445, -.488, 1.81), (.445, -.46, 2.21), '#4C3D2E'))
    for left, right in ((-.445, -.22), (-.18, .18), (.22, .445)):
        parts.append(box('inner upper cabinet face', (left+.025, -.495, 1.86), (right-.025, -.488, 2.16), '#413626'))
        parts.append(box('inner green status label', (right-.065, -.498, 1.86), (right-.035, -.495, 1.90), '#366D3C'))
    parts.append(box('inner lower green status label', (.305, -.482, .51), (.35, -.478, .585), '#3C8443'))
    return parts


def ventilation_face():
    return [box('vent lower molded lip', (-.5, -.5, 2.30), (.5, -.45, 2.36), '#898478'),
            box('original vent grille pixels', (-.46875, -.476, 2.365), (.46875, -.472, 2.645),
                '#FFFFFF', surface=SURFACE, surfaceAxis='XZ')]


def exterior_status_mark():
    return [box('outer white status stencil', (-.035, -.498, 1.97), (.035, -.495, 2.10), '#E2E0DC'),
            box('outer green status stroke', (-.035, -.5, 1.97), (.005, -.498, 2.045), '#3C8443'),
            box('outer green status upright', (.005, -.5, 2.01), (.035, -.498, 2.07), '#366D3C')]


def turn_face(parts, turn):
    result = []
    side = ('south', 'east', 'north', 'west')[turn]
    for part in parts:
        low, high = part['min'][:], part['max'][:]
        for _ in range(turn):
            low, high = [-high[1], low[0], low[2]], [-low[1], high[0], high[2]]
        p = {**part, 'label': side + ' ' + part['label'], 'min': low, 'max': high}
        if p.get('surface'):
            p['surfaceAxis'] = 'YZ' if turn % 2 else 'XZ'
            if turn in (1, 2):
                p['surfaceFlipU'] = True
        result.append(p)
    return result


def main():
    previous = {e['id']: e['atlasIndex'] for e in yaml.safe_load(SURFACE_FILE.read_text())} if SURFACE_FILE.exists() else {}
    next_index = 1 + max(e['atlasIndex'] for p in PROTOTYPES.glob('*.yml')
                        for e in yaml.safe_load(p.read_text()) if e['type'] == 'cmu3DSurface')
    surfaces = []
    for uid, state, crop in [(SURFACE, 'south', (1, 2, 31, 8)),
                             ('CMU3DContainmentNorthWestHatch', 'connect_w2', (13, 2, 24, 11)),
                             ('CMU3DContainmentNorthEastHatch', 'connect_e2', (8, 2, 19, 11))]:
        texture = ROOT / f'Content.CMU/Resources/Textures/CMU14/ThreeD/Surfaces/{uid}.png'
        source = Image.open(ROOT / 'Resources/Textures' / RSI / f'containment_wall_{state}.png').convert('RGBA')
        source.crop(crop).save(texture)
        if uid not in previous:
            previous[uid] = next_index
            next_index += 1
        surfaces.append(dict(type='cmu3DSurface', id=uid, atlasIndex=previous[uid],
                             texture=f'/Textures/CMU14/ThreeD/Surfaces/{uid}.png'))
    SURFACE_FILE.write_text('# Original CC-BY-SA-3.0 ventilation pixels; see SOURCES_CONTAINMENT_WALLS.md.\n' +
                           yaml.safe_dump(surfaces, sort_keys=False))
    models = []
    for name, proto, state, interior in PIECES:
        parts = [box('solid wall core', (-.468, -.468, .10), (.468, .468, 2.65), '#413626'),
                 box('continuous base course', (-.5, -.5, 0), (.5, .5, .10), '#565545'),
                 box('continuous pale cap and vent upper lip', (-.5, -.5, 2.65), (.5, .5, 2.76), '#C4C3BE'),
                 box('inset upper cap', (-.46875, -.46875, 2.76), (.46875, .46875, 2.79), '#D2D0CA')]
        for face in range(4):
            parts.extend(turn_face((brown_face() if face in interior else white_face()) + ventilation_face(), face))
        marked = {'West': 3, 'East': 1, 'Corner': 1, 'NorthWestJunction': 3, 'NorthEastJunction': 1}
        if name in marked:
            parts.extend(turn_face(exterior_status_mark(), marked[name]))
        if name.endswith('Junction'):
            west = name == 'NorthWestJunction'
            left, right = (-.09375, .25) if west else (-.25, .09375)
            parts.append(box('source connector inspection hatch', (left, .15625, 2.79), (right, .4375, 2.8),
                             '#FFFFFF', surface='CMU3DContainmentNorthWestHatch' if west else 'CMU3DContainmentNorthEastHatch',
                             surfaceAxis='XY'))
        for p in parts:
            # A narrow panel seam keeps detailed trim inside its own tile under the
            # ray grid's conservative padding. Continuous base/cap still span a tile.
            if p['label'] not in ('continuous base course', 'continuous pale cap and vent upper lip'):
                for key in ('min', 'max'):
                    p[key][:2] = [v * .99 for v in p[key][:2]]
            for key in ('min', 'max'):
                p[key] = ', '.join(f'{v:.7f}' for v in p[key])
        models.append(dict(type='cmu3DModel', id='CMU3DContainment'+name, label='Containment '+name+' wall',
                           status='draft', sourcePrototypes=[proto], referencePrototype=proto,
                           referenceRsi=RSI, referenceState='containment_wall_'+state,
                           sourceDirections=4 if name == 'Corner' else 1, useEntityRotation=True,
                           description='Fixed full-tile wall with source-palette pale outer panels and brown interior '
                                       'service cabinets. Interior facing follows the saved Redux cell layout; corners '
                                       'rotate with the entity and never auto-connect. Recessed panels, flanges and original '
                                       'vent pixels have physical depth. 2.8-tile height, unseen side elevations, cap and '
                                       'recess dimensions remain inferred. Static source; see SOURCES_CONTAINMENT_WALLS.md.',
                           parts=parts))
    MODEL_FILE.write_text('# Fixed source pieces: no IconSmooth. See SOURCES_CONTAINMENT_WALLS.md.\n' +
                          yaml.safe_dump(models, sort_keys=False, width=110))
    library = build_models.load_models(MODEL_FILE)
    inventory = json.loads((ROOT / 'Tools/three_d/generated/inventory.json').read_text())
    build_models.write_reviews(library, ROOT / 'Tools/three_d/generated/review/containment-walls',
                               {e['id']: e for e in inventory['prototypes']})
    print('Authored', len(library), 'fixed wall pieces;', [len(m['parts']) for m in library], 'parts.')


if __name__ == '__main__':
    main()
