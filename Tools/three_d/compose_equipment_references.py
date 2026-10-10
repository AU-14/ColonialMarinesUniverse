"""Compose static layered references without guessing source-owned visual states."""
import argparse
import hashlib
import json
from pathlib import Path
import re

import numpy as np
from PIL import Image, ImageColor

import build_models as bm
import inventory as inv

ROOT = bm.ROOT
DEST = ROOT / 'Content.CMU/Resources/Textures/CMU14/ThreeD/EquipmentReferences.rsi'


POWERED_SNAPSHOTS = {'CMU3DMPSComputer', 'CMU3DMappingComputer', 'CMU3DSensorComputer', 'CMU3DSensorComputerWide'}


def compose(sprite, allow_animation=False):
    rows = sprite.get('layers') or [{'state': sprite.get('state')}]
    layers, evidence = [], []
    for row in rows:
        if row.get('visible') is False: continue
        if any(row.get(key) for key in ('texture', 'shader', 'rotation', 'offset', 'scale')):
            raise ValueError('Custom layer presentation')
        rsi = inv.texture_reference(row.get('sprite') or sprite.get('sprite'))
        path = inv.resource_path(ROOT, rsi)
        state = row.get('state')
        meta = json.loads((path / 'meta.json').read_text(encoding='utf-8-sig'))
        sm = next(s for s in meta['states'] if s['name'] == state)
        if sm.get('directions', 1) != 1 or not allow_animation and any(len(v) > 1 for v in sm.get('delays', [[1]])):
            raise ValueError('Directional or animated reference')
        frame = Image.open(path / (state + '.png')).convert('RGBA').crop((0,0,meta['size']['x'],meta['size']['y']))
        tint = np.asarray(ImageColor.getcolor(row.get('color', '#FFFFFFFF'), 'RGBA')) / 255
        frame = Image.fromarray(np.rint(np.asarray(frame) * tint).astype(np.uint8))
        layers.append(frame)
        evidence.append({'rsi':rsi, 'state':state, 'color':row.get('color','#FFFFFFFF'),
                         'sha256':hashlib.sha256((path / (state + '.png')).read_bytes()).hexdigest(),
                         'license':meta.get('license'), 'copyright':meta.get('copyright')})
    if not layers or any(layer.size != (32,32) for layer in layers): raise ValueError('Nonstandard reference')
    result = Image.new('RGBA',(32,32))
    for layer in layers: result = Image.alpha_composite(result,layer)
    tint = np.asarray(ImageColor.getcolor(sprite.get('color','#FFFFFFFF'),'RGBA'))/255
    return Image.fromarray(np.rint(np.asarray(result)*tint).astype(np.uint8)), evidence


def author(kinds):
    resolver=inv.Resolver(kinds['entity']); records=[]
    DEST.mkdir(parents=True,exist_ok=True)
    for m in kinds['cmu3DModel'].values():
        if not m.get('sourcePrototypes') or m.get('referenceTint'): continue
        uid=m['sourcePrototypes'][0]; comp=inv.component_map(resolver.resolve(uid));sprite=comp.get('Sprite') or {}
        snapshot = m['id'] in POWERED_SNAPSHOTS
        if not snapshot and any('Visual' in key or key in ('RandomSprite','ItemMapper') for key in comp): continue
        layers=[row for row in sprite.get('layers',[]) if row.get('visible',True)]
        if len(layers)<2 and not sprite.get('color') and not any(row.get('color') for row in layers): continue
        rsi=inv.texture_reference(sprite.get('sprite',''));state=(layers or [sprite])[0].get('state')
        if m.get('referenceRsi') and m['referenceRsi'] != '/Textures/CMU14/ThreeD/EquipmentReferences.rsi' and (inv.texture_reference(m['referenceRsi'])!=rsi or m.get('referenceState') not in {entry.get('state') for entry in layers or [sprite]}): continue
        try: image,evidence=compose(sprite, allow_animation=snapshot)
        except (ValueError,StopIteration,FileNotFoundError,TypeError): continue
        mid=m['id'];image.save(DEST/(mid+'.png'))
        path=ROOT/m['_source'];text=path.read_text(encoding='utf-8');blocks=re.split(r'(?=^- type:)',text,flags=re.M)
        for i,block in enumerate(blocks):
            if re.search(r'^  id: '+re.escape(mid)+r'$',block,re.M):
                block=re.sub(r'^  reference(?:Rsi|State):.*\n','',block,flags=re.M)
                block=block.replace('  id: '+mid+'\n','  id: '+mid+'\n  referenceRsi: /Textures/CMU14/ThreeD/EquipmentReferences.rsi\n  referenceState: '+mid+'\n')
                blocks[i]=block
        path.write_text(''.join(blocks),encoding='utf-8',newline='\n')
        records.append({'model':mid,'prototype':uid,'spriteTint':sprite.get('color','#FFFFFFFF'),'layers':evidence,
                        **({'comparisonPose':'powered, first source frame; comparison only, not an animation replacement'} if snapshot else {})})
    (DEST/'meta.json').write_text(json.dumps({'version':1,'size':{'x':32,'y':32},'license':'CC-BY-SA-4.0',
        'copyright':'Composed from unchanged source layers; original creators and per-image licenses are recorded in Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_EQUIPMENT_REFERENCES.json.',
        'states':[{'name':r['model']} for r in records]},indent=2)+'\n',encoding='utf-8')
    (bm.OUTPUT/'SOURCES_EQUIPMENT_REFERENCES.json').write_text(json.dumps(records,indent=2)+'\n',encoding='utf-8')
    print('Composed references:',len(records))


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--cache',type=Path);args=parser.parse_args()
    author(json.loads(args.cache.read_text(encoding='utf-8')) if args.cache else inv.load_prototypes(ROOT)[0])
