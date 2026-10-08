"""Static source-guided turbine core; side supports are separate map entities.

The axial casing, front intake, paired service banks and metallic conduit follow
the original sprite. Elevation and concealed surfaces are explicitly inferred.
The unused three-frame `biomass_turbine-on` resource is not an implemented state.
"""
import hashlib
import json
import math
from pathlib import Path

from PIL import Image
import yaml

import build_models

ROOT=Path(__file__).resolve().parents[2]
PROTOTYPES=ROOT/'Content.CMU/Resources/ThreeD/Prototypes/World'
MODEL_FILE=PROTOTYPES/'garrison_biomass_turbine.yml'
ART_FILE=PROTOTYPES/'garrison_biomass_turbine_art.yml'
RSI='_RMC14/Structures/Props/biomass_turbine.rsi'
PREFIX='CMU3DBiomassTurbine'
CROPS=[('LeftVent',(7,27,13,50)),('RightVent',(19,27,25,50)),
       ('Intake',(8,81,24,91)),('Badge',(13,77,19,81)),('Bearing',(10,62,22,72))]


def part(label,low,high,color,shape='Box',**extra):
    result=dict(label=label,min=list(low),max=list(high),color=color,**extra)
    if shape!='Box':result['shape']=shape
    return result


def cylinder_y(label,low,high,color):return part(label,low,high,color,'CylinderY')


def annulus_y(label,near,far,color,segments=12,inner=.340,outer=.435):
    """Overlapping tangent segments leave an actual open bore around local Y."""
    radius=(inner+outer)/2
    tangent_half=outer*math.tan(math.pi/segments)
    radial_half=(outer-inner)/2
    result=[]
    for index in range(segments):
        angle=index*2*math.pi/segments
        x,z=radius*math.cos(angle),.480+radius*math.sin(angle)
        pitch=(math.degrees(angle)+180)%180-90
        result.append(part(f'{label} segment {index+1}',
            (x-tangent_half,near,z-radial_half),(x+tangent_half,far,z+radial_half),
            color,pitch=round(pitch,7)))
    return result


def geometry():
    parts=[
        cylinder_y('long axial dark turbine casing',(-.435,-1.16,.045),(.435,1.20,.915),'#151515'),
        cylinder_y('rear metallic bearing housing',(-.448,.74,.035),(.448,1.20,.925),'#3D3D3D'),
        part('rounded rear end head',(-.438,1.12,.04),(.438,1.49,.92),'#5B5B5B','Ellipsoid'),
        cylinder_y('front metallic bearing housing',(-.452,-1.25,.028),(.452,-.65,.932),'#3D3D3D'),
        *annulus_y('south intake hollow casing',-1.487,-1.20,'#5B5B5B'),
        *annulus_y('south intake annular steel lip',-1.495,-1.48,'#898989'),
        cylinder_y('south intake dark recessed backing',(-.340,-1.301,.140),(.340,-1.285,.820),'#131313'),
        part('original intake rotor pixels',(-.280,-1.305,.305),(.280,-1.302,.655),'#FFFFFF',
             surface=PREFIX+'Intake',surfaceAxis='XZ'),
        # Brown bearing shoulders and red couplings are distinct bands around the
        # cylindrical housing, not a stack of painted entire-machine rectangles.
        cylinder_y('rear burgundy casing collar',(-.453,.65,.027),(.453,.75,.933),'#661E25'),
        cylinder_y('front burgundy casing collar',(-.457,-.62,.023),(.457,-.49,.937),'#661E25'),
        cylinder_y('rear brown bearing shoulder',(-.426,.76,.054),(.426,.88,.906),'#735A4D'),
        cylinder_y('front brown bearing shoulder',(-.430,-.75,.05),(.430,-.63,.910),'#735A4D'),
        part('left recessed vent-bank plinth',(-.285,-.125,.795),(-.075,.645,.93),'#4F2B24'),
        part('right recessed vent-bank plinth',(.075,-.125,.795),(.285,.645,.93),'#4F2B24'),
        part('original left service grille',(-.271,-.105,.932),(-.083,.615,.936),'#FFFFFF',
             surface=PREFIX+'LeftVent',surfaceAxis='XY'),
        part('original right service grille',(.083,-.105,.932),(.271,.615,.936),'#FFFFFF',
             surface=PREFIX+'RightVent',surfaceAxis='XY'),
        cylinder_y('central metallic conduit',(-.066,-.24,.875),(.066,.68,1.007),'#898989'),
        cylinder_y('central conduit blue-grey underpipe',(-.071,-.22,.834),(.071,.65,.976),'#243137'),
        part('central upper conduit clamp',(-.083,.345,.958),(.083,.408,1.018),'#661E25'),
        part('central lower conduit clamp',(-.083,.095,.958),(.083,.158,1.018),'#661E25'),
        part('front bearing top block',(-.213,-.90,.770),(.213,-.47,.943),'#3D3D3D'),
        part('original battered bearing pixels',(-.1875,-.83,.944),(.1875,-.5175,.948),'#FFFFFF',
             surface=PREFIX+'Bearing',surfaceAxis='XY'),
        part('front warning badge mounting pad',(-.115,-1.267,.875),(.115,-1.092,.953),'#616161'),
        part('original red and yellow warning badge',(-.09375,-1.242,.954),(.09375,-1.117,.958),'#FFFFFF',
             surface=PREFIX+'Badge',surfaceAxis='XY'),
        part('rear top bearing block',(-.19,.865,.757),(.19,1.262,.955),'#7D7D7D'),
        cylinder_y('rear visible shaft nose',(-.068,1.17,.900),(.068,1.405,1.036),'#B6B6B6'),
    ]
    for x in (-.36,.36):
        parts.extend([
            cylinder_y('long exposed side service line',(x-.026,-.43,.665),(x+.026,.62,.717),'#7D7D7D'),
            cylinder_y('dark side-line socket',(x-.039,-.465,.651),(x+.039,-.392,.729),'#243137'),
        ])
    for y in (-1.0,1.0):
        for side in (-1,1):
            low,high=sorted((.378*side,.49*side))
            parts.append(part('side lug matching separate support entity',(low,y-.10,.45),
                              (high,y+.10,.62),'#5B5B5B'))
            low,high=sorted((.295*side,.365*side))
            parts.append(part('low bearing pedestal foot',(low,y-.12,0),(high,y+.12,.17),'#151515'))
    # Small tangent rust marks are sampled from the original opaque sprite palette.
    parts.extend([
        part('rear brown oxidized top patch',(-.17,1.016,.955),(-.09,1.167,.961),'#4F2B24'),
        part('rear bearing rust patch',(.075,.925,.955),(.18,1.065,.961),'#43220C'),
        part('front right bearing patina',(.444,-.97,.435),(.458,-.83,.64),'#3D1E09'),
        part('front left bearing patina',(-.458,-1.02,.435),(-.444,-.88,.59),'#43220C'),
    ])
    return parts


