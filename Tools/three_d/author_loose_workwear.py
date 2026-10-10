"""Source-specific dropped sneakers and folded hazard-vest shells; no global export."""
from pathlib import Path
from copy import deepcopy
from collections import Counter, defaultdict
from io import BytesIO
import argparse
import hashlib
import json
import math
import sys
import numpy as np
import yaml
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'Tools/three_d'))
import build_models as bm
import scene
import sprite_states
import surfaces
from author_cash_cutlery import rectangles
from placement import resolve_placements

GEN = ROOT / 'Tools/three_d/generated'
BASE = ROOT / '.codex/model-batch-baseline994'
REVIEW = GEN / 'review/loose-workwear'
MODEL = ROOT / 'Content.CMU/Resources/ThreeD/Prototypes/World/garrison_loose_workwear.yml'
ART = MODEL.with_name('garrison_loose_workwear_art.yml')
TEXTURES = ROOT / 'Content.CMU/Resources/Textures/CMU14/ThreeD/Surfaces/LooseWorkwear'
NOTE = ROOT / 'Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_LOOSE_WORKWEAR.md'
CHECK = False
WRITTEN = []

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def write(path, data):
    if CHECK:
        assert path.read_bytes() == data, f'Deterministic asset differs: {path}'
    else:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
    WRITTEN.append(path)

def png(image):
    out = BytesIO()
    image.save(out, format='PNG')
    return out.getvalue()

def rgb(pixel):
    assert pixel[3] == 255
    return '#' + ''.join(f'{x:02X}' for x in pixel[:3])

def box(label, rect, low, high, color):
    x0, y0, x1, y1 = rect
    return dict(label=label, min=[(x0-16)/32, (16-y1)/32, low], max=[(x1-16)/32, (16-y0)/32, high], color=color)

class Pool:
    def __init__(self):
        self.rows, self.evidence, self.cache = [], [], {}
        self.registry = surfaces.load_surfaces()
        surfaces.load_surfaces = lambda: self.registry
        for path in MODEL.parent.glob('*.yml'):
            if path == ART:
                continue
            for row in yaml.load(path.read_text(encoding='utf-8-sig'), Loader=yaml.CSafeLoader) or []:
                assert row.get('type') != 'cmu3DSurface' or not 2900 <= row['atlasIndex'] <= 2999, path

    def top(self, part, original, rect, prototype):
        crop = original.crop(rect)
        assert crop.getchannel('A').getextrema() == (255, 255)
        if np.all(np.asarray(crop) == np.asarray(crop)[0,0]):
            part['color'] = rgb(crop.getpixel((0, 0)))
            written = Image.new('RGBA', crop.size, crop.getpixel((0, 0)))
            surface = None
        else:
            key = (crop.size, crop.tobytes())
            if key not in self.cache:
                index = 2900 + len(self.rows)
                assert index < 3000, 'Reserved atlas range exhausted'
                uid = f'CMU3DLooseWorkwearSurface{index}'
                write(TEXTURES / (uid + '.png'), png(crop))
                row = dict(type='cmu3DSurface', id=uid, atlasIndex=index,
                    texture=f'/Textures/CMU14/ThreeD/Surfaces/LooseWorkwear/{uid}.png')
                self.rows.append(row)
                self.registry[uid] = {**row, 'file':TEXTURES/(uid+'.png'), 'image':crop}
                self.cache[key] = uid
            surface = self.cache[key]
            part.update(color='#FFFFFF', surface=surface, surfaceAxis='XY')
            written = Image.open(TEXTURES / (surface + '.png')).convert('RGBA')
        assert written.size == crop.size and written.tobytes() == crop.tobytes()
        self.evidence.append(dict(prototype=prototype, rect=list(rect), surface=surface,
            uniformColor=part['color'] if surface is None else None, pixels=crop.width*crop.height,
            rgbaSha256=hashlib.sha256(written.tobytes()).hexdigest()))
        return written

