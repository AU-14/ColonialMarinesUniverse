// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using Robust.Shared.Maths;

namespace Content.Shared._AU14.ZLevelBuilding;

/// <summary>
/// Building overhaul (z-level): placed on the support beam that a staircase spawns with, linking it back to the
/// staircase it holds up. When the beam is destroyed, <c>ZStairSystem</c> brings down the whole staircase: it
/// deletes the stair (and its companion on the connected level) and clears the standing platform tiles, so
/// knocking out the support actually collapses the structure it was holding up.
/// </summary>
[RegisterComponent]
public sealed partial class ZStairBeamLinkComponent : Component
{
    /// <summary>The staircase entity this beam supports.</summary>
    public EntityUid Stair;

    /// <summary>The companion/traversal stair on the connected level (down stairs), if any.</summary>
    public EntityUid Companion;

    /// <summary>The grid carrying the standing platform (up stairs).</summary>
    public EntityUid PlatformGrid;

    /// <summary>Centre tile of the standing platform on <see cref="PlatformGrid"/>.</summary>
    public Vector2i PlatformCenter;

    /// <summary>Radius of the platform ring laid around the staircase.</summary>
    public int PlatformRadius;

    /// <summary>True for up stairs, which laid a platform that should be removed when the beam falls.</summary>
    public bool HasPlatform;

    /// <summary>
    /// The exact platform tiles the stair LAID (pre-existing floor is skipped when laying). Beam destruction
    /// clears only these, so it can never delete player-built or mapped floor inside the platform radius.
    /// </summary>
    public readonly List<Vector2i> LaidTiles = new();
}
