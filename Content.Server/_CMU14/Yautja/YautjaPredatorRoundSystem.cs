using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Maps;
using Content.Server.Spawners.Components;
using Content.Server.Spawners.EntitySystems;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Roles;
using Content.Shared._CMU14.Yautja;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Configuration;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Log;
using Robust.Shared.Random;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaPredatorRoundSystem : GameRuleSystem<YautjaPredatorRoundComponent>
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private StationJobsSystem _stationJobs = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private StationSpawningSystem _stationSpawning = default!;
    [Dependency] private IRobustRandom _random = default!;

    private readonly ISawmill _sawmill = Logger.GetSawmill("cmu.yautja.round");
    private readonly YautjaPredatorRoundSchedule _randomSchedule = new(1);
    private int _configuredHunterSlots;
    private bool _randomEnabled;
    private int _lastRandomAttemptRoundId;

    public bool RandomEnabled => _randomEnabled;
    public bool RoundActive => GameTicker.RunLevel == GameRunLevel.InRound;
    public int CurrentRoundId => GameTicker.RoundId;
    public int RandomMinimumRounds => GetRandomMinimumRounds();
    public int RandomMaximumRounds => GetRandomMaximumRounds();
    public int RandomRoundsRemaining => _randomEnabled ? _randomSchedule.RoundsRemaining : 0;
    public int ConfiguredHunterSlots => _configuredHunterSlots;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RulePlayerSpawningEvent>(OnRulePlayerSpawning);
        SubscribeLocalEvent<PlayerSpawningEvent>(OnPlayerSpawning, before: [typeof(SpawnPointSystem)]);
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnGameRunLevelChanged);

        Subs.CVar(_configuration,
            YautjaPredatorRoundCVars.HunterSlots,
            value => _configuredHunterSlots = Math.Clamp(value, 0, 50),
            true);
        Subs.CVar(_configuration,
            YautjaPredatorRoundCVars.RandomEnabled,
            SetRandomEnabled,
            true);
        Subs.CVar(_configuration,
            YautjaPredatorRoundCVars.RandomMinimumRounds,
            _ => OnRandomIntervalChanged(),
            true);
        Subs.CVar(_configuration,
            YautjaPredatorRoundCVars.RandomMaximumRounds,
            _ => OnRandomIntervalChanged(),
            true);
    }

    public bool TryInitializePredatorRound(out string message)
    {
        if (GameTicker.RunLevel != GameRunLevel.InRound)
        {
            message = Loc.GetString("cmu-yautja-admin-editor-round-only");
            return false;
        }

        if (TryGetActivePredatorRule(out var activeRule))
        {
            EnsurePredatorRound(activeRule);
            message = Loc.GetString("cmu-yautja-admin-editor-hunt-already-initialized");
            return true;
        }

        if (!GameTicker.StartGameRule("CMUYautjaPredatorRound", out var ruleUid) ||
            !TryComp(ruleUid, out YautjaPredatorRoundComponent? component))
        {
            message = Loc.GetString("cmu-yautja-admin-editor-hunt-initialize-failed");
            return false;
        }

        EnsurePredatorRound((ruleUid, component));
        message = Loc.GetString("cmu-yautja-admin-editor-hunt-initialized");
        return true;
    }

    public bool TrySetHunterSlots(int slots, out string message)
    {
        if (slots is < 1 or > 50)
        {
            message = Loc.GetString("cmu-yautja-admin-editor-slots-invalid");
            return false;
        }

        _configuration.SetCVar(YautjaPredatorRoundCVars.HunterSlots, slots);

        var applied = false;
        var query = QueryActiveRules();
        while (query.MoveNext(out var uid, out _, out var component, out var gameRule))
        {
            if (!GameTicker.IsGameRuleAdded(uid, gameRule) || !component.ModePredator)
                continue;

            component.MinSlots = slots;
            component.MaxSlots = slots;
            component.Slots = slots;
            SetSlots(component.PredatorJob, slots, component.HunterShipMap);
            applied = true;
        }

        message = applied
            ? Loc.GetString("cmu-yautja-admin-editor-slots-applied", ("slots", slots))
            : Loc.GetString("cmu-yautja-admin-editor-slots-saved", ("slots", slots));
        return true;
    }

    public bool TryConfigureRandom(bool enabled, int minimumRounds, int maximumRounds, out string message)
    {
        if (minimumRounds is < 1 or > 100 ||
            maximumRounds is < 1 or > 100 ||
            minimumRounds > maximumRounds)
        {
            message = Loc.GetString("cmu-yautja-admin-editor-random-invalid");
            return false;
        }

        _configuration.SetCVar(YautjaPredatorRoundCVars.RandomMinimumRounds, minimumRounds);
        _configuration.SetCVar(YautjaPredatorRoundCVars.RandomMaximumRounds, maximumRounds);
        _configuration.SetCVar(YautjaPredatorRoundCVars.RandomEnabled, enabled);
        ScheduleNextRandomHunt();

        message = enabled
            ? Loc.GetString("cmu-yautja-admin-editor-random-enabled-message", ("minimum", minimumRounds), ("maximum", maximumRounds))
            : Loc.GetString("cmu-yautja-admin-editor-random-disabled");
        return true;
    }

    public bool TryGetActiveHunterSlots(out int slots)
    {
        if (TryGetActivePredatorRule(out var rule))
        {
            slots = rule.Comp.Slots;
            return true;
        }

        slots = 0;
        return false;
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

            EnsurePredatorRound((uid, comp), !comp.HunterShipLoaded);
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

    private void OnGameRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (!_randomEnabled || ev.New != GameRunLevel.InRound || GameTicker.RoundId <= 0)
            return;

        if (!_randomSchedule.CountRound(GameTicker.RoundId) ||
            _lastRandomAttemptRoundId == GameTicker.RoundId)
        {
            return;
        }

        _lastRandomAttemptRoundId = GameTicker.RoundId;
        if (!TryInitializePredatorRound(out var message))
        {
            _sawmill.Warning($"Automatic Yautja hunt initialization failed: {message}");
        }

        ScheduleNextRandomHunt();
    }

    private void SetRandomEnabled(bool enabled)
    {
        _randomEnabled = enabled;
        _lastRandomAttemptRoundId = 0;

        if (enabled)
            ScheduleNextRandomHunt();
        else
            _randomSchedule.Reset(1);
    }

    private void OnRandomIntervalChanged()
    {
        if (_randomEnabled)
            ScheduleNextRandomHunt();
    }

    private void ScheduleNextRandomHunt()
    {
        if (!_randomEnabled)
            return;

        _randomSchedule.Reset(_random.Next(GetRandomMinimumRounds(), GetRandomMaximumRounds() + 1));
    }

    private int GetRandomMinimumRounds()
    {
        return Math.Clamp(_configuration.GetCVar(YautjaPredatorRoundCVars.RandomMinimumRounds), 1, 100);
    }

    private int GetRandomMaximumRounds()
    {
        return Math.Clamp(
            _configuration.GetCVar(YautjaPredatorRoundCVars.RandomMaximumRounds),
            GetRandomMinimumRounds(),
            100);
    }

    private bool TryGetActivePredatorRule(out Entity<YautjaPredatorRoundComponent> rule)
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out var uid, out _, out var component, out var gameRule))
        {
            if (!GameTicker.IsGameRuleAdded(uid, gameRule) || !component.ModePredator)
                continue;

            rule = (uid, component);
            return true;
        }

        rule = default;
        return false;
    }

    private void EnsurePredatorRound(Entity<YautjaPredatorRoundComponent> rule, bool applySlots = true)
    {
        if (!rule.Comp.ModePredator)
        {
            if (applySlots)
                SetSlots(rule.Comp.PredatorJob, 0, rule.Comp.HunterShipMap);
            return;
        }

        if (rule.Comp.Slots <= 0)
        {
            if (_configuredHunterSlots > 0)
            {
                rule.Comp.MinSlots = _configuredHunterSlots;
                rule.Comp.MaxSlots = _configuredHunterSlots;
                rule.Comp.Slots = _configuredHunterSlots;
            }
            else
            {
                rule.Comp.Slots = RobustRandom.Next(rule.Comp.MinSlots, rule.Comp.MaxSlots + 1);
            }
        }

        if (rule.Comp.LoadHunterShip && !rule.Comp.HunterShipLoaded)
        {
            if (!HasPredatorSpawnPoint(rule.Comp.PredatorJob))
            {
                var map = _prototypes.Index(rule.Comp.HunterShipMap);
                var options = DeserializationOptions.Default with { InitializeMaps = true };
                GameTicker.LoadGameMap(map, out _, options);
            }

            rule.Comp.HunterShipLoaded = true;
        }

        if (applySlots)
            SetSlots(rule.Comp.PredatorJob, rule.Comp.Slots, rule.Comp.HunterShipMap);
    }

    private void SetSlots(ProtoId<JobPrototype> job, int slots, ProtoId<GameMapPrototype> map)
    {
        // Job slots are scoped to a station. A predator round has one shared
        // cap, so expose the role only on the station that owns the predator
        // spawn points. Setting the same count on every station would multiply
        // the effective cap by the number of stations in the round.
        var predatorStations = GetPredatorStations(job, map);
        if (predatorStations.Count == 0)
            return;

        var query = EntityQueryEnumerator<StationJobsComponent>();
        while (query.MoveNext(out var station, out var stationJobs))
        {
            if (!predatorStations.Contains(station))
                continue;

            _stationJobs.SetRoundStartJobSlot(station, job, slots, stationJobs);
            _stationJobs.TrySetJobSlot(station, job.Id, slots, true, stationJobs);
        }
    }

    private HashSet<EntityUid> GetPredatorStations(ProtoId<JobPrototype> job, ProtoId<GameMapPrototype> map)
    {
        var stations = new HashSet<EntityUid>();
        var query = EntityQueryEnumerator<YautjaHuntSpawnPointComponent, TransformComponent>();
        while (query.MoveNext(out var spawnPoint, out _, out var transform))
        {
            if (_station.GetOwningStation(spawnPoint, transform) is { } station)
                stations.Add(station);
        }

        // Z-level maps are linked to the hunter ship's station but their grids
        // do not carry StationMemberComponent, so resolve the station by the
        // map name when the spawn point itself has no owner.
        if (stations.Count == 0)
        {
            var mapName = _prototypes.Index(map).MapName;
            var stationQuery = EntityQueryEnumerator<StationDataComponent, MetaDataComponent>();
            while (stationQuery.MoveNext(out var station, out _, out var metadata))
            {
                if (metadata.EntityName == mapName)
                    stations.Add(station);
            }
        }

        return stations;
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
