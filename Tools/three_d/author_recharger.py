"""Author the source-owned charging dock only; never run global exports or builds."""
from pathlib import Path
from copy import deepcopy
from collections import Counter
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
import charger_states as cs
import surfaces
import scene
from author_cash_cutlery import rectangles
from placement import resolve_placements

GEN = ROOT / 'Tools/three_d/generated'
BASELINE = ROOT / '.codex/model-batch-baseline1006'
REVIEW = GEN / 'review/recharger'
MODEL = ROOT / 'Content.CMU/Resources/ThreeD/Prototypes/World/garrison_recharger.yml'
ART = MODEL.with_name('garrison_recharger_art.yml')
TEXTURES = ROOT / 'Content.CMU/Resources/Textures/CMU14/ThreeD/Surfaces/Recharger'
NOTE = ROOT / 'Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_RECHARGER.md'
CHECK = False
WRITTEN = []


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write(path, value):
    if CHECK:
        assert path.read_bytes() == value, f'Non-deterministic asset: {path}'
    else:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(value)
    WRITTEN.append(path)


def png(image):
    stream = BytesIO(); image.save(stream, format='PNG'); return stream.getvalue()


def rgb(pixel):
    assert pixel[3] == 255
    return '#' + ''.join(f'{v:02X}' for v in pixel[:3])


def volume(label, rect, front, rear, color):
    x0, y0, x1, y1 = rect
    return dict(label=label, min=[(x0-16)/32, front, (24-y1)/32],
                max=[(x1-16)/32, rear, (24-y0)/32], color=color)


class Pool:
    def __init__(self):
        self.rows, self.cache, self.evidence = [], {}, []
        self.registry = surfaces.load_surfaces()
        surfaces.load_surfaces = lambda: self.registry
        for path in MODEL.parent.glob('*.yml'):
            if path == ART:
                continue
            for row in yaml.load(path.read_text(encoding='utf-8-sig'), Loader=yaml.CSafeLoader) or []:
                assert row.get('type') != 'cmu3DSurface' or not 3100 <= row['atlasIndex'] <= 3199, path

    def front(self, part, image, rect, source, frame):
        crop = image.crop(rect)
        assert crop.getchannel('A').getextrema() == (255, 255)
        surface = None
        if np.all(np.asarray(crop) == np.asarray(crop)[0, 0]):
            part['color'] = rgb(crop.getpixel((0, 0)))
            actual = Image.new('RGBA', crop.size, crop.getpixel((0, 0)))
        else:
            key = (crop.size, crop.tobytes())
            if key not in self.cache:
                index = 3100 + len(self.rows)
                assert index < 3200
                uid = f'CMU3DRechargerSurface{index}'
                path = TEXTURES / (uid+'.png')
                write(path, png(crop))
                row = dict(type='cmu3DSurface', id=uid, atlasIndex=index,
                           texture=f'/Textures/CMU14/ThreeD/Surfaces/Recharger/{uid}.png')
                self.rows.append(row)
                self.registry[uid] = {**row, 'file':path, 'image':crop}
                self.cache[key] = uid
            surface = self.cache[key]
            part.update(color='#FFFFFF', surface=surface, surfaceAxis='XZ')
            actual = Image.open(TEXTURES/(surface+'.png')).convert('RGBA')
        assert actual.tobytes() == crop.tobytes() and actual.size == crop.size
        self.evidence.append(dict(sourceState=source, frame=frame, rect=list(rect), surface=surface,
            uniformColor=part['color'] if surface is None else None,
            pixels=crop.width*crop.height, rgbaSha256=hashlib.sha256(actual.tobytes()).hexdigest()))


