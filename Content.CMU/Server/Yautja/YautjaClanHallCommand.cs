using System.Threading.Tasks;
using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Server.EUI;
using Content.Shared.Administration;
using Content.Shared.CMU14.Yautja;
using Robust.Shared.Console;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Yautja;

public static class YautjaClanHallAccess
{
    // whitelisted hunters (from the lobby or a ghost too), anyone with a clan profile, whoever is
    // wearing a yautja body right now (youngbloods), and Clans staff
    public static async Task<bool> CanOpen(
        ICommonSession player,
        IAdminManager admin,
        IServerDbManager db,
        IEntityManager entities)
    {
        if (admin.HasAdminFlag(player, AdminFlags.Clans))
            return true;

        if (player.AttachedEntity is { } body && entities.HasComponent<YautjaComponent>(body))
            return true;

        if (await db.GetYautjaWhitelistFlagsAsync(player.UserId.UserId) != (int) YautjaWhitelistFlags.None)
            return true;

        return await db.GetYautjaClanMemberAsync(player.UserId.UserId) != null;
    }
}

[AnyCommand]
public sealed partial class YautjaClanHallCommand : LocalizedCommands
{
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private EuiManager _eui = default!;

    public override string Command => "clanhall";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        if (!await YautjaClanHallAccess.CanOpen(player, _admin, _db, _entities))
        {
            shell.WriteError(Loc.GetString("cmd-clanhall-denied"));
            return;
        }

        _eui.OpenEui(new YautjaClanHallEui(), player);
    }
}
