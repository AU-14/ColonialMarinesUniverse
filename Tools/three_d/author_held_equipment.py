"""Explicit held poses for modeled spawn equipment, guarded by live source layers.

Ground props retain their geometry. Grip pivots and orientation are attachment
presentation only; firing, inventory and prediction remain owned by the game.
"""
import argparse
from collections import defaultdict
from copy import deepcopy
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageColor
import yaml

import build_models as bm
import inventory as inv


def vector(values):
    return ', '.join(f'{v:.7f}'.rstrip('0').rstrip('.') or '0' for v in values)


def vec(value, count=2):
    return tuple(float(v.strip()) for v in value.split(',')) if isinstance(value,str) else tuple(value or [0]*count)


def layer(sprite, row):
    if row.get('visible') is False:
        return None
    if row.get('shader') or row.get('texture') or row.get('rotation') or row.get('scale', '1, 1') != '1, 1':
        raise ValueError('Transformed or shader source')
    rsi, state = row.get('sprite') or sprite.get('sprite'), row.get('state')
    if not rsi or not state:
        raise ValueError('No static RSI state')
    rsi = inv.texture_reference(rsi)
    path = inv.resource_path(bm.ROOT, rsi)
    meta = json.loads((path/'meta.json').read_text(encoding='utf-8-sig'))
    sm = next(s for s in meta['states'] if s['name'] == state)
    if any(len(row) != 1 for row in sm.get('delays', [[1]])):
        raise ValueError('Animated ground source')
    color = ImageColor.getcolor(row.get('color', '#FFFFFFFF'), 'RGBA')
    return {'rsi':rsi, 'state':state, 'color':'#'+''.join(f'{v:02X}' for v in color),
            'offset':vector(vec(row.get('offset')))}


def item_layers(components, resolver):
    sprite = components['Sprite']
    rows = sprite.get('layers') or [{'state':sprite.get('state')}]
    result = [entry for row in rows if (entry := layer(sprite,row)) is not None]
    offsets = components.get('AttachableHolderVisuals', {}).get('offsets', {})
    for slot, settings in components.get('AttachableHolder', {}).get('slots', {}).items():
        if not settings.get('startingAttachable') or slot not in offsets:
            continue
        attachment = inv.component_map(resolver.resolve(settings['startingAttachable']))
        visual = attachment.get('AttachableVisuals', {})
        source = attachment['Sprite']
        source_layer = (source.get('layers') or [source])[visual.get('layer',0)]
        state = visual.get('prefix') or source_layer.get('state')
        if visual.get('includeSlotName'): state += slot
        state += visual.get('suffix', '_a') or ''
        if visual.get('showActive') and attachment.get('AttachableToggleable', {}).get('active'):
            state += '-on'
        offset = np.asarray(vec(offsets[slot])) + vec(visual.get('offset'))
        result.append(layer(source, {'sprite':visual.get('rsi') or source_layer.get('sprite') or source.get('sprite'),
                                     'state':state,'offset':vector(offset)}))
    return result


def pose_for(model):
    parts = model['parts']
    lows = np.asarray([bm.part_bounds(p)[0] for p in parts]); highs = np.asarray([bm.part_bounds(p)[1] for p in parts])
    pivot = (lows.min(axis=0)+highs.max(axis=0))/2
    # Explicit +X muzzle convention is documented by the weapon-world batch.
    weapon = model['id'].startswith('CMU3DWorld') and ('Weapon' in model['id'] or 'Rifle' in model['id'])
    if model['id'] == 'CMU3DPulseRifleDraft':
        return {'pivot':'-0.073, 0, 0.17','yaw':-90,'offset':'-0.27, -0.22, 1.02','firstPersonOffset':'0.23, 0.52, -0.3'}
    if weapon:
        grip = next((p for p in parts if 'grip' in p['label'].lower() and 'stripe' not in p['label'].lower()), None)
        if grip: pivot = (np.asarray(bm.vector(grip['min']))+bm.vector(grip['max']))/2
        return {'pivot':vector(pivot),'yaw':-90,'roll':90,'offset':'-0.27, -0.22, 1.02','firstPersonOffset':'0.23, 0.52, -0.3'}
    pose = {'pivot':vector(pivot),'offset':'-0.27, -0.13, 0.86','firstPersonOffset':'0.25, 0.52, -0.32'}
    extent = highs.max(axis=0) - lows.min(axis=0)
    if extent[2] < .15 and max(extent[:2]) > .2:
        # Flat loose tools/cards are authored on the floor. Lift their face into
        # the hand's vertical plane instead of presenting only the thin edge.
        pose['roll'] = 90
    return pose


