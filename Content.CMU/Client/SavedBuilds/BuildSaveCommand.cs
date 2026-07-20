// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
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
