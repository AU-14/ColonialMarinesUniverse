"""Stage or apply three source-matched desk terminals and verify their saved contexts."""
import argparse
from collections import Counter
from copy import deepcopy
import hashlib
import json
import math
from pathlib import Path
import sys

from PIL import Image, ImageDraw, ImageFont
import yaml

ROOT = next(p for p in Path(__file__).resolve().parents if (p / 'Tools/three_d/build_models.py').is_file())
sys.path.insert(0, str(ROOT / 'Tools/three_d'))
import build_models
import placement
import surfaces
from author_wide_machinery import world_parts, contacts, box_bounds

STAGE = ROOT / '.codex/large-computers-staged'
MODEL_REL = Path('Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_large_computers.yml')
ART_REL = MODEL_REL.with_name('garrison_large_computers_art.yml')
TEXTURE_REL = Path('Content.CMU/Resources/Textures/CMU14/ThreeD/Surfaces')
NOTES_REL = Path('Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_LARGE_COMPUTERS.md')
AUDIT = ROOT / 'Tools/three_d/generated/next-large-computers-audit.json'
SOURCE = ROOT / 'Resources/Textures/_RMC14/Structures/Machines/computer.rsi'
VARIANTS = [('RMCPropComputerLarge', 'largecomp', 'Beige desk computer'),
            ('RMCPropComputerLarge0', 'largecomp0', 'Blank-screen desk computer'),
            ('RMCPropComputerLargeDark', 'largecomp_dark', 'Dark desk computer')]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


class Pool:
    def __init__(self, output):
        self.output, self.entries, self.crops, self.hashes = output, [], [], {}
        registry = ROOT / 'Content.CMU/Resources/Prototypes/CMU14/ThreeD'
        paths = [p for p in registry.glob('*.yml') if p.name != ART_REL.name]
        for stage in (ROOT / '.codex').glob('*staged*'):
            if stage != STAGE:
                paths.extend(stage.rglob('*.yml'))
        self.used = {}
        for path in paths:
            for entry in yaml.safe_load(path.read_text(encoding='utf-8-sig')) or []:
                if entry.get('type') == 'cmu3DSurface':
                    self.used.setdefault(entry['atlasIndex'], []).append(str(path.relative_to(ROOT)))
        assert not any(i in self.used for i in range(1360, 1400)), 'Reserved computer atlas range conflicts with another batch.'

    def store(self, image, context):
        signature = hashlib.sha256(str(image.size).encode() + image.tobytes()).hexdigest()
        if signature not in self.hashes:
            index = 1360 + len(self.entries)
            assert index < 1400 and index not in self.used
            uid = f'CMU3DLargeComputerSurface{index}'
            path = self.output / TEXTURE_REL / (uid + '.png')
            path.parent.mkdir(parents=True, exist_ok=True)
            image.save(path)
            self.entries.append({'type': 'cmu3DSurface', 'id': uid, 'atlasIndex': index,
                                 'texture': f'/Textures/CMU14/ThreeD/Surfaces/{uid}.png'})
            self.hashes[signature] = uid
        uid = self.hashes[signature]
        written = Image.open(self.output / TEXTURE_REL / (uid + '.png')).convert('RGBA')
        assert written.size == image.size and written.tobytes() == image.tobytes()
        self.crops.append({**context, 'surface': uid, 'size': list(image.size),
                           'rgbaSha256': hashlib.sha256(image.tobytes()).hexdigest()})
        return uid


