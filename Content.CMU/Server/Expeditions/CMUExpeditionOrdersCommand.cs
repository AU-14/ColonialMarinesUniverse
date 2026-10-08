using System.Globalization;
using System.Numerics;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.NPC.Prototypes;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Expeditions;

[AdminCommand(AdminFlags.Admin)]
public sealed partial class CMUExpeditionOrdersCommand : LocalizedEntityCommands
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private CMUExpeditionAgentSystem _agents = default!;
    public override string Command => "cmu-expedition-orders";
    public override string Description => Loc.GetString("cmd-cmu-expedition-orders-desc");
    public override string Help => Loc.GetString("cmd-cmu-expedition-orders-help");

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 4 || !int.TryParse(args[0], out var number) || !_map.MapExists(new MapId(number)) ||
            !int.TryParse(args[1], out var squad) || squad < 1)
        { shell.WriteError(Help); return; }
        var map = _map.GetMap(new MapId(number));
        if (!EntityManager.HasComponent<CMUExpeditionMapComponent>(map))
        { shell.WriteError(Loc.GetString("cmu-expedition-not-map")); return; }
        var action = args[2];
        var position = Vector2.Zero;
        var disposition = CMUExpeditionDisposition.Steady;
        var factions = args[3].Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (action is "guard" or "move")
        {
            if (args.Length != 5 || !float.TryParse(args[3], NumberStyles.Float, CultureInfo.InvariantCulture, out position.X) ||
                !float.TryParse(args[4], NumberStyles.Float, CultureInfo.InvariantCulture, out position.Y) ||
                !float.IsFinite(position.X) || !float.IsFinite(position.Y))
            { shell.WriteError(Help); return; }
        }
        else if (action == "style")
        {
            if (args.Length != 4 || !Enum.TryParse(args[3], true, out disposition) || !Enum.IsDefined(disposition))
            { shell.WriteError(Help); return; }
        }
        else if (action is "friendly" or "target")
        {
            if (args.Length != 4) { shell.WriteError(Help); return; }
            if (args[3] == "default") factions = Array.Empty<string>();
            foreach (var faction in factions)
                if (!_prototypes.TryIndex<NpcFactionPrototype>(faction, out _))
                { shell.WriteError(Loc.GetString("cmu-expedition-unknown-faction", ("faction", faction))); return; }
        }
        else { shell.WriteError(Help); return; }
        var count = 0;
        var query = EntityManager.EntityQueryEnumerator<CMUExpeditionAgentComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var agent, out var transform))
        {
            if (transform.MapUid != map || agent.Squad != squad) continue;
            if (action is "guard" or "move")
            {
                // Keep squad members separated instead of ordering everyone onto one body-sized point.
                var offset = new Vector2(count % 3 - 1, count / 3) * 2;
                if (!_agents.OrderPosition(uid, new EntityCoordinates(map, position + offset), action == "guard")) continue;
            }
            else
            {
                _agents.ResetOrders(uid, agent);
                if (action == "style")
                {
                    agent.Disposition = disposition;
                    agent.Aggression = disposition == CMUExpeditionDisposition.Aggressive ? .85f : disposition == CMUExpeditionDisposition.Cautious ? .2f : .5f;
                    agent.Courage = disposition == CMUExpeditionDisposition.Aggressive ? .8f : disposition == CMUExpeditionDisposition.Cautious ? .3f : .5f;
                    agent.PreferredFireRange = disposition == CMUExpeditionDisposition.Aggressive ? 6 : disposition == CMUExpeditionDisposition.Cautious ? 10 : 8;
                }
                else
                {
                    var set = action == "friendly" ? agent.FriendlyFactions : agent.TargetFactions;
                    set.Clear();
                    set.UnionWith(factions);
                }
            }
            count++;
        }
        shell.WriteLine(Loc.GetString("cmu-expedition-orders-applied", ("count", count)));
    }
}
