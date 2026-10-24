"""Draws the jeep's panel damage in the Open Vehicles editor. Gunfire leaves holes: pixels erased
through the panel, which the voxel render turns into real holes with the inside showing. Fire leaves
burns: char at the heart, paint darkened round it. Blunt hits leave an impact with stressed metal
creasing diagonally out of it.

Every mark is placed once, at its worst; each level shows the part of it that panel wear has reached,
so damage only grows in place as the panel wears: more holes out from the burst's centre, the burn
spreading from where it started, the creases running further out from the impact.
Run on a project made from the generator's cargo jeep (cmuov.py new FILE --from cargo):

    python draw_damage.py jeep_damage.cmuov"""
import json
import os
import random
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
# The CMU Open Vehicles tool; CMUOV_TOOL points elsewhere.
TOOL = os.path.join(os.environ.get('CMUOV_TOOL', r'C:\cmu\CMUOpenVehiclesTool'), 'backend', 'cmuov.py')
GEN = os.path.join(HERE, '..', 'jeep_pixel_art.py')
PROJECT = sys.argv[1]

# Panel layer, the view its face is drawn in, and the face's inside (x0, y0, x1, y1).
PANELS = [
    ('Driver door', 'left', (31, 18, 44, 26)),
    ('Passenger door', 'side', (27, 18, 40, 26)),
    ('Hood', 'top', (47, 16, 65, 31)),
    ('Tailgate', 'back', (9, 16, 38, 21)),
]
LEVELS = (25, 50, 100)
# The share of each mark's worst showing at each level.
SHOWN = (0.3, 0.6, 1.0)


def run(*args):
    out = subprocess.run([sys.executable, TOOL, '--generator', GEN, *args], capture_output=True, text=True)
    if out.returncode:
        raise SystemExit(out.stderr or out.stdout)
    return out.stdout


def draw(layer, view, ops):
    ops = [op for op in ops if op.get('points')]
    if ops:
        run('draw', PROJECT, layer, view, json.dumps(ops))


def pixels(points, mat=None, add=None):
    op = {'op': 'pixels', 'points': [list(p) for p in points]}
    if mat is not None:
        op['mat'] = mat
    if add is not None:
        op['add'] = add
    return op


