// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Server.Administration.Managers;
using Content.Shared._AU14.Administration;
using Content.Shared.Administration;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Robust.Shared.ContentPack;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._AU14.Administration;

/// <summary>
/// Per-tool editor permissions by ckey (see <see cref="AU14ToolPermissions"/>). Replaces the old
/// JModEditor / InsforEditor job-whitelist gating: only Host-flagged admins can grant or revoke, grants
/// work for offline players (they are plain ckeys), and each tool is granted separately. Persisted in the
/// server user-data folder as "ckey:tool" lines, one grant per line.
/// </summary>
public sealed class AU14ToolPermissionSystem : EntitySystem
{
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly IResourceManager _resource = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;

    private static readonly ResPath SaveFile = new("/au14_tool_permissions.txt");

    // ckey (lowercased) → granted tool ids.
    private readonly Dictionary<string, HashSet<string>> _grants = new(StringComparer.OrdinalIgnoreCase);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RequestOpenToolPermissionsEvent>(OnRequestOpen);
        SubscribeNetworkEvent<ModifyToolPermissionEvent>(OnModify);
        SubscribeNetworkEvent<RequestMyToolPermissionsEvent>(OnRequestMine);
        LoadGrants();
    }

    /// <summary>Whether this session's ckey has been granted the given tool. Host admins pass every
    /// tool check without a grant (checked by callers alongside this).</summary>
    public bool HasGrant(ICommonSession session, string tool)
    {
        return _grants.TryGetValue(session.Name, out var tools) && tools.Contains(tool);
    }

    private bool IsManager(ICommonSession session)
    {
        return _adminManager.HasAdminFlag(session, AdminFlags.Host);
    }

    private void OnRequestOpen(RequestOpenToolPermissionsEvent msg, EntitySessionEventArgs args)
    {
        if (!IsManager(args.SenderSession))
            return;

        RaiseNetworkEvent(BuildOpenEvent(), args.SenderSession);
    }

    private void OnRequestMine(RequestMyToolPermissionsEvent msg, EntitySessionEventArgs args)
    {
        RaiseNetworkEvent(BuildMineEvent(args.SenderSession.Name), args.SenderSession);
    }

    private void OnModify(ModifyToolPermissionEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!IsManager(session))
            return;

        var ckey = msg.Ckey.Trim();
        if (ckey.Length == 0 || !AU14ToolPermissions.IsValidTool(msg.Tool))
            return;

        if (SetGrant(ckey, msg.Tool, msg.Grant))
        {
            SaveGrants();
            _adminLog.Add(LogType.Action, LogImpact.High,
                $"{session.Name} {(msg.Grant ? "granted" : "revoked")} editor tool '{msg.Tool}' {(msg.Grant ? "to" : "from")} ckey {ckey}");

            // If the target is online, push their fresh grants so their client buttons update live
            // (case-insensitive scan: the granter may not have typed the ckey's exact casing).
            foreach (var target in _player.Sessions)
            {
                if (string.Equals(target.Name, ckey, StringComparison.OrdinalIgnoreCase))
                {
                    RaiseNetworkEvent(BuildMineEvent(target.Name), target);
                    break;
                }
            }
        }

        RaiseNetworkEvent(BuildOpenEvent(), session);
    }

    /// <summary>Grants or revokes one tool for one ckey. Returns whether anything changed.</summary>
    public bool SetGrant(string ckey, string tool, bool grant)
    {
        if (grant)
        {
            if (!_grants.TryGetValue(ckey, out var tools))
                _grants[ckey] = tools = new HashSet<string>(StringComparer.Ordinal);

            return tools.Add(tool);
        }

        if (!_grants.TryGetValue(ckey, out var existing) || !existing.Remove(tool))
            return false;

        if (existing.Count == 0)
            _grants.Remove(ckey);

        return true;
    }

    public IReadOnlyDictionary<string, HashSet<string>> AllGrants => _grants;

    /// <summary>Persist after direct <see cref="SetGrant"/> calls (the console command path).</summary>
    public void Save() => SaveGrants();

    private OpenToolPermissionsEvent BuildOpenEvent()
    {
        var ev = new OpenToolPermissionsEvent();
        foreach (var (ckey, tools) in _grants.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            ev.Users.Add(new ToolPermissionUser
            {
                Ckey = ckey,
                Tools = tools.OrderBy(t => t, StringComparer.Ordinal).ToList(),
            });
        }

        return ev;
    }

    private MyToolPermissionsEvent BuildMineEvent(string ckey)
    {
        var ev = new MyToolPermissionsEvent();
        if (_grants.TryGetValue(ckey, out var tools))
            ev.Tools = tools.ToList();

        return ev;
    }

    private void LoadGrants()
    {
        _grants.Clear();
        try
        {
            if (!_resource.UserData.Exists(SaveFile))
                return;

            using var reader = _resource.UserData.OpenText(SaveFile);
            while (reader.ReadLine() is { } line)
            {
                var sep = line.IndexOf(':');
                if (sep <= 0 || sep >= line.Length - 1)
                    continue;

                var ckey = line[..sep].Trim();
                var tool = line[(sep + 1)..].Trim();
                if (ckey.Length == 0 || !AU14ToolPermissions.IsValidTool(tool))
                    continue;

                SetGrant(ckey, tool, grant: true);
            }
        }
        catch (Exception e)
        {
            Log.Error($"Failed to load tool permissions: {e}");
        }
    }

    private void SaveGrants()
    {
        try
        {
            using var writer = _resource.UserData.OpenWriteText(SaveFile);
            foreach (var (ckey, tools) in _grants.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
            {
                foreach (var tool in tools.OrderBy(t => t, StringComparer.Ordinal))
                    writer.WriteLine($"{ckey}:{tool}");
            }
        }
        catch (Exception e)
        {
            Log.Error($"Failed to save tool permissions: {e}");
        }
    }
}
