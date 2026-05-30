using System.Numerics;
using Robust.Shared.Map;

namespace Content.Client._CMU14.ZLevels.Lighting;

/// <summary>
/// Marker component for client-only projected light entities.
/// These entities exist on the receiving map and carry a standard PointLightComponent
/// whose parameters are derived from a source light on an adjacent Z-level.
/// </summary>
[RegisterComponent]
public sealed partial class CMUProjectedLightComponent : Component
{
    /// <summary>
    /// The source light entity on the adjacent Z-level that this projection represents.
    /// Used as the cache key for the source-to-projected mapping.
    /// </summary>
    public EntityUid SourceLight;

    /// <summary>
    /// The world-space center of the opening tile on the receiving map
    /// where this projected light is positioned.
    /// </summary>
    public Vector2 OpeningCenter;

    /// <summary>
    /// The MapId of the source map, used for invalidation when Z-networks change.
    /// </summary>
    public MapId SourceMapId;

    /// <summary>
    /// Depth offset from the viewer's Z-level. Negative values are below, positive values are above.
    /// </summary>
    public int DepthOffset;

    /// <summary>
    /// Tracks the last frame number this projected light was confirmed active.
    /// Stale projected lights are deleted during cleanup.
    /// </summary>
    public uint LastActiveFrame;

    /// <summary>
    /// The receiving map this projected light was last positioned on.
    /// </summary>
    public MapId LastAppliedMapId = MapId.Nullspace;

    /// <summary>
    /// The receiving-map world position last applied to the transform.
    /// </summary>
    public Vector2 LastAppliedCenter;
}
