import test from 'node:test';
import assert from 'node:assert/strict';
import {buildEntityGeometry} from './region-geometry.js';
import {Renderer, INSTANCE_STRIDE} from './renderer.js';

const body = {min: [-.5,-.5,0], max: [.5,.5,2.8], color: '#8899AA'};
const entity = (id, level, modelId = 'wall') => ({
    _pickId: id, _kind: 'exact', level, position: [0,0,level * 3], prototype: 'CMWall', modelId,
});
const options = {center: [0,0], radius: 64, showKinds: new Set(['exact']), cutaway: false, focusLevel: 0, maxHeight: .9};

function uploaded(result) {
    const arrays = [];
    const renderer = {
        batches: Array.from({length: 12}, () => ({})),
        gl: {bindBuffer() {}, bufferData(target, data) { arrays.push(data); }},
    };
    Renderer.prototype.setGeometry.call(renderer, result.values, result.rounded, result.cylinders,
        result.wedges, result.reverseWedges, result.slanted, result.foliage);
    return arrays;
}

test('dense multi-floor regions upload later walls and props after the former part limit', () => {
    const models = new Map([
        ['dense', {parts: Array(32).fill(body)}],
        ['wall', {parts: [body]}],
        ['prop', {parts: [{...body, shape: 'CylinderZ'}]}],
    ]);
    const entities = Array.from({length: 5000}, (_, index) => ({
        ...entity(index + 1, 0, 'dense'), position: [index % 100 - 50, Math.floor(index / 100) - 25, 0],
    }));
    entities.push(entity(5001, 1), {...entity(5002, -1, 'prop'), prototype: 'CMChair'});
    const result = buildEntityGeometry(entities, models, {}, new Map(), options);
    const arrays = uploaded(result);
    const ids = new Set(arrays.flatMap(data => {
        const ids = [];
        for (let i = 12; i < data.length; i += INSTANCE_STRIDE) ids.push(data[i]);
        return ids;
    }));
    assert.deepEqual(ids, new Set(entities.map(item => item._pickId)));
    const lastWall = arrays[0].subarray(-INSTANCE_STRIDE);
    assert.equal(lastWall[12], 5001);
    assert.ok(Math.abs(lastWall[2] - 4.4) < .00001);
});

test('wall cutaway preserves full walls on the other floors and follows the focus floor', () => {
    const models = new Map([['wall', {parts: [body]}]]);
    const entities = [entity(1, -1), entity(2, 0), entity(3, 1)];
    for (const focusLevel of [0, 1]) {
        const result = buildEntityGeometry(entities, models, {}, new Map(), {...options, cutaway: true, focusLevel});
        const data = uploaded(result)[0];
        for (let index = 0; index < entities.length; index++) {
            const start = index * INSTANCE_STRIDE;
            const height = entities[index].level === focusLevel ? .9 : 2.8;
            assert.ok(Math.abs(data[start + 5] - height) < .00001, `wall on level ${entities[index].level}, focus ${focusLevel}`);
            assert.ok(Math.abs(data[start + 2] - (entities[index].position[2] + height/2)) < .00001);
        }
    }
});
