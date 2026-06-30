// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._AU14.SavedBuilds;

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

/// <summary>Client -> server: resolve this selection and send back the highlight set.</summary>
[Serializable, NetSerializable]
public sealed class RequestBuildSelectionEvent : EntityEventArgs
{
    public BuildSelectionData Selection;
}

/// <summary>Server -> client: the resolved, whitelisted entities to highlight.</summary>
[Serializable, NetSerializable]
public sealed class BuildSelectionResultEvent : EntityEventArgs
{
    public List<NetEntity> Entities = new();
}

/// <summary>Client -> server: save the resolved selection under <see cref="Name"/>.</summary>
[Serializable, NetSerializable]
public sealed class RequestSaveBuildEvent : EntityEventArgs
{
    public string Name = string.Empty;
    public BuildSelectionData Selection;
}

/// <summary>One entity in a build's placement preview: prototype + position relative to the anchor.</summary>
[Serializable, NetSerializable]
public struct BuildPreviewEntity
{
    public string Proto;
    public float X;
    public float Y;
    public float Rot;
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

    /// <summary>Bounding box of the build relative to its anchor, in tiles — used to draw the placement footprint.</summary>
    public float RelMinX;
    public float RelMinY;
    public float RelMaxX;
    public float RelMaxY;

    /// <summary>Per-entity preview (prototype + offset from anchor) for the placement ghost.</summary>
    public List<BuildPreviewEntity> Preview;

    /// <summary>The grid this build was saved from (for "place at original"). Invalid if unknown/gone.</summary>
    public NetEntity SourceGrid;
    /// <summary>The build's anchor point in the source grid's local frame (for "place at original").</summary>
    public float AnchorX;
    public float AnchorY;
}

/// <summary>Client -> server: list the saved builds available on the server.</summary>
[Serializable, NetSerializable]
public sealed class RequestSavedBuildListEvent : EntityEventArgs;

/// <summary>Server -> client: the available saved builds.</summary>
[Serializable, NetSerializable]
public sealed class SavedBuildListEvent : EntityEventArgs
{
    public List<SavedBuildInfo> Builds = new();
}

/// <summary>
/// Client -> server: delete a saved build file. The server re-validates permission (admin, or the build's
/// own author) before deleting, then sends the requester a refreshed list.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestDeleteSavedBuildEvent : EntityEventArgs
{
    public string Id = string.Empty;
}

/// <summary>
/// Client -> server: open the saved-builds folder in the host OS file explorer (admin only). On a localhost
/// host this opens on the player's own machine; on a remote/headless server it no-ops (no desktop).
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestOpenSavedBuildsFolderEvent : EntityEventArgs;

/// <summary>Client -> server: place a saved build at <see cref="Target"/>, rotated by <see cref="Rotation"/> radians.</summary>
[Serializable, NetSerializable]
public sealed class RequestPlaceBuildEvent : EntityEventArgs
{
    public string Id = string.Empty;
    public NetCoordinates Target;
    public double Rotation;

    /// <summary>If true, ignore <see cref="Target"/> and place at the build's original grid + coordinates.</summary>
    public bool AtOriginal;
}