def main():
    source=Image.open(ROOT/'Resources/Textures'/RSI/'biomass_turbine.png').convert('RGBA')
    current={s['id']:s['atlasIndex'] for p in PROTOTYPES.glob('*.yml')
             for s in yaml.safe_load(p.read_text(encoding='utf-8')) if s['type']=='cmu3DSurface'}
    art=[]
    for i,(name,crop) in enumerate(CROPS):
        uid=PREFIX+name;index=1080+i
        assert uid not in current or current[uid]==index
        assert not any(other!=uid and slot==index for other,slot in current.items()),index
        image=source.crop(crop)
        assert all(p[3]==255 for p in image.get_flattened_data())
        image.save(ROOT/f'Content.CMU/Resources/Textures/CMU14/ThreeD/Surfaces/{uid}.png')
        art.append(dict(type='cmu3DSurface',id=uid,atlasIndex=index,
                        texture=f'/Textures/CMU14/ThreeD/Surfaces/{uid}.png'))
    ART_FILE.write_text('# Original CC-BY-SA-3.0 biomass turbine pixels; see RSI metadata and model description.\n'+
                        yaml.safe_dump(art,sort_keys=False),encoding='utf-8')
    parts=geometry()
    colors={tuple(p[:3]) for p in source.get_flattened_data() if p[3]==255}
    for p in parts:
        assert p.get('surface') or tuple(bytes.fromhex(p['color'][1:])) in colors,p['color']
        low,high=build_models.part_bounds(p)
        assert -.5<=low[0]<high[0]<=.5 and -1.5<=low[1]<high[1]<=1.5,p
        for key in ('min','max'):p[key]=', '.join(f'{v:.7f}' for v in p[key])
    model=dict(type='cmu3DModel',id=PREFIX,label='Biomass power turbine core',status='draft',
               sourcePrototypes=['RMCPropTurbine'],referencePrototype='RMCPropTurbine',
               referenceRsi=RSI,referenceState='biomass_turbine',sourceDirections=1,useEntityRotation=True,
               description='Long axial turbine with curved bearing housings, a hollow segmented south intake '
               'and source-pixel rotor recessed .190 tiles behind the front lip, paired '
               'source-pixel top service grilles, central metallic conduit, red/brown collars and original badge. '
               'Side lugs at local Y +/-1.0 and Z .45-.62 meet separately saved support entities without joining '
               'the empty gap between supports. Core remains inside its original 1x3 collider. 1.036-tile height, '
               'elevation, concealed construction and pipe depth are inferred. Static saved biomass_turbine pose '
               'only: the unused biomass_turbine-on resource has three .1-second frames, but no active machinery '
               'state or animation is implemented here. Original source design and pixel crops are CC-BY-SA-3.0, '
               'cmss13 commit 0525b5ada7da1afcd9b260e76d5fea01500d9c8d '
               'icons/obj/structures/props/industrial/biomass_turbine.dmi.',parts=parts)
    MODEL_FILE.write_text('# Source-guided static turbine core. Separate struts and border keep their own entities.\n'+
                          yaml.safe_dump([model],sort_keys=False,width=110),encoding='utf-8')
    loaded=build_models.validate_model(model)
    inventory=json.loads((ROOT/'Tools/three_d/generated/inventory.json').read_text())
    build_models.write_reviews([loaded],ROOT/'Tools/three_d/generated/review/biomass-turbine-core',
                               {e['id']:e for e in inventory['prototypes']})
    write_proof(source,loaded)
    print(f'{PREFIX}: {len(parts)} parts; five original pixel surfaces1080..1084; static draft.')


