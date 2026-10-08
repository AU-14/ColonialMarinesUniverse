"""Author only the exact aluminium foam wall family and its bounded source proof."""
from collections import Counter
from copy import deepcopy
from io import BytesIO
from pathlib import Path
import argparse
import hashlib
import json
import math
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont
import yaml

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'Tools/three_d'))
import build_models as bm
import foam_wall_states as fs
import inventory
import scene
import surfaces
from author_cash_cutlery import rectangles
from author_wide_machinery import world_parts

GEN = ROOT / 'Tools/three_d/generated'
BASELINE = ROOT / '.codex/model-batch-baseline1006'
MODEL = ROOT / 'Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_foam_wall.yml'
ART = MODEL.with_name('garrison_foam_wall_art.yml')
TEXTURES = ROOT / 'Content.CMU/Resources/Textures/CMU14/ThreeD/Surfaces/FoamWall'
NOTE = ROOT / 'Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_FOAM_WALL.md'
REVIEW = GEN / 'review/foam-wall/source-model-montage.png'
CHECK = False
WRITTEN = []


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write(path, data):
    if CHECK:
        assert path.is_file() and path.read_bytes() == data, f'Generated asset differs: {path}'
    else:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
    WRITTEN.append(path)


def png(image):
    stream = BytesIO()
    # Source edge PNGs carry an ICC profile. glTF requires unprofiled PNGs;
    # rebuild the image from its exact RGBA bytes without altering source files.
    clean = Image.frombytes('RGBA', image.size, image.convert('RGBA').tobytes())
    clean.save(stream, format='PNG')
    return stream.getvalue()


class Pool:
    def __init__(self):
        self.rows, self.cache, self.evidence = [], {}, []
        self.registry = surfaces.load_surfaces()
        surfaces.load_surfaces = lambda: self.registry
        for path in MODEL.parent.glob('*.yml'):
            if path == ART:
                continue
            for row in yaml.load(path.read_text(encoding='utf-8-sig'), Loader=yaml.CSafeLoader) or []:
                assert row.get('type') != 'cmu3DSurface' or not 3300 <= row['atlasIndex'] <= 3399, path

    def crop(self, image, rect, state):
        crop = image.crop(rect)
        key = crop.size, crop.tobytes()
        if key not in self.cache:
            index = 3300 + len(self.rows)
            assert index <= 3399
            uid = f'CMU3DFoamWallSurface{index}'
            path = TEXTURES / (uid + '.png')
            write(path, png(crop))
            row = dict(type='cmu3DSurface', id=uid, atlasIndex=index,
                       texture=f'/Textures/CMU14/ThreeD/Surfaces/FoamWall/{uid}.png')
            self.rows.append(row)
            self.registry[uid] = {**row, 'file': path, 'image': crop}
            self.cache[key] = uid
        uid = self.cache[key]
        actual = Image.open(TEXTURES / (uid + '.png')).convert('RGBA')
        assert actual.size == crop.size and actual.tobytes() == crop.tobytes()
        self.evidence.append(dict(state=state, rect=list(rect), surface=uid,
            sourceAlpha=sorted(set(crop.getchannel('A').tobytes())),
            rgbaSha256=hashlib.sha256(actual.tobytes()).hexdigest()))
        return uid


def solid(label, rect, offset, low, high, surface, shape='Box'):
    x0, y0, x1, y1 = rect
    dx, dy = offset
    return dict(label=label, min=[(x0-16)/32+dx, (16-y1)/32+dy, low],
                max=[(x1-16)/32+dx, (16-y0)/32+dy, high], shape=shape,
                color=fs.TINT, surface=surface, surfaceAxis='XY')


