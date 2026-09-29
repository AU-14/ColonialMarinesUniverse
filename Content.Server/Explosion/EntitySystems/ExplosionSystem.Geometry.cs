using System.Numerics;
using Content.Server.Explosion.Components;
using Robust.Shared.Map.Components;

namespace Content.Server.Explosion.EntitySystems;

public sealed partial class ExplosionSystem
{
    /// <summary>All geometry inputs are captured before the first preparation yield.</summary>
    private Dictionary<EntityUid, ExplosionGridSnapshot> CaptureExplosionGeometry(IEnumerable<EntityUid> grids)
    {
        var result = new Dictionary<EntityUid, ExplosionGridSnapshot>();
        foreach (var uid in grids)
        {
            if (result.ContainsKey(uid) || !_gridEdges.TryGetValue(uid, out var edges) ||
                !TryComp(uid, out MapGridComponent? grid) || TerminatingOrDeleted(uid))
                continue;
            var (_, angle, matrix, inverse) = _transformSystem.GetWorldPositionRotationMatrixWithInv(Transform(uid));
            var airtight = CompOrNull<ExplosionAirtightGridComponent>(uid)?.Tiles.Capture() ??
                           ExplosionTileMap<ExplosionAirtightGridComponent.TileData>.Snapshot.Empty;
            result.Add(uid, new((uid, grid), grid.TileSize, angle, matrix, inverse, edges.Capture(), airtight));
        }
        return result;
    }

    public sealed record ExplosionGridSnapshot(
        Entity<MapGridComponent> Grid,
        ushort TileSize,
        Angle Angle,
        Matrix3x2 Matrix,
        Matrix3x2 Inverse,
        ExplosionTileMap<NeighborFlag>.Snapshot Edges,
        ExplosionTileMap<ExplosionAirtightGridComponent.TileData>.Snapshot Airtight);
}
