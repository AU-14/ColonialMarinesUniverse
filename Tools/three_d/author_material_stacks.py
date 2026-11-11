"""Dedicated source-guided metal/plastic stack assets, state proofs and context review."""
from pathlib import Path
from copy import deepcopy
from collections import Counter
from io import BytesIO
import argparse,hashlib,json,math,sys
import numpy as np
from PIL import Image,ImageDraw,ImageFont
import yaml
ROOT=Path(__file__).resolve().parents[2];sys.path.insert(0,str(ROOT/'Tools/three_d'))
import build_models as bm
import sprite_states,scene,surfaces
from placement import resolve_placements
from author_wide_machinery import world_parts,contacts
GEN=ROOT/'Tools/three_d/generated';REVIEW=GEN/'review/material-stacks';BASE=ROOT/'.codex/model-batch-baseline978'
MODEL=ROOT/'Content.CMU/Resources/ThreeD/Prototypes/World/garrison_material_stacks.yml'
ART=MODEL.with_name('garrison_material_stacks_art.yml')
TEXTURES=ROOT/'Content.CMU/Resources/Textures/CMU14/ThreeD/Surfaces'
NOTE=ROOT/'Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_MATERIAL_STACKS.md'
IDS=['CMSheetMetal','CMSheetMetal10','CMSheetMetal20','CMSheetMetal50','RMCSheetPlastic']
SHEETS={
 'metal':[(10,8,22,25)],
 'metal_2':[(10,8,22,25),(10,6,22,23)],
 'metal_3':[(10,9,22,26),(9,7,21,24),(10,5,22,22)],
 'metal_4':[(10,10,22,27),(9,8,21,25),(10,6,22,23),(9,4,21,21)],
 'plastic':[(8,5,24,26)],
 'plastic_2':[(8,5,24,26),(7,3,23,24)],
 'plastic_3':[(8,7,24,28),(7,5,23,26),(9,3,25,24)],
 'plastic_4':[(8,8,24,29),(7,6,23,27),(9,4,25,25),(7,2,23,23)],
}
CHECK=False;WRITTEN=[]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def rsi(material):return f'_RMC14/Objects/Materials/Sheets/{material}.rsi'
def source(state):return Image.open(bm.resource_file(rsi(state.split('_')[0]))/(state+'.png')).convert('RGBA')
def write(p,data):
 if CHECK:assert p.read_bytes()==data,f'Generated asset differs: {p}'
 else:p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(data)
 WRITTEN.append(p)
def json_write(p,value):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(value,indent=2)+'\n')
def png(image):stream=BytesIO();image.save(stream,format='PNG');return stream.getvalue()

class Pool:
 def __init__(self):
  self.rows=[];self.crops=[];self.cache={}
  for path in MODEL.parent.glob('*.yml'):
   if path==ART:continue
   for row in yaml.load(path.read_text(encoding='utf-8-sig'),Loader=yaml.CSafeLoader) or []:
    assert row.get('type')!='cmu3DSurface' or not 2600<=row['atlasIndex']<=2699,f'Atlas conflict: {path}'
 def crop(self,state,rect):
  im=source(state).crop(rect);assert im.getchannel('A').getextrema()==(255,255)
  signature=hashlib.sha256(str(im.size).encode()+im.tobytes()).hexdigest()
  if signature not in self.cache:
   index=2600+len(self.rows);assert index<2700
   uid=f'CMU3DMaterialStackSurface{index}';write(TEXTURES/(uid+'.png'),png(im));self.cache[signature]=uid
   self.rows.append(dict(type='cmu3DSurface',id=uid,atlasIndex=index,texture=f'/Textures/CMU14/ThreeD/Surfaces/{uid}.png'))
  uid=self.cache[signature];actual=Image.open(TEXTURES/(uid+'.png')).convert('RGBA');assert actual.tobytes()==im.tobytes() and actual.size==im.size
  self.crops.append(dict(state=state,rect=list(rect),surface=uid,pixels=im.width*im.height,rgbaSha256=hashlib.sha256(im.tobytes()).hexdigest()))
  return uid