def make_geometry(uid, original, pool):
    alpha = np.asarray(original)[:, :, 3] > 0
    assert set(np.asarray(original)[:, :, 3].ravel()) <= {0, 255}
    field = np.zeros((32, 32), dtype=int)
    groups = np.zeros((32, 32), dtype=int)
    shoe = uid.startswith('RMCShoes')
    for y, x in zip(*np.where(alpha)):
        if shoe:
            # Each source-drawn shoe keeps its own heel, lowered toe and sole.
            # The stagger in the pair is already in the icon and is not recentered.
            split = 18 if y == 11 else 17 if y <= 15 else 16 if y == 16 else 15 if y == 17 else 14
            left = x < split
            groups[y, x] = 1 if left else 2
            heights = {11:170, 12:170, 13:170, 14:170, 15:118, 16:82, 17:59, 18:59, 19:38, 20:14} if left else {
                12:188, 13:188, 14:188, 15:188, 16:132, 17:96, 18:66, 19:49, 20:34, 21:14}
            field[y, x] = heights[y]
            # A recessed join exposes two individual uppers while preserving
            # every sole/plan pixel; it is an inferred gap, not new alpha art.
            if y >= 12 and x == split-1 and alpha[y, split]:
                field[y, x] = 18
        else:
            groups[y, x] = 1
            # Open neck/arm silhouette and separate shoulder straps come from
            # the source alpha. Raised folded panels surround a lower closure.
            field[y, x] = 60 if y <= 12 else 44 if y <= 15 else 32 if y <= 18 else 18
            if x in (15, 16) and y >= 12:
                field[y, x] = 16
    if shoe:
        # The two dark source ankle recesses become actual lower cavity beds,
        # bounded by raised collar rims rather than black painted flat tops.
        field[13:14, 13:16] = 30
        field[14:15, 19:22] = 34
        assert alpha[13:14, 13:16].all() and alpha[14:15, 19:22].all()
    field[~alpha] = 0
    parts, patches = [], []
    lining = .010 if shoe else .004
    underside = rgb(original.getpixel((10,20) if shoe else (15,19)))
    side = rgb(original.getpixel((17,15) if shoe else (11,12)))
    for index, (rect, group) in enumerate(rectangles(groups)):
        label = f'{"left" if group == 1 else "right"} shoe sole' if shoe else 'folded vest backing'
        parts.append(box(f'{label} section {index+1}', rect, 0, lining, underside))
    reconstruction = Image.new('RGBA', (32,32))
    for index, (rect, height) in enumerate(rectangles(field)):
        top = height / 1000
        assert top - .002 > lining
        label = 'shoe upper' if shoe else 'folded vest panel'
        parts.append(box(f'{label} section {index+1}', rect, lining, top-.002, side))
        cap = box(f'{label} original fabric {index+1}', rect, top-.002, top, '#FFFFFF')
        written = pool.top(cap, original, rect, uid)
        parts.append(cap)
        reconstruction.paste(written, rect[:2])
        patches.append(dict(rect=list(rect),height=top,surface=cap.get('surface'),color=cap['color']))
    assert reconstruction.tobytes() == original.tobytes()
    assert np.array_equal(field > 0, alpha)
    if shoe:
        assert field[13,14] == 30 and field[12,14] == 170 and field[14,20] == 34 and field[13,20] == 188
        assert field[15,16] == 18 and field[15,15] == 118 and field[15,17] == 188
    return parts, dict(prototype=uid, parts=len(parts), sourceAlphaPixels=int(alpha.sum()), sourceBounds=list(original.getbbox()),
        originalRgbaSha256=hashlib.sha256(original.tobytes()).hexdigest(), exactPrintedPlanReconstruction=True,
        heightRange=[0, max(p['max'][2] for p in parts)], patches=patches,
        construction='Two staggered soles, descending toes, tall collars, two recessed ankle beds and a lowered seam between the uppers.' if shoe else
            'Thin folded backing, lowered central closure, raised front panels and shoulder straps around the source neck opening.')