def geometry(image, source, frame, pool):
    """Source alpha partitions become closed, stepped solids with exact front art."""
    alpha = np.asarray(image)[:, :, 3] > 0
    field = np.zeros((32, 32), dtype=int)
    if source == cs.BASE:
        field[alpha] = 1
        # Continuous housing, inset keypad and a genuinely recessed open channel.
        field[11:19, 9:16] = 2
        field[11:18, 17:19] = 3
        field[11:18, 20:22] = 4
        field[19:21, 9:16] = 5
        field[22:24, :] = 6
        field[11:13, 9:10] = 7
        for y in (16,17):
            for x in (9,11,13):
                field[y,x] = 8
        # Front depth, rear depth and a source-palette unseen-side color.
        sections = {1:(-.165,.140,'#595959'), 2:(-.145,.140,'#ABA790'),
                    3:(-.148,.140,'#4A7C84'), 4:(.025,.140,'#312B2B'),
                    5:(-.175,.140,'#807C65'), 6:(-.150,.130,'#312B2B'),
                    7:(-.179,.140,'#B32222'), 8:(-.179,.140,'#423C3C')}
    elif source in cs.LIGHTS:
        field[alpha] = 1
        sections = {1:(-.185,-.180,'#FFFF44')}
    else:
        field[alpha] = 1
        # Inserted overlays sit in the front-open channel; source order is taser,
        # then baton. Their unseen depth is inferred rather than image evidence.
        sections = {1:(-.230,-.080,'#595959')} if source == cs.INSERTS[0] else {1:(-.270,-.170,'#202728')}
    field[~alpha] = 0
    parts, patches = [], []
    for index, (rect, section) in enumerate(rectangles(field)):
        front, rear, color = sections[section]
        # A separate body ends behind the original printed/front surface so
        # the rear and lateral construction do not duplicate the front artwork.
        skin = min(.003, (rear-front)/3)
        parts.append(volume(f'{source} frame {frame} section {index} solid', rect, front+skin, rear, color))
        face = volume(f'{source} frame {frame} section {index} face', rect, front, front+skin, '#FFFFFF')
        pool.front(face, image, rect, source, frame)
        parts.append(face)
        patches.append(dict(rect=list(rect), front=front, rear=rear, section=section))
    assert sum((p['rect'][2]-p['rect'][0])*(p['rect'][3]-p['rect'][1]) for p in patches) == int(alpha.sum())
    return parts, dict(state=source, frame=frame, parts=len(parts), opaquePixels=int(alpha.sum()),
        sourceRgbaSha256=hashlib.sha256(image.tobytes()).hexdigest(), patches=patches)


def front_projection(parts, pool):
    """Independent nearest-face query using actual bounds and written UV textures."""
    output = Image.new('RGBA', (32,32))
    for y in range(32):
        z = (24-y-.5)/32
        for x in range(32):
            px = (x+.5-16)/32
            candidates = [p for p in parts if p['min'][0] < px < p['max'][0] and p['min'][2] < z < p['max'][2]]
            if not candidates:
                continue
            part = min(candidates,key=lambda p:p['min'][1])
            if part.get('surface'):
                crop = pool.registry[part['surface']]['image']
                u = (px-part['min'][0])/(part['max'][0]-part['min'][0])
                v = (part['max'][2]-z)/(part['max'][2]-part['min'][2])
                color = crop.getpixel((min(crop.width-1,int(u*crop.width)),min(crop.height-1,int(v*crop.height))))
            else:
                color = tuple(bytes.fromhex(part['color'][1:])) + (255,)
            output.putpixel((x,y),color)
    return output


def serialize(model):
    result = deepcopy(model)
    def visit(value):
        if isinstance(value,dict):
            for k,v in list(value.items()):
                if k in ('min','max','groundOffset'):
                    value[k] = ', '.join(f'{n:.9g}' for n in v)
                else:
                    visit(v)
        elif isinstance(value,list):
            for item in value:visit(item)
    visit(result)
    return result


def reviews(model, images):
    REVIEW.mkdir(parents=True,exist_ok=True)
    font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',16)
    canvas = Image.new('RGB',(1320,1030),'#17212B');draw=ImageDraw.Draw(canvas)
    poses = [('recharger-0',0,()),('recharger-3',0,(cs.INSERTS[0],)),
             ('recharger-5',0,(cs.INSERTS[1],)),('recharger-5',1,cs.INSERTS)]
    for row,(light,frame,inserted) in enumerate(poses):
        y=row*255
        draw.text((12,y+8),f'{light}, frame {frame} / '+(', '.join(inserted) or 'empty')+' / physical unseen depth inferred',font=font,fill='white')
        original=images[cs.BASE][0].copy();original.alpha_composite(images[light][frame])
        for state in inserted:original.alpha_composite(images[state][0])
        bg=Image.new('RGBA',(220,220),'#60717A');bg.alpha_composite(original.resize((220,220),Image.Resampling.NEAREST));canvas.paste(bg.convert('RGB'),(10,y+32))
        pose={**model,'parts':cs.compose_parts(model,light,frame,inserted)}
        for col,(yaw,pitch) in enumerate(((-math.pi/2,.05),(-.65,.48),(1.0,.42))):
            canvas.paste(bm.render_model(pose,(350,220),yaw,pitch,pixels_per_unit=315,screen_origin=(170,190)),(240+col*350,y+32))
    canvas.save(REVIEW/'source-model-montage.png')