def geometry(images, pool):
    """Closed cellular pillars with paired ridge slopes; source pixels cover XY exactly.

    The small edge silhouette rectangles are deep solids with progressively lower
    crowns. No alpha-masked square billboard or single cuboid represents the wall.
    Height, ridge structure and unseen sides are explicitly inferred.
    """
    base = images[fs.BASE]
    # The repeated whole-source pattern on unseen pillar sides is an inference.
    side = pool.crop(base, (0, 0, 32, 32), fs.BASE)
    base_parts = []
    for row in range(4):
        for col in range(4):
            x, y = 8*col, 8*row
            crown = .82 + ((row*7+col*11) % 5)*.025
            shoulder = crown - .10 - ((row+col) % 2)*.015
            base_parts.append(solid(f'cell {row}-{col} structural core', (x,y,x+8,y+8), (0,0),
                                    0, shoulder, side))
            # Front and back half crowns meet in a raised ridge, with varied
            # heights between neighboring cells, while preserving every XY pixel.
            for half, shape in ((0, 'WedgeYReverse'), (1, 'WedgeY')):
                rect = (x, y+half*4, x+8, y+half*4+4)
                base_parts.append(solid(f'cell {row}-{col} crown {half}', rect, (0,0), shoulder,
                                        crown, pool.crop(base, rect, fs.BASE), shape))
    edges = {}
    for edge, offset in zip(fs.EDGES, fs.OFFSETS):
        state = fs.BASE+'-'+edge
        image = images[state]
        parts = []
        for index, (rect, _) in enumerate(rectangles((np.asarray(image)[:,:,3] > 0).astype(int))):
            x0,y0,x1,y1 = rect
            outward = {'south':(y0+y1)/2, 'east':(x0+x1)/2,
                       'north':32-(y0+y1)/2, 'west':32-(x0+x1)/2}[edge]
            height = .82 - outward*.046 + (index % 3)*.012
            parts.append(solid(f'{edge} foam lobe {index}', rect, offset, 0, height,
                               pool.crop(image, rect, state)))
        edges[edge] = dict(parts=parts)
    return dict(baseParts=base_parts, edgeParts=edges)


def projection(parts, pool):
    """Independent top-ray/UV query through actual closed boxes and wedge crowns."""
    result = Image.new('RGBA', (96,96))
    for py in range(96):
        y = (48-py-.5)/32
        for px in range(96):
            x = (px+.5-48)/32
            hits = []
            for p in parts:
                lo, hi = p['min'], p['max']
                if not lo[0] < x < hi[0] or not lo[1] < y < hi[1]:
                    continue
                u, v = (x-lo[0])/(hi[0]-lo[0]), (hi[1]-y)/(hi[1]-lo[1])
                z = hi[2]
                if p.get('shape') == 'WedgeY':
                    z = lo[2]+(1-v)*(hi[2]-lo[2])
                elif p.get('shape') == 'WedgeYReverse':
                    z = lo[2]+v*(hi[2]-lo[2])
                hits.append((z, p, u, v))
            if hits:
                _, p, u, v = max(hits, key=lambda h:h[0])
                im = pool.registry[p['surface']]['image']
                rgba = im.getpixel((min(im.width-1,int(u*im.width)), min(im.height-1,int(v*im.height))))
                result.putpixel((px,py), (*rgba[:3], round(rgba[3]*.8)))
    return result


def source_image(images, mask):
    result = Image.new('RGBA',(96,96))
    for i,state in enumerate(fs.STATES):
        if i and not mask & (1 << (i-1)):
            continue
        im = images[state].copy()
        im.putalpha(im.getchannel('A').point(lambda a:round(a*.8)))
        dx,dy = (0,0) if not i else fs.OFFSETS[i-1]
        result.alpha_composite(im,(32+32*dx,32-32*dy))
    return result


def serialize(value):
    if isinstance(value,dict):
        return {k:(', '.join(f'{x:.9g}' for x in v) if k in ('min','max','groundOffset') else serialize(v)) for k,v in value.items()}
    if isinstance(value,(list,tuple)):
        return [serialize(v) for v in value]
    return value