def serialize(models):
    rows = deepcopy(models)
    for model in rows:
        for key in ('groundOffset', 'sourceSpriteOffset'):
            model[key] = ', '.join(str(v) for v in model[key])
        for parts in [model['parts'], model['spriteStates']['icon']['frames'][0]['parts']]:
            for part in parts:
                for key in ('min','max'):
                    part[key] = ', '.join(f'{v:.8g}' for v in part[key])
    return rows

def review(models, originals):
    REVIEW.mkdir(parents=True, exist_ok=True)
    font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 14)
    montage = Image.new('RGB', (1200, 1180), '#17212B')
    draw = ImageDraw.Draw(montage)
    for row, model in enumerate(models):
        y = row*295
        draw.text((12,y+10),model['sourcePrototypes'][0]+' / dropped icon only; physical depth inferred',font=font,fill='white')
        im = originals[model['sourcePrototypes'][0]].resize((240,240),Image.Resampling.NEAREST)
        bg = Image.new('RGBA',(240,240),'#60717A');bg.alpha_composite(im);montage.paste(bg.convert('RGB'),(10,y+45))
        for column, (yaw,pitch) in enumerate(((-math.pi/2,.90),(-.20,.52),(math.pi/2,.28))):
            view = bm.render_model(model,(310,250),yaw,pitch,pixels_per_unit=520,screen_origin=(145,155))
            montage.paste(view,(260+column*310,y+35))
    montage.save(REVIEW/'source-model-montage.png')

def contexts(models, source, draw):
    library = {m['id']:m for m in json.loads((BASE/'models.json').read_text())['models']+models}
    by_proto = {m['sourcePrototypes'][0]:m for m in models}
    records, hashes = [], {}
    for spec in json.loads((BASE/'scenes.json').read_text()):
        path = BASE/spec['file'];doc=json.loads(path.read_text())
        targets = [e for e in doc['instances'] if e['prototype'] in by_proto]
        if not targets:
            continue
        hashes[path.relative_to(ROOT).as_posix()] = sha(path)
        for e in targets:
            e.update(modelId=by_proto[e['prototype']]['id'], matchKind='exact', renderYaw=e['yaw'])
            e.pop('geometryKey',None)
        resolve_placements(doc['instances'],list(library.values()),doc.get('geometryVariants',{}))
        for e in targets:
            nearby = []
            for other in doc['instances']:
                if other['id'] == e['id'] or max(abs(other['position'][i]-e['position'][i]) for i in (0,1)) > .9:
                    continue
                model = library.get(other.get('modelId'))
                if not model:
                    nearby.append(dict(uid=other['id'],prototype=other['prototype'],mapped=False));continue
                nearby.append(dict(uid=other['id'],prototype=other['prototype'],mapped=True,
                    sourceCoincident=other['position']==e['position'] and other['yaw']==e['yaw']))
            records.append(dict(variant=spec['variant'],level=spec['level'],uid=e['id'],prototype=e['prototype'],modelId=e['modelId'],
                position=e['position'],yaw=e['yaw'],renderYaw=e['renderYaw'],renderOffset=e.get('renderOffset',[0,0,0]),support=e.get('support'),neighbors=nearby))
    assert Counter(r['variant'] for r in records)=={'redux':41,'classic':2}
    return records,hashes

