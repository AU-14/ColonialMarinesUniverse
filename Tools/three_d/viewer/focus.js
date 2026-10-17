// Keep floor selection tied to the saved entity, while aiming at its rendered volume.
export function entityFocusPoint(entity, parts, maxHeight) {
    const origin = entity.position.map((v, i) => v + (entity.renderOffset?.[i] || 0));
    const yaw = entity.renderYaw ?? entity.yaw ?? 0, c = Math.cos(yaw), s = Math.sin(yaw);
    const low = [Infinity, Infinity, Infinity], high = [-Infinity, -Infinity, -Infinity];
    for (const part of parts || []) {
        const min = part.min, max = [...part.max];
        if (maxHeight !== undefined) max[2] = Math.min(max[2], maxHeight);
        if (max.some((v, i) => v <= min[i])) continue;
        const center = min.map((v, i) => (v + max[i]) / 2);
        const half = max.map((v, i) => (v - min[i]) / 2);
        const a = yaw + (part.yaw || 0) * Math.PI / 180, ca = Math.cos(a), sa = Math.sin(a);
        const pitch = (part.pitch || 0) * Math.PI / 180, cp = Math.cos(pitch), sp = Math.sin(pitch);
        const xExtent = Math.abs(cp) * half[0] + Math.abs(sp) * half[2];
        const extent = [Math.abs(ca)*xExtent + Math.abs(sa)*half[1],
            Math.abs(sa)*xExtent + Math.abs(ca)*half[1], Math.abs(sp)*half[0] + Math.abs(cp)*half[2]];
        const world = [origin[0] + c*center[0] - s*center[1], origin[1] + s*center[0] + c*center[1], origin[2] + center[2]];
        for (let i = 0; i < 3; i++) {
            low[i] = Math.min(low[i], world[i] - extent[i]);
            high[i] = Math.max(high[i], world[i] + extent[i]);
        }
    }
    return low.every(Number.isFinite) ? low.map((v, i) => (v + high[i]) / 2) :
        [entity.position[0], entity.position[1], entity.position[2] + .6];
}
