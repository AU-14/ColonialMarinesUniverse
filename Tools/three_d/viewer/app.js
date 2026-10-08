import {OrbitCamera, clamp, color} from './math.js';
import {Renderer} from './renderer.js';
import {buildEntityGeometry, structural} from './region-geometry.js';
import {FloatBuffer} from './float-buffer.js';
import {appendTile} from './terrain.js';
import {referenceFrame, barricadeReference} from './reference.js';
import {doorSpriteReference} from './door-timeline.js';
import {entityLevel, tileLevel, floorHeight, visibleLevels} from './levels.js';
import {entityFocusPoint} from './focus.js';
import {tankReferenceCanvas} from './reagent-tank-reference.js';

const $ = id => document.getElementById(id);
const number = value => Number(value).toLocaleString('en-US');
const camera = new OrbitCamera();
const canvas = $('scene');
const state = {
    scene: null, models: new Map(), renderer: null, selected: null, entities: [],
    entityIndex: new Map(), tileIndex: new Map(), floor: 0, radius: 24,
    needsDraw: true, rebuilding: false, lastRegion: null, floorValues: [],
    minimapBase: document.createElement('canvas'), miniTransform: null, referenceToken: 0,
};
const CHUNK_SIZE = 16;

function floorKey(z) { return Math.round((z || 0) * 1000) / 1000; }
function key(x, y, z) { return `${floorKey(z)}:${Math.floor(x / CHUNK_SIZE)}:${Math.floor(y / CHUNK_SIZE)}`; }
function indexItems(items, index, position) {
    index.clear();
    for (const item of items) {
        const [x, y, z] = position(item);
        if (![x, y, z].every(Number.isFinite)) continue;
        const cell = key(x, y, z);
        if (!index.has(cell)) index.set(cell, []);
        index.get(cell).push(item);
    }
}
function inRegion(index, center, radius, floor) {
    const result = [];
    for (const visible of visibleLevels(state.floorValues, floor, $('floor-mode').value))
    for (let x = Math.floor((center[0] - radius - 2) / CHUNK_SIZE); x <= Math.floor((center[0] + radius + 2) / CHUNK_SIZE); x++)
        for (let y = Math.floor((center[1] - radius - 2) / CHUNK_SIZE); y <= Math.floor((center[1] + radius + 2) / CHUNK_SIZE); y++)
            result.push(...(index.get(`${floorKey(visible)}:${x}:${y}`) || []));
    return result;
}
function tileColor(tile) {
    const palette = state.scene.tilePalette?.[tile.palette] || {};
    if (palette.variantColors?.[tile.variant || 0]) return palette.variantColors[tile.variant || 0];
    if (palette.color) return palette.color;
    const name = `${palette.prototype || tile.palette}`.toLowerCase();
    if (/water|river|ocean|pool/.test(name)) return '#3F606A';
    if (/grass|jungle|moss/.test(name)) return '#54604C';
    if (/sand|desert/.test(name)) return '#8C816A';
    if (/dirt|mud|soil|cave/.test(name)) return '#655F52';
    if (/wood|carpet/.test(name)) return '#786455';
    if (/road|asphalt|black/.test(name)) return '#424F53';
    if (/white|light|ceramic/.test(name)) return '#84908C';
    if (/grate|grating|steel|metal/.test(name)) return '#5E6A68';
    return '#68716B';
}
function showKind(kind) { return $(`show-${kind === 'exact' ? 'exact' : kind === 'inherited' ? 'inherited' : 'unmapped'}`).checked; }
function requestRebuild() {
    if (state.rebuilding || !state.scene) return;
    state.rebuilding = true;
    requestAnimationFrame(() => { state.rebuilding = false; rebuild(); });
}

