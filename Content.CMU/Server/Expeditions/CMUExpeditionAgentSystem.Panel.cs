using System.Linq;
using System.Numerics;
using Content.Server.Administration.Managers;
using Content.Server.Administration.Logs;
using Content.Server.EUI;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.CMU14.Expeditions;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Verbs;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    [Dependency] private IAdminManager _squadAdmin = default!;
    [Dependency] private IAdminLogManager _squadLog = default!;
    [Dependency] private EuiManager _squadEui = default!;

    private void InitializeSquadPanel() => SubscribeLocalEvent<CMUExpeditionAgentComponent, GetVerbsEvent<Verb>>(OnSquadPanelVerb);

    private void OnSquadPanelVerb(Entity<CMUExpeditionAgentComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor) || !_squadAdmin.HasAdminFlag(actor.PlayerSession, AdminFlags.Admin))
            return;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("cmu-squads-title"),
            Act = () => _squadEui.OpenEui(new CMUSquadPanelEui(ent.Comp.SquadRoot is { } root ? GetNetEntity(root) : null), actor.PlayerSession),
        });
    }

    public CMUSquadPanelState SquadPanelState(NetEntity? selected, string status)
    {
        UpdateSquadPlans(_timing.CurTime);
        if (selected == null && _squadPlans.Count > 0)
            selected = GetNetEntity(_squadPlans.First().Key);
        var state = new CMUSquadPanelState
        {
            Selected = selected, Status = status,
            Variants = PanelSquadVariants.ToList(), Outfits = OutfitNames.Order().ToList(),
            Doctrines = DoctrineNames.Order().ToList(),
            Factions = ProtoMan.EnumeratePrototypes<NpcFactionPrototype>().Select(proto => proto.ID).Order().ToList(),
        };
        foreach (var (root, plan) in _squadPlans.OrderBy(pair => pair.Key.Id).Take(100))
        {
            var member = plan.Members.FirstOrDefault(uid => Exists(uid) && HasComp<CMUExpeditionAgentComponent>(uid) && CanOrderSquadMember(uid),
                plan.Members.FirstOrDefault(uid => Exists(uid) && HasComp<CMUExpeditionAgentComponent>(uid)));
            if (!Exists(member) || !Exists(root))
                continue;
            var agent = Comp<CMUExpeditionAgentComponent>(member);
            state.Squads.Add(new CMUSquadSummary(GetNetEntity(root), Loc.GetString("cmu-squads-summary",
                ("squad", agent.Squad), ("count", plan.Members.Count), ("map", Transform(member).MapID), ("phase", SquadPhaseDiagnostic(plan.Phase)))));
            if (selected != GetNetEntity(root))
                continue;
            state.Friendlies = agent.FriendlyFactions.Count == 0 ? "default" : string.Join(",", agent.FriendlyFactions);
            state.Targets = agent.TargetFactions.Count == 0 ? "default" : string.Join(",", agent.TargetFactions);
            state.CurrentDoctrine = agent.Doctrine;
            state.AimSkillPercent = (int) MathF.Round(Math.Clamp(agent.AimSkill, 0, 1) * 100);
            state.AutomaticPatrol = plan.Members.Any(uid => Exists(uid) &&
                TryComp<CMUExpeditionAgentComponent>(uid, out var memberAgent) && memberAgent.AutoPatrol);
            state.Coordinating = plan.Members.Any(uid => Exists(uid) &&
                TryComp<CMUExpeditionAgentComponent>(uid, out var memberAgent) && memberAgent.CoordinateSquads);
            state.Overview = Loc.GetString("cmu-squads-overview", ("active", plan.Members.Count(CanOrderSquadMember)),
                ("total", plan.Members.Count), ("phase", SquadPhaseDiagnostic(plan.Phase)), ("points", agent.PatrolPoints.Count),
                ("operation", agent.OperationsDecision));
            foreach (var uid in plan.Members.Where(uid => Exists(uid) && HasComp<CMUExpeditionAgentComponent>(uid)).Take(32))
            {
                var a = Comp<CMUExpeditionAgentComponent>(uid);
                Vector2? Position(EntityCoordinates? point) => point is { } p && Exists(p.EntityId) && Transform(p.EntityId).MapID == Transform(uid).MapID
                    ? _transform.ToMapCoordinates(p).Position : null;
                var hasGun = _guns.TryGetGun(uid, out var gun);
                var ammo = hasGun ? WeaponAmmo(gun) : 0;
                var nativeDelay = hasGun ? Math.Max(0, (gun.Comp.NextFire - _timing.CurTime).TotalSeconds) : 0;
                var condition = Loc.GetString(HasComp<ActorComponent>(uid) ? "cmu-squads-condition-player" :
                    _mobs.IsDead(uid) ? "cmu-squads-condition-dead" : _mobs.IsCritical(uid) ? "cmu-squads-condition-critical" :
                    a.State == CMUExpeditionAgentState.Disabled ? "cmu-squads-condition-disabled" : "cmu-squads-condition-active");
                var summary = Loc.GetString("cmu-squads-member-summary", ("role", a.CombatRole), ("duty", a.Duty),
                    ("condition", condition), ("weapon", hasGun ? MetaData(gun).EntityName : Loc.GetString("cmu-squads-unarmed")),
                    ("ammo", ammo), ("damage", a.LastDamage.ToString("F0")), ("state", a.State));
                var order = SquadOrderDiagnostic(a);
                var tactic = SquadPhaseDiagnostic(a.SquadPhase);
                var tacticReason = SquadPhaseReasonDiagnostic(a.SquadPhaseReason);
                var action = Loc.GetString("cmu-squads-current-action", ("state", a.State),
                    ("owner", a.DecisionOwner), ("reason", a.DecisionReason));
                var activityStatus = SquadActivityStatus(a, nativeDelay);
                var recordedActivity = a.DiagnosticsStoppedAt != null;
                var detail = $"{condition} | Map {Transform(uid).MapID}\n{a.Duty} / {a.Doctrine} / {a.SquadPhase}\n{a.State}: {a.DecisionOwner}\n" +
                    $"Fire: {a.LastFireCheck} | Weapon: {a.WeaponDecision}\n" +
                    $"Native fire wait: {nativeDelay:F2}s | AI aim wait: {Math.Max(0, (a.FireAt - _timing.CurTime).TotalSeconds):F2}s\n" +
                    $"Damage: {a.LastDamage:F0} | Ammo: {ammo} | Stress: {a.Stress:F2}\n" +
                    $"Squad: {a.SquadDecision} | Movement: {a.TrafficDecision} | Door: {a.DoorDecision} | Vault: {a.VaultDecision}\n" +
                    $"Sight: {a.VisionDecision} | Supplies: {a.SupplyDecision}\n" +
                    $"Friendly: {string.Join(",", a.FriendlyFactions)} | Targets: {string.Join(",", a.TargetFactions)}\n" +
                    $"Search: {a.LastRouteMilliseconds:F2} ms / {a.LastRouteCells} cells\n" + string.Join("\n", a.DecisionHistory);
                detail += $"\nSquad update: {_squadPlanMilliseconds:F2} ms | Portal: {a.TravelPortal} | Goal: {a.TravelGoal}";
                detail += $"\nOperations: {a.OperationsDecision} | Support: {a.SupportSquadRoot}\n" +
                    $"Incoming fire: {a.IncomingFireDecision} | Escapes: {a.IncomingFireEscapes}";
                detail += $"\nAim skill: {a.AimSkill:P0} | Sustained fire: {a.SustainedFire} | Volley: {a.FireControlVolley}";
                detail += $"\nFiring movement: {a.ExposureMovementDecision} | Steps: {a.ExposedSteps}";
                detail += "\n" + Loc.GetString("cmu-squads-diagnostic-controller",
                    ("owner", a.DecisionOwner), ("reason", a.DecisionReason), ("changes", a.StateTransitions));
                detail += "\n" + Loc.GetString("cmu-squads-diagnostic-timing",
                    ("last", a.LastThinkMilliseconds.ToString("F2")), ("average", a.AverageThinkMilliseconds.ToString("F2")),
                    ("peak", a.MaxThinkMilliseconds.ToString("F2")), ("samples", a.ThinkSamples));
                detail += "\n" + Loc.GetString("cmu-squads-diagnostic-learning",
                    ("group", a.ExperienceGroup), ("samples", a.ExperienceSamples),
                    ("flank", a.LearnedFlankCost.ToString("F2")), ("danger", a.LearnedDangerCost.ToString("F2")));
                detail += "\n" + Loc.GetString("cmu-squads-diagnostic-corner", ("decision", a.CornerDecision),
                    ("lanes", a.KnownFireLanes.Count), ("flanks", a.CornerFlanks), ("staging", a.CornerStagingMoves));
                detail += "\n" + Loc.GetString("cmu-squads-diagnostic-patrol", ("decision", a.PatrolDecision),
                    ("visits", a.PatrolVisits), ("searches", a.PatrolSearches), ("failures", a.PatrolFailures));
                detail += "\n" + Loc.GetString("cmu-squads-diagnostic-assistance", ("decision", a.AssistanceDecision),
                    ("accepted", a.AssistanceAccepted), ("declined", a.AssistanceDeclined));
                detail += "\n" + Loc.GetString("cmu-squads-diagnostic-grenade", ("decision", a.GrenadeDecision));
                detail += "\n" + Loc.GetString("cmu-squads-diagnostic-fire-response", ("decision", a.FireResponseDecision),
                    ("pats", a.FirePats), ("rolls", a.FireRolls), ("escapes", a.FireEscapes));
                detail += "\n" + Loc.GetString("cmu-squads-diagnostic-close-quarters", ("nudges", a.TrafficNudges),
                    ("strikes", a.LastResortStrikes));
                detail += "\n" + HearingDiagnostic(a.HeardKind, a.HeardAt, _timing.CurTime) + "\n" +
                    SelfTreatmentDiagnostic(a, _timing.CurTime) + "\n" + MedicalTaskDiagnostic(uid);
                detail += "\n" + Loc.GetString("cmu-squads-diagnostic-assault", ("decision", a.AssaultDecision),
                    ("bounds", a.AssaultBoundsCompleted));
                detail += "\n" + Loc.GetString("cmu-squads-diagnostic-rocket", ("decision", a.RocketDecision));
                if (a.DiagnosticsStoppedAt != null)
                {
                    order = Loc.GetString("cmu-squads-order-ended");
                    if (a.LastLivingDiagnostics is { } recorded)
                    {
                        summary = Loc.GetString("cmu-squads-member-recorded", ("role", a.CombatRole), ("duty", recorded.Duty),
                            ("condition", condition), ("weapon", recorded.WeaponName ?? Loc.GetString("cmu-squads-unarmed")),
                            ("ammo", recorded.Ammo), ("damage", recorded.Damage.ToString("F0")), ("state", recorded.State));
                        detail = FrozenDiagnosticDetail(a, recorded, condition);
                        tactic = SquadPhaseDiagnostic(recorded.Phase);
                        tacticReason = SquadPhaseReasonDiagnostic(recorded.PhaseReason);
                        action = Loc.GetString("cmu-squads-current-action", ("state", recorded.State),
                            ("owner", recorded.Owner), ("reason", recorded.Reason));
                        activityStatus = Loc.GetString("cmu-squads-recorded-status", ("condition", condition),
                            ("at", recorded.At.TotalSeconds.ToString("F1")), ("fire", recorded.FireCheck));
                    }
                    else
                    {
                        detail = Loc.GetString("cmu-squads-diagnostic-no-record", ("condition", condition));
                        tactic = tacticReason = action = "";
                        activityStatus = detail;
                    }
                }
                state.Members.Add(new CMUSquadMemberView(GetNetEntity(uid), MetaData(uid).EntityName,
                    (int) Transform(uid).MapID, _transform.GetWorldPosition(uid), Position(a.LastSeen),
                    Position(a.SpacingDestination ?? a.CoverDestination ?? a.OrderedDestination ?? a.AssaultDestination), Position(a.CoverAnchor),
                    a.OrderRoute.Concat(a.Route).Take(48).Select(point => Position(point)).Where(point => point != null).Select(point => point!.Value).ToList(),
                    a.BadCover.Where(entry => entry.Until > _timing.CurTime).Select(entry => Position(entry.Point))
                        .Where(point => point != null).Select(point => point!.Value).ToList(), summary, detail,
                    CanOrderSquadMember(uid), a.LastDamage >= a.HealDamage, order, tactic, tacticReason, action, activityStatus, recordedActivity));
            }
        }
        return state;
    }

    private string SquadOrderDiagnostic(CMUExpeditionAgentComponent agent)
    {
        var kind = agent.AssaultDestination != null ? "assault" : agent.GuardAnchor != null ? "guard" :
            agent.AutoPatrol ? "auto-patrol" : agent.Patrolling ? "patrol" :
            agent.TravelGoal != null || agent.OrderedDestination != null ? "move" :
            agent.HoldPosition ? "hold" : agent.SupportSquadRoot != null ? "support" : "local";
        var label = Loc.GetString($"cmu-squads-order-{kind}");
        var point = agent.AssaultDestination ?? agent.TravelGoal ?? agent.GuardAnchor ?? agent.OrderedDestination;
        if (point is not { } destination || !Exists(destination.EntityId))
            return label;
        var map = _transform.ToMapCoordinates(destination);
        return Loc.GetString("cmu-squads-order-point", ("order", label), ("map", map.MapId),
            ("x", map.Position.X.ToString("F1")), ("y", map.Position.Y.ToString("F1")));
    }

    private string SquadPhaseDiagnostic(string phase) => phase switch
    {
        "holding" or "travelling" or "anti-rush" or "withdraw" or "anti-armor" or "fire-and-move" or
            "assault" or "assault-advance" or "no-active-members" => Loc.GetString($"cmu-squads-phase-{phase}"),
        _ => phase,
    };

    private string SquadPhaseReasonDiagnostic(string reason) => reason switch
    {
        "no-active-members" or "closing-melee-threat" or "casualties-or-empty-weapons" or "vehicle-contact" or
            "assault-contact" or "assault-objective" or "moving-to-order" or "no-contact" or "engaging-contact" =>
            Loc.GetString($"cmu-squad-phase-reason-{reason}"),
        _ => reason,
    };

    private string SquadActivityStatus(CMUExpeditionAgentComponent agent, double nativeDelay)
    {
        var now = _timing.CurTime;
        if (agent.Treatment != null || agent.TreatmentMedicine != null)
            return SelfTreatmentDiagnostic(agent, now);
        if (agent.UtilityCleanupItem != null)
            return Loc.GetString("cmu-squads-status-utility-cleanup");
        if (agent.PendingWeapon != null)
            return Loc.GetString("cmu-squads-status-weapon", ("reason", agent.WeaponDecision));
        if (agent.WaitingForDoor != null)
            return Loc.GetString("cmu-squads-status-door", ("reason", agent.DoorDecision));
        if (agent.TrafficWaitingSince != null || agent.CohesionWaitSince != null ||
            agent.TrafficNudgeDestination != null && now < agent.TrafficNudgeUntil)
            return Loc.GetString("cmu-squads-status-traffic", ("reason", agent.TrafficDecision));
        if (agent.OrderBlocked)
            return Loc.GetString("cmu-squads-status-route");
        if (agent.ManeuverWaitSince != null)
            return Loc.GetString("cmu-squads-status-cover", ("reason", agent.SquadDecision));
        if (agent.CornerHolding)
            return Loc.GetString("cmu-squads-status-corner", ("reason", agent.CornerDecision));
        if (agent.BlockedShotSince != null)
            return Loc.GetString("cmu-squads-status-shot", ("reason", agent.LastFireCheck));
        if (agent.LostAimSince != null)
            return Loc.GetString("cmu-squads-status-sight");
        if (nativeDelay > 0 && agent.LastFireCheck is "weapon-not-ready" or "moving-weapon-not-ready")
            return Loc.GetString("cmu-squads-status-cadence", ("remaining", nativeDelay.ToString("F2")));
        if (now < agent.FireAt && agent.State is CMUExpeditionAgentState.Aim or CMUExpeditionAgentState.Recover or CMUExpeditionAgentState.HoldAngle)
            return Loc.GetString("cmu-squads-status-aim", ("remaining", (agent.FireAt - now).TotalSeconds.ToString("F2")));
        return Loc.GetString("cmu-squads-status-none");
    }

    public string ControlSquad(ICommonSession player, CMUSquadPanelMessage message, ref NetEntity? selected)
    {
        if (!_squadAdmin.HasAdminFlag(player, AdminFlags.Admin) || !Enum.IsDefined(message.Action) ||
            message.Value == null || message.Variant == null || message.Outfit == null || message.Doctrine == null || message.Facing == null ||
            message.Value.Length > 256 || message.Variant.Length > 32 || message.Outfit.Length > 32 || message.Doctrine.Length > 32 ||
            message.AimSkillPercent is < 0 or > 100 ||
            !float.IsFinite(message.X) || !float.IsFinite(message.Y))
            return Loc.GetString("cmu-squads-invalid");
        if (message.Action == CMUSquadPanelAction.Refresh)
            return "";
        if (message.Action == CMUSquadPanelAction.Select)
        {
            if (message.Root is { } choice && _squadPlans.ContainsKey(GetEntity(choice)))
                selected = choice;
            return "";
        }
        EntityCoordinates point = default;
        if (message.Here && player.AttachedEntity is { } observer)
            point = Transform(observer).Coordinates;
        else if (!message.Here && _maps.MapExists(new MapId(message.Map)))
            point = new EntityCoordinates(_maps.GetMap(new MapId(message.Map)), new Vector2(message.X, message.Y));
        if (message.Action == CMUSquadPanelAction.Spawn)
        {
            if (point == default || !Doctrines.ContainsKey(message.Doctrine))
                return Loc.GetString("cmu-squads-invalid");
            var spawned = SpawnSquad(point, message.Count, message.Variant, out var id, message.Outfit);
            if (spawned == 0)
                return Loc.GetString("cmu-expedition-ai-no-space");
            _nextSquadPlan = TimeSpan.Zero;
            UpdateSquadPlans(_timing.CurTime);
            var group = _squadPlans.LastOrDefault(pair => pair.Value.Members.Any(member =>
                Comp<CMUExpeditionAgentComponent>(member).Squad == id && Transform(member).MapID == Transform(point.EntityId).MapID));
            if (group.Value != null)
            {
                selected = GetNetEntity(group.Key);
                foreach (var member in group.Value.Members)
                    SetDoctrine(member, message.Doctrine);
            }
            _squadLog.Add(LogType.AdminCommands, LogImpact.Medium, $"{player.Name} spawned {spawned} expedition agents with squad panel");
            return Loc.GetString("cmu-squads-applied", ("count", spawned));
        }
        if (message.Root is not { } networkRoot || !_squadPlans.TryGetValue(GetEntity(networkRoot), out var plan))
            return Loc.GetString("cmu-expedition-orders-none");
        if (message.Action == CMUSquadPanelAction.AutoPatrol && message.Enabled)
        {
            foreach (var member in plan.Members.Where(CanOrderSquadMember))
            {
                var patrol = Comp<CMUExpeditionAgentComponent>(member);
                ResetOrders(member, patrol);
                patrol.OrderedDestination = null;
                patrol.Patrolling = false;
                patrol.AutoPatrol = true;
                patrol.AutoPatrolAnchor = plan.Leader is { } leader ? Transform(leader).Coordinates : Transform(member).Coordinates;
                patrol.NextAutoPatrol = _timing.CurTime + TimeSpan.FromSeconds(15);
                patrol.OperationsDecision = "planning-auto-patrol";
            }
            var started = StartAutomaticPatrol(plan);
            _squadLog.Add(LogType.AdminCommands, LogImpact.Medium,
                $"{player.Name} enabled automatic patrol on expedition squad {networkRoot}");
            return started == 0 ? Loc.GetString("cmu-squads-patrol-no-space") :
                Loc.GetString("cmu-squads-applied", ("count", started));
        }
        var factions = Array.Empty<string>();
        if (message.Action is CMUSquadPanelAction.Friendly or CMUSquadPanelAction.Target &&
            !TryParseFactionOrders(message.Value, message.Action == CMUSquadPanelAction.Target, out factions, out _))
            return Loc.GetString("cmu-squads-invalid");
        Direction? facing = null;
        if (message.Facing != "auto")
        {
            if (!Enum.TryParse<Direction>(message.Facing, true, out var direction) ||
                direction is not (Direction.North or Direction.South or Direction.East or Direction.West))
                return Loc.GetString("cmu-squads-invalid");
            facing = direction;
        }
        var reserved = new List<EntityCoordinates>();
        var count = 0;
        foreach (var member in plan.Members.ToArray())
        {
            if (!CanOrderSquadMember(member))
                continue;
            var agent = Comp<CMUExpeditionAgentComponent>(member);
            switch (message.Action)
            {
                case CMUSquadPanelAction.Move:
                case CMUSquadPanelAction.Assault:
                case CMUSquadPanelAction.Guard:
                case CMUSquadPanelAction.PatrolAdd:
                    if (point == default || !OrderSquadPoint(member, point,
                            message.Action == CMUSquadPanelAction.Assault ? "assault" : message.Action == CMUSquadPanelAction.Move ? "move" :
                            message.Action == CMUSquadPanelAction.Guard ? "guard" : "patrol-add", reserved, facing))
                        continue;
                    break;
                case CMUSquadPanelAction.PatrolStart:
                case CMUSquadPanelAction.PatrolStop:
                case CMUSquadPanelAction.PatrolClear:
                case CMUSquadPanelAction.AutoPatrol:
                    if (!OrderPatrol(member, agent, message.Action == CMUSquadPanelAction.PatrolStart ? "patrol-start" :
                            message.Action == CMUSquadPanelAction.PatrolClear ? "patrol-clear" : "patrol-stop"))
                        continue;
                    break;
                case CMUSquadPanelAction.Cooperation:
                    agent.CoordinateSquads = message.Enabled;
                    if (!message.Enabled)
                    {
                        agent.SupportSquadRoot = null;
                        agent.SupportUntil = TimeSpan.Zero;
                        ReleaseManeuver(member, agent);
                    }
                    break;
                case CMUSquadPanelAction.Doctrine:
                    if (!SetDoctrine(member, message.Value))
                        continue;
                    break;
                case CMUSquadPanelAction.AimSkill:
                    agent.AimSkill = message.AimSkillPercent / 100f;
                    agent.NextAimCorrection = TimeSpan.Zero;
                    agent.NextThink = _timing.CurTime;
                    break;
                case CMUSquadPanelAction.Friendly:
                case CMUSquadPanelAction.Target:
                    ResetOrders(member, agent);
                    var set = message.Action == CMUSquadPanelAction.Friendly ? agent.FriendlyFactions : agent.TargetFactions;
                    set.Clear();
                    set.UnionWith(factions);
                    break;
                case CMUSquadPanelAction.Regroup:
                    if (plan.Leader is not { } leader || !OrderSquadPoint(member, Transform(leader).Coordinates, "move", reserved))
                        continue;
                    break;
                case CMUSquadPanelAction.Hold:
                case CMUSquadPanelAction.Resupply:
                    ResetOrders(member, agent);
                    agent.OrderedDestination = null;
                    agent.Patrolling = false;
                    agent.HoldPosition = true;
                    agent.Home = Transform(member).Coordinates;
                    agent.NextSupplyRun = TimeSpan.Zero;
                    agent.NextScavenge = TimeSpan.Zero;
                    break;
                default:
                    continue;
            }
            Decision(agent, "admin-order", message.Action.ToString());
            count++;
        }
        _squadLog.Add(LogType.AdminCommands, LogImpact.Medium,
            $"{player.Name} used expedition squad action {message.Action} on {count} agents ({networkRoot})");
        return Loc.GetString("cmu-squads-applied", ("count", count));
    }
}