def contexts(model, source):
    models = json.loads((BASELINE/'models.json').read_text())['models']+[model]
    results, hashes = [], {}
    for spec in json.loads((BASELINE/'scenes.json').read_text()):
        path=BASELINE/spec['file'];doc=json.loads(path.read_text())
        targets=[e for e in doc['instances'] if e['prototype']=='RMCRecharger']
        if not targets:continue
        hashes[path.relative_to(ROOT).as_posix()]=sha(path)
        for entity in targets:
            entity.update(modelId=model['id'],matchKind='exact',renderYaw=0)
            entity.pop('geometryKey',None)
        resolve_placements(doc['instances'],models,doc.get('geometryVariants',{}))
        for e in targets:
            neighbors=[dict(uid=o['id'],prototype=o['prototype'],modelId=o.get('modelId'),position=o['position'])
                for o in doc['instances'] if o['id']!=e['id'] and max(abs(o['position'][i]-e['position'][i]) for i in (0,1))<.8]
            results.append(dict(variant=spec['variant'],level=spec['level'],uid=e['id'],prototype=e['prototype'],
                position=e['position'],yaw=e['yaw'],renderYaw=e['renderYaw'],support=e.get('support'),
                renderOffset=e.get('renderOffset',[0,0,0]),neighbors=neighbors))
    assert len(results)==17 and all(r['variant']=='redux' for r in results)
    return results,hashes


