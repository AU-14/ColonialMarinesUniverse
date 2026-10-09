// RSI stores directions as S,N,E,W,SE,SW,NE,NW, rather than angle order.
export function referenceFrame(meta, stateName, yaw, imageWidth) {
    const state = meta.states?.find(value => value.name === stateName);
    const directions = state?.directions || 1;
    const count = directions === 8 ? 8 : directions === 4 ? 4 : 1;
    const sector = ((Math.floor(yaw / (2*Math.PI/count) + .5) % count) + count) % count;
    const direction = (count === 8 ? [0,4,2,6,1,7,3,5] : count === 4 ? [0,2,1,3] : [0])[sector];
    let first = 0;
    for (let i=0; i<direction; i++) first += (state?.delays?.[i] || state?.delays?.[0] || [1]).length;
    const width = meta.size?.x || 32, height = meta.size?.y || 32;
    const columns = Math.max(1, Math.floor(imageWidth / width));
    return [first % columns * width, Math.floor(first / columns) * height, width, height];
}

export function barricadeReference(entity, model) {
    if (entity.matchKind !== 'exact' || entity.unsupportedState || entity.spriteOverride ||
        !Number.isFinite(entity.yaw) || !['0', '4', '8', '12'].includes(entity.barricadeDamageState)) return null;
    const sector = ((Math.floor(entity.yaw / (Math.PI / 2) + .5) % 4) + 4) % 4;
    if (entity.barricadeWired !== undefined && typeof entity.barricadeWired !== 'boolean') return null;
    if (entity.barricadeAcidFrame !== undefined) {
        if (!Number.isInteger(entity.barricadeAcidFrame) || entity.barricadeAcidFrame < 0 || entity.barricadeAcidFrame >= 5) return null;
        const key = entity.barricadeDamageState + (entity.barricadeWired ? ':wire' : '');
        return model?.barricadeAcidReferences?.[key]?.[entity.barricadeAcidFrame]?.[[0, 2, 1, 3][sector]] || null;
    }
    const references = entity.barricadeWired ? model?.barricadeWiredReferences : model?.barricadeReferences;
    return references?.[entity.barricadeDamageState]?.[[0, 2, 1, 3][sector]] || null;
}
