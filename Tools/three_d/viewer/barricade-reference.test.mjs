import test from 'node:test';
import assert from 'node:assert/strict';
import {barricadeReference} from './reference.js';

test('paired barricade reference follows saved facing and damage suffix', () => {
    const model = {barricadeReferences: Object.fromEntries(['0', '4', '8', '12'].map(key =>
        [key, ['S', 'N', 'E', 'W'].map(direction => `${key}-${direction}`)]))};
    for (const key of ['0', '4', '8', '12'])
        for (const [yaw, direction] of [[0, 'S'], [Math.PI / 2, 'E'], [Math.PI, 'N'], [-Math.PI / 2, 'W'], [2 * Math.PI, 'S']])
            assert.equal(barricadeReference({matchKind: 'exact', yaw, barricadeDamageState: key}, model), `${key}-${direction}`);
});

test('unresolved or overridden barricades never get a false clean reference', () => {
    const model = {barricadeReferences: {'0': ['intact', 'intact', 'intact', 'intact']}};
    const entity = {matchKind: 'exact', yaw: 0, barricadeDamageState: '0'};
    for (const patch of [{matchKind: 'inherited'}, {unsupportedState: 'acid'}, {spriteOverride: {}},
        {yaw: NaN}, {barricadeDamageState: '16'}, {barricadeDamageState: undefined}, {barricadeWired: 'unknown'}, {barricadeWired: true}])
        assert.equal(barricadeReference({...entity, ...patch}, model), null);
});

test('wired references follow all four damage poses and facings without falling back to dry', () => {
    const model = {barricadeWiredReferences: Object.fromEntries(['0', '4', '8', '12'].map(key =>
        [key, ['S', 'N', 'E', 'W'].map(direction => `${key}-wire-${direction}`)]))};
    for (const key of ['0', '4', '8', '12'])
        for (const [yaw, direction] of [[0, 'S'], [Math.PI / 2, 'E'], [Math.PI, 'N'], [-Math.PI / 2, 'W']])
            assert.equal(barricadeReference({matchKind: 'exact', yaw, barricadeDamageState: key, barricadeWired: true}, model), `${key}-wire-${direction}`);
});
