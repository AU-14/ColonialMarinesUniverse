using Content.Server._RMC14.Xenonids.Watch;
using Content.Shared._CMU14.Xenonids.Watch;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Network;

namespace Content.Server._CMU14.Xenonids.Watch;

[AnyCommand]
internal sealed partial class CMUXenoWatchEntityCommand : LocalizedEntityCommands
{
    [Dependency] private XenoWatchSystem _xenoWatch = default!;

    public override string Command => CMUXenoWatchCommand.CommandName;

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1 || shell.Player is not { } player)
            return;

        if (!NetEntity.TryParse(args[0], out var target))
            return;

        _xenoWatch.XenoWatchRequest(player, target);
    }
}
