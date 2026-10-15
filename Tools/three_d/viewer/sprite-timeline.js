// Portable source-frame study only. Native gameplay reads its existing Sprite layer clock.
export function studyModel(model) {
    for (const [family, initial] of [['charger', 'recharger-0--empty'], ['foam', 'edges-15'], ['solution', 'saved-default']]) {
        if (model?.[family+'States']) return {...model, spriteStates:model[family+'States'],
            spriteStateReferences:model[family+'StateReferences'], referenceState:initial};
    }
    return model;
}

function definition(model, state) {
    const value = model?.spriteStates?.[state];
    if (!value || !Array.isArray(value.frames) || !value.frames.length || !Array.isArray(value.delays) ||
        value.frames.length !== value.delays.length || value.delays.some(v => !Number.isFinite(v) || v <= 0)) return null;
    return value;
}

export function spritePeriod(model, state) {
    const value = definition(model, state);
    const period = value?.delays.reduce((sum, delay) => sum + delay, 0);
    return Number.isFinite(period) && period > 0 ? period : null;
}

export function sampleSpriteFrame(model, state, seconds) {
    if (!Number.isFinite(seconds) || seconds < 0) throw new Error('Sprite time must be finite and nonnegative');
    const value = definition(model, state), period = spritePeriod(model, state);
    if (!value || !period) return null;
    if (value.frames.length === 1) return {state, frame: 0};
    const epsilon = Math.min(...value.delays) * 1e-8;
    let local = seconds % period, end = 0;
    if (period - local <= epsilon) local = 0;
    for (let frame = 0; frame < value.frames.length; frame++) {
        end += value.delays[frame];
        if (local + epsilon < end) return {state, frame};
    }
    return {state, frame: 0};
}

export function spriteFrameTime(model, state, frame) {
    const value = definition(model, state);
    if (!value || !Number.isInteger(frame) || frame < 0 || frame >= value.frames.length) return null;
    return value.delays.slice(0, frame).reduce((sum, delay) => sum + delay, 0);
}

export function spriteComposition(model, pose) {
    if (!pose || !Number.isInteger(pose.frame) || pose.frame < 0) return null;
    return model?.spriteStates?.[pose.state]?.frames?.[pose.frame]?.parts || null;
}

export function spriteReference(model, pose) {
    if (!spriteComposition(model, pose)) return null;
    return model?.spriteStateReferences?.[pose.state]?.[pose.frame] || null;
}