def box(label,rect,z0,z1,color,surface=None):
 x0,y0,x1,y1=rect
 part=dict(label=label,min=[(x0-16)/32,(16-y1)/32,z0],max=[(x1-16)/32,(16-y0)/32,z1],color=color)
 if surface:part.update(surface=surface,surfaceAxis='XY')
 return part

def geometry(state,pool):
 im=source(state);mask=np.zeros((32,32),dtype=bool);reconstruction=Image.new('RGBA',(32,32));parts=[];plies=[]
 plastic=state.startswith('plastic');edge,body=('#879495','#D3DCDC') if plastic else ('#242222','#7E7D7D')
 palette={tuple(p) for p in np.asarray(im).reshape(-1,4) if p[3]}
 for color in (edge,body):assert tuple(int(color[i:i+2],16) for i in (1,3,5))+(255,) in palette
 for n,rect in enumerate(SHEETS[state]):
  # Complete, closed individual plates touch vertically. Source-visible edge
  # offsets are real overhangs between plates, not lines painted on one box.
  low=n*.020;high=(n+1)*.020
  parts.append(box(f'sheet {n+1} dark lower edge',rect,low,low+.002,edge))
  parts.append(box(f'sheet {n+1} solid material',rect,low+.002,high-.003,body))
  uid=pool.crop(state,rect);parts.append(box(f'sheet {n+1} original upper surface',rect,high-.003,high,'#FFFFFF',uid))
  x0,y0,x1,y1=rect;mask[y0:y1,x0:x1]=True
  # Each original crop is placed at its unchanged source pixel coordinates.
  # Higher sheets cover lower hidden artwork, preserving the visible source.
  reconstruction.paste(Image.open(TEXTURES/(uid+'.png')).convert('RGBA'),(x0,y0))
  plies.append(dict(rect=list(rect),minZ=low,maxZ=high,surface=uid))
 assert np.array_equal(mask,np.asarray(im)[:,:,3]>0)
 assert reconstruction.tobytes()==im.tobytes()
 return parts,dict(state=state,visibleSheetTiers=len(SHEETS[state]),parts=len(parts),sourceAlphaPixels=int(mask.sum()),
                   originalRgbaSha256=hashlib.sha256(im.tobytes()).hexdigest(),exactPrintedPlanReconstruction=True,plies=plies,
                   sourcePath=(bm.resource_file(rsi(state.split('_')[0]))/(state+'.png')).relative_to(ROOT).as_posix())

def configure(pool):
 registry=surfaces.load_surfaces()
 for row in pool.rows:
  path=TEXTURES/(row['id']+'.png');registry[row['id']]={**row,'file':path,'image':Image.open(path).convert('RGBA')}
 surfaces.load_surfaces=lambda:registry

def serialize(models):
 result=deepcopy(models)
 for model in result:
  for key in ('sourceSpriteOffset','groundOffset'):model[key]=', '.join(str(v) for v in model[key])
  for parts in [model['parts']]+[frame['parts'] for definition in model['spriteStates'].values() for frame in definition['frames']]:
   for p in parts:
    for bound in ('min','max'):p[bound]=', '.join(f'{v:.8g}' for v in p[bound])
 return result

