using System.Linq;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.GameTicking.Rules;
using Content.Server.Maps;
using Content.Server.Preferences.Managers;
using Content.Server.Spawners.Components;
using Content.Server.Spawners.EntitySystems;
using Content.Server.Station.Components;
using Content.Server.Station.Events;
using Content.Server.Station.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Roles;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Preferences;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Configuration;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._CMU14.Yautja;

public enum YautjaSpawnKind : byte
{
    HunterShipClan,
    HuntingGroundsYoungblood,
    SurvivorBase,
}

public readonly record struct YautjaRankSpawnPolicy(YautjaSpawnKind SpawnKind, bool BypassSlotCap);

public sealed partial class YautjaPredatorRoundSystem : GameRuleSystem<YautjaPredatorRoundComponent>
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private StationJobsSystem _stationJobs = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private StationSpawningSystem _stationSpawning = default!;
    [Dependency] private YautjaRankManager _rankManager = default!;
    [Dependency] private IServerPreferencesManager _preferences = default!;
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

    public static YautjaRankSpawnPolicy GetRankSpawnPolicy(YautjaRank rank)
    {
        if (!Enum.IsDefined(rank))
            rank = YautjaRank.Blooded;

        return rank == YautjaRank.YoungBlood
            ? new YautjaRankSpawnPolicy(YautjaSpawnKind.HuntingGroundsYoungblood, false)
            : new YautjaRankSpawnPolicy(
                YautjaSpawnKind.HunterShipClan,
                YautjaRankMetadata.For(rank).BypassesPredatorSlotCap);
    }

    public static bool IsHunterSlotReservedForOrdinaryRank(int? available, int bypassSlotsRemaining)
    {
        return bypassSlotsRemaining > 0 && available is { } slots && slots <= bypassSlotsRemaining;
    }

    public static bool ShouldExcludeOrdinaryRankFromHunterCandidates(
        YautjaRank rank,
        int? available,
        int bypassSlotsRemaining)
    {
        return !GetRankSpawnPolicy(rank).BypassSlotCap &&
            IsHunterSlotReservedForOrdinaryRank(available, bypassSlotsRemaining);
    }

    public static bool ShouldClearExplicitHunterJob(
        YautjaRank rank,
        int? available,
        int bypassSlotsRemaining)
    {
        return ShouldExcludeOrdinaryRankFromHunterCandidates(rank, available, bypassSlotsRemaining);
    }

    public YautjaRank ResolveRankForSession(ICommonSession session, bool youngbloodRole = false)
    {
        return ResolveCapabilitiesForSession(session, youngbloodRole).Rank;
    }

    public YautjaProfileCapabilities ResolveCapabilitiesForSession(
        ICommonSession session,
        bool youngbloodRole = false)
    {
        return ResolveCapabilitiesForPlayer(session.UserId, youngbloodRole);
    }

    private YautjaRank ResolveRankForPlayer(NetUserId userId, bool youngbloodRole = false)
    {
        return ResolveCapabilitiesForPlayer(userId, youngbloodRole).Rank;
    }

    private YautjaProfileCapabilities ResolveCapabilitiesForPlayer(
        NetUserId userId,
        bool youngbloodRole = false)
    {
        var capabilities = _rankManager.ResolveProfileCapabilitiesCached(userId, youngbloodRole);
        var status = (_preferences.GetPreferencesOrNull(userId)?.SelectedCharacter as HumanoidCharacterProfile)?
            .YautjaProfile.Status ?? YautjaProfileStatus.Normal;
        return capabilities.ForStatus(status);
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RulePlayerSpawningEvent>(OnRulePlayerSpawning);
        SubscribeLocalEvent<StationJobsGetCandidatesEvent>(OnStationJobsGetCandidates);
        SubscribeLocalEvent<StationJobsRoundStartPlayerAssignedEvent>(OnStationJobsRoundStartPlayerAssigned);
        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnPlayerBeforeSpawn);
        SubscribeLocalEvent<GetDisallowedJobsEvent>(OnGetDisallowedJobs);
        SubscribeLocalEvent<PlayerSpawningEvent>(OnPlayerSpawning, before: [typeof(SpawnPointSystem)]);
        SubscribeLocalEvent<PlayerJoinedLobbyEvent>(OnPlayerJoinedLobby);
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
            SetSlots(component.PredatorJob, slots + component.RankBypassSlotsRemaining, component.HunterShipMap);
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

            if (!comp.ModePredator)
                continue;

            comp.RankBypassSlotsRemaining = ev.PlayerPool.Count(session =>
                GetRankSpawnPolicy(ResolveRankForSession(session)).BypassSlotCap);
            comp.RoundStartBypassSlotsRemaining = comp.RankBypassSlotsRemaining;
            comp.RoundStartHunterSlotsRemaining = comp.Slots + comp.RankBypassSlotsRemaining;
            SetSlots(comp.PredatorJob, comp.Slots + comp.RankBypassSlotsRemaining, comp.HunterShipMap);
        }
    }

    private void OnStationJobsGetCandidates(ref StationJobsGetCandidatesEvent ev)
    {
        if (!TryGetActivePredatorRule(out var rule) ||
            !rule.Comp.ModePredator ||
            !ev.Jobs.Contains(rule.Comp.PredatorJob) ||
            !IsHunterSlotReservedForOrdinaryRank(
                rule.Comp.RoundStartHunterSlotsRemaining,
                rule.Comp.RoundStartBypassSlotsRemaining))
        {
            return;
        }

        if (ShouldExcludeOrdinaryRankFromHunterCandidates(
                ResolveRankForPlayer(ev.Player),
                rule.Comp.RoundStartHunterSlotsRemaining,
                rule.Comp.RoundStartBypassSlotsRemaining))
        {
            ev.Jobs.Remove(rule.Comp.PredatorJob);
        }
    }

    private void OnStationJobsRoundStartPlayerAssigned(StationJobsRoundStartPlayerAssignedEvent ev)
    {
        if (!TryGetActivePredatorRule(out var rule) ||
            !rule.Comp.ModePredator ||
            ev.Job != rule.Comp.PredatorJob)
        {
            return;
        }

        if (rule.Comp.RoundStartHunterSlotsRemaining > 0)
            rule.Comp.RoundStartHunterSlotsRemaining--;

        if (GetRankSpawnPolicy(ResolveRankForPlayer(ev.Player)).BypassSlotCap &&
            rule.Comp.RoundStartBypassSlotsRemaining > 0)
        {
            rule.Comp.RoundStartBypassSlotsRemaining--;
        }
    }

    private void OnPlayerBeforeSpawn(PlayerBeforeSpawnEvent ev)
    {
        if (!TryGetActivePredatorRule(out var rule) || ev.JobId != rule.Comp.PredatorJob.Id)
        {
            return;
        }

        var rank = ResolveRankForSession(ev.Player);
        if (GetRankSpawnPolicy(rank).BypassSlotCap)
        {
            EnsureHunterSlot(rule.Comp.PredatorJob, rule.Comp.HunterShipMap);
            return;
        }

        if (TryGetHunterSlots(rule.Comp.PredatorJob, rule.Comp.HunterShipMap, out var available) &&
            ShouldClearExplicitHunterJob(rank, available, rule.Comp.RankBypassSlotsRemaining))
        {
            ev.JobId = null;
        }
    }

    private void OnGetDisallowedJobs(ref GetDisallowedJobsEvent ev)
    {
        if (!TryGetActivePredatorRule(out var rule))
            return;

        var rank = ResolveRankForSession(ev.Player);
        if (GetRankSpawnPolicy(rank).BypassSlotCap)
        {
            // A late-joining senior rank must be able to select the Hunter job
            // even after the ordinary pool has reached zero.
            EnsureHunterSlot(rule.Comp.PredatorJob, rule.Comp.HunterShipMap);
            return;
        }

        if (!TryGetHunterSlots(rule.Comp.PredatorJob, rule.Comp.HunterShipMap, out var available) ||
            !IsHunterSlotReservedForOrdinaryRank(available, rule.Comp.RankBypassSlotsRemaining))
        {
            return;
        }

        // Keep the remaining senior reservations out of the ordinary picker.
        ev.Jobs.Add(rule.Comp.PredatorJob);
    }

    private async void OnPlayerJoinedLobby(PlayerJoinedLobbyEvent ev)
    {
        try
        {
            await _rankManager.Prime(ev.PlayerSession.UserId);
        }
        catch (Exception exception)
        {
            _sawmill.Warning($"Failed to prime Yautja rank for {ev.PlayerSession.UserId}: {exception.Message}");
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

            var entitlementCapabilities = ev.PlayerSession is { } session
                ? _rankManager.ResolveProfileCapabilitiesCached(session.UserId)
                : YautjaProfileCapabilities.Default;
            var activeCapabilities = entitlementCapabilities.ForStatus(
                ev.HumanoidCharacterProfile?.YautjaProfile.Status ?? YautjaProfileStatus.Normal);
            var rank = activeCapabilities.Rank;
            var spawnKind = GetRankSpawnPolicy(rank).SpawnKind;

            EnsurePredatorRound((uid, comp), !comp.HunterShipLoaded);
            if (GetRandomPredatorSpawn(comp.PredatorJob, spawnKind) is not { } coordinates)
                return;

            if (GetRankSpawnPolicy(rank).BypassSlotCap && comp.RankBypassSlotsRemaining > 0)
                comp.RankBypassSlotsRemaining--;

            ev.SpawnResult = _stationSpawning.SpawnPlayerMob(
                coordinates,
                ev.Job,
                ev.HumanoidCharacterProfile,
                ev.Station,
                authoritativeYautjaRank: rank,
                authoritativeYautjaCapabilities: entitlementCapabilities);
            return;
        }
    }

    private void EnsureHunterSlot(ProtoId<JobPrototype> job, ProtoId<GameMapPrototype> map)
    {
        if (TryGetHunterSlots(job, map, out var available) &&
            (available is null || available.Value > 0))
            return;

        foreach (var station in GetPredatorStations(job, map))
        {
            _stationJobs.TryAdjustJobSlot(station, job.Id, 1, true);
            break;
        }
    }

    private bool TryGetHunterSlots(
        ProtoId<JobPrototype> job,
        ProtoId<GameMapPrototype> map,
        out int? available)
    {
        foreach (var station in GetPredatorStations(job, map))
        {
            if (_stationJobs.TryGetJobSlot(station, job.Id, out available))
                return true;
        }

        available = null;
        return false;
    }

    private void OnGameRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        // Integration tests intentionally start rounds without a selected game preset.
        // Do not inject a random Yautja rule into those dummy rounds.
        if (!_randomEnabled ||
            ev.New != GameRunLevel.InRound ||
            GameTicker.RoundId <= 0 ||
            (GameTicker.CurrentPreset == null && GameTicker.Preset == null))
        {
            return;
        }

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
        var query = EntityQueryEnumerator<YautjaPredatorSpawnPointComponent, SpawnPointComponent>();
        while (query.MoveNext(out _, out _, out var spawn))
        {
            if (spawn.SpawnType == SpawnPointType.Job && spawn.Job == job)
                return true;
        }

        return false;
    }

    private EntityCoordinates? GetRandomPredatorSpawn(ProtoId<JobPrototype> job, YautjaSpawnKind kind)
    {
        var candidates = new List<EntityCoordinates>();
        var query = EntityQueryEnumerator<YautjaPredatorSpawnPointComponent, SpawnPointComponent, TransformComponent>();
        while (query.MoveNext(out _, out var predatorSpawn, out var spawn, out var xform))
        {
            if (predatorSpawn.Kind != kind ||
                spawn.SpawnType != SpawnPointType.Job ||
                spawn.Job != job)
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
