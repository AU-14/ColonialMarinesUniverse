using Content.Server.Objectives.Systems;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Threats;
using Content.Shared.CMU14.Threats.Mobs.Biomorph;
using Content.Shared.CMU14.Xenomorphs.Pathogen;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Damage.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Objectives.Components;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;

/// <summary>
/// The Early Warning Joe's xenofauna directives: maim N xenofauna, and gather N of the xenofauna's victims
/// in a pinned area. Xenofauna is non-humanoid threat life (<see cref="EWJoeTargetRules.IsXenofauna"/>), so
/// these follow whatever such threat the round has, in any mode. Both are refused unless xenofauna lives on
/// the Joe's planet or the round's threat is flagged as xenofauna (<see cref="EWJoeRequiresXenofaunaComponent"/>).
/// </summary>
public sealed partial class EWJoeXenoObjectiveSystem : EntitySystem
{
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private AreaSystem _area = default!;
    [Dependency] private AuRoundSystem _auRound = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private CounterConditionSystem _counter = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private EWJoeObjectiveSystem _objectives = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    public const string DistressSignalPreset = "DistressSignal";

    /// <summary>How long after the Joe's last hit xenofauna falling still counts as the Joe's work.</summary>
    public static readonly TimeSpan HitMemory = TimeSpan.FromSeconds(6);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeRequiresXenofaunaComponent, ObjectiveAssignedEvent>(OnRequiresXenofaunaAssigned);
        SubscribeLocalEvent<EWJoeRequiresXenofaunaComponent, EWJoeObjectiveStillPossibleEvent>(OnRequiresXenofaunaStillPossible);
        SubscribeLocalEvent<EWJoeDeliverVictimsConditionComponent, ObjectiveAssignedEvent>(OnDeliverAssigned);
        SubscribeLocalEvent<EWJoeDeliverVictimsConditionComponent, ObjectiveAfterAssignEvent>(OnDeliverAfterAssign);
        SubscribeLocalEvent<EWJoeDeliverVictimsConditionComponent, ObjectiveGetProgressEvent>(OnDeliverProgress);
        // broadcast: the objective ladder already listens to deaths the same way
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        // DamageChangedEvent is raised directed only, so a broadcast subscription would never fire. Xenofauna
        // is a xeno or a threat-faction body, so these two directed pairs cover it; IsXenofauna filters the rest.
        SubscribeLocalEvent<XenoComponent, DamageChangedEvent>(OnXenoDamaged);
        SubscribeLocalEvent<ThreatComponent, DamageChangedEvent>(OnThreatDamaged);
    }

    /// <summary>A Distress Signal round: the preset was chosen, or its rule is running.</summary>
    public bool IsDistressSignal()
    {
        if (string.Equals(_auRound.SelectedPreset?.ID, DistressSignalPreset, StringComparison.OrdinalIgnoreCase))
            return true;

        var rules = EntityQueryEnumerator<CMDistressSignalRuleComponent>();
        return rules.MoveNext(out _, out _);
    }

    #region Xenofauna presence

    /// <summary>
    /// Xenofauna that counts for the Joe's directives: <see cref="EWJoeTargetRules.IsXenofauna"/>, minus the
    /// small forms (facehuggers and other xeno parasites) that could otherwise farm the maim count.
    /// </summary>
    public bool IsCountableXenofauna(EntityUid uid)
    {
        return EWJoeTargetRules.IsXenofauna(EntityManager, uid) && !HasComp<XenoParasiteComponent>(uid);
    }

    /// <summary>
    /// Whether the owner's round has xenofauna for these directives: the selected threat is flagged
    /// <see cref="ThreatPrototype.Xenofauna"/> (saves a scan before it has spawned), or a living xenofauna
    /// body stands on the owner's planet z-network.
    /// </summary>
    public bool XenofaunaExpected(EntityUid owner)
    {
        if (_auRound.SelectedThreat is { Xenofauna: true })
            return true;

        var mapId = Transform(owner).MapID;
        if (mapId == MapId.Nullspace)
            return false;

        return HasLivingXenofauna(_zLevels.GetAllNetworkMapIds(mapId));
    }

    /// <summary>Whether any living, countable xenofauna body is on <paramref name="maps"/>.</summary>
    public bool HasLivingXenofauna(HashSet<MapId> maps)
    {
        var xenos = EntityQueryEnumerator<XenoComponent, MobStateComponent, TransformComponent>();
        while (xenos.MoveNext(out var uid, out _, out var mob, out var xform))
        {
            if (IsLivingXenofaunaOn(uid, mob, xform, maps))
                return true;
        }

        var threats = EntityQueryEnumerator<ThreatComponent, MobStateComponent, TransformComponent>();
        while (threats.MoveNext(out var uid, out _, out var mob, out var xform))
        {
            if (IsLivingXenofaunaOn(uid, mob, xform, maps))
                return true;
        }

        return false;
    }

    private bool IsLivingXenofaunaOn(EntityUid uid, MobStateComponent mob, TransformComponent xform, HashSet<MapId> maps)
    {
        return maps.Contains(xform.MapID) && !_mobState.IsDead(uid, mob) && IsCountableXenofauna(uid);
    }

    private void OnRequiresXenofaunaAssigned(Entity<EWJoeRequiresXenofaunaComponent> ent, ref ObjectiveAssignedEvent args)
    {
        if (args.Cancelled)
            return;

        if (args.Mind.OwnedEntity is not { } owner || !XenofaunaExpected(owner))
        {
            args.Cancelled = true;
            return;
        }

        var mapId = Transform(owner).MapID;
        ent.Comp.SeenLiving = mapId != MapId.Nullspace && HasLivingXenofauna(_zLevels.GetAllNetworkMapIds(mapId));
    }

    /// <summary>
    /// ARCH-02: a xenofauna directive stays possible while living xenofauna is on the planet. Before any has
    /// been seen, a flagged threat that has not spawned yet keeps it open; once seen and all gone, it is
    /// withdrawn. A gather directive also stays possible while enough victims already lie on the planet.
    /// </summary>
    private void OnRequiresXenofaunaStillPossible(Entity<EWJoeRequiresXenofaunaComponent> ent, ref EWJoeObjectiveStillPossibleEvent args)
    {
        if (HasLivingXenofauna(args.Maps))
        {
            ent.Comp.SeenLiving = true;
            return;
        }

        if (!ent.Comp.SeenLiving && _auRound.SelectedThreat is { Xenofauna: true })
            return;

        if (TryComp(ent, out EWJoeDeliverVictimsConditionComponent? deliver) && CountVictimsOn(args.Maps) >= deliver.Count)
            return;

        args.Possible = false;
    }

    /// <summary>
    /// Walks a damage origin back to xenofauna: the body itself, a projectile it fired, an item it threw,
    /// or something in its container tree. The same walk as <see cref="EWJoeObjectiveSystem.ResolveKiller"/>.
    /// </summary>
    public EntityUid? ResolveXenofauna(EntityUid? origin)
    {
        if (origin is not { } current)
            return null;

        for (var depth = 0; depth < 4 && !TerminatingOrDeleted(current); depth++)
        {
            if (IsCountableXenofauna(current))
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

    #endregion

    #region Maim

    private void OnXenoDamaged(Entity<XenoComponent> ent, ref DamageChangedEvent args)
    {
        RememberJoeHit(ent, args);
    }

    private void OnThreatDamaged(Entity<ThreatComponent> ent, ref DamageChangedEvent args)
    {
        RememberJoeHit(ent, args);
    }

    /// <summary>Remembers the Joe behind each hit on xenofauna, for the grace-delayed critical below.</summary>
    private void RememberJoeHit(EntityUid target, DamageChangedEvent args)
    {
        if (!args.DamageIncreased || !IsCountableXenofauna(target))
            return;

        if (_objectives.ResolveKiller(args.Origin) is not { } joe || !HasComp<EWJoeComponent>(joe))
            return;

        var hit = EnsureComp<EWJoeXenoHitComponent>(target);
        hit.Joe = joe;
        hit.At = _timing.CurTime;
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive)
        {
            // revived: no longer a corpse the xenofauna made
            if (HasComp<EWJoeXenofaunaKilledComponent>(args.Target))
                RemCompDeferred<EWJoeXenofaunaKilledComponent>(args.Target);

            return;
        }

        if (args.NewMobState != MobState.Critical && args.NewMobState != MobState.Dead)
            return;

        StampXenofaunaKill(args);
        CountMaim(args);
    }

    /// <summary>
    /// Xenofauna brought to critical or killed by the Joe's hand, weapon or trigger counts once toward each of
    /// the Joe's open maim objectives. RMC holds a xeno in a grace period before critical and then re-checks
    /// its thresholds with no origin, so a state change that names nobody falls back to the Joe's recent hit.
    /// Facehuggers and other xeno parasites do not count.
    /// </summary>
    private void CountMaim(MobStateChangedEvent args)
    {
        if (!IsCountableXenofauna(args.Target))
            return;

        var joe = _objectives.ResolveKiller(args.Origin);
        if (joe == null && TryComp(args.Target, out EWJoeXenoHitComponent? hit) && _timing.CurTime - hit.At <= HitMemory)
            joe = hit.Joe;

        if (joe == null || !TryComp(joe, out EWJoeComponent? comp))
            return;

        foreach (var objective in comp.CurrentObjectives)
        {
            if (comp.CompletedCredit.ContainsKey(objective) || !TryComp(objective, out EWJoeXenoCounterComponent? counter))
                continue;

            if (counter.Counted.Add(args.Target))
                _counter.IncreaseCount(objective);
        }
    }

    /// <summary>
    /// An organic downed or killed by xenofauna is stamped, so its corpse is a victim for "Gather N victims"
    /// even when the threat has no infection mechanic (apes, wendigo). The stamp is kept through a later
    /// finishing blow from anyone else, and removed on revival.
    /// </summary>
    private void StampXenofaunaKill(MobStateChangedEvent args)
    {
        if (!EWJoeTargetRules.IsOrganic(EntityManager, args.Target))
            return;

        if (ResolveXenofauna(args.Origin) is not { } killer)
            return;

        var stamp = EnsureComp<EWJoeXenofaunaKilledComponent>(args.Target);
        stamp.Killer = killer;
        stamp.At = _timing.CurTime;
    }

    #endregion

    #region Deliver

    private void OnDeliverAssigned(Entity<EWJoeDeliverVictimsConditionComponent> ent, ref ObjectiveAssignedEvent args)
    {
        if (args.Cancelled || ent.Comp.AreaId != null)
            return;

        if (args.Mind.OwnedEntity is not { } owner || Transform(owner).MapID == MapId.Nullspace)
        {
            args.Cancelled = true;
            return;
        }

        // the xenofauna gate refuses it anyway; skip the area scan
        if (HasComp<EWJoeRequiresXenofaunaComponent>(ent) && !XenofaunaExpected(owner))
            return;

        var areas = FindAreas(_zLevels.GetAllNetworkMapIds(Transform(owner).MapID), ent.Comp.AreaSource);
        if (areas.Count == 0)
        {
            args.Cancelled = true;
            return;
        }

        var (areaId, areaName) = _random.Pick(areas);
        ent.Comp.AreaId = areaId;
        ent.Comp.AreaName = areaName;
        ent.Comp.Count = _random.Next(ent.Comp.Min, ent.Comp.Max + 1);
    }

    private void OnDeliverAfterAssign(Entity<EWJoeDeliverVictimsConditionComponent> ent, ref ObjectiveAfterAssignEvent args)
    {
        _metaData.SetEntityName(ent, Loc.GetString(ent.Comp.Title, ("count", ent.Comp.Count), ("area", ent.Comp.AreaName)), args.Meta);
        _metaData.SetEntityDescription(ent, Loc.GetString(ent.Comp.Description, ("count", ent.Comp.Count), ("area", ent.Comp.AreaName)), args.Meta);
    }

    private void OnDeliverProgress(Entity<EWJoeDeliverVictimsConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        if (ent.Comp.AreaId is not { } areaId || ent.Comp.Count <= 0)
        {
            args.Progress = 0f;
            return;
        }

        args.Progress = Math.Min(1f, CountVictimsIn(areaId) / (float) ent.Comp.Count);
    }

    /// <summary>
    /// A humanoid body the xenofauna have used: burst open or still carrying a xeno or pathogen embryo
    /// (pathogen spores infect through the same <see cref="VictimInfectedComponent"/>), carrying a biomorph
    /// infection or dormant pathogen spores, or a corpse whose critical or killing blow came from xenofauna.
    /// </summary>
    public bool IsXenofaunaVictim(EntityUid uid)
    {
        if (!HasComp<HumanoidProfileComponent>(uid))
            return false;

        return HasComp<VictimBurstComponent>(uid)
            || HasComp<VictimInfectedComponent>(uid)
            || HasComp<BiomorphInfectionComponent>(uid)
            || HasComp<MycotoxPendingComponent>(uid)
            || (HasComp<EWJoeXenofaunaKilledComponent>(uid) && _mobState.IsDead(uid));
    }

    /// <summary>How many xenofauna victims lie in the area <paramref name="areaId"/> right now, each once.</summary>
    public int CountVictimsIn(string areaId)
    {
        var count = 0;
        var bodies = EntityQueryEnumerator<HumanoidProfileComponent, TransformComponent>();
        while (bodies.MoveNext(out var uid, out _, out var xform))
        {
            if (IsXenofaunaVictim(uid) && InArea(xform, areaId))
                count++;
        }

        return count;
    }

    /// <summary>How many xenofauna victims lie anywhere on <paramref name="maps"/>, each once.</summary>
    public int CountVictimsOn(HashSet<MapId> maps)
    {
        var count = 0;
        var bodies = EntityQueryEnumerator<HumanoidProfileComponent, TransformComponent>();
        while (bodies.MoveNext(out var uid, out _, out var xform))
        {
            if (maps.Contains(xform.MapID) && IsXenofaunaVictim(uid))
                count++;
        }

        return count;
    }

    private bool InArea(TransformComponent xform, string areaId)
    {
        return _area.TryGetArea(xform.Coordinates, out _, out var proto) && proto.ID == areaId;
    }

    /// <summary>Every distinct area on <paramref name="maps"/> holding at least one whitelisted entity.</summary>
    private List<(string Id, string Name)> FindAreas(HashSet<MapId> maps, Content.Shared.Whitelist.EntityWhitelist source)
    {
        var found = new Dictionary<string, string>();
        if (source.Components == null)
            return new List<(string, string)>();

        foreach (var name in source.Components)
        {
            if (!_factory.TryGetRegistration(name, out var registration))
            {
                Log.Error($"Deliver-victims objective area source names unknown component {name}");
                continue;
            }

            foreach (var (uid, _) in EntityManager.GetAllComponents(registration.Type))
            {
                var xform = Transform(uid);
                if (!maps.Contains(xform.MapID) || !_area.TryGetArea(xform.Coordinates, out var area, out var proto))
                    continue;

                found.TryAdd(proto.ID, Name(area.Value));
            }
        }

        var list = new List<(string, string)>();
        foreach (var (id, areaName) in found)
        {
            list.Add((id, areaName));
        }

        return list;
    }

    #endregion
}
