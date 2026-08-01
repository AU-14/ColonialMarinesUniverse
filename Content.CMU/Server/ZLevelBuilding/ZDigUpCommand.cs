// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._AU14.ZLevelBuilding;

/// <summary>
/// Building overhaul (z-level): digs straight UP one level from where you are standing. You surface at the same
/// world x/y, so where you come up reflects how far you travelled underground. Blocked if a solid wall sits
/// directly above the spot (dig somewhere without a wall above instead).
///
/// (A proper in-world digging tool/interaction is a later polish step; this drives the same
/// <see cref="ZLevelBuildingSystem.DigUp"/> pipeline.)
/// </summary>
[AdminCommand(AdminFlags.Debug)]
public sealed class ZDigUpCommand : IConsoleCommand
{
    public string Command => "au_digup";
    public string Description => "Dig straight up one z-level, surfacing at your current horizontal position.";
    public string Help => "au_digup";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player?.AttachedEntity is not { } player)
        {
            shell.WriteError("This command must be run by an in-game player.");
            return;
        }

        var system = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<ZLevelBuildingSystem>();
        if (system.DigUp(player))
            shell.WriteLine("Dug up a level.");
        else
            shell.WriteError("Could not dig up here (nothing above, a wall blocks the spot above, or the feature is disabled).");
    }
}