function rebuild() {
    if (!state.renderer || !state.scene) return;
    const center = camera.target, radius = state.radius;
    const terrain = new FloatBuffer();
    let tileCount = 0;
    const inside = (x, y) => Math.abs(x - center[0]) <= radius && Math.abs(y - center[1]) <= radius;
    for (const tile of inRegion(state.tileIndex, center, radius, state.floor)) {
        const yaw = tile.yaw || 0;
        const tileCenter = [tile.x + (Math.cos(yaw) - Math.sin(yaw)) * 0.5, tile.y + (Math.sin(yaw) + Math.cos(yaw)) * 0.5];
        if (!inside(...tileCenter)) continue;
        const palette = state.scene.tilePalette?.[tile.palette] || {};
        appendTile(terrain, tile, state.renderer.terrain.rects.get(`${tile.palette}:${tile.variant || 0}`),
            color(tileColor(tile)), palette.allowRotationMirror === true);
        tileCount++;
    }
    const geometry = buildEntityGeometry(inRegion(state.entityIndex, center, radius + 2, state.floor),
        state.models, state.scene.geometryVariants, state.renderer.surfaces, {
            center, radius, showKinds: new Set(['exact', 'inherited', 'unmapped'].filter(showKind)),
            cutaway: $('cutaway').checked, maxHeight: Number($('wall-height').value), focusLevel: state.floor,
        });
    const {values, rounded, cylinders, wedges, reverseWedges, slanted, foliage, exact, inherited, unmapped, solidCount} = geometry;
    state.renderer.setGeometry(values, rounded, cylinders, wedges, reverseWedges, slanted, foliage);
    state.renderer.terrain.setGeometry(terrain.finish());
    state.needsDraw = true;
    state.lastRegion = [center[0], center[1]];
    $('region-stats').textContent = `${number(exact)} authored · ${number(inherited)} inherited · ${number(unmapped)} missing`;
    $('region-note').textContent = `${number(tileCount)} terrain tiles · ${number(solidCount)} solid parts · ${visibleLevels(state.floorValues, state.floor, $('floor-mode').value).length} floors · ${radius * 2} × ${radius * 2} tile region`;
    updateCoordinates();
    drawMinimap();
}

function updateCoordinates() {
    $('coordinates').textContent = `X ${camera.target[0].toFixed(1)} / Y ${camera.target[1].toFixed(1)}`;
}
function focusAt(position, reset = false, target, level = position[2]) {
    const z = floorKey(level ?? state.floor);
    if (state.floorValues.includes(z) && z !== state.floor) {
        state.floor = z;
        $('floor').value = String(z);
        buildMinimap();
    }
    camera.target = target || [position[0], position[1], floorHeight(state.scene, state.floor) + 0.6];
    if (reset) { camera.yaw = -Math.PI / 3; camera.pitch = 0.8; camera.distance = state.radius * 1.55; }
    rebuild();
}
function home() { focusAt(state.scene.map?.defaultFocus || [0, 0, state.floor], true); }

function buildMinimap() {
    const base = state.minimapBase;
    base.width = $('minimap').width; base.height = $('minimap').height;
    const ctx = base.getContext('2d');
    ctx.fillStyle = '#101b1e'; ctx.fillRect(0, 0, base.width, base.height);
    const bounds = state.scene.map?.bounds || [-100, -100, 100, 100];
    const width = Math.max(1, bounds[2] - bounds[0]), height = Math.max(1, bounds[3] - bounds[1]);
    const scale = Math.min((base.width - 20) / width, (base.height - 20) / height);
    const x0 = (base.width - width * scale) / 2, y0 = (base.height - height * scale) / 2;
    state.miniTransform = {scale, x0, y0, bounds};
    for (const tile of state.scene.tiles || []) {
        if (floorKey(tileLevel(tile)) !== state.floor) continue;
        ctx.fillStyle = tileColor(tile);
        ctx.globalAlpha = 0.42;
        ctx.fillRect(x0 + (tile.x - bounds[0]) * scale, y0 + (bounds[3] - tile.y - 1) * scale,
            Math.max(1, scale), Math.max(1, scale));
    }
    ctx.globalAlpha = 1;
    for (const entity of state.entities) {
        if (floorKey(entityLevel(entity)) !== state.floor) continue;
        ctx.fillStyle = entity._kind === 'unmapped' ? '#82928755' : entity._kind === 'exact' ? '#D4DBC5' : '#C2A878';
        ctx.fillRect(x0 + (entity.position[0] - bounds[0]) * scale - 0.5, y0 + (bounds[3] - entity.position[1]) * scale - 0.5,
            Math.max(1, scale * 0.65), Math.max(1, scale * 0.65));
    }
    drawMinimap();
}
function drawMinimap() {
    if (!state.miniTransform) return;
    const canvas = $('minimap'), ctx = canvas.getContext('2d');
    ctx.drawImage(state.minimapBase, 0, 0);
    const {scale, x0, y0, bounds} = state.miniTransform;
    const x = x0 + (camera.target[0] - bounds[0]) * scale, y = y0 + (bounds[3] - camera.target[1]) * scale;
    const radius = state.radius * scale;
    ctx.fillStyle = '#E7C48C15'; ctx.strokeStyle = '#E7C48C'; ctx.lineWidth = 2;
    ctx.fillRect(x - radius, y - radius, radius * 2, radius * 2);
    ctx.strokeRect(x - radius, y - radius, radius * 2, radius * 2);
    ctx.beginPath(); ctx.moveTo(x - 7, y); ctx.lineTo(x + 7, y); ctx.moveTo(x, y - 7); ctx.lineTo(x, y + 7); ctx.stroke();
}

