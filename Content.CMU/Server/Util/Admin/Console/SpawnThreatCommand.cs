using System.Linq;
using Content.Server.Administration;
using Content.Server.GameTicking;
using Content.Server.CMU14.Threats;
using Content.Shared.CMU14.Threats;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Util.Admin.Console;

[AdminCommand(AdminFlags.Round)]
public sealed partial class SpawnThreatCommand : LocalizedEntityCommands
{
    [Dependency] private IPrototypeManager _prototype = default!;

    public override string Command => "spawnthreat";

    public override string Description => "Force starts a threat mid-round at the map's threat markers (fallsback to your location).";

    public override string Help
        => "spawnthreat [threatId] [nowin (true/false, default: false)] [now (true/false, default: false)]\n"
        + " now=true skips the player-interest wait: the threat deploys at once and every body nobody asked for becomes a ghost role. Use it for testing.\n"
        + " Spawns a threat and its bodies. Bodies spawn at the map's threat markers; if there are fewer markers than bodies (or none at all), the rest spawn at your position.\n"
        + " Bodies not taken by players become ghost roles.\n"
        + " nowin=true spawns the threat without its win-condition rules: use it to stack multiple event threats, then end the round yourself with endround. Pair with rmcdelayroundend to hold the round open while the event plays out.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 1 or > 3)
        {
            shell.WriteError("Usage: spawnthreat [threatId] [nowin (true/false, default: false)] [now (true/false, default: false)]");
            return;
        }

        var nowin = false;
        if (args.Length == 2 && !bool.TryParse(args[1], out nowin))
        {
            shell.WriteError("Second argument must be true or false for nowin.");
            return;
        }

        var now = false;
        if (args.Length == 3 && !bool.TryParse(args[2], out now))
        {
            shell.WriteError("Third argument must be true or false for now.");
            return;
        }

        var ticker = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<GameTicker>();
        if (ticker.RunLevel != GameRunLevel.InRound)
        {
            shell.WriteError("This command can only be run in-round!");
            return;
        }

        if (shell.Player?.AttachedEntity is not { } ent)
        {
            shell.WriteError("You have no entity! Observe on the map where the threat should spawn.");
            return;
        }

        if (!_prototype.TryIndex(args[0], out ThreatPrototype? threat))
        {
            shell.WriteError($"No threat prototype found with ID: {args[0]}");
            return;
        }

        var threatSystem = EntityManager.EntitySysManager.GetEntitySystem<ThreatSystem>();
        if (!threatSystem.ForceSpawnThreat(threat.ID, ent, startWinConditions: !nowin, skipInterest: now))
        {
            shell.WriteError($"Could not spawn threat '{threat.ID}' (no roundstart partySpawn?).");
            return;
        }

        shell.WriteLine(now
            ? $"Deploying threat '{threat.ID}' now, skipping player interest (winConditions={!nowin}). Unclaimed bodies are ghost roles."
            : $"Queued threat '{threat.ID}' for player interest (winConditions={!nowin}).");
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(
                _prototype.EnumeratePrototypes<ThreatPrototype>()
                    .OrderBy(prototype => prototype.ID)
                    .Select(prototype => prototype.ID),
                "<threatId>"),
            2 => CompletionResult.FromHintOptions(CompletionHelper.Booleans, "<nowin (default: false)>"),
            3 => CompletionResult.FromHintOptions(CompletionHelper.Booleans, "<now: skip player interest (default: false)>"),
            _ => CompletionResult.Empty
        };
    }
}
