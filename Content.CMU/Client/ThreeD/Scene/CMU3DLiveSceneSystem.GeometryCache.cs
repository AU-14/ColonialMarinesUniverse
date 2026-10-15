using System.Numerics;
using Content.Shared.CMU14.ThreeD;

namespace Content.Client.CMU14.ThreeD.Scene;

public sealed partial class CMU3DLiveSceneSystem
{
    private readonly record struct GeometryKey(IReadOnlyList<CMU3DModelPart> Parts, float Yaw, float HeightScale,
        float WallHeight, Color Tint);
    private readonly record struct Geometry(GeometryKey Key, List<CMU3DSceneBox>? Boxes, int PartCount);
    private readonly Dictionary<EntityUid, Geometry> _geometryCache = [];
    private readonly HashSet<EntityUid> _geometryUsed = [];
    private readonly List<EntityUid> _geometryStale = [];
    private int _cachedParts;
    private readonly HashSet<EntityUid> _publishedSources = [];
    private readonly Dictionary<EntityUid, int> _mapDemand = [];
    private int _mapRejectedParts;
    private readonly HashSet<EntityUid> _nearSources = [];
    private readonly HashSet<EntityUid> _nextNearSources = [];
    private readonly HashSet<EntityUid> _detailedWalls = [];
    private readonly HashSet<EntityUid> _nextDetailedWalls = [];
    private readonly Dictionary<IReadOnlyList<CMU3DModelPart>, IReadOnlyList<CMU3DModelPart>> _distantWalls = [];

    private bool NearSource(EntityUid uid, Vector2 position, Vector2 origin) =>
        Vector2.DistanceSquared(position, origin) <= (_nearSources.Contains(uid) ? 100 : 64);

    private IReadOnlyList<CMU3DModelPart> WallDetail(EntityUid uid, IReadOnlyList<CMU3DModelPart> parts,
        Vector2 position, float height)
    {
        if (position.LengthSquared() + height * height <= (_detailedWalls.Contains(uid) ? 64 : 36))
        {
            _nextDetailedWalls.Add(uid);
            return parts;
        }
        if (!_distantWalls.TryGetValue(parts, out var distant))
            _distantWalls[parts] = distant = CMU3DSceneDetail.DistantWall(parts);
        return distant;
    }

    private void ClearGeometryCache()
    {
        CancelRefresh();
        _geometryCache.Clear();
        _geometryUsed.Clear();
        _geometryStale.Clear();
        _terrainBounds.Clear();
        _cachedParts = 0;
        _combined.Clear();
        _publishedSources.Clear();
        _mapDemand.Clear();
        _nearSources.Clear();
        _nextNearSources.Clear();
        _detailedWalls.Clear();
        _nextDetailedWalls.Clear();
        _distantWalls.Clear();
        _activeAnimatedSprites.Clear();
    }

    private void PruneGeometryCache()
    {
        _geometryStale.Clear();
        foreach (var uid in _geometryCache.Keys)
            if (!_geometryUsed.Contains(uid)) _geometryStale.Add(uid);
        foreach (var uid in _geometryStale)
        {
            _cachedParts -= _geometryCache[uid].Boxes?.Count ?? 0;
            _geometryCache.Remove(uid);
        }
    }

    private void AppendGeometry(IReadOnlyList<CMU3DSceneBox> boxes, Vector2 position)
    {
        var offset = new Vector3(position, 0);
        for (var i = 0; i < boxes.Count; i++)
            _entityBoxes.Add(boxes[i] with { Center = boxes[i].Center + offset });
    }
}
