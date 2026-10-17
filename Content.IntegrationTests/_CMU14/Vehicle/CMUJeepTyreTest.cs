using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Vehicle;
using Content.Shared.CMU14.Vehicle.Open;
using Content.Shared.Maps;
using Robust.Shared.ContentPack;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Vehicle;

/// <summary>
/// A jeep runs on the tyres of the wheel set on its axle: off-road ones as it was tuned, road ones
/// quicker on paving, slower on dug ground and dragged harder by water, drawn with art of their own.
/// The colony's crates ship road wheels.
/// </summary>
[TestFixture]
public sealed class CMUJeepTyreTest : GameTest
{
    private async Task<TestMapData> Floor()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitPost(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            for (var x = -4; x <= 4; x++)
            {
                for (var y = -4; y <= 4; y++)
                {
                    maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);
                }
            }
        });
        return map;
    }

    [Test]
    public async Task TyresFollowTheFittedWheelsAndTheGround()
    {
        var map = await Floor();
        EntityUid jeep = default;
        await Server.WaitPost(() =>
            jeep = SEntMan.SpawnEntity("CMUVehicleJeepCargo", new EntityCoordinates(map.Grid, 0.5f, 0.5f)));
        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            var tyres = SEntMan.System<CMUVehicleTyreSystem>();
            var fitted = SEntMan.GetComponent<CMUVehicleTyresComponent>(jeep);
            Assert.That(fitted.Tyre?.Id, Is.EqualTo("OffRoad"));
            Assert.That(tyres.SpeedMultiplier(jeep), Is.EqualTo(1f), "Off-road tyres leave the jeep as it was tuned.");

            var containers = SEntMan.System<SharedContainerSystem>();
            var axle = containers.GetContainer(jeep, "wheel-1");
            SEntMan.DeleteEntity(axle.ContainedEntities[0]);
            Assert.That(fitted.Tyre, Is.Null, "Bare drums have no tyres.");

            var road = SEntMan.SpawnEntity("CMUJeepWheelRoad", SEntMan.GetComponent<TransformComponent>(jeep).Coordinates);
            Assert.That(containers.Insert(road, axle), Is.True);
            Assert.That(fitted.Tyre?.Id, Is.EqualTo("Road"));
            Assert.That(tyres.StatePrefix(jeep), Is.EqualTo("road_"));
            Assert.That(tyres.OnRoughGround(jeep), Is.False, "Plating is paved.");
            Assert.That(tyres.SpeedMultiplier(jeep), Is.EqualTo(1.15f).Within(0.001f));
            Assert.That(tyres.WaterDrag(jeep), Is.EqualTo(1.35f).Within(0.001f));

            var maps = SEntMan.System<SharedMapSystem>();
            var dirt = Server.ResolveDependency<ITileDefinitionManager>()["CMPlanetDirt"];
            for (var x = -4; x <= 4; x++)
            {
                for (var y = -4; y <= 4; y++)
                {
                    maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), new Tile(dirt.TileId));
                }
            }

            Assert.That(tyres.OnRoughGround(jeep), Is.True, "Dirt digs, so it is rough.");
            Assert.That(tyres.SpeedMultiplier(jeep), Is.EqualTo(0.8f).Within(0.001f));
        });
    }

    [Test]
    public async Task ColonyCratesShipRoadWheels()
    {
        var map = await Floor();
        EntityUid crate = default;
        await Server.WaitPost(() =>
            crate = SEntMan.SpawnEntity("CMUCrateJeepCargoColony", new EntityCoordinates(map.Grid, 0.5f, 0.5f)));
        await RunTicksSync(5);

        EntityUid jeep = default;
        await Server.WaitPost(() =>
            jeep = SEntMan.System<CMUVehicleCrateSystem>().Open((crate, SEntMan.GetComponent<CMUVehicleCrateComponent>(crate))));
        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            var containers = SEntMan.System<SharedContainerSystem>();
            foreach (var slot in new[] { "wheel-1", CMUOpenVehicleSystem.SpareSlot })
            {
                var held = containers.GetContainer(jeep, slot).ContainedEntities;
                Assert.That(held, Has.Count.EqualTo(1), slot);
                Assert.That(SEntMan.GetComponent<MetaDataComponent>(held[0]).EntityPrototype?.ID, Is.EqualTo("CMUJeepWheelRoad"), slot);
            }

            Assert.That(SEntMan.GetComponent<CMUVehicleTyresComponent>(jeep).Tyre?.Id, Is.EqualTo("Road"), "It rolls out on road tyres.");
        });
    }

    [Test]
    public async Task RoadTyresHaveTheirOwnArt()
    {
        await Server.WaitAssertion(() =>
        {
            var resources = Server.ResolveDependency<IResourceManager>();
            const string dir = "/Textures/CMU14/Structures/vehicles/jeep";
            var states = new[]
            {
                "road_wheels_0", "road_wheels_overlay_0", "road_spare_0", "road_spare_overlay_0",
                "road_spare_open_0", "road_spare_opening_0", "road_spare_closing_0",
            };

            foreach (var rsi in new[] { "jeep", "jeep_gunner", "jeep_medical", "jeep_transport" })
            {
                foreach (var state in states.Concat(states.Select(s => "rims_" + s[5..])))
                {
                    Assert.That(resources.ContentFileExists(new ResPath($"{dir}/{rsi}.rsi/{state}.png")), Is.True, $"{rsi} {state}");
                }
            }

            Assert.That(resources.ContentFileExists(new ResPath($"{dir}/hardpoints.rsi/road_tires.png")), Is.True);
        });
    }
}
