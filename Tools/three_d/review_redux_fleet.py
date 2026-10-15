"""CMU14: compare every drivable variant with front/rear/underside model views."""
import json

from PIL import Image, ImageDraw, ImageFont
import yaml

import author_redux_coverage as author
import build_models as bm


def main():
    out=bm.OUTPUT/'Reviews/ReduxCoverage'
    models=[bm.validate_model(m) for m in yaml.safe_load(author.MODEL.read_text())
            if m.get('vehicleLayers') and m.get('sourcePrototypes')]
    sources=author.Sources()
    font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',15)
    rows=[]
    for m in models:
        # Default parts contain the physical base and wheels, without inventing
        # installed hardpoints. Aimed turret entities have their own model.
        rows.append((m['label']+' | '+', '.join(m['sourcePrototypes']),m['parts'],sources.frame(m['referenceRsi'],m['referenceState'])))
        for state in ('jetfighter','vtolmode','cargo_open'):
            alternate=next((l for l in m['vehicleLayers'] if l['state']==state),None)
            if alternate:
                rows.append((m['label']+' | '+state,alternate['parts'],sources.frame(alternate['rsi'],state)))
    report=[]
    for page in range((len(rows)+4)//5):
        selected=rows[page*5:(page+1)*5]
        canvas=Image.new('RGB',(1400,len(selected)*260),'#17212B');draw=ImageDraw.Draw(canvas)
        for i,(label,parts,source) in enumerate(selected):
            y=i*260
            draw.text((10,y+5),label,fill='white',font=font)
            draw.text((10,y+28),f'{len(parts)} parts / {bm.triangle_count(parts)} triangles — source, front, rear, underside',fill='#B6C5CF',font=font)
            source.thumbnail((175,195),Image.Resampling.NEAREST)
            factor=max(1,min(175//source.width,195//source.height))
            source=source.resize((source.width*factor,source.height*factor),Image.Resampling.NEAREST)
            canvas.paste(source,(10,y+56),source)
            for j,(yaw,pitch) in enumerate(((-1.05,.38),(1.25,.52),(2.7,-.3))):
                canvas.paste(bm.render_model({'parts':parts},(393,205),yaw,pitch),(200+j*397,y+50))
            report.append(dict(label=label,parts=len(parts),triangles=bm.triangle_count(parts)))
        canvas.save(out/f'fleet-{page+1:02}.png')
        print('Rendered fleet page',page+1,flush=True)
    (out/'fleet-review.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8',newline='\n')
    overview=Image.new('RGB',(1200,960),'#17212B');draw=ImageDraw.Draw(overview)
    selected=['VehicleAPC','VehicleHumvee','VehicleSPPVanMedical',
              'AU14VehicleCivCopMonoSupron','VehiclePizzaVan','AU14VehicleCivBlueCargoTruck',
              'AU14VehicleCivBrownBarrelsTruck','CMUCargoCarrier','CMUFighterGround']
    for i,uid in enumerate(selected):
        m=next(m for m in models if uid in m['sourcePrototypes'])
        x,y=i%3*400,i//3*320
        overview.paste(bm.render_model(m,(390,280),-1.05,.5),(x+5,y+30))
        draw.text((x+10,y+8),uid,fill='white',font=font)
    overview.save(out/'fleet-overview.png')


if __name__=='__main__':
    main()
