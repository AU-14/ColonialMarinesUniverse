"""
Derives two screen textures from screen_<tone>.png: one with a square bottom edge and one with a
square top edge.

The lobby housing stacks two screens with a strip of case between them. With the stock texture both
screens are rounded on all four corners, so where each meets that strip its corners curve away and
leave a wedge of bare case colour - the strip's own highlight and shadow then stop short of the
housing wall and it reads as a bar floating in a pool of plastic rather than as the case passing
between two tubes. Squaring the edge that faces the strip makes the two screens butt flush to it.

Each variant keeps the rounded half of the source and rebuilds the other by wrapping the source's
own edge profile around a right angle, so the squared edge keeps the rim, the dark band and the fade
into the glass that every other edge on the panel has - only the corner curve goes. All four patch
margins stay at the stock 28: the squared edge carries real detail, not a flat run of glass.

Writes screen_top_<tone>.png and screen_bottom_<tone>.png beside the source. Regenerate rather than
hand-edit; rerun after any change to screen_<tone>.png.
"""
import os

from PIL import Image

OUT = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
    'Content.CMU', 'Resources', 'Textures', 'CMU14', 'Interface', 'ChatHousing')

TONES = ('gunmetal', 'olive', 'beige')


def variant(im, keep_top):
    """
    keep_top: keep the source's rounded top and square off the bottom. False mirrors that.

    The squared half is rebuilt by depth rather than copied. The source's edge is a one-pixel bright
    rim, then a dark band, then glass brightening toward the middle; reading that profile off the
    straight part of a side and then painting each pixel by its distance from the nearest edge wraps
    the very same profile around a right angle. The rim turns the corner, the dark band follows it
    in, and the glass fills behind - so the squared edge darkens into the case exactly as the rounded
    ones do, which is the whole point of rebuilding it rather than repeating a straight row.

    Blending the two fade profiles multiplicatively was tried first and is wrong: the rim is far
    brighter than the glass it is divided by, so the corner clamps to white and the channels clamp
    unevenly, which shows up as coloured specks in the corners.
    """
    w, h = im.size
    mid_y, mid_x = h // 2, w // 2
    # Depth 0 is the outermost pixel. Taken from the middle row, where the side is straight.
    profile = [im.getpixel((d, mid_y)) for d in range(mid_x + 1)]

    out = Image.new('RGBA', (w, h))
    for y in range(h):
        keep = y < mid_y if keep_top else y >= mid_y
        for x in range(w):
            if keep:
                out.putpixel((x, y), im.getpixel((x, y)))
                continue

            depth = min(x, w - 1 - x, (h - 1 - y) if keep_top else y, mid_x)
            out.putpixel((x, y), profile[depth])

    return out


if __name__ == '__main__':
    for tone in TONES:
        src = Image.open(os.path.join(OUT, f'screen_{tone}.png')).convert('RGBA')
        variant(src, keep_top=True).save(os.path.join(OUT, f'screen_top_{tone}.png'))
        variant(src, keep_top=False).save(os.path.join(OUT, f'screen_bottom_{tone}.png'))
    print('wrote screen_top_* and screen_bottom_* for', ', '.join(TONES), 'in', OUT)