function chooseEntity(entity) {
    state.selected = entity;
    state.renderer.selected = entity?._pickId || 0;
    state.needsDraw = true;
    $('empty-inspector').hidden = !!entity;
    $('selection').hidden = !entity;
    if (!entity) { state.referenceToken++; return; }
    const model = state.models.get(entity.modelId);
    const kind = entity._kind;
    $('selection-status').textContent = kind === 'exact' ? `${model?.status || 'draft'} / authored` : kind === 'inherited' ? 'DRAFT / INHERITED CANDIDATE' : entity.unsupportedState ? 'UNRESOLVED APPEARANCE' : 'MISSING ART';
    $('selection-title').textContent = entity.zStair ? 'Inter-floor stairs' : model?.label || (entity.unsupportedState ? 'Unresolved object state' : 'Unmodeled object');
    $('selection-prototype').textContent = entity.prototype;
    $('selection-id').textContent = `${entity.id} · level ${entityLevel(entity)}`;
    $('selection-position').textContent = entity.position.map(value => Number(value).toFixed(2)).join(', ');
    $('selection-model').textContent = model?.id || entity.modelId || (entity.unsupportedState ? 'No model selected for this state' : 'No authored model');
    $('selection-parts').textContent = model ? String((state.scene.geometryVariants?.[entity.geometryKey] || model.parts)?.length || 0) : 'Placeholder only';
    let note = kind === 'exact' ? 'Draft geometry linked to this prototype. Silhouette, dimensions, and gameplay states still need review.' :
        kind === 'inherited' ? 'Model selected through prototype inheritance. This is a candidate, not a verified visual match.' :
            'The muted marker locates an object with no assigned model. Its shape and size do not represent the source sprite.';
    if ((entity.spriteOverride && !entity.spriteState && !entity.reagentTankPose) || (entity.unappliedStateComponents ?? entity.stateOverrideComponents)?.length)
        note += ' Saved sprite or state overrides are present and are not applied to this model.';
    if (entity.zStair) note = 'Continuous stair flight fitted between the two landings. The camera preserves the game’s stair crossing and jump height.';
    if (entity.spriteState) note += ` Source state: ${entity.spriteState}; saved study frame ${(entity.spriteFrame ?? 0) + 1}.`;
    if (entity.reagentTankPose) note += ' Tank layers show their serialized/default snapshot before the live solution visualizer supplies Fill color and visibility.';
    if (entity.floorCladding) note += ` Wheels rest on floor grate ${entity.floorCladding.entity} at ${entity.floorCladding.height.toFixed(3)} tiles.`;
    if (entity.doorState) note += ` Door pose: ${entity.doorState}.`;
    if (typeof entity.folded === 'boolean') note += ` Fold pose: ${entity.folded ? 'folded' : 'unfolded'}.`;
    if (entity.randomSpriteState) note += ` Selected random sprite: ${entity.randomSpriteState}.`;
    if (entity.unsupportedState) note += ` ${entity.unsupportedState}. An amber marker shows its saved position.`;
    if (entity.support)
        note += ` Rests on support entity ${entity.support.entity} at ${entity.support.height.toFixed(2)} tiles.`;
    else if (model?.placement === 'surface')
        note += ' No exact authored support beneath this prop; it remains at floor height.';
    if (entity.connectionMask !== undefined)
        note += ` Connected edges: ${[[1,'N'],[2,'S'],[4,'E'],[8,'W']].filter(([flag]) => entity.connectionMask & flag).map(([,name]) => name).join(', ') || 'isolated'}.`;
    if (entity.layoutAlignment) note += ` Aligned to ${entity.layoutAlignment}.`;
    $('selection-note').textContent = note;
    $('selection-animation').hidden = !model?.spriteStates && !model?.reagentTankAppearance && !model?.chargerAppearance && !model?.foamAppearance && !model?.solutionAppearance;
    $('selection-animation').textContent = model?.reagentTankAppearance ? 'Compare tank Fill appearance' : 'Compare source states';
    if (model?.spriteStates || model?.chargerAppearance || model?.foamAppearance || model?.solutionAppearance)
        $('selection-animation').href = `./sprite-animation.html?model=${encodeURIComponent(model.id)}`;
    else if (model?.reagentTankAppearance)
        $('selection-animation').href = `./reagent-tank.html?model=${encodeURIComponent(model.id)}`;
    loadReference(entity, model);
}

