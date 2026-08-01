// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._AU14.SavedBuilds;

/// <summary>
/// Which selection/placement ruleset a saved-build action runs under. The server re-validates the caller's
/// admin flags for any privileged mode, so a client asking for a mode it isn't authorized for is rejected.
/// </summary>
[Serializable, NetSerializable]
public enum BuildSaveMode : byte
{
    /// <summary>Default. Select only entities you (or a build partner) built; place via costed construction ghosts.</summary>
    Player = 0,

    /// <summary>Admin (AdminFlags.Spawn): same player-built selection rules, but instant + free placement.</summary>
    Admin = 1,

    /// <summary>Mapper (AdminFlags.Mapping): select ANY world entity (map-placed, admin-spawned, anything) and place instantly + free.</summary>
    Mapper = 2,
}

/// <summary>
/// A square tile-range box used to select build entities. <see cref="Radius"/> is the half-extent in
/// tiles, so a radius of 2 selects a 5x5 area centred on <see cref="Center"/> (max 5 => 11x11).
/// </summary>
[Serializable, NetSerializable]
public struct BuildSelectionBox
{
    public NetCoordinates Center;
    public int Radius;
}

/// <summary>
/// Describes a build selection: the union of any number of range boxes, plus entities the player
/// manually clicked to include or exclude. The server is authoritative — it resolves this descriptor
/// against the soft-whitelist (<see cref="PlayerBuiltComponent"/> + build-partner grants) to produce
/// the actual entity set, both for highlighting and for saving.
/// </summary>
[Serializable, NetSerializable]
public struct BuildSelectionData
{
    public List<BuildSelectionBox> Boxes;
    public List<NetEntity> ManualAdds;
    public List<NetEntity> ManualRemoves;
}

/// <summary>One selected grid tile, used by mapper-mode tile saving.</summary>
[Serializable, NetSerializable]
public struct BuildSelectionTile
{
    public NetEntity Grid;
    public int X;
    public int Y;
    public string Tile;
}

/// <summary>Client -> server: resolve this selection and send back the highlight set.</summary>
[Serializable, NetSerializable]
public sealed class RequestBuildSelectionEvent : EntityEventArgs
{
    public BuildSelectionData Selection;

    /// <summary>Which selection ruleset to resolve under (re-validated server-side against the caller's flags).</summary>
    public BuildSaveMode Mode;

    /// <summary>Mapper mode only: also select unanchored loose items (default false = anchored structures only).</summary>
    public bool IncludeLoose;

    /// <summary>Also select tiles. Player mode is limited to construction-menu-supported tiles; admin/mapper modes can save any tile.</summary>
    public bool IncludeTiles;

    /// <summary>Also scan the linked z-levels above/below the selection box. Off by default: capturing
    /// other levels unasked pulls in structures the builder never meant to select.</summary>
    public bool IncludeMultiZ;
}

/// <summary>Server -> client: the resolved, whitelisted entities to highlight.</summary>
[Serializable, NetSerializable]
public sealed class BuildSelectionResultEvent : EntityEventArgs
{
    public List<NetEntity> Entities = new();
    public List<BuildSelectionTile> Tiles = new();
}

/// <summary>Client -> server: save the resolved selection under <see cref="Name"/>.</summary>
[Serializable, NetSerializable]
public sealed class RequestSaveBuildEvent : EntityEventArgs
{
    public string Name = string.Empty;
    public BuildSelectionData Selection;

    /// <summary>Which selection ruleset to save under (re-validated server-side against the caller's flags).</summary>
    public BuildSaveMode Mode;

    /// <summary>Mapper mode only: also save unanchored loose items (default false = anchored structures only).</summary>
    public bool IncludeLoose;

    /// <summary>Also save tiles. Player mode is limited to construction-menu-supported tiles; admin/mapper modes can save any tile.</summary>
    public bool IncludeTiles;

    /// <summary>Also save from the linked z-levels above/below the selection box. Must match what the
    /// selection preview used, or the save captures more than the highlight showed.</summary>
    public bool IncludeMultiZ;
}

/// <summary>One entity in a build's placement preview: prototype + position relative to the anchor.</summary>
[Serializable, NetSerializable]
public struct BuildPreviewEntity
{
    public string Proto;
    public float X;
    public float Y;
    public float Rot;
    public int Z;
}

