using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Pair;
using Content.Server.AU14.Round;
using Content.Server._CMU14.ZLevels.Core;
using Content.Server._CMU14.ZLevels.PVS;
using Content.Server.GameTicking;
using Content.Shared._CMU14.RoundSetup.LegacyBush;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Content.Shared._CMU14.ZLevels.Vehicles;
using Content.Shared.AU14;
using Content.Shared.AU14.util;
using Content.Shared.Light.Components;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Shared.EntitySerialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Utility;
using Serilog.Events;
using ServerMapSystem = Robust.Server.GameObjects.MapSystem;
using ServerRoofSystem = Content.Server.Light.EntitySystems.RoofSystem;

namespace Content.IntegrationTests._CMU14.ZLevels;

[TestFixture]
public sealed class USSBushMultiZTest : GameTest
{
    private const string MapPrototype = "USSBushRedux";
    private static readonly ProtoId<PlatoonPrototype> UsmcPlatoon = "USCM";
    private static readonly EntProtoId PlatoonSpawnRule = "PlatoonSpawn";

    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
    };

    [Test]
    public async Task LoadsNetworkAndResolvesActiveShipMarkers()
    {
        var server = Pair.Server;
        var mapSystem = SEntMan.System<SharedMapSystem>();
        var ticker = SEntMan.System<GameTicker>();
        var zLevels = SEntMan.System<CMUZLevelsSystem>();

        EntityUid networkUid = default;
        NetEntity networkNet = default;
        Dictionary<int, EntityUid> loadedMaps = [];
        Dictionary<int, NetEntity> loadedMapNets = [];
        var uscmEquipmentVendorsBefore = 0;
        var uscmWeaponsVendorsBefore = 0;

        await server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<AuRoundSystem>().SetPlanet("AUPlanetLV747"), Is.True);
            SEntMan.System<PlatoonSpawnRuleSystem>().SelectedGovforPlatoon =
                SProtoMan.Index(UsmcPlatoon);

            var options = DeserializationOptions.Default with { InitializeMaps = true };
            var grids = ticker.LoadGameMap(SProtoMan.Index<GameMapPrototype>(MapPrototype), out var mapId, options);
            foreach (var grid in grids)
            {
                var faction = SEntMan.EnsureComponent<ShipFactionComponent>(grid);
                faction.Faction = "govfor";
            }

            uscmEquipmentVendorsBefore = CountPrototype("AU14USCMequipmentvendor");
            uscmWeaponsVendorsBefore = CountPrototype("AU14USCMWeaponsVendor");

            var mainMap = mapSystem.GetMap(mapId);
            Assert.That(zLevels.TryGetZNetwork(mainMap, out var matchingNetwork), Is.True);
            Assert.That(matchingNetwork, Is.Not.Null);
            networkUid = matchingNetwork.Value.Owner;
            networkNet = SEntMan.GetNetEntity(networkUid);

            Assert.That(zLevels.TryGetDepthBounds(matchingNetwork.Value, out var minDepth, out var maxDepth), Is.True);
            Assert.That(minDepth, Is.EqualTo(-1));
            Assert.That(maxDepth, Is.EqualTo(3));

            foreach (var depth in new[] { -1, 0, 1, 2, 3 })
            {
                Assert.That(zLevels.TryGetMapAtDepth(matchingNetwork.Value, depth, out var mapUid), Is.True,
                    $"Depth {depth} has no map.");
                Assert.That(SEntMan.TryGetComponent<CMUZLevelMapComponent>(mapUid, out var map));
                Assert.That(map.Depth, Is.EqualTo(depth));
                Assert.That(map.NetworkUid, Is.EqualTo(networkUid));
                loadedMaps.Add(depth, mapUid);
                loadedMapNets.Add(depth, SEntMan.GetNetEntity(mapUid));
            }
        });

        await server.WaitRunTicks(2);
        await Pair.RunTicksSync(5);
        await AssertClientTopology(networkNet, loadedMapNets);

        await server.WaitAssertion(() => ticker.StartGameRule(PlatoonSpawnRule));
        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var unresolvedMarkers = 0;
            var markerQuery = SEntMan.EntityQueryEnumerator<VendorMarkerComponent>();
            while (markerQuery.MoveNext(out _, out var marker))
            {
                Assert.That(marker.Replacement, Is.Null,
                    $"Resolved {marker.Class} marker was not consumed after map initialization.");
                unresolvedMarkers++;
            }

            Assert.That(unresolvedMarkers, Is.GreaterThan(0),
                "Dynamic Bush markers should remain available for the selected platoon rule.");
            Assert.That(CountPrototype("AU14USCMequipmentvendor"), Is.GreaterThan(uscmEquipmentVendorsBefore),
                "The selected USCM platoon did not resolve Bush's rifleman vendor markers.");
            Assert.That(CountPrototype("AU14USCMWeaponsVendor"), Is.GreaterThan(uscmWeaponsVendorsBefore),
                "The selected USCM platoon did not resolve Bush's weapons vendor markers.");
            AssertPrototypeCountAtLeast("RMCOverwatchConsoleGovforRotating", 1);
        });

        await server.WaitAssertion(() =>
        {
            var roofSystem = SEntMan.System<ServerRoofSystem>();
            var tileManager = server.ResolveDependency<ITileDefinitionManager>();
            var steel = new Tile(tileManager["FloorSteel"].TileId);
            var testTile = new Vector2i(10000, 10000);

            var depthTwo = loadedMaps[2];
            var depthThree = loadedMaps[3];
            var depthTwoGrid = SEntMan.GetComponent<MapGridComponent>(depthTwo);
            var depthThreeGrid = SEntMan.GetComponent<MapGridComponent>(depthThree);

            mapSystem.SetTile(depthTwo, depthTwoGrid, testTile, steel);
            AssertRoofs(roofSystem, loadedMaps, testTile, true, 1, 0, -1);

            mapSystem.SetTile(depthThree, depthThreeGrid, testTile, steel);
            AssertRoofs(roofSystem, loadedMaps, testTile, true, 2, 1, 0, -1);

            mapSystem.SetTile(depthThree, depthThreeGrid, testTile, Tile.Empty);
            AssertRoofs(roofSystem, loadedMaps, testTile, false, 2);
            AssertRoofs(roofSystem, loadedMaps, testTile, true, 1, 0, -1);

            mapSystem.SetTile(depthTwo, depthTwoGrid, testTile, Tile.Empty);
            AssertRoofs(roofSystem, loadedMaps, testTile, false, 1, 0, -1);
        });

        await server.WaitAssertion(() =>
        {
            var shooting = SEntMan.System<CMUZLevelShootingSystem>();
            var sourceMap = loadedMaps[0];
            var lowerMap = loadedMaps[-1];
            var sourceMapId = SEntMan.GetComponent<MapComponent>(sourceMap).MapId;
            var lowerMapId = SEntMan.GetComponent<MapComponent>(lowerMap).MapId;
            var source = new Vector2(10100.5f, 10100.5f);
            var target = source + new Vector2(3f, 0f);
            var shooter = SEntMan.SpawnEntity(null, new EntityCoordinates(sourceMap, source));
            Assert.That(shooting.SetShootDown(shooter, true), Is.True);

            var wall = SEntMan.SpawnEntity("WallSolid", new EntityCoordinates(sourceMap, source + Vector2.UnitX));
            Assert.That(
                shooting.TryAdjustShotMapCoordinates(
                    shooter,
                    new MapCoordinates(source, sourceMapId),
                    new MapCoordinates(target, sourceMapId),
                    out var blockedFrom,
                    out var blockedTo),
                Is.True);
            Assert.That(blockedFrom.MapId, Is.EqualTo(sourceMapId),
                "A source-deck wall was bypassed by cross-Z shot projection.");
            Assert.That(blockedTo.MapId, Is.EqualTo(sourceMapId));
            Assert.That(
                shooting.TryGetProjectileVisualOffset(
                    shooter,
                    new MapCoordinates(source, sourceMapId),
                    blockedFrom,
                    out _),
                Is.False,
                "A blocked same-level shot received a cross-Z sprite offset.");

            SEntMan.DeleteEntity(wall);

            var barricade = SEntMan.SpawnEntity(
                "CMBarricadeMetal",
                new EntityCoordinates(sourceMap, source + new Vector2(1f, 0.44f)));
            Assert.That(
                shooting.TryAdjustShotMapCoordinates(
                    shooter,
                    new MapCoordinates(source, sourceMapId),
                    new MapCoordinates(target, sourceMapId),
                    out blockedFrom,
                    out blockedTo,
                    (int) CollisionGroup.BarricadeImpassable),
                Is.True);
            Assert.That(blockedFrom.MapId, Is.EqualTo(sourceMapId),
                "A projectile-specific barricade blocker was bypassed by cross-Z shot projection.");
            Assert.That(blockedTo.MapId, Is.EqualTo(sourceMapId));

            SEntMan.DeleteEntity(barricade);

            Assert.That(
                shooting.TryAdjustShotMapCoordinates(
                    shooter,
                    new MapCoordinates(source, sourceMapId),
                    new MapCoordinates(target, sourceMapId),
                    out var projectedFrom,
                    out var projectedTo),
                Is.True);
            Assert.That(projectedFrom.MapId, Is.EqualTo(lowerMapId),
                "An unobstructed shot did not project to the lower Z-level.");
            Assert.That(projectedTo.MapId, Is.EqualTo(lowerMapId));

            SEntMan.DeleteEntity(shooter);

            var upwardSource = source + new Vector2(0f, 10f);
            var upwardTarget = upwardSource + new Vector2(3f, 0f);
            var upwardShooter = SEntMan.SpawnEntity(null, new EntityCoordinates(lowerMap, upwardSource));
            zLevels.EnsureZLevelViewer(upwardShooter);
            Assert.That(zLevels.SetLookUp(upwardShooter, true), Is.True);
            var lowerWall = SEntMan.SpawnEntity(
                "WallSolid",
                new EntityCoordinates(lowerMap, upwardSource + Vector2.UnitX));

            Assert.That(
                shooting.TryAdjustShotMapCoordinates(
                    upwardShooter,
                    new MapCoordinates(upwardSource, lowerMapId),
                    new MapCoordinates(upwardTarget, lowerMapId),
                    out blockedFrom,
                    out blockedTo),
                Is.True);
            Assert.That(blockedFrom.MapId, Is.EqualTo(lowerMapId),
                "A source-deck wall was bypassed by an upward cross-Z shot.");
            Assert.That(blockedTo.MapId, Is.EqualTo(lowerMapId));
            Assert.That(
                shooting.TryGetProjectileVisualOffset(
                    upwardShooter,
                    new MapCoordinates(upwardSource, lowerMapId),
                    blockedFrom,
                    out _),
                Is.False,
                "A blocked upward shot received a cross-Z sprite offset.");

            SEntMan.DeleteEntity(lowerWall);

            Assert.That(
                shooting.TryAdjustShotMapCoordinates(
                    upwardShooter,
                    new MapCoordinates(upwardSource, lowerMapId),
                    new MapCoordinates(upwardTarget, lowerMapId),
                    out projectedFrom,
                    out projectedTo),
                Is.True);
            Assert.That(projectedFrom.MapId, Is.EqualTo(sourceMapId),
                "An unobstructed upward shot did not project to the upper Z-level.");
            Assert.That(projectedTo.MapId, Is.EqualTo(sourceMapId));

            SEntMan.DeleteEntity(upwardShooter);
        });

        var clientNetManager = Client.ResolveDependency<IClientNetManager>();
        await Client.WaitPost(() => clientNetManager.ClientDisconnect("CMU topology reconnect validation."));
        await Pair.RunTicksSync(5);
        Client.SetConnectTarget(Server);
        await Client.WaitPost(() => clientNetManager.ClientConnect(null, 0, null));
        await Pair.RunTicksSync(20);
        await AssertClientTopology(networkNet, loadedMapNets);

        await server.WaitPost(() =>
        {
            ticker.ClearGameRules();
            foreach (var mapUid in loadedMaps.Values)
            {
                if (SEntMan.TryGetComponent<MapComponent>(mapUid, out var map))
                    mapSystem.DeleteMap(map.MapId);
            }
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityExists(networkUid), Is.False,
                "Empty Z-networks must be deleted with their final map.");
        });
    }

    [Test]
    public async Task ReplicatesMinimumFallingAndVehicleTraversalState()
    {
        var map = await Pair.CreateTestMap();
        EntityUid serverEntity = default;
        NetEntity entityNet = default;

        await Server.WaitAssertion(() =>
        {
            serverEntity = SEntMan.SpawnEntity(null, map.GridCoords);
            entityNet = SEntMan.GetNetEntity(serverEntity);

            var physics = SEntMan.EnsureComponent<CMUZPhysicsComponent>(serverEntity);
            SEntMan.EnsureComponent<CMUVehicleZTraversalComponent>(serverEntity);
            SEntMan.EnsureComponent<CMUZFallingComponent>(serverEntity);
            SEntMan.EnsureComponent<CMUPvsOverrideComponent>(serverEntity);
#pragma warning disable RA0002
            physics.Falling = true;
#pragma warning restore RA0002
            SEntMan.DirtyField(serverEntity, physics, nameof(CMUZPhysicsComponent.Falling));
        });
        await Pair.RunTicksSync(5);

        await Client.WaitAssertion(() =>
        {
            var clientEntity = CEntMan.GetEntity(entityNet);
            var physics = CEntMan.GetComponent<CMUZPhysicsComponent>(clientEntity);
            var traversal = CEntMan.GetComponent<CMUVehicleZTraversalComponent>(clientEntity);
            Assert.Multiple(() =>
            {
                Assert.That(physics.Falling, Is.True);
                Assert.That(physics.Bounciness, Is.EqualTo(0.3f));
                Assert.That(CEntMan.HasComponent<CMUZFallingComponent>(clientEntity), Is.False,
                    "The server active-set marker must not replicate.");
                Assert.That(traversal.SupportSampleSpacing,
                    Is.EqualTo(CMUVehicleSupportFootprint.DefaultSampleSpacing));
                Assert.That(traversal.MaxAirDriftSpeed, Is.EqualTo(4f));
            });
        });

        await Server.WaitAssertion(() =>
        {
            var physics = SEntMan.GetComponent<CMUZPhysicsComponent>(serverEntity);
#pragma warning disable RA0002
            physics.Falling = false;
#pragma warning restore RA0002
            SEntMan.DirtyField(serverEntity, physics, nameof(CMUZPhysicsComponent.Falling));
            SEntMan.RemoveComponent<CMUZFallingComponent>(serverEntity);
        });
        await Pair.RunTicksSync(5);

        await Client.WaitAssertion(() =>
        {
            var clientEntity = CEntMan.GetEntity(entityNet);
            Assert.That(CEntMan.GetComponent<CMUZPhysicsComponent>(clientEntity).Falling, Is.False);
        });

        await Server.WaitPost(() => SEntMan.DeleteEntity(serverEntity));
    }

    [Test]
    public async Task RollsBackOwnedLevelsWhenDeclaredLevelFailsToLoad()
    {
        var server = Pair.Server;
        var lifecycle = SEntMan.System<CMUZNetworkLifecycleSystem>();
        var mapSystem = SEntMan.System<ServerMapSystem>();
        var source = SProtoMan.Index<GameMapPrototype>(MapPrototype);
        var gameMap = source.Persistence(source.MapPath);
        gameMap.MapsBelow.Add(source.MapsBelow[0]);
        gameMap.MapsAbove.Add(new ResPath("/Maps/USSBushRedux/missing-transaction-level.yml"));
        Func<string, LogEvent, bool> allowExpectedMissingMap = (sawmill, log) =>
            sawmill == "system.map_loader" &&
            log.RenderMessage().Contains("missing-transaction-level.yml");

        MapId baseMapId = default;

        Pair.ServerLogHandler.JudgeLog += allowExpectedMissingMap;
        try
        {
            await server.WaitAssertion(() =>
            {
                var baseLevel = mapSystem.CreateMap(out baseMapId);
                var mapsBefore = CountComponents<MapComponent>();
                var networksBefore = CountComponents<CMUZLevelsNetworkComponent>();

                Assert.That(lifecycle.TryCreateRoundNetwork(gameMap, baseLevel, out var error), Is.False);
                Assert.That(error, Does.Contain("missing-transaction-level.yml"));
                Assert.That(CountComponents<MapComponent>(), Is.EqualTo(mapsBefore),
                    "An auxiliary level loaded before the failure was not rolled back.");
                Assert.That(CountComponents<CMUZLevelsNetworkComponent>(), Is.EqualTo(networksBefore),
                    "A failed load created an orphan Z-network.");
                Assert.That(SEntMan.HasComponent<CMUZLevelMapComponent>(baseLevel), Is.False,
                    "The caller-owned base level was modified before all auxiliary levels loaded.");
            });
        }
        finally
        {
            Pair.ServerLogHandler.JudgeLog -= allowExpectedMissingMap;
        }

        await server.WaitPost(() => mapSystem.DeleteMap(baseMapId));
    }

    [Test]
    public async Task PostCommitObserverFailureRetainsCommittedTopology()
    {
        var server = Pair.Server;
        var fault = SEntMan.System<CMUZLifecycleFaultInjectionSystem>();
        var lifecycle = SEntMan.System<CMUZNetworkLifecycleSystem>();
        var mapSystem = SEntMan.System<ServerMapSystem>();
        var source = SProtoMan.Index<GameMapPrototype>(MapPrototype);
        var gameMap = source.Persistence(source.MapPath);
        gameMap.MapsAbove.Add(new ResPath("/Maps/Test/empty.yml"));
        gameMap.ZLevelsComponentOverrides = new ComponentRegistry
        {
            ["CMUZLifecycleFaultInjection"] =
                new EntityPrototype.ComponentRegistryEntry(new CMUZLifecycleFaultInjectionComponent
                {
                    Value = 99,
                }),
        };

        MapId baseMapId = default;
        var createdMapIds = new List<MapId>();
        var expectedObserverFailureLogged = false;
        Func<string, LogEvent, bool> allowExpectedObserverFailure = (_, log) =>
        {
            var expected =
                log.RenderMessage().Contains(CMUZLifecycleFaultInjectionSystem.TopologyFailure) ||
                log.Exception?.Message.Contains(CMUZLifecycleFaultInjectionSystem.TopologyFailure) == true;
            expectedObserverFailureLogged |= expected;
            return expected;
        };

        Pair.ServerLogHandler.JudgeLog += allowExpectedObserverFailure;
        try
        {
            await server.WaitAssertion(() =>
            {
                var baseLevel = mapSystem.CreateMap(out baseMapId, runMapInit: false);
                var originalOverride = SEntMan.AddComponent<CMUZLifecycleFaultInjectionComponent>(baseLevel);
                originalOverride.Value = 41;
                var mapsBefore = CountComponents<MapComponent>();
                var networksBefore = CountComponents<CMUZLevelsNetworkComponent>();
                fault.InjectedTopologyFailures = 0;
                fault.ThrowOnTopologyPublished = true;

                Assert.That(lifecycle.TryCreateRoundNetwork(gameMap, baseLevel, out var error), Is.True, error);
                Assert.DoesNotThrow(() => mapSystem.InitializeMap(baseMapId));
                Assert.That(fault.InjectedTopologyFailures, Is.EqualTo(1),
                    "The post-commit topology observer was not invoked exactly once.");
                Assert.That(expectedObserverFailureLogged, Is.True,
                    "The injected post-commit observer exception was not logged as expected.");
                Assert.That(CountComponents<MapComponent>(), Is.EqualTo(mapsBefore + 2),
                    "A post-commit observer failure rolled back an initialized auxiliary level.");
                Assert.That(CountComponents<CMUZLevelsNetworkComponent>(), Is.EqualTo(networksBefore + 1),
                    "A post-commit observer failure rolled back the committed Z-network.");
                Assert.That(SEntMan.HasComponent<CMUZLevelMapComponent>(baseLevel), Is.True,
                    "A post-commit observer failure detached the caller-owned base.");
                Assert.That(
                    SEntMan.GetComponent<CMUZLifecycleFaultInjectionComponent>(baseLevel).Value,
                    Is.EqualTo(99),
                    "A post-commit observer failure restored pre-commit component state.");

                var zLevels = SEntMan.System<CMUZLevelsSystem>();
                Assert.That(zLevels.TryGetZNetwork(baseLevel, out var network), Is.True);
                foreach (var mapUid in network.Value.Comp.ZLevels.Values)
                {
                    if (mapUid is { } uid &&
                        SEntMan.TryGetComponent<MapComponent>(uid, out var map))
                    {
                        createdMapIds.Add(map.MapId);
                    }
                }
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                fault.ThrowOnTopologyPublished = false;
                fault.InjectedTopologyFailures = 0;
                foreach (var mapId in createdMapIds)
                {
                    if (mapSystem.MapExists(mapId))
                        mapSystem.DeleteMap(mapId);
                }

                if (createdMapIds.Count == 0 &&
                    mapSystem.MapExists(baseMapId))
                {
                    mapSystem.DeleteMap(baseMapId);
                }
            });
            Pair.ServerLogHandler.JudgeLog -= allowExpectedObserverFailure;
        }
    }

    [Test]
    public async Task RollsBackRoundNetworkWhenAuxiliaryMapInitializationThrows()
    {
        var server = Pair.Server;
        var fault = SEntMan.System<CMUZLifecycleFaultInjectionSystem>();
        var lifecycle = SEntMan.System<CMUZNetworkLifecycleSystem>();
        var mapSystem = SEntMan.System<ServerMapSystem>();
        var source = SProtoMan.Index<GameMapPrototype>(MapPrototype);
        var gameMap = source.Persistence(source.MapPath);
        gameMap.MapsAbove.Add(new ResPath("/Maps/Test/empty.yml"));
        gameMap.ZLevelsComponentOverrides = new ComponentRegistry
        {
            ["CMUZLifecycleFaultInjection"] =
                new EntityPrototype.ComponentRegistryEntry(new CMUZLifecycleFaultInjectionComponent()),
        };

        MapId baseMapId = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var baseLevel = mapSystem.CreateMap(out baseMapId, runMapInit: false);
                var mapsBefore = CountComponents<MapComponent>();
                var networksBefore = CountComponents<CMUZLevelsNetworkComponent>();
                fault.AuxiliaryMapInitBase = baseLevel;
                fault.ThrowOnAuxiliaryMapInit = true;

                Assert.That(lifecycle.TryCreateRoundNetwork(gameMap, baseLevel, out var error), Is.True, error);
                var exception = Assert.Throws<InvalidOperationException>(() => mapSystem.InitializeMap(baseMapId));
                Assert.That(exception.Message, Does.Contain(CMUZLifecycleFaultInjectionSystem.AuxiliaryMapInitFailure));
                Assert.That(CountComponents<MapComponent>(), Is.EqualTo(mapsBefore),
                    "An auxiliary level survived failed map initialization.");
                Assert.That(CountComponents<CMUZLevelsNetworkComponent>(), Is.EqualTo(networksBefore),
                    "Failed auxiliary map initialization left an orphan Z-network.");
                Assert.That(SEntMan.HasComponent<CMUZLevelMapComponent>(baseLevel), Is.False,
                    "Failed auxiliary map initialization left the caller-owned base attached.");
                Assert.That(SEntMan.HasComponent<CMUZLifecycleFaultInjectionComponent>(baseLevel), Is.False,
                    "Failed auxiliary map initialization did not restore component overrides on the caller-owned base.");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                fault.ThrowOnAuxiliaryMapInit = false;
                fault.AuxiliaryMapInitBase = null;
                if (mapSystem.MapExists(baseMapId))
                    mapSystem.DeleteMap(baseMapId);
            });
        }
    }

    [Test]
    public async Task CombinesExistingMapsThroughLifecycle()
    {
        var server = Pair.Server;
        var lifecycle = SEntMan.System<CMUZNetworkLifecycleSystem>();
        var mapSystem = SEntMan.System<ServerMapSystem>();
        var mapIds = new MapId[4];
        var levelMaps = new EntityUid[4];
        var removalRoofTile = new Vector2i(10000, 10000);
        EntityUid networkUid = default;

        await server.WaitAssertion(() =>
        {
            for (var i = 0; i < mapIds.Length; i++)
            {
                levelMaps[i] = mapSystem.CreateMap(out mapIds[i]);
                SEntMan.AddComponent<MapGridComponent>(levelMaps[i]);
            }

            var sharedMap = SEntMan.System<SharedMapSystem>();
            var tileManager = server.ResolveDependency<ITileDefinitionManager>();
            var floor = new Tile(tileManager["FloorSteel"].TileId);
            sharedMap.SetTile(
                levelMaps[0],
                SEntMan.GetComponent<MapGridComponent>(levelMaps[0]),
                removalRoofTile,
                floor);
            sharedMap.SetTile(
                levelMaps[1],
                SEntMan.GetComponent<MapGridComponent>(levelMaps[1]),
                removalRoofTile,
                floor);

            var networksBefore = CountComponents<CMUZLevelsNetworkComponent>();
            Assert.That(
                lifecycle.TryCombineLevels(levelMaps[..3], out var network, out var error),
                Is.True,
                error);
            Assert.That(network, Is.Not.Null);
            networkUid = network.Value.Owner;
            Assert.That(CountComponents<CMUZLevelsNetworkComponent>(), Is.EqualTo(networksBefore + 1));

            for (var depth = 0; depth < 3; depth++)
            {
                Assert.That(SEntMan.TryGetComponent<CMUZLevelMapComponent>(levelMaps[depth], out var level));
                Assert.That(level.Depth, Is.EqualTo(depth));
                Assert.That(level.NetworkUid, Is.EqualTo(networkUid));
            }

            Assert.That(
                lifecycle.TryCombineLevels(levelMaps[1..], out _, out error),
                Is.False);
            Assert.That(error, Does.Contain("already in network"));
            Assert.That(CountComponents<CMUZLevelsNetworkComponent>(), Is.EqualTo(networksBefore + 1),
                "A rejected combine created an orphan Z-network.");
            Assert.That(SEntMan.HasComponent<CMUZLevelMapComponent>(levelMaps[3]), Is.False,
                "A rejected combine modified an unattached map.");

            var source = SProtoMan.Index<GameMapPrototype>(MapPrototype);
            var invalidRoundNetwork = source.Persistence(source.MapPath);
            invalidRoundNetwork.MapsAbove.Add(source.MapsAbove[0]);
            var mapsBeforeRejectedRound = CountComponents<MapComponent>();

            Assert.That(
                lifecycle.TryCreateRoundNetwork(invalidRoundNetwork, levelMaps[0], out error),
                Is.False);
            Assert.That(error, Does.Contain("already in network"));
            Assert.That(CountComponents<MapComponent>(), Is.EqualTo(mapsBeforeRejectedRound),
                "Final validation failure did not roll back the loaded auxiliary level.");
            Assert.That(CountComponents<CMUZLevelsNetworkComponent>(), Is.EqualTo(networksBefore + 1),
                "Final validation failure created an orphan Z-network.");

            var roofSystem = SEntMan.System<ServerRoofSystem>();
            var lowerGrid = SEntMan.GetComponent<MapGridComponent>(levelMaps[0]);
            var lowerRoof = SEntMan.GetComponent<RoofComponent>(levelMaps[0]);
            Assert.That(
                roofSystem.IsRooved((levelMaps[0], lowerGrid, lowerRoof), removalRoofTile),
                Is.True,
                "The middle level did not roof the level below before removal.");
        });

        await server.WaitPost(() => mapSystem.DeleteMap(mapIds[1]));
        await server.WaitRunTicks(2);
        await server.WaitAssertion(() =>
        {
            var zLevels = SEntMan.System<CMUZLevelsSystem>();
            Assert.That(SEntMan.TryGetComponent<CMUZLevelsNetworkComponent>(networkUid, out var network), Is.True);
            Assert.That(zLevels.TryGetMapAtDepth((networkUid, network), 0, out var lowerMap), Is.True);
            Assert.That(lowerMap, Is.EqualTo(levelMaps[0]));
            Assert.That(zLevels.TryGetMapAtDepth((networkUid, network), 1, out _), Is.False);
            Assert.That(zLevels.TryGetMapAtDepth((networkUid, network), 2, out var upperMap), Is.True);
            Assert.That(upperMap, Is.EqualTo(levelMaps[2]));
            Assert.That(zLevels.TryGetZNetwork(levelMaps[1], out _), Is.False);

            Assert.That(SEntMan.TryGetComponent<CMUZLevelMapComponent>(levelMaps[0], out var lower));
            Assert.That(lower.MapAbove, Is.Null);
            Assert.That(SEntMan.TryGetComponent<CMUZLevelMapComponent>(levelMaps[2], out var upper));
            Assert.That(upper.MapBelow, Is.Null);

            var roofSystem = SEntMan.System<ServerRoofSystem>();
            var lowerGrid = SEntMan.GetComponent<MapGridComponent>(levelMaps[0]);
            var lowerRoof = SEntMan.GetComponent<RoofComponent>(levelMaps[0]);
            Assert.That(
                roofSystem.IsRooved((levelMaps[0], lowerGrid, lowerRoof), removalRoofTile),
                Is.False,
                "Removing the middle level did not clear its roof contribution below.");
        });

        await server.WaitPost(() =>
        {
            foreach (var mapId in mapIds)
            {
                if (mapSystem.MapExists(mapId))
                    mapSystem.DeleteMap(mapId);
            }
        });

        await server.WaitRunTicks(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityExists(networkUid), Is.False,
                "A combined Z-network must be deleted with its final map.");
        });
    }

    private async Task AssertClientTopology(
        NetEntity networkNet,
        IReadOnlyDictionary<int, NetEntity> mapNets)
    {
        await Client.WaitAssertion(() =>
        {
            var networkUid = CEntMan.GetEntity(networkNet);
            var network = CEntMan.GetComponent<CMUZLevelsNetworkComponent>(networkUid);
            Assert.That(network.ZLevels.Count, Is.EqualTo(mapNets.Count));
            Assert.That(network.ZLevelByEntity.Count, Is.EqualTo(mapNets.Count));

            foreach (var (depth, mapNet) in mapNets)
            {
                var mapUid = CEntMan.GetEntity(mapNet);
                var map = CEntMan.GetComponent<CMUZLevelMapComponent>(mapUid);
                var expectedAbove = mapNets.TryGetValue(depth + 1, out var above)
                    ? CEntMan.GetEntity(above)
                    : (EntityUid?) null;
                var expectedBelow = mapNets.TryGetValue(depth - 1, out var below)
                    ? CEntMan.GetEntity(below)
                    : (EntityUid?) null;

                Assert.Multiple(() =>
                {
                    Assert.That(network.ZLevels[depth], Is.EqualTo(mapUid));
                    Assert.That(network.ZLevelByEntity[mapUid], Is.EqualTo(depth));
                    Assert.That(map.NetworkUid, Is.EqualTo(networkUid));
                    Assert.That(map.Depth, Is.EqualTo(depth));
                    Assert.That(map.MapAbove, Is.EqualTo(expectedAbove));
                    Assert.That(map.MapBelow, Is.EqualTo(expectedBelow));
                });
            }
        });
    }

    private void AssertRoofs(
        ServerRoofSystem roofSystem,
        IReadOnlyDictionary<int, EntityUid> maps,
        Vector2i tile,
        bool expected,
        params int[] depths)
    {
        foreach (var depth in depths)
        {
            var mapUid = maps[depth];
            Assert.That(SEntMan.TryGetComponent<MapGridComponent>(mapUid, out var grid));
            Assert.That(SEntMan.TryGetComponent<RoofComponent>(mapUid, out var roof));
            Assert.That(
                roofSystem.IsRooved((mapUid, grid, roof), tile),
                Is.EqualTo(expected),
                $"Unexpected roof state at depth {depth}.");
        }
    }

    private int CountComponents<T>() where T : IComponent
    {
        var count = 0;
        var query = SEntMan.EntityQueryEnumerator<T>();
        while (query.MoveNext(out _, out _))
        {
            count++;
        }

        return count;
    }

    private void AssertPrototypeCountAtLeast(string prototype, int expected)
    {
        var count = CountPrototype(prototype);

        Assert.That(count, Is.GreaterThanOrEqualTo(expected),
            $"Expected at least {expected} resolved {prototype} entities.");
    }

    private int CountPrototype(string prototype)
    {
        var count = 0;
        var query = SEntMan.EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out _, out var metadata))
        {
            if (!metadata.Deleted && metadata.EntityPrototype?.ID == prototype)
                count++;
        }

        return count;
    }
}

