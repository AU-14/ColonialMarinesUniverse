#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.Spawners;
using Content.Server.CMU14.Round;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.CMU14;
using Content.Shared.GameTicking;
using Content.Shared.Maps;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Station.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
[TestOf(typeof(StationJobsSystem))]
public sealed class StationJobsMergeRegressionTest : GameTest
{
    private const string MapId = "StationJobsMergeMap";
    private static readonly ProtoId<JobPrototype> Govfor = "AU14JobGOVFORSquadRifleman";
    private static readonly ProtoId<JobPrototype> Opfor = "AU14JobOPFORSquadRifleman";
    private static readonly ProtoId<JobPrototype> ThreatLeader = "AU14JobThreatLeader";
    private static readonly ProtoId<JobPrototype> ThreatMember = "AU14JobThreatMember";
    private static readonly ProtoId<JobPrototype> ThirdPartyLeader = "AU14JobThirdPartyLeader";
    private static readonly ProtoId<JobPrototype> GovforSaw = "AU14JobGOVFORSquadAutomaticRifleman";
    private static readonly ProtoId<JobPrototype> OpforSaw = "AU14JobOPFORSquadAutomaticRifleman";
    private static readonly ProtoId<JobPrototype> GovforNurse = "AU14JobGOVFORNurse";
    private static readonly ProtoId<JobPrototype> OpforNurse = "AU14JobOPFORNurse";

    [TestPrototypes]
    private const string Prototypes = $@"
- type: gameMap
  id: {MapId}
  minPlayers: 0
  mapName: {MapId}
  mapPath: /Maps/Test/empty.yml
  stations:
    Merge:
      mapNameTemplate: Merge
      stationProto: StandardNanotrasenStation
      components:
      - type: StationJobs
        availableJobs:
          AU14JobCivilianColonist: [-1, -1]
          AU14JobGOVFORSquadRifleman: [-1, -1]
          AU14JobOPFORSquadRifleman: [-1, -1]
          AU14JobGOVFORSquadAutomaticRifleman: [-1, -1]
          AU14JobOPFORSquadAutomaticRifleman: [-1, -1]
    MergeGovOnly:
      mapNameTemplate: MergeGovOnly
      stationProto: StandardNanotrasenStation
      components:
      - type: StationJobs
        availableJobs:
          AU14JobGOVFORSquadRifleman: [-1, -1]

- type: entity
  id: StationJobsMergeAnyJobSpawn
  components:
  - type: SpawnPoint
    spawn_type: Job
    job_id: null

- type: entity
  id: StationJobsMergeLateJoinSpawn
  components:
  - type: SpawnPoint
    spawn_type: LateJoin

- type: entity
  id: StationJobsMergeObserverSpawn
  components:
  - type: SpawnPoint
    spawn_type: Observer
";

