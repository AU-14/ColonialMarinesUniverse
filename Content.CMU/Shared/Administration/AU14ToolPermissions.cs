// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using Robust.Shared.Serialization;

namespace Content.Shared._AU14.Administration;

/// <summary>
/// The per-tool editor permissions replacing the old JModEditor / InsforEditor job-whitelist gating.
/// The job-whitelist approach let any admin with the jobwhitelistadd command hand out editor access;
/// these grants can only be managed by Host-flagged admins (through the Tool Permissions window or the
/// toolperm console command), are keyed by ckey so offline players can be granted, and are per tool.
/// Host-flagged admins always pass every tool check without needing a grant.
/// </summary>
public static class AU14ToolPermissions
{
    /// <summary>Construction Items Editor (incl. the right-click editor verbs and recipe hide/change).</summary>
    public const string Construction = "construction";

    /// <summary>Mass Entity Editor (batch recipes and batch tiles).</summary>
    public const string Mass = "mass";

    /// <summary>Tiles Editor.</summary>
    public const string Tiles = "tiles";

    /// <summary>Lathe Editor.</summary>
    public const string Lathe = "lathe";

    /// <summary>Z-Level Toggles (allow/deny z-level building per map).</summary>
    public const string ZLevelToggles = "zleveltoggles";

    /// <summary>Z-Sync Lists (border reflection black/whitelists).</summary>
    public const string ZSync = "zsync";

    /// <summary>INSFOR Faction Editor.</summary>
    public const string Insfor = "insfor";

    /// <summary>Spawnlist Delete (removes a whole spawnlist and every generated recipe in it).</summary>
    public const string SpawnlistDelete = "spawnlistdelete";

    /// <summary>Every grantable tool id, with the loc key for its display name.</summary>
    public static readonly (string Id, string NameLoc)[] AllTools =
    {
        (Construction, "au14-toolperm-tool-construction"),
        (Mass, "au14-toolperm-tool-mass"),
        (Tiles, "au14-toolperm-tool-tiles"),
        (Lathe, "au14-toolperm-tool-lathe"),
        (ZLevelToggles, "au14-toolperm-tool-zleveltoggles"),
        (ZSync, "au14-toolperm-tool-zsync"),
        (Insfor, "au14-toolperm-tool-insfor"),
        (SpawnlistDelete, "au14-toolperm-tool-spawnlistdelete"),
    };

    public static bool IsValidTool(string tool)
    {
        foreach (var (id, _) in AllTools)
        {
            if (id == tool)
                return true;
        }

        return false;
    }
}

/// <summary>Client → server: open the Tool Permissions manager (Host-gated, re-validated server-side).</summary>
[Serializable, NetSerializable]
public sealed class RequestOpenToolPermissionsEvent : EntityEventArgs;

/// <summary>One user's grants in the Tool Permissions window.</summary>
[Serializable, NetSerializable]
public struct ToolPermissionUser
{
    public string Ckey;
    public List<string> Tools;
}

/// <summary>Server → client: everyone with tool grants (Host viewers only).</summary>
[Serializable, NetSerializable]
public sealed class OpenToolPermissionsEvent : EntityEventArgs
{
    public List<ToolPermissionUser> Users = new();
}

/// <summary>Client → server: grant or revoke one tool for one ckey (Host-gated).</summary>
[Serializable, NetSerializable]
public sealed class ModifyToolPermissionEvent : EntityEventArgs
{
    public string Ckey = string.Empty;
    public string Tool = string.Empty;

    /// <summary>True = grant, false = revoke.</summary>
    public bool Grant = true;
}

/// <summary>Client → server: ask for the local player's own tool grants (any player may ask).</summary>
[Serializable, NetSerializable]
public sealed class RequestMyToolPermissionsEvent : EntityEventArgs;

/// <summary>Server → client: the receiving player's own tool grants (drives client-side button pre-checks).</summary>
[Serializable, NetSerializable]
public sealed class MyToolPermissionsEvent : EntityEventArgs
{
    public List<string> Tools = new();
}
