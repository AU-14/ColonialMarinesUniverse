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

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args) => args.Length == 1
        ? CMUExpeditionCommandCompletion.Maps(EntityManager, shell, here: true) : CompletionResult.Empty;

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Help);
            return;
        }
        MapId map;
        if (args[0] == "here" && shell.Player?.AttachedEntity is { } player &&
            EntityManager.TryGetComponent<TransformComponent>(player, out var playerTransform))
            map = playerTransform.MapID;
        else if (int.TryParse(args[0], out var number) && _map.MapExists(new MapId(number)))
            map = new MapId(number);
        else
        {
            shell.WriteError(Help);
            return;
        }
        var query = EntityManager.EntityQueryEnumerator<CMUExpeditionAgentComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var agent, out var transform))
        {
            if (transform.MapID != map)
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
            shell.WriteLine(Loc.GetString("cmu-expedition-ai-orders-status",
                ("destination", agent.OrderedDestination?.ToString() ?? "-"), ("patrolling", agent.Patrolling),
                ("waypoint", agent.PatrolPoints.Count == 0 ? 0 : agent.PatrolIndex + 1),
                ("points", agent.PatrolPoints.Count), ("blocked", agent.OrderBlocked)));
            shell.WriteLine(Loc.GetString("cmu-expedition-ai-radio-status",
                ("received", agent.ReportsReceived), ("accepted", agent.ReportsAccepted),
                ("decision", agent.RadioDecision), ("radioContact", agent.ContactFromRadio),
                ("destination", agent.InvestigationDestination?.ToString() ?? "-")));
            shell.WriteLine(Loc.GetString("cmu-expedition-ai-fallback-status",
                ("callouts", agent.RadioCallouts), ("scavenged", agent.WeaponsScavenged),
                ("strikes", agent.LastResortStrikes),
                ("item", agent.ScavengeTarget is { } item ? EntityManager.GetNetEntity(item).ToString() : "-")));
            shell.WriteLine(Loc.GetString("cmu-expedition-ai-fieldcraft-status",
                ("construction", agent.FortificationDecision), ("facing", agent.FortificationFacing.ToString()),
                ("supplies", agent.SuppliesScavenged), ("hazard", agent.HazardDecision), ("dodges", agent.HazardDodges),
                ("detours", agent.LocalDetours), ("failures", agent.OrderFailures)));
            shell.WriteLine(Loc.GetString("cmu-expedition-ai-door-status",
                ("decision", agent.DoorDecision), ("opened", agent.DoorsOpened), ("failures", agent.DoorFailures),
                ("door", agent.WaitingForDoor is { } door ? EntityManager.GetNetEntity(door).ToString() : "-")));
            shell.WriteLine(Loc.GetString("cmu-expedition-ai-survival-status",
                ("weapon", agent.WeaponRecoveryDecision), ("recovered", agent.WeaponsRecovered),
                ("spacing", agent.SpacingDecision), ("destination", agent.SpacingDestination?.ToString() ?? "-"),
                ("cover", agent.RejectedCover), ("preparing", agent.PreparingWork),
                ("working", agent.WorkItem != null)));
            agent.TargetAssignments.TryGetValue(agent.Target ?? EntityUid.Invalid, out var assigned);
            shell.WriteLine(Loc.GetString("cmu-expedition-ai-threat-status",
                ("enemies", agent.VisibleThreats.Count), ("sectors", agent.OccupiedThreatSectors),
                ("shooters", agent.RecentShooters.Count), ("crossfire", agent.Crossfire),
                ("moves", agent.CrossfireMoves), ("assigned", assigned), ("responses", agent.FlankResponses)));
            shell.WriteLine(Loc.GetString("cmu-expedition-ai-equipment-status",
                ("weapon", agent.WeaponDecision), ("switches", agent.WeaponSwitches), ("rockets", agent.RocketsFired),
                ("grenade", agent.GrenadeDecision), ("smokes", agent.SmokesThrown), ("movingShots", agent.TotalMovingShots)));
            shell.WriteLine(Loc.GetString("cmu-expedition-ai-coordination-status",
                ("role", agent.CombatRole.ToString()), ("decision", agent.SquadDecision),
                ("shooter", agent.CoveringShooter is { } shooter ? EntityManager.GetNetEntity(shooter).ToString() : "-"),
                ("mover", agent.CoveringFor is { } mover ? EntityManager.GetNetEntity(mover).ToString() : "-"),
                ("moves", agent.CoveredMoves), ("interruptions", agent.InterruptedMoves), ("traffic", agent.TrafficDecision)));
            if (EntityManager.TryGetComponent<CMUExpeditionMedicComponent>(uid, out var medic))
                shell.WriteLine(Loc.GetString("cmu-expedition-ai-medical-status",
                    ("decision", medic.Decision), ("phase", medic.Phase.ToString()),
                    ("patient", medic.Patient is { } patient ? EntityManager.GetNetEntity(patient).ToString() : "-"),
                    ("covered", medic.Covered), ("doses", medic.Doses), ("shocks", medic.Shocks),
                    ("revivals", medic.Revivals), ("extractions", medic.Extractions)));
        }
    }
}
