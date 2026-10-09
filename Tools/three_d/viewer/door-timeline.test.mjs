import test from 'node:test';
import assert from 'node:assert/strict';
import {sampleDoorFrame, doorComposition, doorSpriteReference} from './door-timeline.js';

const frames = Array.from({length: 6}, (_, i) => ({parts: [{label: `frame ${i}`}]}));
const model = {doorSpriteStates: {opening: {frames, delays: [.1,.1,.1,.1,.1,.1]}, closing: {frames, delays: [.1,.1,.1,.1,.1,.1]},
    closed: {frames: [{parts: [{label:'closed'}]}]}, open: {frames: [{parts: [{label:'open'}]}]}},
    doorAnimationDurations: {opening:1, closing:1}};

test('six source frames clamp until one-second owner completion', () => {
    for (const state of ['opening','closing']) {
        for (let i=0;i<6;i++) assert.deepEqual(sampleDoorFrame(model,state,i/10),{state,frame:i,settled:false});
        for (const t of [.6,.8,.999]) assert.equal(sampleDoorFrame(model,state,t).frame,5);
        for (const t of [1,2]) assert.deepEqual(sampleDoorFrame(model,state,t),{state:state==='opening'?'open':'closed',frame:0,settled:true});
    }
});
test('changing direction or restarting uses the selected source strip', () => {
    assert.equal(sampleDoorFrame(model,'opening',.3).frame,3);
    assert.deepEqual(sampleDoorFrame(model,'closing',0),{state:'closing',frame:0,settled:false});
    assert.equal(doorComposition(model,sampleDoorFrame(model,'closing',1))[0].label,'closed');
    assert.equal(sampleDoorFrame(model,'opening',0).frame,0);
});
test('unknown states and frames cannot borrow a stable pose', () => {
    assert.equal(sampleDoorFrame(model,'welded',0),null);
    assert.equal(doorComposition(model,{state:'opening',frame:6}),null);
    assert.equal(doorComposition(model,{state:'opening',frame:-1}),null);
    for (const t of [-1,NaN,Infinity]) assert.throws(()=>sampleDoorFrame(model,'opening',t));
});
test('fixture references use source direction and selected frame, with no inherited fallback', () => {
    const m = {doorSpriteReferences:{opening:[['s0','n0','e0','w0'],['s1','n1','e1','w1']]}};
    const e = {matchKind:'exact',yaw:0,doorSpriteState:'opening',doorSpriteFrame:1};
    for (const [yaw, url] of [[0,'s1'],[Math.PI,'n1'],[Math.PI/2,'e1'],[-Math.PI/2,'w1']])
        assert.equal(doorSpriteReference({...e,yaw},m),url);
    assert.equal(doorSpriteReference({...e,doorSpriteFrame:2},m),null);
    assert.equal(doorSpriteReference({...e,matchKind:'inherited'},m),null);
    assert.equal(doorSpriteReference({...e,spriteOverride:{}},m),null);
});
