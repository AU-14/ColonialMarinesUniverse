"""Read final foam assets and their immediate exported contexts; never export or build."""
from collections import Counter
from io import BytesIO
from pathlib import Path
import argparse
import hashlib
import json
import math
import struct

from PIL import Image
import build_models as bm
import foam_wall_states as fs
import surfaces
from author_wide_machinery import world_parts, box_bounds

ROOT = bm.ROOT
GEN = ROOT/'Tools/three_d/generated'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def glb(path):
    raw=path.read_bytes()
    assert struct.unpack_from('<4sII',raw)==(b'glTF',2,len(raw))
    document=binary=None
    offset=12
    while offset<len(raw):
        size,kind=struct.unpack_from('<I4s',raw,offset)
        payload=raw[offset+8:offset+8+size]
        if kind==b'JSON':document=json.loads(payload)
        if kind==b'BIN\0':binary=payload
        offset+=size+8
    assert document is not None and binary is not None and offset==len(raw)
    return document,binary


def near(a,b):
    return len(a)==len(b) and all(abs(x-y)<2e-6 for x,y in zip(a,b))


def check_nodes(doc,binary,nodes,parts):
    registry=surfaces.load_surfaces()
    assert len(nodes)==len(parts)
    checked_images=set()
    for index,p in zip(nodes,parts):
        n=doc['nodes'][index]
        lo,hi=p['min'],p['max']
        mid=[(a+b)/2 for a,b in zip(lo,hi)];size=[b-a for a,b in zip(lo,hi)]
        assert n['name']==p['label'] and near(n['translation'],[mid[0],mid[2],-mid[1]])
        assert near(n['scale'],[size[0],size[2],size[1]])
        mesh=doc['meshes'][n['mesh']]['primitives'][0]
        material=doc['materials'][mesh['material']]['pbrMetallicRoughness']
        assert near(material['baseColorFactor'],[1,1,1,.8])
        source=doc['textures'][material['baseColorTexture']['index']]['source']
        if (source,p['surface']) not in checked_images:
            view=doc['bufferViews'][doc['images'][source]['bufferView']]
            start=view.get('byteOffset',0)
            actual=Image.open(BytesIO(binary[start:start+view['byteLength']])).convert('RGBA')
            original=registry[p['surface']]['image']
            assert actual.size==original.size and actual.tobytes()==original.tobytes()
            checked_images.add((source,p['surface']))
    return len(checked_images)


def world_bounds(entity,model,variants):
    parts=variants.get(entity.get('geometryKey'),model['parts'])
    offset=list(entity.get('renderOffset',[0,0,0]))
    offset[2]+=entity['position'][2]
    return [box_bounds(p) for p in world_parts(parts,entity['position'],entity.get('renderYaw',entity['yaw']),offset)]