def source_context(model):
    kinds, _, _ = inventory.load_prototypes(ROOT)
    resolver = inventory.Resolver(kinds['entity'])
    contexts, defaults, maps, baseline_hashes, raw_records = [], {}, {}, {}, []
    # Read all configured baseline scenes to prove absence on the remaining levels.
    for spec in json.loads((BASELINE/'scenes.json').read_text()):
        baseline_path = BASELINE/spec['file']
        doc = json.loads(baseline_path.read_text())
        targets = [deepcopy(e) for e in doc['instances'] if e['prototype'] == fs.PROTOTYPE]
        baseline_hashes[baseline_path.relative_to(ROOT).as_posix()] = sha(baseline_path)
        if not targets:
            continue
        path,_ = scene.configured_map(ROOT,spec['variant'],spec['level'])
        scene.READ_COMPONENTS.update(('SmoothEdge','CMIconSmooth'))
        _, records = scene.read_map(path)
        for proto in sorted({r['prototype'] for r in records.values()}):
            if proto in defaults or proto not in kinds['entity']:
                continue
            try:
                defaults[proto] = inventory.component_map(resolver.resolve(proto))
            except inventory.ResolutionError:
                continue
        transforms = scene.WorldTransforms(records,defaults)
        graph = fs.EdgeGraph(records, defaults, transforms)
        variants = {}
        for entity in targets:
            entity.update(modelId=model['id'],matchKind='exact')
        applied = fs.apply_scene(targets,{model['id']:model},records,defaults,transforms,variants,scene.normalize_tint)
        assert not applied['rejected'], applied
        for entity in targets:
            uid = entity['id']
            world = transforms.resolve(uid)
            assert all(abs(entity['position'][i]-world[i])<1e-7 for i in (0,1))
            assert abs(entity['yaw']-world[2])<1e-7
            neighbors = [dict(uid=e['id'],prototype=e['prototype'],modelId=e.get('modelId'),position=e['position'])
                for e in doc['instances'] if e['id'] != uid and max(abs(e['position'][i]-entity['position'][i]) for i in (0,1))<1.6]
            contexts.append(dict(variant=spec['variant'],level=spec['level'],uid=uid,position=entity['position'],
                yaw=entity['yaw'],renderYaw=entity['yaw'],edgeMask=entity['foamPose']['edgeMask'],
                matchingNeighbors=entity['foamPose']['matchingNeighbors'],geometryKey=entity['geometryKey'],
                gridCell=list(graph.grid_context(uid)),neighbors=neighbors))
            raw_records.append(dict(variant=spec['variant'],level=spec['level'],uid=uid,
                savedComponents=records[uid]['components'],pose=entity['foamPose']))
        maps[path.relative_to(ROOT).as_posix()] = sha(path)
    assert len(contexts)==21 and all(e['variant']=='redux' for e in contexts)
    duplicates = [e for e in contexts if e['uid'] in (16177,16181)]
    assert len(duplicates)==2 and duplicates[0]['position']==duplicates[1]['position']
    source_files = ['Resources/Prototypes/_RMC14/Entities/Structures/Walls/foamed_metal.yml',
        'Content.Client/IconSmoothing/IconSmoothSystem.cs','Content.Client/IconSmoothing/IconSmoothSystem.Edge.cs',
        'Content.Client/IconSmoothing/IconSmoothComponent.cs','Content.Shared/IconSmoothing/SmoothEdgeComponent.cs',
        'Content.Shared/_RMC14/IconSmoothing/CMIconSmoothComponent.cs']
    source_files += [p.relative_to(ROOT).as_posix() for p in sorted(bm.resource_file(fs.RSI).glob('metal_foam*.png'))]
    source_files += [(bm.resource_file(fs.RSI)/'meta.json').relative_to(ROOT).as_posix()]
    audit = dict(status='source-captured',prototype=fs.PROTOTYPE,defaults=defaults[fs.PROTOTYPE],records=raw_records,
        sourceFilesSha256={f:sha(ROOT/f) for f in source_files},savedMapsSha256=maps,baselineScenesSha256=baseline_hashes,
        owner='IconSmoothSystem NoSprite / SmoothEdge. Grid-cardinal visibility does not rotate with entity yaw.',
        preservedDuplicateUids=[16177,16181])
    write(GEN/'foam-wall-source-audit.json',(json.dumps(audit,indent=2)+'\n').encode())
    return contexts