for layer, view, (x0, y0, x1, y1) in PANELS:
    rng = random.Random(layer)
    width, height = x1 - x0 + 1, y1 - y0 + 1

    def inside(p):
        return x0 <= p[0] <= x1 and y0 <= p[1] <= y1

    # Gunfire: a burst's holes, each a couple of pixels across, ordered out from where it centres;
    # the worst also has a ragged pixel torn off some of them.
    centre = (x0 + width // 2 + rng.randint(-width // 4, width // 4), y0 + height // 2 + rng.randint(-1, 1))
    sites = []
    for _ in range(200):
        if len(sites) >= max(3, min(7, width * height // 30)):
            break
        p = (centre[0] + round(rng.gauss(0, width / 4)), centre[1] + round(rng.gauss(0, height / 4)))
        if inside(p) and inside((p[0] + 1, p[1] + 1)) and all(abs(p[0] - q[0]) > 3 or abs(p[1] - q[1]) > 3 for q in sites):
            sites.append(p)
    sites.sort(key=lambda p: (p[0] - centre[0]) ** 2 + (p[1] - centre[1]) ** 2)
    ragged = {p: rng.choice(((-1, 0), (2, 1), (0, 2), (1, -1))) for p in sites}
    state = f'{layer} holes'
    for i, level in enumerate(LEVELS):
        shown = sites[:max(1, round(len(sites) * SHOWN[i]))]
        holes = [(x + dx, y + dy) for x, y in shown for dx in (0, 1) for dy in (0, 1)]
        if i == 2:
            holes += [(x + ragged[(x, y)][0], y + ragged[(x, y)][1]) for x, y in shown[::2]]
        run('stage', PROJECT, layer, 'add', state, '--type', 'Piercing', '--from', str(level))
        draw(f'{state} {level}', view, [pixels([p for p in holes if inside(p)], 'empty')])

    # Fire: the whole burn grown once from where it started, in the order it spreads.
    seed = (rng.randint(x0 + 1, x1 - 1), rng.randint(y0 + 1, y1 - 1))
    order = [seed]
    burnt = {seed}
    while len(order) < int(width * height * 0.6):
        x, y = rng.choice(order)
        p = (x + rng.choice((-1, 0, 1)), y + rng.choice((-1, 0, 1)))
        if inside(p) and p not in burnt:
            burnt.add(p)
            order.append(p)
    state = f'{layer} burnt'
    for i, level in enumerate(LEVELS):
        part = set(order[:max(4, round(len(order) * SHOWN[i]))])
        edge = [p for p in part if any((p[0] + dx, p[1] + dy) not in part for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))]
        core = [p for p in part if p not in edge]
        run('stage', PROJECT, layer, 'add', state, '--type', 'Heat', '--from', str(level))
        draw(f'{state} {level}', view, [pixels(edge, 'od_in'), pixels(core, 'char')])

    # Blunt: an impact with creases running diagonally out of it, the metal stressed along them:
    # each a dark line pushed in, lit along one edge. The creases are laid once at full length and
    # each level shows them further out from the impact.
    impact = (rng.randint(x0 + width // 4, x1 - width // 4), rng.randint(y0 + 1, y1 - 1))
    rays = []
    for sx, sy in rng.sample(((1, 1), (1, -1), (-1, 1), (-1, -1)), 3):
        x, y = impact
        ray = []
        for step in range(max(width, height)):
            # Mostly diagonal, now and then running straight a step, as metal folds.
            if step % 3 == 2:
                x += sx if rng.random() < 0.5 else 0
            else:
                x, y = x + sx, y + sy
            if not inside((x, y)):
                break
            ray.append(((x, y), (x + sx, y) if inside((x + sx, y)) else None))
        rays.append(ray)

    state = f'{layer} dented'
    for i, level in enumerate(LEVELS):
        creases, shine = [], []
        for ray in rays:
            for p, lit in ray[:max(1, round(len(ray) * SHOWN[i]))]:
                creases.append(p)
                if lit is not None:
                    shine.append(lit)
        shine = [p for p in shine if p not in creases]
        pit = [impact] + ([(impact[0] + 1, impact[1]), (impact[0], impact[1] + 1)] if i > 0 else [])
        ops = [pixels(shine, 'od_light'), pixels(creases, 'od_seam'), pixels(pit, 'od_in')]
        if i == 0:
            ops.append(pixels(pit, add=1))
        run('stage', PROJECT, layer, 'add', state, '--type', 'Blunt', '--from', str(level))
        draw(f'{state} {level}', view, ops)
    print(layer, 'done')


# The body: gunfire only. Its holes are dark pits painted on the body's surface where it is: the body
# is never carved or made voxels, which would lose detail the generator gives it. Its fenders and
# back each take bursts of their own, fixed in place, growing with the hull's wear.
# Where the body shows from above: the two front fenders either side of the hood, and the back panel.
# Its side walls take theirs in export_damage.py, on the overlay as the game draws it from the sides.
BODY_BURSTS = [
    ('top', (47, 5, 62, 14)), ('top', (47, 33, 62, 42)),
    ('back', (5, 23, 42, 29)),
]


def depth_map(layer, view):
    """The depth of each drawn pixel of a layer's view, from `show --depth`."""
    text = run('show', PROJECT, layer, view, '--depth')
    rows = text.split('depth (base 36', 1)[1].splitlines()[1:]
    depths = {}
    for line in rows:
        parts = line.split(maxsplit=1)
        if len(parts) != 2 or not parts[0].isdigit():
            continue
        for x, ch in enumerate(parts[1]):
            if ch != '.':
                depths[(x, int(parts[0]))] = int(ch, 36)
    return depths


body_rng = random.Random('Body')
body_holes = []
surface = {view: depth_map('Body', view) for view in {v for v, _ in BODY_BURSTS}}
for view, (x0, y0, x1, y1) in BODY_BURSTS:
    w, h = x1 - x0 + 1, y1 - y0 + 1
    c = (x0 + w // 2 + body_rng.randint(-w // 4, w // 4), y0 + h // 2)
    sites = []
    for _ in range(200):
        if len(sites) >= 4:
            break
        p = (c[0] + round(body_rng.gauss(0, w / 4)), c[1] + round(body_rng.gauss(0, h / 4)))
        cells = [(p[0] + dx, p[1] + dy) for dx in (0, 1) for dy in (0, 1)]
        if (x0 <= p[0] < x1 and y0 <= p[1] < y1 and all(q in surface[view] for q in cells) and
                all(abs(p[0] - q[0]) > 3 or abs(p[1] - q[1]) > 3 for q in sites)):
            sites.append(p)
    sites.sort(key=lambda p: (p[0] - c[0]) ** 2 + (p[1] - c[1]) ** 2)
    body_holes.append((view, sites))

for i, level in enumerate(LEVELS):
    name = f'Body holes {level}'
    run('stage', PROJECT, 'Body', 'add', 'Body holes', '--type', 'Piercing', '--from', str(level))
    for view, sites in body_holes:
        shown = sites[:max(1, round(len(sites) * SHOWN[i]))]
        cells = [[x + dx, y + dy] for x, y in shown for dx in (0, 1) for dy in (0, 1)]
        run('draw', PROJECT, name, view, json.dumps([{'op': 'pixels', 'points': cells, 'mat': 'black'}]))
print('Body done')
