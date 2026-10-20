export function entityLevel(entity) { return entity.level ?? entity.position[2] ?? 0; }
export function tileLevel(tile) { return tile.level ?? tile.z ?? 0; }
export function floorHeight(scene, level) {
    return scene.map?.levels?.find(item => item.z === level)?.height ?? level;
}
export function visibleLevels(levels, selected, mode) {
    return levels.filter(level => mode === 'all' || (mode === 'below' ? level <= selected : level === selected))
        .sort((a, b) => Math.abs(a-selected)-Math.abs(b-selected) || a-b);
}
