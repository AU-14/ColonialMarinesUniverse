"""Read-only verification of written glass GLBs, source references and contexts."""
from io import BytesIO
import hashlib
import json
import math
from pathlib import Path
import struct

from PIL import Image

import author_solution_glasses as author
import build_models as bm
import solution_glass_states as sg


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def glb(path):
    raw = path.read_bytes(); length = struct.unpack_from('<I',raw,12)[0]
    document = json.loads(raw[20:20+length])
    binary_start = 20+length+8
    return document, raw[binary_start:]


def image_checks(document,binary):
    checks = []
    for entry in document.get('images',[]):
        uid = entry.get('name','')
        if not uid.startswith('CMU3DSolutionGlassSurface'):
            continue
        view = document['bufferViews'][entry['bufferView']]
        data = binary[view.get('byteOffset',0):view.get('byteOffset',0)+view['byteLength']]
        image = Image.open(BytesIO(data)); original = Image.open(author.TEXTURES/(uid+'.png')).convert('RGBA')
        assert not image.info, (uid,'embedded unsupported PNG metadata')
        assert image.size == original.size and image.convert('RGBA').tobytes() == original.tobytes(), uid
        checks.append(dict(surface=uid,dimensions=list(image.size),rgbaSha256=hashlib.sha256(original.tobytes()).hexdigest(),
            embeddedPixelsExact=True,pngMetadataEmpty=True))
    return checks


def close(a,b):
    assert len(a) == len(b)
    assert all(abs(x-y)<1e-8 for x,y in zip(a,b)), (a,b)


