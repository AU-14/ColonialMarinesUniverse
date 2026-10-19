"""Takes the jeep's panel and body damage, drawn in the CMU Open Vehicles tool, into the jeep's RSIs.

The tool renders the damage project (made from the generator's cargo jeep) with the generator's own
renderer. Each damage state is the pixels a part shows changed at that level. The tool splits a
part between under and over the riders its own way, with its overlay unslid; the jeep's RSIs split
it by their own overlay states, slid up per facing as the overlay entity draws them. So each state
is put back into one picture and split again for each RSI: a pixel goes over the riders where that
RSI's overlay of the part covers it. The body's holes also go on its overlay where the tool can't
put them: its back and side walls (side_holes).

    python export_damage.py jeep_damage.cmuov            # checks the tool draws the jeep in place
    python export_damage.py jeep_damage.cmuov --write    # and writes the states into the RSIs
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
# The CMU Open Vehicles tool; CMUOV_TOOL points elsewhere.
sys.path.insert(0, os.path.join(os.environ.get('CMUOV_TOOL', r'C:\cmu\CMUOpenVehiclesTool'), 'backend'))
import export as X  # noqa: E402
import project as P  # noqa: E402
import voxels as V  # noqa: E402
from PIL import Image  # noqa: E402

REPO = os.path.normpath(os.path.join(HERE, '..', '..', '..', '..', '..'))
GEN = os.path.join(HERE, '..', 'jeep_pixel_art.py')
TEXTURES = os.path.join(REPO, r'Content.CMU\Resources\Textures\CMU14\Structures\vehicles\jeep')
RSIS = ['jeep', 'jeep_gunner', 'jeep_medical', 'jeep_transport']
SIZE = 96
DIRS = ['S', 'N', 'E', 'W']
# How far up the overlay entity draws its frames, per facing (CMUVehicleOverlayVisuals directionOffsets).
LIFT = {'S': 1, 'N': 0, 'E': 16, 'W': 16}
# The tool's part ids: the jeep's, the jeep's overlay state for it, and whether it only goes over the riders.
PARTS = {
    'driver_door': ('door_driver', 'door_driver_closed_overlay', False),
    'passenger_door': ('door_passenger', 'door_passenger_closed_overlay', False),
    'hood': ('hood', 'hood_closed_overlay', False),
    'tailgate': ('tailgate', 'tailgate_closed_overlay', False),
    'body': ('body', 'jeep_overlay', False),
}


def frames(rsi, name):
    """An RSI state's first frame in each direction, as {pixel: rgba}."""
    folder = os.path.join(TEXTURES, rsi + '.rsi')
    meta = json.load(open(os.path.join(folder, 'meta.json'), encoding='utf-8'))
    info = next(s for s in meta['states'] if s['name'] == name)
    per = len(info['delays'][0]) if info.get('delays') else 1
    sheet = Image.open(os.path.join(folder, name + '.png')).convert('RGBA')
    cols = sheet.width // SIZE
    out = {}
    for d, view in enumerate(DIRS):
        i = d * per
        tile = sheet.crop((i % cols * SIZE, i // cols * SIZE, i % cols * SIZE + SIZE, i // cols * SIZE + SIZE)).load()
        out[view] = {(x, y): tile[x, y] for x in range(SIZE) for y in range(SIZE) if tile[x, y][3]}
    return out


def split_for(rsi, damage):
    """The damage states for one RSI, split under and over the riders as that RSI splits its parts."""
    covers = {}
    out = {}
    for name, st in damage.items():
        if '_overlay_' in name:
            continue
        tool_part, rest = name.split('_dmg_', 1)
        kind, level = rest.rsplit('_', 1)
        part, overlay_state, overlay_only = PARTS[tool_part]
        cover = covers.setdefault(overlay_state, frames(rsi, overlay_state))
        over_st = damage.get(f'{tool_part}_dmg_{kind}_overlay_{level}')
        base_frames, over_frames = {}, {}
        for view in DIRS:
            pixels = dict(st['frames'][view][0])
            if over_st:
                pixels.update(over_st['frames'][view][0])
            base, over = {}, {}
            for (x, y), c in pixels.items():
                slid = (x, y + LIFT[view])
                if slid in cover[view]:
                    over[slid] = c
                elif not overlay_only and 0 <= x < SIZE and 0 <= y < SIZE:
                    base[(x, y)] = c
            base_frames[view] = [base]
            over_frames[view] = [over]
        key = f'{part}_dmg_{kind.split("_")[-1]}'
        if not overlay_only:
            out[f'{key}_{level}'] = {'frames': base_frames, 'delay': None}
        out[f'{key}_overlay_{level}'] = {'frames': over_frames, 'delay': None}
    return out


# The overlay's states drawn over the body's from the sides, as the jeep's overlay lists them.
OVER_BODY = ['wheels_overlay_0', 'hood_closed_overlay', 'fuel_door_closed_overlay', 'door_driver_closed_overlay',
             'door_passenger_closed_overlay', 'tailgate_closed_overlay', 'spare_overlay_0', 'jerrycan_overlay_0',
             'shovel_overlay_0', 'axe_overlay_0', 'jack_overlay_0', 'extinguisher_overlay_0',
             'headlight_left_overlay_0', 'headlight_right_overlay_0', 'turn_signal_left_overlay_0',
             'turn_signal_right_overlay_0', 'taillight_left_overlay_0', 'taillight_right_overlay_0',
             'windshield_up_overlay_0', 'wirecutter_overlay_0']
SHOWN = (0.3, 0.6, 1.0)


def side_holes(rsi, out):
    """Gunfire holes in the body the overlay shows from behind and the sides: its back and its near
    side wall. The tool's holes show over the riders from the front; from the other sides the body the
    game draws over them is little of what the tool's views draw, or hidden behind the spare. So they
    go on the overlay itself, where its body shows over everything else: a burst on each of its two
    biggest stretches of bare metal, pits the colour of the tool's holes, each level showing more of
    the same holes."""
    import random
    names = set(n['name'] for n in json.load(open(os.path.join(TEXTURES, rsi + '.rsi', 'meta.json'), encoding='utf-8'))['states'])
    body = frames(rsi, 'jeep_overlay')
    covered = {view: set() for view in DIRS}
    for name in OVER_BODY:
        if name in names:
            for view, px in frames(rsi, name).items():
                covered[view] |= set(px)
    worst = out['body_dmg_holes_overlay_100']['frames']['S'][0]
    pit = min(worst.values(), key=lambda c: sum(c[:3])) if worst else (20, 22, 16, 255)
    rng = random.Random(rsi + ' sides')
    for view in ('N', 'E', 'W'):
        # Bare body sheet metal, not under anything, its olive not a seam or a fitting.
        wall = {p for p, c in body[view].items()
                if p not in covered[view] and c[1] >= c[0] and c[1] >= c[2] and 60 < sum(c[:3]) < 450}
        fits = [p for p in wall if all((p[0] + dx, p[1] + dy) in wall for dx in (0, 1) for dy in (0, 1))]
        if not fits:
            continue
        # The two biggest stretches of bare wall, a burst in the middle of each.
        left = set(fits)
        areas = []
        while left:
            todo = [left.pop()]
            area = []
            while todo:
                x, y = todo.pop()
                area.append((x, y))
                for n in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                    if n in left:
                        left.remove(n)
                        todo.append(n)
            areas.append(area)
        areas.sort(key=len, reverse=True)
        sites = []
        for area in areas[:2]:
            cx = sum(p[0] for p in area) / len(area)
            cy = sum(p[1] for p in area) / len(area)
            near = sorted(area, key=lambda p: ((p[0] - cx) ** 2 + (p[1] - cy) ** 2, rng.random()))[:40]
            centre = near[0]
            burst = []
            for p in sorted(near, key=lambda p: rng.random()):
                if len(burst) >= 3:
                    break
                if all(abs(p[0] - q[0]) > 3 or abs(p[1] - q[1]) > 3 for q in burst + sites):
                    burst.append(p)
            sites += sorted(burst, key=lambda p: (p[0] - centre[0]) ** 2 + (p[1] - centre[1]) ** 2)
        by_burst = [sites[:len(sites) // 2], sites[len(sites) // 2:]]
        for i, level in enumerate((25, 50, 100)):
            frame = out[f'body_dmg_holes_overlay_{level}']['frames'][view][0]
            for burst in by_burst:
                for x, y in burst[:max(1, round(len(burst) * SHOWN[i]))]:
                    for dx in (0, 1):
                        for dy in (0, 1):
                            frame[(x + dx, y + dy)] = pit


def install(rsi, states):
    """Writes the states into the RSI, replacing any of the same name."""
    folder = os.path.join(TEXTURES, rsi + '.rsi')
    path = os.path.join(folder, 'meta.json')
    raw = open(path, encoding='utf-8').read()
    meta = json.loads(raw)
    meta['states'] = [s for s in meta['states'] if s['name'] not in states]
    for name, st in states.items():
        Image.Image.save(sheet(st), os.path.join(folder, name + '.png'))
        meta['states'].append({'name': name, 'directions': 4})
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        json.dump(meta, f, indent=4)
        f.write('\n' if raw.endswith('\n') else '')


def sheet(st):
    """A state's four frames on one sheet, south, north, east, west, two to a row."""
    img = Image.new('RGBA', (SIZE * 2, SIZE * 2), (0, 0, 0, 0))
    for i, view in enumerate(DIRS):
        tile = X.to_image(st['frames'][view][0])
        img.paste(tile, (i % 2 * SIZE, i // 2 * SIZE))
    return img


project = P.load(sys.argv[1])
gen = V.Generator(GEN)
if project.get('customMaterials'):
    gen.add_materials(project['customMaterials'])
states = X.render_states(gen, project)

# The tool must draw the jeep where its RSIs do: the shut hood seen from the sides, which neither
# splits, matches pixel for pixel.
ours = frames('jeep', 'hood_closed')
for view in ('E', 'W'):
    tool = set(states['hood_closed']['frames'][view][0])
    if tool != set(ours[view]):
        raise SystemExit(f'The tool draws the hood {view} {len(tool ^ set(ours[view]))} pixels off the jeep: not written.')
print('The tool draws the jeep in place.')

damage = {n: s for n, s in states.items() if '_dmg_' in n}
if '--write' in sys.argv:
    for rsi in RSIS:
        out = split_for(rsi, damage)
        side_holes(rsi, out)
        install(rsi, out)
        print(f'{rsi}: {len(out)} damage states')
