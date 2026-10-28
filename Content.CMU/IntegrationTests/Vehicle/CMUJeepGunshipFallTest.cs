using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Vehicle.Open;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Vehicle;

[TestFixture]
public sealed class CMUJeepGunshipFallTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task OccupiedJeepLeavingGunshipFallsThroughEmptyUpperMap()
    {
        EntityUid upper = default, ground = default, jeep = default, rider = default;
        NetEntity groundNet = default, jeepNet = default, riderNet = default;
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            Assert.That(Server.System<MapLoaderSystem>().TryLoadMap(
                new ResPath("/Maps/_RMC14/Shuttles/dynamic_gunship.yml"), out var loaded, out var grids,
                DeserializationOptions.Default with { InitializeMaps = true }), Is.True);
            upper = loaded!.Value.Owner;
            var gunship = grids!.Single();
            Assert.That(SEntMan.HasComponent<MapGridComponent>(upper), Is.False,
                "The air level must have no terrain grid beneath the gunship.");
            ground = maps.CreateMap(runMapInit: true);
            var groundGrid = SEntMan.EnsureComponent<MapGridComponent>(ground);
            var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
            for (var x = -5; x <= 25; x++)
            for (var y = -5; y <= 5; y++)
                maps.SetTile(ground, groundGrid, new Vector2i(x, y), floor);
            var z = Server.System<CMUZLevelsSystem>();
            Assert.That(z.TryAddMapsIntoZNetwork(z.CreateZNetwork(), new() { [ground] = 0, [upper] = 1 }), Is.True);

            var tile = maps.GetAllTiles(gunship.Owner, gunship.Comp)
                .OrderBy(tile => tile.GridIndices.X * tile.GridIndices.X + tile.GridIndices.Y * tile.GridIndices.Y).First();
            var coordinates = maps.GridTileToLocal(gunship.Owner, gunship.Comp, tile.GridIndices);
            jeep = SEntMan.SpawnEntity("CMUVehicleJeepGunner", coordinates);
            rider = SEntMan.SpawnEntity("CMMobHuman", coordinates);
            var seat = SEntMan.GetComponent<CMUVehicleSeatsComponent>(jeep).SeatEntities[1];
            Server.System<SharedTransformSystem>().SetCoordinates(rider, SComp<TransformComponent>(seat).Coordinates);
            Server.System<CMUVehicleSeatSystem>().ClickSeat(seat, rider);
            groundNet = SEntMan.GetNetEntity(ground);
            jeepNet = SEntMan.GetNetEntity(jeep);
            riderNet = SEntMan.GetNetEntity(rider);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, rider);
        });
        await RunSeconds(4);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<BuckleComponent>(rider).Buckled, Is.True);
            Assert.That(SEntMan.HasComponent<CMUZFallingComponent>(jeep), Is.False);
            Server.System<SharedTransformSystem>().SetCoordinates(jeep, new EntityCoordinates(upper, new Vector2(20.5f, 0.5f)));
            Assert.That(SEntMan.HasComponent<CMUZFallingComponent>(jeep), Is.True,
                "Unloading into empty air must wake the vehicle without a manual physics wake.");
        });
        await RunSeconds(4);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<TransformComponent>(jeep).MapUid, Is.EqualTo(ground));
            Assert.That(SComp<CMUZPhysicsComponent>(jeep).LocalPosition, Is.Zero.Within(0.001f));
            Assert.That(SEntMan.HasComponent<CMUZFallingComponent>(jeep), Is.False);
            Assert.That(SComp<TransformComponent>(rider).MapUid, Is.EqualTo(ground));
            Assert.That(SComp<BuckleComponent>(rider).Buckled, Is.True);
            foreach (var seat in SComp<CMUVehicleSeatsComponent>(jeep).SeatEntities)
                Assert.That(SComp<TransformComponent>(seat).MapUid, Is.EqualTo(ground));
            foreach (var part in SComp<CMUOpenVehicleComponent>(jeep).PartEntities)
                Assert.That(SComp<TransformComponent>(part).MapUid, Is.EqualTo(ground));
        });
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var clientGround = CEntMan.GetEntity(groundNet);
            Assert.That(CEntMan.GetComponent<TransformComponent>(CEntMan.GetEntity(jeepNet)).MapUid, Is.EqualTo(clientGround));
            Assert.That(CEntMan.GetComponent<TransformComponent>(CEntMan.GetEntity(riderNet)).MapUid, Is.EqualTo(clientGround));
        });
    }
}
