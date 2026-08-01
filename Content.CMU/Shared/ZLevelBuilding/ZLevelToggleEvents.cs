// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.Serialization;

namespace Content.Shared._AU14.ZLevelBuilding;

/// <summary>
/// Client -> server: the admin pressed the construction menu's "Z-Level Toggles" tool. The server re-checks
/// permission and answers with <see cref="OpenZLevelTogglesEvent"/>.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestOpenZLevelTogglesEvent : EntityEventArgs
{
}

/// <summary>One map's row in the Z-Level Toggles window.</summary>
[Serializable, NetSerializable]
public sealed class ZLevelToggleEntry
{
    /// <summary>GameMapPrototype id (stable across rounds; what the toggle is persisted under).</summary>
    public string MapProtoId = string.Empty;

    /// <summary>The map's display name.</summary>
    public string MapName = string.Empty;

    /// <summary>Whether z-level building (stairs, beams, digging) is allowed on this map.</summary>
    public bool Enabled = true;

    /// <summary>Whether this map is loaded in the CURRENT round (a toggle applies to it immediately).</summary>
    public bool Loaded;
}

/// <summary>
/// Server -> requesting admin only: open the Z-Level Toggles window listing every game-map prototype with its
/// current allow/deny state.
/// </summary>
[Serializable, NetSerializable]
public sealed class OpenZLevelTogglesEvent : EntityEventArgs
{
    public List<ZLevelToggleEntry> Maps = new();
}

/// <summary>
/// Client -> server: the admin flipped one map's z-building toggle. The server re-checks permission, persists
/// the choice across rounds/restarts, and applies it live to the map if it is loaded this round.
/// </summary>
[Serializable, NetSerializable]
public sealed class SetZLevelToggleEvent : EntityEventArgs
{
    public string MapProtoId = string.Empty;
    public bool Enabled = true;
}
