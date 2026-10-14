// Original tile pixels preserve room materials, borders and floor markings.
// Rotation/mirroring follows Clyde.GridRendering.WriteTileToBuffers.
export function tileUvs(rotationMirroring = 0) {
    let uv = [[0, 1], [1, 1], [1, 0], [0, 0]];
    const value = rotationMirroring & 7;
    for (let i = 0; i < value % 4; i++) uv = [uv[3], uv[0], uv[1], uv[2]];
    if (value >= 4) uv = uv.map(([u, v]) => value % 2 === 0 ? [1 - u, v] : [u, 1 - v]);
    return uv;
}

export function appendTile(vertices, tile, rect, tint, allowRotationMirror = false) {
    const c = Math.cos(tile.yaw || 0), s = Math.sin(tile.yaw || 0);
    const uv = tileUvs(allowRotationMirror ? tile.rotationMirroring : 0);
    const ramp = tile.elevationRamp;
    const height = (x,y) => (tile.z || 0) - .005 + (ramp ? ramp.bottom +
        ((x-.5)*ramp.direction[0]+(y-.5)*ramp.direction[1]+.5)*(ramp.top-ramp.bottom) : tile.elevation || 0);
    for (const [x0,y0,x1,y1] of tile.floorFragments ?? [[0,0,1,1]]) {
    const corners = [[x0, y0], [x1, y0], [x1, y1], [x0, y1]];
    for (const index of [0, 1, 2, 0, 2, 3]) {
        const [x, y] = corners[index];
        // Interpolate the original whole-tile coordinates; fragments must not stretch the artwork.
        const sampled = [0,1].map(i => uv[0][i]*(1-x)*(1-y) + uv[1][i]*x*(1-y) + uv[2][i]*x*y + uv[3][i]*(1-x)*y);
        vertices.push(tile.x + x*c-y*s, tile.y + x*s+y*c, height(x,y),
            ...(rect ? [rect[0] + sampled[0]*rect[2], rect[1] + sampled[1]*rect[3]] : [-1, -1]), ...tint.slice(0, 3));
    }
    }
    if (tile.solid || (tile.foundationDepth || .07) > .071 || ramp) {
        const bottom = (tile.z || 0) + (tile.elevation || 0) - .005 - (tile.foundationDepth || .07);
        // Close exposed rises, including the sides of clipped floor fragments.
        for (const [x0,y0,x1,y1] of tile.floorFragments ?? [[0,0,1,1]]) {
            const corners = [[x0,y0],[x1,y0],[x1,y1],[x0,y1]];
            for (let edge = 0; edge < 4; edge++) {
                const a = corners[edge], b = corners[(edge+1)%4];
                const face = [[...a,bottom],[...b,bottom],[...b,height(...b)],[...a,height(...a)]];
                for (const i of [0,1,2,0,2,3]) {
                    const [x,y,z] = face[i];
                    vertices.push(tile.x+x*c-y*s,tile.y+x*s+y*c,z,-1,-1,...tint.slice(0,3).map(v=>v*.7));
                }
            }
            for (const i of [0,2,1,0,3,2]) {
                const [x,y] = corners[i];
                vertices.push(tile.x+x*c-y*s,tile.y+x*s+y*c,bottom,-1,-1,...tint.slice(0,3).map(v=>v*.7));
            }
        }
    }
}