def compose(state, pool):
    image = Image.open(SOURCE / (state + '.png')).convert('RGBA')
    parts, source_regions = [], []
    reconstructed = Image.new('RGBA', (32, 32))
    color = lambda x, y: '#' + ''.join(f'{c:02X}' for c in image.getpixel((x, y))[:3])

    def box(label, lo, hi, tint, **extra):
        parts.append({'label': label, 'min': list(lo), 'max': list(hi), 'color': tint, **extra})

    def crop(label, rect, hole=None):
        pixels = image.crop(rect)
        if hole:
            ImageDraw.Draw(pixels).rectangle((hole[0]-rect[0], hole[1]-rect[1],
                                              hole[2]-rect[0]-1, hole[3]-rect[1]-1), fill=(0, 0, 0, 0))
        reconstructed.alpha_composite(pixels, (rect[0], rect[1]))
        source_regions.append({'label': label, 'rect': list(rect), 'hole': list(hole) if hole else None})
        return pool.store(pixels, {'state': state, 'region': label, 'rect': list(rect), 'hole': hole})

    # Inferred physical monitor depth; stepped X/Z outline follows source rows 6..21.
    box('monitor rear casing', (-.3125, .03, .3125), (.3125, .18, .75), color(9, 8))
    box('stepped upper casing', (-.28125, -.105, .75), (.28125, .18, .78125), color(9, 8))
    box('rounded crown step', (-.25, -.105, .78125), (.25, .18, .8125), color(9, 8))
    box('upper bezel housing', (-.3125, -.105, .625), (.3125, .03, .75), color(9, 8))
    box('left bezel housing', (-.3125, -.105, .40625), (-.1875, .03, .625), color(7, 12))
    box('right bezel housing', (.1875, -.105, .40625), (.3125, .03, .625), color(24, 12))
    box('lower bezel housing', (-.3125, -.105, .3125), (.3125, .03, .40625), color(9, 20))
    screen = (10, 12, 22, 19)
    monitor = crop('monitor casting', (6, 6, 26, 22), screen)
    box('original stepped monitor face', (-.3125, -.109, .3125), (.3125, -.106, .8125),
        '#FFFFFF', surface=monitor, surfaceAxis='XZ')
    display = crop('recessed display', screen)
    box('recessed display glass', (-.1875, -.060, .40625), (.1875, -.057, .625),
        '#FFFFFF', surface=display, surfaceAxis='XZ')

    # Separate terminal hinge and a solid sloping keyboard deck, not a full-image plate.
    hinge = crop('hinge and lower monitor rail', (6, 22, 26, 24))
    box('hinge rail', (-.3125, -.085, .25), (.3125, .05, .3125),
        '#FFFFFF', surface=hinge, surfaceAxis='XZ')
    keyboard = crop('keyboard deck', (6, 24, 26, 30))
    box('sloping original keyboard deck', (-.3125, -.30, .0625), (.3125, .025, .25),
        '#FFFFFF', shape='WedgeY', surface=keyboard, surfaceAxis='XY')
    box('keyboard underside', (-.28125, -.30, .03125), (.28125, .16, .0625), color(9, 30))
    foot = crop('lower foot and bevel', (6, 30, 26, 32))
    box('original front base bevel', (-.3125, -.303, 0), (.3125, -.30, .0625),
        '#FFFFFF', surface=foot, surfaceAxis='XZ')
    box('source-width supporting foot', (-.25, -.295, 0), (.25, .15, .03125), color(10, 31))
    box('rear keyboard platform', (-.28125, .025, .0625), (.28125, .16, .25), color(8, 25))
    assert reconstructed.tobytes() == image.tobytes(), state
    return parts, {'state': state, 'sourceRegions': source_regions,
                   'sourceRgbaReassembled': True, 'opaquePixels': 508,
                   'sourceRgbaSha256': hashlib.sha256(image.tobytes()).hexdigest()}


def stage_registry(pool):
    merged = dict(surfaces.load_surfaces())
    for entry in pool.entries:
        path = pool.output / TEXTURE_REL / (entry['id'] + '.png')
        merged[entry['id']] = {**entry, 'file': path, 'image': Image.open(path).convert('RGBA')}
    surfaces.load_surfaces = lambda: merged


