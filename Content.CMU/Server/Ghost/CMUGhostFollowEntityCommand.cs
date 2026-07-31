// ReSharper disable CheckNamespace

using Content.Shared._CMU14.Ghost;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Network;

namespace Content.Server.Ghost;

[AnyCommand]
internal sealed partial class CMUGhostFollowEntityCommand : LocalizedEntityCommands
{
    public const string CommandName = CMUGhostFollowCommand.CommandName;

    [Dependency] private GhostSystem _ghost = default!;

    public override string Command => CommandName;

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1 || shell.Player is not { } player)
            return;

        if (!NetEntity.TryParse(args[0], out var target))
            return;

        _ghost.GhostFollowRequest(player, target);
    }
}