def reviews(compositions):
 REVIEW.mkdir(parents=True,exist_ok=True);font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',15)
 montage=Image.new('RGB',(1400,660),'#17212B');draw=ImageDraw.Draw(montage)
 draw.text((12,10),'Original source / complete stacked solids; tier thickness and unseen sides inferred',font=font,fill='white')
 for n,(state,parts) in enumerate(compositions.items()):
  x=n%4*350;y=n//4*310+40;ref=Image.new('RGBA',(112,112),'#60717A');ref.alpha_composite(source(state).resize((112,112),Image.Resampling.NEAREST));montage.paste(ref.convert('RGB'),(x+6,y+65))
  montage.paste(bm.render_model({'parts':parts},(232,240),-math.pi/2+.3,.62,pixels_per_unit=245,screen_origin=(112,145)),(x+118,y+20));draw.text((x+12,y+272),state,font=font,fill='white')
  card=Image.new('RGB',(1080,345),'#17212B');d=ImageDraw.Draw(card);d.text((12,10),state+' / '+str(len(parts))+' solid parts',font=font,fill='white')
  original=Image.new('RGBA',(224,224),'#60717A');original.alpha_composite(source(state).resize((224,224),Image.Resampling.NEAREST));card.paste(original.convert('RGB'),(12,60))
  for i,(yaw,pitch) in enumerate(((-math.pi/2,.88),(-.25,.6),(math.pi/2,.26))):card.paste(bm.render_model({'parts':parts},(275,280),yaw,pitch,pixels_per_unit=280,screen_origin=(135,155)),(245+275*i,45))
  card.save(REVIEW/(state+'.png'))
 montage.save(REVIEW/'source-model-montage.png')

def context_checks(models,draw):
 index={m['id']:m for m in json.loads((BASE/'models.json').read_text())['models']+models};by_proto={p:m for m in models for p in m['sourcePrototypes']}
 source_audit=json.loads((GEN/'material-stacks-source-audit.json').read_text());saved={(r['map'],r['uid']):r for r in source_audit['records']}
 records=[];inputs={};cards=0
 for spec in json.loads((BASE/'scenes.json').read_text()):
  path=BASE/spec['file'];doc=json.loads(path.read_text());targets=[e for e in doc['instances'] if e['prototype'] in by_proto]
  if not targets:continue
  inputs[path.relative_to(ROOT).as_posix()]=sha(path)
  for e in targets:
   model=by_proto[e['prototype']];raw=saved[(doc['map']['path'],e['id'])]
   assert set(raw['savedComponents'])=={'Transform'}
   state,reason=sprite_states.saved_pose(model,source_audit['defaults'][e['prototype']],raw['savedComponents'],scene.normalize_tint)
   assert state==raw['sourceOwnerState']==model['referenceState'] and reason is None
   e.update(modelId=model['id'],matchKind='exact',renderYaw=e['yaw']);e.pop('geometryKey',None)
  resolve_placements(doc['instances'],list(index.values()),doc.get('geometryVariants',{}))
  for e in targets:
   model=by_proto[e['prototype']];own=world_parts(model['parts'],e['position'],e['renderYaw'],e.get('renderOffset',[0,0,0]));near=[];pieces=deepcopy(own)
   for other in doc['instances']:
    if e['id']==other['id'] or max(abs(e['position'][i]-other['position'][i]) for i in (0,1))>.9:continue
    target=index.get(other.get('modelId'))
    if target is None:near.append(dict(uid=other['id'],prototype=other['prototype'],mapped=False));continue
    actual=doc.get('geometryVariants',{}).get(other.get('geometryKey'),target['parts']);world=world_parts(actual,other['position'],other.get('renderYaw',other['yaw']),other.get('renderOffset',[0,0,0]));hits,_=contacts(own,world)
    near.append(dict(uid=other['id'],prototype=other['prototype'],mapped=True,conservativePartPairs=len(hits),hits=hits[:12],sourceDuplicate=other['position']==e['position'] and other['prototype']==e['prototype']))
    pieces.extend(world)
   records.append(dict(variant=spec['variant'],level=spec['level'],uid=e['id'],prototype=e['prototype'],modelId=e['modelId'],position=e['position'],yaw=e['yaw'],renderYaw=e['renderYaw'],state=model['referenceState'],renderOffset=e.get('renderOffset',[0,0,0]),support=e.get('support'),neighbors=near))
   if draw and (cards<8 or any(n.get('conservativePartPairs') for n in near)):
    local=[]
    for p in pieces:
     for bound in ('min','max'):
      for axis in (0,1):p[bound][axis]-=e['position'][axis]
     if p['min'][2]>=1.4:continue
     p['max'][2]=min(p['max'][2],1.4);local.append(p)
    bm.render_model({'parts':local},(760,620),-math.pi/2+.33,.67,pixels_per_unit=255,screen_origin=(380,430)).save(REVIEW/f'context-{spec["variant"]}-{spec["level"]}-{e["id"]}.png');cards+=1
 assert Counter(r['variant'] for r in records)=={'redux':21,'classic':3}
 return records,inputs

