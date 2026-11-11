#!/usr/bin/env python3
"""Inspect the placed reinforced plasteel barricade's source state combinations.

This produces 2D reference material, not model exports or runtime verification.
Read complete saved components for this prototype so unsupported fields are not
silently discarded by the general scene reader's component allowlist.
"""
from __future__ import annotations

import hashlib
import json
import math
from collections import Counter
from pathlib import Path

from PIL import Image, ImageDraw

import inventory as inv

ROOT = Path(__file__).resolve().parents[2]
TARGET = 'RMCBarricadeBrutePlasteel'
DIRECTIONS = ('South', 'North', 'East', 'West')
WALLS = 'Resources/Textures/_RMC14/Structures/Walls/Barricades'
SOURCES = (
    'Resources/Prototypes/_RMC14/Entities/Structures/Walls/Barricades/barricade_plasteel.yml',
    'Resources/Prototypes/_RMC14/Entities/Structures/Walls/Barricades/barricade_base.yml',
    'Content.Client/Damage/DamageVisualsSystem.cs',
    'Content.Client/Damage/DamageVisualsComponent.cs',
    'Content.Shared/_RMC14/Barricade/SharedBarbedSystem.cs',
    'Content.Shared/_RMC14/Barricade/Components/BarbedComponent.cs',
    'Content.Server/_RMC14/Barricade/BarbedSystem.cs',
    'Content.Shared/_RMC14/Xenonids/Spray/XenoSprayAcidSystem.cs',
    'Content.Shared/_RMC14/Xenonids/Spray/SprayAcidedComponent.cs',
    'RobustToolbox/Robust.Client/GameObjects/EntitySystems/GenericVisualizerSystem.cs',
    'RobustToolbox/Robust.Client/ResourceManagement/ResourceTypes/RSIResource.cs',
)


def saved_entities(path):
    block = []
    reading = False
    with path.open(encoding='utf-8-sig') as stream:
        for line in stream:
            if line.startswith('- proto:'):
                if reading:
                    break
                reading = line.strip() == '- proto: ' + TARGET
            if reading:
                block.append(line)
    return inv.load_yaml(''.join(block))[0]['entities'] if block else []