export class Terrain {
    constructor(gl) {
        this.gl = gl;
        this.count = 0;
        this.rects = new Map();
        const vertex = `#version 300 es
            layout(location=0) in vec3 position;
            layout(location=1) in vec2 uv;
            layout(location=2) in vec3 tint;
            uniform mat4 matrix;
            out vec2 texcoord; out vec3 fallback;
            void main(){texcoord=uv;fallback=tint;gl_Position=matrix*vec4(position,1.0);}`;
        const fragment = `#version 300 es
            precision highp float;
            in vec2 texcoord; in vec3 fallback;
            uniform sampler2D atlas; uniform bool picking;
            out vec4 outputColor;
            void main(){
                vec4 source=texcoord.x<0.0?vec4(fallback,1.0):texture(atlas,texcoord);
                if(source.a<0.05) discard;
                outputColor=picking?vec4(0,0,0,1):vec4(source.rgb*0.91,1);
            }`;
        this.program = gl.createProgram();
        for (const [type, code] of [[gl.VERTEX_SHADER, vertex], [gl.FRAGMENT_SHADER, fragment]]) {
            const shader = gl.createShader(type); gl.shaderSource(shader, code); gl.compileShader(shader);
            if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) throw new Error(gl.getShaderInfoLog(shader));
            gl.attachShader(this.program, shader); gl.deleteShader(shader);
        }
        gl.linkProgram(this.program);
        if (!gl.getProgramParameter(this.program, gl.LINK_STATUS)) throw new Error(gl.getProgramInfoLog(this.program));
        this.matrix = gl.getUniformLocation(this.program, 'matrix');
        this.picking = gl.getUniformLocation(this.program, 'picking');
        this.atlasUniform = gl.getUniformLocation(this.program, 'atlas');
        this.vao = gl.createVertexArray(); gl.bindVertexArray(this.vao);
        this.buffer = gl.createBuffer(); gl.bindBuffer(gl.ARRAY_BUFFER, this.buffer);
        for (const [index, width, offset] of [[0,3,0],[1,2,3],[2,3,5]]) {
            gl.enableVertexAttribArray(index); gl.vertexAttribPointer(index,width,gl.FLOAT,false,32,offset*4);
        }
        this.texture = gl.createTexture();
    }
    async load(palette) {
        const entries = Object.entries(palette).filter(([, value]) => value.textureUrl);
        const frames = entries.reduce((count, [, value]) => count + (value.variants || 1), 0);
        const canvas = document.createElement('canvas'); canvas.width = 1024;
        canvas.height = Math.max(32, Math.ceil(frames / 32) * 32);
        if (canvas.height > this.gl.getParameter(this.gl.MAX_TEXTURE_SIZE)) throw new Error('Tile atlas exceeds the graphics limit.');
        const ctx = canvas.getContext('2d'); ctx.imageSmoothingEnabled = false;
        let index = 0;
        const cache = new Map();
        for (const [id, value] of entries) {
            if (!cache.has(value.textureUrl)) cache.set(value.textureUrl, new Promise(resolve => {
                const image = new Image(); image.onload = () => resolve(image); image.onerror = () => resolve(null); image.src = value.textureUrl;
            }));
            const image = await cache.get(value.textureUrl);
            if (!image || image.height !== 32 || image.width !== 32*(value.variants || 1)) continue;
            for (let variant = 0; variant < (value.variants || 1); variant++, index++) {
                const x = index % 32 * 32, y = Math.floor(index / 32) * 32;
                ctx.drawImage(image,variant*32,0,32,32,x,y,32,32);
                // Half-texel inset avoids sampling the neighboring tile at exact edges.
                this.rects.set(`${id}:${variant}`,[(x+.5)/canvas.width,(y+.5)/canvas.height,31/canvas.width,31/canvas.height]);
            }
        }
        const gl = this.gl; gl.bindTexture(gl.TEXTURE_2D,this.texture);
        gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL,false);
        gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,gl.RGBA,gl.UNSIGNED_BYTE,canvas);
        gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MIN_FILTER,gl.NEAREST);
        gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MAG_FILTER,gl.NEAREST);
        gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_S,gl.CLAMP_TO_EDGE);
        gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_T,gl.CLAMP_TO_EDGE);
    }
    setGeometry(vertices) {
        this.gl.bindBuffer(this.gl.ARRAY_BUFFER,this.buffer);
        this.gl.bufferData(this.gl.ARRAY_BUFFER,vertices instanceof Float32Array ? vertices : new Float32Array(vertices),this.gl.STATIC_DRAW);
        this.count = vertices.length / 8;
    }
    draw(matrix,picking) {
        const gl=this.gl;
        gl.useProgram(this.program); gl.bindVertexArray(this.vao);
        gl.activeTexture(gl.TEXTURE0); gl.bindTexture(gl.TEXTURE_2D,this.texture);
        gl.uniform1i(this.atlasUniform,0); gl.uniform1i(this.picking,picking);
        gl.uniformMatrix4fv(this.matrix,false,matrix);
        gl.drawArrays(gl.TRIANGLES,0,this.count);
    }
    dispose() {
        const gl=this.gl;
        gl.deleteBuffer(this.buffer); gl.deleteVertexArray(this.vao); gl.deleteTexture(this.texture); gl.deleteProgram(this.program);
    }
}