def review(model, images, contexts):
    poses = sorted({(e['edgeMask'],round(e['yaw'],8)) for e in contexts}|{(15,0)})
    font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',15)
    canvas = Image.new('RGB',(1250,250*len(poses)),'#17212B')
    draw = ImageDraw.Draw(canvas)
    for row,(mask,entity_yaw) in enumerate(poses):
        y=row*250
        actual = [e for e in contexts if e['edgeMask']==mask and abs(e['yaw']-entity_yaw)<1e-7]
        ids=', '.join(str(e['uid']) for e in actual)
        draw.text((12,y+8),f'Edge mask {mask}, source yaw {round(math.degrees(entity_yaw))} degrees: '+(ids or 'all-edges study'),font=font,fill='white')
        source=source_image(images,mask).rotate(math.degrees(entity_yaw),resample=Image.Resampling.NEAREST)
        bg=Image.new('RGBA',(210,210),'#61717A')
        bg.alpha_composite(source.crop((23,23,73,73)).resize((210,210),Image.Resampling.NEAREST))
        canvas.paste(bg.convert('RGB'),(12,y+32))
        pose={**model,'parts':world_parts(fs.compose_parts(model,mask),(0,0),entity_yaw)}
        for col,(yaw,pitch) in enumerate(((-math.pi/2,math.pi/2),(-.7,.65),(1.0,.4))):
            canvas.paste(bm.render_model(pose,(330,210),yaw,pitch),(235+col*335,y+32))
    REVIEW.parent.mkdir(parents=True,exist_ok=True)
    canvas.save(REVIEW)


