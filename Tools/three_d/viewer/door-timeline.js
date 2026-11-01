// Portable comparison timing; native playback follows the existing sprite owner.
export function sampleDoorFrame(model, state, seconds) {
    if (!Number.isFinite(seconds) || seconds < 0) throw new Error('Door time must be finite and nonnegative');
    if (!['closed', 'open', 'opening', 'closing'].includes(state)) return null;
    const definition = model?.doorSpriteStates?.[state];
    if (!definition) return null;
    if (state === 'open' || state === 'closed') return {state, frame: 0, settled: true};
    const duration = model.doorAnimationDurations?.[state];
    if (!Number.isFinite(duration)) return null;
    if (seconds >= duration) return {state: state === 'opening' ? 'open' : 'closed', frame: 0, settled: true};
    let boundary = 0;
    for (let frame = 0; frame < definition.delays.length; frame++) {
        boundary += definition.delays[frame];
        if (seconds + 1e-9 < boundary) return {state, frame, settled: false};
    }
    return {state, frame: definition.frames.length - 1, settled: false};
}

export function doorComposition(model, pose) {
    if (!pose || !Number.isInteger(pose.frame) || pose.frame < 0) return null;
    return model?.doorSpriteStates?.[pose.state]?.frames?.[pose.frame]?.parts || null;
}

export function doorSpriteReference(entity, model) {
    if (entity.matchKind !== 'exact' || entity.unsupportedState || entity.spriteOverride ||
        !Number.isFinite(entity.yaw) || !Number.isInteger(entity.doorSpriteFrame) || entity.doorSpriteFrame < 0) return null;
    const sector = ((Math.floor(entity.yaw / (Math.PI / 2) + .5) % 4) + 4) % 4;
    return model?.doorSpriteReferences?.[entity.doorSpriteState]?.[entity.doorSpriteFrame]?.[[0, 2, 1, 3][sector]] || null;
}
