// Build dense regions in GPU-ready storage without a second full-sized conversion.
export class FloatBuffer {
    constructor() {
        this.data = new Float32Array(0);
        this.length = 0;
    }
    push(...values) {
        const needed = this.length + values.length;
        if (needed > this.data.length) {
            const data = new Float32Array(Math.max(4096, this.data.length * 2, needed));
            data.set(this.data.subarray(0, this.length));
            this.data = data;
        }
        this.data.set(values, this.length);
        this.length = needed;
    }
    finish() { return this.data.subarray(0, this.length); }
}
