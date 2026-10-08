"""Planar surface artwork shared by GLB, browser and native review geometry.

PNG rows run top to bottom. XZ reads from local south, XY from above with north
at the top, and YZ from east. The same projection is used on both sides of a
thin volume, so the reverse reads as the back of the physical printed surface.
"""
from functools import lru_cache
from io import BytesIO
from pathlib import Path
import json
import re

from PIL import Image
import yaml

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'Content.CMU/Resources/Prototypes/CMU14/ThreeD'
CELL = 256
COLUMNS = 64
MAX_SURFACES = 4095
AXES = {'XZ': 1, 'XY': 2, 'YZ': 3}


def uv(point, axis):
    x, y, z = point  # Normalized local box coordinates, -0.5..0.5.
    if axis == 'XZ':
        return x + .5, .5 - z
    if axis == 'XY':
        return x + .5, .5 - y
    if axis == 'YZ':
        return .5 - y, .5 - z
    raise ValueError(f'Unknown surface projection {axis}')


def texture_path(value):
    if not isinstance(value, str) or not value.startswith('/Textures/') or not value.endswith('.png'):
        raise ValueError('Surface texture must name an absolute /Textures/...png resource')
    for base in (ROOT / 'Content.CMU/Resources', ROOT / 'Resources'):
        path = (base / value.lstrip('/')).resolve()
        if path.is_relative_to(base.resolve()) and path.is_file():
            return path
    raise ValueError(f'Missing or unsafe surface texture: {value}')


@lru_cache(maxsize=1)
def load_surfaces():
    result, used = {}, set()
    for path in sorted(SOURCE.glob('*.yml')):
        for entry in yaml.load(path.read_text(encoding='utf-8'), Loader=getattr(yaml, 'CSafeLoader', yaml.SafeLoader)) or []:
            if entry.get('type') != 'cmu3DSurface':
                continue
            uid, index = entry.get('id'), entry.get('atlasIndex')
            if not isinstance(uid, str) or not re.fullmatch(r'[A-Za-z][A-Za-z0-9_]*', uid):
                raise ValueError('Surface ID must be a safe prototype identifier')
            if uid in result or type(index) is not int or not 1 <= index <= MAX_SURFACES or index in used:
                raise ValueError(f'{uid}: surface ID/index must be unique, with index in 1..{MAX_SURFACES}')
            file = texture_path(entry.get('texture'))
            image = Image.open(file).convert('RGBA')
            if not 1 <= image.width <= CELL or not 1 <= image.height <= CELL:
                raise ValueError(f'{uid}: surface image must fit a {CELL} by {CELL} atlas cell')
            if not image.getbbox():
                raise ValueError(f'{uid}: surface image is entirely transparent')
            result[uid] = {**entry, 'file': file, 'image': image}
            used.add(index)
    return result


def png_bytes(image):
    stream = BytesIO()
    # glTF consumes RGBA pixels; inherited ICC/DPI metadata is not portable.
    pixels = image.copy()
    pixels.info.clear()
    pixels.save(stream, format='PNG')
    return stream.getvalue()


def viewer_outputs():
    surfaces = load_surfaces()
    largest = max((max(s['image'].size) for s in surfaces.values()), default=1)
    cell = 1 << (largest - 1).bit_length()
    rows = max(1, (max((s['atlasIndex'] for s in surfaces.values()), default=1) + COLUMNS - 1) // COLUMNS)
    atlas = Image.new('RGBA', (cell * COLUMNS, cell * rows))
    entries = {}
    for uid, surface in sorted(surfaces.items()):
        index, image = surface['atlasIndex'] - 1, surface['image']
        x, y = index % COLUMNS * cell, index // COLUMNS * cell
        atlas.paste(image, (x, y))  # Preserve source alpha and pixel values exactly.
        entries[uid] = {'index': index + 1, 'rect': [x, y, image.width, image.height],
                        'texture': surface['texture']}
    document = {'cellSize': cell, 'columns': COLUMNS, 'size': list(atlas.size),
                'imageUrl': '../generated/surface-atlas.png', 'surfaces': entries}
    return {'surface-atlas.png': png_bytes(atlas),
            'surfaces.json': (json.dumps(document, indent=2) + '\n').encode('utf-8')}
