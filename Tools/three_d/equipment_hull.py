"""Conservative four-view volume studies from original equipped RSI artwork.

Source silhouettes constrain volume; they do not identify hidden construction.
These are review studies, never a reason to mark equipment fidelity approved.
"""
import json
from pathlib import Path

import numpy as np
from PIL import Image

import inventory as inv

ROOT = Path(__file__).resolve().parents[2]


def frames(rsi, state):
    path = inv.resource_path(ROOT, inv.texture_reference(rsi))
    if path is None:
        raise ValueError(f'Missing RSI: {rsi}')
    meta = json.loads((path / 'meta.json').read_text(encoding='utf-8-sig'))
    entry = next(s for s in meta['states'] if s['name'] == state)
    if entry.get('directions', 1) != 4:
        raise ValueError(f'{rsi}/{state}: four views required')
    image = Image.open(path / (state + '.png')).convert('RGBA')
    width, height = meta['size']['x'], meta['size']['y']
    index = 0
    result = []
    for delays in entry.get('delays', [[1]] * 4):
        x, y = index % (image.width // width) * width, index // (image.width // width) * height
        result.append(image.crop((x, y, x + width, y + height)))
        index += len(delays)
    return result, meta


def cuboids(voxels):
    """Greedy non-overlapping boxes preserve every occupied voxel and its paint."""
    data = voxels.copy()
    parts = []
    for x, y, z in np.argwhere(data >= 0):
        color = data[x, y, z]
        if color < 0:
            continue
        xx, yy, zz = x + 1, y + 1, z + 1
        while xx < data.shape[0] and data[xx, y, z] == color:
            xx += 1
        while yy < data.shape[1] and np.all(data[x:xx, yy, z] == color):
            yy += 1
        while zz < data.shape[2] and np.all(data[x:xx, y:yy, zz] == color):
            zz += 1
        data[x:xx, y:yy, z:zz] = -1
        parts.append(((int(x), int(y), int(z)), (int(xx), int(yy), int(zz)), int(color)))
    return parts


def hull(images, resolution=16, colors=12):
    # RSI order S/N/E/W. Rear and west screen axes reverse their world axes.
    width, height = images[0].size
    scale = resolution / 32
    size = (round(width * scale), round(height * scale))
    arrays = [np.asarray(im.resize(size, Image.Resampling.NEAREST)) for im in images]
    arrays[1] = arrays[1][:, ::-1]
    arrays[3] = arrays[3][:, ::-1]
    opaque = np.concatenate([a[:, :, :3][a[:, :, 3] >= 128] for a in arrays])
    if not len(opaque):
        raise ValueError('Empty equipped artwork')
    palette_image = Image.fromarray(opaque.reshape((1, -1, 3))).quantize(colors=colors, method=Image.Quantize.MEDIANCUT)
    palette = np.asarray(palette_image.getpalette(), dtype=np.uint8).reshape((-1, 3))[:colors]
    # Median-cut averages are only clustering hints. Keep actual source paints.
    paints = np.unique(opaque, axis=0)
    distances = paints[:, None, :].astype(np.int32) - palette[None, :, :].astype(np.int32)
    palette = paints[np.argmin(np.sum(distances * distances, axis=2), axis=0)]
    indexed = []
    for a in arrays:
        diff = a[:, :, :3].astype(np.int32)[:, :, None, :] - palette.astype(np.int32)[None, None, :, :]
        indexed.append(np.argmin(np.sum(diff * diff, axis=3), axis=2))
    front = (arrays[0][:, :, 3] >= 128) | (arrays[1][:, :, 3] >= 128)
    side = (arrays[2][:, :, 3] >= 128) | (arrays[3][:, :, 3] >= 128)
    grid = np.full((size[0], size[0], size[1]), -1, dtype=np.int16)
    for row in range(size[1]):
        xs, ys = np.flatnonzero(front[row]), np.flatnonzero(side[row])
        if not len(xs) or not len(ys):
            continue
        for x in xs:
            for y in ys:
                # Paint the nearest observed face. No lightening or invented faction colours.
                options = [(y - ys[0], 0, x), (ys[-1] - y, 1, x),
                           (xs[-1] - x, 2, y), (x - xs[0], 3, y)]
                options = [option for option in options if arrays[option[1]][row, option[2], 3] >= 128]
                if not options:
                    continue
                _, face, column = min(options)
                grid[x, y, size[1] - row - 1] = indexed[face][row, column]
    parts = []
    unit = 1.6 / resolution
    for low, high, color in cuboids(grid):
        parts.append({'label': 'source volume ' + str(len(parts) + 1),
                      'min': ((low[0] - size[0] / 2) * unit, (low[1] - size[0] / 2) * unit, low[2] * unit),
                      'max': ((high[0] - size[0] / 2) * unit, (high[1] - size[0] / 2) * unit, high[2] * unit),
                      'color': '#' + ''.join(f'{v:02X}' for v in palette[color])})
    return parts
