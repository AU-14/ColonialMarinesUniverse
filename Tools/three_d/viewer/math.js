export const clamp = (value, min, max) => Math.min(max, Math.max(min, value));
export const add = (a, b) => a.map((v, i) => v + b[i]);
export const subtract = (a, b) => a.map((v, i) => v - b[i]);
export const dot = (a, b) => a.reduce((sum, v, i) => sum + v * b[i], 0);
export const cross = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
export const normalize = a => { const length = Math.hypot(...a) || 1; return a.map(v => v / length); };

export function perspective(fov, aspect, near, far) {
    const f = 1 / Math.tan(fov / 2), nf = 1 / (near - far);
    return new Float32Array([f / aspect, 0, 0, 0, 0, f, 0, 0, 0, 0, (far + near) * nf, -1, 0, 0, 2 * far * near * nf, 0]);
}

export function lookAt(eye, target) {
    const z = normalize(subtract(eye, target));
    const x = normalize(cross([0, 0, 1], z));
    const y = cross(z, x);
    return new Float32Array([
        x[0], y[0], z[0], 0, x[1], y[1], z[1], 0, x[2], y[2], z[2], 0,
        -dot(x, eye), -dot(y, eye), -dot(z, eye), 1,
    ]);
}

export function multiply(a, b) {
    const output = new Float32Array(16);
    for (let column = 0; column < 4; column++)
        for (let row = 0; row < 4; row++)
            for (let k = 0; k < 4; k++)
                output[column * 4 + row] += a[k * 4 + row] * b[column * 4 + k];
    return output;
}

export function color(hex, alpha = 1) {
    if (typeof hex !== 'string' || !/^#[0-9a-f]{6}([0-9a-f]{2})?$/i.test(hex)) return [0.6, 0.65, 0.65, alpha];
    const channel = offset => parseInt(hex.slice(offset, offset + 2), 16) / 255;
    return [channel(1), channel(3), channel(5), hex.length === 9 ? channel(7) * alpha : alpha];
}

export class OrbitCamera {
    constructor() {
        this.target = [0, 0, 0.6];
        this.yaw = -Math.PI / 3;
        this.pitch = 0.8;
        this.distance = 38;
    }
    get eye() {
        const radial = Math.cos(this.pitch) * this.distance;
        return [this.target[0] + Math.cos(this.yaw) * radial, this.target[1] + Math.sin(this.yaw) * radial,
            this.target[2] + Math.sin(this.pitch) * this.distance];
    }
    matrix(aspect) {
        return multiply(perspective(Math.PI / 4, aspect, 0.1, 700), lookAt(this.eye, this.target));
    }
    orbit(dx, dy) {
        this.yaw -= dx * 0.006;
        this.pitch = clamp(this.pitch + dy * 0.006, 0.08, 1.53);
    }
    pan(dx, dy, height) {
        const scale = this.distance * 0.83 / Math.max(1, height);
        const right = [-Math.sin(this.yaw), Math.cos(this.yaw)];
        const away = [Math.cos(this.yaw), Math.sin(this.yaw)];
        this.target[0] += (-right[0] * dx + away[0] * dy) * scale;
        this.target[1] += (-right[1] * dx + away[1] * dy) * scale;
    }
    zoom(delta) { this.distance = clamp(this.distance * Math.exp(delta * 0.0012), 3, 220); }
}
