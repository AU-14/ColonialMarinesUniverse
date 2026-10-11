import {color} from './math.js';
import {Terrain} from './terrain.js';
import {ellipsoidGeometry, cylinderGeometry, wedgeGeometry, slantedGeometry, foliageGeometry} from './primitives.js';

const vertexSource = `#version 300 es
precision highp float;
layout(location=0) in vec3 aPosition;
layout(location=1) in vec3 aNormal;
layout(location=2) in vec3 iCenter;
layout(location=3) in vec3 iSize;
layout(location=4) in vec2 iRotation;
layout(location=5) in vec4 iColor;
layout(location=6) in float iPickId;
layout(location=7) in vec4 iSurfaceRect;
layout(location=8) in float iSurfaceAxis;
layout(location=9) in float iSurfaceVScale;
layout(location=10) in vec2 iPitch;
uniform mat4 uViewProjection;
out vec3 vNormal;
out vec3 vWorld;
out vec4 vColor;
flat out uint vPickId;
flat out vec4 vSurfaceRect;
out vec2 vSurfaceUV;
void main() {
    mat2 rotation = mat2(iRotation.x, iRotation.y, -iRotation.y, iRotation.x);
    vec3 local = aPosition * iSize;
    local.xz = mat2(iPitch.x, iPitch.y, -iPitch.y, iPitch.x) * local.xz;
    local.xy = rotation * local.xy;
    vWorld = local + iCenter;
    vec3 normal = normalize(aNormal / iSize);
    normal.xz = mat2(iPitch.x, iPitch.y, -iPitch.y, iPitch.x) * normal.xz;
    vNormal = vec3(rotation * normal.xy, normal.z);
    vColor = iColor;
    vPickId = uint(iPickId + 0.5);
    vSurfaceRect = iSurfaceRect;
    float axis = mod(iSurfaceAxis, 4.0);
    vSurfaceUV = vec2(aPosition.x + 0.5, 1.0 - (aPosition.z + 0.5) * iSurfaceVScale);
    if (axis > 1.5 && axis < 2.5) vSurfaceUV = vec2(aPosition.x + 0.5, 0.5 - aPosition.y);
    if (axis > 2.5) vSurfaceUV.x = 0.5 - aPosition.y;
    if (iSurfaceAxis > 3.5) vSurfaceUV.x = 1.0 - vSurfaceUV.x;
    gl_Position = uViewProjection * vec4(vWorld, 1.0);
}`;

const fragmentSource = `#version 300 es
precision highp float;
precision highp int;
in vec3 vNormal;
in vec3 vWorld;
in vec4 vColor;
flat in uint vPickId;
flat in vec4 vSurfaceRect;
in vec2 vSurfaceUV;
uniform sampler2D uSurfaces;
uniform vec2 uSurfaceSize;
uniform uint uSelected;
uniform vec3 uEye;
uniform bool uPicking;
out vec4 outputColor;
void main() {
    vec4 surfaceColor = vec4(1.0);
    if (vSurfaceRect.z > 0.0) {
        vec2 pixel = clamp(floor(vSurfaceUV * vSurfaceRect.zw), vec2(0.0), vSurfaceRect.zw - 1.0);
        surfaceColor = texture(uSurfaces, (vSurfaceRect.xy + pixel + 0.5) / uSurfaceSize);
        if (surfaceColor.a < 0.5) discard; // Cutouts also pass picking through to geometry behind.
    }
    // Screen-door transparency preserves real depth for glass and missing-art markers.
    // It avoids ordering-dependent alpha blending across intersecting instanced cuboids.
    const float pattern[16] = float[](0., 8., 2., 10., 12., 4., 14., 6., 3., 11., 1., 9., 15., 7., 13., 5.);
    ivec2 cell = ivec2(gl_FragCoord.xy) % 4;
    float alpha = vColor.a * surfaceColor.a;
    if (alpha <= 0.0 || (!uPicking && alpha < (pattern[cell.y * 4 + cell.x] + 0.5) / 16.0)) discard;
    if (uPicking) {
        outputColor = vec4(float(vPickId & 255u), float((vPickId >> 8u) & 255u), float((vPickId >> 16u) & 255u), 255.) / 255.;
        return;
    }
    vec3 light = normalize(vec3(-0.4, -0.6, 1.0));
    float lighting = 0.52 + 0.48 * max(0.0, dot(normalize(vNormal), light));
    vec3 shaded = vColor.rgb * surfaceColor.rgb * lighting;
    if (vPickId == uSelected && uSelected != 0u) shaded = mix(shaded, vec3(1.0, 0.68, 0.28), 0.6);
    float fog = smoothstep(90., 260., distance(uEye, vWorld));
    outputColor = vec4(mix(shaded, vec3(0.052, 0.075, 0.084), fog * 0.8), 1.0);
}`;

