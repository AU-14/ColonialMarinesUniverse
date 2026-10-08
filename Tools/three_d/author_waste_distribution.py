"""Source-guided low waste-intake deck and its separately saved broken counterpart."""
import json
import math
from pathlib import Path

from PIL import Image
import yaml
import build_models

ROOT = Path(__file__).resolve().parents[2]
RSI = '_RMC14/Structures/Filtration/96x96.rsi'
SOURCE = ROOT / 'Resources/Textures' / RSI
PROTOTYPES = ROOT / 'Content.CMU/Resources/Prototypes/CMU14/ThreeD'
MODEL_FILE = PROTOTYPES / 'garrison_waste_distribution.yml'
SURFACE_FILE = PROTOTYPES / 'garrison_waste_distribution_art.yml'
TEXTURES = ROOT / 'Content.CMU/Resources/Textures/CMU14/ThreeD/Surfaces'
CROPS = {
    'LeftHazard': (16, 16, 29, 80), 'RightHazard': (65, 16, 79, 80),
    'RearHazard': (29, 16, 65, 29), 'Grille': (30, 34, 64, 44),
    'LeftChannel': (9, 34, 16, 77), 'RightChannel': (79, 34, 86, 77),
    'FrontLeft': (0, 81, 29, 96), 'FrontRight': (65, 81, 96, 96),
}


def box(label, low, high, color, **extra):
    return dict(label=label, min=list(low), max=list(high), color=color, **extra)


def tilted(label, center, size, pitch, color):
    return box(label, [c-s/2 for c,s in zip(center,size)],
               [c+s/2 for c,s in zip(center,size)], color, pitch=pitch)


def build_parts(art, damaged):
    p = [box('left solid deck', (-1.49,-1.49,0), (-.58,1.49,.18), '#646D66'),
         box('right solid deck', (.53,-1.49,0), (1.49,1.49,.18), '#646D66'),
         box('rear solid deck', (-.58,.30,0), (.53,1.49,.18), '#646D66'),
         box('recessed intake shadow bed', (-.58,-1.49,.005), (.53,.30,.025), '#101213'),
         box('rear pale rim', (-1.49,1.43,.18), (1.49,1.49,.22), '#89938C'),
         box('left pale rim', (-1.49,-1.49,.18), (-1.43,1.43,.22), '#89938C'),
         box('right pale rim', (1.43,-1.49,.18), (1.49,1.43,.22), '#89938C')]
    # Warning paint belongs on the horizontal intake rim, not tall door jambs.
    for label, lo, hi, face in [
        ('left hazard rail', (-1,-1.49,.18), (-.59375,.98,.30), 'LeftHazard'),
        ('right hazard rail', (.53125,-1.49,.18), (.96875,.98,.30), 'RightHazard'),
        ('rear hazard cross rail', (-.59375,.60,.18), (.53125,.98,.30), 'RearHazard'),
        ('left copper return channel', (-1.22,-1.38,.18), (-1,.42,.23), 'LeftChannel'),
        ('right copper return channel', (.96875,-1.38,.18), (1.1875,.42,.23), 'RightChannel'),
        ('rear screen recess', (-.56,-.34,.09), (.5,.57,.105), 'Grille')]:
        p.append(box(label, lo, hi, '#FFFFFF', surface=art[face], surfaceAxis='XY'))
    for face, x0, x1 in [('FrontLeft',-1.49,-.59375),('FrontRight',.53125,1.49)]:
        p.append(box('original near deck fascia '+face, (x0,-1.49,.01), (x1,-1.486,.175),
                     '#FFFFFF',surface=art[face],surfaceAxis='XZ'))
    # Rear exposed copper manifold, raised from the deck and open between ribs.
    p.append(box('rear dark manifold bed', (-.48,1.01,.18), (.45,1.20,.25), '#31251C'))
    for x in (-.42,-.25,-.08,.09,.26,.40):
        p.append(box('rear copper manifold rib', (x,1.0,.25), (x+.045,1.22,.30), '#794F32'))
    # A low half-round hood encloses the near-facing intake. Empty volume remains
    # under the arch, with a dark recessed bed and rear screen instead of a solid drum.
    cx, z0, radius = -.03125, .21, .53
    retained = (-80,-60,-40,40,60,80) if damaged else range(-80,81,20)
    rust = ('#4D4C4B','#79685B','#6B4532','#635743','#4D4C4B')
    for i, angle in enumerate(retained):
        rad=math.radians(angle)
        p.append(tilted('broken hood remnant' if damaged else 'curved rusty hood segment',
                        (cx+radius*math.sin(rad),-.98,z0+radius*math.cos(rad)),
                        (.192,1.0,.078),-angle,rust[i%len(rust)]))
    # Side rim survives in both poses; shortened rear portions reveal the break.
    for side in (-1,1):
        x=cx+side*.515
        p.append(box('hood side foot', (x-.035,-1.47,.16), (x+.035,-.48,.25), '#44372C'))
    if damaged:
        p.extend([
            tilted('torn left yellow collar', (-.37,-1.30,.55), (.095,.24,.65), -18, '#979533'),
            tilted('bent right collar arm', (.28,-1.27,.46), (.11,.27,.51), 28, '#918E2C'),
            tilted('collapsed rusty hood plate', (.07,-1.13,.16), (.57,.58,.065), -16, '#6B4532'),
            tilted('loose dark hood shard', (-.13,-.86,.20), (.33,.35,.06), 35, '#4D4C4B'),
            tilted('raised broken upper tab', (.33,-.56,.73), (.26,.21,.055), 25, '#979533'),
        ])
    else:
        for angle in (-60,0,60):
            rad=math.radians(angle)
            p.append(tilted('hood raised seam', (cx+(radius+.047)*math.sin(rad),-.94,z0+(radius+.047)*math.cos(rad)),
                            (.038,.98,.012),-angle,'#79685B'))
    return p


