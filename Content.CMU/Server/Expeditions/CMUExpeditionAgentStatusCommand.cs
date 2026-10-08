using Content.Server.Administration;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Administration;
using Content.Shared.Damage.Systems;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Expeditions;

[AdminCommand(AdminFlags.Admin)]
public sealed partial class CMUExpeditionAgentStatusCommand : LocalizedEntityCommands
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private GunSystem _guns = default!;
    [Dependency] private DamageableSystem _damage = default!;

    public override string Command => "cmu-expedition-ai-status";
    public override string Description => Loc.GetString("cmd-cmu-expedition-ai-status-desc");
    public override string Help => Loc.GetString("cmd-cmu-expedition-ai-status-help");

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1 || !int.TryParse(args[0], out var number) || !_map.MapExists(new MapId(number)))
        {
            shell.WriteError(Help);
            return;
        }
        var query = EntityManager.EntityQueryEnumerator<CMUExpeditionAgentComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var agent, out var transform))
        {
            if (transform.MapID != new MapId(number))
                continue;
            var ammo = new GetAmmoCountEvent();
            if (_guns.TryGetGun(uid, out var gun))
                EntityManager.EventBus.RaiseLocalEvent(gun.Owner, ref ammo);
            shell.WriteLine(Loc.GetString("cmu-expedition-ai-status-line",
                ("entity", EntityManager.GetNetEntity(uid)), ("state", agent.State.ToString()),
                ("goal", agent.Goal.ToString()), ("action", agent.Action?.ToString() ?? "-"), ("squad", agent.Squad),
                ("reloads", agent.Reloads), ("grenades", agent.GrenadesThrown), ("rescues", agent.Rescues), ("flanks", agent.Flanks),
                ("reports", agent.ReportsReceived), ("failures", agent.FailedPlans), ("fireCheck", agent.LastFireCheck),
                ("routeCells", agent.LastRouteCells), ("routeMs", agent.LastRouteMilliseconds.ToString("F2")),
                ("maxSearchMs", agent.MaxSearchMilliseconds.ToString("F2")),
                ("disposition", agent.Disposition.ToString()), ("emotion", agent.Emotion.ToString()),
                ("stress", agent.Stress.ToString("F2")), ("initiative", agent.Initiative.ToString("F2")),
                ("position", transform.Coordinates.ToString()), ("ammo", ammo.Count),
                ("damage", _damage.GetTotalDamage(uid).Float()), ("suppressed", agent.SuppressedUntil > _timing.CurTime),
                ("anchor", agent.CoverAnchor?.ToString() ?? "-"), ("peek", agent.PeekPosition?.ToString() ?? "-"),
                ("cells", agent.LastSearchCells), ("milliseconds", agent.LastSearchMilliseconds.ToString("F2"))));
        }
    }
}
