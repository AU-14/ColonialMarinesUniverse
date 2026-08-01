// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._AU14.ZLevelBuilding;

/// <summary>Client → server: open the "Z-Sync Lists" tool (admin-gated, re-validated server-side).</summary>
[Serializable, NetSerializable]
public sealed class RequestOpenZBorderSyncEvent : EntityEventArgs;

/// <summary>One selectable map scope for the Z-Sync tool (a planet map prototype).</summary>
[Serializable, NetSerializable]
public struct ZSyncMapOption
{
    public string Id;
    public string Name;
}

/// <summary>
/// Server → client: the current z-level border-sync lists, per scope. WHITELISTED prototypes are mirrored
/// across z-levels as map borders; the BLACKLIST overrides the whitelist (for walls that share the
/// invincible border parent but are really just structures, e.g. dropship walls). Lists are keyed by map
/// scope: the empty string is the GLOBAL scope (applies on every map), any other key is a planet map
/// prototype id and applies only when that map is the round's planet. Pre-existing saved lists load into
/// the global scope, so nothing saved before scoping existed is lost.
/// </summary>
[Serializable, NetSerializable]
public sealed class OpenZBorderSyncEvent : EntityEventArgs
{
    /// <summary>Scope ("" = global, otherwise planet prototype id) → whitelisted prototype ids.</summary>
    public Dictionary<string, List<string>> Whitelists = new();

    /// <summary>Scope ("" = global, otherwise planet prototype id) → blacklisted prototype ids.</summary>
    public Dictionary<string, List<string>> Blacklists = new();

    /// <summary>Every selectable map scope (planet map prototypes), for the scope picker.</summary>
    public List<ZSyncMapOption> Maps = new();
}

/// <summary>Client → server: add or remove a batch of prototype ids on one of the sync lists.</summary>
[Serializable, NetSerializable]
public sealed class ModifyZBorderSyncEvent : EntityEventArgs
{
    public List<string> ProtoIds = new();

    /// <summary>True = operate on the blacklist, false = the whitelist.</summary>
    public bool Blacklist;

    /// <summary>True = add the ids to the list, false = remove them.</summary>
    public bool Add = true;

    /// <summary>Map scopes to apply the change to. Empty = the GLOBAL scope (all maps).</summary>
    public List<string> MapIds = new();
}

/// <summary>Client -> server: add the prototype of a clicked in-round entity to a z-sync list (global scope).</summary>
[Serializable, NetSerializable]
public sealed class PickZBorderSyncEntityEvent : EntityEventArgs
{
    public NetEntity Entity;
    public bool Blacklist;
}
