import {Renderer, appendBox} from './renderer.js';
import {OrbitCamera, clamp} from './math.js';
import {sampleStudy, sequenceDuration} from './button-timeline.js';

const $ = id => document.getElementById(id);
const base = '../generated/button-animation-study/';
const views = [];
let data, seconds = 0, started = 0, running = false, dirty = true;

function error(problem) {
    running = false;
    $('error').hidden = false;
    $('error').textContent = problem.message;
}
function duration() { return sequenceDuration(data.studies, $('sequence').value); }
function updateControls() {
    const gameplay = ['Press', 'Denied'].includes($('sequence').value);
    $('play').disabled = running || duration() === 0 || gameplay && !$('power').checked;
    $('play').textContent = running ? 'Playing…' : 'Play';
    $('clock').value = `${seconds.toFixed(3)} s`;
    $('time').value = seconds;
    $('behavior').textContent = gameplay
        ? 'Press and denied sequences last 1.25 seconds, restart their source strip at 0.5 seconds, and return to idle. Unpowered controls reject new use.'
        : 'Resource inspection: the full source strip or pose is shown. Its presence in an RSI does not establish a gameplay trigger.';
}
function render() {
    for (const view of views) {
        const key = sampleStudy(view.study, $('sequence').value, seconds, $('power').checked);
        if (key !== view.key) {
            const frame = view.study.frames[key];
            const values = [];
            for (const part of frame.parts) appendBox(values, part.min, part.max, part.color, [0, 0, 0], 0, 0);
            view.renderer.setGeometry(values);
            view.source.src = base + frame.sourceImage;
            view.text.textContent = `${frame.state} · frame ${frame.frameIndex + 1} · ${frame.powered ? 'powered' : 'power overlay visible'}`;
            view.key = key;
        }
        view.renderer.draw(view.camera);
    }
    updateControls();
}
function refresh() {
    if (!data) return;
    dirty = false;
    try { render(); } catch (problem) { error(problem); }
}
function addView(study) {
    const article = document.createElement('article');
    const heading = document.createElement('h2'); heading.textContent = study.label;
    const row = document.createElement('div'); row.className = 'views';
    const sourceBox = document.createElement('div');
    const source = document.createElement('img'); source.className = 'source'; source.alt = study.label + ' source frame';
    const sourceCaption = document.createElement('p'); sourceCaption.className = 'caption'; sourceCaption.textContent = 'Original 32 × 32 frame';
    sourceBox.append(source, sourceCaption);
    const canvas = document.createElement('canvas'); canvas.className = 'model'; canvas.tabIndex = 0;
    canvas.setAttribute('aria-label', study.label + ' rotatable 3D animation');
    row.append(sourceBox, canvas);
    const text = document.createElement('p'); text.className = 'frame';
    const download = document.createElement('a'); download.href = base + study.glb; download.textContent = 'Download GLB · press + denied clips'; download.download = study.glb;
    article.append(heading, row, text, download); $('studies').append(article);
    const camera = new OrbitCamera(); camera.target = study.previewTarget || [-.015, 0, 1.3]; camera.yaw = -1.25; camera.pitch = .3; camera.distance = 1.1;
    const renderer = new Renderer(canvas);
    views.push({study, source, renderer, camera, text});
    let drag;
    canvas.addEventListener('pointerdown', event => { if (event.button !== 0) return; drag = [event.clientX, event.clientY]; canvas.setPointerCapture(event.pointerId); });
    canvas.addEventListener('pointermove', event => { if (!drag) return; camera.orbit(event.clientX - drag[0], event.clientY - drag[1]); drag = [event.clientX, event.clientY]; dirty = true; });
    for (const kind of ['pointerup', 'pointercancel', 'lostpointercapture']) canvas.addEventListener(kind, () => { drag = null; });
    canvas.addEventListener('wheel', event => { event.preventDefault(); camera.distance = clamp(camera.distance * Math.exp(event.deltaY * .0012), .55, 4); dirty = true; }, {passive: false});
    canvas.addEventListener('keydown', event => {
        const delta = {ArrowLeft: [-30, 0], ArrowRight: [30, 0], ArrowUp: [0, -30], ArrowDown: [0, 30]}[event.key];
        if (delta) { event.preventDefault(); camera.orbit(...delta); dirty = true; }
    });
    canvas.addEventListener('webglcontextlost', event => { event.preventDefault(); error(new Error('The graphics context was lost. Reload this page to resume the study.')); });
}

$('play').addEventListener('click', () => { if (running || $('play').disabled) return; seconds = 0; started = performance.now(); running = true; refresh(); });
$('reset').addEventListener('click', () => { running = false; seconds = 0; refresh(); });
$('time').addEventListener('input', () => { running = false; seconds = Number($('time').value); refresh(); });
$('power').addEventListener('change', refresh);
$('sequence').addEventListener('change', () => { if (!data) return; running = false; seconds = 0; $('time').max = duration() || 1; $('time').disabled = duration() === 0; refresh(); });
window.addEventListener('resize', () => { dirty = true; });
window.addEventListener('pagehide', () => { for (const view of views) view.renderer.dispose(); });
function tick(now) {
    if (data && running) { seconds = Math.min(duration(), (now - started) / 1000); if (seconds >= duration()) running = false; dirty = true; }
    if (data && dirty) { dirty = false; try { render(); } catch (problem) { error(problem); } }
    requestAnimationFrame(tick);
}
try {
    const response = await fetch(base + 'study.json');
    if (!response.ok) throw new Error(`Study data request failed: ${response.status}`);
    data = await response.json();
    for (const study of data.studies) addView(study);
    // Decode all tiny frames before playback to prevent stale image loads in the source comparison.
    await Promise.all(data.studies.flatMap(study => Object.values(study.frames).map(async frame => {
        const image = new Image(); image.src = base + frame.sourceImage; await image.decode();
    })));
    $('reset').disabled = false;
    refresh(); requestAnimationFrame(tick);
} catch (problem) { error(problem); }
