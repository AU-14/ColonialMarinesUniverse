using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Spawners.Components;
using Content.Server.Spawners.EntitySystems;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Roles;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaPredatorRoundSystem : GameRuleSystem<YautjaPredatorRoundComponent>
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private StationJobsSystem _stationJobs = default!;
    [Dependency] private StationSpawningSystem _stationSpawning = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RulePlayerSpawningEvent>(OnRulePlayerSpawning);
        SubscribeLocalEvent<PlayerSpawningEvent>(OnPlayerSpawning, before: [typeof(SpawnPointSystem)]);
    }

    private void OnRulePlayerSpawning(RulePlayerSpawningEvent ev)
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out var uid, out _, out var comp, out var gameRule))
        {
            if (!GameTicker.IsGameRuleAdded(uid, gameRule))
                continue;

            EnsurePredatorRound((uid, comp));
        }
    }

    private void OnPlayerSpawning(PlayerSpawningEvent ev)
    {
        if (ev.SpawnResult != null || ev.Job is not { } job)
            return;

        var query = QueryActiveRules();
        while (query.MoveNext(out var uid, out _, out var comp, out var gameRule))
        {
            if (!GameTicker.IsGameRuleAdded(uid, gameRule) ||
                !comp.ModePredator ||
                job != comp.PredatorJob)
            {
                continue;
            }

            EnsurePredatorRound((uid, comp));
            if (GetRandomPredatorSpawn(comp.PredatorJob) is not { } coordinates)
                return;

            ev.SpawnResult = _stationSpawning.SpawnPlayerMob(
                coordinates,
                ev.Job,
                ev.HumanoidCharacterProfile,
                ev.Station);
            return;
        }
    }

    private void EnsurePredatorRound(Entity<YautjaPredatorRoundComponent> rule)
    {
        if (!rule.Comp.ModePredator)
        {
            SetSlots(rule.Comp.PredatorJob, 0);
            return;
        }

        if (rule.Comp.Slots <= 0)
            rule.Comp.Slots = RobustRandom.Next(rule.Comp.MinSlots, rule.Comp.MaxSlots + 1);

        if (rule.Comp.LoadHunterShip && !rule.Comp.HunterShipLoaded)
        {
            if (!HasPredatorSpawnPoint(rule.Comp.PredatorJob))
            {
                var map = _prototypes.Index(rule.Comp.HunterShipMap);
                GameTicker.LoadGameMap(map, out _);
            }

            rule.Comp.HunterShipLoaded = true;
        }

        SetSlots(rule.Comp.PredatorJob, rule.Comp.Slots);
    }

    private void SetSlots(ProtoId<JobPrototype> job, int slots)
    {
        var query = EntityQueryEnumerator<StationJobsComponent>();
        while (query.MoveNext(out var station, out var stationJobs))
        {
            _stationJobs.SetRoundStartJobSlot(station, job, slots, stationJobs);
            _stationJobs.TrySetJobSlot(station, job.Id, slots, true, stationJobs);
        }
    }

    private bool HasPredatorSpawnPoint(ProtoId<JobPrototype> job)
    {
        var query = EntityQueryEnumerator<SpawnPointComponent>();
        while (query.MoveNext(out _, out var spawn))
        {
            if (spawn.SpawnType == SpawnPointType.Job && spawn.Job == job)
                return true;
        }

        return false;
    }

    private EntityCoordinates? GetRandomPredatorSpawn(ProtoId<JobPrototype> job)
    {
        var candidates = new List<EntityCoordinates>();
        var query = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        while (query.MoveNext(out _, out var spawn, out var xform))
        {
            if (spawn.SpawnType != SpawnPointType.Job || spawn.Job != job)
                continue;

            candidates.Add(xform.Coordinates);
        }

        return candidates.Count == 0
            ? null
            : RobustRandom.Pick(candidates);
    }

    public void RegisterYoungblood(EntityUid youngblood)
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out var uid, out _, out var comp, out var gameRule))
        {
            if (!GameTicker.IsGameRuleAdded(uid, gameRule) || !comp.ModePredator)
                continue;

            TrackYoungblood((uid, comp), youngblood);
        }
    }

    public void TrackYoungblood(Entity<YautjaPredatorRoundComponent> rule, EntityUid youngblood)
    {
        rule.Comp.Youngbloods.Add(youngblood);
    }

    protected override void AppendRoundEndText(
        EntityUid uid,
        YautjaPredatorRoundComponent component,
        GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, component, gameRule, ref args);

        if (component.Youngbloods.Count == 0)
            return;

        args.AddLine(Loc.GetString("cmu-yautja-youngblood-round-end-header"));
        foreach (var youngblood in component.Youngbloods)
        {
            if (Deleted(youngblood))
                continue;

            var status = Loc.GetString(_mobState.IsDead(youngblood)
                ? "cmu-yautja-youngblood-round-end-dead"
                : "cmu-yautja-youngblood-round-end-alive");
            args.AddLine(Loc.GetString(
                "cmu-yautja-youngblood-round-end-entry",
                ("name", Name(youngblood)),
                ("status", status)));
        }
    }
}