def source_state(directory, state):
    meta = json.loads((directory / 'meta.json').read_text(encoding='utf-8-sig'))
    definition = next(row for row in meta['states'] if row['name'] == state)
    width, height = meta['size']['x'], meta['size']['y']
    count = definition.get('directions', 1)
    delays = definition.get('delays')
    frame_counts = [len(row) for row in delays] if delays else [1] * count
    atlas = Image.open(directory / (state + '.png')).convert('RGBA')
    columns = atlas.width // width
    assert count == 4 and atlas.width % width == 0 and atlas.height % height == 0
    assert sum(frame_counts) <= columns * (atlas.height // height)
    frames = []
    index = 0
    # RSI image cells are consecutive frames per direction, S/N/E/W.
    for frame_count in frame_counts:
        directional = []
        for _ in range(frame_count):
            x, y = index % columns * width, index // columns * height
            directional.append(atlas.crop((x, y, x + width, y + height)))
            index += 1
        frames.append(directional)
    evidence = {'rsi': directory.relative_to(ROOT).as_posix(), 'state': state,
                'directions': count, 'framesPerDirection': frame_counts,
                'delaysSeconds': delays, 'license': meta['license'], 'copyright': meta['copyright'],
                'imageSha256': hashlib.sha256((directory / (state + '.png')).read_bytes()).hexdigest()}
    return frames, evidence


def main():
    output = ROOT / 'Tools/three_d/generated/plasteel-state-review'
    output.mkdir(parents=True, exist_ok=True)
    inventory = json.loads((output.parent / 'inventory.json').read_text())
    kinds, issues, _ = inv.load_prototypes(ROOT)
    assert not issues, issues
    components = inv.component_map(inv.Resolver(kinds['entity']).resolve(TARGET))
    assert not {'Door', 'Foldable', 'RMCFoldingBarricade'} & components.keys()
    damage = components['DamageVisuals']
    assert damage['trackAllDamage'] and not damage['overlay'] and not damage['hideIfZero']
    thresholds = sorted({0, *damage['thresholds']})
    assert thresholds == [0, 4, 8, 12]
    assert len(damage['targetLayers']) == 2
    assert [layer['map'] for layer in components['Sprite']['layers']] == [
        ['enum.RMCDamageOverlayVisuals.DamageOverlay'],
        ['enum.RMCDamageOverlayVisuals.AdditionalDamageOverlay'], ['acided']]
    # GenericVisualizer reserves the absent wire layer after the three base layers.
    # Thus normal runtime order is body, reinforcement, acid, then wire.
    assert components['GenericVisualizer']['visuals']['enum.BarbedWireVisualLayers.Wire']['barbWired']['WiredClosed']['state'] == 'plasteel_wire'
    assert components['GenericVisualizer']['visuals']['enum.SprayAcidedVisuals.Acided']['acided'][True]['state'] == 'acid'
    resources, bodies, braces = [], {}, {}
    for threshold in thresholds:
        bodies[threshold], evidence = source_state(ROOT / WALLS / 'plasteel_barricade_cracks.rsi', f'DamageOverlay_{threshold}')
        resources.append(evidence)
        braces[threshold], evidence = source_state(ROOT / WALLS / 'brute_barricade_cracks.rsi', f'AdditionalDamageOverlay_{threshold}')
        resources.append(evidence)
    wire, evidence = source_state(ROOT / WALLS / 'barricade.rsi', 'plasteel_wire')
    resources.append(evidence)
    acid, evidence = source_state(ROOT / 'Resources/Textures/_RMC14/Effects/xeno_spray_acid.rsi', 'acid')
    resources.append(evidence)
    assert evidence['framesPerDirection'] == [5] * 4
    assert evidence['delaysSeconds'] == [[.1] * 5] * 4
    frames = []
    # One readable sheet per direction: four damage levels x two wire choices,
    # each showing the dry pose and all five acid strip frames.
    for direction, name in enumerate(DIRECTIONS):
        sheet = Image.new('RGB', (992, 1256), '#151e28')
        draw = ImageDraw.Draw(sheet)
        draw.text((16, 12), f'{TARGET} / {name} / SOURCE ART ONLY', fill='#f4f6f8')
        draw.text((16, 30), 'Rows: damage 0/4/8/12, unwired then wired. Columns: dry, acid frames 0..4.', fill='#bccddd')
        for ti, threshold in enumerate(thresholds):
            for barbed in (False, True):
                row = ti * 2 + int(barbed)
                for acid_frame in range(-1, 5):
                    composed = Image.alpha_composite(bodies[threshold][direction][0], braces[threshold][direction][0])
                    if acid_frame >= 0:
                        composed = Image.alpha_composite(composed, acid[direction][acid_frame])
                    if barbed:
                        composed = Image.alpha_composite(composed, wire[direction][0])
                    key = f'{name.lower()}-damage{threshold}-wire{int(barbed)}-acid{acid_frame}'
                    composed.save(output / (key + '.png'))
                    frames.append({'key': key, 'direction': name, 'damageThreshold': threshold,
                                   'wired': barbed, 'acidFrame': None if acid_frame < 0 else acid_frame,
                                   'image': key + '.png', 'rgbaSha256': hashlib.sha256(composed.tobytes()).hexdigest()})
                    x, y = 16 + (acid_frame + 1) * 162, 64 + row * 148
                    draw.text((x, y), f'D{threshold} W{int(barbed)} ' + ('dry' if acid_frame < 0 else f'acid {acid_frame}'), fill='#dce8f0')
                    preview = composed.resize((128, 128), Image.Resampling.NEAREST)
                    sheet.paste(preview, (x, y + 16), preview)
        sheet.save(output / f'{name.lower()}-source-states.png')
    placements = []
    for map_entry in inventory['maps']:
        records = saved_entities(ROOT / map_entry['path'])
        assert len(records) == map_entry['prototypeCounts'].get(TARGET, 0), map_entry['path']
        if not records:
            continue
        counts = Counter()
        for record in records:
            saved = {c['type']: c for c in record.get('components', [])}
            transform = saved.get('Transform', {})
            rotation = str(transform.get('rot', 0)).strip()
            degrees = math.degrees(float(rotation[:-3])) if rotation.endswith('rad') else float(rotation)
            counts[str(round(degrees % 360, 4))] += 1
        placements.append({'map': map_entry['path'], 'variant': map_entry['variant'], 'count': len(records),
                           'savedRotationDegrees': dict(counts), 'savedComponentTypes': sorted({c['type'] for r in records for c in r.get('components', [])}),
                           'entities': records})
    report = {'schemaVersion': 1, 'prototype': TARGET, 'status': 'source reference generation only; current 3D implementation is tracked in plasteel-verification.json',
              'sourceCompositions': len(frames), 'threeDStateCompositionsAdded': 0, 'animationClipsAdded': 0,
              'foldable': False, 'damageThresholds': thresholds,
              'totalDamageBoundaries': [t * damage['damageDivisor'] for t in thresholds],
              'damageTracked': 'All damage; both body and reinforcement switch to the same threshold suffix.',
              'sourceLayerOrder': ['plasteel body', 'brute reinforcement', 'acid if visible', 'dynamically reserved wire if visible'],
              'acidStripDurationSeconds': .5, 'acidLifetime': 'SprayAcided.ExpireAt, refreshed by spray; not the strip duration. Removal or water clears the overlay.',
              'destruction': '900 damage normally. Server BarbedStateChanged adjusts the trigger by MaxHealthIncrease (default 50). Destruction removes the barricade and spawns three CMSheetPlasteel1.',
              'fixtureBounds': components['Fixtures']['fixtures']['fix1']['shape']['bounds'],
              'placements': placements, 'resolvedComponents': components, 'resources': resources, 'frames': frames,
              'sourceCode': [{'path': path, 'sha256': hashlib.sha256((ROOT / path).read_bytes()).hexdigest()} for path in SOURCES],
              'limitations': ['These are source sprite compositions, not 3D geometry or native playback verification.',
                             'Do not count 192 source compositions as models, state completion or exported clips.',
                             'World orientation, fixture fit and native sprite-layer composition still require interactive review.',
                             'Other overlays, corrosive melting, burning, upgrade/downgrade replacements and spawned debris need separate review.',
                             'This reference report reads every saved component on the target; the offline model adapter supports only the reviewed default dry pose without relevant overrides.']}
    (output / 'report.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    (output / 'LICENSE.txt').write_text('\n\n'.join(f"{r['rsi']}/{r['state']}\n{r['license']}\n{r['copyright']}" for r in resources)
                                        + '\n\nCompositions retain original source pixels. No 3D model or animation export is produced.\n', encoding='utf-8')
    print(json.dumps({'sourceCompositions': len(frames), 'savedInstances': sum(p['count'] for p in placements),
                      'maps': [{k: v for k, v in p.items() if k != 'entities'} for p in placements],
                      'threeDStatesAdded': 0, 'animationClipsAdded': 0}))


if __name__ == '__main__':
    main()
