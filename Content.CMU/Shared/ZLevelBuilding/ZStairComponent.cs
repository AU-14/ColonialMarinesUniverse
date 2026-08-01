// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using Robust.Shared.Maths;

namespace Content.Shared._AU14.ZLevelBuilding;

/// <summary>
/// Building overhaul (z-level): setup marker for a buildable staircase.
///
/// Walk-through traversal is handled by the inherited <c>CMUZLevelHighGround</c> component; this component
/// carries only the setup parameters used by <c>ZStairSystem</c> on <c>MapInitEvent</c>.
///
/// UP stair (direction +1): ensures level above, lays a standing platform there, and places a companion stair
/// so the player can walk back down.
/// DOWN stair (direction -1): generates a stone underground level (like au_digdown), clears a landing pocket
/// at the stair position, and places a companion stair at the landing spot.
/// </summary>
[RegisterComponent]
public sealed partial class ZStairComponent : Component
{
    /// <summary>Which way the stair leads: +1 = up a level, -1 = down a level.</summary>
    [DataField]
    public int Direction = 1;

    /// <summary>Tile offset from the stair where the support beam is placed on the current level (north of it).</summary>
    [DataField]
    public Vector2i BeamOffset = new(0, 1);

    /// <summary>Support beam structure placed next to the stair on the current level.</summary>
    [DataField]
    public string ReflectBeam = "AU14NavalisSupportBeamBlue1Tile";

    /// <summary>Floor tile laid as a standing platform on the UPPER level (so there is somewhere to stand).</summary>
    [DataField]
    public string ReflectFloorTile = "Plating";

    /// <summary>
    /// Radius (in tiles) of the standing platform laid on the UPPER level around the staircase. 1 = a 3x3 platform.
    /// Only empty tiles are filled, so it never overwrites existing floor or structures.
    /// </summary>
    [DataField]
    public int PlatformRadius = 1;

    /// <summary>
    /// Prototype spawned on the CONNECTED level as the return stair. Must carry NO ZStairComponent to prevent
    /// recursive setup on its own MapInit. Defaults to the plain walk-through companion stair.
    /// </summary>
    [DataField]
    public string PartnerProto = "AU14ZStairPure";
}
