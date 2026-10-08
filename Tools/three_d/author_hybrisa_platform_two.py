"""Consolidate Platform Two's opaque color voxels without changing its solid profile.

The seven boxes are the union of the earlier 105 coplanar colored rectangles.
Original front/cap pixels supply their color; actual end feet, recessed panel,
source-facing convention and 0.15-tile cap depth are retained.
"""
import json
from pathlib import Path
import re

from PIL import Image, ImageOps
import yaml

import build_models

ROOT = Path(__file__).resolve().parents[2]
PROTOTYPES = ROOT/'Content.CMU/Resources/ThreeD/Prototypes/World'
MODEL_FILE = PROTOTYPES/'garrison_architecture.yml'
ART_FILE = PROTOTYPES/'garrison_hybrisa_platform_two_art.yml'
RSI = '_RMC14/Structures/platforms.rsi'
MODEL = 'CMU3DPlatformHybrisaTwo'
PREFIX = 'CMU3DPlatformHybrisaTwo'
# Crop coordinates are in the original 64x64 four-direction RSI state sheet.
# North supplies the mirrored vertical profile. South supplies the horizontal cap.
SPECS = [
    ('Beam', (32,6,64,10), True, (-.5,-.49,.2163636), (.5,-.36,.34), 'XZ'),
    ('Recess', (36,10,60,16), True, (-.375,-.465,.0309091), (.375,-.385,.2163636), 'XZ'),
    ('LeftEnd', (60,10,64,16), True, (-.5,-.49,.0309091), (-.375,-.36,.2163636), 'XZ'),
    ('RightEnd', (32,10,36,16), True, (.375,-.49,.0309091), (.5,-.36,.2163636), 'XZ'),
    ('LeftFoot', (60,16,63,17), True, (-.46875,-.49,0), (-.375,-.36,.0309091), 'XZ'),
    ('RightFoot', (33,16,36,17), True, (.375,-.49,0), (.46875,-.36,.0309091), 'XZ'),
    ('Cap', (0,26,32,32), False, (-.5,-.5,.34), (.5,-.35,.39), 'XY'),
]


def main():
    text=MODEL_FILE.read_text(encoding='utf-8')
    blocks=re.split(r'(?=^- type: cmu3DModel\s*$)',text,flags=re.M)
    matches=[i for i,b in enumerate(blocks) if re.search(r'^  id: '+MODEL+r'$',b,re.M)]
    assert len(matches)==1
    index=matches[0]
    model=yaml.safe_load(blocks[index])[0]
    backup=ROOT/'.codex/filtration-budget-audit/platform-before.json'
    if not backup.exists():
        backup.parent.mkdir(parents=True,exist_ok=True)
        backup.write_text(json.dumps(model,indent=2)+'\n')
    current={s['id']:s['atlasIndex'] for p in PROTOTYPES.glob('*.yml')
             for s in yaml.safe_load(p.read_text(encoding='utf-8')) if s['type']=='cmu3DSurface'}
    source=Image.open(ROOT/'Resources/Textures'/RSI/'hybrisaplatform2.png').convert('RGBA')
    parts=[]
    surfaces=[]
    for number,(name,crop,mirror,low,high,axis) in enumerate(SPECS):
        uid=PREFIX+name
        atlas=1040+number
        assert uid not in current or current[uid]==atlas
        assert not any(other!=uid and slot==atlas for other,slot in current.items()),atlas
        image=source.crop(crop)
        if mirror:image=ImageOps.mirror(image)
        assert all(p[3]==255 for p in image.get_flattened_data()),name
        image.save(ROOT/f'Content.CMU/Resources/Textures/CMU14/ThreeD/Surfaces/{uid}.png')
        surfaces.append(dict(type='cmu3DSurface',id=uid,atlasIndex=atlas,
                             texture=f'/Textures/CMU14/ThreeD/Surfaces/{uid}.png'))
        parts.append(dict(label='source '+name,min=', '.join(f'{v:.7f}' for v in low),
                          max=', '.join(f'{v:.7f}' for v in high),color='#FFFFFF',surface=uid,surfaceAxis=axis))
    ART_FILE.write_text('# Original CC-BY-SA-3.0 pixels; see SOURCES_HYBRISA_PLATFORMS.md.\n'+
                        yaml.safe_dump(surfaces,sort_keys=False),encoding='utf-8')
    model['parts']=parts
    model['description']='Source-textured ridged cap, paired end feet and dark recessed lower panel. Seven opaque '
    model['description']+='volumes preserve the previous 105-piece solid profile and original front/cap pixels, '
    model['description']+='reducing cell crowding beside the saved filtration machinery. Height .39, structural depth '
    model['description']+='.13, cap depth .15 and central panel recess .025 are retained. Saved facing and exact mapping '
    model['description']+='are unchanged. Height, hidden construction and physical depths remain inferred; static draft. '
    model['description']+='See SOURCES_HYBRISA_PLATFORMS.md.'
    blocks[index]=yaml.safe_dump([model],sort_keys=False,width=112)+'\n'
    MODEL_FILE.write_text(''.join(blocks),encoding='utf-8')
    # Other architecture entries have alternate poses in separate files; validate
    # only this independent model here. The root build validates the full library.
    loaded=[build_models.validate_model(model)]
    inventory=json.loads((ROOT/'Tools/three_d/generated/inventory.json').read_text())
    build_models.write_reviews(loaded,ROOT/'Tools/three_d/generated/review/filtration-platform',
                               {e['id']:e for e in inventory['prototypes']})
    print(f'{MODEL}: 7 parts, atlas 1040..1046; unchanged profile and placement metadata.')


if __name__=='__main__':
    main()