def accessor(document,binary,index):
    entry = document['accessors'][index]; view = document['bufferViews'][entry['bufferView']]
    width = {5121:1,5123:2,5125:4,5126:4}[entry['componentType']]
    elements = {'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[entry['type']]
    stride = width*elements; offset = view.get('byteOffset',0)+entry.get('byteOffset',0)
    assert view.get('byteStride',stride) == stride
    return entry['componentType'],entry['type'],entry['count'],binary[offset:offset+stride*entry['count']]


def check_parts(document,binary,children,model,parts):
    expected,data = bm.glb_document({**model,'parts':parts})
    assert len(children) == len(expected['nodes'])
    for actual_index,wanted in zip(children,expected['nodes']):
        actual = document['nodes'][actual_index]
        assert actual['name'] == wanted['name']
        for field,default in [('translation',[0,0,0]),('scale',[1,1,1]),('rotation',[0,0,0,1])]:
            close(actual.get(field,default),wanted.get(field,default))
        ap = document['meshes'][actual['mesh']]['primitives'][0]
        wp = expected['meshes'][wanted['mesh']]['primitives'][0]
        assert set(ap['attributes']) == set(wp['attributes'])
        for key in ap['attributes']:
            assert accessor(document,binary,ap['attributes'][key]) == accessor(expected,data,wp['attributes'][key]),key
        assert accessor(document,binary,ap['indices']) == accessor(expected,data,wp['indices'])
        am = document['materials'][ap['material']]; wm = expected['materials'][wp['material']]
        close(am['pbrMetallicRoughness']['baseColorFactor'],wm['pbrMetallicRoughness']['baseColorFactor'])
        assert am.get('alphaMode','OPAQUE') == wm.get('alphaMode','OPAQUE')
        for doc,mat,which in [(document,am,'actual'),(expected,wm,'expected')]:
            tex = mat['pbrMetallicRoughness'].get('baseColorTexture')
            uid = doc['images'][doc['textures'][tex['index']]['source']]['name'] if tex else None
            if which == 'actual':
                actual_surface = uid
            else:
                assert actual_surface == uid,(actual_surface,uid)


def main():
    gen = author.GEN; library = {m['id']:m for m in bm.load_models(author.MODEL)}
    viewer = {m['id']:m for m in json.loads((gen/'models.json').read_text())['models'] if m['id'] in library}
    library_checks = []
    for uid,model in library.items():
        path = author.NOTE.parent/(uid+'.glb'); document,binary = glb(path)
        assert path.read_bytes() == bm.glb_bytes(model), (uid,'written GLB differs from source model')
        states = sg.portable_states(model); view = viewer[uid]
        assert set(states) == set(view['solutionStates']) == set(view['solutionStateReferences'])
        assert set(document['extras']['spriteStaticScenes']) == set(states)
        assert not document.get('animations')
        for state,pose in states.items():
            assert view['solutionStates'][state]['layers'] == pose['layers']
            urls = view['solutionStateReferences'][state]; assert len(urls) == 1
            reference = Image.open((author.ROOT/'Tools/three_d/viewer'/urls[0]).resolve()).convert('RGBA')
            expected = sg.composite(model,pose['layers'],bm.resource_file)
            assert reference.size == expected.size and reference.tobytes() == expected.tobytes(), (uid,state)
        library_checks.append(dict(modelId=uid,staticStudies=len(states),sourceReferencesExact=True,
            writtenGlbExact=True,animationClocks=0,glbSha256=sha(path),embeddedImages=image_checks(document,binary)))
    specs = json.loads((gen/'utility-batch-scenes.json').read_text())
    scenes = {s['file']:json.loads((gen/s['file']).read_text()) for s in specs}
    contexts = []; checked = set()
    for context in json.loads((gen/'utility-batch-export-audit.json').read_text())['contextExports']:
        source = scenes[context['scene']]; entities = {e['id']:e for e in source['instances'] if e.get('modelId') in library}
        if not set(context['targetIds']).intersection(entities):
            continue
        path = author.ROOT/context['file']; document,binary = glb(path); roots = []
        for node in document['nodes']:
            extra = node.get('extras',{}); saved = extra.get('savedUid')
            if saved not in entities:
                continue
            entity = entities[saved]; model = library[entity['modelId']]; pose = entity['solutionPose']
            assert extra['solutionPreview'] == {**pose,'runtimeStateKnown':False}
            assert extra['sourcePosition'] == entity['position'] and extra['sourceYaw'] == entity['yaw']
            assert extra['renderYaw'] == entity['renderYaw']
            off = entity.get('renderOffset',[0,0,0]); pos = [a+b for a,b in zip(entity['position'],off)]
            close(node['translation'],[pos[0],pos[2],-pos[1]])
            yaw = entity['renderYaw']/2; close(node['rotation'],[0,math.sin(yaw),0,math.cos(yaw)])
            parts = sg.compose_parts(model,pose['layers'],pose['spriteTint'])
            check_parts(document,binary,node['children'],model,parts)
            roots.append(dict(savedUid=saved,parts=len(parts),sourcePoseExact=True,sourceTransformAndSupportExact=True,
                vertexBuffersExact=True,partTransformsAndMaterialsExact=True))
            checked.add((context['scene'],saved))
        contexts.append(dict(file=context['file'],glbSha256=sha(path),roots=roots,embeddedImages=image_checks(document,binary)))
    assert len(checked) == 41,len(checked)
    report = dict(passed=True,modelCount=16,staticStudies=241,models=library_checks,
        uniqueContextPlacements=len(checked),contextGlbs=len(contexts),contexts=contexts)
    (gen/'solution-glasses-library-export-audit.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    path = gen/'solution-glasses-final-export-audit.json'; final = json.loads(path.read_text()); assert final['passed']
    final['embeddedGlbVerification'] = dict(passed=True,uniqueContextPlacements=41,contextGlbs=len(contexts),
        detailedReport='solution-glasses-library-export-audit.json',allEmbeddedPixelsExact=True,sourceLayersExact=True)
    path.write_text(json.dumps(final,indent=2)+'\n',encoding='utf-8')
    proof_path = gen/'solution-glasses-proof.json'; proof = json.loads(proof_path.read_text())
    proof['finalSceneParity'] = final; proof['libraryExportAudit'] = dict(passed=True,modelCount=16,staticStudies=241,
        uniqueContextPlacements=41,contextGlbs=len(contexts),report='solution-glasses-library-export-audit.json')
    proof_path.write_text(json.dumps(proof,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(dict(passed=True,models=16,states=241,uniquePlacements=41,contextGlbs=len(contexts))))


if __name__ == '__main__':
    main()
