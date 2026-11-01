import {Renderer, appendBox} from './renderer.js';
import {OrbitCamera, clamp} from './math.js';
import {entityFocusPoint} from './focus.js';
import {studyModel, spritePeriod, sampleSpriteFrame, spriteFrameTime, spriteComposition, spriteReference} from './sprite-timeline.js';

const $ = id => document.getElementById(id), canvas = $('model');
const camera = new OrbitCamera();
camera.target = [0,0,1.3]; camera.yaw = -1.3; camera.pitch = .28; camera.distance = 4;
let library, renderer, model, seconds = 0, running = false, lastTime = 0, dirty = true, lastKey = '', request = 0, ready = false;
const loadedImages = new Map();
function fail(error) { running = false; ready = false; $('error').hidden = false; $('error').textContent = error.message; controls(); }
function controls() {
    const moving = ready && model.spriteStates[$('state').value].frames.length > 1;
    for (const id of ['play','previous','next','time']) $(id).disabled = !moving;
    $('reset').disabled = !ready; $('play').textContent = running ? 'Pause' : 'Play loop';
}
function selectedYaw() {
    let yaw = [0,Math.PI,Math.PI/2,-Math.PI/2][model.referenceDirection || 0];
    if (!model.useEntityRotation && model.sourceCardinalFacings?.length === 4) {
        const turn = ((Math.round(yaw/(Math.PI/2))%4)+4)%4;
        yaw = model.sourceCardinalFacings[turn]*Math.PI/2;
    }
    if (!model.useEntityRotation && model.swapEastWest && Math.abs(Math.sin(yaw))>.5) yaw += Math.PI;
    return yaw + (model.yawOffset || 0)*Math.PI/180;
}
function fitModel() {
    const parts = Object.values(model.spriteStates).flatMap(state => state.frames.flatMap(frame => frame.parts));
    const yaw = selectedYaw(), c = Math.cos(yaw), s = Math.sin(yaw);
    camera.target = entityFocusPoint({position:[0,0,0], yaw}, parts);
    let radius = 0;
    for (const part of parts) {
        const center = part.min.map((v,i) => (v+part.max[i])/2);
        const world = [c*center[0]-s*center[1], s*center[0]+c*center[1], center[2]];
        const half = part.min.map((v,i) => (part.max[i]-v)/2);
        radius = Math.max(radius, Math.hypot(...world.map((v,i) => v-camera.target[i])) + Math.hypot(...half));
    }
    const angle = Math.atan(Math.tan(Math.PI/8)*Math.min(1, canvas.clientWidth/canvas.clientHeight));
    camera.distance = clamp(radius*1.15/Math.sin(angle), .7, 12);
}
function render() {
    if (!ready) return;
    const state = $('state').value, pose = sampleSpriteFrame(model, state, seconds);
    const key = `${model.id}:${state}:${pose.frame}`;
    if (key !== lastKey) {
        const parts = spriteComposition(model, pose), source = spriteReference(model, pose);
        if (!parts || !source) throw new Error('This pose is missing its geometry or source reference.');
        const boxes=[], rounded=[], cylinders=[[],[],[]], wedges=[], reverse=[], slanted=[[],[],[],[]], foliage=[];
        for (const part of parts) {
            const target = part.shape?.startsWith('Cylinder') ? cylinders['XYZ'.indexOf(part.shape.at(-1))]
                : part.shape==='Ellipsoid' ? rounded : part.shape==='WedgeY' ? wedges : part.shape==='WedgeYReverse' ? reverse
                : part.shape?.startsWith('Slanted') ? slanted[['SlantedX','SlantedXReverse','SlantedY','SlantedYReverse'].indexOf(part.shape)]
                : part.shape==='Foliage' ? foliage : boxes;
            appendBox(target, part.min, part.max, part.color, [0,0,0], selectedYaw(), 0, {partYaw:part.yaw, partPitch:part.pitch,
                surface:renderer.surfaces.get(part.surface), surfaceAxis:part.surfaceAxis, surfaceFlipU:part.surfaceFlipU});
        }
        renderer.setGeometry(boxes, rounded, cylinders, wedges, reverse, slanted, foliage);
        $('source').src = source;
        $('frame').textContent = `${model.label} · ${state} · frame ${pose.frame+1} of ${model.spriteStates[state].frames.length} · ${parts.length} solid parts`;
        lastKey = key;
    }
    renderer.draw(camera);
    $('clock').value = `${seconds.toFixed(3)} s`; $('time').value = seconds; controls();
}
async function selectModel() {
    const token = ++request, root = library.get($('family').value);
    ready = false; running = false; $('state').disabled = true; controls();
    try {
        const direction = root.sourceDirections === 4 ? Number($('facing').value) : 0;
        model = library.get(root.directionalModels?.[direction]) || (direction===0 ? root : null);
        if (!model?.spriteStates) throw new Error('The selected source facing has no authored model.');
        const previous = $('state').value;
        $('state').replaceChildren(...Object.keys(model.spriteStates).map(state => new Option(state, state)));
        $('state').value = model.spriteStates[previous] ? previous : model.referenceState;
        $('facing').disabled = root.sourceDirections !== 4;
        if (root.sourceDirections !== 4) $('facing').value = '0';
        await Promise.all(Object.values(model.spriteStateReferences || {}).flat().map(url => {
            if (!loadedImages.has(url)) {
                const image = new Image(); image.src = url;
                loadedImages.set(url, image.decode().then(() => image));
            }
            return loadedImages.get(url);
        }));
        if (token !== request) return;
        $('error').hidden = true; ready = true; $('state').disabled = false; fitModel(); reset();
    } catch (error) { if (token === request) fail(error); }
}
function reset() { running=false; seconds=0; lastKey=''; lastTime=performance.now(); $('time').max=spritePeriod(model,$('state').value); dirty=true; controls(); }
for (const id of ['family','facing']) $(id).addEventListener('change',selectModel);
$('state').addEventListener('change',reset);
$('play').addEventListener('click',()=>{running=!running;lastTime=performance.now();dirty=true;});
$('reset').addEventListener('click',reset);
for (const [id, delta] of [['previous',-1],['next',1]]) $(id).addEventListener('click',()=>{
    const state=$('state').value, count=model.spriteStates[state].frames.length;
    const frame=(sampleSpriteFrame(model,state,seconds).frame+delta+count)%count;
    running=false;seconds=spriteFrameTime(model,state,frame);dirty=true;
});
$('time').addEventListener('input',()=>{running=false;seconds=Number($('time').value);dirty=true;});
let drag;
canvas.addEventListener('pointerdown',e=>{if(e.button!==0)return;drag=[e.clientX,e.clientY];canvas.setPointerCapture(e.pointerId);});
canvas.addEventListener('pointermove',e=>{if(!drag)return;camera.orbit(e.clientX-drag[0],e.clientY-drag[1]);drag=[e.clientX,e.clientY];dirty=true;});
for (const event of ['pointerup','pointercancel','lostpointercapture']) canvas.addEventListener(event,()=>{drag=null;});
canvas.addEventListener('wheel',e=>{e.preventDefault();camera.distance=clamp(camera.distance*Math.exp(e.deltaY*.0012),.7,12);dirty=true;},{passive:false});
canvas.addEventListener('keydown',e=>{const delta={ArrowLeft:[-30,0],ArrowRight:[30,0],ArrowUp:[0,-30],ArrowDown:[0,30]}[e.key];if(delta){e.preventDefault();camera.orbit(...delta);dirty=true;}});
canvas.addEventListener('webglcontextlost',e=>{e.preventDefault();fail(new Error('Graphics context lost. Reload to resume.'));});
window.addEventListener('resize',()=>{dirty=true;});window.addEventListener('pagehide',()=>renderer?.dispose());
document.addEventListener('visibilitychange',()=>{if(document.hidden){running=false;dirty=true;}lastTime=performance.now();});
function tick(now) {
    if (running && ready) {seconds=(seconds+(now-lastTime)/1000)%spritePeriod(model,$('state').value);lastTime=now;dirty=true;}
    if (dirty) {dirty=false;try{render();}catch(error){fail(error);}}
    requestAnimationFrame(tick);
}
try {
    const response=await fetch('../generated/models.json');if(!response.ok)throw new Error('Model library failed to load.');
    library=new Map((await response.json()).models.map(studyModel).filter(m=>m.spriteStates).map(m=>[m.id,m]));
    const roots=[...library.values()].filter(m=>!m.directionalModels?.length || m.referenceDirection===0);
    if(!roots.length)throw new Error('No authored generic sprite states have been exported yet.');
    $('family').replaceChildren(...roots.map(m=>new Option(m.label,m.id)));
    const requested=library.get(new URLSearchParams(location.search).get('model'));
    if(requested){$('family').value=requested.directionalModels?.[0]||requested.id;$('facing').value=String(requested.referenceDirection||0);}
    renderer=new Renderer(canvas);await renderer.loadSurfaces();$('family').disabled=false;
    await selectModel();requestAnimationFrame(tick);
} catch(error) {fail(error);}