function resourceUrl(path) {
    path = String(path).replaceAll('\\', '/').replace(/^\/+/, '');
    if (!path.startsWith('Textures/')) path = `Textures/${path}`;
    return `/resources/${path.split('/').map(encodeURIComponent).join('/')}`;
}
async function imageAt(url) {
    const image = new Image(); image.src = url; await image.decode(); return image;
}
async function loadReference(entity, model) {
    const token = ++state.referenceToken;
    const canvas = $('reference-image'), ctx = canvas.getContext('2d');
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    $('reference-empty').hidden = false;
    $('reference-caption-detail').textContent = 'Map direction · uncomposited';
    if (model?.reagentTankReferences && entity.reagentTankPose) {
        try {
            const images = Object.fromEntries(await Promise.all(Object.entries(model.reagentTankReferences)
                .map(async ([name,url]) => [name,await imageAt(new URL(url,document.baseURI).href)])));
            if (token !== state.referenceToken) return;
            const pose=entity.reagentTankPose, rgba=color(pose.color), overall=color(pose.spriteTint);
            const image=tankReferenceCanvas(images,{...pose,color:pose.color.slice(0,7),alpha:rgba[3],
                spriteTint:pose.spriteTint.slice(0,7),spriteAlpha:overall[3]});
            const scale=Math.min(4,(canvas.width-28)/32,(canvas.height-22)/32);
            ctx.imageSmoothingEnabled=false;ctx.drawImage(image,(canvas.width-32*scale)/2,(canvas.height-32*scale)/2,32*scale,32*scale);
            $('reference-empty').hidden=true;$('reference-caption-detail').textContent='Four source layers · saved/default snapshot';
        } catch { if(token===state.referenceToken)$('reference-caption-detail').textContent='Tank source layers unavailable'; }
        return;
    }
    if (entity.unsupportedState?.startsWith('Random sprite') || entity.unsupportedState?.startsWith('Unsupported random sprite')) {
        $('reference-caption-detail').textContent = 'Random appearance unresolved · no default frame assumed';
        return;
    }
    const source = state.scene.sourceReferences?.[entity.prototype] || entity.source || {};
    const sprite = {...source.sprite, ...entity.spriteOverride};
    if (entity.cornerStates?.length === 4 && entity.cornerStateBase && sprite.sprite && !entity.spriteOverride) {
        try {
            const response = await fetch(resourceUrl(`${sprite.sprite}/meta.json`));
            if (!response.ok) throw new Error('Missing corner metadata');
            const meta = await response.json();
            const names = entity.cornerStates.map(value => `${entity.cornerStateBase}${value}`);
            const images = await Promise.all(names.map(name => imageAt(resourceUrl(`${sprite.sprite}/${name}.png`))));
            if (token !== state.referenceToken) return;
            const width = meta.size.x, height = meta.size.y;
            const scale = Math.min(4, (canvas.width - 28) / width, (canvas.height - 22) / height);
            const directions = [0, Math.PI / 2, Math.PI, -Math.PI / 2];
            ctx.imageSmoothingEnabled = false;
            images.forEach((image, i) => {
                const [sx, sy, sw, sh] = referenceFrame(meta, names[i], directions[i], image.width);
                ctx.drawImage(image, sx, sy, sw, sh, (canvas.width - width * scale) / 2,
                    (canvas.height - height * scale) / 2, width * scale, height * scale);
            });
            $('reference-empty').hidden = true;
            $('reference-caption-detail').textContent = 'Source corners · saved neighbours';
        } catch {
            if (token === state.referenceToken) $('reference-caption-detail').textContent = 'Corner source unavailable';
        }
        return;
    }
    const layers = sprite.layers || [];
    const candidates = [...(layers.length ? layers.filter(layer => layer && layer.visible !== false)
        .map(layer => ({...sprite, ...layer, sprite: layer.rsi || layer.sprite || sprite.sprite})) : [sprite]), source.icon || {}];
    const attempts = [];
    if (model?.foamStateReferences && entity.foamPose) {
        const pose = entity.foamPose, url = model.foamStateReferences[`edges-${pose.edgeMask}`]?.[0];
        const tint = color(pose.spriteTint); tint[3] /= .8;
        if (url) attempts.push({url:new URL(url, document.baseURI).href, tint,
            caption:'Foam layers · saved neighbours'});
    }
    if (model?.solutionStateReferences && entity.solutionPose) {
        const pose = entity.solutionPose;
        const same = (a,b) => a.role===b.role && a.rsi===b.rsi && a.state===b.state && a.visible===b.visible &&
            color(a.color).every((v,i)=>Math.abs(v-color(b.color)[i])<1e-6);
        const match = Object.entries(model.solutionStates).find(([,s])=>s.layers.length===pose.layers.length &&
            s.layers.every((layer,i)=>same(layer,pose.layers[i])));
        const url = match && model.solutionStateReferences[match[0]]?.[0];
        if (url) attempts.push({url:new URL(url, document.baseURI).href, tint:color(pose.spriteTint),
            caption:`Source vessel and fill · ${pose.volume}/${pose.maxVolume}`});
    }
    if (model?.chargerStateReferences && entity.chargerPose) {
        const pose = entity.chargerPose;
        const suffix = ['recharger-taser', 'recharger-baton'].filter(s => pose.inserted.includes(s)).map(s => s.replace('recharger-', '')).join('-') || 'empty';
        const url = model.chargerStateReferences[`${pose.light}--${suffix}`]?.[pose.frame];
        if (url) attempts.push({url: new URL(url, document.baseURI).href,
            caption: `${pose.light} · ${suffix} · source frame ${pose.frame + 1}`});
    }
    const spriteReference = model?.spriteStateReferences?.[entity.spriteState]?.[entity.spriteFrame ?? 0];
    if (spriteReference) attempts.push({url: new URL(spriteReference, document.baseURI).href,
        caption: `${entity.spriteState} · saved study frame ${(entity.spriteFrame ?? 0) + 1}`});
    const doorReference = doorSpriteReference(entity, model);
    if (doorReference) attempts.push({url: new URL(doorReference, document.baseURI).href,
        caption: `${entity.doorSpriteState} · source frame ${entity.doorSpriteFrame + 1}`});
    const pairedReference = barricadeReference(entity, model);
    if (pairedReference) {
        attempts.push({url: new URL(pairedReference, document.baseURI).href,
            caption: 'Body + reinforcement · damage ' + entity.barricadeDamageState +
                (entity.barricadeWired ? ' · barbed wire installed' : ' · wire absent') +
                (entity.barricadeAcidFrame === undefined ? ' · acid absent' : ' · acid frame ' + entity.barricadeAcidFrame)});
    }
    // Explicit one-direction references include the reviewed closed-lid/cap layer
    // composition. Saved overrides and smoothed states still use their map source.
    if (entity.matchKind === 'exact' && model?.referenceRsi && model.sourceDirections === 1 &&
        model.reference?.imageUrl && !entity.spriteOverride && !entity.connectionState)
        attempts.push({url: new URL(model.reference.imageUrl, document.baseURI).href,
            caption: 'Authored static source reference'});
    if (source.preview && !entity.spriteOverride) {
        const preview = source.preview;
        const spriteTint = color(preview.spriteTint || '#FFFFFF'), layerTint = color(preview.layerTint || '#FFFFFF');
        const stateName = entity.referenceState || entity.connectionState || preview.state;
        const folder = preview.resource?.slice(0, preview.resource.lastIndexOf('/'));
        attempts.push({url: folder ? resourceUrl(`${folder}/${stateName}.png`) : preview.url,
            meta: folder ? resourceUrl(`${folder}/meta.json`) : null, state: stateName,
            size: preview.frameSize, tint: spriteTint.map((value, i) => value * layerTint[i])});
    }
    for (const candidate of candidates) {
        if (candidate.texture) attempts.push({url: resourceUrl(candidate.texture)});
        const rsi = candidate.rsi || candidate.sprite;
        const candidateState = entity.referenceState || candidate.state;
        if (rsi && typeof candidateState === 'string')
            attempts.push({url: resourceUrl(`${rsi}/${candidateState}.png`), meta: resourceUrl(`${rsi}/meta.json`), state: candidateState});
    }
    if (model?.reference?.imageUrl) attempts.push({url: new URL(model.reference.imageUrl, document.baseURI).href});
    for (const attempt of attempts) {
        try {
            const image = await imageAt(attempt.url);
            let width = attempt.size?.[0] || image.width, height = attempt.size?.[1] || image.height;
            let sx = 0, sy = 0;
            if (attempt.meta) {
                const response = await fetch(attempt.meta);
                if (!response.ok) continue;
                const meta = await response.json();
                [sx,sy,width,height] = referenceFrame(meta, attempt.state, entity.yaw || 0, image.width);
            }
            if (token !== state.referenceToken) return;
            const scale = Math.min(4, (canvas.width - 28) / width, (canvas.height - 22) / height);
            ctx.imageSmoothingEnabled = false;
            ctx.drawImage(image, sx, sy, width, height, (canvas.width - width * scale) / 2,
                (canvas.height - height * scale) / 2, width * scale, height * scale);
            if (attempt.tint) {
                const pixels = ctx.getImageData(0, 0, canvas.width, canvas.height);
                for (let i = 0; i < pixels.data.length; i += 4)
                    for (let channel = 0; channel < 4; channel++) pixels.data[i + channel] *= attempt.tint[channel];
                ctx.putImageData(pixels, 0, 0);
            }
            $('reference-empty').hidden = true;
            $('reference-caption-detail').textContent = attempt.caption || 'Map direction · uncomposited';
            return;
        } catch { /* Try the next explicit source, then leave the honest missing-reference label. */ }
    }
}

