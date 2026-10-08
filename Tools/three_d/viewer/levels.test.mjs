import test from 'node:test';
import assert from 'node:assert/strict';
import {entityLevel, tileLevel, floorHeight, visibleLevels} from './levels.js';

test('stack cutaways retain lower floors and keep logical selection separate from height', () => {
    assert.deepEqual(visibleLevels([-2,-1,0,1,2], 1, 'below'), [1,0,-1,-2]);
    assert.deepEqual(visibleLevels([-1,0,1], 0, 'all'), [0,-1,1]);
    assert.deepEqual(visibleLevels([-1,0,1], 0, 'single'), [0]);
    assert.equal(entityLevel({level: -2, position: [0,0,-6]}), -2);
    assert.equal(tileLevel({level: 1, z: 3}), 1);
    assert.equal(floorHeight({map: {levels: [{z: -2, height: -6}]}}, -2), -6);
});
