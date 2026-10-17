using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Client.CMU14.ZLevels.Core;

/// <summary>
/// Evidence for a render contribution. Incomplete geometry or failed point samples are Unknown,
/// not proof that the whole contribution is hidden.
/// </summary>
public enum CMUZVisibility : byte
{
    Unknown,
    Hidden,
    Visible,
}

/// <summary>
/// Conservative coverage in one render pass's map coordinates. The owner rebuilds it for each
/// viewport render; it contains no decisions from an earlier camera, tile or occluder state.
/// </summary>
public sealed class CMUZVisibilityMask
{
    private List<Box2> _bounds = new();
    private List<Box2> _intersectionScratch = new();

    public MapId MapId { get; private set; } = MapId.Nullspace;
    public Box2 WorldBounds { get; private set; }
    public IReadOnlyList<Box2> Bounds => _bounds;
    public CMUZVisibility Visibility { get; private set; }
    public bool DynamicOnly { get; private set; }

    /// <summary>
    /// Copies an aperture query. An incomplete query covers the entire view conservatively;
    /// its truncated list must never hide an unchecked aperture.
    /// </summary>
    public void SetOpenings(
        MapId mapId,
        Box2 worldBounds,
        IReadOnlyList<Box2> openings,
        bool complete,
        bool dynamicOnly = true)
    {
        Reset(mapId, worldBounds, dynamicOnly);
        if (!complete)
        {
            _bounds.Add(worldBounds);
            Visibility = CMUZVisibility.Unknown;
            return;
        }

        foreach (var opening in openings)
            AddBounds(opening);
    }

    /// <summary>
    /// Moves the preceding aperture coverage into the source map of the next world pass.
    /// Eye.Offset is added to the source coordinates, as in Eye.GetViewMatrixInv.
    /// </summary>
    public void SetProjected(CMUZVisibilityMask source, MapId mapId, Vector2 offset, float filterMargin = 0f)
    {
        Reset(mapId, source.WorldBounds.Translated(offset), source.DynamicOnly);
        foreach (var bounds in source._bounds)
            AddBounds(bounds.Translated(offset).Enlarged(filterMargin));
    }

    /// <summary>
    /// Restricts a deeper view to openings in the intervening floor. Rectangle intersections
    /// preserve disconnected apertures and holes; their enclosing union is not an aperture.
    /// If the geometry or fragment budget is incomplete, retain the preceding conservative mask.
    /// </summary>
    public bool IntersectOpenings(IReadOnlyList<Box2> openings, bool complete, int maxFragments)
    {
        if (!complete)
        {
            Visibility = _bounds.Count == 0 ? CMUZVisibility.Hidden : CMUZVisibility.Unknown;
            return false;
        }

        _intersectionScratch.Clear();
        foreach (var bounds in _bounds)
        {
            foreach (var opening in openings)
            {
                if (!bounds.Intersects(opening))
                    continue;

                var intersection = bounds.Intersect(opening);
                if (intersection.Width <= 0f || intersection.Height <= 0f)
                    continue;

                if (maxFragments > 0 && _intersectionScratch.Count >= maxFragments)
                {
                    _intersectionScratch.Clear();
                    Visibility = CMUZVisibility.Unknown;
                    return false;
                }

                _intersectionScratch.Add(intersection);
            }
        }

        (_bounds, _intersectionScratch) = (_intersectionScratch, _bounds);
        _intersectionScratch.Clear();
        Visibility = _bounds.Count == 0 ? CMUZVisibility.Hidden : CMUZVisibility.Unknown;
        return true;
    }

    /// <summary>
    /// Marks positive visibility evidence for at least one retained region, such as a stair tile
    /// selected by the preview's LOS policy. This never follows merely from a failed LOS sample.
    /// </summary>
    public void ConfirmVisible()
    {
        if (_bounds.Count != 0)
            Visibility = CMUZVisibility.Visible;
    }

    /// <summary>
    /// Only a disjoint conservative mask proves a sprite hidden. Overlap remains Unknown because
    /// an AABB is not proof that a pixel or a ray reaches an aperture.
    /// </summary>
    public CMUZVisibility ClassifyBounds(Box2 bounds)
    {
        foreach (var opening in _bounds)
        {
            if (bounds.Intersects(opening))
                return CMUZVisibility.Unknown;
        }

        return CMUZVisibility.Hidden;
    }

    public void Clear()
    {
        Reset(MapId.Nullspace, default, dynamicOnly: true);
    }

    private void Reset(MapId mapId, Box2 worldBounds, bool dynamicOnly)
    {
        MapId = mapId;
        WorldBounds = worldBounds;
        DynamicOnly = dynamicOnly;
        Visibility = CMUZVisibility.Hidden;
        _bounds.Clear();
        _intersectionScratch.Clear();
    }

    private void AddBounds(Box2 bounds)
    {
        if (!bounds.Intersects(WorldBounds))
            return;

        var intersection = bounds.Intersect(WorldBounds);
        if (intersection.Width <= 0f || intersection.Height <= 0f)
            return;

        _bounds.Add(intersection);
        Visibility = CMUZVisibility.Unknown;
    }
}
