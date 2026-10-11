import test from 'node:test';
import assert from 'node:assert/strict';
import {studyModel, spritePeriod, sampleSpriteFrame, spriteFrameTime, spriteComposition, spriteReference} from './sprite-timeline.js';

const frame = label => ({parts: [{label}]});
const model = {spriteStates: {running: {frames: [frame('a'), frame('b'), frame('c')], delays: [.1, .2, .3]},
    off: {frames: [frame('off')], delays: [1]}}, spriteStateReferences: {running: ['a.png', 'b.png', 'c.png'], off: ['off.png']}};

test('composed source studies select their actual default and retain source references without changing library data', () => {
    for (const [family, name] of [['charger','recharger-0--empty'],['foam','edges-15'],['solution','saved-default']]) {
        const original = {id:family, referenceState:'icon', [family+'States']:{[name]:{frames:[frame(family)],delays:[1]}},
            [family+'StateReferences']:{[name]:[family+'.png']}};
        const adapted = studyModel(original), pose = sampleSpriteFrame(adapted, adapted.referenceState, 15);
        assert.equal(spriteComposition(adapted, pose)[0].label, family);
        assert.equal(spriteReference(adapted, pose), family+'.png');
        assert.equal(original.referenceState, 'icon');
        assert.equal(original.spriteStates, undefined);
    }
    assert.equal(studyModel(model), model);
});

test('source delays define every boundary and repeat without clamping at completion', () => {
    assert.ok(Math.abs(spritePeriod(model, 'running') - .6) < 1e-9);
    for (const [time, expected] of [[0,0],[.099,0],[.1,1],[.299,1],[.3,2],[.599,2],[.6,0],[.7,1],[1.2,0],[6.3,2]])
        assert.equal(sampleSpriteFrame(model, 'running', time).frame, expected);
});

test('pause and single-frame stepping use exact source boundaries', () => {
    for (let frame = 0; frame < 3; frame++) {
        const time = spriteFrameTime(model, 'running', frame);
        const first = sampleSpriteFrame(model, 'running', time);
        assert.deepEqual(sampleSpriteFrame(model, 'running', time), first);
        assert.equal(first.frame, frame);
        assert.equal(spriteReference(model, first), ['a.png','b.png','c.png'][frame]);
        assert.equal(spriteComposition(model, first)[0].label, ['a','b','c'][frame]);
    }
});

test('static off has one pose and changing state begins in that state', () => {
    for (const time of [0, .5, 1, 100]) assert.deepEqual(sampleSpriteFrame(model, 'off', time), {state:'off',frame:0});
    assert.equal(spriteReference(model, sampleSpriteFrame(model, 'off', 0)), 'off.png');
    assert.equal(sampleSpriteFrame(model, 'running', 0).frame, 0);
});

test('unknown and malformed input cannot borrow a default frame', () => {
    assert.equal(sampleSpriteFrame(model, 'missing', 0), null);
    for (const frame of [-1, 3, .5]) {
        assert.equal(spriteFrameTime(model, 'running', frame), null);
        assert.equal(spriteComposition(model, {state:'running',frame}), null);
        assert.equal(spriteReference(model, {state:'running',frame}), null);
    }
    for (const time of [-1,NaN,Infinity]) assert.throws(() => sampleSpriteFrame(model, 'running', time));
    for (const delays of [[0], [NaN], [Infinity], [-1], [1,2]])
        assert.equal(sampleSpriteFrame({spriteStates:{off:{frames:[frame('off')],delays}}}, 'off', 0), null);
});

test('selecting another directional model uses that model’s frame and reference', () => {
    const other = {...model, spriteStateReferences:{running:['north-a.png','north-b.png','north-c.png']}};
    assert.equal(spriteReference(other, sampleSpriteFrame(other, 'running', .1)), 'north-b.png');
    assert.equal(spriteReference(model, sampleSpriteFrame(model, 'running', .1)), 'b.png');
});
