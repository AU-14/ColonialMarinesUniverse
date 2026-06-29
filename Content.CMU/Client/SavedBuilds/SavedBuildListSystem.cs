// SPDX-License-Identifier: LicenseRef-AdvancedAtkinsonatorv2-Proprietary
// Copyright (c) 2026 wray-git. All rights reserved.
// Proprietary - reuse only with the Author's prior written authorization. See LICENSE-AdvancedAtkinsonatorv2.md.
using System.Collections.Generic;
using Content.Shared._AU14.SavedBuilds;

namespace Content.Client._AU14.SavedBuilds;

/// <summary>
/// Fetches and caches the server's list of saved builds for the "Saved Builds" construction-menu
/// spawnlist. Consumers call <see cref="Refresh"/> and listen to <see cref="ListUpdated"/>.
/// </summary>
public sealed class SavedBuildListSystem : EntitySystem
{
    /// <summary>The most recently received saved-build list.</summary>
    public IReadOnlyList<SavedBuildInfo> Builds => _builds;
    private List<SavedBuildInfo> _builds = new();

    /// <summary>Raised when a fresh list arrives from the server.</summary>
    public event Action? ListUpdated;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<SavedBuildListEvent>(OnList);
    }

    /// <summary>Asks the server for the current saved-build list.</summary>
    public void Refresh()
    {
        RaiseNetworkEvent(new RequestSavedBuildListEvent());
    }

    /// <summary>Asks the server to delete a saved build (server re-validates admin / author). The server
    /// responds with a refreshed list, which fires <see cref="ListUpdated"/>.</summary>
    public void Delete(string id)
    {
        if (!string.IsNullOrEmpty(id))
            RaiseNetworkEvent(new RequestDeleteSavedBuildEvent { Id = id });
    }

    /// <summary>Asks the server (host) to open the saved-builds folder in the OS file explorer (admin only).</summary>
    public void OpenFolder()
    {
        RaiseNetworkEvent(new RequestOpenSavedBuildsFolderEvent());
    }

    private void OnList(SavedBuildListEvent ev)
    {
        _builds = ev.Builds;
        ListUpdated?.Invoke();
    }
}
