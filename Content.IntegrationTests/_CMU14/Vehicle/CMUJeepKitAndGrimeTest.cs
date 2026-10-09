using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.CMU14.Vehicle.Jeep;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Vehicle;

/// <summary>
/// A jeep kit crate opens into its chassis with the parts beside it, and space cleaner takes a
/// jeep's dirt, blood and crayon off.
/// </summary>
[TestFixture]
public sealed class CMUJeepKitAndGrimeTest : GameTest
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
    public async Task KitCrateOpensIntoChassis()
    {
        var map = await Floor();
        EntityUid crate = default;
        EntityUid user = default;
        await Server.WaitPost(() =>
        {
            crate = SEntMan.SpawnEntity("CMUCrateJeepKitCargo", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            user = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 0.5f, 2.5f));
        });
        await RunTicksSync(5);

        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.EntityExists(crate), Is.True, "Spawning the crate should not unpack it.");
            SEntMan.System<SharedEntityStorageSystem>().OpenStorage(crate, user);
        });
        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(crate), Is.True, "The crate should give way to the chassis.");
            var chassis = SEntMan.EntityQuery<CMUJeepComponent>(true).Select(c => c.Owner).ToList();
            Assert.That(chassis, Has.Count.EqualTo(1));
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(chassis[0]).EntityPrototype?.ID, Is.EqualTo("CMUVehicleJeepCargoChassis"));
            var manual = SEntMan.EntityQuery<MetaDataComponent>(true)
                .Count(m => m.EntityPrototype?.ID == "CMUPaperJeepAssemblyManual");
            Assert.That(manual, Is.EqualTo(1), "The manual should come out of the crate.");
        });
    }

    [Test]
    public async Task SpaceCleanerTakesGrimeOff()
    {
        var map = await Floor();
        EntityUid jeep = default;
        await Server.WaitPost(() =>
        {
            jeep = SEntMan.SpawnEntity("CMUVehicleJeepTransport", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
        });
        await RunTicksSync(5);

        await Server.WaitPost(() =>
        {
            var grime = SEntMan.System<CMUVehicleGrimeSystem>();
            var comp = SEntMan.GetComponent<CMUVehicleGrimeComponent>(jeep);
            grime.SetDirt((jeep, comp), 0.8f);
            for (var i = 0; i < 6; i++)
            {
                grime.AddBlood(jeep, Color.DarkRed, new Vector3(-6, -10, 13), "up", true);
            }

            Assert.That(comp.Dirt, Is.GreaterThan(0.5f));
            Assert.That(SEntMan.GetComponent<CMUVehicleCrayonComponent>(jeep).Blood, Has.Count.EqualTo(6));

            // Two good sprays' worth.
            var reactive = SEntMan.System<ReactiveSystem>();
            var solution = new Solution("SpaceCleaner", 10);
            reactive.DoEntityReaction(jeep, solution, ReactionMethod.Touch);
        });
        await RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<CMUVehicleGrimeComponent>(jeep).Dirt, Is.Zero);
            Assert.That(SEntMan.GetComponent<CMUVehicleCrayonComponent>(jeep).Blood, Is.Empty);
        });
    }
}
