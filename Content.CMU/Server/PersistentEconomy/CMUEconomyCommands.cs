using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;

namespace Content.Server.CMU14.PersistentEconomy;

[AnyCommand]
public sealed class CMUBankCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entities = default!;
    public string Command => "cmubank";
    public string Description => Loc.GetString("cmu-economy-command-description");
    public string Help => Command;
    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is { } player)
            _entities.System<CMUPersistentEconomySystem>().Open(player);
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed class CMUEconomyCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPlayerManager _players = default!;
    public string Command => "economy";
    public string Description => Loc.GetString("cmu-economy-admin-description");
    public string Help => "economy balance|history|round <player>; economy add|remove <player> <amount> <reason>; economy stats";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var economy = _entities.System<CMUPersistentEconomySystem>();
        if (args.Length == 1 && args[0] == "stats")
        {
            shell.WriteLine(economy.Store.Statistics());
            return;
        }
        if (args.Length < 2)
        {
            shell.WriteError(Help);
            return;
        }
        if (!Guid.TryParse(args[1], out var player))
        {
            if (!_players.TryGetSessionByUsername(args[1], out var session))
            {
                shell.WriteError(Loc.GetString("shell-target-player-does-not-exist"));
                return;
            }
            player = session.UserId.UserId;
        }
        switch (args[0])
        {
            case "balance":
                shell.WriteLine(System.Text.Json.JsonSerializer.Serialize(economy.Store.ReadAccount(player)));
                return;
            case "round":
                shell.WriteLine(System.Text.Json.JsonSerializer.Serialize(economy.Store.ReadRound(player, economy.RoundId)));
                return;
            case "history":
                foreach (var entry in economy.Store.History(player))
                    shell.WriteLine($"{entry.Timestamp} {entry.Type} {entry.Amount:+#;-#;0} {entry.Before} → {entry.After}: {entry.Description}");
                return;
            case "add":
            case "remove":
                if (args.Length < 4 || !long.TryParse(args[2], out var amount) || amount <= 0 || amount > CMUEconomyStore.MaximumBalance)
                    break;
                var reason = $"{shell.Player?.UserId.ToString() ?? "server"}: {string.Join(" ", args.Skip(3))}";
                var ok = economy.Store.Mutate(player, economy.RoundId, $"admin:{Guid.NewGuid()}", op =>
                    op.Change(args[0] == "add" ? amount : -amount, "AdminAdjustment", reason));
                shell.WriteLine(Loc.GetString(ok ? "cmu-economy-ok" : "cmu-economy-rejected"));
                return;
        }
        shell.WriteError(Help);
    }
}