def pulse_rifle_poses(poses, kinds, resolver):
    """Two authored magazine geometries; accept only pixel-identical stock skins.

    MagazineVisuals hides the mag layer without an inserted magazine. The folded
    stock's map camouflages currently share artwork, verified before binding.
    """
    original = kinds['cmu3DModel']['CMU3DPulseRifleDraft']
    empty = {k: deepcopy(v) for k, v in original.items() if not k.startswith('_')}
    empty.update(id='CMU3DPulseRifleUnloadedEquipment', label='Pulse rifle without magazine (held)',
                 equipmentOnly=True, sourcePrototypes=[],
                 description='Rigid held pose without an inserted magazine. The upper magazine well remains part of the receiver; unknown attachments retain their sprite.')
    magazine = next(p for p in empty['parts'] if p['label'] == 'magazine')
    magazine.update(label='empty magazine well', min='0.015, -0.065, 0.11')
    variants = []
    for pose in poses:
        if pose['model'] != original['id']:
            variants.append(pose)
            continue
        stock = next(l for l in pose['itemLayers'] if l['state'] == 'm54c-col_a')
        pixels = np.asarray(Image.open(inv.resource_path(bm.ROOT, stock['rsi']) / (stock['state'] + '.png')).convert('RGBA'))
        attachment = inv.component_map(resolver.resolve('RMCAttachmentM54CStockCollapsible'))
        rsis = {stock['rsi'], *(inv.texture_reference(r) for r in attachment['ItemCamouflage']['camouflageVariations'].values())}
        for rsi in sorted(rsis):
            alternative = np.asarray(Image.open(inv.resource_path(bm.ROOT, rsi) / (stock['state'] + '.png')).convert('RGBA'))
            if not np.array_equal(pixels, alternative):
                continue
            for loaded in (False, True):
                variant = deepcopy(pose)
                variant['id'] += Path(rsi).stem.title() + ('Loaded' if loaded else 'Unloaded')
                variant['model'] = original['id'] if loaded else empty['id']
                variant['itemLayers'] = [l for l in variant['itemLayers'] if loaded or l['state'] != 'mag-0']
                next(l for l in variant['itemLayers'] if l['state'] == stock['state'])['rsi'] = rsi
                variants.append(variant)
    (bm.SOURCE / 'garrison_held_equipment_variants.yml').write_text(
        '# Generated by Tools/three_d/author_held_equipment.py. Source attribution: SOURCES_STYLE_ALIGNMENT.json.\n' +
        yaml.safe_dump([empty], sort_keys=False, width=110), encoding='utf-8', newline='\n')
    return variants


def author(kinds, equipment):
    resolver = inv.Resolver(kinds['entity'])
    poses, skipped = [], []
    groups = defaultdict(list)
    for row in equipment:
        if len(row['models']) != 1:
            continue
        model = kinds['cmu3DModel'][row['models'][0]]
        components = inv.component_map(resolver.resolve(row['prototype']))
        if any(model.get(key) for key in ('doorSpriteStates','poweredLightStates','chargerAppearance','foamAppearance','solutionAppearance','reagentTankAppearance','barricadeDamageStates')):
            skipped.append({'prototype':row['prototype'],'reason':'Specialized appearance adapter needs an equipment pose'})
            continue
        try:
            layers = item_layers(components, resolver)
        except (ValueError, StopIteration, FileNotFoundError, KeyError) as error:
            skipped.append({'prototype':row['prototype'],'reason':str(error) or 'Missing static source'})
            continue
        groups[(model['id'],json.dumps(layers,sort_keys=True))].append(row['prototype'])
    for (mid,signature), ids in sorted(groups.items()):
        poses.append({'type':'cmu3DEquipmentPose','id':mid+'Held'+ids[0], 'sourcePrototypes':sorted(ids),
                      'model':mid,'slot':'hand',**pose_for(kinds['cmu3DModel'][mid]),'itemLayers':json.loads(signature)})
    poses = pulse_rifle_poses(poses, kinds, resolver)
    path=bm.SOURCE/'garrison_held_equipment_poses.yml'
    path.write_text('# Generated by Tools/three_d/author_held_equipment.py. Geometry/source licenses remain with each model.\n'+yaml.safe_dump(poses,sort_keys=False,width=110),encoding='utf-8',newline='\n')
    report={'poses':len(poses),'equipment':len({uid for p in poses for uid in p['sourcePrototypes']}),'skipped':skipped}
    (bm.ROOT/'Tools/three_d/generated/held-equipment-review.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({k:v for k,v in report.items() if k!='skipped'}),'skipped',len(skipped))


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cache',type=Path)
    args=parser.parse_args()
    kinds=json.loads(args.cache.read_text(encoding='utf-8')) if args.cache else inv.load_prototypes(bm.ROOT)[0]
    author(kinds,json.loads((bm.ROOT/'Tools/three_d/generated/equipment-inventory.json').read_text())['equipment'])
