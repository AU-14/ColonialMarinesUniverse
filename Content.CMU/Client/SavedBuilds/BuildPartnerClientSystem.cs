// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared._AU14.SavedBuilds;

namespace Content.Client._AU14.SavedBuilds;

/// <summary>
/// Client side of build partners. Opens the <see cref="BuildPartnerWindow"/> from the construction menu's
/// "Partners" button, asks the server for the online-player list, and relays grant/revoke toggles back. The
/// server (BuildPartnerSystem) is authoritative and round-scopes the grants.
/// </summary>
public sealed class BuildPartnerClientSystem : EntitySystem
{
    private BuildPartnerWindow? _window;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<BuildPartnerListEvent>(OnList);
    }

    /// <summary>Opens (or focuses) the partner window and requests a fresh list.</summary>
    public void OpenWindow()
    {
        if (_window is { IsOpen: true })
        {
            _window.MoveToFront();
            RaiseNetworkEvent(new RequestBuildPartnerListEvent());
            return;
        }

        _window = new BuildPartnerWindow();
        _window.OnSetPartner += (user, add) => RaiseNetworkEvent(new SetBuildPartnerEvent { Partner = user, Add = add });
        _window.OnClearAll += () => RaiseNetworkEvent(new ClearBuildPartnersEvent());
        _window.OnClose += () => _window = null;
        _window.OpenCentered();

        RaiseNetworkEvent(new RequestBuildPartnerListEvent());
    }

    private void OnList(BuildPartnerListEvent ev)
    {
        _window?.Populate(ev.Players);
    }
}
