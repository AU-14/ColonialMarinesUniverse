"""Author the remaining Redux carpet using the existing connected-corner adapter."""
from pathlib import Path
import hashlib
import json

from PIL import Image, ImageDraw
import yaml

import build_models as bm
from layout import corner_parts

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'Resources/Textures/Structures/Furniture/Carpets/skyblue_carpet.rsi'
TEXTURES = ROOT / 'Content.CMU/Resources/Textures/CMU14/ThreeD/SkyBlueCarpet'
REFERENCE = TEXTURES.parent / 'SkyBlueCarpetReferences.rsi'
REVIEW = ROOT / 'Tools/three_d/generated/review/skyblue-carpet'
RECTS = ((16, 16, 32, 32), (0, 0, 16, 16), (16, 0, 32, 16), (0, 16, 16, 32))
QUADRANTS = (('SE', 0, -.5, .5, 0, 0), ('NE', 0, 0, .5, .5, 2),
             ('NW', -.5, 0, 0, .5, 1), ('SW', -.5, -.5, 0, 0, 3))


def main():
    for path in (TEXTURES, REFERENCE, REVIEW):
        path.mkdir(parents=True, exist_ok=True)
    slots, surfaces, rows, crops, frames = [], [], [], {}, {}
    hashes = {}
    isolated = Image.new('RGBA', (32, 32))
    metadata = json.loads((SOURCE / 'meta.json').read_text())
    for state in range(8):
        sheet = Image.open(SOURCE / f'carpet_{state}.png').convert('RGBA')
        assert sheet.size == (64, 64)
        for direction, rect in enumerate(RECTS):
            x, y = direction % 2 * 32, direction // 2 * 32
            frame = sheet.crop((x, y, x + 32, y + 32))
            assert frame.getbbox() == rect
            crop = frame.crop(rect)
            assert crop.getchannel('A').getextrema() == (255, 255)
            digest = hashlib.sha256(crop.tobytes()).hexdigest()
            if digest not in hashes:
                uid = f'CMU3DSkyBlueCarpetSurface{len(surfaces) + 1:02d}'
                hashes[digest] = uid
                crop.save(TEXTURES / (uid + '.png'))
                surfaces.append(dict(type='cmu3DSurface', id=uid, atlasIndex=1700 + len(surfaces),
                                     texture=f'/Textures/CMU14/ThreeD/SkyBlueCarpet/{uid}.png'))
                crops[uid] = crop
            uid = hashes[digest]
            slots.append(uid)
            frames[state, direction] = frame
            rows.append(dict(state=f'carpet_{state}', direction=direction, rect=rect, surface=uid, rgbaSha256=digest))
            if state == 0:
                isolated = Image.alpha_composite(isolated, frame)
    isolated.save(REFERENCE / 'isolated.png')
    metadata['states'] = [{'name': 'isolated'}]
    metadata['copyright'] += '; original quarter crops and isolated source composition for CMU 3D review.'
    (REFERENCE / 'meta.json').write_text(json.dumps(metadata, indent=2) + '\n')
    (TEXTURES / 'LICENSE.txt').write_text(metadata['license'] + '\n' + metadata['copyright'] + '\n')
    parts = [dict(label=f'{name} carpet quadrant', min=f'{l}, {b}, 0', max=f'{r}, {t}, 0.012',
                  color='#FFFFFF', surface=slots[direction], surfaceAxis='XY')
             for name, l, b, r, t, direction in QUADRANTS]
    model = dict(type='cmu3DModel', id='CMU3DSkyBlueCarpet', label='Sky blue carpet, connected corners',
                 status='draft', sourcePrototypes=['CarpetSBlue'], referencePrototype='CarpetSBlue',
                 referenceRsi='/Textures/CMU14/ThreeD/SkyBlueCarpetReferences.rsi', referenceState='isolated',
                 sourceDirections=1, cornerSurfaces=slots,
                 description='Original eight-neighbour corner artwork on four thin solid fabric quadrants. '
                             'Source borders and grid orientation are retained. Thickness 0.012 tiles is inferred; '
                             'the isolated reference is not every saved appearance. See SOURCES_SKYBLUE_CARPET.md.',
                 parts=parts)
    target = bm.SOURCE / 'garrison_skyblue_carpet.yml'
    target.write_text(yaml.safe_dump([model, *surfaces], sort_keys=False, width=110), encoding='utf-8')
    bm.surfaces.load_surfaces.cache_clear()
    model = bm.validate_model(model)
    montage = Image.new('RGB', (16 * 66, 16 * 80), '#15232b')
    draw = ImageDraw.Draw(montage)
    for mask in range(256):
        # Independently express the source CornerFill bits, rather than reusing the adapter's state selector.
        n, s, e, w, ne, se, sw, nw = [bool(mask & bit) for bit in (1, 2, 4, 8, 16, 32, 64, 128)]
        source_states = (int(e) + 2 * se + 4 * s, int(n) + 2 * ne + 4 * e,
                         int(w) + 2 * nw + 4 * n, int(s) + 2 * sw + 4 * w)
        original = Image.new('RGBA', (32, 32))
        actual = Image.new('RGBA', (32, 32))
        for part, state, direction in zip(corner_parts(model, mask), source_states, (0, 2, 1, 3)):
            original = Image.alpha_composite(original, frames[state, direction])
            actual.alpha_composite(crops[part['surface']], RECTS[direction][:2])
        assert original.tobytes() == actual.tobytes(), mask
        x, y = mask % 16 * 66, mask // 16 * 80
        montage.paste(actual.resize((64, 64), Image.Resampling.NEAREST), (x, y))
        draw.text((x, y + 65), str(mask), fill='white')
    montage.save(REVIEW / 'all-source-masks.png')
    bm.write_reviews([model], REVIEW, {})
    proof = dict(model=model['id'], source=str(SOURCE.relative_to(ROOT)), sourceQuarterCrops=rows,
                 uniqueCrops=len(surfaces), sourceMasksVerified=256, sourcePixelSamples=256 * 32 * 32,
                 partCount=4, heightInferred=.012, staticSourceStates=8, animationClipsAdded=0,
                 savedPlacementReview='Verified separately in the final batch scene audit.', fidelityApproved=False)
    (REVIEW / 'proof.json').write_text(json.dumps(proof, indent=2) + '\n')
    note = ROOT / 'Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_SKYBLUE_CARPET.md'
    note.write_text('# Sky blue connected carpet\n\n'
                    'Source: `Structures/Furniture/Carpets/skyblue_carpet.rsi`, made by Hqlle (github), '
                    'CC-BY-SA-3.0. The eight original four-direction CornerFill sheets supply 32 exact quarter '
                    'crops, deduplicated without resampling. All 256 neighbour masks reproduce the original pixels.\n\n'
                    'The existing corner adapter selects borders using anchored neighbours with the source smoothing '
                    'key and grid orientation. Four nonoverlapping solid quadrants use an inferred 0.012-tile fabric '
                    'thickness. The isolated reference is a composition of original state-zero quarters. No new '
                    'animation, damage or destruction controller is introduced. Source collision remains unchanged. '
                    'This model is a draft, and full live material/fidelity acceptance is unfinished.\n', encoding='utf-8')
    print(f'Sky blue carpet: 1 model, {len(surfaces)} exact crops, 256 source masks verified.', flush=True)


if __name__ == '__main__':
    main()
