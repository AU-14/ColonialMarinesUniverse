// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
namespace Content.Shared._AU14.Xenos;

/// <summary>
/// A resin floor the hive can spin across a hole left by a cave-in.
///
/// The tile underneath matters more than it looks: an anchored entity cannot exist on an empty tile at all
/// (AddToSnapGridCell refuses one), so a resin patch over a collapsed floor has to LAY a tile before it can
/// anchor itself there. It puts <see cref="Tile"/> down on spawn and, if it was the one that placed it, tears
/// it back out when destroyed - so cutting the resin reopens the hole rather than leaving free floor behind.
/// </summary>
[RegisterComponent]
public sealed partial class ResinFloorPatchComponent : Component
{
    /// <summary>Tile laid under the resin when it is built over open space.</summary>
    [DataField]
    public string Tile = "Plating";

    /// <summary>True when this patch created the tile beneath it, and so should remove it again on death.
    /// False when it was built over existing floor, which must survive the resin being cut.</summary>
    [ViewVariables]
    public bool PlacedTile;
}