def main():
    global CHECK
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check',action='store_true')
    parser.add_argument('--skip-reviews',action='store_true')
    args=parser.parse_args(); CHECK=args.check
    model=dict(type='cmu3DModel',id='CMU3DRMCFoamedAluminiumMetal',label='Foamed aluminium wall',status='draft',
        sourcePrototypes=[fs.PROTOTYPE],referencePrototype=fs.PROTOTYPE,referenceRsi=fs.RSI,referenceState=fs.BASE,
        sourceDirections=1,referenceDirection=0,referenceTint=fs.TINT,bakedSpriteTint=fs.TINT,
        placement='floor',groundOffset=[0,0],useEntityRotation=True,
        description='Cellular aluminium foam mass with source-owned SmoothEdge lobes and exact top artwork. Height and unseen depth inferred; see SOURCES_FOAM_WALL.md.')
    images=fs.validate_source(model,bm.resource_file)
    pool=Pool();model['foamAppearance']=geometry(images,pool);model['parts']=fs.compose_parts(model)
    model=bm.validate_model(model)
    poses=[]
    for mask in range(16):
        parts=fs.compose_parts(model,mask)
        actual=projection(parts,pool);expected=source_image(images,mask)
        assert actual.tobytes()==expected.tobytes(), mask
        poses.append(dict(mask=mask,parts=len(parts),exactTopRgba=True,
                          rgbaSha256=hashlib.sha256(actual.tobytes()).hexdigest()))
    for path,rows in ((MODEL,[serialize(model)]),(ART,pool.rows)):
        write(path,('# Generated by Tools/three_d/author_foam_wall.py.\n'+yaml.safe_dump(rows,sort_keys=False,width=110)).encode())
    assert len(bm.load_models(MODEL))==1
    contexts=source_context(model)
    if not args.skip_reviews:
        review(model,images,contexts)
    meta=json.loads((bm.resource_file(fs.RSI)/'meta.json').read_text())
    note='''# Aluminium foam wall

One exact mapping: RMCFoamedAluminiumMetal. Twenty-one saved Redux instances are present: eighteen on the surface and three on level +1. Two source sprites have -90-degree yaw; their entity poses remain rotated. UIDs 16177 and 16181 share one saved location and both are preserved. No classic placement or iron-foam prototype is claimed.

The original static one-direction metal_foam base is followed by south, east, north and west edge layers. SmoothEdge startup offsets them by (0,-1), (1,0), (0,1), (-1,0). Enabled IconSmooth with key walls and mode NoSprite hides an edge when any enabled anchored walls-key neighbor occupies that grid-cardinal cell. That visibility query is independent of sprite yaw. The portable saved adapter reads all neighboring source prototypes, including ordinary walls, not only mapped foam geometry. Native rendering samples the actual five keyed source layers and their visibility; it does not implement another smoothing system or timer.

The wall is a physical cellular mass with sixteen structural columns, paired sloping crowns and closed irregular edge lobes. Its base covers the source tile; lobes extend only where the source PNG has nonzero pixels. Every top-facing source pixel is retained, including edge PNG alpha 240 and 255. Sprite color #FFFFFFCC appears once in each part, with bakedSpriteTint preventing a second multiplication. Source pixel alpha remains in the PNG crops and is multiplied by that 0.8 opacity once. True blend appearance is approximated by the renderer's dithered solid transparency.

Top artwork, silhouette, pivot, saved transforms and all sixteen edge compositions are source evidence. Physical height, sloped cellular crowns, repeating unseen side texture and depth are authored inferences. Independent top-ray/UV reconstruction of all sixteen assemblies matches the original five-layer alpha composite byte for byte. This proves source projection, not unseen 3D correctness or a runtime clearance guarantee. Source duplicate entities may visibly overlap just as they do in the saved map. Mobs remain sprites.

The source audit records all twenty-one saved poses, source owner fields and hashes. Context proof lists original positions, grid cells, source matching neighbor IDs and nearby entities; every actual saved mask is represented in the one review montage. Unknown source owners, changed layer states/transforms, animation, shader layers and unresolvable smoothing neighbors retain sprite fallback. Portable GLB/browser views expose edges-0 through edges-15 as static states, with no animation clock.

Reproduce this family only: python Tools/three_d/author_foam_wall.py. Deterministic check: --check --skip-reviews. Evidence: generated/foam-wall-source-audit.json and generated/foam-wall-proof.json. Review: generated/review/foam-wall/source-model-montage.png. Whole-library export and native execution are coordinated separately.

## Attribution

Original sprites and derived crops: CC-BY-SA-3.0. Preserve attribution on redistribution.

'''+meta['copyright']+'\n'
    write(NOTE,note.encode())
    proof=dict(status='dedicated-assets-ready',modelId=model['id'],models=1,visibleRedux=21,visibleClassic=0,
        surfaces=len(pool.rows),atlasIndices=[r['atlasIndex'] for r in pool.rows],crops=pool.evidence,
        poses=poses,contexts=contexts,actualMasks=dict(sorted(Counter(e['edgeMask'] for e in contexts).items())),
        sourceAlpha=sorted({v for im in images.values() for v in im.getchannel('A').tobytes()}),
        preservedDuplicateUids=[16177,16181],generatorSha256=sha(Path(__file__)),
        adapterSha256=sha(ROOT/'Tools/three_d/foam_wall_states.py'),
        writtenAssetsSha256={p.relative_to(ROOT).as_posix():sha(p) for p in WRITTEN},
        verification=dict(exactTopRgbaCompositions=16,portableStaticStates=16,nativeExecuted=False),
        limitations=['Height, cellular crowns and unseen sides are inferred.',
                    'Dithered solid alpha approximates source blending.',
                    'Saved source duplicates preserved; no universal contextual clearance claim.'])
    write(GEN/'foam-wall-proof.json',(json.dumps(proof,indent=2)+'\n').encode())
    print(json.dumps(dict(models=1,surfaces=len(pool.rows),parts=[poses[0]['parts'],poses[15]['parts']],
                         contexts=len(contexts),actualMasks=proof['actualMasks'])))


if __name__=='__main__':
    main()
