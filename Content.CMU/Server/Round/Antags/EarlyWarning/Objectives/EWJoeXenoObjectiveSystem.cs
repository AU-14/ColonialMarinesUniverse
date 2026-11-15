using Content.Server.Objectives.Systems;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Damage.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Objectives.Components;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;

/// <summary>
/// The Early Warning Joe's Distress Signal directives: maim N xenofauna, and gather N of the xenofauna's
/// victims (burst corpses or the still-infected) in a pinned area. Both are refused outside Distress
/// Signal; the level pools list them, so an admin-made Joe in that mode can roll them.
/// </summary>
public sealed partial class EWJoeXenoObjectiveSystem : EntitySystem
{
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private AreaSystem _area = default!;
    [Dependency] private AuRoundSystem _auRound = default!;
    [Dependency] private CounterConditionSystem _counter = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private EWJoeObjectiveSystem _objectives = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    public const string DistressSignalPreset = "DistressSignal";

    /// <summary>How long after the Joe's last hit a xeno falling still counts as the Joe's work.</summary>
    public static readonly TimeSpan HitMemory = TimeSpan.FromSeconds(6);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeDistressSignalOnlyComponent, ObjectiveAssignedEvent>(OnDistressOnlyAssigned);
        SubscribeLocalEvent<EWJoeDeliverVictimsConditionComponent, ObjectiveAssignedEvent>(OnDeliverAssigned);
        SubscribeLocalEvent<EWJoeDeliverVictimsConditionComponent, ObjectiveAfterAssignEvent>(OnDeliverAfterAssign);
        SubscribeLocalEvent<EWJoeDeliverVictimsConditionComponent, ObjectiveGetProgressEvent>(OnDeliverProgress);
        // broadcast: the objective ladder already listens to deaths the same way
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<XenoComponent, DamageChangedEvent>(OnXenoDamaged);
    }

    /// <summary>A Distress Signal round: the preset was chosen, or its rule is running.</summary>
    public bool IsDistressSignal()
    {
        if (string.Equals(_auRound.SelectedPreset?.ID, DistressSignalPreset, StringComparison.OrdinalIgnoreCase))
            return true;

        var rules = EntityQueryEnumerator<CMDistressSignalRuleComponent>();
        return rules.MoveNext(out _, out _);
    }

    private void OnDistressOnlyAssigned(Entity<EWJoeDistressSignalOnlyComponent> ent, ref ObjectiveAssignedEvent args)
    {
        if (!IsDistressSignal())
            args.Cancelled = true;
    }

    #region Maim

    /// <summary>Remembers the Joe behind each hit on a xeno, for the grace-delayed critical below.</summary>
    private void OnXenoDamaged(Entity<XenoComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || _objectives.ResolveKiller(args.Origin) is not { } joe || !HasComp<EWJoeComponent>(joe))
            return;

        var hit = EnsureComp<EWJoeXenoHitComponent>(ent);
        hit.Joe = joe;
        hit.At = _timing.CurTime;
    }

    /// <summary>
    /// A xeno brought to critical or killed by the Joe's hand, weapon or trigger counts once toward each of
    /// the Joe's open maim objectives. RMC holds a xeno in a grace period before critical and then re-checks
    /// its thresholds with no origin, so a state change that names nobody falls back to the Joe's recent hit.
    /// Facehuggers do not count.
    /// </summary>
    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Critical && args.NewMobState != MobState.Dead)
            return;

        if (!HasComp<XenoComponent>(args.Target) || HasComp<XenoParasiteComponent>(args.Target))
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

    #endregion

    #region Deliver

    private void OnDeliverAssigned(Entity<EWJoeDeliverVictimsConditionComponent> ent, ref ObjectiveAssignedEvent args)
    {
        // the Distress Signal gate refuses it anyway; skip the area scan
        if (args.Cancelled || ent.Comp.AreaId != null || !IsDistressSignal())
            return;

        if (args.Mind.OwnedEntity is not { } owner || Transform(owner).MapID == MapId.Nullspace)
        {
            args.Cancelled = true;
            return;
        }

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

    /// <summary>A body the xenofauna have used: burst open, or still carrying what they put in it.</summary>
    public bool IsXenofaunaVictim(EntityUid uid)
    {
        return HasComp<HumanoidProfileComponent>(uid)
            && (HasComp<VictimBurstComponent>(uid) || HasComp<VictimInfectedComponent>(uid));
    }

    /// <summary>How many xenofauna victims lie in the area <paramref name="areaId"/> right now, each once.</summary>
    public int CountVictimsIn(string areaId)
    {
        var victims = new HashSet<EntityUid>();
        var burst = EntityQueryEnumerator<VictimBurstComponent, TransformComponent>();
        while (burst.MoveNext(out var uid, out _, out var xform))
        {
            if (IsXenofaunaVictim(uid) && InArea(xform, areaId))
                victims.Add(uid);
        }

        var infected = EntityQueryEnumerator<VictimInfectedComponent, TransformComponent>();
        while (infected.MoveNext(out var uid, out _, out var xform))
        {
            if (IsXenofaunaVictim(uid) && InArea(xform, areaId))
                victims.Add(uid);
        }

        return victims.Count;
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
