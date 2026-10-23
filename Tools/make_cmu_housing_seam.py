"""
Generates the housing seam tile: a strip of the case's own plastic laid between two screens, so the
housing itself says they are two separate tubes rather than one screen with a line drawn on it.

Everything either side of the seam is an active display, so the one strip that plainly is not a
display is what reads as the join. Unlit on purpose - a glowing rule reads as a UI element inside
one screen, which is the opposite of what this is for.

Same palette and bevel language as the housing's corner screws: a highlight along the top edge, a
shadow along the bottom, and one rivet per tile so it reads as machined casing once it repeats
rather than a bar with an ornament stuck in the middle.

Writes Content.CMU/Resources/Textures/CMU14/Interface/ChatHousing/seam_<tone>.png. Regenerate rather than
hand-edit. Drawn at 4x and downsampled so the rivet gets an anti-aliased edge.
"""
import os

from PIL import Image, ImageDraw

OUT = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
    'Content.CMU', 'Resources', 'Textures', 'CMU14', 'Interface', 'ChatHousing')

# top, bottom, highlight, shadow - the plastic of each housing tone.
TONES = {
    'gunmetal': ('#2b2e28', '#1a1c18', '#454940', '#0b0c0a'),
    'olive': ('#30331f', '#1f2115', '#4d5234', '#0b0c0a'),
    'beige': ('#b7ae96', '#968d76', '#d8d0b8', '#5f5848'),
}

TILE_W = 64
TILE_H = 16
SCALE = 4


def hexrgb(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) + (255,)


def lerp(a, b, t):
    return tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(4))


def seam(tone):
    top, bot, hi, lo = (hexrgb(c) for c in TONES[tone])
    w, h = TILE_W * SCALE, TILE_H * SCALE
    im = Image.new('RGBA', (w, h))
    px = im.load()
    for y in range(h):
        c = lerp(top, bot, y / (h - 1))
        for x in range(w):
            px[x, y] = c

    d = ImageDraw.Draw(im)
    # Lit from above, like the housing's own outer edge - not from the screens either side of it.
    d.rectangle([0, 0, w - 1, SCALE - 1], fill=hi)
    d.rectangle([0, h - SCALE * 2, w - 1, h - 1], fill=lo)

    cx, cy = w // 2, h // 2
    r = 3 * SCALE
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=lo)
    d.ellipse([cx - r + SCALE, cy - r + SCALE, cx + r - SCALE, cy + r - SCALE], fill=lerp(hi, lo, 0.35))
    d.ellipse([cx - r + SCALE * 2, cy - r + SCALE, cx + r - SCALE * 2, cy + r - SCALE * 2], fill=lerp(hi, top, 0.25))
    # A scored line across the rivet head, matching the screws on the housing's corners.
    d.line([cx - r + SCALE, cy, cx + r - SCALE, cy - SCALE], fill=lo, width=SCALE)

    im = im.resize((TILE_W, TILE_H), Image.LANCZOS)
    im.save(os.path.join(OUT, f'seam_{tone}.png'))


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    for name in TONES:
        seam(name)
    print('wrote', ', '.join(f'seam_{t}.png' for t in TONES), 'to', OUT)
