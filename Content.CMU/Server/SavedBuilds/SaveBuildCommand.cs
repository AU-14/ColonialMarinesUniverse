// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._AU14.SavedBuilds;

/// <summary>
/// Test/dev command for the saved-builds save pipeline: serializes the player-built entities in a box
/// around you to a user-data file. Usage: <c>savebuild "My Base" [radius]</c> (radius 0-5, default 2).
/// The proper selection overlay drives the same <see cref="SavedBuildSystem"/> save path.
/// </summary>
[AnyCommand]
public sealed class SaveBuildCommand : IConsoleCommand
{
    public string Command => "savebuild";
    public string Description => "Save the player-built entities in a box around you to a shareable file.";
    public string Help => "savebuild <name> [radius 0-5]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError("This command can only be run by a player.");
            return;
        }

        if (args.Length < 1)
        {
            shell.WriteLine(Help);
            return;
        }

        var radius = 2;
        if (args.Length >= 2 && !int.TryParse(args[1], out radius))
        {
            shell.WriteError("Radius must be a number.");
            return;
        }

        var system = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<SavedBuildSystem>();
        system.SaveAroundPlayer(player, args[0], radius);
    }
}