def main():
 global CHECK
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true');parser.add_argument('--skip-context',action='store_true');parser.add_argument('--skip-reviews',action='store_true');args=parser.parse_args();CHECK=args.check
 audit=json.loads((GEN/'material-stacks-source-audit.json').read_text());pool=Pool();compositions={};proof=[]
 for state in SHEETS:compositions[state],record=geometry(state,pool);proof.append(record)
 configure(pool);models=[]
 for uid in IDS:
  material='plastic' if uid=='RMCSheetPlastic' else 'metal';count=audit['defaults'][uid]['Stack']['count'];states=[material]+[material+'_'+str(n) for n in (2,3,4)];reference=states[min(3,count*4//50)]
  model=dict(type='cmu3DModel',id='CMU3D'+uid,label=uid,status='draft',sourcePrototypes=[uid],referencePrototype=uid,referenceRsi=rsi(material),referenceState=reference,referenceTint='#FFFFFF',
   sourceDirections=1,useEntityRotation=True,placement='surface',groundOffset='0, 0',sourceSpriteOffset='0, 0',sourceSpriteRotates=True,
   description='Source-owned material stack appearance with complete closed sheets, staggered physical edges and exact original upper artwork. Tier thickness and unseen sides inferred; see SOURCES_MATERIAL_STACKS.md.',
   parts=deepcopy(compositions[reference]),spriteStates={state:dict(frames=[dict(parts=deepcopy(compositions[state]))],delays=[1]) for state in states})
  model=bm.validate_model(model);sprite_states.validate_source(model,bm.resource_file);models.append(model)
 for path,rows in ((MODEL,serialize(models)),(ART,pool.rows)):write(path,('# Generated by Tools/three_d/author_material_stacks.py.\n'+yaml.safe_dump(rows,sort_keys=False,width=110)).encode())
 actual=bm.load_models(MODEL);assert len(actual)==5
 for model in actual:
  for definition in model['spriteStates'].values():assert min(p['min'][2] for p in definition['frames'][0]['parts'])==0
 if not args.skip_reviews:reviews(compositions)
 contexts,inputs=([],{}) if args.skip_context else context_checks(models,not args.skip_reviews)
 attribution=json.loads((bm.resource_file(rsi('metal'))/'meta.json').read_text())['copyright']
 note='''# Metal and plastic sheet stacks

Five exact draft mappings: CMSheetMetal, CMSheetMetal10, CMSheetMetal20, CMSheetMetal50 and RMCSheetPlastic. This batch has 24 saved world instances: 21 Redux and 3 classic; all 24 are visible/exported and no hidden containers were found. Raw maps have only Transform overrides. The only rotated saved pose is metal10 at -90 degrees (Redux 5696/classic 2806).

Each material has four original static world states. Source RSI is _RMC14/Objects/Materials/Sheets/metal.rsi or plastic.rsi, 32x32, one direction; in-hand art is excluded. Sprite.noRot=false and zero offset are preserved through sourceSpriteRotates and useEntityRotation. No source gameplay or count logic changes.

StackSystem owns the mapped base layer. CMSteel and RMCPlastic are explicitly unparented stacks with maximum 50. Their StackLayerFunction.None passes actual/max directly to ItemCounterSystem.ProcessOpaqueSprite and RoundToEqualLevels, which truncates positive actual/max*4 and clamps the maximum to index3. Default count 10 selects metal, 20 selects metal_2, 50 selects metal_4/plastic_4. Misleading top-level Sprite.state fields on 10/20 are not the visible owner state. The narrow offline stack_states adapter verifies these source maxima and honors valid saved maxCountOverride; other owners, missing compositions, invalid values or unknown Appearance data fall back. Native spriteStates follows the actual source layer rather than duplicating stack logic.

Each appearance has one to four complete closed sheets, with actual staggered overhanging edges. Three solids per sheet form a dark lower edge, material body and original textured upper surface. Every retained crop is verbatim RGBA from its state. Compositing the written surfaces at their original 32px coordinates exactly reproduces each full source frame; neither source pivot nor alpha silhouette is recentered. Higher sheets conceal lower crop areas, so hidden texture is not invented. The one-to-four tiers reproduce the sprite's representative stack, not every gameplay item in a 50-count stack. Thickness 0.02 per visible tier and unobserved sides are explicit depth inference, not recovered camera geometry.

placement:surface uses the existing exact authored support footprint and 0.002 gap, otherwise the stack rests on the floor. All appearances keep minimum Z 0, so count changes do not alter the support base. Context proof checks all 24 source placements against the frozen 978 scene library. Redux surface has two exact source duplicate pairs: metal 5693/5694 and plastic 21756/21757. Identical saved positions remain identical; a future reusable source-order stacking adapter is needed to separate them. No UID-dependent offsets or changed map transforms are used. Twelve further contexts intersect raised rack beams, trim, slots or post caps above the correctly selected shelf plane (0.948 + 0.002 gap). These remain explicit fitting work. material-stacks-contact-classification.json records the exact opaque-box contacts; material-stacks-proof.json also retains unknown nearby neighbors. These are rendered-solid checks, not gameplay collision tests.

Run `python Tools/three_d/author_material_stacks.py` for dedicated assets and reviews; `--check --skip-reviews` verifies deterministic assets. No global library/scene export, native build, game or server is launched. See generated/material-stacks-source-audit.json, material-stacks-proof.json and review/material-stacks/source-model-montage.png.

## Attribution

Original sheets and derived crops: CC-BY-SA-3.0. Preserve original attribution on redistribution.

'''+attribution+'\n'
 write(NOTE,note.encode())
 statechecks=[];lookup={p:m for m in models for p in m['sourcePrototypes']}
 for saved in audit['records']:
  state,reason=sprite_states.saved_pose(lookup[saved['prototype']],audit['defaults'][saved['prototype']],saved['savedComponents'],scene.normalize_tint);assert state==saved['sourceOwnerState'] and reason is None
  statechecks.append(dict(variant=saved['variant'],level=saved['level'],uid=saved['uid'],count=saved['effectiveCount'],state=state,hidden=saved['hiddenContainer']))
 report=dict(status='dedicated-assets-ready',models=5,exactMappings=IDS,sourceCompositions=proof,crops=pool.crops,surfaces=len(pool.rows),atlasIndices=[r['atlasIndex'] for r in pool.rows],
  sourceStateChecks=statechecks,contexts=contexts,contextInputSha256=inputs,sourceAuditSha256=sha(GEN/'material-stacks-source-audit.json'),
  writtenAssetsSha256={p.relative_to(ROOT).as_posix():sha(p) for p in WRITTEN},generatorSha256=sha(Path(__file__)),
  adapterSha256=sha(ROOT/'Tools/three_d/stack_states.py'),limitations=['Layer thickness and unseen construction are inferred; original printed plan/alpha preserved exactly.',
  'Two source-coincident stack pairs are unresolved physical overlaps, preserved explicitly.',
  'Conservative local contacts include unknown source neighbors and do not claim universal clearance.',
  'Native/global export verification belongs to the coordinated root pass.'])
 json_write(GEN/('material-stacks-geometry-proof.json' if args.skip_context else 'material-stacks-proof.json'),report)
 print(json.dumps(dict(models=5,surfaces=len(pool.rows),compositions={s:len(p) for s,p in compositions.items()},contexts=len(contexts),contacts=sum(n.get('conservativePartPairs',0) for r in contexts for n in r['neighbors']))))

if __name__=='__main__':main()
