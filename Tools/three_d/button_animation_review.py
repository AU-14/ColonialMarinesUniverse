#!/usr/bin/env python3
"""Build source-pixel door-control animation studies, separate from accepted assets.

Source colors and silhouette are extruded into shallow solids. Depth is inferred;
this is a rotatable animation study, not a finished reconstruction or live adapter.
The existing map/library models are deliberately not promoted by this experiment.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image
import yaml

from build_models import ROOT, encode_glb, glb_document, validate_model
from frame_animation import add_clips

LENGTH = 1.25
# AnimationTrackSpriteFlick.KeyTime is relative to the previous keyframe.
# The third flick is at 1.5 s, outside RMCDoorVisualsSystem's 1.25 s animation.
FLICK_INTERVALS = (0.0, 0.5, 1.0, 1.25)


def frame_at(delays, seconds):
    """SpriteFlick clamps at state.AnimationLength - .01, rather than looping."""
    seconds = max(0, min(seconds, sum(delays) - .01))
    for index, delay in enumerate(delays):
        if seconds < delay - 1e-8:
            return index
        seconds -= delay
    return len(delays) - 1


def flick_timeline(delays):
    starts, elapsed = [], 0.0
    for interval in FLICK_INTERVALS:
        elapsed += interval
        if elapsed < LENGTH:
            starts.append(elapsed)
    changes = []
    for index, start in enumerate(starts):
        end = starts[index + 1] if index + 1 < len(starts) else LENGTH
        changes.append((start, 0))
        offset = 0.0
        for frame, delay in enumerate(delays[:-1], 1):
            offset += delay
            if start + offset < end - 1e-8:
                changes.append((round(start + offset, 8), frame))
    return changes


def extrude(image):
    """Merge equal-color horizontal pixels; preserve all occupied front-face pixels."""
    if image.size != (32, 32):
        raise ValueError('This study requires a 32 by 32 single-direction source frame')
    parts = []
    for y in range(32):
        x = 0
        while x < 32:
            pixel = image.getpixel((x, y))
            start = x
            x += 1
            while x < 32 and image.getpixel((x, y)) == pixel:
                x += 1
            if not pixel[3]:
                continue
            parts.append({'label': f'pixels-{start}-{x}-row-{y}',
                          'min': [(start - 16) / 32, -.035, 1.3 + (15 - y) / 32],
                          'max': [(x - 16) / 32, .025, 1.3 + (16 - y) / 32],
                          'color': '#' + ''.join(f'{c:02X}' for c in pixel)})
    return parts


def source_frames(path):
    meta = json.loads((path / 'meta.json').read_text(encoding='utf-8'))
    if meta['size'] != {'x': 32, 'y': 32}:
        raise ValueError('Unexpected door-control RSI size')
    frames = {}
    for state in meta['states']:
        if state.get('directions', 1) != 1:
            raise ValueError('Directional button art requires a separate study')
        delays = state.get('delays', [[1.0]])[0]
        sheet = Image.open(path / (state['name'] + '.png')).convert('RGBA')
        columns = sheet.width // 32
        if sheet.width % 32 or sheet.height % 32 or columns * (sheet.height // 32) < len(delays):
            raise ValueError('Invalid source frame sheet')
        frames[state['name']] = {'delays': delays, 'frames': [sheet.crop((i % columns * 32, i // columns * 32,
                                                                         (i % columns + 1) * 32, (i // columns + 1) * 32))
                                                              for i in range(len(delays))]}
    return meta, frames



def build_study(root, output):
    output.mkdir(parents=True, exist_ok=True)
    studies = []
    licenses = []
    source = root / 'Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_architecture.yml'
    authored = {m['id']: validate_model(m) for m in yaml.load(source.read_text(encoding='utf-8'), Loader=yaml.CSafeLoader)
                if m.get('doorButtonStates')} if source.is_file() else {}
    for name, model_id, label in [('door_button', 'CMU3DOrangeDoorButton', 'Small door control'),
                                  ('door_button_br', 'CMU3DRedDoorButton', 'Large red door control')]:
        path = root / 'Resources/Textures/_RMC14/Objects' / (name + '.rsi')
        meta, states = source_frames(path)
        library_model = authored.get(model_id)
        frames, parts, groups = {}, [], {}
        overlay = states['doorctrl-p']['frames'][0]
        for state, entry in states.items():
            for index, image in enumerate(entry['frames']):
                for powered in (True, False):
                    key = f'{state}:{index}:{"powered" if powered else "unpowered"}'
                    composed = image if powered else Image.alpha_composite(image, overlay)
                    geometry = (library_model['doorButtonStates'][state]['frames' if powered else 'unpoweredFrames'][index]['parts']
                                if library_model else extrude(composed))
                    filename = f'{name}-{state}-{index}-{int(powered)}.png'
                    composed.save(output / filename)
                    frames[key] = {'parts': geometry, 'sourceImage': filename, 'state': state, 'frameIndex': index,
                                   'powered': powered, 'rgbaSha256': hashlib.sha256(composed.tobytes()).hexdigest()}
                    groups[key] = list(range(len(parts), len(parts) + len(geometry)))
                    parts.extend(geometry)
        default = 'doorctrl:0:powered'
        clips = []
        for title, state, trigger in [('Press', 'doorctrl1', 'Successful powered use'),
                                       ('Denied', 'doorctrl-denied', 'Powered use denied by AccessReader')]:
            changes = [{'time': t, 'frame': f'{state}:{frame}:powered'} for t, frame in flick_timeline(states[state]['delays'])]
            changes.append({'time': LENGTH, 'frame': default})
            clips.append({'name': title, 'trigger': trigger, 'changes': changes, 'duration': LENGTH})
        document, binary = glb_document({'id': model_id + 'AnimationStudy', 'label': label + ' animation study',
                                        'status': 'draft', 'sourcePrototypes': [], 'parts': parts})
        binary = add_clips(document, binary, groups, clips, default)
        document['asset']['copyright'] = meta['copyright'] + '; ' + meta['license'] + '; see LICENSE.txt.'
        document['extras'].update(studyOnly=True, libraryModel=model_id, sourceRsi=path.relative_to(root).as_posix(),
                                 inferredDepth=.06, frameGroups=groups, gameplayIntegrated=False,
                                 usesLibraryFrameGeometry=library_model is not None)
        (output / f'{model_id}-animation-study.glb').write_bytes(encode_glb(document, binary))
        studies.append({'modelId': model_id, 'label': label, 'default': default, 'frames': frames, 'clips': clips,
                        'sourceRsi': path.relative_to(root).as_posix(), 'sourceStates': meta['states'],
                        'usesLibraryFrameGeometry': library_model is not None,
                        'previewTarget': [-.015, -.527, 1.335] if library_model else [-.015, 0, 1.3],
                        'glb': f'{model_id}-animation-study.glb', 'groupCount': len(groups), 'solidParts': len(parts)})
        licenses.append(f'{name}: all PNGs and derived GLB geometry\n{meta["license"]}\n{meta["copyright"]}\n'
                        'Original source pixels, power-overlay composition and inferred shallow 3D extrusion.\n')
    report = {'schemaVersion': 1, 'status': 'Source-frame comparison. Uses authored library frame geometry when available; full gameplay conversion and native interactive review are unfinished.',
              'sourceFlickIntervals': list(FLICK_INTERVALS), 'effectiveFlickTimes': [0, .5], 'completionTime': LENGTH,
              'notes': ['Source pixels and frame timing are retained. Depth and rear construction are inferred.',
                        'KeyTime values are intervals: the later 1.5/2.75 second flicks lie outside the 1.25 second animation.',
                        'SpriteFlick clamps each strip at its last frame; it does not loop.',
                        'Repeated press events are ignored while the original animation is running.',
                        'Unpowered controls reject normal use. The power overlay can appear during a running animation.',
                        'doorctrl0 and doorctrl-open are resource poses, not proven gameplay states for these placed controls.',
                        'Mechanical articulation, material emission, native playback and saved placement review remain unfinished.'],
              'sources': ['Content.Client/_RMC14/Doors/RMCDoorVisualsSystem.cs',
                          'RobustToolbox/Robust.Client/Animations/AnimationTrackSpriteFlick.cs',
                          'Content.Shared/_RMC14/Doors/RMCDoorButtonComponent.cs',
                          'Content.Shared/_RMC14/Doors/CMDoorSystem.cs',
                          'Resources/Prototypes/_RMC14/Entities/Structures/Doors/Shutters/poddoor_buttons.yml'],
              'studyExportedAnimationClips': sum(len(s['clips']) for s in studies), 'studies': studies}
    (output / 'study.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    (output / 'LICENSE.txt').write_text('\n'.join(licenses), encoding='utf-8')
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=ROOT / 'Tools/three_d/generated/button-animation-study')
    args = parser.parse_args()
    report = build_study(ROOT, args.output)
    print(json.dumps({'models': len(report['studies']), 'studyClips': report['studyExportedAnimationClips'],
                      'libraryChanged': False, 'output': str(args.output)}, indent=2))


if __name__ == '__main__':
    main()
