"""Source-specific turbine edge brackets and flat hazard paint, in their saved pivots."""
import json
from pathlib import Path

from PIL import Image
import yaml

import build_models

ROOT = Path(__file__).resolve().parents[2]
RSI = '_RMC14/Structures/Props/biomass_turbine.rsi'
SOURCE = ROOT/'Resources/Textures'/RSI
PROTOTYPES = ROOT/'Content.CMU/Resources/Prototypes/CMU14/ThreeD'
MODEL_FILE = PROTOTYPES/'garrison_biomass_supports.yml'
ART_FILE = PROTOTYPES/'garrison_biomass_support_art.yml'
TEXTURES = ROOT/'Content.CMU/Resources/Textures/CMU14/ThreeD/Surfaces'


def opaque_runs(image):
    """Partition the binary source alpha into nonoverlapping rectangles without filling gaps."""
    rectangles, active = [], {}
    for y in range(image.height + 1):
        spans, start = [], None
        for x in range(image.width + 1):
            opaque = y < image.height and x < image.width and image.getpixel((x,y))[3] == 255
            if opaque and start is None:
                start = x
            if not opaque and start is not None:
                spans.append((start,x))
                start = None
        for span in list(active):
            if span not in spans:
                rectangles.append((span[0],active.pop(span),span[1],y))
        for span in spans:
            active.setdefault(span,y)
    return rectangles


def main():
    current = {s['id']:s['atlasIndex'] for path in PROTOTYPES.glob('*.yml')
               for s in yaml.safe_load(path.read_text(encoding='utf-8')) if s['type'] == 'cmu3DSurface'}
    models, surfaces, evidence = [], [], []
    for suffix,state in [('Left','support_struts_l'),('Right','support_struts_r'),('Border','biomass_turbine_border')]:
        original = Image.open(SOURCE/(state+'.png')).convert('RGBA')
        assert original.size == (32,96)
        assert {p[3] for p in original.get_flattened_data()} == {0,255}
        border = suffix == 'Border'
        # Paint has a single alpha-cut plane. Brackets have actual L-shaped opaque
        # volumes, avoiding an invisible rectangular bridge between their ends.
        rectangles = [(0,0,32,96)] if border else opaque_runs(original)
        parts, records = [], []
        reconstructed = Image.new('RGBA', original.size)
        for index,crop in enumerate(rectangles):
            uid = f'CMU3DBiomass{suffix}Art{index}'
            slot = 1047 + len(surfaces)
            assert uid not in current or current[uid] == slot
            assert not any(other != uid and value == slot for other,value in current.items()), slot
            pixels = original.crop(crop)
            pixels.save(TEXTURES/(uid+'.png'))
            surfaces.append(dict(type='cmu3DSurface',id=uid,atlasIndex=slot,
                texture=f'/Textures/CMU14/ThreeD/Surfaces/{uid}.png'))
            reconstructed.paste(pixels,(crop[0],crop[1]))
            x0,y0,x1,y1 = crop
            low_z,high_z = (.008,.012) if border else (.45,.62)
            lo,hi = (x0/32-.5,1.5-y1/32,low_z),(x1/32-.5,1.5-y0/32,high_z)
            label = 'original floor hazard paint' if border else f'cast edge bracket {index+1}'
            parts.append(dict(label=label,min=', '.join(f'{v:.7f}' for v in lo),
                max=', '.join(f'{v:.7f}' for v in hi),color='#FFFFFF',surface=uid,surfaceAxis='XY'))
            records.append(dict(surface=uid,crop=list(crop),pixels=pixels.width*pixels.height,
                                opaquePixels=sum(p[3] == 255 for p in pixels.get_flattened_data())))
        assert reconstructed.tobytes() == original.tobytes(), state
        proto = 'RMCPropTurbineStruts'+suffix
        description = ('Original transparent warning-border pixels as thin floor paint; keeps its open center, '
                       'curved end outline and narrow side markings. This is not a solid platform or a raised railing.'
                       if border else
                       'Two small cast-metal edge brackets at the exact source pixel offsets. The far bracket is '
                       'six pixels wide and the near bracket five; their L silhouettes are real opaque volumes. '
                       'Left and right variants occupy opposite edges of a shared tile and leave the middle clear. '
                       'Attachment height .45-.62 tiles is inferred to meet the turbine side lugs; no floor pier is invented.')
        models.append(dict(type='cmu3DModel',id='CMU3DBiomass'+suffix,
            label='Turbine floor warning border' if border else f'Turbine {suffix.lower()} edge brackets',status='draft',
            sourcePrototypes=[proto],referencePrototype=proto,referenceRsi=RSI,referenceState=state,
            sourceDirections=1,useEntityRotation=True,
            description=description+' Static source pose; original CC-BY-SA-3.0 artwork. See SOURCES_BIOMASS_TURBINE.md.',
            parts=parts))
        evidence.append(dict(model=models[-1]['id'],state=state,parts=len(parts),rectangles=records,
            reconstructedSourceRgbaPixels=original.width*original.height,
            opaqueSourcePixels=sum(p[3] == 255 for p in original.get_flattened_data()),
            sourcePivot=[16,48],pixelsPerTile=32,sourceRgbaPreserved=True))
    ART_FILE.write_text('# Original CC-BY-SA-3.0 pixels; see SOURCES_BIOMASS_TURBINE.md.\n'+
        yaml.safe_dump(surfaces,sort_keys=False),encoding='utf-8')
    MODEL_FILE.write_text('# Independent edge brackets and hazard paint; source offsets are intentional.\n'+
        yaml.safe_dump(models,sort_keys=False,width=110),encoding='utf-8')
    loaded = build_models.load_models(MODEL_FILE)
    inventory = json.loads((ROOT/'Tools/three_d/generated/inventory.json').read_text())
    build_models.write_reviews(loaded,ROOT/'Tools/three_d/generated/review/biomass-supports',
        {entity['id']:entity for entity in inventory['prototypes']})
    (ROOT/'Tools/three_d/generated/biomass-support-source-audit.json').write_text(json.dumps(evidence,indent=2)+'\n')
    print('Authored',[(model['id'],len(model['parts'])) for model in loaded],len(surfaces),'exact source crops')


if __name__ == '__main__':
    main()