    [Test]
    public async Task ForcedAssignmentsOverflowAlternationAndRoundStartSlotsRemainDistinct()
    {
        var jobs = Server.System<StationJobsSystem>();
        var stations = Server.System<StationSystem>();
        var forced = Server.System<AuJobSelectionSystem>();
        var ticker = Server.System<GameTicker>();
        var map = SProtoMan.Index<GameMapPrototype>(MapId);
        var forceOnForce = SProtoMan.Index<GamePresetPrototype>("ForceOnForce");
        var originalCurrentPreset = ticker.CurrentPreset;

        EntityUid station = default;
        await Server.WaitPost(() =>
        {
            station = stations.InitializeNewStation(map.Stations["Merge"], null, "Merge", map);
        });

        var dummies = await Server.AddDummySessions(6);
        var stayInLobby = dummies.ToDictionary(
            session => session.UserId,
            _ => new HumanoidCharacterProfile()
                .WithJobPriorities(Array.Empty<KeyValuePair<ProtoId<JobPrototype>, JobPriority>>())
                .WithPreferenceUnavailable(PreferenceUnavailableMode.StayInLobby));
        stayInLobby[dummies[0].UserId] = stayInLobby[dummies[0].UserId].WithJobPriority(ThirdPartyLeader, JobPriority.Low);
        stayInLobby[dummies[1].UserId] = stayInLobby[dummies[1].UserId].WithJobPriority(ThreatMember, JobPriority.Low);
        stayInLobby[dummies[2].UserId] = stayInLobby[dummies[2].UserId].WithJobPriority(Govfor, JobPriority.Low);

        try
        {
            await Server.WaitAssertion(() =>
            {
                forced.ForcedJobAssignments.Clear();
                forced.ForcedJobAssignments[dummies[0].UserId] = ThirdPartyLeader.Id;
                forced.ForcedJobAssignments[dummies[1].UserId] = ThreatMember.Id;
                forced.ForcedJobAssignments[dummies[2].UserId] = Govfor.Id;

                var assigned = jobs.AssignJobs(stayInLobby, [station]);
                Assert.Multiple(() =>
                {
                    Assert.That(assigned.ContainsKey(dummies[0].UserId), Is.False,
                        "an absent ThirdParty utility role must not create a placeholder assignment");
                    Assert.That(assigned[dummies[1].UserId],
                        Is.EqualTo(((ProtoId<JobPrototype>?) ThreatMember, station)),
                        "other absent forced roles retain the historical first-station fallback");
                    Assert.That(assigned[dummies[2].UserId],
                        Is.EqualTo(((ProtoId<JobPrototype>?) Govfor, station)));
                    Assert.That(assigned, Has.Count.EqualTo(2),
                        "forced players must not flow through normal preference assignment a second time");
                });

                jobs.SetRoundStartJobSlot(station, Govfor, 3);
                jobs.SetRoundStartJobSlot(station, ThreatLeader, 2);
                var component = SEntMan.GetComponent<StationJobsComponent>(station);
                Assert.Multiple(() =>
                {
                    Assert.That(component.SetupAvailableJobs[Govfor], Is.EqualTo(new[] { 3, -1 }));
                    Assert.That(component.SetupAvailableJobs[ThreatLeader], Is.EqualTo(new[] { 2, -1 }));
                });

                forced.ForcedJobAssignments.Clear();
                SetCurrentPreset(ticker, forceOnForce);
                Assert.That(ticker.CurrentPreset?.ID, Is.EqualTo(forceOnForce.ID));

                var overflowProfiles = dummies.Skip(3).ToDictionary(
                    session => session.UserId,
                    _ => HumanoidCharacterProfile.Random()
                        .WithJobPriorities(Array.Empty<KeyValuePair<ProtoId<JobPrototype>, JobPriority>>())
                        .WithJobPriority(Govfor, JobPriority.Low)
                        .WithJobPriority(Opfor, JobPriority.Low)
                        .WithPreferenceUnavailable(PreferenceUnavailableMode.SpawnAsOverflow));

                var firstPass = new Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)>();
                jobs.AssignOverflowJobs(
                    ref firstPass,
                    new[] { dummies[3].UserId, dummies[4].UserId },
                    overflowProfiles,
                    [station]);
                Assert.Multiple(() =>
                {
                    Assert.That(firstPass[dummies[3].UserId].Item1, Is.EqualTo((ProtoId<JobPrototype>?) Govfor));
                    Assert.That(firstPass[dummies[4].UserId].Item1, Is.EqualTo((ProtoId<JobPrototype>?) Opfor));
                });

                // AssignJobs is the round boundary and must reset the alternating side to GOVFOR.
                jobs.AssignJobs(new Dictionary<NetUserId, HumanoidCharacterProfile>(), [station]);
                var resetPass = new Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)>();
                jobs.AssignOverflowJobs(
                    ref resetPass,
                    new[] { dummies[5].UserId },
                    overflowProfiles,
                    [station]);
                Assert.That(resetPass[dummies[5].UserId].Item1, Is.EqualTo((ProtoId<JobPrototype>?) Govfor));
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                forced.ForcedJobAssignments.Clear();
                SetCurrentPreset(ticker, originalCurrentPreset);
            });
        }
    }

    [TestCase("ColonyFall")]
    [TestCase("DistressSignal")]
    [TestCase("ForceOnForce")]
    public async Task NeverBlocksGamemodeOverflow(string presetId)
    {
        var jobs = Server.System<StationJobsSystem>();
        var stations = Server.System<StationSystem>();
        var ticker = Server.System<GameTicker>();
        var map = SProtoMan.Index<GameMapPrototype>(MapId);
        var preset = SProtoMan.Index<GamePresetPrototype>(presetId);
        var originalPreset = ticker.CurrentPreset;
        var station = EntityUid.Invalid;
        await Server.WaitPost(() =>
            station = stations.InitializeNewStation(map.Stations["Merge"], null, "Merge", map));
        var dummies = await Server.AddDummySessions(1);
        var player = dummies[0].UserId;
        var profiles = new Dictionary<NetUserId, HumanoidCharacterProfile>
        {
            [player] = new HumanoidCharacterProfile()
                .WithJobPriorities(Array.Empty<KeyValuePair<ProtoId<JobPrototype>, JobPriority>>())
                .WithPreferenceUnavailable(PreferenceUnavailableMode.SpawnAsOverflow),
        };

        try
        {
            await Server.WaitAssertion(() =>
            {
                SetCurrentPreset(ticker, preset);
                // Round boundary: the FoF alternation opens on GOVFOR regardless of pair history.
                jobs.AssignJobs(new Dictionary<NetUserId, HumanoidCharacterProfile>(), [station]);
                var blocked = new Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)>();
                jobs.AssignOverflowJobs(ref blocked, profiles.Keys, profiles, [station]);
                Assert.That(blocked[player], Is.EqualTo(((ProtoId<JobPrototype>?) null, EntityUid.Invalid)),
                    "Gamemode overflow must not override Never for colonist or either faction's rifleman.");

                profiles[player] = profiles[player].WithJobPriority(Opfor, JobPriority.Low);
                var accepted = new Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)>();
                jobs.AssignOverflowJobs(ref accepted, profiles.Keys, profiles, [station]);
                // FoF deals the alternating side: the queued OPFOR rifleman is served by its
                // GOVFOR mirror on the first deal; other modes deal the queued role directly.
                var expected = presetId == "ForceOnForce"
                    ? (ProtoId<JobPrototype>?) Govfor
                    : (ProtoId<JobPrototype>?) Opfor;
                Assert.That(accepted[player], Is.EqualTo((expected, station)),
                    "An accepted overflow role must remain available when the mode's usual role is Never.");
            });
        }
        finally
        {
            await Server.WaitPost(() => SetCurrentPreset(ticker, originalPreset));
        }
    }

    [Test]
    public async Task OverflowDealtOpforRemapsQueuedGovforRoles()
    {
        var jobs = Server.System<StationJobsSystem>();
        var stations = Server.System<StationSystem>();
        var ticker = Server.System<GameTicker>();
        var map = SProtoMan.Index<GameMapPrototype>(MapId);
        var forceOnForce = SProtoMan.Index<GamePresetPrototype>("ForceOnForce");
        var originalCurrentPreset = ticker.CurrentPreset;

        EntityUid station = default;
        await Server.WaitPost(() =>
        {
            station = stations.InitializeNewStation(map.Stations["Merge"], null, "Merge", map);
        });

        var dummies = await Server.AddDummySessions(2);
        var govforOnlyProfiles = dummies.ToDictionary(
            session => session.UserId,
            _ => new HumanoidCharacterProfile()
                .WithJobPriorities(Array.Empty<KeyValuePair<ProtoId<JobPrototype>, JobPriority>>())
                .WithJobPriority(GovforSaw, JobPriority.Low)
                .WithPreferenceUnavailable(PreferenceUnavailableMode.SpawnAsOverflow));

        try
        {
            await Server.WaitAssertion(() =>
            {
                SetCurrentPreset(ticker, forceOnForce);
                // AssignJobs is the round boundary; it also resets the alternation to GOVFOR.
                jobs.AssignJobs(new Dictionary<NetUserId, HumanoidCharacterProfile>(), [station]);

                var assigned = new Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)>();
                jobs.AssignOverflowJobs(
                    ref assigned,
                    new[] { dummies[0].UserId, dummies[1].UserId },
                    govforOnlyProfiles,
                    [station]);
                Assert.Multiple(() =>
                {
                    Assert.That(assigned[dummies[0].UserId].Item1, Is.EqualTo((ProtoId<JobPrototype>?) GovforSaw),
                        "a dealt GOVFOR slot takes the queued GOVFOR role directly");
                    Assert.That(assigned[dummies[1].UserId].Item1, Is.EqualTo((ProtoId<JobPrototype>?) OpforSaw),
                        "a dealt OPFOR slot must remap the queued GOVFOR role onto its OPFOR mirror");
                });
            });
        }
        finally
        {
            await Server.WaitPost(() => SetCurrentPreset(ticker, originalCurrentPreset));
        }
    }

    [Test]
    public async Task OverflowDealtGovforRemapsQueuedOpforRoles()
    {
        var jobs = Server.System<StationJobsSystem>();
        var stations = Server.System<StationSystem>();
        var ticker = Server.System<GameTicker>();
        var map = SProtoMan.Index<GameMapPrototype>(MapId);
        var forceOnForce = SProtoMan.Index<GamePresetPrototype>("ForceOnForce");
        var originalCurrentPreset = ticker.CurrentPreset;

        EntityUid station = default;
        await Server.WaitPost(() =>
        {
            station = stations.InitializeNewStation(map.Stations["Merge"], null, "Merge", map);
        });

        var dummies = await Server.AddDummySessions(2);
        var opforOnlyProfiles = dummies.ToDictionary(
            session => session.UserId,
            _ => new HumanoidCharacterProfile()
                .WithJobPriorities(Array.Empty<KeyValuePair<ProtoId<JobPrototype>, JobPriority>>())
                .WithJobPriority(OpforSaw, JobPriority.Low)
                .WithPreferenceUnavailable(PreferenceUnavailableMode.SpawnAsOverflow));

        try
        {
            await Server.WaitAssertion(() =>
            {
                SetCurrentPreset(ticker, forceOnForce);
                // AssignJobs is the round boundary; it also resets the alternation to GOVFOR.
                jobs.AssignJobs(new Dictionary<NetUserId, HumanoidCharacterProfile>(), [station]);

                var assigned = new Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)>();
                jobs.AssignOverflowJobs(
                    ref assigned,
                    new[] { dummies[0].UserId, dummies[1].UserId },
                    opforOnlyProfiles,
                    [station]);
                Assert.Multiple(() =>
                {
                    Assert.That(assigned[dummies[0].UserId].Item1, Is.EqualTo((ProtoId<JobPrototype>?) GovforSaw),
                        "a dealt GOVFOR slot must remap the queued OPFOR role onto its GOVFOR mirror");
                    Assert.That(assigned[dummies[1].UserId].Item1, Is.EqualTo((ProtoId<JobPrototype>?) OpforSaw),
                        "a dealt OPFOR slot takes the queued OPFOR role directly");
                });
            });
        }
        finally
        {
            await Server.WaitPost(() => SetCurrentPreset(ticker, originalCurrentPreset));
        }
    }

    [Test]
    public async Task OverflowDealtOpforIsNotCapturedByGovforOnlyStations()
    {
        var jobs = Server.System<StationJobsSystem>();
        var stations = Server.System<StationSystem>();
        var ticker = Server.System<GameTicker>();
        var map = SProtoMan.Index<GameMapPrototype>(MapId);
        var forceOnForce = SProtoMan.Index<GamePresetPrototype>("ForceOnForce");
        var originalCurrentPreset = ticker.CurrentPreset;

        EntityUid station = default;
        await Server.WaitPost(() =>
        {
            station = stations.InitializeNewStation(map.Stations["MergeGovOnly"], null, "MergeGovOnly", map);
        });

        var dummies = await Server.AddDummySessions(2);
        var profiles = dummies.ToDictionary(
            session => session.UserId,
            _ => new HumanoidCharacterProfile()
                .WithJobPriorities(Array.Empty<KeyValuePair<ProtoId<JobPrototype>, JobPriority>>())
                .WithJobPriority(Govfor, JobPriority.Low)
                .WithPreferenceUnavailable(PreferenceUnavailableMode.SpawnAsOverflow));

        try
        {
            await Server.WaitAssertion(() =>
            {
                SetCurrentPreset(ticker, forceOnForce);
                jobs.AssignJobs(new Dictionary<NetUserId, HumanoidCharacterProfile>(), [station]);

                // The first success deals GOVFOR and flips the alternation to OPFOR.
                var first = new Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)>();
                jobs.AssignOverflowJobs(ref first, new[] { dummies[0].UserId }, profiles, [station]);
                Assert.That(first[dummies[0].UserId].Item1, Is.EqualTo((ProtoId<JobPrototype>?) Govfor));

                var second = new Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)>();
                jobs.AssignOverflowJobs(ref second, new[] { dummies[1].UserId }, profiles, [station]);
                Assert.That(second[dummies[1].UserId], Is.EqualTo(((ProtoId<JobPrototype>?) null, EntityUid.Invalid)),
                    "a dealt OPFOR slot must not fall back into the other side's overflow roles");
            });
        }
        finally
        {
            await Server.WaitPost(() => SetCurrentPreset(ticker, originalCurrentPreset));
        }
    }

    [Test]
    public async Task AssignJobsForceOnForceReadsOwnStorePreferences()
    {
        var jobs = Server.System<StationJobsSystem>();
        var stations = Server.System<StationSystem>();
        var ticker = Server.System<GameTicker>();
        var map = SProtoMan.Index<GameMapPrototype>(MapId);
        var forceOnForce = SProtoMan.Index<GamePresetPrototype>("ForceOnForce");
        var originalCurrentPreset = ticker.CurrentPreset;

        EntityUid station = default;
        await Server.WaitPost(() =>
        {
            station = stations.InitializeNewStation(map.Stations["Merge"], null, "Merge", map);
        });

        var dummies = await Server.AddDummySessions(2);
        // The Force on Force tab edits military role preferences under the ForceOnForce key.
        // The flatten below mirrors what GameTicker.SpawnPlayers does before AssignJobs.
        var profiles = dummies.ToDictionary(
            session => session.UserId,
            _ => new HumanoidCharacterProfile()
                .WithGamemodeJobPriority("ForceOnForce", GovforNurse, JobPriority.High)
                .WithPreferenceUnavailable(PreferenceUnavailableMode.SpawnAsOverflow));

        try
        {
            await Server.WaitAssertion(() =>
            {
                SetCurrentPreset(ticker, forceOnForce);

                foreach (var nurse in new[] { GovforNurse, OpforNurse })
                {
                    Assert.That(jobs.TrySetJobSlot(station, nurse.Id, 1, true), Is.True);
                    jobs.SetRoundStartJobSlot(station, nurse, 1);
                }

                var flattened = profiles.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value.WithJobPriorities(
                        pair.Value.GetJobPrioritiesForGamemode("ForceOnForce")));

                var assigned = jobs.AssignJobs(flattened, [station]);
                Assert.That(assigned, Has.Count.EqualTo(2),
                    "a queued nurse preference must translate into a nurse assignment, not overflow");
                var dealt = new[]
                {
                    assigned[dummies[0].UserId].Item1,
                    assigned[dummies[1].UserId].Item1,
                };
                Assert.That(dealt, Is.EquivalentTo(new ProtoId<JobPrototype>?[] { GovforNurse, OpforNurse }),
                    "the dealt GOVFOR slot takes the queued nurse directly and the dealt OPFOR slot takes its mirror");
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                SEntMan.DeleteEntity(station);
                SetCurrentPreset(ticker, originalCurrentPreset);
            });
        }
    }

    [Test]
    public async Task GenericLateJoinAcceptsNullJobAndPrefersJobMarker()
    {
        var map = await Pair.CreateTestMap();
        var spawning = Server.System<StationSpawningSystem>();
        var ticker = Server.System<GameTicker>();
        var stations = Server.System<StationSystem>();
        var originalRunLevel = ticker.RunLevel;

        // The late-join picker scopes spawn points to the given station. A null station
        // scans the whole pair world, where pooled rounds can hold a hunter ship's markers.
        var stationConfig = SProtoMan.Index<GameMapPrototype>(MapId).Stations["Merge"];
        var station = EntityUid.Invalid;
        var anyJob = EntityUid.Invalid;
        var lateJoin = EntityUid.Invalid;
        var observer = EntityUid.Invalid;
        await Server.WaitPost(() =>
            station = stations.InitializeNewStation(stationConfig, [map.Grid], "Merge"));

        try
        {
            await Server.WaitAssertion(() =>
            {
                var transform = Server.System<SharedTransformSystem>();
                var anyJobCoordinates = map.GridCoords.Offset(new Vector2(5, 0));
                var lateJoinCoordinates = map.GridCoords.Offset(new Vector2(10, 0));
                var observerCoordinates = map.GridCoords.Offset(new Vector2(15, 0));
                anyJob = SEntMan.SpawnEntity("StationJobsMergeAnyJobSpawn", anyJobCoordinates);
                lateJoin = SEntMan.SpawnEntity("StationJobsMergeLateJoinSpawn", lateJoinCoordinates);
                observer = SEntMan.SpawnEntity("StationJobsMergeObserverSpawn", observerCoordinates);

                // The picker's station filter needs each marker to resolve to the station. The grid
                // branch of GetOwningStation does not resolve this fixture grid under pooled CI runs,
                // so the markers are tethered through StationTracker, which the filter reads first.
                foreach (var marker in new[] { anyJob, lateJoin, observer })
                {
                    var tracker = SEntMan.AddComponent<StationTrackerComponent>(marker);
                    stations.SetStation((marker, tracker), station);
                }

                SetRunLevel(ticker, GameRunLevel.InRound);

                var preferred = spawning.SpawnPlayerCharacterOnStation(station, null, null);
                Assert.That(preferred, Is.Not.Null);
                Assert.That(transform.GetMapCoordinates(preferred!.Value),
                    Is.EqualTo(transform.ToMapCoordinates(anyJobCoordinates)),
                    "a null-job Job marker must be accepted and preferred over LateJoin");
                SEntMan.DeleteEntity(preferred.Value);
                SEntMan.DeleteEntity(anyJob);

                var fallback = spawning.SpawnPlayerCharacterOnStation(station, null, null);
                Assert.That(fallback, Is.Not.Null);
                Assert.That(transform.GetMapCoordinates(fallback!.Value),
                    Is.EqualTo(transform.ToMapCoordinates(lateJoinCoordinates)),
                    "an Observer marker must remain excluded from the generic late-join fallback");
                SEntMan.DeleteEntity(fallback.Value);
            });
        }
        finally
        {
            // Trackers left on the markers reference the station; once cleanup deletes the
            // station the next PVS tick serializes the dead reference and fails the clean
            // dispose. Delete markers and station in the same tick.
            await Server.WaitPost(() =>
            {
                foreach (var marker in new[] { anyJob, lateJoin, observer })
                {
                    if (!SEntMan.Deleted(marker))
                        SEntMan.DeleteEntity(marker);
                }

                SEntMan.DeleteEntity(station);
                SetRunLevel(ticker, originalRunLevel);
            });
        }
    }

    private static void SetRunLevel(GameTicker ticker, GameRunLevel level)
    {
        var setter = typeof(GameTicker)
            .GetProperty(nameof(GameTicker.RunLevel))!
            .GetSetMethod(nonPublic: true)!;
        setter.Invoke(ticker, new object[] { level });
    }

    private static void SetCurrentPreset(GameTicker ticker, GamePresetPrototype? preset)
    {
        var setter = typeof(GameTicker)
            .GetProperty(nameof(GameTicker.CurrentPreset))!
            .GetSetMethod(nonPublic: true)!;
        setter.Invoke(ticker, new object?[] { preset });
    }
}

#pragma warning restore RA0002
