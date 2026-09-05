using System.Numerics;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Client.CMU14.ZLevels.Core;

/// <summary>
/// Reusable storage owned by a ScalingViewport. All decisions are rebuilt within its synchronous
/// render; only the existing grid opening cache persists across camera or world changes.
/// </summary>
internal sealed class CMUZViewportRenderPlan
{
    public readonly CMUZVisibilityMask BaseOpenings = new();
    public readonly CMUZVisibilityMask LowerChain = new();
    public readonly CMUZVisibilityMask StairPreview = new();
    public readonly List<StairTile> StairTiles = new();
    private readonly CMUZVisibilityMask[] _lowerPasses = new CMUZVisibilityMask[CMUSharedZLevelsSystem.MaxZLevelsBelowRendering];

    public CMUZViewportRenderPlan()
    {
        for (var i = 0; i < _lowerPasses.Length; i++)
            _lowerPasses[i] = new CMUZVisibilityMask();
    }

    public CMUZVisibilityMask LowerPass(int depth)
    {
        return _lowerPasses[-depth - 1];
    }

    public CMUZVisibilityMask? FindLowerPass(int depth, MapId mapId)
    {
        if (depth >= 0 || depth < -_lowerPasses.Length)
            return null;

        var mask = LowerPass(depth);
        return mask.MapId == mapId ? mask : null;
    }

    public void Reset()
    {
        BaseOpenings.Clear();
        LowerChain.Clear();
        StairPreview.Clear();
        StairTiles.Clear();
        foreach (var mask in _lowerPasses)
            mask.Clear();
    }

    /// <summary>Exact world corners for drawing, and conservative bounds for sprite inclusion.</summary>
    public readonly record struct StairTile(Vector2 BottomLeft, Vector2 TopLeft, Vector2 TopRight, Vector2 BottomRight)
    {
        public Box2 Bounds => new(
            Vector2.Min(Vector2.Min(BottomLeft, TopLeft), Vector2.Min(TopRight, BottomRight)),
            Vector2.Max(Vector2.Max(BottomLeft, TopLeft), Vector2.Max(TopRight, BottomRight)));
    }
}