def main():
    global CHECK
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check',action='store_true');parser.add_argument('--skip-context',action='store_true');parser.add_argument('--skip-reviews',action='store_true')
    args=parser.parse_args();CHECK=args.check
    source=json.loads((GEN/'loose-workwear-source-audit.json').read_text());pool=Pool();models=[];proof=[];originals={}
    for uid in source['ids']:
        sprite=source['defaults'][uid]['Sprite'];assert sprite['state']=='icon' and not sprite.get('layers') and sprite['noRot'] is False
        original=Image.open(bm.resource_file(sprite['sprite'])/'icon.png').convert('RGBA');originals[uid]=original
        parts,evidence=make_geometry(uid,original,pool);proof.append(evidence)
        model=dict(type='cmu3DModel',id='CMU3D'+uid,label=uid,status='draft',sourcePrototypes=[uid],referencePrototype=uid,
            referenceRsi=sprite['sprite'],referenceState='icon',referenceTint='#FFFFFF',sourceDirections=1,
            useEntityRotation=True,placement='surface',groundOffset='0, 0',sourceSpriteOffset='0, 0',sourceSpriteRotates=True,
            description='Source-specific dropped garment with exact original icon pixels and physical sole/collar or folded-cloth construction. Worn and stained appearances are unsupported; see SOURCES_LOOSE_WORKWEAR.md.',
            parts=parts,spriteStates={'icon':{'frames':[{'parts':deepcopy(parts)}],'delays':[1]}})
        model=bm.validate_model(model);sprite_states.validate_source(model,bm.resource_file);models.append(model)
    for path,rows in ((MODEL,serialize(models)),(ART,pool.rows)):
        write(path,('# Generated by Tools/three_d/author_loose_workwear.py.\n'+yaml.safe_dump(rows,sort_keys=False,width=110)).encode())
    registry=surfaces.load_surfaces()
    for row in pool.rows:
        path=TEXTURES/(row['id']+'.png');registry[row['id']]={**row,'file':path,'image':Image.open(path).convert('RGBA')}
    surfaces.load_surfaces=lambda:registry
    assert len(bm.load_models(MODEL))==4
    if not args.skip_reviews:review(models,originals)
    context_records,hashes=([],{}) if args.skip_context else contexts(models,source,not args.skip_reviews)
    state_checks=[];lookup={m['sourcePrototypes'][0]:m for m in models}
    for record in source['records']:
        assert set(record['savedComponents'])=={'Transform'}
        model=lookup[record['prototype']]
        state,reason=sprite_states.saved_pose(model,source['defaults'][record['prototype']],record['savedComponents'],scene.normalize_tint)
        assert state=='icon' and reason is None
        state_checks.append(dict(variant=record['variant'],level=record['level'],uid=record['uid'],state=state,visible=record['exported'],hidden=record['hiddenContainer']))
    fallback_checks=[]
    for uid,model in lookup.items():
        for reason,saved in [('unknown state',{'Sprite':{'state':'unsupported'}}),('worn state',{'Sprite':{'state':'equipped-FEET'}}),
            ('extra visible layer',{'Sprite':{'layers':[{'state':'icon'},{'state':'icon'}]}}),('layer tint',{'Sprite':{'layers':[{'state':'icon','color':'#AA0000'}]}}),
            ('source offset',{'Sprite':{'offset':'0.1, 0'}}),('unknown appearance',{'Appearance':{'data':{'unknown':True}}}),
            ('stained source',{'CMUItemStain':{'color':'#AA0000'}}),('alpha-zero stain',{'CMUItemStain':{'color':'#AA000000'}})]:
            state,error=sprite_states.saved_pose(model,source['defaults'][uid],saved,scene.normalize_tint)
            assert state is None and error,(uid,reason,state,error)
            fallback_checks.append(dict(prototype=uid,case=reason,reason=error))
    groups=defaultdict(list)
    for r in context_records:groups[(r['variant'],r['level'],tuple(r['position']),r['prototype'])].append(r['uid'])
    duplicates=[dict(variant=k[0],level=k[1],position=list(k[2]),prototype=k[3],uids=v) for k,v in groups.items() if len(v)>1]
    attribution='\n\n'.join(dict.fromkeys(json.loads((bm.resource_file(m['referenceRsi'])/'meta.json').read_text())['copyright'] for m in models))
    note='''# Loose workwear: dropped items only

Four exact mappings: RMCShoesPrisoner, RMCShoesBlack, RMCShoesBlue and AU14CivilianHazardVestKellandMiningCorporation. All source world states are static, one-direction `icon`, noRot=false, zero offset. Saved transforms remain unchanged. Equipped-FEET and equipped-OUTERCLOTHING are character clothing sprites and are deliberately not model states.

The 79 raw records contain 43 visible world items (41 Redux, 2 classic) and 36 contained shoes. Prisoner shoes account for 13 visible Redux and 32 contained records; black shoes have one visible and two contained per map variant; blue shoes have one visible per variant. All 26 Kelland vests are visible on the Redux surface. Raw overrides are Transform only, including after explicitly retaining CMUItemStain/CMItemSlots/ItemSlots in the source audit.

Sneakers preserve the original pair silhouette and source pixels: individual dark soles, descending toe sections, taller staggered ankle collars and two recessed beds at the source's dark ankle apertures. A one-pixel-wide lowered seam separates the uppers above the intact sole footprint. Collar tops are 0.170/0.188 tiles, while toe tops descend to 0.038/0.049 and sole tips to 0.014; this makes the pair legible from low views. The dropped vest is a shallow folded shell, with separate raised front panels, low central closure and shoulder straps around the original neck/arm alpha cutout. Written texture crops or exact uniform palette solids reconstruct each original RGBA frame without interpolation, recentering or filled transparent pixels. Depth, unseen sides, ankle recess depth and the folded-cloth interpretation are explicit construction inferences, not recovered 3D evidence.

The existing sourceSpriteRotates/spriteStates contract selects only the actual clean icon layer and frame. Unknown states, multiple visible layers, layer tint, altered source offset or unknown Appearance data use the original sprite fallback. Overall sprite tint remains owned by the runtime presentation adapter. CMInventorySystem changes shoe fill only if an enum.CMItemSlotsLayers.Fill layer exists; these shoe icons have no such layer, so starting hidden cash does not alter the visible shoe. CMUItemStain.color is null when clean; a non-null stain makes CMUItemStainVisualizerSystem add shader/mask layers, which remain outside the authored clean-icon contract. No stain, worn-clothing or attachment geometry is invented.

Surface placement uses existing authored support footprints and the 0.002 gap, otherwise the item remains on the floor. All 43 visible placements and their selected supports are inspected against the frozen 994 library. Source-coincident prison-shoe groups and large vest piles retain their saved pivots and therefore overlap; a reusable source-order item-pile adapter is separate work. Nearby mapped/unknown neighbors, exact duplicate identities and saved support offsets remain in loose-workwear-proof.json. No exhaustive part-contact audit or universal fit/gameplay collision claim is made for these known overlapping piles.

Reproduce only this batch with `python Tools/three_d/author_loose_workwear.py`. Use `--check --skip-reviews` to check deterministic assets. The generator does not export the global library, build native projects or launch the game/server. Source evidence: generated/loose-workwear-source-audit.json. Review: generated/review/loose-workwear/source-model-montage.png.

## Attribution

Original icons and derived crops are CC-BY-SA-3.0. Retain original attribution on redistribution.

'''+attribution+'\n'
    write(NOTE,note.encode())
    report=dict(status='dedicated-assets-ready',models=4,ids=source['ids'],surfaces=len(pool.rows),atlasIndices=[r['atlasIndex'] for r in pool.rows],
        geometry=proof,crops=pool.evidence,sourceStateChecks=state_checks,fallbackChecks=fallback_checks,contexts=context_records,sourceCoincidentGroups=duplicates,
        sourceAuditSha256=sha(GEN/'loose-workwear-source-audit.json'),contextInputSha256=hashes,
        writtenAssetsSha256={p.relative_to(ROOT).as_posix():sha(p) for p in WRITTEN},generatorSha256=sha(Path(__file__)),
        spriteStateAdapterSha256=sha(ROOT/'Tools/three_d/sprite_states.py'),
        limitations=['Dropped clean world items only; character worn appearances stay sprites.',
            'Physical depth and unseen construction inferred; printed source pixels and alpha footprint retained exactly.',
            'Source pile overlaps, unknown neighbors and any support contacts are explicit unresolved fitting work.',
            'Dedicated offline checks only; final shared exports are coordinated separately.'])
    destination=GEN/('loose-workwear-geometry-proof.json' if args.skip_context else 'loose-workwear-proof.json')
    destination.write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps(dict(models=4,surfaces=len(pool.rows),parts={p['prototype']:p['parts'] for p in proof},
        contexts=len(context_records),hidden=sum(r['hidden'] for r in state_checks),fallbackChecks=len(fallback_checks),
        sourceCoincidentGroups=len(duplicates))))

if __name__=='__main__':main()