def contacts(target,others,library,variants):
    own=world_bounds(target,library[target['modelId']],variants)
    contacts=[]
    for other in others:
        if other['id']==target['id'] or other.get('modelId') not in library:
            continue
        if max(abs(other['position'][i]-target['position'][i]) for i in (0,1))>1.6:
            continue
        boxes=world_bounds(other,library[other['modelId']],variants)
        penetration=touch=0
        max_depth=0
        for lo,hi in own:
            for low,high in boxes:
                overlaps=[min(hi[i],high[i])-max(lo[i],low[i]) for i in range(3)]
                if min(overlaps)<-2e-6:
                    continue
                if min(overlaps)>2e-6:
                    penetration+=1;max_depth=max(max_depth,min(overlaps))
                else:
                    touch+=1
        if penetration or touch:
            duplicate=other['prototype']==fs.PROTOTYPE and other['position']==target['position']
            contacts.append(dict(uid=other['id'],prototype=other['prototype'],modelId=other['modelId'],
                classification='preserved source duplicate' if duplicate else 'conservative part-bounds penetration' if penetration else 'boundary contact',
                boundPenetrationPairs=penetration,boundTouchPairs=touch,maxBoundsDepth=round(max_depth,7)))
    return contacts


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--prefix',default='utility-batch')
    args=parser.parse_args()
    model=bm.load_models(ROOT/'Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_foam_wall.yml')[0]
    models=json.loads((GEN/'models.json').read_text())['models']
    library={m['id']:m for m in models}
    for field in ('parts','foamAppearance','sourcePrototypes','referencePrototype','referenceRsi','referenceState',
                  'referenceTint','bakedSpriteTint','groundOffset','useEntityRotation'):
        assert json.dumps(library[model['id']][field],sort_keys=True)==json.dumps(model[field],sort_keys=True), field
    assert len(library[model['id']]['foamStates'])==16
    proof=json.loads((GEN/'foam-wall-proof.json').read_text())
    expected={(e['variant'],e['level'],e['uid']):e for e in proof['contexts']}
    scenes=json.loads((GEN/f'{args.prefix}-scenes.json').read_text())
    batch=json.loads((GEN/f'{args.prefix}-export-audit.json').read_text())
    actual={};seen=set();scene_hashes={};contact_rows=[]
    for spec in scenes:
        path=GEN/spec['file']
        doc=json.loads(path.read_text())
        targets=[e for e in doc['instances'] if e['prototype']==fs.PROTOTYPE]
        if not targets:
            continue
        scene_hashes[path.relative_to(ROOT).as_posix()]=sha(path)
        for e in targets:
            identity=(spec['variant'],spec['level'],e['id']);before=expected[identity]
            assert identity not in actual
            assert e['position']==before['position'] and e['yaw']==before['yaw']
            assert near([e['renderYaw']],[before['renderYaw']])
            assert e['modelId']==model['id'] and e['matchKind']=='exact'
            assert e['foamPose']['edgeMask']==before['edgeMask']==0
            assert e['foamPose']['matchingNeighbors']==before['matchingNeighbors']
            assert e['foamPose']['spriteTint']==fs.TINT
            assert e.get('renderOffset',[0,0,0])==[0,0,0]
            parts=doc['geometryVariants'][e['geometryKey']]
            assert json.dumps(parts,sort_keys=True)==json.dumps(fs.compose_parts(model,0),sort_keys=True)
            actual[identity]=e
            contact_rows.append(dict(variant=spec['variant'],level=spec['level'],uid=e['id'],
                contacts=contacts(e,doc['instances'],library,doc['geometryVariants'])))
    assert set(actual)==set(expected) and len(actual)==21
    context_checks=[]
    for entry in batch['contextExports']:
        spec=next(s for s in scenes if s['file']==entry['scene'])
        if not any((spec['variant'],spec['level'],uid) in expected for uid in entry['targetIds']):
            continue
        path=ROOT/entry['file'];doc,binary=glb(path)
        checks=[]
        for n in doc['nodes']:
            extra=n.get('extras',{})
            if extra.get('sourcePrototype')!=fs.PROTOTYPE:
                continue
            identity=spec['variant'],spec['level'],extra['savedUid']
            e=actual[identity]
            assert extra['modelId']==model['id'] and extra['matchKind']=='exact'
            assert extra['sourcePosition']==e['position'] and extra['sourceYaw']==e['yaw']
            assert extra['renderYaw']==e['renderYaw'] and extra['foamPreview']['edgeMask']==0
            assert near(n['translation'],[e['position'][0],e['position'][2],-e['position'][1]])
            assert near(n['rotation'],[0,math.sin(e['renderYaw']/2),0,math.cos(e['renderYaw']/2)])
            image_count=check_nodes(doc,binary,n['children'],fs.compose_parts(model,0))
            checks.append(dict(uid=e['id'],children=len(n['children']),exactEmbeddedTextures=image_count))
            seen.add(identity)
        context_checks.append(dict(file=entry['file'],sha256=sha(path),foam=checks))
    assert seen==set(expected), ('Missing foam context GLB identity',set(expected)-seen)
    path=ROOT/'Content.CMU/Resources/Models/CMU14/Garrison'/f'{model["id"]}.glb'
    doc,binary=glb(path)
    assert len(doc['extras']['spriteStaticScenes'])==16 and not doc.get('animations')
    for mask in range(16):
        name=f'edges-{mask}'
        index=doc['extras']['spriteStaticScenes'][name]
        nodes=doc['scenes'][index]['nodes']
        # Sprite-state exports wrap the solid nodes under one static state root.
        if len(nodes)==1 and 'children' in doc['nodes'][nodes[0]]:
            nodes=doc['nodes'][nodes[0]]['children']
        check_nodes(doc,binary,nodes,fs.compose_parts(model,mask))
    report=dict(passed=True,modelId=model['id'],sceneInstances=len(actual),contextGlbIdentities=len(seen),
        portableStaticCompositions=16,sourceYawCounts=dict(Counter(str(e['yaw']) for e in actual.values())),
        masks=dict(Counter(str(e['foamPose']['edgeMask']) for e in actual.values())),preservedDuplicateUids=[16177,16181],
        sceneSha256=scene_hashes,libraryGlbSha256=sha(path),contexts=context_checks,immediateNeighbors=contact_rows,
        contactMethod='Conservative transformed part bounds, including wedge bounds. Contacts do not prove occupied-solid intersection.',
        limitations=['Source duplicates remain overlapped. Hidden height and side structure are inferred.',
                     'Immediate mapped neighbors only; no universal clearance or gameplay runtime claim.'])
    out=GEN/'foam-wall-final-export-audit.json'
    out.write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps(dict(passed=True,instances=21,staticStates=16,contextGlbs=len(context_checks),
        contacts=Counter(c['classification'] for row in contact_rows for c in row['contacts']))))


if __name__=='__main__':
    main()
