"""Author the placed semiotic plates from their source silhouettes and unmodified printed faces."""
import json
from pathlib import Path

from PIL import Image
import yaml

import build_models

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'Resources/Textures/_RMC14/Structures/Wallmounts/semiotics.rsi'
PROTOTYPES = ROOT / 'Content.CMU/Resources/Prototypes/CMU14/ThreeD'
TEXTURES = ROOT / 'Content.CMU/Resources/Textures/CMU14/ThreeD/Surfaces'
MODEL_FILE = PROTOTYPES / 'garrison_semiotic_signs.yml'
SURFACE_FILE = PROTOTYPES / 'garrison_semiotic_art.yml'
# Actual backing wall families found around the 43 Redux placements. Connected windows
# remain under the existing wall/opening placement rules rather than being treated as blocks.
BACKING_WALLS = ['RMCWallElevatorNoConnect8', 'RMCWallHybrisaEngi', 'RMCWallHybrisaMedical',
                 'RMCWallHybrisaRock', 'RMCWallKutjevoRockBorder', 'RMCWallPrisonHull',
                 'RMCWallPrisonReinforced', 'RMCWallSPPGreyReinforced', 'RMCWallStrata']


def part(label, low, high, color, **extra):
    return {'label': label, 'min': ', '.join(f'{v:.7f}' for v in low),
            'max': ', '.join(f'{v:.7f}' for v in high), 'color': color, **extra}


def main():
    inventory = json.loads((ROOT / 'Tools/three_d/generated/inventory.json').read_text())
    entries = sorted((e for e in inventory['prototypes']
                      if '/Textures/_RMC14/Structures/Wallmounts/semiotics.rsi' in e['resources']),
                     key=lambda e: e['id'])
    previous = {e['id']: e['atlasIndex'] for e in yaml.safe_load(SURFACE_FILE.read_text())} if SURFACE_FILE.exists() else {}
    indices = [e['atlasIndex'] for p in PROTOTYPES.glob('*.yml')
               for e in yaml.safe_load(p.read_text(encoding='utf-8')) if e['type'] == 'cmu3DSurface']
    next_index = max(indices, default=0) + 1
    models, surfaces, evidence = [], [], []
    for entry in entries:
        proto, state = entry['id'], entry['sprite']['state']
        uid = 'CMU3DSemiotic' + proto.removeprefix('CMSemiotic').replace('_', '')
        surface = uid + 'Face'
        image = Image.open(SOURCE / (state + '.png')).convert('RGBA')
        assert image.size == (32, 32), 'Only single-frame source signs are supported.'
        bounds = image.getbbox()
        left, top, right, bottom = bounds
        image.crop(bounds).save(TEXTURES / (surface + '.png'))
        if surface not in previous:
            previous[surface] = next_index
            next_index += 1
        surfaces.append({'type': 'cmu3DSurface', 'id': surface, 'atlasIndex': previous[surface],
                         'texture': '/Textures/CMU14/ThreeD/Surfaces/' + surface + '.png'})

        # Keep the source's lateral pivot, including its visibly left-offset symbol.
        # 32 pixels/tile; 1.9 is the inferred plate center height, not a world Y shift.
        x = lambda pixel: (pixel - 16) / 32
        z = lambda pixel: 1.9 + (16 - pixel) / 32
        strips = []
        for row in range(top, bottom):
            columns = [col for col in range(left, right) if image.getpixel((col, row))[3]]
            assert columns == list(range(min(columns), max(columns) + 1)), 'Plate backing requires a solid row.'
            span = (min(columns), max(columns) + 1)
            if strips and strips[-1][:2] == span:
                strips[-1] = (*span, strips[-1][2], row + 1)
            else:
                strips.append((*span, row, row + 1))
        parts = [part('clipped metal plate backing', (x(a), -.526, z(d)), (x(b), -.501, z(c)), '#626052')
                 for a, b, c, d in strips]
        parts.append(part('original printed face', (x(left), -.528, z(bottom)), (x(right), -.526, z(top)),
                          '#FFFFFF', surface=surface, surfaceAxis='XZ'))
        models.append({'type': 'cmu3DModel', 'id': uid, 'label': state.replace('_', ' ') + ' semiotic plate',
                       'status': 'draft', 'sourcePrototypes': [proto], 'referencePrototype': proto,
                       'referenceRsi': '_RMC14/Structures/Wallmounts/semiotics.rsi', 'referenceState': state,
                       'sourceDirections': 1, 'wallMounted': True, 'useEntityRotation': True,
                       'backWallMountTargets': BACKING_WALLS, 'fitInsideWall': True,
                       'description': 'Clipped solid plate with original printed pixels, source lateral pivot and wall-face mounting. '
                                      'Thickness, back material and 1.9-tile mounting height remain inferred. Static single-frame source.',
                       'parts': parts})
        evidence.append({'prototype': proto, 'model': uid, 'sourceState': state, 'sourceBounds': bounds,
                         'sourcePixelsUnmodified': True, 'parts': len(parts), 'instanceCounts': entry['instanceCounts']})
    MODEL_FILE.write_text('# Source and mounting assumptions: SOURCES_SEMIOTIC_SIGNS.md.\n' + yaml.safe_dump(models, sort_keys=False))
    SURFACE_FILE.write_text('# Unmodified CC-BY-SA-3.0 source crops; see SOURCES_SEMIOTIC_SIGNS.md.\n' + yaml.safe_dump(surfaces, sort_keys=False))
    build_models.write_reviews(build_models.load_models(MODEL_FILE),
                               ROOT / 'Tools/three_d/generated/review/semiotic-signs',
                               {e['id']: e for e in inventory['prototypes']})
    (ROOT / 'Tools/three_d/generated/semiotic-source-audit.json').write_text(json.dumps(evidence, indent=2) + '\n')
    print(f'Authored {len(models)} solid sign plates and source comparison cards.')


if __name__ == '__main__':
    main()
