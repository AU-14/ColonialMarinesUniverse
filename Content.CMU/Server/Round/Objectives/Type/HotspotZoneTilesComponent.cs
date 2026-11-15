using System.Collections.Generic;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Round.Objectives.Type;

/// <summary>
/// Snapshot of the tiles painted under an active hotspot zone, restored when the zone leaves.
/// </summary>
[RegisterComponent]
public sealed partial class HotspotZoneTilesComponent : Component
{
    /// <summary>Grid the tiles were painted on; <see cref="Originals"/> indices are local to it.</summary>
    public EntityUid Grid;

    /// <summary>Pre-paint tile per grid-local position.</summary>
    public Dictionary<Vector2i, Tile> Originals = new();

    /// <summary>The zone tile currently painted, compared by TypeId on restore.</summary>
    public Tile Painted;
}
