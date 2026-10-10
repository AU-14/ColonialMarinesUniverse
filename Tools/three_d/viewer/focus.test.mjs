import {test} from 'node:test';
import assert from 'node:assert/strict';
import {entityFocusPoint} from './focus.js';
const near = (a,b) => a.forEach((v,i) => assert.ok(Math.abs(v-b[i])<1e-8, `${a} != ${b}`));

test('overhead geometry focuses above its floor, including underground levels', () => {
    const entity={position:[218.5,-104.5,-2],yaw:0};
    near(entityFocusPoint(entity,[{min:[-.5,-.5,2.82],max:[.5,.5,3.02]}]),[218.5,-104.5,.92]);
    assert.deepEqual(entity.position,[218.5,-104.5,-2]);
});
test('placed and rotated geometry focuses on the rendered volume', () => {
    near(entityFocusPoint({position:[10,20,0],renderOffset:[.2,-.3,1],yaw:0,renderYaw:Math.PI/2},
        [{min:[1,-.25,0],max:[3,.25,2]}]),[10.2,21.7,2]);
});
test('cutaway focus uses the visible lower wall and ignores removed upper parts', () => {
    near(entityFocusPoint({position:[0,0,0]},[
        {min:[-.5,-.5,0],max:[.5,.5,2.8]}, {min:[10,-.5,2],max:[11,.5,3]}],.9),[0,0,.45]);
});
test('tilted parts contribute their full height to the focus bounds', () => {
    near(entityFocusPoint({position:[0,0,0]},[
        {min:[-2,-.5,0],max:[2,.5,1],pitch:90},
        {min:[-.5,-.5,2],max:[.5,.5,4]}]),[0,0,1.25]);
});
test('missing or fully cut geometry keeps the saved-position fallback', () => {
    const e={position:[4,5,-2],renderOffset:[0,0,5]};
    near(entityFocusPoint(e),[4,5,-1.4]);
    near(entityFocusPoint(e,[{min:[0,0,2],max:[1,1,3]}],.9),[4,5,-1.4]);
});
