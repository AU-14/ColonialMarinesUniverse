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

/// <summary>
/// Server → client: the current z-level border-sync lists. WHITELISTED prototypes are mirrored across
/// z-levels as map borders; the BLACKLIST overrides the whitelist (for walls that share the invincible
/// border parent but are really just structures, e.g. dropship walls). The default CMBaseWallInvincible
/// family rule is materialized as explicit whitelist entries so it is visible and editable in the menu.
/// </summary>
[Serializable, NetSerializable]
public sealed class OpenZBorderSyncEvent : EntityEventArgs
{
    public List<string> Whitelist = new();
    public List<string> Blacklist = new();
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
}

/// <summary>Client -> server: add the prototype of a clicked in-round entity to a z-sync list.</summary>
[Serializable, NetSerializable]
public sealed class PickZBorderSyncEntityEvent : EntityEventArgs
{
    public NetEntity Entity;
    public bool Blacklist;
}