function shader(gl, type, source) {
    const result = gl.createShader(type);
    gl.shaderSource(result, source);
    gl.compileShader(result);
    if (!gl.getShaderParameter(result, gl.COMPILE_STATUS)) {
        const error = gl.getShaderInfoLog(result);
        gl.deleteShader(result);
        throw new Error(`WebGL shader compilation failed: ${error}`);
    }
    return result;
}

function cube() {
    const output = [];
    const faces = [
        [[1, 0, 0], [0, 1, 0], [0, 0, 1]], [[-1, 0, 0], [0, -1, 0], [0, 0, 1]],
        [[0, 1, 0], [0, 0, 1], [1, 0, 0]], [[0, -1, 0], [0, 0, -1], [1, 0, 0]],
        [[0, 0, 1], [1, 0, 0], [0, 1, 0]], [[0, 0, -1], [-1, 0, 0], [0, 1, 0]],
    ];
    for (const [normal, tangent, bitangent] of faces) {
        const corners = [[-1, -1], [1, -1], [1, 1], [-1, 1]].map(([u, v]) =>
            normal.map((n, i) => (n + u * tangent[i] + v * bitangent[i]) * 0.5));
        for (const index of [0, 1, 2, 0, 2, 3]) output.push(...corners[index], ...normal);
    }
    return new Float32Array(output);
}

// Transform, color, picking, atlas rectangle, projection, cutaway UV scale and local tilt.
export const INSTANCE_STRIDE = 21;

export function appendBox(target, min, max, material, position, yaw, pickId, options = {}) {
    const vector = value => Array.isArray(value) && value.length === 3 && value.every(Number.isFinite);
    if (!vector(min) || !vector(max) || !vector(position) || !Number.isFinite(yaw) ||
        !Number.isSafeInteger(pickId) || pickId < 0 || pickId > 16777215 ||
        (options.maxHeight !== undefined && !Number.isFinite(options.maxHeight)) ||
        (options.partYaw !== undefined && (!Number.isFinite(options.partYaw) || Math.abs(options.partYaw) > 360)) ||
        (options.partPitch !== undefined && (!Number.isFinite(options.partPitch) || Math.abs(options.partPitch) > 90 || (options.partPitch !== 0 && options.surface))) ||
        (options.alpha !== undefined && (!Number.isFinite(options.alpha) || options.alpha < 0 || options.alpha > 1))) return;
    if (options.surface && (!Array.isArray(options.surface.rect) || options.surface.rect.length !== 4 ||
        !options.surface.rect.every(Number.isFinite) || options.surface.rect.some((v,i) => i < 2 ? v < 0 : v <= 0) ||
        !['XZ','XY','YZ'].includes(options.surfaceAxis || 'XZ'))) return;
    const low = [...min], high = [...max];
    if (options.maxHeight !== undefined) high[2] = Math.min(high[2], options.maxHeight);
    const size = high.map((value, i) => value - low[i]);
    if (size.some(value => value <= 0)) return;
    const center = low.map((value, i) => (value + high[i]) / 2);
    const c = Math.cos(yaw), s = Math.sin(yaw);
    const rotation = yaw + (options.partYaw || 0) * Math.PI / 180;
    const pitch = (options.partPitch || 0) * Math.PI / 180;
    const tint = color(material, options.alpha ?? 1);
    target.push(position[0] + center[0] * c - center[1] * s, position[1] + center[0] * s + center[1] * c,
        position[2] + center[2], ...size, Math.cos(rotation), Math.sin(rotation), ...tint, pickId,
        ...(options.surface?.rect || [0,0,0,0]), {XZ:1,XY:2,YZ:3}[options.surfaceAxis || 'XZ'] + (options.surfaceFlipU ? 4 : 0),
        size[2] / (max[2] - min[2]), Math.cos(pitch), Math.sin(pitch));
}

