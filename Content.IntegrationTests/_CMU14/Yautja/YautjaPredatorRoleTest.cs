using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;
using Content.Client.Popups;
using Content.Client.CharacterInfo;
using Content.Client.StatusIcon;
using Content.Client.ContextMenu.UI;
using Content.Client.UserInterface.Systems.Chat;
using Content.Client.CMU14.Yautja;
using Content.Client.Clickable;
using Content.Client.Interactable.Components;
using Content.Client.UserInterface.Systems.Actions.Controls;
using Content.Client.Verbs.UI;
using Content.Server.CMU14.Yautja;
using Content.Server.Administration.Logs;
using Content.Server.Database;
using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Server.Mind;
using Content.Server.Radio;
using Content.Server.Radio.Components;
using Content.Server.Radio.EntitySystems;
using Content.Server.Spawners.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Access.Components;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Humanoid;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Stealth;
using Content.Shared._RMC14.Xenonids.Construction.Nest;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Database;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Chat.Prototypes;
using Content.Shared.CharacterInfo;
using Content.Shared.Clothing.Components;
using Content.Shared.DoAfter;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Preferences;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Content.Shared.Roles;
using Content.Shared.Speech;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Verbs;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Containers;
using Robust.Shared.Localization;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.UnitTesting;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaPredatorRoleTest
{

    private static readonly Robust.Shared.Prototypes.ProtoId<GameMapPrototype> CMUYautjaHunterShipPrototype = "CMUYautjaHunterShip";

    private static readonly Robust.Shared.Prototypes.ProtoId<RadioChannelPrototype> CMUYautjaPrototype = "CMUYautja";
    private static readonly Robust.Shared.Prototypes.ProtoId<RadioChannelPrototype> CMUYautjaStrandedPrototype = "CMUYautjaStranded";
    private static readonly Robust.Shared.Prototypes.ProtoId<RadioChannelPrototype> CMUYautjaBadBloodPrototype2 = "CMUYautjaBadBlood";

    [Test]
    public async Task PredatorRoundStartLoadsHunterShipZLevels()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });
        var server = pair.Server;
        var userId = pair.Player!.UserId.UserId;

        try
        {
            var session = pair.Player!;
            var db = server.ResolveDependency<IServerDbManager>();
            // Ancient comes from the council whitelist now, a stored Ancient rank caps at Elder
            await db.SetYautjaWhitelistFlagsAsync(session.UserId.UserId,
                (int) (YautjaWhitelistFlags.Yautja | YautjaWhitelistFlags.Council));
            await server.ResolveDependency<YautjaRankManager>().Refresh(session.UserId);
            var clientNet = pair.Client.ResolveDependency<IClientNetManager>();
            await pair.Client.WaitPost(() => clientNet.ClientDisconnect("Predator round spawn entitlement test"));
            await pair.RunTicksSync(2);

            await server.WaitPost(() =>
            {
                var cfg = server.CfgMan;
                cfg.SetCVar(YautjaPredatorRoundCVars.RandomEnabled, false);
                server.EntMan.System<GameTicker>().RestartRound();
                // the test preset never rolls a hunt, so start it the way staff would
                Assert.That(server.EntMan.System<YautjaPredatorRoundSystem>().TryInitializePredatorRound(out _), Is.True);
            });

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var prototypes = server.ResolveDependency<IPrototypeManager>();
                var ticker = entMan.System<GameTicker>();
                Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));

                var ruleQuery = entMan.EntityQueryEnumerator<YautjaPredatorRoundComponent>();
                Assert.That(ruleQuery.MoveNext(out _, out var rule), Is.True);
                Assert.That(rule.HunterShipLoaded, Is.True);

                var networks = new List<(EntityUid Uid, CMUZLevelsNetworkComponent Component)>();
                var networkQuery = entMan.EntityQueryEnumerator<CMUZLevelsNetworkComponent>();
                while (networkQuery.MoveNext(out var networkUid, out var network))
                {
                    if (network.ZLevels.Keys.ToHashSet().SetEquals(new[] { -1, 0, 1 }))
                        networks.Add((networkUid, network));
                }

                Assert.That(networks, Has.Count.EqualTo(1));
                var loadedNetwork = networks[0];
                foreach (var (depth, mapUid) in loadedNetwork.Component.ZLevels)
                {
                    Assert.That(mapUid, Is.Not.Null);
                    Assert.That(entMan.TryGetComponent(mapUid!.Value, out CMUZLevelMapComponent? mapLevel), Is.True);
                    Assert.That(mapLevel!.NetworkUid, Is.EqualTo(loadedNetwork.Uid));
                    Assert.That(mapLevel.Depth, Is.EqualTo(depth));
                    Assert.That(entMan.TryGetComponent(mapUid.Value, out MapComponent map), Is.True);
                    Assert.That(map!.MapId, Is.Not.EqualTo(MapId.Nullspace));
                }

                var stationSystem = entMan.System<StationSystem>();
                var predatorStations = new HashSet<EntityUid>();
                var spawnQuery = entMan.EntityQueryEnumerator<YautjaHuntSpawnPointComponent, TransformComponent>();
                var huntSpawnPointCount = 0;
                while (spawnQuery.MoveNext(out var spawnUid, out var spawn, out var transform))
                {
                    huntSpawnPointCount++;

                    if (stationSystem.GetOwningStation(spawnUid, transform) is { } station)
                        predatorStations.Add(station);
                }

                if (predatorStations.Count == 0)
                {
                    var hunterMap = prototypes.Index<GameMapPrototype>(CMUYautjaHunterShipPrototype);
                    var stationQuery = entMan.EntityQueryEnumerator<StationDataComponent, MetaDataComponent>();
                    while (stationQuery.MoveNext(out var station, out _, out var metadata))
                    {
                        if (metadata.EntityName == hunterMap.MapName)
                            predatorStations.Add(station);
                    }
                }

                Assert.That(huntSpawnPointCount, Is.GreaterThan(0), "Hunter ship map must contain hunt spawn points.");
                var jobSpawnPointCount = 0;
                var jobSpawnQuery = entMan.EntityQueryEnumerator<SpawnPointComponent>();
                while (jobSpawnQuery.MoveNext(out _, out var spawn))
                {
                    if (spawn.SpawnType == SpawnPointType.Job && spawn.Job?.Id == "CMUYautjaHunter")
                        jobSpawnPointCount++;
                }

                Assert.That(jobSpawnPointCount, Is.GreaterThan(0), "Hunter ship map must contain hunter job spawn points.");

                var hunterShuttles = new List<EntityUid>();
                var shuttleQuery = entMan.EntityQueryEnumerator<ShuttleComponent, TransformComponent, MetaDataComponent>();
                while (shuttleQuery.MoveNext(out var shuttle, out _, out var shuttleTransform, out var shuttleMetadata))
                {
                    if (shuttleMetadata.EntityName == "Hunter Shuttle" &&
                        shuttleTransform.MapUid is { } shuttleMap &&
                        loadedNetwork.Component.ZLevels.Values.Contains(shuttleMap))
                    {
                        hunterShuttles.Add(shuttle);
                    }
                }

                Assert.That(hunterShuttles, Has.Count.EqualTo(1),
                    "Hunt initialization must spawn exactly one Hunter Shuttle on the Hunter Ship.");

                Assert.That(predatorStations, Has.Count.EqualTo(1),
                    "Hunter slots must be attached to the hunter ship station, not duplicated on every station.");

                var hunterSlotStations = new List<(EntityUid Station, int? Slots)>();
                var jobsQuery = entMan.EntityQueryEnumerator<StationJobsComponent>();
                while (jobsQuery.MoveNext(out var station, out var stationJobs) )
                {
                    if (!entMan.System<StationJobsSystem>().TryGetJobSlot(station, "CMUYautjaHunter", out var slots, stationJobs))
                        continue;

                    hunterSlotStations.Add((station, slots));
                }

                Assert.That(hunterSlotStations, Has.Count.EqualTo(1));
                Assert.That(hunterSlotStations[0].Station, Is.EqualTo(predatorStations.Single()));
                Assert.That(hunterSlotStations[0].Slots, Is.EqualTo(rule.Slots));

                // A hunter join must consume a slot. The spawn hook is raised
                // before StationSpawningSystem assigns the job, so it must not
                // reset the remaining count back to the round maximum.
                var hunterStation = predatorStations.Single();
                var hunterJobs = entMan.GetComponent<StationJobsComponent>(hunterStation);
                var stationJobsSystem = entMan.System<StationJobsSystem>();
                Assert.That(stationJobsSystem.TryAdjustJobSlot(
                    hunterStation,
                    "CMUYautjaHunter",
                    -1,
                    false,
                    false,
                    hunterJobs), Is.True);
                var remainingHunterSlots = stationJobsSystem.GetJobs(hunterStation, hunterJobs)["CMUYautjaHunter"];
                var selectedYautjaProfile = YautjaCharacterProfile.Default
                    .WithName("Late Join Kainde Amedha")
                    .WithSkinColor(YautjaSkinColor.Red)
                    .WithEyeColor(YautjaEyeColor.Gold)
                    .WithArmor(YautjaGearMaterial.Bronze, 3)
                    .WithStatus(YautjaProfileStatus.Normal)
                    .WithUnique(YautjaUniqueSet.Plated)
                    .WithLegacy(YautjaLegacySet.None)
                    .WithCapeStyle(YautjaCapeStyle.Ceremonial)
                    .WithBracer(YautjaBracerMaterial.Bone);
                var selectedLobbyProfile = HumanoidCharacterProfile.DefaultWithSpecies("Human")
                    .WithName("Human Lobby Profile")
                    .WithYautjaProfile(selectedYautjaProfile);
                // SpawnPlayerCharacterOnStation raises the same PlayerSpawningEvent
                // used by GameTicker.MakeJoinGame for a late-joining player.
                var spawned = entMan.System<StationSpawningSystem>().SpawnPlayerCharacterOnStation(
                    hunterStation,
                    "CMUYautjaHunter",
                    selectedLobbyProfile,
                    player: session);
                try
                {
                    Assert.That(spawned, Is.Not.Null);
                    var spawnedUid = spawned!.Value;
                    var spawnedTransform = entMan.GetComponent<TransformComponent>(spawnedUid);
                    Assert.That(spawnedTransform.MapUid, Is.Not.Null,
                        "A predator late-join must be placed on a real hunter-ship z-level map.");
                    Assert.That(loadedNetwork.Component.ZLevels.Values,
                        Does.Contain(spawnedTransform.MapUid!.Value),
                        "A predator late-join must use a spawn marker on one of the hunter ship z-levels.");

                    var spawnedYautja = entMan.GetComponent<YautjaAppliedProfileComponent>(spawnedUid);
                    var spawnedHumanoid = entMan.GetComponent<HumanoidProfileComponent>(spawnedUid);
                    var spawnedMeta = entMan.GetComponent<MetaDataComponent>(spawnedUid);
                    Assert.Multiple(() =>
                    {
                        Assert.That(spawnedMeta.EntityName, Is.EqualTo(selectedYautjaProfile.Name));
                        Assert.That(spawnedYautja.Profile.Name, Is.EqualTo(selectedYautjaProfile.Name));
                        Assert.That(spawnedHumanoid.Species, Is.EqualTo("Yautja"));
                        Assert.That(YautjaTestAppearance.SkinColor(entMan, spawnedUid),
                            Is.EqualTo(YautjaCharacterProfile.GetSkinColorColor(YautjaSkinColor.Red)));
                        Assert.That(YautjaTestAppearance.EyeColor(entMan, spawnedUid),
                            Is.EqualTo(YautjaCharacterProfile.GetEyeColorColor(YautjaEyeColor.Gold)));
                        Assert.That(spawnedYautja.Profile.Status, Is.EqualTo(YautjaProfileStatus.Normal));
                        Assert.That(spawnedYautja.Profile.ClanRank, Is.EqualTo(YautjaRank.Blooded));
                        Assert.That(spawnedYautja.Profile.Unique, Is.EqualTo(YautjaUniqueSet.Plated));
                        Assert.That(spawnedYautja.Profile.CapeStyle, Is.EqualTo(YautjaCapeStyle.Ceremonial));
                        Assert.That(spawnedYautja.Profile.BracerMaterial, Is.EqualTo(YautjaBracerMaterial.Bone));
                    });
                    Assert.That(stationJobsSystem.GetJobs(hunterStation, hunterJobs)["CMUYautjaHunter"], Is.EqualTo(remainingHunterSlots));
                }
                finally
                {
                    if (spawned is { } spawnedUid && !entMan.Deleted(spawnedUid))
                        entMan.DeleteEntity(spawnedUid);
                }
            });
        }
        finally
        {
            var cfg = server.CfgMan;
            server.CfgMan.SetCVar(YautjaPredatorRoundCVars.RandomEnabled, YautjaPredatorRoundCVars.RandomEnabled.DefaultValue);
            await server.ResolveDependency<IServerDbManager>()
                .SetYautjaWhitelistFlagsAsync(userId, (int) YautjaWhitelistFlags.None);
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayBeaconShowsAndUsesMultipleGroundDestinationsById()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid beacon = default;
        EntityUid first = default;
        EntityUid second = default;
        MapCoordinates secondCoordinates = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var transform = entMan.System<SharedTransformSystem>();
                var ui = entMan.System<SharedUserInterfaceSystem>();
                entMan.EnsureComponent<RMCPlanetComponent>(map.Grid.Owner);

                hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                beacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);
                first = entMan.SpawnEntity("CMUYautjaGroundRelayDestination", map.GridCoords.Offset(new Vector2(12, 0)));
                second = entMan.SpawnEntity("CMUYautjaGroundRelayDestination", map.GridCoords.Offset(new Vector2(16, 0)));

                entMan.GetComponent<YautjaRelayDestinationComponent>(first).Id = "test-ground-first";
                entMan.GetComponent<YautjaRelayDestinationComponent>(first).DisplayName = "First Ground";
                entMan.GetComponent<YautjaRelayDestinationComponent>(second).Id = "test-ground-second";
                entMan.GetComponent<YautjaRelayDestinationComponent>(second).DisplayName = "Second Ground";
                secondCoordinates = transform.GetMapCoordinates(second);

                Assert.That(hands.TryPickupAnyHand(hunter, beacon), Is.True);
                var use = new UseInHandEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(beacon, use);
                Assert.That(use.Handled, Is.True);
                Assert.That(ui.TryGetUiState<YautjaRelayBeaconState>(beacon, YautjaRelayBeaconUIKey.Key, out var state), Is.True);

                var ground = state!.Destinations
                    .Where(destination => destination.Kind.ToString() == "Ground")
                    .ToArray();
                Assert.That(ground, Has.Length.EqualTo(2));
                Assert.That(ground.Select(destination => destination.DestinationId), Is.EquivalentTo(new[]
                {
                    "test-ground-first",
                    "test-ground-second",
                }));

                var selected = ground.Single(destination => destination.DestinationId == "test-ground-second");
                ui.RaiseUiMessage(beacon, YautjaRelayBeaconUIKey.Key,
                    new YautjaRelayBeaconDestinationMsg(selected.Kind, selected.CustomIndex, selected.DestinationId)
                    {
                        Actor = hunter,
                    });
            });

            await pair.RunTicksSync(pair.SecondsToTicks(10.5f));

            await server.WaitAssertion(() =>
            {
                var actual = server.EntMan.System<SharedTransformSystem>().GetMapCoordinates(hunter);
                Assert.That(actual.MapId, Is.EqualTo(secondCoordinates.MapId));
                Assert.That(actual.Position, Is.EqualTo(secondCoordinates.Position));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { hunter, beacon, first, second })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InvalidGroundRelayDestinationsAreOmittedAndCannotStartRelay()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid beacon = default;
        var destinations = new List<EntityUid>();

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var ui = entMan.System<SharedUserInterfaceSystem>();

                hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                beacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);

                var valid = entMan.SpawnEntity("CMUYautjaGroundRelayDestination", map.GridCoords.Offset(new Vector2(12, 0)));
                var blank = entMan.SpawnEntity("CMUYautjaGroundRelayDestination", map.GridCoords.Offset(new Vector2(14, 0)));
                var duplicate = entMan.SpawnEntity("CMUYautjaGroundRelayDestination", map.GridCoords.Offset(new Vector2(16, 0)));
                var nullspace = entMan.SpawnEntity("CMUYautjaGroundRelayDestination", MapCoordinates.Nullspace);
                destinations.AddRange(new[] { valid, blank, duplicate, nullspace });

                entMan.GetComponent<YautjaRelayDestinationComponent>(valid).Id = "valid-ground";
                entMan.GetComponent<YautjaRelayDestinationComponent>(valid).DisplayName = "Valid Ground";
                entMan.GetComponent<YautjaRelayDestinationComponent>(blank).Id = " ";
                entMan.GetComponent<YautjaRelayDestinationComponent>(blank).DisplayName = "Blank ID";
                entMan.GetComponent<YautjaRelayDestinationComponent>(duplicate).Id = "valid-ground";
                entMan.GetComponent<YautjaRelayDestinationComponent>(duplicate).DisplayName = "Duplicate Ground";
                entMan.GetComponent<YautjaRelayDestinationComponent>(nullspace).Id = "nullspace-ground";
                entMan.GetComponent<YautjaRelayDestinationComponent>(nullspace).DisplayName = "Nullspace Ground";

                Assert.That(hands.TryPickupAnyHand(hunter, beacon), Is.True);
                var use = new UseInHandEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(beacon, use);
                Assert.That(use.Handled, Is.True);
                Assert.That(ui.TryGetUiState<YautjaRelayBeaconState>(beacon, YautjaRelayBeaconUIKey.Key, out var state), Is.True);

                var ground = state!.Destinations
                    .Where(destination => destination.Kind == YautjaRelayDestinationKind.Ground)
                    .ToArray();
                Assert.That(ground.Select(destination => destination.DestinationId), Is.EqualTo(new[] { "valid-ground" }));

                foreach (var invalidId in new[] { " ", "nullspace-ground" })
                {
                    ui.RaiseUiMessage(beacon, YautjaRelayBeaconUIKey.Key,
                        new YautjaRelayBeaconDestinationMsg(YautjaRelayDestinationKind.Ground, -1, invalidId)
                        {
                            Actor = hunter,
                        });
                }

                Assert.That(ActiveRelayDoAfters(entMan, hunter), Is.EqualTo(0));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in destinations.Append(hunter).Append(beacon))
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SimpleRelayBeaconTeleportsDirectlyToYautjaShipLikeCmss13ThrallTeleporter()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid bloodedThrall = default;
        EntityUid simpleBeacon = default;
        EntityUid yautjaShipDestination = default;
        EntityUid humanShipDestination = default;
        MapCoordinates yautjaShipCoordinates = default;
        MapCoordinates humanShipCoordinates = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var transform = entMan.System<SharedTransformSystem>();
                var ui = entMan.System<SharedUserInterfaceSystem>();

                bloodedThrall = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(bloodedThrall);
                simpleBeacon = entMan.SpawnEntity("CMUYautjaSimpleRelayBeacon", map.GridCoords);
                yautjaShipDestination = entMan.SpawnEntity("CMUHunterShipMarkerPredatorSpawn", map.GridCoords.Offset(new Vector2(14, 0)));
                humanShipDestination = entMan.SpawnEntity("CMUYautjaHumanShipRelayDestination", map.GridCoords.Offset(new Vector2(20, 0)));
                yautjaShipCoordinates = transform.GetMapCoordinates(yautjaShipDestination);
                humanShipCoordinates = transform.GetMapCoordinates(humanShipDestination);

                Assert.That(hands.TryPickupAnyHand(bloodedThrall, simpleBeacon), Is.True);

                var use = new UseInHandEvent(bloodedThrall);
                entMan.EventBus.RaiseLocalEvent(simpleBeacon, use);

                Assert.Multiple(() =>
                {
                    Assert.That(use.Handled, Is.True);
                    Assert.That(ui.IsUiOpen(simpleBeacon, YautjaRelayBeaconUIKey.Key, bloodedThrall), Is.False,
                        "CMSS13 /obj/item/device/thrall_teleporter/attack_self() does not open the yautja_teleporter ship-selection UI.");
                    Assert.That(ActiveRelayDoAfters(entMan, bloodedThrall), Is.EqualTo(1));
                });

                var doAfter = entMan.GetComponent<DoAfterComponent>(bloodedThrall);
                var active = doAfter.DoAfters.Values.Single(active =>
                    !active.Cancelled &&
                    !active.Completed &&
                    active.Args.Event is YautjaRelayBeaconDoAfterEvent);
                var relayEvent = (YautjaRelayBeaconDoAfterEvent) active.Args.Event;

                Assert.Multiple(() =>
                {
                    Assert.That(active.Args.Delay, Is.EqualTo(TimeSpan.FromSeconds(10)));
                    Assert.That(relayEvent.Destination, Is.EqualTo(YautjaRelayDestinationKind.YautjaShip));
                    Assert.That(relayEvent.CustomIndex, Is.EqualTo(-1));
                });
            });

            await pair.RunTicksSync(pair.SecondsToTicks(10.5f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var transform = entMan.System<SharedTransformSystem>();
                var actual = transform.GetMapCoordinates(bloodedThrall);

                Assert.Multiple(() =>
                {
                    Assert.That(actual.MapId, Is.EqualTo(yautjaShipCoordinates.MapId));
                    Assert.That(actual.Position, Is.EqualTo(yautjaShipCoordinates.Position));
                    Assert.That(actual.Position, Is.Not.EqualTo(humanShipCoordinates.Position),
                        "The simple thrall relay should resolve only the Yautja ship spawnpoint, never the Human Ship relay destination.");
                    Assert.That(ActiveRelayDoAfters(entMan, bloodedThrall), Is.EqualTo(0));
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { bloodedThrall, simpleBeacon, yautjaShipDestination, humanShipDestination })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SimpleRelayBeaconSuccessfulDoAfterKeepsPulledPassengerCloakedAndTeleportsTrain()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);
        server.ResolveDependency<ILogManager>().GetSawmill("tts").Level = LogLevel.Fatal;
        var gridCoords = await CreateServerTestGrid(server);

        EntityUid bloodedThrall = default;
        EntityUid passenger = default;
        EntityUid simpleBeacon = default;
        EntityUid passengerBracer = default;
        EntityUid yautjaShipDestination = default;
        MapCoordinates yautjaShipCoordinates = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var inventory = entMan.System<InventorySystem>();
                var pulling = entMan.System<PullingSystem>();
                var transform = entMan.System<SharedTransformSystem>();

                bloodedThrall = entMan.SpawnEntity("CMMobHuman", gridCoords);
                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(bloodedThrall);
                passenger = entMan.SpawnEntity("CMMobHuman", gridCoords.Offset(new Vector2(1, 0)));
                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(passenger);
                simpleBeacon = entMan.SpawnEntity("CMUYautjaSimpleRelayBeacon", gridCoords);
                passengerBracer = entMan.SpawnEntity("CMUYautjaBracer", gridCoords);
                yautjaShipDestination = entMan.SpawnEntity("CMUHunterShipMarkerPredatorSpawn", gridCoords.Offset(new Vector2(14, 0)));
                yautjaShipCoordinates = transform.GetMapCoordinates(yautjaShipDestination);

                Assert.That(inventory.TryEquip(passenger, passengerBracer, "gloves", silent: true, force: true), Is.True);
                MakeActivelyCloaked(entMan, passenger);
                Assert.That(pulling.TryStartPull(bloodedThrall, passenger), Is.True);
                Assert.That(hands.TryPickupAnyHand(bloodedThrall, simpleBeacon), Is.True);

                var use = new UseInHandEvent(bloodedThrall);
                entMan.EventBus.RaiseLocalEvent(simpleBeacon, use);

                Assert.That(use.Handled, Is.True);
                Assert.That(ActiveRelayDoAfters(entMan, bloodedThrall), Is.EqualTo(1));
            });

            await server.WaitRunTicks((int) Math.Ceiling(10.5f / server.Timing.TickPeriod.TotalSeconds));
            await server.WaitRunTicks(1);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var transform = entMan.System<SharedTransformSystem>();
                var userCoordinates = transform.GetMapCoordinates(bloodedThrall);
                var passengerCoordinates = transform.GetMapCoordinates(passenger);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<EntityActiveInvisibleComponent>(passenger), Is.True,
                        "A relay teleport must preserve a pulled passenger's active invisibility.");
                    Assert.That(userCoordinates.MapId, Is.EqualTo(yautjaShipCoordinates.MapId));
                    Assert.That(userCoordinates.Position, Is.EqualTo(yautjaShipCoordinates.Position));
                    Assert.That(passengerCoordinates.MapId, Is.EqualTo(yautjaShipCoordinates.MapId));
                    Assert.That(passengerCoordinates.Position, Is.EqualTo(yautjaShipCoordinates.Position));
                    Assert.That(entMan.GetComponent<PullerComponent>(bloodedThrall).Pulling, Is.EqualTo(passenger));
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { bloodedThrall, passenger, simpleBeacon, passengerBracer, yautjaShipDestination })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        server.Dispose();
    }

    [Test]
    public async Task RelayBeaconTeleLocStoresCustomDestination()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            ClearRelayTeleLocDestinations(entMan);
            entMan.EnsureComponent<RMCPlanetComponent>(map.Grid.Owner);

            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var beacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);
            var action = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            try
            {
                var actionComp = entMan.EnsureComponent<ActionComponent>(action);
                var ev = new YautjaAddTeleporterLocationActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                };

                entMan.EventBus.RaiseLocalEvent(beacon, ev);

                Assert.That(ev.Handled, Is.True);

                var saved = new YautjaRelayBeaconNameDestinationEvent(
                    entMan.GetNetEntity(hunter),
                    entMan.GetNetCoordinates(map.GridCoords),
                    "Trophy Hall");

                entMan.EventBus.RaiseLocalEvent(beacon, saved);

                var relay = entMan.GetComponent<YautjaRelayBeaconComponent>(beacon);
                Assert.That(relay.CustomDestinations.Count, Is.EqualTo(1));
                Assert.That(relay.CustomDestinations[0].Name, Is.EqualTo("Trophy Hall"));
                Assert.That(relay.CustomDestinations[0].Coordinates, Is.EqualTo(map.GridCoords));
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(beacon))
                    entMan.DeleteEntity(beacon);
                if (!entMan.Deleted(action))
                    entMan.DeleteEntity(action);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayBeaconTeleLocRejectsDeadUserLikeCmss13StatCheck()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mobState = entMan.System<MobStateSystem>();
            ClearRelayTeleLocDestinations(entMan);
            entMan.EnsureComponent<RMCPlanetComponent>(map.Grid.Owner);

            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var beacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);

            try
            {
                mobState.ChangeMobState(hunter, MobState.Dead);

                var saved = new YautjaRelayBeaconNameDestinationEvent(
                    entMan.GetNetEntity(hunter),
                    entMan.GetNetCoordinates(map.GridCoords),
                    "Trophy Hall");

                entMan.EventBus.RaiseLocalEvent(beacon, saved);

                var relay = entMan.GetComponent<YautjaRelayBeaconComponent>(beacon);
                Assert.That(relay.CustomDestinations, Is.Empty);
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(beacon))
                    entMan.DeleteEntity(beacon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayBeaconTeleLocBroadcastSkipsBadBloodLikeCmss13DefaultHuntingNetwork()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid badBlood = default;
        EntityUid beacon = default;
        EntityUid? previousAttached = null;
        var expectedBroadcast = string.Empty;
        const string destinationName = "Hidden Shrine";
        const string hunterName = "Sek Met";

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var areas = entMan.System<AreaSystem>();
                var metadata = entMan.System<MetaDataSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                ClearRelayTeleLocDestinations(entMan);
                entMan.EnsureComponent<RMCPlanetComponent>(map.Grid.Owner);

                hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                badBlood = entMan.SpawnEntity("CMUMobYautjaBadBlood", map.GridCoords.Offset(new Vector2(1, 0)));
                beacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);
                metadata.SetEntityName(hunter, hunterName);
                server.PlayerMan.SetAttachedEntity(session, badBlood);

                expectedBroadcast = Loc.GetString(
                    "cmu-yautja-relay-add-destination-broadcast",
                    ("hunter", hunterName),
                    ("name", destinationName),
                    ("location", map.GridCoords.ToString()),
                    ("area", areas.GetAreaName(hunter)));
            });

            await pair.ReallyBeIdle(10);

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var saved = new YautjaRelayBeaconNameDestinationEvent(
                    entMan.GetNetEntity(hunter),
                    entMan.GetNetCoordinates(map.GridCoords),
                    destinationName);

                entMan.EventBus.RaiseLocalEvent(beacon, saved);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();
                var joinedLabels = string.Join("\n", labels);

                Assert.That(
                    labels,
                    Does.Not.Contain(expectedBroadcast),
                    $"CMSS13 message_all_yautja() defaults to YAUTJA_NET_HUNTING; pred_can_receive_message() should not deliver that network to FACTION_YAUTJA_BADBLOOD recipients without a hunting-network bracer.\nActual labels:\n{joinedLabels}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { hunter, badBlood, beacon })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }

                ClearRelayTeleLocDestinations(entMan);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CommunicatorRejectsNonYautjaAndNonThrallSpeakersLikeCmss13TalkInto()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var radio = entMan.System<RadioSystem>();
            var recorder = entMan.System<YautjaTestRadioReceiveRecorderSystem>();
            var prototypes = server.ResolveDependency<IPrototypeManager>();

            var speaker = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var communicator = entMan.SpawnEntity("CMUYautjaCommunicator", map.GridCoords);
            var ordinaryRadio = entMan.SpawnEntity("CMUYautjaCommunicator", map.GridCoords.Offset(new Vector2(1, 0)));
            var regularHellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(2, 0)));

            try
            {
                entMan.EnsureComponent<ActiveRadioComponent>(ordinaryRadio).Channels.Add("CMUYautja");
                recorder.Watch(ordinaryRadio);
                recorder.Watch(regularHellhound);

                recorder.Clear();
                radio.SendRadioMessage(
                    speaker,
                    "Stolen words.",
                    prototypes.Index<RadioChannelPrototype>(CMUYautjaPrototype),
                    communicator);

                Assert.Multiple(() =>
                {
                    Assert.That(recorder.DeliveredMessageOnChannel("Stolen words.", "CMUYautja"), Is.False,
                        "CMSS13 yautja headset talk_into() returns before parent radio delivery for non-Yautja/non-thrall speakers.");
                    Assert.That(recorder.DeliveredTo(regularHellhound, "Stolen words."), Is.False,
                        "CMSS13 yautja headset talk_into() does not forward rejected speakers to hellhounds.");
                });
            }
            finally
            {
                DeleteEntities(entMan, speaker, communicator, ordinaryRadio, regularHellhound);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CommunicatorYautjaChannelForwardsOnlyToRegularHellhoundsLikeCmss13TalkInto()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var radio = entMan.System<RadioSystem>();
            var recorder = entMan.System<YautjaTestRadioReceiveRecorderSystem>();
            var prototypes = server.ResolveDependency<IPrototypeManager>();

            var speaker = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var communicator = entMan.SpawnEntity("CMUYautjaCommunicator", map.GridCoords);
            var regularHellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(1, 0)));
            var badBloodHellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(2, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(speaker);
                SetNpcFaction(entMan, badBloodHellhound, "CMUYautjaBadBlood");
                recorder.Watch(regularHellhound);
                recorder.Watch(badBloodHellhound);

                recorder.Clear();
                radio.SendRadioMessage(
                    speaker,
                    "Honor binds the pack.",
                    prototypes.Index<RadioChannelPrototype>(CMUYautjaPrototype),
                    communicator);

                Assert.Multiple(() =>
                {
                    Assert.That(recorder.DeliveredTo(regularHellhound, "Honor binds the pack."), Is.True,
                        "CMSS13 yautja headset talk_into() forwards RADIO_CHANNEL_YAUTJA to living hellhounds in FACTION_YAUTJA.");
                    Assert.That(recorder.DeliveredTo(badBloodHellhound, "Honor binds the pack."), Is.False,
                        "CMSS13 yautja headset talk_into() skips Bad Blood hellhounds when check_channel is RADIO_CHANNEL_YAUTJA.");
                });
            }
            finally
            {
                DeleteEntities(entMan, speaker, communicator, regularHellhound, badBloodHellhound);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StrandedCommunicatorForwardsOnlyToStrandedHellhoundsLikeCmss13TalkInto()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var radio = entMan.System<RadioSystem>();
            var recorder = entMan.System<YautjaTestRadioReceiveRecorderSystem>();
            var prototypes = server.ResolveDependency<IPrototypeManager>();

            var speaker = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var communicator = entMan.SpawnEntity("CMUYautjaStrandedCommunicator", map.GridCoords);
            var regularHellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(1, 0)));
            var badBloodHellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(2, 0)));
            var strandedHellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(3, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(speaker);
                SetNpcFaction(entMan, badBloodHellhound, "CMUYautjaBadBlood");
                SetNpcFaction(entMan, strandedHellhound, "CMUYautjaStranded");
                recorder.Watch(regularHellhound);
                recorder.Watch(badBloodHellhound);
                recorder.Watch(strandedHellhound);

                recorder.Clear();
                radio.SendRadioMessage(
                    speaker,
                    "No clan, only survival.",
                    prototypes.Index<RadioChannelPrototype>(CMUYautjaStrandedPrototype),
                    communicator);

                Assert.Multiple(() =>
                {
                    Assert.That(recorder.DeliveredTo(strandedHellhound, "No clan, only survival."), Is.True,
                        "CMSS13 yautja headset talk_into() forwards RADIO_CHANNEL_YAUTJA_STRANDED to living hellhounds in FACTION_YAUTJA_STRANDED.");
                    Assert.That(recorder.DeliveredTo(regularHellhound, "No clan, only survival."), Is.False,
                        "CMSS13 yautja headset talk_into() skips regular Yautja hellhounds when check_channel is RADIO_CHANNEL_YAUTJA_STRANDED.");
                    Assert.That(recorder.DeliveredTo(badBloodHellhound, "No clan, only survival."), Is.False,
                        "CMSS13 yautja headset talk_into() skips Bad Blood hellhounds when check_channel is RADIO_CHANNEL_YAUTJA_STRANDED.");
                });
            }
            finally
            {
                DeleteEntities(entMan, speaker, communicator, regularHellhound, badBloodHellhound, strandedHellhound);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BadBloodCommunicatorHivebrokenIntrinsicReceiverGetsSingleRadioReceiveEvent()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid speaker = default;
        EntityUid communicator = default;
        EntityUid hivebrokenXeno = default;
        EntityUid? previousAttached = null;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var radio = entMan.System<RadioSystem>();
            var recorder = entMan.System<YautjaTestRadioReceiveRecorderSystem>();
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var session = server.PlayerMan.Sessions.Single();
            previousAttached = session.AttachedEntity;

            speaker = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            communicator = entMan.SpawnEntity("CMUYautjaBadBloodCommunicator", map.GridCoords);
            hivebrokenXeno = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(1, 0)));

            entMan.EnsureComponent<YautjaComponent>(speaker);
            SetNpcFaction(entMan, speaker, "CMUYautjaBadBlood");
            SetNpcFaction(entMan, hivebrokenXeno, "CMUYautjaBadBlood");
            entMan.EnsureComponent<YautjaHivebrokenXenoComponent>(hivebrokenXeno);
            entMan.EnsureComponent<IntrinsicRadioReceiverComponent>(hivebrokenXeno);
            entMan.RemoveComponent<YautjaHellhoundComponent>(hivebrokenXeno);
            server.PlayerMan.SetAttachedEntity(session, hivebrokenXeno);

            recorder.Watch(hivebrokenXeno);
            recorder.Clear();

            radio.SendRadioMessage(
                speaker,
                "Only once.",
                prototypes.Index<RadioChannelPrototype>(CMUYautjaBadBloodPrototype2),
                communicator);

            Assert.That(recorder.DeliveryCount(hivebrokenXeno, "Only once."), Is.EqualTo(1),
                "Bad Blood hivebroken xenos with intrinsic radio receive should get the communicator forwarding event once.");
        });

        await pair.RunTicksSync(5);

        await client.WaitAssertion(() =>
        {
            var history = client.ResolveDependency<IUserInterfaceManager>()
                .GetUIController<ChatUIController>()
                .History
                .Select(entry => entry.Msg)
                .ToList();

            Assert.That(history.Count(message => message.Message == "Only once."), Is.EqualTo(1),
                "A hivebroken xeno with IntrinsicRadioReceiver should receive one client chat message, not one intrinsic delivery plus one direct network send.");
        });

        await server.WaitPost(() =>
        {
            if (previousAttached is { } attached)
                server.PlayerMan.SetAttachedEntity(server.PlayerMan.Sessions.Single(), attached);

            DeleteEntities(server.EntMan, speaker, communicator, hivebrokenXeno);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BadBloodCommunicatorForwardsOnlyToBadBloodHellhoundAndHivebrokenXenoLikeCmss13TalkInto()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var radio = entMan.System<RadioSystem>();
            var recorder = entMan.System<YautjaTestRadioReceiveRecorderSystem>();
            var prototypes = server.ResolveDependency<IPrototypeManager>();

            var speaker = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var communicator = entMan.SpawnEntity("CMUYautjaBadBloodCommunicator", map.GridCoords);
            var regularHellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(1, 0)));
            var badBloodHellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(2, 0)));
            var hivebrokenXeno = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(3, 0)));
            var ordinaryXeno = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(4, 0)));
            var ordinaryYautja = entMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new Vector2(5, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(speaker);
                SetNpcFaction(entMan, speaker, "CMUYautjaBadBlood");
                SetNpcFaction(entMan, badBloodHellhound, "CMUYautjaBadBlood");

                SetNpcFaction(entMan, hivebrokenXeno, "CMUYautjaBadBlood");
                entMan.EnsureComponent<YautjaHivebrokenXenoComponent>(hivebrokenXeno);
                entMan.RemoveComponent<YautjaHellhoundComponent>(hivebrokenXeno);

                SetNpcFaction(entMan, ordinaryXeno, "RMCXeno");
                entMan.RemoveComponent<YautjaHellhoundComponent>(ordinaryXeno);

                recorder.Watch(regularHellhound);
                recorder.Watch(badBloodHellhound);
                recorder.Watch(hivebrokenXeno);
                recorder.Watch(ordinaryXeno);
                recorder.Watch(ordinaryYautja);

                recorder.Clear();
                radio.SendRadioMessage(
                    speaker,
                    "The clan is dead.",
                    prototypes.Index<RadioChannelPrototype>(CMUYautjaBadBloodPrototype2),
                    communicator);

                Assert.Multiple(() =>
                {
                    Assert.That(recorder.DeliveredTo(badBloodHellhound, "The clan is dead."), Is.True,
                        "CMSS13 yautja headset talk_into() forwards RADIO_CHANNEL_YAUTJA_BADBLOOD to living hellhounds in FACTION_YAUTJA_BADBLOOD.");
                    Assert.That(recorder.DeliveredTo(hivebrokenXeno, "The clan is dead."), Is.True,
                        "CMSS13 yautja headset talk_into() forwards RADIO_CHANNEL_YAUTJA_BADBLOOD to living xenos in XENO_HIVE_YAUTJA_BADBLOOD.");
                    Assert.That(recorder.DeliveredTo(regularHellhound, "The clan is dead."), Is.False,
                        "CMSS13 yautja headset talk_into() skips regular hellhounds for the Bad Blood channel.");
                    Assert.That(recorder.DeliveredTo(ordinaryXeno, "The clan is dead."), Is.False,
                        "CMSS13 yautja headset talk_into() only forwards Bad Blood channel traffic to the Bad Blood hive, not ordinary xenos.");
                    Assert.That(recorder.DeliveredTo(ordinaryYautja, "The clan is dead."), Is.False,
                        "The Bad Blood communicator encryption key should not leak RADIO_CHANNEL_YAUTJA_BADBLOOD to ordinary Yautja headset listeners.");
                });
            }
            finally
            {
                DeleteEntities(entMan, speaker, communicator, regularHellhound, badBloodHellhound, hivebrokenXeno, ordinaryXeno, ordinaryYautja);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayBeaconTeleLocRejectsNestedUserLikeCmss13NestCheck()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            ClearRelayTeleLocDestinations(entMan);
            entMan.EnsureComponent<RMCPlanetComponent>(map.Grid.Owner);

            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var beacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);
            var nest = entMan.SpawnEntity("XenoNest", map.GridCoords);

            try
            {
                entMan.EnsureComponent<XenoNestedComponent>(hunter);

                var saved = new YautjaRelayBeaconNameDestinationEvent(
                    entMan.GetNetEntity(hunter),
                    entMan.GetNetCoordinates(map.GridCoords),
                    "Trophy Hall");

                entMan.EventBus.RaiseLocalEvent(beacon, saved);

                var relay = entMan.GetComponent<YautjaRelayBeaconComponent>(beacon);
                Assert.That(relay.CustomDestinations, Is.Empty,
                    "CMSS13 add_tele_loc() returns false while the user is buckled to /obj/structure/bed/nest.");
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(beacon))
                    entMan.DeleteEntity(beacon);
                if (!entMan.Deleted(nest))
                    entMan.DeleteEntity(nest);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayBeaconTeleLocRejectsNonGroundLevelUserLikeCmss13IsGroundLevel()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var groundMap = await pair.CreateTestMap();
        var orbitMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            ClearRelayTeleLocDestinations(entMan);
            entMan.EnsureComponent<RMCPlanetComponent>(groundMap.Grid.Owner);

            var hunter = entMan.SpawnEntity("CMUMobYautja", orbitMap.GridCoords);
            var beacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", orbitMap.GridCoords);

            try
            {
                var saved = new YautjaRelayBeaconNameDestinationEvent(
                    entMan.GetNetEntity(hunter),
                    entMan.GetNetCoordinates(orbitMap.GridCoords),
                    "Orbital Trophy Hall");

                entMan.EventBus.RaiseLocalEvent(beacon, saved);

                var relay = entMan.GetComponent<YautjaRelayBeaconComponent>(beacon);
                Assert.That(relay.CustomDestinations, Is.Empty,
                    "CMSS13 add_tele_loc() returns false when !is_ground_level(usr.z). Local tele_loc should only save destinations from the RMC planet/ground map equivalent.");
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(beacon))
                    entMan.DeleteEntity(beacon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayBeaconTeleLocAllowsYoungbloodWithTechAuthorizationLikeCmss13Verb()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mobState = entMan.System<MobStateSystem>();
            ClearRelayTeleLocDestinations(entMan);
            entMan.EnsureComponent<RMCPlanetComponent>(map.Grid.Owner);

            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var beacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaYoungbloodComponent>(hunter);
                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(hunter);
                mobState.ChangeMobState(hunter, MobState.Alive);

                var saved = new YautjaRelayBeaconNameDestinationEvent(
                    entMan.GetNetEntity(hunter),
                    entMan.GetNetCoordinates(map.GridCoords),
                    "Trophy Hall");

                entMan.EventBus.RaiseLocalEvent(beacon, saved);

                var relay = entMan.GetComponent<YautjaRelayBeaconComponent>(beacon);
                Assert.That(relay.CustomDestinations, Has.Count.EqualTo(1));
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(beacon))
                    entMan.DeleteEntity(beacon);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayBeaconUseBlockedInteractionUsesCmss13AttackSelfDenialForBothBeaconTypes()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid relayBeacon = default;
        EntityUid simpleBeacon = default;
        EntityUid yautjaShipDestination = default;
        EntityUid humanShipDestination = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var mobState = entMan.System<MobStateSystem>();
                var ui = entMan.System<SharedUserInterfaceSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                relayBeacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);
                simpleBeacon = entMan.SpawnEntity("CMUYautjaSimpleRelayBeacon", map.GridCoords);
                yautjaShipDestination = entMan.SpawnEntity("CMUHunterShipMarkerPredatorSpawn", map.GridCoords.Offset(new Vector2(10, 0)));
                humanShipDestination = entMan.SpawnEntity("CMUYautjaHumanShipRelayDestination", map.GridCoords.Offset(new Vector2(12, 0)));
                mobState.ChangeMobState(hunter, MobState.Critical);
                server.PlayerMan.SetAttachedEntity(session, hunter);

                var relayUse = new UseInHandEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(relayBeacon, relayUse);

                var simpleUse = new UseInHandEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(simpleBeacon, simpleUse);

                Assert.Multiple(() =>
                {
                    Assert.That(relayUse.Handled, Is.True);
                    Assert.That(simpleUse.Handled, Is.True);
                    Assert.That(ui.IsUiOpen(relayBeacon, YautjaRelayBeaconUIKey.Key, hunter), Is.False,
                        "CMSS13 yautja_teleporter/attack_self() checks should_block_game_interaction(H) before opening the ship-selection prompt.");
                    Assert.That(ActiveRelayDoAfters(entMan, hunter), Is.EqualTo(0),
                        "CMSS13 yautja_teleporter and thrall_teleporter return before starting the 10 second do_after when should_block_game_interaction(H) is true.");
                });
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();
                var joined = string.Join("\n", labels);

                var denied = Loc.GetString("cmu-yautja-relay-beacon-denied");
                Assert.That(labels.Any(label => label.StartsWith(denied, StringComparison.Ordinal)),
                    Is.True,
                    $"CMSS13 relay blocked-interaction denial shares the non-tech attack_self() text.\nActual labels:\n{joined}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { hunter, relayBeacon, simpleBeacon, yautjaShipDestination, humanShipDestination })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayBeaconUseNonHumanReturnsSilentlyLikeCmss13AttackSelf()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid xeno = default;
        EntityUid beacon = default;
        EntityUid destination = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var ui = entMan.System<SharedUserInterfaceSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                xeno = entMan.SpawnEntity("CMXenoDrone", map.GridCoords);
                beacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);
                destination = entMan.SpawnEntity("CMUHunterShipMarkerPredatorSpawn", map.GridCoords.Offset(new Vector2(12, 0)));
                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(xeno);

                server.PlayerMan.SetAttachedEntity(session, xeno);
                var use = new UseInHandEvent(xeno);
                entMan.EventBus.RaiseLocalEvent(beacon, use);

                Assert.Multiple(() =>
                {
                    Assert.That(use.Handled, Is.True);
                    Assert.That(ui.IsUiOpen(beacon, YautjaRelayBeaconUIKey.Key, xeno), Is.False,
                        "CMSS13 attack_self() returns silently before opening the relay prompt when !ishuman(user).");
                    Assert.That(ActiveRelayDoAfters(entMan, xeno), Is.EqualTo(0),
                        "A non-human tech-authorized entity must not start the relay beacon do_after.");
                });
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();

                Assert.Multiple(() =>
                {
                    Assert.That(labels, Does.Not.Contain(Loc.GetString("cmu-yautja-relay-beacon-denied")),
                        "CMSS13 !ishuman(user) branch returns silently before the non-tech denial.");
                    Assert.That(labels, Does.Not.Contain(Loc.GetString("cmu-yautja-relay-beacon-pulse")),
                        "CMSS13 !ishuman(user) branch returns silently before destination lookup or signal feedback.");
                    Assert.That(labels, Does.Not.Contain(Loc.GetString("cmu-yautja-relay-beacon-start")),
                        "CMSS13 !ishuman(user) branch returns silently before the teleport start message.");
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in new[] { xeno, beacon, destination })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayBeaconDuplicateUseDoesNotCancelExistingDoAfterLikeCmss13Timer()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var ui = entMan.System<SharedUserInterfaceSystem>();

            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var beacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);
            var destination = entMan.SpawnEntity("CMUHunterShipMarkerPredatorSpawn", map.GridCoords.Offset(new Vector2(12, 0)));

            try
            {
                entMan.GetComponent<YautjaRelayBeaconComponent>(beacon).PulseSound =
                    new SoundPathSpecifier("/Audio/CMU14/Yautja/Equipment/pred_bracer.wav");
                Assert.That(hands.TryPickupAnyHand(hunter, beacon), Is.True);

                Assert.That(ui.TryOpenUi(beacon, YautjaRelayBeaconUIKey.Key, hunter), Is.True);
                ui.RaiseUiMessage(beacon, YautjaRelayBeaconUIKey.Key,
                    new YautjaRelayBeaconDestinationMsg(YautjaRelayDestinationKind.YautjaShip) { Actor = hunter });

                var doAfter = entMan.GetComponent<DoAfterComponent>(hunter);
                var activeBefore = doAfter.DoAfters.Values
                    .Single(active => !active.Cancelled && !active.Completed && active.Args.Event is YautjaRelayBeaconDoAfterEvent);

                Assert.That(ui.TryOpenUi(beacon, YautjaRelayBeaconUIKey.Key, hunter), Is.True);
                ui.RaiseUiMessage(beacon, YautjaRelayBeaconUIKey.Key,
                    new YautjaRelayBeaconDestinationMsg(YautjaRelayDestinationKind.YautjaShip) { Actor = hunter });

                var activeAfter = doAfter.DoAfters.Values
                    .Single(active => !active.Cancelled && !active.Completed && active.Args.Event is YautjaRelayBeaconDoAfterEvent);

                Assert.Multiple(() =>
                {
                    Assert.That(activeAfter.Index, Is.EqualTo(activeBefore.Index),
                        "Repeated relay starts should be blocked while busy without cancelling the existing CMSS13 do_after timer.");
                    Assert.That(ActiveRelayDoAfters(entMan, hunter), Is.EqualTo(1));
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, beacon, destination })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    private static int ActiveRelayDoAfters(IEntityManager entMan, EntityUid user)
    {
        return entMan.TryGetComponent(user, out DoAfterComponent? doAfter)
            ? doAfter.DoAfters.Values.Count(active =>
                !active.Cancelled &&
                !active.Completed &&
                active.Args.Event is YautjaRelayBeaconDoAfterEvent)
            : 0;
    }

    private static void ClearRelayTeleLocDestinations(IEntityManager entMan)
    {
        entMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
    }

    [Test]
    public async Task RelayBeaconDestinationSelectionCompletesDoAfterAndTeleports()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid beacon = default;
        EntityUid destination = default;
        MapCoordinates destinationCoordinates = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var transform = entMan.System<SharedTransformSystem>();
                var ui = entMan.System<SharedUserInterfaceSystem>();

                hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                beacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);
                destination = entMan.SpawnEntity("CMUHunterShipMarkerPredatorSpawn", map.GridCoords.Offset(new Vector2(12, 0)));
                destinationCoordinates = transform.GetMapCoordinates(destination);

                Assert.That(hands.TryPickupAnyHand(hunter, beacon), Is.True);

                var relay = entMan.GetComponent<YautjaRelayBeaconComponent>(beacon);
                Assert.That(relay.DoAfter, Is.EqualTo(TimeSpan.FromSeconds(10)));

                var use = new UseInHandEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(beacon, use);
                Assert.That(use.Handled, Is.True);
                Assert.That(ui.IsUiOpen(beacon, YautjaRelayBeaconUIKey.Key, hunter), Is.True);

                ui.RaiseUiMessage(beacon, YautjaRelayBeaconUIKey.Key,
                    new YautjaRelayBeaconDestinationMsg(YautjaRelayDestinationKind.YautjaShip) { Actor = hunter });

                var doAfter = entMan.GetComponent<DoAfterComponent>(hunter);
                var active = doAfter.DoAfters.Values.Single(active =>
                    !active.Cancelled &&
                    !active.Completed &&
                    active.Args.Event is YautjaRelayBeaconDoAfterEvent);
                var relayEvent = (YautjaRelayBeaconDoAfterEvent) active.Args.Event;

                Assert.Multiple(() =>
                {
                    Assert.That(active.Args.Delay, Is.EqualTo(TimeSpan.FromSeconds(10)));
                    Assert.That(active.Args.Target, Is.EqualTo(hunter));
                    Assert.That(active.Args.Used, Is.EqualTo(beacon));
                    Assert.That(relayEvent.Destination, Is.EqualTo(YautjaRelayDestinationKind.YautjaShip));
                    Assert.That(relayEvent.CustomIndex, Is.EqualTo(-1));
                    Assert.That(transform.GetMapCoordinates(hunter).Position, Is.Not.EqualTo(destinationCoordinates.Position));
                });
            });

            await pair.RunTicksSync(pair.SecondsToTicks(10.5f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var transform = entMan.System<SharedTransformSystem>();
                var actual = transform.GetMapCoordinates(hunter);

                Assert.Multiple(() =>
                {
                    Assert.That(actual.MapId, Is.EqualTo(destinationCoordinates.MapId));
                    Assert.That(actual.Position, Is.EqualTo(destinationCoordinates.Position));
                    Assert.That(entMan.TryGetComponent(hunter, out DoAfterComponent? doAfter) &&
                                doAfter.DoAfters.Values.Any(active =>
                                    !active.Cancelled &&
                                    !active.Completed &&
                                    active.Args.Event is YautjaRelayBeaconDoAfterEvent),
                        Is.False);
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { hunter, beacon, destination })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayBeaconTeleLocSavedDestinationIsSharedAcrossBeaconsLikeCmss13Globals()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid savingBeacon = default;
        EntityUid travelBeacon = default;
        EntityUid savedLocation = default;
        MapCoordinates savedCoordinates = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var transform = entMan.System<SharedTransformSystem>();
                var ui = entMan.System<SharedUserInterfaceSystem>();
                ClearRelayTeleLocDestinations(entMan);
                entMan.EnsureComponent<RMCPlanetComponent>(map.Grid.Owner);

                hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                savingBeacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);
                travelBeacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);
                savedLocation = entMan.SpawnEntity("CMUHunterShipMarkerPredatorSpawn", map.GridCoords.Offset(new Vector2(16, 0)));
                savedCoordinates = transform.GetMapCoordinates(savedLocation);
                Assert.That(hands.TryPickupAnyHand(hunter, travelBeacon), Is.True);

                var saved = new YautjaRelayBeaconNameDestinationEvent(
                    entMan.GetNetEntity(hunter),
                    entMan.GetNetCoordinates(transform.GetMoverCoordinates(savedLocation)),
                    "Trophy Hall");

                entMan.EventBus.RaiseLocalEvent(savingBeacon, saved);

                var use = new UseInHandEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(travelBeacon, use);
                Assert.That(use.Handled, Is.True);
                Assert.That(ui.IsUiOpen(travelBeacon, YautjaRelayBeaconUIKey.Key, hunter), Is.True);
                Assert.That(ui.TryGetUiState<YautjaRelayBeaconState>(travelBeacon, YautjaRelayBeaconUIKey.Key, out var state), Is.True);

                var custom = state!.Destinations.Single(destination => destination.Name == "Trophy Hall");
                Assert.Multiple(() =>
                {
                    Assert.That(custom.Available, Is.True);
                    Assert.That(custom.CustomIndex, Is.GreaterThanOrEqualTo(0));
                });

                ui.RaiseUiMessage(travelBeacon, YautjaRelayBeaconUIKey.Key,
                    new YautjaRelayBeaconDestinationMsg(custom.Kind, custom.CustomIndex) { Actor = hunter });
            });

            await pair.RunTicksSync(pair.SecondsToTicks(10.5f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var transform = entMan.System<SharedTransformSystem>();
                var actual = transform.GetMapCoordinates(hunter);

                Assert.Multiple(() =>
                {
                    Assert.That(actual.MapId, Is.EqualTo(savedCoordinates.MapId));
                    Assert.That(actual.Position, Is.EqualTo(savedCoordinates.Position));
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { hunter, savingBeacon, travelBeacon, savedLocation })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayBeaconTeleLocSavedDestinationsClearOnRoundRestartLikeCmss13Globals()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var ui = entMan.System<SharedUserInterfaceSystem>();
            ClearRelayTeleLocDestinations(entMan);
            entMan.EnsureComponent<RMCPlanetComponent>(map.Grid.Owner);

            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var savingBeacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);
            var travelBeacon = entMan.SpawnEntity("CMUYautjaRelayBeacon", map.GridCoords);

            try
            {
                var saved = new YautjaRelayBeaconNameDestinationEvent(
                    entMan.GetNetEntity(hunter),
                    entMan.GetNetCoordinates(map.GridCoords.Offset(new Vector2(4, 0))),
                    "Trophy Hall");

                entMan.EventBus.RaiseLocalEvent(savingBeacon, saved);

                Assert.That(hands.TryPickupAnyHand(hunter, travelBeacon), Is.True);
                var use = new UseInHandEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(travelBeacon, use);
                Assert.That(use.Handled, Is.True);
                Assert.That(ui.TryGetUiState<YautjaRelayBeaconState>(travelBeacon, YautjaRelayBeaconUIKey.Key, out var beforeRestart), Is.True);
                Assert.That(beforeRestart!.Destinations.Any(destination => destination.Name == "Trophy Hall"), Is.True);

                ClearRelayTeleLocDestinations(entMan);

                var useAfterRestart = new UseInHandEvent(hunter);
                entMan.EventBus.RaiseLocalEvent(travelBeacon, useAfterRestart);
                Assert.That(useAfterRestart.Handled, Is.True);
                Assert.That(ui.TryGetUiState<YautjaRelayBeaconState>(travelBeacon, YautjaRelayBeaconUIKey.Key, out var afterRestart), Is.True);
                Assert.That(
                    afterRestart!.Destinations.Any(destination => destination.Name == "Trophy Hall"),
                    Is.False,
                    "CMSS13 tele_loc destinations live in GLOB.yautja_teleports/GLOB.yautja_teleport_descs and should not survive round restart cleanup.");
            }
            finally
            {
                foreach (var uid in new[] { hunter, savingBeacon, travelBeacon })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MarkForHuntActionTogglesPreyMarkThroughWornBracer()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<Content.Shared.Inventory.InventorySystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new System.Numerics.Vector2(1, 0)));
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            var action = entMan.SpawnEntity("CMUActionYautjaMarkForHunt", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                entMan.EventBus.RaiseLocalEvent(hunter, new YautjaMarkForHuntActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                    Target = target,
                });

                Assert.That(entMan.TryGetComponent<YautjaMarkComponent>(target, out var mark), Is.True);
                Assert.That(mark!.Marks[YautjaMarkKind.Prey], Is.EqualTo(hunter));

                entMan.EventBus.RaiseLocalEvent(hunter, new YautjaMarkForHuntActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                    Target = target,
                });

                var marks = entMan.System<YautjaMarkSystem>();
                Assert.That(marks.IsMarkedBy(target, YautjaMarkKind.Prey, hunter), Is.False);
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(target))
                    entMan.DeleteEntity(target);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
                if (!entMan.Deleted(action))
                    entMan.DeleteEntity(action);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StudentAndBloodedMarksApplyStatusIconCarriers()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<Content.Shared.Inventory.InventorySystem>();
            var marks = entMan.System<YautjaMarkSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var student = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new System.Numerics.Vector2(1, 0)));
            var thrall = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new System.Numerics.Vector2(2, 0)));
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaComponent>(student);
                entMan.EnsureComponent<YautjaYoungbloodComponent>(student);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                Assert.That(marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), hunter, student, YautjaMarkKind.Student, null), Is.True);
                Assert.That(marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), hunter, thrall, YautjaMarkKind.Thrall, null), Is.True);
                Assert.That(marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), hunter, thrall, YautjaMarkKind.Blooded, null), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<YautjaMarkComponent>(student).Marks.Keys, Does.Contain(YautjaMarkKind.Student));
                    Assert.That(entMan.HasComponent<StatusIconComponent>(student), Is.True);
                    Assert.That(entMan.GetComponent<YautjaMarkComponent>(thrall).Marks.Keys, Does.Contain(YautjaMarkKind.Blooded));
                    Assert.That(entMan.HasComponent<StatusIconComponent>(thrall), Is.True);
                    Assert.That(entMan.GetComponent<YautjaThrallComponent>(thrall).Blooded, Is.True);
                });
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(student))
                    entMan.DeleteEntity(student);
                if (!entMan.Deleted(thrall))
                    entMan.DeleteEntity(thrall);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static SpriteSpecifier.Rsi StatusIcon(string state)
    {
        return new SpriteSpecifier.Rsi(new ResPath("/Textures/CMU14/Yautja/hud_yautja.rsi"), state);
    }

    private static void MakeActivelyCloaked(IEntityManager entMan, EntityUid user)
    {
        var invisible = entMan.EnsureComponent<EntityActiveInvisibleComponent>(user);
        invisible.Opacity = 0.2f;
        var turnInvisible = entMan.EnsureComponent<EntityTurnInvisibleComponent>(user);
        turnInvisible.Enabled = true;
    }

    private static async Task<EntityCoordinates> CreateServerTestGrid(RobustIntegrationTest.ServerIntegrationInstance server)
    {
        EntityCoordinates gridCoords = default;

        await server.WaitPost(() =>
        {
            var mapSystem = server.System<SharedMapSystem>();
            mapSystem.CreateMap(out var mapId);
            var grid = mapSystem.CreateGridEntity(mapId);
            gridCoords = new EntityCoordinates(grid, 0, 0);

            var tileDefinitionManager = server.ResolveDependency<ITileDefinitionManager>();
            var plating = tileDefinitionManager["Plating"];
            mapSystem.SetTile(grid.Owner, grid.Comp, gridCoords, new Tile(plating.TileId));
        });

        return gridCoords;
    }

    private static void SetNpcFaction(IEntityManager entMan, EntityUid uid, ProtoId<NpcFactionPrototype> faction)
    {
        var factions = entMan.EnsureComponent<NpcFactionMemberComponent>(uid);
        factions.Factions.Clear();
        factions.Factions.Add(faction);
    }

    private static void DeleteEntities(IEntityManager entMan, params EntityUid[] uids)
    {
        foreach (var uid in uids)
        {
            if (uid != default && !entMan.Deleted(uid))
                entMan.DeleteEntity(uid);
        }
    }

}

