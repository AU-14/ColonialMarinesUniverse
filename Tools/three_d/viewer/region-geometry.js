import {appendBox, INSTANCE_STRIDE} from './renderer.js';
import {entityLevel} from './levels.js';
import {FloatBuffer} from './float-buffer.js';

export function structural(entity) { return /wall|window|shutter|airlock|door|curtain|barricade|railing|fence/i.test(entity.prototype); }

export function buildEntityGeometry(entities, models, variants, surfaces, {center, radius, showKinds, cutaway, maxHeight, focusLevel}) {
    const values = new FloatBuffer(), rounded = new FloatBuffer(), wedges = new FloatBuffer(), reverseWedges = new FloatBuffer(), foliage = new FloatBuffer();
    const cylinders = Array.from({length: 3}, () => new FloatBuffer());
    const slanted = Array.from({length: 4}, () => new FloatBuffer());
    const slantedNames = ['SlantedX','SlantedXReverse','SlantedY','SlantedYReverse'];
    let exact = 0, inherited = 0, unmapped = 0;
    const inside = (x, y) => Math.abs(x - center[0]) <= radius && Math.abs(y - center[1]) <= radius;
    for (const entity of entities) {
        if (!inside(entity.position[0], entity.position[1]) || !showKinds.has(entity._kind)) continue;
        const model = models.get(entity.modelId);
        const parts = variants?.[entity.geometryKey] || model?.parts;
        if (parts?.length) {
            const position = entity.position.map((value, axis) => value + (entity.renderOffset?.[axis] || 0));
            const options = cutaway && entityLevel(entity) === focusLevel && (structural(entity) || model?.wallMounted) ? {maxHeight} : {};
            for (const part of parts)
                appendBox(part.shape === 'Foliage' ? foliage : slantedNames.includes(part.shape) ? slanted[slantedNames.indexOf(part.shape)] : part.shape === 'WedgeYReverse' ? reverseWedges : part.shape === 'WedgeY' ? wedges : part.shape?.startsWith('Cylinder') ? cylinders['XYZ'.indexOf(part.shape.at(-1))] : part.shape === 'Ellipsoid' ? rounded : values, part.min, part.max, part.color || '#A0ACAA', position, entity.renderYaw ?? entity.yaw ?? 0, entity._pickId, {...options, partYaw: part.yaw, partPitch: part.pitch, surface: surfaces.get(part.surface), surfaceAxis: part.surfaceAxis, surfaceFlipU: part.surfaceFlipU});
            if (entity._kind === 'exact') exact++; else inherited++;
        } else {
            const wall = structural(entity);
            const halfWidth = entity.unsupportedState ? 0.10 : wall ? 0.47 : 0.18;
            const height = entity.unsupportedState ? 0.15 : wall ? 0.52 : 0.23;
            appendBox(values, [-halfWidth, -halfWidth, 0.005], [halfWidth, halfWidth, height], entity.unsupportedState ? '#DDAD60' : '#7A9296',
                entity.position.map((v,i)=>v+(entity.renderOffset?.[i]||0)), entity.yaw || 0, entity._pickId, {alpha: 0.48});
            unmapped++;
        }
    }
    return {
        values: values.finish(), rounded: rounded.finish(), cylinders: cylinders.map(data => data.finish()),
        wedges: wedges.finish(), reverseWedges: reverseWedges.finish(), slanted: slanted.map(data => data.finish()), foliage: foliage.finish(),
        exact, inherited, unmapped,
        solidCount: [values, rounded, ...cylinders, wedges, reverseWedges, ...slanted, foliage].reduce((sum, data) => sum + data.length, 0) / INSTANCE_STRIDE,
    };
}
