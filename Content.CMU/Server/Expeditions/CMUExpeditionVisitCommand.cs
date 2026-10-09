using System.Numerics;
using Content.Server.Administration;
using Content.Server.GameTicking;
using Content.Shared.Administration;
using Content.Shared.CMU14.Expeditions;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Enums;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

/// <summary>Enter a ready expedition for environment inspection, including on a dummy-ticker server.</summary>
[AdminCommand(AdminFlags.Admin)]
public sealed partial class CMUExpeditionVisitCommand : LocalizedEntityCommands
{
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override string Command => "cmu-expedition-visit";
    public override string Description => Loc.GetString("cmd-cmu-expedition-visit-desc");
    public override string Help => Loc.GetString("cmd-cmu-expedition-visit-help");

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 1 or > 2 || !int.TryParse(args[0], out var number) ||
            !_map.MapExists(new MapId(number)))
        {
            shell.WriteError(Help);
            return;
        }
        var mapUid = _map.GetMap(new MapId(number));
        if (!EntityManager.TryGetComponent<CMUExpeditionMapComponent>(mapUid, out var expedition))
        {
            shell.WriteError(Loc.GetString("cmu-expedition-not-map"));
            return;
        }
        if (!expedition.Ready)
        {
            shell.WriteError(Loc.GetString("cmu-expedition-not-ready"));
            return;
        }
        var player = shell.Player;
        if (args.Length == 2)
            _players.TryGetSessionByUsername(args[1], out player);
        if (player == null || player.Status != SessionStatus.InGame)
        {
            shell.WriteError(Loc.GetString("cmu-expedition-visit-no-player"));
            return;
        }

        var center = expedition.Plan.LandingZone;
        var coordinates = new EntityCoordinates(mapUid, new Vector2(center.X + 0.5f, center.Y + 0.5f));
        if (player.AttachedEntity is { } existing)
        {
            _transform.SetCoordinates(existing, coordinates);
        }
        else
        {
            var observer = EntityManager.SpawnEntity(GameTicker.AdminObserverPrototypeName, coordinates);
            var mind = _mind.TryGetMind(player, out var existingMind, out _)
                ? existingMind : _mind.CreateMind(player.UserId, player.Name).Owner;
            _mind.TransferTo(mind, observer);
        }
        // Preview servers have no database round to join; only switch the client's gameplay view.
        if (_gameTicker.DummyTicker)
            EntityManager.EntityNetManager.SendSystemNetworkMessage(new TickerJoinGameEvent(), player.Channel);
        else if (!_gameTicker.PlayerGameStatuses.TryGetValue(player.UserId, out var status) ||
            status != PlayerGameStatus.JoinedGame)
            _gameTicker.PlayerJoinGame(player, silent: true);
        shell.WriteLine(Loc.GetString("cmu-expedition-visited", ("player", player.Name), ("map", number)));
    }
}
