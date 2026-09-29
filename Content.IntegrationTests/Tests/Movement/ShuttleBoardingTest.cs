using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._RMC14.CCVar;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.CCVar;
using Content.Shared.CMU14.TileMovement;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Gravity;
using Content.Shared.Tests;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Movement;

public sealed class ShuttleBoardingTest : InteractionTest
{
    protected override string PlayerPrototype => "CMMobHuman";
    protected override ResPath? TestMapPath => new("/Maps/CMU14/Shuttles/alamo.yml");

    [TestCase(true, false)]
    [TestCase(false, false)]
    [TestCase(true, true)]
    [TestCase(false, true)]
    public async Task DecompressionDoesNotLaunchBoardingPlayer(bool staticShuttle, bool tileMovement)
    {
        await Server.WaitAssertion(() =>
        {
            // The breach is on the map. Keep the boarding target stationary while testing that impulse.
            Server.System<AtmosphereSystem>().SetAtmosphereSimulation(
                (MapData.Grid.Owner, SEntMan.GetComponent<GridAtmosphereComponent>(MapData.Grid.Owner)), false);
        });
        // Reproduce the grid impulse independently of direct wind on the player.
        await OverrideCVar(Side.Server, CCVars.SpaceWind, false);
        await OverrideCVar(Side.Server, CCVars.AtmosGridImpulse, true);
        await OverrideCVar(Side.Server, RMCCVars.RMCAtmosTileEqualize, true);
        await OverrideCVar(Side.Server, CCVars.AtmosMaxProcessTime, 9999f);
        await Server.WaitAssertion(() =>
        {
            MapSystem.SetPaused(MapData.MapUid, false);
            Transform.SetLocalPosition(MapData.Grid.Owner, new Vector2(1000, -800));
            var terrain = SEntMan.EnsureComponent<MapGridComponent>(MapData.MapUid);
            // CMU maps can be saved with both Map and dynamic Shuttle physics on the same entity.
            SEntMan.EnsureComponent<PhysicsComponent>(MapData.MapUid);
            SEntMan.EnsureComponent<FixturesComponent>(MapData.MapUid);
            SEntMan.EnsureComponent<ShuttleComponent>(MapData.MapUid);
            var floor = new Tile(TileMan["Plating"].TileId);
            for (var x = 980; x < 1020; x++)
            for (var y = -820; y < -780; y++)
                MapSystem.SetTile(MapData.MapUid, terrain, new Vector2i(x, y), floor);
            SEntMan.EnsureComponent<GravityComponent>(MapData.MapUid);
            var atmos = Server.System<AtmosphereSystem>();
            var mapAtmos = SEntMan.EnsureComponent<GridAtmosphereComponent>(MapData.MapUid);
            var mapOverlay = SEntMan.EnsureComponent<GasTileOverlayComponent>(MapData.MapUid);
            var processMap = new Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent>(
                MapData.MapUid, mapAtmos, mapOverlay, terrain, SEntMan.GetComponent<TransformComponent>(MapData.MapUid));
            atmos.RunProcessingFull(processMap, MapData.MapUid, atmos.AtmosTickRate);
            for (var x = 980; x < 1020; x++)
            for (var y = -820; y < -780; y++)
            {
                var air = atmos.GetTileMixture((MapData.MapUid, mapAtmos, mapOverlay), MapData.MapUid, new Vector2i(x, y), true);
                Assert.That(air, Is.Not.Null);
                Assert.That(air!.Immutable, Is.False);
                air.Temperature = Atmospherics.T20C;
                air.SetMoles(Gas.Oxygen, 21.824879f);
                air.SetMoles(Gas.Nitrogen, 82.10312f);
            }
            Transform.SetCoordinates(SPlayer, new EntityCoordinates(MapData.MapUid, 994.5f, -798.5f));
            var playerPhysics = SEntMan.GetComponent<PhysicsComponent>(SPlayer);
            Server.System<SharedPhysicsSystem>().SetLinearVelocity(SPlayer, Vector2.Zero, body: playerPhysics);
            var molesBefore = mapAtmos.Tiles.Values.Sum(tile => tile.Air?.TotalMoles ?? 0f);
            atmos.RunProcessingFull(processMap, MapData.MapUid, atmos.AtmosTickRate);
            Assert.That(mapAtmos.Tiles.Values.Sum(tile => tile.Air?.TotalMoles ?? 0f), Is.LessThan(molesBefore),
                "The map must actually decompress to exercise the grid impulse.");
            Transform.SetCoordinates(SPlayer, new EntityCoordinates(MapData.Grid.Owner, -2.5f, 1.5f));
            Assert.That(playerPhysics.LinearVelocity.Length(), Is.LessThan(10), "Entering the shuttle inherited decompression velocity from its map.");
            var mapPhysics = SEntMan.GetComponent<PhysicsComponent>(MapData.MapUid);
            Assert.That(mapPhysics.LinearVelocity, Is.EqualTo(Vector2.Zero));
            Assert.That(mapPhysics.AngularVelocity, Is.Zero);
            if (staticShuttle)
                Server.System<ShuttleSystem>().Disable(MapData.Grid.Owner);
            if (tileMovement)
                SEntMan.EnsureComponent<CMUTileMovementComponent>(SPlayer);
            foreach (var door in SEntMan.EntityQuery<DoorComponent>())
            {
                if (SEntMan.GetComponent<TransformComponent>(door.Owner).GridUid == MapData.Grid.Owner)
                    Server.System<SharedDoorSystem>().StartOpening(door.Owner, door);
            }
            Transform.SetCoordinates(SPlayer, new EntityCoordinates(MapData.MapUid, 994.5f, -798.5f));
        });
        await Pair.RunUntilSynced();
        var serverPosition = Transform.GetWorldPosition(SPlayer);
        var clientTransform = Client.System<SharedTransformSystem>();
        var clientPosition = clientTransform.GetWorldPosition(CPlayer);
        var boarded = false;
        var exited = false;

        foreach (var key in new[] { EngineKeyFunctions.MoveRight, EngineKeyFunctions.MoveLeft })
        {
            await SetKey(key, BoundKeyState.Down);
            for (var i = 0; i < 50; i++)
            {
                await Pair.RunTicksSync(1);
                await Server.WaitAssertion(() =>
                {
                    var position = Transform.GetWorldPosition(SPlayer);
                    Assert.That(Vector2.Distance(position, serverPosition), Is.LessThan(0.75f),
                        $"Server moved player from {serverPosition} to {position} in one tick.");
                    Assert.That(SEntMan.GetComponent<PhysicsComponent>(SPlayer).LinearVelocity.Length(), Is.LessThan(10));
                    serverPosition = position;
                    var parent = SEntMan.GetComponent<TransformComponent>(SPlayer).ParentUid;
                    boarded |= parent == MapData.Grid.Owner;
                    exited |= boarded && parent == MapData.MapUid;
                });
                await Client.WaitAssertion(() =>
                {
                    var position = clientTransform.GetWorldPosition(CPlayer);
                    Assert.That(Vector2.Distance(position, clientPosition), Is.LessThan(0.75f),
                        $"Client moved player from {clientPosition} to {position} in one tick.");
                    clientPosition = position;
                });
            }
            await SetKey(key, BoundKeyState.Up);
        }
        Assert.That(boarded, Is.True, "The player must actually board the shuttle.");
        Assert.That(exited, Is.True, "The player must actually leave the shuttle.");
    }
}