async function loadScene() {
    $('loading').hidden = false; $('error').hidden = true;
    try {
        const requested = new URLSearchParams(location.search).get('scene') || '../generated/redux-scene.json';
        const sceneUrl = new URL(requested, document.baseURI);
        if (sceneUrl.origin !== location.origin) throw new Error('The review scene must be served by this local server.');
        const response = await fetch(sceneUrl);
        if (!response.ok) throw new Error(`Scene request returned HTTP ${response.status}. Generate ${sceneUrl.pathname} first.`);
        const scene = await response.json();
        for (const reference of scene.sceneChunks || []) {
            const chunkUrl = new URL(reference, sceneUrl);
            if (chunkUrl.origin !== location.origin) throw new Error('Scene chunks must be local.');
            const chunkResponse = await fetch(chunkUrl);
            if (!chunkResponse.ok) throw new Error(`Scene chunk returned HTTP ${chunkResponse.status}.`);
            const compressed = chunkUrl.pathname.endsWith('.gz') && !chunkResponse.headers.get('Content-Encoding')?.includes('gzip');
            const chunk = await (compressed ? new Response(chunkResponse.body.pipeThrough(new DecompressionStream('gzip'))) : chunkResponse).json();
            for (const [name, value] of Object.entries(chunk)) {
                if (!['instances', 'tiles', 'geometryVariants'].includes(name)) throw new Error('Unsupported scene chunk.');
                if (Array.isArray(value)) for (const item of value) scene[name].push(item);
                else Object.assign(scene[name], value);
            }
        }
        if (!Array.isArray(scene.instances) || !Array.isArray(scene.tiles)) throw new Error('The scene needs instances and tiles arrays.');
        state.scene = scene;
        let models = scene.models;
        if (!models) {
            const modelResponse = await fetch(new URL(scene.modelsUrl || '../generated/models.json', document.baseURI));
            if (!modelResponse.ok) throw new Error(`Model catalog returned HTTP ${modelResponse.status}.`);
            models = (await modelResponse.json()).models;
        }
        const entries = Array.isArray(models) ? models : Object.entries(models || {}).map(([id, model]) => ({id, ...model}));
        state.models = new Map(entries.map(model => [model.id, model]));
        state.entities = scene.instances.filter(entity => Array.isArray(entity.position) && entity.position.length === 3 && entity.position.every(Number.isFinite))
            .map((entity, index) => ({...entity, _pickId: index + 1,
                _kind: state.models.has(entity.modelId) ? (entity.matchKind === 'exact' ? 'exact' : 'inherited') : 'unmapped'}));
        if (state.entities.length >= 16777215) throw new Error('The scene exceeds the entity picking capacity. Export a smaller region.');
        $('loading-detail').textContent = `Indexing ${number(state.entities.length)} objects and ${number(scene.tiles.length)} terrain tiles…`;
        await new Promise(resolve => requestAnimationFrame(resolve));
        indexItems(state.entities, state.entityIndex, entity => [entity.position[0], entity.position[1], entityLevel(entity)]);
        indexItems(scene.tiles, state.tileIndex, tile => [tile.x, tile.y, tileLevel(tile)]);
        const floors = new Set(scene.tiles.map(tile => floorKey(tileLevel(tile))));
        if (!floors.size) for (const entity of state.entities) floors.add(floorKey(entityLevel(entity)));
        if (!floors.size) floors.add(0);
        state.floorValues = [...floors].sort((a, b) => a - b);
        const preferredFloor = floorKey(scene.map?.defaultFocus?.[2]);
        state.floor = state.floorValues.includes(preferredFloor) ? preferredFloor : state.floorValues[0];
        $('floor').replaceChildren(...state.floorValues.map(z => {
            const option = document.createElement('option'); option.value = z;
            option.textContent = scene.map?.levels?.find(level => level.z === z)?.label || (z === 0 ? 'Ground level' : `Elevation ${z}`); return option;
        }));
        $('floor').value = String(state.floor);
        $('variant-label').textContent = `${scene.map?.variant || 'Classic'} / ${number(state.entities.length)} objects`;
        const poseReview = Boolean(scene.diagnostics?.reviewFixture);
        $('map-title').textContent = poseReview ? (scene.map?.name || 'Pose comparison') :
            /redux/i.test(scene.map?.variant || '') ? 'Garrison Redux' : 'Stable Garrison';
        $('scene-description').textContent = poseReview ?
            'Separate model poses for comparison. Positions in this scene are test placements.' :
            'The saved map, assembled in 3D. Authored drafts and missing art shown separately.';
        $('placement-label').textContent = poseReview ? 'ISOLATED POSE COMPARISON' : 'ACTUAL SAVED PLACEMENT';
        state.renderer?.dispose();
        state.renderer = new Renderer(canvas);
        $('loading-detail').textContent = 'Loading original floor materials…';
        await state.renderer.terrain.load(scene.tilePalette || {});
        await state.renderer.loadSurfaces();
        for (const model of state.models.values()) for (const part of model.parts) {
            if (part.surface && !state.renderer.surfaces.has(part.surface))
                throw new Error(`Missing source artwork ${part.surface}; regenerate the model and surface exports.`);
        }
        chooseEntity(null);
        buildMinimap(); home();
        $('loading').hidden = true;
    } catch (error) {
        $('loading').hidden = true; $('error').hidden = false;
        $('error-message').textContent = error.message || String(error);
        console.error(error);
    }
}

