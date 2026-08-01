// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._AU14.ZLevelBuilding;

/// <summary>
/// Building overhaul (z-level), Phase 2 test/driver command: digs straight down from where you are standing.
/// On the first dig over a map, this lazily creates a stone level below and links it into a z-network - so it
/// works on ANY map, including ones that were not authored as multi-z. Run again to keep descending.
///
/// (A proper in-world digging tool/interaction is a later polish step; this command drives the same
/// <see cref="ZLevelBuildingSystem.DigDown"/> pipeline.)
/// </summary>
[AdminCommand(AdminFlags.Debug)]
public sealed class ZDigDownCommand : IConsoleCommand
{
    public string Command => "au_digdown";
    public string Description => "Dig straight down, creating/descending into a stone z-level beneath you.";
    public string Help => "au_digdown";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player?.AttachedEntity is not { } player)
        {
            shell.WriteError("This command must be run by an in-game player.");
            return;
        }

        var system = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<ZLevelBuildingSystem>();
        if (system.DigDown(player))
            shell.WriteLine("Dug down a level.");
        else
            shell.WriteError("Could not dig down here (map opted out, feature disabled, or a hand-authored level is already below).");
    }
}
