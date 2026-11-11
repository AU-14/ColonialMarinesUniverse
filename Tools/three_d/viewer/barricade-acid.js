import {Renderer, appendBox} from './renderer.js';
import {OrbitCamera, clamp} from './math.js';
import {sampleAcidFrame, acidComposition} from './acid-timeline.js';
import {barricadeReference} from './reference.js';

const $=id=>document.getElementById(id), canvas=$('model');
const camera=new OrbitCamera(); camera.target=[0,-.375,.35]; camera.yaw=-1.3; camera.pitch=.35; camera.distance=2;
let model,renderer,seconds=0,running=false,lastTime=0,dirty=true,lastKey='';
function fail(error){running=false;$('error').hidden=false;$('error').textContent=error.message;}
function render(){
    if(!model)return;
    const damage=$('damage').value,wired=$('wire').checked,visible=$('acid').checked,direction=Number($('facing').value);
    const frame=sampleAcidFrame(seconds,visible),yaw=[0,Math.PI,Math.PI/2,-Math.PI/2][direction];
    const key=[damage,wired,frame,direction].join(':');
    if(key!==lastKey){
        const parts=acidComposition(model,damage,wired,frame);
        if(!parts)throw new Error('This state has no authored composition.');
        const boxes=[],rounded=[],cylinders=[[],[],[]];
        for(const p of parts){
            const target=p.shape?.startsWith('Cylinder')?cylinders['XYZ'.indexOf(p.shape.at(-1))]:p.shape==='Ellipsoid'?rounded:boxes;
            appendBox(target,p.min,p.max,p.color,[0,0,0],yaw,0,{partYaw:p.yaw,partPitch:p.pitch,
                surface:renderer.surfaces.get(p.surface),surfaceAxis:p.surfaceAxis,surfaceFlipU:p.surfaceFlipU});
        }
        renderer.setGeometry(boxes,rounded,cylinders);
        camera.target=[.375*Math.sin(yaw),-.375*Math.cos(yaw),.35];
        const entity={matchKind:'exact',yaw,barricadeDamageState:damage,barricadeWired:wired};
        if(frame!==null)entity.barricadeAcidFrame=frame;
        const source=barricadeReference(entity,model);
        if(!source)throw new Error('This state has no original source composition.');
        $('source').src=source;
        $('frame').textContent=`Damage ${damage} · ${wired?'wired':'unwired'} · ${frame===null?'acid cleared':`acid frame ${frame+1} of 5`} · ${parts.length} solid parts`;
        lastKey=key;
    }
    renderer.draw(camera);
    $('play').textContent=running?'Pause':'Play loop';
    $('clock').value=(seconds%.5).toFixed(3)+' s';$('time').value=seconds%.5;
}
for(const id of ['damage','wire','acid','facing'])$(id).addEventListener('change',()=>{dirty=true;});
$('play').addEventListener('click',()=>{running=!running;lastTime=performance.now();dirty=true;});
$('reset').addEventListener('click',()=>{running=false;seconds=0;dirty=true;});
$('time').addEventListener('input',()=>{running=false;seconds=Number($('time').value);dirty=true;});
let drag;
canvas.addEventListener('pointerdown',e=>{if(e.button!==0)return;drag=[e.clientX,e.clientY];canvas.setPointerCapture(e.pointerId);});
canvas.addEventListener('pointermove',e=>{if(!drag)return;camera.orbit(e.clientX-drag[0],e.clientY-drag[1]);drag=[e.clientX,e.clientY];dirty=true;});
for(const event of ['pointerup','pointercancel','lostpointercapture'])canvas.addEventListener(event,()=>{drag=null;});
canvas.addEventListener('wheel',e=>{e.preventDefault();camera.distance=clamp(camera.distance*Math.exp(e.deltaY*.0012),.65,6);dirty=true;},{passive:false});
canvas.addEventListener('keydown',e=>{const delta={ArrowLeft:[-30,0],ArrowRight:[30,0],ArrowUp:[0,-30],ArrowDown:[0,30]}[e.key];if(delta){e.preventDefault();camera.orbit(...delta);dirty=true;}});
canvas.addEventListener('webglcontextlost',e=>{e.preventDefault();fail(new Error('Graphics context lost. Reload to resume.'));});
window.addEventListener('resize',()=>{dirty=true;});window.addEventListener('pagehide',()=>renderer?.dispose());
function tick(now){
    if(running){seconds+=(now-lastTime)/1000;lastTime=now;dirty=true;}
    if(dirty){dirty=false;try{render();}catch(error){fail(error);}}
    requestAnimationFrame(tick);
}
try{
    const response=await fetch('../generated/models.json');if(!response.ok)throw new Error('Model library failed to load.');
    model=(await response.json()).models.find(m=>m.id==='CMU3DReinforcedPlasteelBarricade');
    if(!model?.barricadeAcidStates)throw new Error('Acid model frames are missing.');
    renderer=new Renderer(canvas);await renderer.loadSurfaces();
    const urls=[...Object.values(model.barricadeReferences).flat(),...Object.values(model.barricadeWiredReferences).flat(),
        ...Object.values(model.barricadeAcidReferences).flat(2)];
    await Promise.all(urls.map(async url=>{const image=new Image();image.src=url;await image.decode();}));
    $('play').disabled=false;$('reset').disabled=false;requestAnimationFrame(tick);
}catch(error){fail(error);}
