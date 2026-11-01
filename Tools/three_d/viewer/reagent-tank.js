import {Renderer,appendBox} from './renderer.js';
import {OrbitCamera,clamp} from './math.js';
import {tankComposition} from './reagent-tank-state.js';
import {tankReferenceCanvas} from './reagent-tank-reference.js';
const $=id=>document.getElementById(id),canvas=$('model'),source=$('source'),ctx=source.getContext('2d');
const camera=new OrbitCamera();camera.target=[0,0,.4];camera.distance=2.1;camera.yaw=-1.3;camera.pitch=.35;
let renderer,library,model,images={},ready=false,dirty=true,drag,request=0;
function fail(error){ready=false;$('error').hidden=false;$('error').textContent=error.message;}
function pose(){return {visible:$('visible').checked,color:$('color').value,alpha:Number($('alpha').value),spriteTint:$('tint').value,spriteAlpha:1};}
function sourceImage(p){
    ctx.clearRect(0,0,32,32);
    ctx.drawImage(tankReferenceCanvas(images,{...p,state:$('state').value}),0,0);
}
function rebuild(){
    if(!ready)return;const p=pose(),parts=tankComposition(model,p),boxes=[],rounded=[],cylinders=[[],[],[]],wedges=[],reverse=[],slanted=[[],[],[],[]],foliage=[];
    for(const part of parts){const target=part.shape?.startsWith('Cylinder')?cylinders['XYZ'.indexOf(part.shape.at(-1))]:part.shape==='Ellipsoid'?rounded:part.shape==='WedgeY'?wedges:part.shape==='WedgeYReverse'?reverse:part.shape?.startsWith('Slanted')?slanted[['SlantedX','SlantedXReverse','SlantedY','SlantedYReverse'].indexOf(part.shape)]:part.shape==='Foliage'?foliage:boxes;
        appendBox(target,part.min,part.max,part.color,[0,0,0],(model.yawOffset||0)*Math.PI/180,0,{partYaw:part.yaw,partPitch:part.pitch,surface:renderer.surfaces.get(part.surface),surfaceAxis:part.surfaceAxis,surfaceFlipU:part.surfaceFlipU});}
    renderer.setGeometry(boxes,rounded,cylinders,wedges,reverse,slanted,foliage);sourceImage(p);dirty=true;
    $('status').textContent=`${model.label} · ${parts.length} solid parts · Fill ${p.visible?'visible':'hidden'} · opacity ${p.alpha.toFixed(2)}`;
}
async function select(){const token=++request;ready=false;model=library.get($('family').value);try{
    const loaded=await Promise.all(Object.entries(model.reagentTankReferences).map(async([state,url])=>{const image=new Image();image.src=url;await image.decode();return[state,image];}));
    if(token!==request)return;images=Object.fromEntries(loaded);ready=true;$('error').hidden=true;rebuild();
}catch(error){if(token===request)fail(error);}}
$('family').addEventListener('change',select);for(const id of ['state','visible','color','alpha','tint'])$(id).addEventListener('input',()=>{try{rebuild();}catch(error){fail(error);}});
canvas.addEventListener('pointerdown',e=>{if(e.button!==0)return;drag=[e.clientX,e.clientY];canvas.setPointerCapture(e.pointerId);});
canvas.addEventListener('pointermove',e=>{if(!drag)return;camera.orbit(e.clientX-drag[0],e.clientY-drag[1]);drag=[e.clientX,e.clientY];dirty=true;});
for(const event of ['pointerup','pointercancel','lostpointercapture'])canvas.addEventListener(event,()=>drag=null);
canvas.addEventListener('wheel',e=>{e.preventDefault();camera.distance=clamp(camera.distance*Math.exp(e.deltaY*.0012),.7,8);dirty=true;},{passive:false});
canvas.addEventListener('keydown',e=>{const d={ArrowLeft:[-30,0],ArrowRight:[30,0],ArrowUp:[0,-30],ArrowDown:[0,30]}[e.key];if(d){e.preventDefault();camera.orbit(...d);dirty=true;}});
canvas.addEventListener('webglcontextlost',e=>{e.preventDefault();fail(new Error('Graphics context lost. Reload to resume.'));});
window.addEventListener('resize',()=>dirty=true);window.addEventListener('pagehide',()=>renderer?.dispose());
function tick(){if(ready&&dirty){dirty=false;renderer.draw(camera);}requestAnimationFrame(tick);}
try{const response=await fetch('../generated/models.json');if(!response.ok)throw new Error('Model library failed to load.');library=new Map((await response.json()).models.filter(m=>m.reagentTankAppearance).map(m=>[m.id,m]));
    if(!library.size)throw new Error('Tank models have not been exported yet.');$('family').replaceChildren(...[...library.values()].map(m=>new Option(m.label,m.id)));
    const id=new URLSearchParams(location.search).get('model');if(library.has(id))$('family').value=id;
    renderer=new Renderer(canvas);await renderer.loadSurfaces();$('family').disabled=false;await select();requestAnimationFrame(tick);
}catch(error){fail(error);}