def review(models, audit, output):
    destination = (ROOT / 'Tools/three_d/generated/large-computers-review'
                   if output == ROOT else STAGE / 'review')
    destination.mkdir(exist_ok=True, parents=True)
    font = ImageFont.load_default(size=17)
    small = ImageFont.load_default(size=13)
    for model in models:
        card = Image.new('RGB', (1150, 490), '#17212B')
        draw = ImageDraw.Draw(card)
        draw.text((18, 12), model['label'] + ' / source and three solid views', fill='white', font=font)
        draw.text((18, 39), 'Original pixels; inferred depth and keyboard slope. Draft, not a fidelity approval.', fill='#B8CDD8', font=small)
        original = Image.open(SOURCE / (model['referenceState'] + '.png')).convert('RGBA').resize((288, 288), Image.Resampling.NEAREST)
        card.paste(original, (10, 85), original)
        for i, (yaw, pitch) in enumerate([(-math.pi/2, .25), (-math.pi/3, .55), (math.pi/3, .45)]):
            panel = build_models.render_model(model, (275, 340), yaw, pitch, pixels_per_unit=310,
                                               screen_origin=(137, 270))
            card.paste(panel, (310 + i*280, 80))
        draw.text((18, 450), '0.625 tile source width; 0.8125 tile source height reference; approximately 0.48 tile inferred depth.', fill='#B8CDD8', font=small)
        card.save(destination / (model['id'] + '.png'))

    library = {m['id']: m for m in json.loads((ROOT / 'Tools/three_d/generated/models.json').read_text())['models']}
    direct = {m['sourcePrototypes'][0]: m for m in models}
    library.update({m['id']: m for m in models})
    contexts = []
    scenes = {}
    for level, label in [(-1, 'minus1'), (-2, 'minus2'), (1, 'plus1')]:
        doc = json.loads((ROOT / f'Tools/three_d/generated/wide-redux-{label}-scene.json').read_text())
        for entity in doc['instances']:
            if entity['prototype'] in direct:
                entity.update(modelId=direct[entity['prototype']]['id'], matchKind='exact', renderYaw=entity['yaw'])
        placement.resolve_placements(doc['instances'], list(library.values()), doc['geometryVariants'])
        scenes[level] = doc
    for record in audit['placements']:
        doc = scenes[record['level']]
        indexed = {i['id']: i for i in doc['instances']}
        entity = indexed[record['id']]
        model = direct[record['prototype']]
        assert entity['position'] == record['position'] and entity['yaw'] == record['yaw']
        assert entity['renderYaw'] == record['yaw']
        expected = record['selectedIfPlacementSurfaceAtSavedPivot']
        assert entity['support']['entity'] == expected['entity']
        assert entity['support']['height'] == .86 and entity['renderOffset'][2] == .862
        candidate = world_parts(model['parts'], entity['position'], entity['renderYaw'], entity['renderOffset'])
        assembled = list(candidate)
        details, unknown = [], []
        px, py = entity['position'][:2]
        for neighbor in record['neighborsWithinTwoTiles']:
            placed = indexed[neighbor['id']]
            other_model = library.get(placed.get('modelId'))
            if other_model is None:
                unknown.append({'id': neighbor['id'], 'prototype': neighbor['prototype']})
                continue
            other = world_parts(doc['geometryVariants'].get(placed.get('geometryKey'), other_model['parts']),
                                placed['position'], placed.get('renderYaw', placed['yaw']), placed.get('renderOffset', [0, 0, 0]))
            hits, gap = contacts(candidate, other)
            details.append({'id': neighbor['id'], 'prototype': neighbor['prototype'],
                            'partAabbContacts': hits, 'minimumPartAabbSeparation': gap})
            if abs(neighbor['position'][0]-px) <= 1.6 and abs(neighbor['position'][1]-py) <= 1.6:
                assembled.extend(other)
        entry = {'id': entity['id'], 'prototype': entity['prototype'], 'level': record['level'],
                 'savedPosition': entity['position'], 'savedYaw': entity['yaw'], 'renderYaw': entity['renderYaw'],
                 'support': entity['support'], 'renderOffset': entity['renderOffset'],
                 'modeledNeighbors': details, 'unknownNeighbors': unknown,
                 'contactPairs': sum(len(n['partAabbContacts']) for n in details)}
        contexts.append(entry)
        for p in assembled:
            for key in ('min', 'max'):
                p[key][0] -= px
                p[key][1] -= py
        card = Image.new('RGB', (1200, 600), '#17212B')
        draw = ImageDraw.Draw(card)
        draw.text((18, 12), f'Redux {record["level"]:+d} / UID {entity["id"]} / support {entity["support"]["entity"]} at .86 / saved yaw {math.degrees(entity["yaw"]):.0f} degrees', fill='white', font=font)
        draw.text((18, 40), 'Actual saved neighbors and authored support. Table border rivets reach .869; terminals stay inside their rim.', fill='#B8CDD8', font=small)
        for i, yaw in enumerate([-math.pi/2 + .3, math.pi/2 + .3]):
            panel = build_models.render_model({'parts': assembled}, (585, 470), yaw, .55,
                                               pixels_per_unit=160, screen_origin=(285, 360))
            card.paste(panel, (10 + i*600, 75))
        draw.text((18, 566), f'{entry["contactPairs"]} conservative solid contact pairs; {len(unknown)} unknown neighboring objects retained in the audit.', fill='#B8CDD8', font=small)
        card.save(destination / f'context-{record["level"]}-{entity["id"]}.png')
    return contexts


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--apply', action='store_true', help='Write live asset files only after root authorizes the captured checkpoint.')
    args = parser.parse_args()
    output = ROOT if args.apply else STAGE
    audit = json.loads(AUDIT.read_text())
    for path, expected in audit['evidenceSha256'].items():
        assert sha(ROOT / path) == expected, f'Audit input changed: {path}'
    pool = Pool(output)
    models, proofs = [], []
    for proto, state, label in VARIANTS:
        parts, proof = compose(state, pool)
        proofs.append(proof)
        models.append({'type': 'cmu3DModel', 'id': 'CMU3D' + proto, 'label': label, 'status': 'draft',
                       'sourcePrototypes': [proto], 'referencePrototype': proto,
                       'referenceRsi': '_RMC14/Structures/Machines/computer.rsi', 'referenceState': state,
                       'referenceDirection': 0, 'sourceDirections': 1, 'useEntityRotation': True, 'yawOffset': 0,
                       'placement': 'surface', 'groundOffset': '0,0',
                       'description': 'Source-sized stepped CRT-style desk terminal with a recessed display, solid rear housing and a separate sloping keyboard deck. Exact source crops preserve the lit, blank and dark-casing variants. Source offset is zero and saved entity yaw is retained. Monitor depth, hidden faces, keyboard slope and physical interpretation of sprite height are inferred. Authored table support remains .86; no gameplay physics changes. Draft; see SOURCES_LARGE_COMPUTERS.md.',
                       'parts': parts})
    stage_registry(pool)
    for model in models:
        build_models.validate_model(model)
    serialized = deepcopy(models)
    for model in serialized:
        for part in model['parts']:
            # Robust vector fields use scalar comma-separated coordinates in resource YAML.
            for key in ('min', 'max'):
                part[key] = ', '.join(f'{v:.7f}' for v in part[key])
    for relative, data in [(MODEL_REL, serialized), (ART_REL, pool.entries)]:
        path = output / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text('# Generated source-matched desk terminal drafts; see SOURCES_LARGE_COMPUTERS.md.\n' + yaml.safe_dump(data, sort_keys=False, width=110), encoding='utf-8')
    # Validate the written files and reconstruct original source pixels from written PNGs.
    loaded = build_models.load_models(output / MODEL_REL)
    for generated, written in zip(models, loaded, strict=True):
        assert generated['id'] == written['id']
        for original, decoded in zip(generated['parts'], written['parts'], strict=True):
            for key in ('min', 'max'):
                assert tuple(original[key]) == tuple(decoded[key]), (generated['id'], key)
    for proto, state, _ in VARIANTS:
        reconstructed = Image.new('RGBA', (32, 32))
        for crop in [c for c in pool.crops if c['state'] == state]:
            pixels = Image.open(output / TEXTURE_REL / (crop['surface'] + '.png')).convert('RGBA')
            reconstructed.alpha_composite(pixels, tuple(crop['rect'][:2]))
        assert reconstructed.tobytes() == Image.open(SOURCE / (state + '.png')).convert('RGBA').tobytes()
    contexts = review(loaded, audit, output)
    notes = '''# Large desk computer drafts

Three exact source mappings cover six Redux placements: RMCPropComputerLarge (3), RMCPropComputerLarge0 (1), and RMCPropComputerLargeDark (2). The source is `Resources/Textures/_RMC14/Structures/Machines/computer.rsi`, states `largecomp`, `largecomp0`, and `largecomp_dark`. Each is one 32×32 frame and one direction, with zero Sprite offset and noRot=false. No new animation or power transition is invented.

The original art depicts a stepped monitor housing above a separate keyboard deck. These drafts use a thick rear case, four bezel solids around a physically recessed screen, stepped crown, hinge rail, sloping solid keyboard, and supporting base. Source width is 20 pixels / .625 tiles; the 26-pixel sprite height supplies a .8125-tile height reference. Depth (approximately .48 tiles), rear construction, keyboard slope, and the conversion from screen-space height to physical height are inferred. Source-colored planar crops are attached to those solids; the whole computer is not a single image plate.

All 508 opaque source pixels reassemble exactly from the on-disk crops. This verifies retained artwork, not an exact arbitrary-camera silhouette or automatic fidelity approval. Lit versus blank changes 19 display pixels; dark casing changes 282 pixels. All source alpha masks are identical.

The existing authored support selects table tops at .86, then adds a .002 gap above the model bottom. UID 10612 selects table 10615 over co-located 10616. Other support pairs are 10618→10617, 3094→687, 3095→701, 3096→703, and 4737→1042. Requisition-table border rivets reach .869; computer footprints stay inside the rim and context contact checks include those decorative parts. Saved positions and rotations are preserved, including UID 3094 at pi radians. Static, non-colliding, unanchored prop physics is unchanged.

Artwork license: CC-BY-SA-3.0. Source attribution from RSI metadata: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/d5b119380250ea512db2a5319e36592c7f604250/icons/obj/structures/machinery/computer.dmi; edits to overwatch and register by github noctyrnal. Derived crops retain original RGBA pixels. Keep this note and the RSI attribution with redistributed assets.

Review images show actual saved modeled neighbors at both front and rear angles. Unknown neighbors remain explicit. Conservative part AABB checks do not prove gameplay collision, native capacity, or player visibility. Global exports and native validation are coordinated separately; no game is launched by the generator.

Reproduce the dedicated assets with `Tools/three_d/author_large_computers.py` (staging by default; `--apply` writes the dedicated resources). The source/context evidence is `Tools/three_d/generated/large-computers-proof.json`; review cards are in `Tools/three_d/generated/large-computers-review/`. The generator verifies the recorded historical source-context inputs before recreating those cards.
'''
    path = output / NOTES_REL
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(notes, encoding='utf-8')
    report = {'schemaVersion': 1, 'status': 'staged drafts' if not args.apply else 'applied draft asset files',
              'models': [{'id': m['id'], 'parts': len(m['parts']), 'bounds': {'min': [min(p['min'][i] for p in m['parts']) for i in range(3)], 'max': [max(p['max'][i] for p in m['parts']) for i in range(3)]}} for m in loaded],
              'atlasIndices': [e['atlasIndex'] for e in pool.entries], 'uniqueTextures': len(pool.entries),
              'reservedAtlasRange': [1360, 1399], 'liveAndOtherStagedAtlasConflictCheck': True,
              'writtenSourceProof': proofs, 'sourceCrops': pool.crops, 'contexts': contexts,
              'sourceAudit': AUDIT.relative_to(ROOT).as_posix(), 'sourceAuditSha256': sha(AUDIT),
              'sourceReferences': {p.relative_to(ROOT).as_posix(): sha(p) for p in [SOURCE/'meta.json', *[SOURCE/(s+'.png') for _, s, _ in VARIANTS]]},
              'limitations': ['Hidden geometry and keyboard slope are inferred.', 'Original-art reassembly does not certify a 3D view as pixel-identical to the 2D source.', 'Native encoder admission and global deterministic export have not been run for these drafts.']}
    manifest = {str(p.relative_to(output)).replace('\\', '/'): sha(p) for p in [output/MODEL_REL, output/ART_REL, output/NOTES_REL, *[output/TEXTURE_REL/(e['id']+'.png') for e in pool.entries]]}
    report['writtenAssetsSha256'] = manifest
    report['generatorSha256'] = sha(Path(__file__))
    proof_path = (ROOT / 'Tools/three_d/generated/large-computers-proof.json'
                  if args.apply else STAGE / 'large-computers-proof.json')
    proof_path.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    manifest_path = (ROOT / 'Tools/three_d/generated/large-computers-assets.json'
                     if args.apply else STAGE / 'asset-manifest.json')
    manifest_path.write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'models': len(models), 'parts': [len(m['parts']) for m in loaded], 'textures': len(pool.entries),
                      'contexts': len(contexts), 'contactPairs': sum(c['contactPairs'] for c in contexts), 'applied': args.apply}))


if __name__ == '__main__':
    main()
