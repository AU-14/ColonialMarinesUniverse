using System.Linq;
using Content.Server.Administration;
using Content.Server.Audio;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Administration;

[AdminCommand(AdminFlags.Moderator)]
public sealed class CMUStopGlobalSoundCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPlayerManager _players = default!;

    public string Command => "stopglobalsound";
    public string Description => Loc.GetString("cmu-stop-global-sound-command-description");
    public string Help => Loc.GetString("cmu-stop-global-sound-command-help");

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var filter = Filter.Empty();
        if (args.Length == 0)
        {
            filter.AddAllPlayers(_players);
        }
        else
        {
            foreach (var username in args)
            {
                if (!_players.TryGetSessionByUsername(username, out var session))
                {
                    shell.WriteError(Loc.GetString("cmu-stop-global-sound-command-player-not-found", ("username", username)));
                    continue;
                }

                filter.AddPlayer(session);
            }
        }

        // Like playglobalsound, only broadcasts affect replay playback.
        _entities.System<ServerGlobalSoundSystem>().StopAdminGlobal(filter, replay: args.Length == 0);
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return CompletionResult.FromHintOptions(
            _players.Sessions.Select(session => session.Name),
            Loc.GetString("cmu-stop-global-sound-command-arg-user"));
    }
}
