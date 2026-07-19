// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using Content.Client.Administration.Managers;
using Content.Shared._AU14.Administration;
using Content.Shared.Administration;
using Content.Shared.Popups;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client._AU14.Administration;

/// <summary>
/// Client side of the per-tool editor permissions (see <see cref="AU14ToolPermissions"/>). Caches the
/// local player's own grants (requested when the player attaches, pushed by the server when a host
/// changes them live) so tool buttons can pre-check without a round trip, and drives the Host-only
/// Tool Permissions manager window. The server re-validates everything.
/// </summary>
public sealed class ToolPermissionClientSystem : EntitySystem
{
    [Dependency] private readonly IClientAdminManager _admin = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private readonly HashSet<string> _myTools = new();
    private ToolPermissionsWindow? _window;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<MyToolPermissionsEvent>(OnMine);
        SubscribeNetworkEvent<OpenToolPermissionsEvent>(OnOpen);
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttached);

        // Re-fetch grants whenever admin status flips (admin/deadmin/readmin), so a deadmined host or a
        // freshly granted player has working tool buttons without rejoining.
        _admin.AdminStatusUpdated += RequestMyGrants;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _admin.AdminStatusUpdated -= RequestMyGrants;
    }

    private void OnPlayerAttached(LocalPlayerAttachedEvent ev)
    {
        RequestMyGrants();
    }

    private void RequestMyGrants()
    {
        RaiseNetworkEvent(new RequestMyToolPermissionsEvent());
    }

    private void OnMine(MyToolPermissionsEvent ev)
    {
        _myTools.Clear();
        _myTools.UnionWith(ev.Tools);
    }

    /// <summary>Client-side pre-check mirroring the server gate: Host admin or a per-tool grant.</summary>
    public bool CanUse(string tool)
    {
        return _admin.HasFlag(AdminFlags.Host) || _myTools.Contains(tool);
    }

    /// <summary>Admin Tools > Tool Permissions: Host-only manager for per-tool ckey grants.</summary>
    public void OpenManager()
    {
        if (!_admin.HasFlag(AdminFlags.Host))
        {
            _popup.PopupCursor(Loc.GetString("construction-menu-editor-not-admin"), PopupType.MediumCaution);
            return;
        }

        RaiseNetworkEvent(new RequestOpenToolPermissionsEvent());
    }

    private void OnOpen(OpenToolPermissionsEvent ev)
    {
        // The server re-sends the grants after every change; refresh the open window in place.
        if (_window is { IsOpen: true })
        {
            _window.Populate(ev);
            return;
        }

        _window = new ToolPermissionsWindow();
        _window.OnModify += modify => RaiseNetworkEvent(modify);
        _window.OnClose += () => _window = null;
        _window.Populate(ev);
        _window.OpenCentered();
    }
}
