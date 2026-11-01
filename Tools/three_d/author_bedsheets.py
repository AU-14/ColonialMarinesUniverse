"""Build the eight placed folded linen colors and source-matched review cards."""
import argparse
from collections import Counter
from copy import deepcopy
import hashlib
import json
from pathlib import Path

from PIL import Image
import yaml

import build_models as bm
import surfaces

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'Resources/Textures/_RMC14/Objects/Misc/bedsheets.rsi'
MODEL = ROOT / 'Content.CMU/Resources/ThreeD/Prototypes/World/garrison_bedsheets.yml'
ART = MODEL.with_name('garrison_bedsheets_art.yml')
OUT = ROOT / 'Tools/three_d/generated'
NOTE = ROOT / 'Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_BEDSHEETS.md'
COLORS = ('Blue', 'Brown', 'Gray', 'Green', 'Purple', 'Red', 'White', 'Yellow')


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def hex_color(pixel):
    return '#' + ''.join(f'{v:02X}' for v in pixel[:3])


def geometry(image, surface):
    dark, shade, body, light = [hex_color(image.getpixel(p)) for p in [(2, 10), (2, 11), (5, 15), (3, 15)]]
    stripe, stripe_shadow = [hex_color(image.getpixel(p)) for p in [(6, 15), (6, 23)]]
    parts = []

    def box(label, lo, hi, color, **extra):
        parts.append(dict(label=label, min=list(lo), max=list(hi), color=color, **extra))

    # The source has two horizontal layers, a rounded left fold and staggered
    # free ends at +X. Hidden depth is inferred; the source pivot is unchanged.
    box('lower folded cloth', (-.4375, -.203125, 0), (.21875, .203125, .024), body)
    box('recessed fold seam', (-.4375, -.191, .024), (.1875, .191, .030), shade)
    box('upper folded cloth', (-.4375, -.195, .030), (.1875, .195, .067), body)
    box('upper fold loft', (-.425, -.1875, .067), (.09375, .1875, .092), light)
    box('staggered free cloth end', (.09375, -.1875, .067), (.15625, .1875, .078), body)
    box('lower free hem', (.195, -.203125, .005), (.21875, .203125, .015), light)
    box('upper free hem', (.164, -.195, .044), (.1875, .195, .061), light)
    box('soft left folded edge', (-.453125, -.1875, .012), (-.425, .1875, .075), body)
    box('left lower seam', (-.453125, -.191, .025), (-.4375, .191, .030), dark)
    box('original upper cloth weave and stripes', (-.4375, -.1875, .092), (.09375, .1875, .095), '#FFFFFF',
        surface=surface, surfaceAxis='XY')
    # Trace the two source-colored bands across the exposed vertical folds too.
    for x in (-.3125, -.25):
        for y in (-.204, .197):
            box('colored lower hem band', (x, y, .006), (x + 1/32, y + .007, .024), stripe)
            box('colored upper hem band', (x, y + .009 if y < 0 else y - .01, .035),
                (x + 1/32, y + .016 if y < 0 else y - .003, .064), stripe)
        box('band at fold recess', (x, -.197, .024), (x + 1/32, -.19, .030), stripe_shadow)
    return parts


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--skip-reviews', action='store_true')
    args = parser.parse_args()
    assets = []

    def write(path, data):
        if args.check:
            assert path.read_bytes() == data, path
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
        assets.append(path)

    inventory = {p['id']: p for p in json.loads((OUT / 'inventory.json').read_text())['prototypes']}
    metadata = json.loads((SOURCE / 'meta.json').read_text())
    assert metadata['size'] == dict(x=32, y=32)
    models, art, evidence = [], [], []
    for index, color in enumerate(COLORS):
        uid, state = 'CMBedsheet' + color, 'sheet' + color.lower()
        sprite = inventory[uid]['sprite']
        assert sprite == dict(state=state, sprite='_RMC14/Objects/Misc/bedsheets.rsi', noRot=True, drawdepth='Items')
        assert next(s for s in metadata['states'] if s['name'] == state) == dict(name=state)
        image = Image.open(SOURCE / (state + '.png')).convert('RGBA')
        assert image.size == (32, 32)
        surface = 'CMU3DBedsheetWeave' + color
        filename = 'bedsheet_weave_' + color.lower() + '.png'
        crop = image.crop((2, 11, 19, 23))
        texture = ROOT / 'Content.CMU/Resources/Textures/CMU14/ThreeD/Surfaces' / filename
        write(texture, surfaces.png_bytes(crop))
        assert Image.open(texture).convert('RGBA').tobytes() == crop.tobytes()
        art.append(dict(type='cmu3DSurface', id=surface, atlasIndex=2300 + index,
                        texture='/Textures/CMU14/ThreeD/Surfaces/' + filename))
        models.append(dict(type='cmu3DModel', id='CMU3DBedsheet' + color,
            label=color + '-band folded linen', status='draft', sourcePrototypes=[uid],
            referencePrototype=uid, referenceRsi='_RMC14/Objects/Misc/bedsheets.rsi', referenceState=state,
            sourceDirections=1, useEntityRotation=False, groundOffset='0, 0', placement='surface',
            description='Folded linen with separate cloth layers, recessed seams, staggered hems and original colored bands. Source noRot and pivot retained. Hidden fold depth is inferred. Uses actual declared bed/table tops. See SOURCES_BEDSHEETS.md.',
            parts=geometry(image, surface)))
        evidence.append(dict(prototype=uid, state=state, sourceSha256=digest(SOURCE / (state + '.png')),
            sourceRgbaSha256=hashlib.sha256(image.tobytes()).hexdigest(), sourceBounds=list(image.getbbox()),
            crop=[2, 11, 19, 23], cropSha256=digest(texture), sourceNoRot=True, sourceOffset=[0, 0],
            sourceDirections=1, frames=1, instanceCounts=inventory[uid]['instanceCounts']))
    write(ART, ('# Original-source cloth crops; see SOURCES_BEDSHEETS.md.\n' + yaml.safe_dump(art, sort_keys=False)).encode())
    surfaces.load_surfaces.cache_clear()
    models = [bm.validate_model(m) for m in models]
    raw = deepcopy(models)
    for model in raw:
        model['groundOffset'] = '0, 0'
        for part in model['parts']:
            for key in ('min', 'max'):
                part[key] = ', '.join(f'{v:.8f}' for v in part[key])
    write(MODEL, ('# Generated by author_bedsheets.py; inferred folded cloth solids.\n' + yaml.safe_dump(raw, sort_keys=False, width=110)).encode())
    actual = bm.load_models(MODEL)
    note = '''# Placed folded linen colors

Eight source-specific models cover nine Redux and eight classic saved sheets. The source is the static single-direction `_RMC14/Objects/Misc/bedsheets.rsi`, with `Sprite.noRot=true`, zero offset and no folding-state owner. The eight original frames are the complete references. All bodies are tan linen; the colored bands, including brown/gray variants, are sampled from each original image rather than replacing the body with the color in the item name.

Separate lower/upper cloth layers, a recessed fold seam, rounded-in-profile left edge, staggered free hems and source-colored hem bands provide physical depth. Eight unmodified 17 by 12 source crops retain top cloth/stripe pixels. Source X extents guide the footprint and the source pivot stays at zero. Height (.095 tiles), hidden thickness, ground depth and cloth construction are inferred. These are folded-object drafts, not a new unfolding animation or bed-cover controller.

All saved sheets sit on an existing bed. `CMU3DBed` and `CMU3DRMCBedDingy` now declare their actual existing `Blanket source pixels` solid as a support: its top is .477. Placement adds the existing .002 gap, keeping the folded cloth above the bed instead of inside its frame. Those two bed geometries are unchanged. Sheets preserve all saved positions/yaws; source noRot ignores their four nonzero saved orientations. Duplicate co-located beds remain a source-map issue. Other loose surface props can use these real bed footprints too; final scene deltas record them.

The remaining four unplaced bedsheet resource variants, worn/equipped appearances, live movement/throwing and destruction states are outside this static batch. Original source copyright and license: CC-BY-SA-3.0, taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/106c92cdf232ebc12c9d7a2feb23956c6755496f/icons/obj/items/items.dmi . Geometry contributions are CC0-1.0 to the extent separately licensable; derived source appearance retains its source license.

Regenerate with `python Tools/three_d/author_bedsheets.py`; `--check --skip-reviews` verifies generated bytes without rewriting assets. Context and native checks are published separately with the batch.
'''
    write(NOTE, note.encode())
    if args.check:
        print(json.dumps(dict(models=8, byteIdenticalFiles=len(assets))))
        return
    if not args.skip_reviews:
        bm.write_reviews(actual, OUT / 'review/bedsheets', inventory)
    report = dict(assetChecksPass=True, models=8, parts=sum(len(m['parts']) for m in actual),
        textures=8, atlasIndices=list(range(2300, 2308)), source=metadata, sources=evidence,
        placements=dict(redux=9, classic=8), sourceCropPixels=8 * 17 * 12,
        writtenAssetSha256={p.relative_to(ROOT).as_posix(): digest(p) for p in assets},
        generatorSha256=digest(Path(__file__)), contextValidation='pending batch export',
        limitations=['Hidden fold thickness and ground depth are inferred.', 'Static source geometry does not prove every live appearance or state.'])
    (OUT / 'bedsheets-proof.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps({k: report[k] for k in ('models', 'parts', 'textures', 'placements')}))


if __name__ == '__main__':
    main()
