using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.CMU14.Round;
using Content.Server.Objectives;
using Content.Server.Objectives.Systems;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.CMU14.DroneOperator;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Threats;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Database;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Content.Shared.Throwing;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;

/// <summary>
/// Drives the Early Warning Joe's objective ladder on the upstream per-mind objective pipeline:
/// each level offers three objectives from the level table's pool, two completions advance the level
/// (D8), a completion made before Colony Fall's threat is selected counts double (A-17), level 4 cannot
/// be left without a finished console assimilation (D8), and the unfinished leftover is withdrawn on
/// advance. Also attributes organic kills to the "murder N" counter.
/// </summary>
public sealed partial class EWJoeObjectiveSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private AuRoundSystem _auRound = default!;
    [Dependency] private EWJoeAssimilateConditionSystem _assimilate = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private CounterConditionSystem _counter = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private ObjectivesSystem _objectives = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private EWJoeRuleSystem _rule = default!;

    private static readonly TimeSpan EvaluateEvery = TimeSpan.FromSeconds(1);

    private TimeSpan _nextEvaluate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeComponent, EWJoeLevelChangedEvent>(OnLevelChanged);
        SubscribeLocalEvent<EWJoeComponent, EWJoeRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnLevelChanged(Entity<EWJoeComponent> ent, ref EWJoeLevelChangedEvent args)
    {
        if (args.NewLevel > 0 && _proto.TryIndex(ent.Comp.LevelTable, out var table) && args.NewLevel < table.Levels.Count)
        {
            _popup.PopupEntity(Loc.GetString("cmu-ew-joe-level-up", ("levelName", Loc.GetString(table.Levels[args.NewLevel].Name))),
                ent, ent, PopupType.Large);
        }

        OfferObjectives(ent);
    }

    /// <summary>A stood-down antag keeps nothing it has not finished (D10).</summary>
    private void OnRemoved(Entity<EWJoeComponent> ent, ref EWJoeRemovedEvent args)
    {
        WithdrawUnfinished(ent);
        ent.Comp.CurrentObjectives.Clear();
        ent.Comp.CompletedCredit.Clear();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextEvaluate)
            return;

        _nextEvaluate = _timing.CurTime + EvaluateEvery;

        var joes = new List<Entity<EWJoeComponent>>();
        var query = EntityQueryEnumerator<EWJoeComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            joes.Add((uid, comp));
        }

        foreach (var joe in joes)
        {
            Evaluate(joe);
        }
    }

    /// <summary>
    /// Offers the current level's objective set to the Joe's mind: the level's forced objective first,
    /// then picks from the level's pool until <see cref="EWJoeComponent.ObjectivesPerLevel"/> are on
    /// offer. Nothing is offered when the level has no pool (Melding) or the mind is absent.
    /// </summary>
    public void OfferObjectives(Entity<EWJoeComponent> ent)
    {
        if (!TryGetLevel(ent, out var def))
            return;

        if (def.ObjectiveGroup == null && def.ForcedObjective == null)
            return;

        if (!_mind.TryGetMind(ent, out var mindId, out var mind))
            return;

        var offered = OfferedPrototypes(ent);

        if (def.ForcedObjective is { } forced && !offered.Contains(forced))
            TryOfferForced(ent, mindId, mind, forced, offered);

        if (def.ObjectiveGroup is { } pool)
        {
            while (ent.Comp.CurrentObjectives.Count < ent.Comp.ObjectivesPerLevel)
            {
                if (PickFromPool(mindId, mind, pool, offered) is not { } objective)
                    break;

                Offer(ent, mindId, mind, objective, offered);
            }
        }

        if (ent.Comp.CurrentObjectives.Count == 0)
            ent.Comp.NextOfferAttempt = _timing.CurTime + ent.Comp.OfferRetryDelay;
    }

    /// <summary>
    /// The traitor pool walk (a weightedRandom of weightedRandom groups) with one addition: prototypes
    /// already on offer are removed before rolling, so a non-unique prototype is never picked twice in
    /// one offer and never costs the offer a slot.
    /// </summary>
    private EntityUid? PickFromPool(EntityUid mindId, MindComponent mind, ProtoId<WeightedRandomPrototype> poolId, HashSet<string> exclude)
    {
        if (!_proto.TryIndex(poolId, out var pool))
        {
            Log.Error($"Early Warning objective pool {poolId} does not exist");
            return null;
        }

        var groups = pool.Weights.ShallowClone();
        while (_random.TryPickAndTake(groups, out var groupId))
        {
            if (!_proto.TryIndex<WeightedRandomPrototype>(groupId, out var group))
            {
                Log.Error($"Early Warning objective group {groupId} does not exist");
                continue;
            }

            var objectives = group.Weights.ShallowClone();
            foreach (var id in exclude)
            {
                objectives.Remove(id);
            }

            while (_random.TryPickAndTake(objectives, out var protoId))
            {
                if (_objectives.TryCreateObjective((mindId, mind), protoId, out var objective))
                    return objective;
            }
        }

        return null;
    }

    private bool TryOfferForced(Entity<EWJoeComponent> ent, EntityUid mindId, MindComponent mind, EntProtoId forced, HashSet<string> offered)
    {
        if (!_objectives.TryCreateObjective((mindId, mind), forced, out var objective))
            return false;

        Offer(ent, mindId, mind, objective.Value, offered);
        return true;
    }

    private void Offer(Entity<EWJoeComponent> ent, EntityUid mindId, MindComponent mind, EntityUid objective, HashSet<string> offered)
    {
        _mind.AddObjective(mindId, mind, objective);
        ent.Comp.CurrentObjectives.Add(objective);
        if (MetaData(objective).EntityPrototype is { } proto)
            offered.Add(proto.ID);
    }

    private HashSet<string> OfferedPrototypes(Entity<EWJoeComponent> ent)
    {
        var offered = new HashSet<string>();
        foreach (var existing in ent.Comp.CurrentObjectives)
        {
            if (Exists(existing) && MetaData(existing).EntityPrototype is { } proto)
                offered.Add(proto.ID);
        }

        return offered;
    }

    /// <summary>
    /// Scores the current offer. Each newly finished objective earns credit (see <see cref="CreditFor"/>);
    /// once the level's requirement is met the leftover is withdrawn and the level advances. Objectives
    /// that can no longer finish (a pinned console destroyed) are withdrawn, and a level's forced
    /// objective is offered again when it is missing, so the ladder cannot stall on a lost console.
    /// </summary>
    public void Evaluate(Entity<EWJoeComponent> ent)
    {
        if (!_mind.TryGetMind(ent, out var mindId, out var mind))
            return;

        if (!TryGetLevel(ent, out var def))
            return;

        var hasPool = def.ObjectiveGroup != null || def.ForcedObjective != null;

        // drop objectives that vanished (admin rmobjective, or the entity was deleted)
        ent.Comp.CurrentObjectives.RemoveAll(o => !Exists(o) || !mind.Objectives.Contains(o));

        // withdraw assimilations whose console is gone or taken by someone else; they can never finish
        foreach (var objective in ent.Comp.CurrentObjectives.ToArray())
        {
            if (ent.Comp.CompletedCredit.ContainsKey(objective))
                continue;

            if (!TryComp(objective, out EWJoeAssimilateConditionComponent? assimilate)
                || _assimilate.CanStillFinish((objective, assimilate), mind.OwnedEntity))
                continue;

            Withdraw(mindId, mind, objective);
            ent.Comp.CurrentObjectives.Remove(objective);
        }

        // credit belongs to the offer it was earned in
        foreach (var credited in ent.Comp.CompletedCredit.Keys.ToArray())
        {
            if (!ent.Comp.CurrentObjectives.Contains(credited))
                ent.Comp.CompletedCredit.Remove(credited);
        }

        if (ent.Comp.CurrentObjectives.Count == 0)
        {
            if (hasPool && _timing.CurTime >= ent.Comp.NextOfferAttempt)
                OfferObjectives(ent);

            return;
        }

        // the forced objective (level 4's console) is re-offered if it was refused or withdrawn
        if (def.ForcedObjective is { } forced && _timing.CurTime >= ent.Comp.NextOfferAttempt)
        {
            var offered = OfferedPrototypes(ent);
            if (!offered.Contains(forced))
            {
                TryOfferForced(ent, mindId, mind, forced, offered);
                ent.Comp.NextOfferAttempt = _timing.CurTime + ent.Comp.OfferRetryDelay;
            }
        }

        foreach (var objective in ent.Comp.CurrentObjectives)
        {
            if (ent.Comp.CompletedCredit.ContainsKey(objective))
                continue;

            if (!_objectives.IsCompleted(objective, (mindId, mind)))
                continue;

            var credit = CreditFor(ent.Comp);
            ent.Comp.CompletedCredit[objective] = credit;
            _popup.PopupEntity(Loc.GetString("cmu-ew-joe-objective-complete"), ent, ent, PopupType.Medium);
            _adminLogger.Add(LogType.Mind, LogImpact.Medium,
                $"Early Warning Joe {ToPrettyString(ent):joe} completed {ToPrettyString(objective):objective} for {credit} credit");
        }

        if (!RequirementMet(ent, def))
            return;

        WithdrawUnfinished(ent);
        ent.Comp.CurrentObjectives.Clear();
        ent.Comp.CompletedCredit.Clear();

        _adminLogger.Add(LogType.Mind, LogImpact.Medium,
            $"Early Warning Joe {ToPrettyString(ent):joe} advances from level {ent.Comp.Level}");

        // raises EWJoeLevelChangedEvent, which offers the next set
        _rule.TryAdvanceLevel(ent.AsNullable());
    }

    /// <summary>
    /// True when the current level's credit requirement is met: the lesser of the configured credit and
    /// the number of objectives actually on offer, and, where the level demands it, at least one finished
    /// console assimilation.
    /// </summary>
    public bool RequirementMet(Entity<EWJoeComponent> ent, EWJoeLevelDef def)
    {
        if (ent.Comp.CurrentObjectives.Count == 0)
            return false;

        var required = Math.Max(1, Math.Min(ent.Comp.CreditPerLevel, ent.Comp.CurrentObjectives.Count));
        if (ent.Comp.CompletedCredit.Values.Sum() < required)
            return false;

        if (!def.RequireAssimilation)
            return true;

        foreach (var objective in ent.Comp.CompletedCredit.Keys)
        {
            if (HasComp<EWJoeAssimilateConditionComponent>(objective))
                return true;
        }

        return false;
    }

    /// <summary>
    /// A-17: in Colony Fall, work done before the threat is selected counts double, because the mode's
    /// colonist-only window is short and the threat ends most rounds before level 5.
    /// </summary>
    public int CreditFor(EWJoeComponent comp)
    {
        return IsPreThreatWindow(comp) ? comp.PreThreatCreditMultiplier : 1;
    }

    public bool IsPreThreatWindow(EWJoeComponent comp)
    {
        return _auRound.SelectedPreset?.ID == comp.PreThreatPreset && _auRound.SelectedThreat == null;
    }

    private bool TryGetLevel(Entity<EWJoeComponent> ent, out EWJoeLevelDef def)
    {
        def = default!;
        if (!_proto.TryIndex(ent.Comp.LevelTable, out var table) || ent.Comp.Level >= table.Levels.Count)
            return false;

        def = table.Levels[ent.Comp.Level];
        return true;
    }

    /// <summary>Removes every unfinished objective of the current offer from the mind. Finished ones stay for the round-end summary.</summary>
    private void WithdrawUnfinished(Entity<EWJoeComponent> ent)
    {
        if (!_mind.TryGetMind(ent, out var mindId, out var mind))
            return;

        foreach (var objective in ent.Comp.CurrentObjectives)
        {
            if (ent.Comp.CompletedCredit.ContainsKey(objective))
                continue;

            Withdraw(mindId, mind, objective);
        }
    }

    private void Withdraw(EntityUid mindId, MindComponent mind, EntityUid objective)
    {
        var index = mind.Objectives.IndexOf(objective);
        if (index >= 0)
            _mind.TryRemoveObjective(mindId, mind, index);
    }

    /// <summary>Credits an organic death to the Joe's "murder N" counters when the killing blow resolves to it.</summary>
    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (!IsOrganicVictim(args.Target))
            return;

        if (ResolveKiller(args.Origin) is not { } joe || joe == args.Target || !TryComp(joe, out EWJoeComponent? comp))
            return;

        foreach (var objective in comp.CurrentObjectives)
        {
            if (!HasComp<EWJoeKillCounterComponent>(objective) || comp.CompletedCredit.ContainsKey(objective))
                continue;

            _counter.IncreaseCount(objective);
        }
    }

    private bool IsOrganicVictim(EntityUid uid)
    {
        return HasComp<HumanoidProfileComponent>(uid)
            && !HasComp<SynthComponent>(uid)
            && !HasComp<ThreatComponent>(uid)
            && !HasComp<XenoComponent>(uid)
            && !HasComp<YautjaComponent>(uid)
            && !HasComp<CMUDroneAndroidComponent>(uid);
    }

    /// <summary>
    /// Walks a damage origin back to the Early Warning Joe: the Joe itself, a projectile it fired, an
    /// item it threw, or a weapon held in its container tree.
    /// </summary>
    private EntityUid? ResolveKiller(EntityUid? origin)
    {
        if (origin is not { } current)
            return null;

        for (var depth = 0; depth < 4 && !TerminatingOrDeleted(current); depth++)
        {
            if (HasComp<EWJoeComponent>(current))
                return current;

            if (TryComp(current, out ProjectileComponent? projectile) && projectile.Shooter is { } shooter)
            {
                current = shooter;
                continue;
            }

            if (TryComp(current, out ThrownItemComponent? thrown) && thrown.Thrower is { } thrower)
            {
                current = thrower;
                continue;
            }

            if (!_container.TryGetContainingContainer(current, out var container))
                return null;

            current = container.Owner;
        }

        return null;
    }
}