def write_proof(source,model):
    """Check the true rotated opening, not just unrotated part coordinates."""
    bounds=[build_models.part_bounds(p) for p in model['parts']]
    cavity_parts=[p for p in model['parts'] if p['min'][1]<=-1.4<=p['max'][1]]
    assert all(p.get('shape','Box')=='Box' and not p.get('yaw') for p in cavity_parts)
    clear_samples=0
    for ix in range(-33,34):
        for iz in range(-33,34):
            x,z=ix/100,.480+iz/100
            if ix*ix+iz*iz>33*33:continue
            for p in cavity_parts:
                center=[(a+b)/2 for a,b in zip(p['min'],p['max'])]
                half=[(b-a)/2 for a,b in zip(p['min'],p['max'])]
                angle=math.radians(p.get('pitch',0))
                dx,dz=x-center[0],z-center[2]
                local_x=math.cos(angle)*dx+math.sin(angle)*dz
                local_z=-math.sin(angle)*dx+math.cos(angle)*dz
                assert abs(local_x)>half[0] or abs(local_z)>half[2],(x,z,p['label'])
            clear_samples+=1
    rotor=next(p for p in model['parts'] if p['label']=='original intake rotor pixels')
    assert rotor['max'][1]<-1.301
    exact_pixels=0
    for name,crop in CROPS:
        actual=Image.open(ROOT/f'Content.CMU/Resources/Textures/CMU14/ThreeD/Surfaces/{PREFIX+name}.png').convert('RGBA')
        expected=source.crop(crop)
        assert actual.tobytes()==expected.tobytes()
        exact_pixels+=expected.width*expected.height
    proof=dict(model=PREFIX,parts=len(model['parts']),sourceCrops=len(CROPS),
        exactRgbaPixelSamples=exact_pixels,
        bounds=[[min(b[0][axis] for b in bounds) for axis in range(3)],
                [max(b[1][axis] for b in bounds) for axis in range(3)]],
        sourceSha256=hashlib.sha256((ROOT/'Resources/Textures'/RSI/'biomass_turbine.png').read_bytes()).hexdigest(),
        sourceStaticState='biomass_turbine',
        unusedAnimatedResource=dict(state='biomass_turbine-on',frames=3,durations=[.1,.1,.1],implemented=False),
        intakeCavity=dict(frontLipY=-1.495,rotorFrontY=rotor['min'][1],recessDepth=.190,
                          annularSegments=24,openBoreRadius=.340,
                          unobstructedCrossSectionY=-1.4,checkedClearRadius=.330,
                          clearSamples=clear_samples,rotatedBoxContainmentChecked=True),
        draftLimitations=['Height and concealed surfaces inferred','No runtime operating transition or clip',
                          'Placement review limited to saved yaw-zero contexts',
                          'Assembled scene exports and native budget verification pending parent'],
        sourceNormals=dict(frontIntake='local -Y',ventBanks='local +Z'))
    destination=ROOT/'.codex/biomass-turbine-core-proof.json'
    destination.parent.mkdir(exist_ok=True)
    destination.write_text(json.dumps(proof,indent=2)+'\n')


if __name__=='__main__':main()
