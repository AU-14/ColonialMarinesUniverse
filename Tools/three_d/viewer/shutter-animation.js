import {Renderer, appendBox} from './renderer.js';
import {OrbitCamera, clamp} from './math.js';
import {sampleDoorFrame, doorComposition} from './door-timeline.js';

const $=id=>document.getElementById(id), canvas=$('model');
const camera=new OrbitCamera(); camera.target=[0,0,1.35]; camera.yaw=-1.3; camera.pitch=.28; camera.distance=4;
let model,renderer,seconds=0,running=false,lastTime=0,dirty=true,lastKey='';
function fail(error){running=false;$('error').hidden=false;$('error').textContent=error.message;}
function render(){
    if(!model)return;
    const state=$('state').value,direction=Number($('facing').value);
    const pose=sampleDoorFrame(model,state,seconds),yaw=[0,Math.PI,Math.PI/2,-Math.PI/2][direction];
    const key=[pose.state,pose.frame,direction].join(':');
    if(key!==lastKey){
        const parts=doorComposition(model,pose);
        if(!parts)throw new Error('This state has no authored composition.');
        const boxes=[],rounded=[],cylinders=[[],[],[]];
        for(const p of parts){
            const target=p.shape?.startsWith('Cylinder')?cylinders['XYZ'.indexOf(p.shape.at(-1))]:p.shape==='Ellipsoid'?rounded:boxes;
            appendBox(target,p.min,p.max,p.color,[0,0,0],yaw,0,{partYaw:p.yaw,partPitch:p.pitch,
                surface:renderer.surfaces.get(p.surface),surfaceAxis:p.surfaceAxis,surfaceFlipU:p.surfaceFlipU});
        }
        renderer.setGeometry(boxes,rounded,cylinders);
        const source=model.doorSpriteReferences[pose.state]?.[pose.frame]?.[direction];
        if(!source)throw new Error('This frame has no source reference.');
        $('source').src=source;
        $('frame').textContent=`${pose.state} · frame ${pose.frame+1} of ${model.doorSpriteStates[pose.state].frames.length} · ${parts.length} solid parts`;
        lastKey=key;
    }
    renderer.draw(camera);
    $('play').textContent=running?'Pause':'Play transition';
    $('clock').value=seconds.toFixed(3)+' s';$('time').value=seconds;
}
for(const id of ['state','facing'])$(id).addEventListener('change',()=>{if(id==='state'){seconds=0;lastKey='';lastTime=performance.now();}dirty=true;});
$('play').addEventListener('click',()=>{if(seconds>=1)seconds=0;running=!running;lastTime=performance.now();dirty=true;});
$('reset').addEventListener('click',()=>{running=false;seconds=0;dirty=true;});
$('time').addEventListener('input',()=>{running=false;seconds=Number($('time').value);dirty=true;});
let drag;
canvas.addEventListener('pointerdown',e=>{if(e.button!==0)return;drag=[e.clientX,e.clientY];canvas.setPointerCapture(e.pointerId);});
canvas.addEventListener('pointermove',e=>{if(!drag)return;camera.orbit(e.clientX-drag[0],e.clientY-drag[1]);drag=[e.clientX,e.clientY];dirty=true;});
for(const event of ['pointerup','pointercancel','lostpointercapture'])canvas.addEventListener(event,()=>{drag=null;});
canvas.addEventListener('wheel',e=>{e.preventDefault();camera.distance=clamp(camera.distance*Math.exp(e.deltaY*.0012),1,9);dirty=true;},{passive:false});
canvas.addEventListener('keydown',e=>{const delta={ArrowLeft:[-30,0],ArrowRight:[30,0],ArrowUp:[0,-30],ArrowDown:[0,30]}[e.key];if(delta){e.preventDefault();camera.orbit(...delta);dirty=true;}});
canvas.addEventListener('webglcontextlost',e=>{e.preventDefault();fail(new Error('Graphics context lost. Reload to resume.'));});
window.addEventListener('resize',()=>{dirty=true;});window.addEventListener('pagehide',()=>renderer?.dispose());
function tick(now){
    if(running){seconds=Math.min(1,seconds+(now-lastTime)/1000);lastTime=now;if(seconds>=1)running=false;dirty=true;}
    if(dirty){dirty=false;try{render();}catch(error){fail(error);}}
    requestAnimationFrame(tick);
}
try{
    const response=await fetch('../generated/models.json');if(!response.ok)throw new Error('Model library failed to load.');
    model=(await response.json()).models.find(m=>m.id==='CMU3DHybrisaWindowShutter');
    if(!model?.doorSpriteStates)throw new Error('Shutter animation frames are missing.');
    renderer=new Renderer(canvas);await renderer.loadSurfaces();
    const urls=Object.values(model.doorSpriteReferences).flat(2);
    await Promise.all(urls.map(async url=>{const image=new Image();image.src=url;await image.decode();}));
    $('play').disabled=false;$('reset').disabled=false;requestAnimationFrame(tick);
}catch(error){fail(error);}