let drag = null;
canvas.addEventListener('contextmenu', event => event.preventDefault());
canvas.addEventListener('pointerdown', event => {
    if (!state.renderer) return;
    if (event.button !== 0 && event.button !== 2 && event.button !== 1) return;
    canvas.focus(); canvas.setPointerCapture(event.pointerId);
    drag = {x: event.clientX, y: event.clientY, startX: event.clientX, startY: event.clientY,
        pan: event.button !== 0 || event.shiftKey, moved: false, pointer: event.pointerId};
});
canvas.addEventListener('pointermove', event => {
    if (!drag || drag.pointer !== event.pointerId) return;
    const dx = event.clientX - drag.x, dy = event.clientY - drag.y;
    if (Math.hypot(event.clientX - drag.startX, event.clientY - drag.startY) > 3) drag.moved = true;
    if (drag.pan) {
        camera.pan(dx, dy, canvas.clientHeight);
        updateCoordinates(); drawMinimap();
        if (!state.lastRegion || Math.hypot(camera.target[0] - state.lastRegion[0], camera.target[1] - state.lastRegion[1]) > 2) requestRebuild();
    } else camera.orbit(dx, dy);
    drag.x = event.clientX; drag.y = event.clientY;
    state.needsDraw = true;
});
function endDrag(event, cancelled = false) {
    if (!drag || drag.pointer !== event.pointerId) return;
    if (!cancelled && !drag.moved && !drag.pan) {
        const id = state.renderer.pick(camera, event.clientX, event.clientY);
        chooseEntity(state.entities[id - 1] || null);
    }
    if (drag.pan) requestRebuild();
    drag = null;
    if (canvas.hasPointerCapture(event.pointerId)) canvas.releasePointerCapture(event.pointerId);
}
canvas.addEventListener('pointerup', event => endDrag(event));
canvas.addEventListener('pointercancel', event => endDrag(event, true));
canvas.addEventListener('wheel', event => { event.preventDefault(); camera.zoom(event.deltaY); state.needsDraw = true; }, {passive: false});
canvas.addEventListener('keydown', event => {
    if (!state.renderer || !state.scene) return;
    if (event.key.startsWith('Arrow')) {
        const amount = event.shiftKey ? 90 : 30;
        const movement = {ArrowLeft: [amount, 0], ArrowRight: [-amount, 0], ArrowUp: [0, -amount], ArrowDown: [0, amount]}[event.key];
        if (!movement) return;
        camera.pan(...movement, canvas.clientHeight);
        requestRebuild();
    }
    else if (event.key === 'Escape') chooseEntity(null);
    else if (event.key.toLowerCase() === 'h') home();
    else if (event.key === '+' || event.key === '=') camera.zoom(-100);
    else if (event.key === '-') camera.zoom(100);
    else return;
    event.preventDefault(); state.needsDraw = true;
});
$('scope').addEventListener('change', () => { state.radius = Number($('scope').value); requestRebuild(); });
$('floor').addEventListener('change', () => {
    state.floor = Number($('floor').value); camera.target[2] = floorHeight(state.scene, state.floor) + 0.6;
    chooseEntity(null); buildMinimap(); requestRebuild();
});
for (const id of ['show-exact', 'show-inherited', 'show-unmapped', 'cutaway', 'floor-mode']) $(id).addEventListener('change', requestRebuild);
$('wall-height').addEventListener('input', () => { $('wall-height-value').value = `${Number($('wall-height').value).toFixed(1)} tiles`; requestRebuild(); });
$('home').addEventListener('click', home);
$('reset-camera').addEventListener('click', () => {
    camera.yaw = -Math.PI / 3; camera.pitch = 0.8; camera.distance = state.radius * 1.55; state.needsDraw = true;
});
$('top-view').addEventListener('click', () => { camera.pitch = 1.53; camera.yaw = -Math.PI / 2; state.needsDraw = true; });
$('toggle-panels').addEventListener('click', () => {
    const hidden = $('workspace').classList.toggle('panels-hidden');
    $('toggle-panels').textContent = hidden ? 'Show panels' : 'Hide panels';
    $('toggle-panels').setAttribute('aria-pressed', String(hidden));
});
$('clear-selection').addEventListener('click', () => chooseEntity(null));
$('focus-selection').addEventListener('click', () => {
    if (!state.selected) return;
    const entity = state.selected, model = state.models.get(entity.modelId);
    const parts = state.scene.geometryVariants?.[entity.geometryKey] || model?.parts;
    const height = $('cutaway').checked && (structural(entity) || model?.wallMounted) ? Number($('wall-height').value) : undefined;
    focusAt(entity.position, false, entityFocusPoint(entity, parts, height), entityLevel(entity));
    camera.distance = 7; state.needsDraw = true;
});
$('minimap').addEventListener('click', event => {
    if (!state.miniTransform) return;
    const rect = $('minimap').getBoundingClientRect(), {scale, x0, y0, bounds} = state.miniTransform;
    const px = (event.clientX - rect.left) * $('minimap').width / rect.width;
    const py = (event.clientY - rect.top) * $('minimap').height / rect.height;
    focusAt([clamp(bounds[0] + (px - x0) / scale, bounds[0], bounds[2]),
        clamp(bounds[3] - (py - y0) / scale, bounds[1], bounds[3]), state.floor]);
});
let lastSearch = '', searchIndex = -1;
function findEntity() {
    if (!state.scene) return;
    const query = $('search').value.trim().toLowerCase();
    if (!query) { $('search-status').textContent = 'Enter a prototype name or map entity ID.'; return; }
    if (query !== lastSearch) searchIndex = -1;
    lastSearch = query;
    const matches = state.entities.filter(entity => entity.prototype.toLowerCase().includes(query) || String(entity.id) === query || entity.sceneKey === query ||
        state.models.get(entity.modelId)?.label?.toLowerCase().includes(query));
    if (!matches.length) { $('search-status').textContent = 'No matching objects in this saved scene.'; return; }
    searchIndex = (searchIndex + 1) % matches.length;
    const entity = matches[searchIndex];
    $(`show-${entity._kind}`).checked = true;
    focusAt(entity.position, false, undefined, entityLevel(entity)); chooseEntity(entity);
    $('search-status').textContent = `${number(searchIndex + 1)} of ${number(matches.length)} matches. Find again for the next.`;
}
$('find').addEventListener('click', findEntity);
$('search').addEventListener('keydown', event => { if (event.key === 'Enter') findEntity(); });
$('retry').addEventListener('click', loadScene);
new ResizeObserver(() => { state.needsDraw = true; }).observe(canvas);
canvas.addEventListener('webglcontextlost', event => {
    event.preventDefault(); $('error').hidden = false;
    $('error-message').textContent = 'The graphics context was interrupted. Reload the page to restore the scene.';
});
canvas.addEventListener('webglcontextrestored', () => location.reload());
window.addEventListener('pagehide', () => state.renderer?.dispose());

function frame() {
    if (state.renderer && state.needsDraw && !state.renderer.gl.isContextLost()) {
        state.needsDraw = false;
        try { state.renderer.draw(camera); } catch (error) { $('error').hidden = false; $('error-message').textContent = error.message; }
    }
    requestAnimationFrame(frame);
}
loadScene(); frame();
