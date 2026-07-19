// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
namespace Content.Shared._AU14.ZLevelBuilding;

/// <summary>
/// Building overhaul (z-level): the result of "constructing a tile" from the construction menu's Tiles section.
/// Tiles are not entities, so the construction recipe builds this tiny applier entity instead; on spawn it sets
/// the underlying grid tile to <see cref="Tile"/> and then deletes itself, leaving just the tile. This lets tiles
/// be placed through the normal construction-ghost flow (handy for building floors in open air).
/// </summary>
[RegisterComponent]
public sealed partial class TileApplierComponent : Component
{
    /// <summary>The content tile prototype id to lay down where this entity is placed.</summary>
    [DataField]
    public string Tile = "Plating";
}