public sealed class CMUZLifecycleFaultInjectionSystem : EntitySystem
{
    public const string AuxiliaryMapInitFailure = "Injected auxiliary Z-level map initialization failure.";
    public const string TopologyFailure = "Injected Z-network topology publication failure.";

    public EntityUid? AuxiliaryMapInitBase;
    public bool ThrowOnAuxiliaryMapInit;
    public bool ThrowOnTopologyPublished;
    public int InjectedTopologyFailures;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUZLevelNetworkUpdatedEvent>(OnNetworkUpdated);
        SubscribeLocalEvent<CMUZLifecycleFaultInjectionComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<CMUZLifecycleFaultInjectionComponent> ent, ref MapInitEvent args)
    {
        if (ThrowOnAuxiliaryMapInit &&
            ent.Owner != AuxiliaryMapInitBase)
        {
            throw new InvalidOperationException(AuxiliaryMapInitFailure);
        }
    }

    private void OnNetworkUpdated(ref CMUZLevelNetworkUpdatedEvent args)
    {
        if (!ThrowOnTopologyPublished)
            return;

        InjectedTopologyFailures++;
        throw new InvalidOperationException(TopologyFailure);
    }
}

[RegisterComponent]
public sealed partial class CMUZLifecycleFaultInjectionComponent : Component
{
    [DataField]
    public int Value;
}
