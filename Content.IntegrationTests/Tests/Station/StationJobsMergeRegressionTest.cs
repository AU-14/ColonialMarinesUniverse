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
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Station;

[TestFixture]
[TestOf(typeof(StationJobsSystem))]
public sealed class StationJobsMergeRegressionTest : GameTest
{
    private const string MapId = "StationJobsMergeMap";
    private static readonly ProtoId<JobPrototype> AegisResearcher = "CMUJobAegisResearcher";
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
    // CMU14: Force on Force roles, hijacking, announcements and identification.
    public async Task ForcedAssignmentsRoundStartSlotsAndForceOnForceOverflowRemainDistinct()
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
                    // CMU14: Force on Force roles, hijacking, announcements and identification.
                    Assert.That(firstPass[dummies[3].UserId].Item1, Is.Null);
                    Assert.That(firstPass[dummies[4].UserId].Item1, Is.Null);
                });

                // CMU14: Force on Force roles, hijacking, announcements and identification.
                // The legacy overflow pass must never bypass the joint FoF roll or its fallback limits.
                jobs.AssignJobs(new Dictionary<NetUserId, HumanoidCharacterProfile>(), [station]);
                var resetPass = new Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)>();
                jobs.AssignOverflowJobs(
                    ref resetPass,
                    new[] { dummies[5].UserId },
                    overflowProfiles,
                    [station]);
                // CMU14: Force on Force roles, hijacking, announcements and identification.
                Assert.That(resetPass[dummies[5].UserId].Item1, Is.Null);
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
                jobs.AssignJobs(new Dictionary<NetUserId, HumanoidCharacterProfile>(), [station]); // CMU14
                var blocked = new Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)>();
                jobs.AssignOverflowJobs(ref blocked, profiles.Keys, profiles, [station]);
                Assert.That(blocked[player], Is.EqualTo(((ProtoId<JobPrototype>?) null, EntityUid.Invalid)),
                    "Gamemode overflow must not override Never for colonist or either faction's rifleman.");

                profiles[player] = profiles[player].WithJobPriority(Opfor, JobPriority.Low);
                var accepted = new Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)>();
                jobs.AssignOverflowJobs(ref accepted, profiles.Keys, profiles, [station]);
                // CMU14: Force on Force roles, hijacking, announcements and identification.
                var expected = presetId == "ForceOnForce"
                    ? ((ProtoId<JobPrototype>?) null, EntityUid.Invalid)
                    : ((ProtoId<JobPrototype>?) Opfor, station);
                Assert.That(accepted[player], Is.EqualTo(expected),
                    "FoF fallback belongs to its balanced joint roll; other modes retain their accepted overflow roles.");
            });
        }
        finally
        {
            await Server.WaitPost(() => SetCurrentPreset(ticker, originalPreset));
        }
    }

    [Test]
    public async Task ForceOnForceRollBalancesFlexiblePlayersAndLeavesStrictOverflowInLobby()
    {
        var jobs = Server.System<StationJobsSystem>();
        var stations = Server.System<StationSystem>();
        var ticker = Server.System<GameTicker>();
        var map = SProtoMan.Index<GameMapPrototype>(MapId);
        var originalPreset = ticker.CurrentPreset;
        EntityUid station = default;
        await Server.WaitPost(() => station = stations.InitializeNewStation(map.Stations["Merge"], null, "FoF", map));
        var players = await Server.AddDummySessions(6);
        var profiles = players.ToDictionary(p => p.UserId, _ => new HumanoidCharacterProfile()
            .WithForceOnForcePreferences(ForceOnForceSide.Govfor, ForceOnForceFallback.OtherSide)
            .WithGamemodeJobPriority("ForceOnForce", Govfor, JobPriority.High));
        try
        {
            await Server.WaitAssertion(() =>
            {
                SetCurrentPreset(ticker, SProtoMan.Index<GamePresetPrototype>("ForceOnForce"));
                var flexible = jobs.AssignJobs(profiles, [station]);
                Assert.That(flexible, Has.Count.EqualTo(6));
                Assert.That(flexible.Values.Count(v => v.Item1 == Govfor), Is.EqualTo(3));
                Assert.That(flexible.Values.Count(v => v.Item1 == Opfor), Is.EqualTo(3));
                foreach (var player in players)
                    profiles[player.UserId] = profiles[player.UserId]
                        .WithForceOnForcePreferences(ForceOnForceSide.Govfor, ForceOnForceFallback.StayInLobby);
                profiles[players[0].UserId] = new HumanoidCharacterProfile()
                    .WithForceOnForcePreferences(ForceOnForceSide.Opfor, ForceOnForceFallback.StayInLobby)
                    .WithGamemodeJobPriority("ForceOnForce", Opfor, JobPriority.High);
                var strict = jobs.AssignJobs(profiles, [station]);
                Assert.That(strict, Has.Count.EqualTo(3));
                Assert.That(strict.Values.Count(v => v.Item1 == Govfor), Is.EqualTo(2));
                Assert.That(strict.Values.Count(v => v.Item1 == Opfor), Is.EqualTo(1));
                jobs.AssignOverflowJobs(ref strict, profiles.Keys, profiles, [station]);
                Assert.That(strict.Values.Count(v => v.Item1 == null), Is.EqualTo(3));
            });
        }
        finally
        {
            await Server.WaitPost(() => SetCurrentPreset(ticker, originalPreset));
        }
    }

    [Test]
    public async Task JointRollRemapsGovforRoleOnlyWithOtherSideFallback()
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
                .WithForceOnForcePreferences(ForceOnForceSide.Govfor, ForceOnForceFallback.OtherSide)
                .WithGamemodeJobPriority("ForceOnForce", GovforSaw, JobPriority.Low)
                .WithPreferenceUnavailable(PreferenceUnavailableMode.SpawnAsOverflow));

        try
        {
            await Server.WaitAssertion(() =>
            {
                SetCurrentPreset(ticker, forceOnForce);
                var assigned = jobs.AssignJobs(govforOnlyProfiles, [station]);
                Assert.That(assigned.Values.Select(v => v.Item1),
                    Is.EquivalentTo(new ProtoId<JobPrototype>?[] { GovforSaw, OpforSaw }),
                    "the explicit other-side fallback preserves the requested role while balancing sides");
            });
        }
        finally
        {
            await Server.WaitPost(() => SetCurrentPreset(ticker, originalCurrentPreset));
        }
    }

    // CMU14 OverflowDealtGovforRemapsQueuedOpforRoles Begin
    [Test]
    public async Task JointRollRemapsOpforRoleOnlyWithOtherSideFallback()
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
                .WithForceOnForcePreferences(ForceOnForceSide.Opfor, ForceOnForceFallback.OtherSide)
                .WithGamemodeJobPriority("ForceOnForce", OpforSaw, JobPriority.Low)
                .WithPreferenceUnavailable(PreferenceUnavailableMode.SpawnAsOverflow));

        try
        {
            await Server.WaitAssertion(() =>
            {
                SetCurrentPreset(ticker, forceOnForce);
                var assigned = jobs.AssignJobs(opforOnlyProfiles, [station]);
                Assert.That(assigned.Values.Select(v => v.Item1),
                    Is.EquivalentTo(new ProtoId<JobPrototype>?[] { GovforSaw, OpforSaw }),
                    "the explicit other-side fallback preserves the requested role while balancing sides");
            });
        }
        finally
        {
            await Server.WaitPost(() => SetCurrentPreset(ticker, originalCurrentPreset));
        }
    }
    // CMU14 End

    [Test]
    public async Task StrictOpforPreferenceIsNotCapturedByGovforOnlyStations()
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
                .WithForceOnForcePreferences(ForceOnForceSide.Opfor, ForceOnForceFallback.StayInLobby)
                .WithGamemodeJobPriority("ForceOnForce", Opfor, JobPriority.Low)
                .WithPreferenceUnavailable(PreferenceUnavailableMode.SpawnAsOverflow));

        try
        {
            await Server.WaitAssertion(() =>
            {
                SetCurrentPreset(ticker, forceOnForce);
                var assigned = jobs.AssignJobs(profiles, [station]);
                Assert.That(assigned, Is.Empty, "a strict OPFOR preference cannot use GOVFOR-only station slots");
                jobs.AssignOverflowJobs(ref assigned, profiles.Keys, profiles, [station]);
                Assert.That(assigned.Values, Is.All.EqualTo(((ProtoId<JobPrototype>?) null, EntityUid.Invalid)),
                    "legacy overflow must not bypass the player's side restriction");
            });
        }
        finally
        {
            await Server.WaitPost(() => SetCurrentPreset(ticker, originalCurrentPreset));
        }
    }

    [Test]
    // CMU14: FoF uses its own role tab and explicit fallback preferences.
    public async Task AssignJobsForceOnForceIgnoresInsurgencyPreferences()
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
        // Conflicting Insurgency preferences must not leak into the independent FoF roll.
        var profiles = dummies.ToDictionary(
            session => session.UserId,
            _ => new HumanoidCharacterProfile()
                .WithForceOnForcePreferences(ForceOnForceSide.Govfor, ForceOnForceFallback.OtherSide)
                .WithGamemodeJobPriority("Insurgency", GovforSaw, JobPriority.High)
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
                    "the FoF nurse preference and explicit other-side fallback take priority over the Insurgency role");
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
    public async Task EveryQueueableSideJobHasAnOpposingSideMirror()
    {
        await Server.WaitAssertion(() =>
        {
            var missing = new List<string>();
            foreach (var job in SProtoMan.EnumeratePrototypes<JobPrototype>())
            {
                if (job.Abstract || !job.SetPreference || job.Hidden)
                    continue;

                foreach (var (source, mirror) in new[] { ("GOVFOR", "OPFOR"), ("OPFOR", "GOVFOR") })
                {
                    if (!job.ID.Contains(source))
                        continue;

                    var twin = job.ID.Replace(source, mirror);
                    if (!SProtoMan.HasIndex<JobPrototype>(twin))
                        missing.Add($"{job.ID} -> {twin}");
                }
            }

            Assert.That(missing, Is.Empty,
                "ForceOnForce side mirroring swaps GOVFOR/OPFOR in queued job ids, so every "
                + "queueable side job needs its mirror: " + string.Join(", ", missing));
        });
    }

    [Test]
    public async Task GenericLateJoinAcceptsNullJobAndPrefersJobMarker()
    {
        var map = await Pair.CreateTestMap();
        var spawning = Server.System<StationSpawningSystem>();
        var ticker = Server.System<GameTicker>();
        var originalRunLevel = ticker.RunLevel;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var transform = Server.System<SharedTransformSystem>();
                var anyJobCoordinates = map.GridCoords.Offset(new Vector2(5, 0));
                var lateJoinCoordinates = map.GridCoords.Offset(new Vector2(10, 0));
                var observerCoordinates = map.GridCoords.Offset(new Vector2(15, 0));
                var anyJob = SEntMan.SpawnEntity("StationJobsMergeAnyJobSpawn", anyJobCoordinates);
                SEntMan.SpawnEntity("StationJobsMergeLateJoinSpawn", lateJoinCoordinates);
                SEntMan.SpawnEntity("StationJobsMergeObserverSpawn", observerCoordinates);

                SetRunLevel(ticker, GameRunLevel.InRound);

                var preferred = spawning.SpawnPlayerCharacterOnStation(null, null, null);
                Assert.That(preferred, Is.Not.Null);
                Assert.That(transform.GetMapCoordinates(preferred!.Value),
                    Is.EqualTo(transform.ToMapCoordinates(anyJobCoordinates)),
                    "a null-job Job marker must be accepted and preferred over LateJoin");
                SEntMan.DeleteEntity(preferred.Value);
                SEntMan.DeleteEntity(anyJob);

                var fallback = spawning.SpawnPlayerCharacterOnStation(null, null, null);
                Assert.That(fallback, Is.Not.Null);
                Assert.That(transform.GetMapCoordinates(fallback!.Value),
                    Is.EqualTo(transform.ToMapCoordinates(lateJoinCoordinates)),
                    "an Observer marker must remain excluded from the generic late-join fallback");
                SEntMan.DeleteEntity(fallback.Value);
            });
        }
        finally
        {
            await Server.WaitPost(() => SetRunLevel(ticker, originalRunLevel));
        }
    }

    [Test]
    public async Task AdjustRoundStartJobSlotAddsTheRawFirstEntryAcrossArrayShapes()
    {
        var jobs = Server.System<StationJobsSystem>();
        var stations = Server.System<StationSystem>();
        var map = SProtoMan.Index<GameMapPrototype>(MapId);
        EntityUid station = default;

        await Server.WaitPost(() =>
        {
            station = stations.InitializeNewStation(map.Stations["Merge"], null, "Merge", map);
        });

        try
        {
            await Server.WaitAssertion(() =>
            {
                var component = SEntMan.GetComponent<StationJobsComponent>(station);

                component.SetupAvailableJobs.Remove(ThirdPartyLeader);
                jobs.AdjustRoundStartJobSlot(station, ThirdPartyLeader, 2, component);
                Assert.That(component.SetupAvailableJobs[ThirdPartyLeader], Is.EqualTo(new[] { 2, -1 }),
                    "an absent slot array must delegate the raw adjustment as the new round-start count");

                component.SetupAvailableJobs[Govfor] = new[] { 3, 8 };
                jobs.AdjustRoundStartJobSlot(station, Govfor, 2, component);
                Assert.That(component.SetupAvailableJobs[Govfor], Is.EqualTo(new[] { 5, 8 }),
                    "a finite first entry must be added without disturbing the mid-round entry");

                component.SetupAvailableJobs[Opfor] = new[] { -1, -1 };
                jobs.AdjustRoundStartJobSlot(station, Opfor, 1, component);
                Assert.That(component.SetupAvailableJobs[Opfor], Is.EqualTo(new[] { 0, -1 }),
                    "the helper must add the raw negative sentinel instead of normalizing it as unlimited");

                component.SetupAvailableJobs[ThreatLeader] = new[] { 4 };
                jobs.AdjustRoundStartJobSlot(station, ThreatLeader, 2, component);
                Assert.That(component.SetupAvailableJobs[ThreatLeader], Is.EqualTo(new[] { 6, -1 }),
                    "a length-one array must contribute slot zero before the setter restores canonical shape");
            });
        }
        finally
        {
            await Server.WaitPost(() => SEntMan.DeleteEntity(station));
        }
    }

    [Test]
    public async Task AegisJobSlotQueryDistinguishesNoStationAbsentUnlimitedAndFinite()
    {
        var aegis = Server.System<AegisLobbyEventSystem>();
        var jobs = Server.System<StationJobsSystem>();
        var stations = Server.System<StationSystem>();
        var map = SProtoMan.Index<GameMapPrototype>(MapId);
        EntityUid station = default;

        await Server.WaitAssertion(() =>
        {
            Assert.That(aegis.GetAegisJobSlots(), Is.Null,
                "no GOVFOR station must remain distinct from a station with an absent job");
        });

        await Server.WaitPost(() =>
        {
            station = stations.InitializeNewStation(map.Stations["Merge"], null, "Merge", map);
            var faction = SEntMan.EnsureComponent<ShipFactionComponent>(station);
            faction.Faction = "GOVFOR";
        });

        try
        {
            await Server.WaitAssertion(() =>
            {
                var component = SEntMan.GetComponent<StationJobsComponent>(station);
                component.JobList.Remove(AegisResearcher);
                Assert.That(aegis.GetAegisJobSlots(), Is.Zero,
                    "an absent AEGIS job must report zero rather than no station or unlimited slots");

                jobs.MakeJobUnlimited(station, AegisResearcher.Id, component);
                Assert.That(aegis.GetAegisJobSlots(), Is.Null,
                    "an explicitly unlimited AEGIS job must preserve its null slot count");

                Assert.That(jobs.TrySetJobSlot(station, AegisResearcher.Id, 4, true, component), Is.True);
                Assert.That(aegis.GetAegisJobSlots(), Is.EqualTo(4),
                    "a finite AEGIS job must report its exact available slot count");
            });
        }
        finally
        {
            await Server.WaitPost(() => SEntMan.DeleteEntity(station));
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
