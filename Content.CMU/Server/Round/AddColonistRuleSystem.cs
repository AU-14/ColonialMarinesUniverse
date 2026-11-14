using System.Collections.Generic;
using System.Linq;
using Content.Server.CMU14.Roles;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Spawners.EntitySystems;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server._RMC14.Rules;
using Content.Shared.CMU14.Round.Roles;
using Content.Shared.CMU14.Round;
using Content.Shared.CMU14.Threats;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Inventory;
using Content.Shared.Radio.Components;
using Content.Shared.Roles;
using JetBrains.Annotations;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Round;

[UsedImplicitly]
public sealed partial class AddColonistRuleSystem : GameRuleSystem<AddColonistRuleComponent>
{
    [Dependency] private StationJobsSystem _stationJobs = default!;
    [Dependency] private StationSystem _stationSystem = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private AuRoundSystem _round = default!;
    [Dependency] private StationSpawningSystem _spawning = default!;
    [Dependency] private RoundJobProfileSystem _roundJobs = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private ISawmill _logs = default!;

    public override void Initialize()
    {
        base.Initialize();
        _logs = Logger.GetSawmill("addcolonist");
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
        SubscribeLocalEvent<PlayerSpawningEvent>(OnPlayerSpawning,
            before: [typeof(CMDistressSignalRuleSystem), typeof(SpawnPointSystem)]);
    }

    protected override void Started(EntityUid uid, AddColonistRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        var mapId = _gameTicker.DefaultMap;
        var stationUid = _stationSystem.GetStationInMap(mapId);
        if (stationUid == null || !Exists(stationUid.Value))
            return;

        var stationJobs = EntityManager.GetComponentOrNull<StationJobsComponent>(stationUid.Value);
        if (stationJobs == null)
            return;

        // The rule opens its own roster after the planet's jobs have been cleared.
        var counts = new Dictionary<ProtoId<JobPrototype>, int>(component.Jobs);
        if (component.RandomJobs.Count > 0)
            counts.TryAdd(_random.Pick(component.RandomJobs), 1);

        foreach (var (jobId, count) in counts)
        {
            _stationJobs.TrySetJobSlot(stationUid.Value, jobId.ToString(), count, true, stationJobs);
            _stationJobs.SetRoundStartJobSlot(stationUid.Value, jobId, count, stationJobs);
        }
    }

    /// <summary>Limit Distress Signal colony jobs to one per four assigned GOVFOR players.</summary>
    public void LimitDistressColonists(Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignments)
    {
        if (!_round.IsDistressSignalSelected)
            return;

        var rules = EntityQueryEnumerator<AddColonistRuleComponent>();
        if (!rules.MoveNext(out _, out var rule))
        {
            _logs.Error("Distress Signal has no AddColonist rule; cannot limit colony assignments.");
            return;
        }

        var markersAvailable = GetSurvivorMarkers().Count > 0;
        var removed = LimitDistressColonistAssignments(assignments, rule,
            job => _prototypes.TryIndex(job, out var prototype) &&
                   _roundJobs.GetRoundSide(prototype) == RoundJobSide.Govfor,
            players => _random.Shuffle(players),
            markersAvailable);
        if (!markersAvailable && removed > 0)
            _logs.Error("No planet third-party member spawn markers found; returning Distress Signal colonists to the lobby.");

        // Only the selected roles remain open; late joins cannot bypass the round-start cap.
        if (_stationSystem.GetStationInMap(_gameTicker.DefaultMap) is not { } station)
        {
            _logs.Error("No planet station found while limiting Distress Signal colonist slots.");
            return;
        }

        foreach (var job in rule.Jobs.Keys.Concat(rule.RandomJobs))
        {
            if (!_stationJobs.GetJobs(station).ContainsKey(job))
                continue;

            var count = assignments.Count(pair => pair.Value.Item1 == job && pair.Value.Item2 == station);
            if (!_stationJobs.TrySetJobSlot(station, job.Id, count))
                _logs.Warning($"Could not set Distress Signal colony job slots for {job}.");
        }
    }

    internal static int GetDistressColonistLimit(int govfor) => Math.Max(0, govfor) / 4;

    internal static int LimitDistressColonistAssignments(
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignments,
        AddColonistRuleComponent rule,
        Func<ProtoId<JobPrototype>, bool> isGovfor,
        Action<List<NetUserId>> shuffle,
        bool markersAvailable)
    {
        var govfor = 0;
        var colonists = new List<NetUserId>();
        foreach (var (player, (job, _)) in assignments)
        {
            if (job is not { } jobId)
                continue;

            if (IsColonistJob(rule, jobId))
                colonists.Add(player);
            else if (isGovfor(jobId))
                govfor++;
        }

        var limit = markersAvailable ? GetDistressColonistLimit(govfor) : 0;
        shuffle(colonists);
        foreach (var player in colonists.Skip(limit))
            assignments[player] = (null, EntityUid.Invalid);

        return Math.Max(0, colonists.Count - limit);
    }

    private static bool IsColonistJob(AddColonistRuleComponent rule, ProtoId<JobPrototype> job)
        => rule.Jobs.ContainsKey(job) || rule.RandomJobs.Contains(job);

    private List<EntityUid> GetSurvivorMarkers()
    {
        var markers = new List<EntityUid>();
        var query = EntityQueryEnumerator<ThreatSpawnMarkerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var marker, out var transform))
        {
            if (marker.ThirdParty && marker.ThreatMarkerType == ThreatMarkerType.Member &&
                marker.ID == string.Empty && transform.MapID == _gameTicker.DefaultMap)
                markers.Add(uid);
        }

        return markers;
    }

    private void OnPlayerSpawning(PlayerSpawningEvent ev)
    {
        if (ev.SpawnResult != null || !_round.IsDistressSignalSelected ||
            ev.Job is not { } job || GetColonistRule(job.Id) == null)
            return;

        var markers = GetSurvivorMarkers();
        if (markers.Count == 0)
        {
            _logs.Error($"No survivor spawn marker available for colonist job {job}.");
            return;
        }

        var marker = _random.Pick(markers);
        ev.SpawnResult = _spawning.SpawnPlayerMob(Transform(marker).Coordinates, job,
            ev.HumanoidCharacterProfile, ev.Station);
    }

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent ev)
    {
        var rule = GetColonistRule(ev.JobId);
        if (rule == null)
            return;

        if (_inventory.TryGetSlotEntity(ev.Mob, "ears", out var existing)
            && HasComp<HeadsetComponent>(existing))
            return;

        var headset = Spawn(rule.Headset, Transform(ev.Mob).Coordinates);
        if (!_inventory.TryEquip(ev.Mob, headset, "ears", silent: true, force: true))
            _logs.Warning($"could not equip {rule.Headset} on a spawned {ev.JobId} colonist");
    }

    private AddColonistRuleComponent? GetColonistRule(string? jobId)
    {
        if (jobId == null)
            return null;

        var query = EntityQueryEnumerator<AddColonistRuleComponent>();
        while (query.MoveNext(out _, out var rule))
        {
            if (rule.Jobs.ContainsKey(jobId) || rule.RandomJobs.Contains(jobId))
                return rule;
        }

        return null;
    }
}
