using System.Numerics;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.CMU14.Expeditions;
using Robust.Shared.Console;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

[AdminCommand(AdminFlags.Admin)]
public sealed partial class CMUExpeditionAgentCommand : LocalizedEntityCommands
{
    [Dependency] private SharedMapSystem _map = default!;

    public override string Command => "cmu-expedition-ai";
    public override string Description => Loc.GetString("cmd-cmu-expedition-ai-desc");
    public override string Help => Loc.GetString("cmd-cmu-expedition-ai-help");

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var count = 3;
        if (args.Length is < 1 or > 2 || !int.TryParse(args[0], out var number) ||
            args.Length == 2 && !int.TryParse(args[1], out count) || count is < 1 or > 6 ||
            !_map.MapExists(new MapId(number)))
        {
            shell.WriteError(Help);
            return;
        }
        var uid = _map.GetMap(new MapId(number));
        if (!EntityManager.TryGetComponent<CMUExpeditionMapComponent>(uid, out var expedition) || !expedition.Ready)
        {
            shell.WriteError(Loc.GetString("cmu-expedition-not-ready"));
            return;
        }
        var squad = expedition.NextSquad++;
        var occupied = new List<Vector2>();
        var existing = EntityManager.EntityQueryEnumerator<CMUExpeditionAgentComponent, TransformComponent>();
        while (existing.MoveNext(out _, out var agent, out var transform))
            if (transform.MapUid == uid && agent.State != CMUExpeditionAgentState.Disabled)
                occupied.Add(transform.LocalPosition);
        var plan = expedition.Plan;
        var positions = new List<EntityCoordinates>();
        for (var radius = 4; radius <= 12 && positions.Count < count; radius += 3)
        for (var y = -radius; y <= radius && positions.Count < count; y += 3)
        for (var x = -radius; x <= radius && positions.Count < count; x += 3)
        {
            var px = plan.Objective.X + x;
            var py = plan.Objective.Y + y;
            if (px < 1 || py < 1 || px >= plan.Size - 1 || py >= plan.Size - 1)
                continue;
            var i = plan.Index(px, py);
            if (plan.Props[i] != CMUExpeditionProp.None || !plan.Paths[i] ||
                plan.Terrain[i] is CMUExpeditionTerrain.Water or CMUExpeditionTerrain.Cliff)
                continue;
            var safe = true;
            foreach (var fire in plan.FirePockets)
                safe &= Math.Abs(px - fire.X) > 3 || Math.Abs(py - fire.Y) > 3;
            var coordinates = new EntityCoordinates(uid, new Vector2(px + 0.5f, py + 0.5f));
            foreach (var position in occupied)
                safe &= Vector2.DistanceSquared(position, coordinates.Position) >= 2;
            if (safe && !positions.Contains(coordinates))
                positions.Add(coordinates);
        }
        var profiles = new[] { "CMUExpeditionScavenger", "CMUExpeditionScavengerAggressive", "CMUExpeditionScavengerCautious" };
        for (var i = 0; i < positions.Count; i++)
        {
            var guard = EntityManager.SpawnEntity(profiles[i % profiles.Length], positions[i]);
            var agent = EntityManager.GetComponent<CMUExpeditionAgentComponent>(guard);
            agent.Squad = squad;
            agent.Entrench = true;
        }
        expedition.GuardsSpawned = positions.Count > 0;
        shell.WriteLine(Loc.GetString("cmu-expedition-ai-spawned", ("count", positions.Count)));
        shell.WriteLine(Loc.GetString("cmu-expedition-ai-squad", ("squad", squad)));
    }
}