def main():
    global CHECK
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check',action='store_true');parser.add_argument('--skip-reviews',action='store_true')
    args=parser.parse_args();CHECK=args.check
    pool=Pool(); source=json.loads((GEN/'recharger-source-audit.json').read_text())
    model=dict(type='cmu3DModel',id='CMU3DRMCRecharger',label='Weapon charging dock',status='draft',
        sourcePrototypes=['RMCRecharger'],referencePrototype='RMCRecharger',referenceRsi=cs.RSI,
        referenceState=cs.BASE,referenceTint='#FFFFFF',sourceDirections=1,placement='surface',groundOffset=[0,0],
        description='Physical charging dock with source-owned base, six indicator states and independent taser/baton inserts. Unshaded illumination is approximated by source color; see SOURCES_RECHARGER.md.')
    images=cs.validate_source(model,bm.resource_file); evidence=[]; geometry_by_state={}
    for state,frames in images.items():
        geometry_by_state[state]=[]
        for frame,image in enumerate(frames):
            parts,proof=geometry(image,state,frame,pool);geometry_by_state[state].append(parts);evidence.append(proof)
    model['chargerAppearance']=dict(baseParts=geometry_by_state[cs.BASE][0],
        lightStates={s:dict(frames=[dict(parts=p) for p in geometry_by_state[s]],delays=[.1,.1] if s=='recharger-5' else [1]) for s in cs.LIGHTS},
        insertedStates={s:dict(parts=geometry_by_state[s][0]) for s in cs.INSERTS})
    model['parts']=cs.compose_parts(model)
    model=bm.validate_model(model)
    poses=[]
    for light in (*cs.LIGHTS,'hidden'):
        for mask in range(4):
            inserted=[s for i,s in enumerate(cs.INSERTS) if mask&(1<<i)]
            for frame in range(2 if light=='recharger-5' else 1):
                original=images[cs.BASE][0].copy()
                if light!='hidden':original.alpha_composite(images[light][frame])
                for state in inserted:original.alpha_composite(images[state][0])
                parts=cs.compose_parts(model,light,frame,inserted)
                reconstructed=front_projection(parts,pool)
                assert reconstructed.tobytes()==original.tobytes(), (light,frame,inserted)
                poses.append(dict(light=light,frame=frame,inserted=inserted,parts=len(parts),exactFrontRgba=True,
                    rgbaSha256=hashlib.sha256(reconstructed.tobytes()).hexdigest()))
    for path,rows in ((MODEL,[serialize(model)]),(ART,pool.rows)):
        write(path,('# Generated by Tools/three_d/author_recharger.py.\n'+yaml.safe_dump(rows,sort_keys=False,width=110)).encode())
    assert len(bm.load_models(MODEL))==1
    records,hashes=contexts(model,source)
    source_checks=[]
    for record in source['records']:
        pose,reason=cs.saved_pose(model,source['defaults'],record['savedComponents'],scene.normalize_tint)
        assert pose and reason is None and pose['light']=='recharger-0' and pose['frame']==0 and not pose['inserted']
        source_checks.append(dict(variant=record['variant'],level=record['level'],uid=record['uid'],pose=pose))
    if not args.skip_reviews:reviews(model,images)
    meta=json.loads((bm.resource_file(cs.RSI)/'meta.json').read_text())
    note='''# Source-owned charging dock

One exact mapping: RMCRecharger. Seventeen saved Redux chargers are visible, with no classic placements. Every saved override is Transform only; source slots and machine containers are empty, so source MapInit selects recharger-0 with no item overlays. Ten saved yaws are zero, six -90 degrees and one +90 degrees. The source is one-direction, noRot=false and snapCardinals=true: cardinal transform yaw cancels in its visible sprite rotation. The model preserves that source-facing rule rather than treating the saved transform as a recovered physical axis. Saved transforms and source pivot remain unchanged; ordinary authored surface support placement is used.

The original base is a solid stepped dock with an inset pale control panel, raised individual keys, a red control, a cyan meter and a recessed front-open charging channel. Two feet remain separate at the bottom. Every opaque source pixel is represented at its original XZ coordinate; transparent silhouette pixels remain empty. A front skin contains exact source pixels or exact uniform palette colors, while separate structural solids provide unseen side/rear construction. The taser and hooked baton inserts are independently shaped solid silhouettes, placed in the source's ItemMapper order; both can coexist. Their depth and the unseen dock construction are explicit inferences, not 3D reconstruction evidence.

All six indicator states are source-owned. States 0–4 have one frame; state 5 has two 0.1-second frames. Native rendering reads the actual Base/Light layer states, current frame, visibility and keyed ItemMapper layers without a second timer or chemistry/charge simulation. Portable GLB/browser studies expose 28 compositions (including hidden-light studies), 32 frame assemblies and four 0.2-second blinking loops. The original Light uses an unshaded shader; source colors and blinking are preserved, but the current 3D renderer still lights those colors normally. True unshaded/emissive illumination remains a documented limitation.

The saved adapter verifies Charger, PowerChargerVisuals, ItemMapper, Sprite layers and known containers before selecting a pose. For fully resolved nonempty source records it follows source MapInit Battery startingCharge/maxCharge, direct or slotted batteries and tags across all containers. The source arithmetic is zero when empty/no battery, 1–4 by ceiling of ratio times four, and 5 at full charge. Unresolved starting items, unknown owners, overlays, layer tints, source transforms, shaders or Appearance data use the original sprite fallback. No gameplay or interaction state is invented. Live sampling reads actual layers rather than this saved-file derivation.

Independent front-ray/UV reconstruction compares all 32 assemblies to source alpha composites byte for byte. Written crops are copied without filtering. The 17 context records retain saved source coordinates and report the chosen mapped support or ordinary floor; they are not a universal no-contact or game-runtime verification claim. Focused Python tests cover all compositions, loop timing, empty/direct/slotted battery selection, both overlays, source thresholds and conservative fallback. Root coordinates native builds and final whole-library exports separately.

Reproduce this family only: `python Tools/three_d/author_recharger.py`; deterministic check: `--check --skip-reviews`. Source evidence: generated/recharger-source-audit.json. Proof: generated/recharger-proof.json. Review: generated/review/recharger/source-model-montage.png.

## Attribution

Original sprites and derived crops are CC-BY-SA-3.0. Preserve attribution on redistribution.

'''+meta['copyright']+'\n'
    write(NOTE,note.encode())
    report=dict(status='dedicated-assets-ready',models=1,modelId=model['id'],prototype='RMCRecharger',visibleRedux=17,visibleClassic=0,
        surfaces=len(pool.rows),atlasIndices=[r['atlasIndex'] for r in pool.rows],geometry=evidence,crops=pool.evidence,poses=poses,
        minParts=min(p['parts'] for p in poses),maxParts=max(p['parts'] for p in poses),sourceStateChecks=source_checks,contexts=records,
        sourceAuditSha256=sha(GEN/'recharger-source-audit.json'),contextInputSha256=hashes,
        writtenAssetsSha256={p.relative_to(ROOT).as_posix():sha(p) for p in WRITTEN},generatorSha256=sha(Path(__file__)),
        adapterSha256=sha(ROOT/'Tools/three_d/charger_states.py'),
        verification=dict(frontRgbaCompositions=len(poses),sourcePixels=sum(p['opaquePixels'] for p in evidence),
            sourceFrames=len(evidence),portableStates=28,animatedLoops=4,loopPeriodSeconds=.2,nativeExecuted=False),
        limitations=['Source unshaded light is normally lit in the current 3D renderer.',
            'Physical depth and unseen sides inferred; exact front source art and alpha retained.',
            'Saved derivation is MapInit only; live charge ownership stays in existing game systems.',
            'Unknown or unresolved source appearances use sprites; no universal contextual clearance claim.'])
    (GEN/'recharger-proof.json').write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps(dict(models=1,surfaces=len(pool.rows),parts=[report['minParts'],report['maxParts']],
        sourceFrames=len(evidence),exactCompositions=len(poses),contexts=len(records),supported=sum(r['support'] is not None for r in records))))


if __name__=='__main__':main()
