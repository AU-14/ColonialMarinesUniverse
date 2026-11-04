import test from 'node:test';
import assert from 'node:assert/strict';
import {sampleAcidFrame, acidComposition} from './acid-timeline.js';
import {barricadeReference} from './reference.js';

test('acid repeats the five source frames without imposing a gameplay lifetime', () => {
    for (const [time, frame] of [[0,0],[.099,0],[.1,1],[.2,2],[.3,3],[.4,4],[.499,4],[.5,0],[.6,1],[8.7,2]])
        assert.equal(sampleAcidFrame(time), frame);
    assert.equal(sampleAcidFrame(99, false), null);
    for (const value of [NaN, Infinity, -.1]) assert.throws(() => sampleAcidFrame(value));
});

test('removal returns the current damage/wire pose and missing acid never returns clean geometry', () => {
    const model = {barricadeWiredStates:{'8':{parts:['wire']}}, barricadeDamageStates:{'8':{parts:['dry']}},
        barricadeAcidStates:{'8':{frames:Array.from({length:5},(_,i)=>({parts:['dry-acid-'+i]})),
            wiredFrames:Array.from({length:5},(_,i)=>({parts:['wire-acid-'+i]}))}}};
    for (let i=0;i<5;i++) {
        assert.deepEqual(acidComposition(model,'8',true,i), ['wire-acid-'+i]);
        assert.deepEqual(acidComposition(model,'8',false,i), ['dry-acid-'+i]);
    }
    assert.deepEqual(acidComposition(model,'8',true,null),['wire']);
    assert.deepEqual(acidComposition(model,'8',false,null),['dry']);
    for (const frame of [undefined, -1, 5, 1.5]) assert.equal(acidComposition(model,'8',true,frame),null);
    assert.equal(acidComposition(model,'12',true,1),null);
});

test('all 160 acid references select their original source direction and layering context', () => {
    const model = {barricadeAcidReferences:{},barricadeReferences:{'0':['clean']}};
    for (const damage of ['0','4','8','12']) for (const wired of [false,true]) {
        const key=damage+(wired?':wire':'');
        model.barricadeAcidReferences[key]=Array.from({length:5},(_,f)=>['S','N','E','W'].map(d=>`${key}-${f}-${d}`));
        for (let f=0;f<5;f++) for (const [yaw,d] of [[0,'S'],[Math.PI/2,'E'],[Math.PI,'N'],[-Math.PI/2,'W']])
            assert.equal(barricadeReference({matchKind:'exact',yaw,barricadeDamageState:damage,barricadeWired:wired,barricadeAcidFrame:f},model),`${key}-${f}-${d}`);
    }
    for (const frame of [null,-1,5,.5,'0']) assert.equal(barricadeReference({matchKind:'exact',yaw:0,barricadeDamageState:'0',barricadeAcidFrame:frame},model),null);
});
