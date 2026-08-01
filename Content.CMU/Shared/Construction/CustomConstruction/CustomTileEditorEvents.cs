// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System.Collections.Generic;
using Robust.Shared.Serialization;

namespace Content.Shared._AU14.Construction.CustomConstruction;

/// <summary>
/// Server -> client: open the TILE editor (the "Tiles" sibling of the Construction Items Editor). Lets a
/// permitted admin add a tile to the construction menu; the server turns the chosen tile into a buildable
/// tile-applier entity automatically. Sent in response to <see cref="RequestOpenCustomTileEditorEvent"/>.
/// </summary>
[Serializable, NetSerializable]
public sealed class OpenCustomTileEditorEvent : EntityEventArgs
{
    /// <summary>Every content tile id (ContentTileDefinition) the admin can pick from.</summary>
    public List<string> AvailableTiles = new();

    /// <summary>Existing non-z-level spawnlists, for the Spawnlists-page case.</summary>
    public List<string> AvailableSpawnlists = new();
}

/// <summary>Client -> server: the admin pressed the in-menu "Tiles Editor" button. The server re-checks permission.</summary>
[Serializable, NetSerializable]
public sealed class RequestOpenCustomTileEditorEvent : EntityEventArgs
{
}

/// <summary>
/// Client -> server: the admin confirmed the tile editor. The server validates, writes the generated
/// tile-applier + recipe, and pops up the result.
/// </summary>
[Serializable, NetSerializable]
public sealed class SubmitCustomTileEditorEvent : EntityEventArgs
{
    /// <summary>The ContentTileDefinition id to lay (e.g. FloorSteel, Plating).</summary>
    public string TileId = string.Empty;

    /// <summary>Material stack id consumed to lay one tile (e.g. CMSteel).</summary>
    public string Material = "CMSteel";

    /// <summary>How many sheets of <see cref="Material"/> one tile costs.</summary>
    public int Amount = 1;

    /// <summary>Category the recipe is filed under within its spawnlist.</summary>
    public string Category = "Flooring";

    /// <summary>Spawnlist for the Spawnlists-page case (ignored when <see cref="ZLevelPage"/> is true).</summary>
    public string Spawnlist = "AU14";

    /// <summary>
    /// Which MAIN CATEGORY (top-bar page) the tile goes on: true = the "Z-Level (Experimental)" page (filed in
    /// the "Tiles" spawnlist), false = the normal Spawnlists page (filed in <see cref="Spawnlist"/>).
    /// </summary>
    public bool ZLevelPage = true;
}