public sealed partial class YautjaTestRadioReceiveRecorderSystem : EntitySystem
{
    public readonly List<(EntityUid Receiver, string Message, string Channel)> Deliveries = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaTestRadioReceiveRecorderComponent, RadioReceiveEvent>(OnRadioReceive);
    }

    public void Clear()
    {
        Deliveries.Clear();
    }

    public bool DeliveredTo(EntityUid receiver, string message)
    {
        return Deliveries.Any(delivery => delivery.Receiver == receiver && delivery.Message == message);
    }

    public int DeliveryCount(EntityUid receiver, string message)
    {
        return Deliveries.Count(delivery => delivery.Receiver == receiver && delivery.Message == message);
    }

    public bool DeliveredMessageOnChannel(string message, string channel)
    {
        return Deliveries.Any(delivery => delivery.Message == message && delivery.Channel == channel);
    }

    public void Watch(EntityUid uid)
    {
        EnsureComp<YautjaTestRadioReceiveRecorderComponent>(uid);
    }

    private void OnRadioReceive(Entity<YautjaTestRadioReceiveRecorderComponent> ent, ref RadioReceiveEvent ev)
    {
        var receiver = ent.Owner;
        if (HasComp<HeadsetComponent>(receiver))
        {
            var parent = Transform(receiver).ParentUid;
            if (parent.IsValid())
                receiver = parent;
        }

        Deliveries.Add((receiver, ev.Message, ev.Channel.ID));
    }
}

[RegisterComponent]
public sealed partial class YautjaTestRadioReceiveRecorderComponent : Component;
