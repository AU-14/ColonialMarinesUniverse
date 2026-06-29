// SPDX-License-Identifier: LicenseRef-AdvancedAtkinsonatorv2-Proprietary
// Copyright (c) 2026 wray-git. All rights reserved.
// Proprietary - reuse only with the Author's prior written authorization. See LICENSE-AdvancedAtkinsonatorv2.md.
using Robust.Shared.Console;

namespace Content.Client._AU14.SavedBuilds;

/// <summary>
/// Opens (or closes) the saved-build selection panel and enters selection mode. Bindable to a key.
/// </summary>
public sealed class BuildSaveCommand : IConsoleCommand
{
    public string Command => "buildsave";
    public string Description => "Open the build-save selection panel.";
    public string Help => "buildsave";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        IoCManager.Resolve<IEntitySystemManager>()
            .GetEntitySystem<BuildSaveModeSystem>()
            .ToggleWindow();
    }
}
