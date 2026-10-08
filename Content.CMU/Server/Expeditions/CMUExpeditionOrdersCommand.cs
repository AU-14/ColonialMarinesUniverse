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
    [Dependency] private SharedTransformSystem _transform = default!;
    public override string Command => "cmu-expedition-orders";
    public override string Description => Loc.GetString("cmd-cmu-expedition-orders-desc");
    public override string Help => Loc.GetString("cmd-cmu-expedition-orders-help");

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 3 ||
            !int.TryParse(args[1], out var squad) || squad < 1)
        { shell.WriteError(Help); return; }
        EntityUid map;
        var position = Vector2.Zero;
        var here = args[0] == "here";
        if (here)
        {
            if (shell.Player?.AttachedEntity is not { } player ||
                !EntityManager.TryGetComponent<TransformComponent>(player, out var transform) || transform.MapUid is not { } currentMap)
            { shell.WriteError(Loc.GetString("cmu-expedition-here-no-player")); return; }
            map = currentMap;
            position = _transform.ToCoordinates(map, _transform.GetMapCoordinates(player)).Position;
        }
        else if (int.TryParse(args[0], out var number) && _map.MapExists(new MapId(number)))
            map = _map.GetMap(new MapId(number));
        else { shell.WriteError(Help); return; }
        if (EntityManager.TryGetComponent<CMUExpeditionMapComponent>(map, out var expedition) && !expedition.Ready)
        { shell.WriteError(Loc.GetString("cmu-expedition-not-ready")); return; }
        var action = args[2];
        var disposition = CMUExpeditionDisposition.Steady;
        var factions = Array.Empty<string>();
        if (action is "guard" or "move" or "patrol-add")
        {
            if (here && args.Length != 3 || !here && (args.Length != 5 || !float.TryParse(args[3], NumberStyles.Float, CultureInfo.InvariantCulture, out position.X) ||
                !float.TryParse(args[4], NumberStyles.Float, CultureInfo.InvariantCulture, out position.Y) ||
                !float.IsFinite(position.X) || !float.IsFinite(position.Y)))
            { shell.WriteError(Help); return; }
        }
        else if (action is "patrol-start" or "patrol-stop" or "patrol-clear")
        {
            if (args.Length != 3) { shell.WriteError(Help); return; }
        }
        else if (action == "style")
        {
            if (args.Length != 4 || !Enum.TryParse(args[3], true, out disposition) || !Enum.IsDefined(disposition))
            { shell.WriteError(Help); return; }
        }
        else if (action is "friendly" or "target")
        {
            if (args.Length != 4) { shell.WriteError(Help); return; }
            if (args[3] != "default") factions = args[3].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var faction in factions)
                if (!_prototypes.TryIndex<NpcFactionPrototype>(faction, out _))
                { shell.WriteError(Loc.GetString("cmu-expedition-unknown-faction", ("faction", faction))); return; }
        }
        else { shell.WriteError(Help); return; }
        var count = 0;
        var reserved = new List<EntityCoordinates>();
        var query = EntityManager.EntityQueryEnumerator<CMUExpeditionAgentComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var agent, out var transform))
        {
            if (transform.MapUid != map || agent.Squad != squad || !_agents.CanOrderSquadMember(uid)) continue;
            if (action is "guard" or "move" or "patrol-add")
            {
                if (!_agents.OrderSquadPoint(uid, new EntityCoordinates(map, position), action, reserved)) continue;
            }
            else if (action is "patrol-start" or "patrol-stop" or "patrol-clear")
            {
                if (!_agents.OrderPatrol(uid, agent, action)) continue;
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
        if (count == 0)
            shell.WriteError(Loc.GetString("cmu-expedition-orders-none"));
        else
            shell.WriteLine(Loc.GetString("cmu-expedition-orders-applied", ("count", count)));
    }
}
