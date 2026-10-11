import {Renderer, appendBox} from './renderer.js';
import {OrbitCamera, clamp} from './math.js';

const $=id=>document.getElementById(id),canvas=$('model');
const camera=new OrbitCamera();camera.target=[0,-.35,2.3];camera.yaw=1.3;camera.pitch=.5;camera.distance=1.1;
let models,renderer,dirty=true;
function fail(error){$('error').hidden=false;$('error').textContent=error.message;}
function render(){
    if(!models)return;
    const model=models.find(m=>m.id===$('variant').value),suffix=$('state').value;
    const state=model.referenceState.replace(/(?:1|0|-empty|-broken|-burned)$/,'')+suffix;
    const direction=Number($('facing').value),yaw=[0,Math.PI,Math.PI/2,-Math.PI/2][direction];
    const inside=$('mount').value==='inside',parts=model.poweredLightStates[state].parts;
    const boxes=[],rounded=[],cylinders=[[],[],[]];
    for(const p of parts){
        const low=[...p.min],high=[...p.max];
        if(inside){low[1]=-1-p.max[1];high[1]=-1-p.min[1];}
        const target=p.shape?.startsWith('Cylinder')?cylinders['XYZ'.indexOf(p.shape.at(-1))]:p.shape==='Ellipsoid'?rounded:boxes;
        appendBox(target,low,high,p.color,[0,0,0],yaw,0);
    }
    if($('wall').checked)appendBox(boxes,[-.5,inside?-1.5:-.5,0],[.5,inside?-.5:.5,2.6],'#42494E',[0,0,0],yaw,0);
    renderer.setGeometry(boxes,rounded,cylinders);
    const y=inside?-.35:-.65;camera.target=[-y*Math.sin(yaw),y*Math.cos(yaw),2.3];
    $('source').src=model.poweredLightReferences[state][direction];
    $('frame').textContent=`${$('variant').selectedOptions[0].textContent} · ${$('state').selectedOptions[0].textContent} · ${parts.length} solid parts`;
    renderer.draw(camera);
}
for(const id of ['variant','state','facing','mount','wall'])$(id).addEventListener('change',()=>{
    if(id==='variant')$('state').value=$('variant').value==='CMU3DSmallEmptyWallLight'?'-empty':'1';
    dirty=true;
});
let drag;
canvas.addEventListener('pointerdown',e=>{if(e.button!==0)return;drag=[e.clientX,e.clientY];canvas.setPointerCapture(e.pointerId);});
canvas.addEventListener('pointermove',e=>{if(!drag)return;camera.orbit(e.clientX-drag[0],e.clientY-drag[1]);drag=[e.clientX,e.clientY];dirty=true;});
for(const event of ['pointerup','pointercancel','lostpointercapture'])canvas.addEventListener(event,()=>{drag=null;});
canvas.addEventListener('wheel',e=>{e.preventDefault();camera.distance=clamp(camera.distance*Math.exp(e.deltaY*.0012),.3,5);dirty=true;},{passive:false});
canvas.addEventListener('keydown',e=>{const d={ArrowLeft:[-30,0],ArrowRight:[30,0],ArrowUp:[0,-30],ArrowDown:[0,30]}[e.key];if(d){e.preventDefault();camera.orbit(...d);dirty=true;}});
canvas.addEventListener('webglcontextlost',e=>{e.preventDefault();fail(new Error('Graphics context lost. Reload to resume.'));});
window.addEventListener('resize',()=>{dirty=true;});window.addEventListener('pagehide',()=>renderer?.dispose());
function tick(){if(dirty){dirty=false;try{render();}catch(error){fail(error);}}requestAnimationFrame(tick);}
try{
    const response=await fetch('../generated/models.json');if(!response.ok)throw new Error('Model library failed to load.');
    models=(await response.json()).models.filter(m=>m.poweredLightStates);
    for(const option of $('variant').options){
        if(!models.some(model=>model.id===option.value))throw new Error(`Light states are missing for ${option.textContent}.`);
    }
    renderer=new Renderer(canvas);await renderer.loadSurfaces();requestAnimationFrame(tick);
}catch(error){fail(error);}
