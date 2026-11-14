using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.CMU14.Round;
using Content.Server.CMU14.Threats;
using Content.Server.GameTicking;
using Content.Server.Objectives;
using Content.Server.Objectives.Systems;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.CMU14.DroneOperator;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Combat;
using Content.Shared.CMU14.Threats;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Database;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Content.Shared.Throwing;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;

/// <summary>
/// Drives the Early Warning Joe's objective ladder on the upstream per-mind objective pipeline:
/// each level offers three objectives from the level table's pool, two completions advance the level
/// (D8), a completion made before Colony Fall's threat has arrived counts double (A-17, RB-36), level 4 cannot
/// be left without a finished console assimilation (D8), and the unfinished leftover is withdrawn on
/// advance. Also attributes organic kills to the "murder N" counter.
/// </summary>
public sealed partial class EWJoeObjectiveSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private EWJoeMessageSystem _messages = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private AuRoundSystem _auRound = default!;
    [Dependency] private EWJoeCollectConditionSystem _collect = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private CounterConditionSystem _counter = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private ObjectivesSystem _objectives = default!;
    [Dependency] private EWJoeRuleSystem _rule = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private ThreatSystem _threats = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    private static readonly TimeSpan EvaluateEvery = TimeSpan.FromSeconds(1);

    private TimeSpan _nextEvaluate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeComponent, EWJoeLevelChangedEvent>(OnLevelChanged);
        SubscribeLocalEvent<EWJoeComponent, EWJoeRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);

        InitializeReroll();
    }

    private void OnLevelChanged(Entity<EWJoeComponent> ent, ref EWJoeLevelChangedEvent args)
    {
        // text pass B: progression goes to the Joe's chat log; a level-up names what arrived, a respawn's
        // drop says what was lost
        if (_proto.TryIndex(ent.Comp.LevelTable, out var table) && args.NewLevel < table.Levels.Count)
        {
            var levelName = Loc.GetString(table.Levels[args.NewLevel].Name);
            if (args.NewLevel > args.OldLevel && args.NewLevel > 0)
                AnnounceLevelUp(ent, table, args.OldLevel, args.NewLevel, levelName);
            else if (args.NewLevel < args.OldLevel)
                _messages.Notify(ent, Loc.GetString("cmu-ew-joe-level-down", ("levelName", levelName)));
        }

        // CORE-01: a respawn's drop leaves the higher level's directives unfinishable or judged by the wrong
        // level's rules; the lower level starts over with its own set
        if (args.NewLevel < args.OldLevel)
        {
            WithdrawUnfinished(ent);
            ent.Comp.CurrentObjectives.Clear();
            ent.Comp.CompletedCredit.Clear();
            ent.Comp.NextOfferAttempt = TimeSpan.Zero;
        }

        // ARCH-02: a new level owes no refill from the last one
        var state = EnsureComp<EWJoeDirectiveStateComponent>(ent);
        state.PendingRefill = false;
        state.HoldRequirementUntil = TimeSpan.Zero;

        OfferObjectives(ent);
        UpdateStatus(ent);
    }

    /// <summary>The Signal wheels are not abilities; the level-up notice leaves them out.</summary>
    private const string WheelActionPrefix = "CMUActionEWJoeWheel";

    /// <summary>
    /// Text pass B: the level-up notice, then one line per level gained naming its new abilities (from the
    /// level table, wheels left out) and what changed in the Joe itself (<c>cmu-ew-joe-level-gains-N</c>).
    /// </summary>
    private void AnnounceLevelUp(EntityUid joe, EWJoeLevelsPrototype table, int oldLevel, int newLevel, string levelName)
    {
        _messages.Notify(joe, Loc.GetString("cmu-ew-joe-level-up", ("levelName", levelName)));

        for (var level = Math.Max(1, oldLevel + 1); level <= newLevel; level++)
        {
            var names = new List<string>();
            foreach (var action in table.Levels[level].Actions)
            {
                if (_proto.TryIndex(action, out var proto) && !proto.ID.StartsWith(WheelActionPrefix))
                    names.Add(proto.Name);
            }

            if (names.Count > 0)
                _messages.Notify(joe, Loc.GetString("cmu-ew-joe-level-new-abilities", ("abilities", string.Join(", ", names))));

            // ARCH-06: the CQC sequences this level unlocks
            var sequences = _proto.EnumeratePrototypes<EWJoeCqcSequencePrototype>()
                .Where(s => s.MinLevel == level)
                .OrderBy(s => s.ID, StringComparer.Ordinal)
                .Select(s => Loc.GetString(s.Name))
                .ToList();

            if (sequences.Count > 0)
                _messages.Notify(joe, Loc.GetString("cmu-ew-joe-level-new-sequences", ("sequences", string.Join(", ", sequences))));

            if (Loc.TryGetString($"cmu-ew-joe-level-gains-{level}", out var gains))
                _messages.Notify(joe, gains);
        }
    }

    /// <summary>A stood-down antag keeps nothing it has not finished (D10).</summary>
    private void OnRemoved(Entity<EWJoeComponent> ent, ref EWJoeRemovedEvent args)
    {
        WithdrawUnfinished(ent);
        ent.Comp.CurrentObjectives.Clear();
        ent.Comp.CompletedCredit.Clear();
        RemComp<EWJoeStatusComponent>(ent);
        RemComp<EWJoeDirectiveStateComponent>(ent);
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

            // playtest 3: the Joe's own panel follows the ladder
            if (joe.Comp.Running)
                UpdateStatus(joe);
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

        // ARCH-02: stale directives are judged only on the planet the offer was made on
        var mapId = Transform(ent).MapID;
        if (mapId != MapId.Nullspace)
            EnsureComp<EWJoeDirectiveStateComponent>(ent).OfferMap = mapId;

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
        // death is terminal for the ladder: a corpse neither earns credit nor levels (ARCH-C01)
        if (_mobState.IsDead(ent))
            return;

        if (!_mind.TryGetMind(ent, out var mindId, out var mind))
            return;

        if (!TryGetLevel(ent, out var def))
            return;

        var hasPool = def.ObjectiveGroup != null || def.ForcedObjective != null;

        // drop objectives that vanished (admin rmobjective, or the entity was deleted)
        ent.Comp.CurrentObjectives.RemoveAll(o => !Exists(o) || !mind.Objectives.Contains(o));

        // ARCH-02: withdraw directives that can no longer finish (a console gone, the items or the xenofauna
        // all gone, a district that went dark without the Joe and has nowhere left to re-pin)
        var state = EnsureComp<EWJoeDirectiveStateComponent>(ent);
        WithdrawStale(ent, state, mindId, mind);

        // credit belongs to the offer it was earned in
        foreach (var credited in ent.Comp.CompletedCredit.Keys.ToArray())
        {
            if (!ent.Comp.CurrentObjectives.Contains(credited))
                ent.Comp.CompletedCredit.Remove(credited);
        }

        // a withdrawn directive's slot is offered again after the retry delay
        if (state.PendingRefill && hasPool && _timing.CurTime >= ent.Comp.NextOfferAttempt)
        {
            if (ent.Comp.CurrentObjectives.Count < ent.Comp.ObjectivesPerLevel)
                OfferObjectives(ent);

            if (ent.Comp.CurrentObjectives.Count >= ent.Comp.ObjectivesPerLevel)
                state.PendingRefill = false;
            else
                ent.Comp.NextOfferAttempt = _timing.CurTime + ent.Comp.OfferRetryDelay;
        }

        if (ent.Comp.CurrentObjectives.Count == 0)
        {
            if (hasPool && _timing.CurTime >= ent.Comp.NextOfferAttempt)
                OfferObjectives(ent);

            return;
        }

        // the forced objective (level 4's console) is re-offered if it was refused or withdrawn and the offer
        // has room; a full offer without it keeps the console requirement waived for the level (CORE-05)
        if (def.ForcedObjective is { } forced && _timing.CurTime >= ent.Comp.NextOfferAttempt
            && ent.Comp.CurrentObjectives.Count < ent.Comp.ObjectivesPerLevel)
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
            // items that paid for this objective are spent; a rerolled collect objective needs fresh ones
            _collect.MarkSpent(ent, objective);
            if (TryComp(objective, out EWJoeAssimilateConditionComponent? assimilate))
                assimilate.Spent = true;
            _messages.Notify(ent, Loc.GetString("cmu-ew-joe-objective-complete"));
            _adminLogger.Add(LogType.Mind, LogImpact.Medium,
                $"Early Warning Joe {ToPrettyString(ent):joe} completed {ToPrettyString(objective):objective} for {credit} credit");
        }

        // a withdrawn directive must not lower the bar before its slot was offered again
        if (_timing.CurTime < state.HoldRequirementUntil
            && ent.Comp.CompletedCredit.Values.Sum() < ent.Comp.CreditPerLevel)
        {
            return;
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

        var required = RequiredCredit(ent.Comp);
        if (ent.Comp.CompletedCredit.Values.Sum() < required)
            return false;

        if (!def.RequireAssimilation)
            return true;

        // the console gate applies only while a console objective is on offer; when the planet has no
        // console left to pin (the forced offer failed), the standard credit carries the level (FLOW-C02)
        var consoleOffered = false;
        foreach (var objective in ent.Comp.CurrentObjectives)
        {
            if (!HasComp<EWJoeAssimilateConditionComponent>(objective))
                continue;

            consoleOffered = true;
            if (ent.Comp.CompletedCredit.ContainsKey(objective))
                return true;
        }

        return !consoleOffered;
    }

    /// <summary>
    /// A-17: in Colony Fall, work done before the threat arrives counts double, because the mode's
    /// colonist-only window is short and the threat ends most rounds before level 5. RB-36: the window lasts
    /// until the threat has actually spawned (<see cref="ThreatHasArrived"/>), not merely until the vote picks it.
    /// </summary>
    public int CreditFor(EWJoeComponent comp)
    {
        return IsPreThreatWindow(comp) ? comp.PreThreatCreditMultiplier : 1;
    }

    /// <summary>Credit needed to advance: the level's credit, capped by how many directives are on offer.</summary>
    public static int RequiredCredit(EWJoeComponent comp)
    {
        return Math.Max(1, Math.Min(comp.CreditPerLevel, comp.CurrentObjectives.Count));
    }

    public bool IsPreThreatWindow(EWJoeComponent comp)
    {
        return _auRound.SelectedPreset?.ID == comp.PreThreatPreset && !ThreatHasArrived();
    }

    /// <summary>
    /// RB-36: whether the round's selected threat has spawned. Colony Fall schedules the spawn 600-1200 s after
    /// the vote (<see cref="ThreatSystem.SpawnThreatFromVote"/>) and starts the threat's win-condition rules in
    /// the same callback that spawns its bodies, so an added win-condition rule is the arrival signal. A threat
    /// with no win-condition rule falls back on its scheduled spawn time: arrived once it is no longer pending.
    /// </summary>
    public bool ThreatHasArrived()
    {
        if (_auRound.SelectedThreat is not { } threat)
            return false;

        if (threat.WinConditions.Count > 0)
        {
            foreach (var rule in threat.WinConditions)
            {
                if (_ticker.IsGameRuleAdded(rule))
                    return true;
            }

            return false;
        }

        foreach (var pending in _threats.PendingThreatSpawnsForDebug)
        {
            if (pending.ThreatId == threat.ID)
                return false;
        }

        return true;
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

            Withdraw(ent, mindId, mind, objective);
        }
    }

    /// <summary>
    /// ARCH-02: asks every unfinished directive of the current offer whether it can still be finished
    /// (<see cref="EWJoeObjectiveStillPossibleEvent"/>) and withdraws those that cannot. A lost console is
    /// re-offered at once, so the level-4 gate is never waived while a console still exists; any other slot
    /// is offered again after <see cref="EWJoeComponent.OfferRetryDelay"/>. Judged only while the Joe stands
    /// on the planet the offer was made on.
    /// </summary>
    private void WithdrawStale(Entity<EWJoeComponent> ent, EWJoeDirectiveStateComponent state, EntityUid mindId, MindComponent mind)
    {
        if (mind.OwnedEntity is not { } owner)
            return;

        var mapId = Transform(owner).MapID;
        if (mapId == MapId.Nullspace)
            return;

        var maps = _zLevels.GetAllNetworkMapIds(mapId);
        if (state.OfferMap is { } offerMap && !maps.Contains(offerMap))
            return;

        foreach (var objective in ent.Comp.CurrentObjectives.ToArray())
        {
            if (ent.Comp.CompletedCredit.ContainsKey(objective) || _objectives.IsCompleted(objective, (mindId, mind)))
                continue;

            var ev = new EWJoeObjectiveStillPossibleEvent(mindId, mind, owner, maps);
            RaiseLocalEvent(objective, ref ev);
            if (ev.Possible)
                continue;

            var console = HasComp<EWJoeAssimilateConditionComponent>(objective);
            _adminLogger.Add(LogType.Mind, LogImpact.Low,
                $"Early Warning Joe {ToPrettyString(ent):joe} had {ToPrettyString(objective):objective} withdrawn: it can no longer be finished");

            Withdraw(ent, mindId, mind, objective);
            ent.Comp.CurrentObjectives.Remove(objective);
            _messages.Notify(ent, Loc.GetString("cmu-ew-joe-objective-withdrawn-stale"));

            if (console)
            {
                // retry the forced offer now, so the level-4 gate is never waived while a console still exists
                ent.Comp.NextOfferAttempt = TimeSpan.Zero;
                continue;
            }

            state.PendingRefill = true;
            ent.Comp.NextOfferAttempt = _timing.CurTime + ent.Comp.OfferRetryDelay;
            state.HoldRequirementUntil = ent.Comp.NextOfferAttempt;
        }
    }

    private void Withdraw(Entity<EWJoeComponent> ent, EntityUid mindId, MindComponent mind, EntityUid objective)
    {
        // whatever the Joe already obtained for a withdrawn collect objective is used up: a reroll of
        // the same kind must be earned with items it has not touched for an objective before
        _collect.MarkSpent(ent, objective);

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
        return EWJoeTargetRules.IsOrganic(EntityManager, uid);
    }

    /// <summary>
    /// Walks a damage origin back to the Early Warning Joe: the Joe itself, a projectile it fired, an
    /// item it threw, or a weapon held in its container tree.
    /// </summary>
    public EntityUid? ResolveKiller(EntityUid? origin)
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
