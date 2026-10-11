export function sampleStudy(study, sequence, seconds, powered = true) {
    if (!Number.isFinite(seconds) || seconds < 0) throw new Error('Time must be finite and nonnegative');
    const clip = study.clips.find(item => item.name === sequence);
    let key;
    if (clip) {
        key = clip.changes[0].frame;
        for (const change of clip.changes) {
            if (change.time > seconds + 1e-8) break;
            key = change.frame;
        }
    } else {
        const state = study.sourceStates.find(item => item.name === sequence);
        if (!state) throw new Error(`Unknown source state: ${sequence}`);
        const delays = state.delays?.[0] || [1];
        let frame = 0, remaining = seconds;
        while (frame < delays.length - 1 && remaining >= delays[frame] - 1e-8) remaining -= delays[frame++];
        key = `${sequence}:${frame}:powered`;
    }
    key = key.replace(/:(?:powered|unpowered)$/, powered ? ':powered' : ':unpowered');
    if (!study.frames[key]) throw new Error(`Missing authored study frame: ${key}`);
    return key;
}

export function sequenceDuration(studies, sequence) {
    return Math.max(...studies.map(study => study.clips.find(c => c.name === sequence)?.duration ??
        study.sourceStates.find(s => s.name === sequence)?.delays?.[0]?.reduce((a, b) => a + b, 0) ?? 0));
}