export class Renderer {
    constructor(canvas) {
        this.canvas = canvas;
        const gl = this.gl = canvas.getContext('webgl2', {antialias: true, alpha: false, preserveDrawingBuffer: false});
        if (!gl) throw new Error('This browser does not provide WebGL 2. Enable hardware acceleration or use a browser with WebGL 2 support.');
        const program = this.program = gl.createProgram();
        const vertex = shader(gl, gl.VERTEX_SHADER, vertexSource);
        const fragment = shader(gl, gl.FRAGMENT_SHADER, fragmentSource);
        gl.attachShader(program, vertex);
        gl.attachShader(program, fragment);
        gl.linkProgram(program);
        gl.deleteShader(vertex);
        gl.deleteShader(fragment);
        if (!gl.getProgramParameter(program, gl.LINK_STATUS)) throw new Error(gl.getProgramInfoLog(program));
        this.uniforms = Object.fromEntries(['uViewProjection', 'uSelected', 'uEye', 'uPicking', 'uSurfaces', 'uSurfaceSize'].map(name => [name, gl.getUniformLocation(program, name)]));
        this.batches = [cube(), ellipsoidGeometry(), ...['X','Y','Z'].map(cylinderGeometry), wedgeGeometry(), wedgeGeometry(true),
            slantedGeometry('X'), slantedGeometry('X',true), slantedGeometry('Y'), slantedGeometry('Y',true), foliageGeometry()].map(geometry => this.createBatch(geometry));
        this.pickFramebuffer = gl.createFramebuffer();
        this.pickColor = gl.createTexture();
        this.pickDepth = gl.createRenderbuffer();
        this.count = 0;
        this.selected = 0;
        this.width = 0;
        this.height = 0;
        gl.enable(gl.DEPTH_TEST);
        gl.depthFunc(gl.LEQUAL);
        gl.enable(gl.CULL_FACE);
        gl.cullFace(gl.BACK);
        gl.clearColor(0.052, 0.075, 0.084, 1);
        this.terrain = new Terrain(gl);
        this.surfaces = new Map();
        this.surfaceSize = [1,1];
        this.surfaceTexture = gl.createTexture();
        gl.bindTexture(gl.TEXTURE_2D, this.surfaceTexture);
        gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,1,1,0,gl.RGBA,gl.UNSIGNED_BYTE,new Uint8Array([255,255,255,255]));
        gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MIN_FILTER,gl.NEAREST);
        gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MAG_FILTER,gl.NEAREST);
        gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_S,gl.CLAMP_TO_EDGE);
        gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_T,gl.CLAMP_TO_EDGE);
    }
    async loadSurfaces(url = '../generated/surfaces.json') {
        const response = await fetch(url);
        if (!response.ok) throw new Error(`Surface catalog request failed: ${response.status}`);
        const data = await response.json();
        this.surfaces = new Map(Object.entries(data.surfaces));
        if (!this.surfaces.size) return;
        const image = new Image();
        image.src = new URL(data.imageUrl, document.baseURI).href;
        await image.decode();
        const gl = this.gl;
        if (image.width > gl.getParameter(gl.MAX_TEXTURE_SIZE) || image.height > gl.getParameter(gl.MAX_TEXTURE_SIZE))
            throw new Error('The surface atlas exceeds this device’s texture limit.');
        gl.bindTexture(gl.TEXTURE_2D, this.surfaceTexture);
        gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, false);
        gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,gl.RGBA,gl.UNSIGNED_BYTE,image);
        this.surfaceSize = [image.width,image.height];
    }
    createBatch(vertices) {
        const gl = this.gl;
        const batch = {vao: gl.createVertexArray(), geometry: gl.createBuffer(), instances: gl.createBuffer(), vertices: vertices.length / 6, count: 0};
        const {vao, geometry, instances} = batch;
        gl.bindVertexArray(vao);
        gl.bindBuffer(gl.ARRAY_BUFFER, geometry);
        gl.bufferData(gl.ARRAY_BUFFER, vertices, gl.STATIC_DRAW);
        gl.enableVertexAttribArray(0);
        gl.vertexAttribPointer(0, 3, gl.FLOAT, false, 24, 0);
        gl.enableVertexAttribArray(1);
        gl.vertexAttribPointer(1, 3, gl.FLOAT, false, 24, 12);
        gl.bindBuffer(gl.ARRAY_BUFFER, instances);
        let offset = 0;
        for (const [location, width] of [[2, 3], [3, 3], [4, 2], [5, 4], [6, 1], [7, 4], [8, 1], [9, 1], [10, 2]]) {
            gl.enableVertexAttribArray(location);
            gl.vertexAttribPointer(location, width, gl.FLOAT, false, INSTANCE_STRIDE * 4, offset * 4);
            gl.vertexAttribDivisor(location, 1);
            offset += width;
        }
        return batch;
    }
    setGeometry(values, rounded = [], cylinders = [[],[],[]], wedges = [], reverseWedges = [], slanted = [[],[],[],[]], foliage = []) {
        const gl = this.gl;
        const groups = [values, rounded, ...cylinders, wedges, reverseWedges, ...slanted, foliage];
        for (const [index, data] of groups.entries()) {
            const batch = this.batches[index];
            gl.bindBuffer(gl.ARRAY_BUFFER, batch.instances);
            gl.bufferData(gl.ARRAY_BUFFER, data instanceof Float32Array ? data : new Float32Array(data), gl.STATIC_DRAW);
            batch.count = data.length / INSTANCE_STRIDE;
        }
        this.count = groups.reduce((sum, data) => sum + data.length, 0) / INSTANCE_STRIDE;
    }
    resize() {
        const gl = this.gl;
        const ratio = Math.min(2, window.devicePixelRatio || 1);
        const width = Math.max(1, Math.round(this.canvas.clientWidth * ratio));
        const height = Math.max(1, Math.round(this.canvas.clientHeight * ratio));
        if (width === this.width && height === this.height) return;
        this.canvas.width = this.width = width;
        this.canvas.height = this.height = height;
        gl.bindTexture(gl.TEXTURE_2D, this.pickColor);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA8, width, height, 0, gl.RGBA, gl.UNSIGNED_BYTE, null);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
        gl.bindRenderbuffer(gl.RENDERBUFFER, this.pickDepth);
        gl.renderbufferStorage(gl.RENDERBUFFER, gl.DEPTH_COMPONENT24, width, height);
        gl.bindFramebuffer(gl.FRAMEBUFFER, this.pickFramebuffer);
        gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, this.pickColor, 0);
        gl.framebufferRenderbuffer(gl.FRAMEBUFFER, gl.DEPTH_ATTACHMENT, gl.RENDERBUFFER, this.pickDepth);
        if (gl.checkFramebufferStatus(gl.FRAMEBUFFER) !== gl.FRAMEBUFFER_COMPLETE) throw new Error('Could not allocate the picking depth buffer.');
        gl.bindFramebuffer(gl.FRAMEBUFFER, null);
    }
    draw(camera, picking = false) {
        const gl = this.gl;
        this.resize();
        gl.bindFramebuffer(gl.FRAMEBUFFER, picking ? this.pickFramebuffer : null);
        gl.viewport(0, 0, this.width, this.height);
        gl.clearColor(...(picking ? [0, 0, 0, 1] : [0.052, 0.075, 0.084, 1]));
        gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);
        this.terrain.draw(camera.matrix(this.width / this.height), picking);
        gl.useProgram(this.program);
        gl.uniformMatrix4fv(this.uniforms.uViewProjection, false, camera.matrix(this.width / this.height));
        gl.uniform3fv(this.uniforms.uEye, camera.eye);
        gl.uniform1ui(this.uniforms.uSelected, this.selected);
        gl.uniform1i(this.uniforms.uPicking, picking ? 1 : 0);
        gl.activeTexture(gl.TEXTURE1);
        gl.bindTexture(gl.TEXTURE_2D, this.surfaceTexture);
        gl.uniform1i(this.uniforms.uSurfaces, 1);
        gl.uniform2fv(this.uniforms.uSurfaceSize, this.surfaceSize);
        gl.activeTexture(gl.TEXTURE0);
        if (picking) gl.disable(gl.DITHER);
        // Printed cutouts can reveal the exit face at oblique angles.
        gl.disable(gl.CULL_FACE);
        for (const batch of this.batches) {
            gl.bindVertexArray(batch.vao);
            gl.drawArraysInstanced(gl.TRIANGLES, 0, batch.vertices, batch.count);
        }
        gl.enable(gl.CULL_FACE);
        if (picking) gl.enable(gl.DITHER);
    }
    pick(camera, x, y) {
        this.draw(camera, true);
        const pixel = new Uint8Array(4);
        const rect = this.canvas.getBoundingClientRect();
        const px = Math.min(this.width - 1, Math.max(0, Math.floor((x - rect.left) * this.width / rect.width)));
        const py = Math.min(this.height - 1, Math.max(0, this.height - 1 - Math.floor((y - rect.top) * this.height / rect.height)));
        this.gl.readPixels(px, py, 1, 1, this.gl.RGBA, this.gl.UNSIGNED_BYTE, pixel);
        this.gl.bindFramebuffer(this.gl.FRAMEBUFFER, null);
        return pixel[0] + pixel[1] * 256 + pixel[2] * 65536;
    }
    dispose() {
        this.terrain.dispose();
        const gl = this.gl;
        for (const batch of this.batches) {
            gl.deleteBuffer(batch.geometry);
            gl.deleteBuffer(batch.instances);
            gl.deleteVertexArray(batch.vao);
        }
        gl.deleteFramebuffer(this.pickFramebuffer);
        gl.deleteTexture(this.pickColor);
        gl.deleteTexture(this.surfaceTexture);
        gl.deleteRenderbuffer(this.pickDepth);
        gl.deleteProgram(this.program);
    }
}