/// <summary>One tile in a saved build's placement preview: tile id + position relative to the anchor.</summary>
[Serializable, NetSerializable]
public struct BuildPreviewTile
{
    public string Tile;
    public float X;
    public float Y;
    public int Z;
}

/// <summary>Metadata for one saved build, shown in the "Saved Builds" construction-menu spawnlist.</summary>
[Serializable, NetSerializable]
public struct SavedBuildInfo
{
    /// <summary>The build file name (with extension), used to load it for placement.</summary>
    public string Id;
    public string Name;
    /// <summary>Source grid/map name — used as the menu sub-category.</summary>
    public string Source;
    public string Author;
    public int EntityCount;
    public int TileCount;

    /// <summary>Bounding box of the build relative to its anchor, in tiles — used to draw the placement footprint.</summary>
    public float RelMinX;
    public float RelMinY;
    public float RelMaxX;
    public float RelMaxY;

    /// <summary>Per-entity preview (prototype + offset from anchor) for the placement ghost.</summary>
    public List<BuildPreviewEntity> Preview;

    /// <summary>Per-tile preview (tile id + offset from anchor) for mapper tile saves.</summary>
    public List<BuildPreviewTile> Tiles;

    /// <summary>The grid this build was saved from (for "place at original"). Invalid if unknown/gone.</summary>
    public NetEntity SourceGrid;
    /// <summary>The build's anchor point in the source grid's local frame (for "place at original").</summary>
    public float AnchorX;
    public float AnchorY;
}

/// <summary>
/// Server -> client (requester ONLY): the serialized build produced by a save request. The CLIENT writes
/// this to its own local user-data folder - saved builds are private local files, never stored on or
/// listed by the server, and shared by players copying files between their folders themselves.
/// </summary>
[Serializable, NetSerializable]
public sealed class SavedBuildDataEvent : EntityEventArgs
{
    /// <summary>Suggested file name ("&lt;author&gt;__&lt;name&gt;.build.yml").</summary>
    public string FileName = string.Empty;

    /// <summary>The full saved-build YAML document (meta header + serialized entities).</summary>
    public string Yaml = string.Empty;
}

// -------------------------------------------------------------------------
// Build partners (the "Partners" menu button). A partner you add may include
// YOUR built entities in THEIR saved builds. One-directional, round-scoped.
// -------------------------------------------------------------------------

/// <summary>Client -> server: send me the list of online players and whether each is currently my partner.</summary>
[Serializable, NetSerializable]
public sealed class RequestBuildPartnerListEvent : EntityEventArgs;

/// <summary>One online player in the partner-management window.</summary>
[Serializable, NetSerializable]
public struct BuildPartnerInfo
{
    public NetUserId User;
    public string Name;
    /// <summary>True if this player is currently allowed to include the viewer's builds.</summary>
    public bool IsPartner;
}

/// <summary>Server -> client: the online players (minus the viewer) with their partner status.</summary>
[Serializable, NetSerializable]
public sealed class BuildPartnerListEvent : EntityEventArgs
{
    public List<BuildPartnerInfo> Players = new();
}

/// <summary>Client -> server: grant (<see cref="Add"/> true) or revoke a player's access to my builds.</summary>
[Serializable, NetSerializable]
public sealed class SetBuildPartnerEvent : EntityEventArgs
{
    public NetUserId Partner;
    public bool Add;
}

/// <summary>Client -> server: revoke ALL of my build partners at once. Replies with a fresh list.</summary>
[Serializable, NetSerializable]
public sealed class ClearBuildPartnersEvent : EntityEventArgs;

/// <summary>
/// Client -> server: place a saved build at <see cref="Target"/>, rotated by <see cref="Rotation"/> radians.
/// The build file lives on the CLIENT, so the client uploads its YAML here; the server re-validates the
/// caller's admin flags (Spawn/Mapping) and size caps before loading anything from it.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestPlaceBuildEvent : EntityEventArgs
{
    /// <summary>The local file name, used only for logging/popups.</summary>
    public string Id = string.Empty;

    /// <summary>The full saved-build YAML document read from the client's local file.</summary>
    public string Yaml = string.Empty;

    public NetCoordinates Target;
    public double Rotation;

    /// <summary>If true, ignore <see cref="Target"/> and place at the build's original grid + coordinates.</summary>
    public bool AtOriginal;
}