def main():
    previous={e['id']:e['atlasIndex'] for e in yaml.safe_load(SURFACE_FILE.read_text())} if SURFACE_FILE.exists() else {}
    next_index=1+max(e['atlasIndex'] for f in PROTOTYPES.glob('*.yml')
                     for e in yaml.safe_load(f.read_text(encoding='utf-8')) if e['type']=='cmu3DSurface')
    originals={state:Image.open(SOURCE/(state+'.png')).convert('RGBA') for state in ('distribution','distribution-damaged')}
    surfaces, evidence, art = [], [], {}
    for state, image in originals.items():
        art[state]={}
        for name, crop in CROPS.items():
            pixels=image.crop(crop)
            same=pixels.tobytes()==originals['distribution'].crop(crop).tobytes()
            uid='CMU3DWaste'+name+('Damaged' if state.endswith('-damaged') and not same else '')
            art[state][name]=uid
            if any(s['id']==uid for s in surfaces):continue
            pixels.save(TEXTURES/(uid+'.png'))
            if uid not in previous:
                previous[uid]=next_index;next_index+=1
            surfaces.append(dict(type='cmu3DSurface',id=uid,atlasIndex=previous[uid],texture=f'/Textures/CMU14/ThreeD/Surfaces/{uid}.png'))
            evidence.append(dict(surface=uid,state=state,crop=crop,pixels=pixels.width*pixels.height))
    SURFACE_FILE.write_text('# Original CC-BY-SA-3.0 filtration artwork; see SOURCES_FILTRATION.md.\n'+yaml.safe_dump(surfaces,sort_keys=False))
    models=[]
    for state in originals:
        damaged=state.endswith('-damaged')
        proto='RMCFiltrationDistribution'+('Damaged' if damaged else '')
        parts=build_parts(art[state],damaged)
        palette={rgb[:3] for rgb in originals[state].getdata() if rgb[3]==255}
        for p in parts:
            if not p.get('surface'):
                rgb=tuple(int(p['color'][i:i+2],16) for i in (1,3,5))
                chosen=min(palette,key=lambda candidate:(sum((a-b)**2 for a,b in zip(candidate,rgb)),candidate))
                p['color']='#'+''.join(f'{v:02X}' for v in chosen)
            for key in ('min','max'):p[key]=', '.join(f'{v:.7f}' for v in p[key])
        models.append(dict(type='cmu3DModel',id='CMU3DFiltrationDistribution'+('Damaged' if damaged else ''),
                           label='Broken waste intake deck' if damaged else 'Waste intake deck', status='draft',
                           sourcePrototypes=[proto],referencePrototype=proto,referenceRsi=RSI,referenceState=state,
                           sourceDirections=1,useEntityRotation=True,
                           terrainCutoutTargets=['RMCWallKutjevoRockBorder'],
                           terrainCutoutMin='-1.49, -1.49, 0',terrainCutoutMax='1.49, 1.49, 1.10',
                           description='Low blocked waste-intake deck with horizontal source hazard rims, copper channels, '
                           'recessed screen, and a hollow rusted front hood. The damaged source has missing hood plates '
                           'and bent collar/shards. Plan remains within the 3x3 collision footprint. Physical height, '
                           'depth and unseen construction are inferred; static source, no invented animation. '
                           'See SOURCES_FILTRATION.md.',parts=parts))
    MODEL_FILE.write_text('# Source-specific waste intake decks; not walk-through doors.\n'+yaml.safe_dump(models,sort_keys=False,width=110))
    loaded=build_models.load_models(MODEL_FILE)
    for model in loaded:
        for p in model['parts']:
            low,high=build_models.part_bounds(p)
            assert all(-1.49-1e-6<=v<=1.49+1e-6 for values in (low,high) for v in values[:2]), (model['id'],p['label'],low,high)
            assert low[2]>=0, (model['id'],p['label'],low)
    inventory=json.loads((ROOT/'Tools/three_d/generated/inventory.json').read_text())
    build_models.write_reviews(loaded,ROOT/'Tools/three_d/generated/review/waste-distribution',{e['id']:e for e in inventory['prototypes']})
    (ROOT/'Tools/three_d/generated/waste-distribution-crops.json').write_text(json.dumps(evidence,indent=2)+'\n')
    print('Authored',[(m['id'],len(m['parts'])) for m in loaded],len(surfaces),'source crops')


if __name__=='__main__':main()
