// Source strip timing. Gameplay decides visibility/expiry separately.
export function sampleAcidFrame(seconds, visible = true) {
    if (!visible) return null;
    if (!Number.isFinite(seconds) || seconds < 0) throw new Error('Acid time must be finite and nonnegative');
    return Math.floor(seconds * 10 + 1e-9) % 5;
}

export function acidComposition(model, damage, wired, frame) {
    if (!['0', '4', '8', '12'].includes(damage) || typeof wired !== 'boolean') return null;
    if (frame === null) return model?.[wired ? 'barricadeWiredStates' : 'barricadeDamageStates']?.[damage]?.parts || null;
    if (!Number.isInteger(frame) || frame < 0 || frame >= 5) return null;
    return model?.barricadeAcidStates?.[damage]?.[wired ? 'wiredFrames' : 'frames']?.[frame]?.parts || null;
}
