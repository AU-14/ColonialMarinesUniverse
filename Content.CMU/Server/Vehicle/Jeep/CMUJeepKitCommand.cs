using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Vehicle.Jeep;

/// <summary>
/// Parks another set of jeeps and the jeep test kit next to a player, on any server.
/// </summary>
[AdminCommand(AdminFlags.Spawn)]
public sealed partial class CMUJeepKitCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPlayerManager _players = default!;

    public string Command => "jeepkit";
    public string Description => "Parks the three jeeps and the jeep test kit on open ground next to a player.";
    public string Help => "Usage: jeepkit [username]\nWithout a name, uses your own character.";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1)
        {
            shell.WriteError(Help);
            return;
        }

        var session = shell.Player;
        if (args.Length == 1 && !_players.TryGetSessionByUsername(args[0], out session))
        {
            shell.WriteError($"No player called {args[0]}.");
            return;
        }

        if (session?.AttachedEntity is not { } player)
        {
            shell.WriteError("Name a player, or run it as a player with a character.");
            return;
        }

        if (!_entities.System<CMUJeepDevKitSystem>().TrySpawnKit(player, out var jeeps, out var error))
        {
            shell.WriteError($"No room for the jeeps: {error}.");
            return;
        }

        shell.WriteLine($"Spawned {string.Join(", ", jeeps.Select(j => _entities.ToPrettyString(j)))}.");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1
            ? CompletionResult.FromHintOptions(CompletionHelper.SessionNames(players: _players), "[username]")
            : CompletionResult.Empty;
    }
}
