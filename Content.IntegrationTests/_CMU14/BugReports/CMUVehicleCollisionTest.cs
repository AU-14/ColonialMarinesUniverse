using System.Numerics;
using System.Reflection;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Damage.Systems;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using VehicleSystem = Content.Shared.Vehicle.Systems.VehicleSystem;

namespace Content.IntegrationTests.CMU14.BugReports;

[TestFixture]
public sealed class CMUVehicleCollisionTest
{
    [TestCase(0.5f)]
    [TestCase(8f)]
    public async Task VehicleImpactChargesHullAndDriverOncePerContact(float speed)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        EntityUid rammer = default, struck = default, driver = default;
        var firstDriverDamage = 0f;
        var secondDriverDamage = 0f;
        var firstRammerIntegrity = 0f;
        var firstStruckIntegrity = 0f;
        var contacts = typeof(GridVehicleMoverSystem).GetMethod("CanOccupyTransform", BindingFlags.Instance | BindingFlags.NonPublic)!;

        bool Probe(bool applyEffects = true)
        {
            var mover = entities.GetComponent<GridVehicleMoverComponent>(rammer);
            #pragma warning disable RA0002
            mover.CurrentSpeed = speed;
            #pragma warning restore RA0002
            return (bool) contacts.Invoke(entities.System<GridVehicleMoverSystem>(),
                [rammer, mover, map.Grid.Owner, new Vector2(0.5f, speed / 60f), null, 0.01f, applyEffects, false, null, null, null, null])!;
        }

        float DriverDamage() => entities.System<DamageableSystem>().GetTotalDamage(driver).Float();
        float Integrity(EntityUid vehicle) => entities.GetComponent<HardpointIntegrityComponent>(vehicle).Integrity;

        await pair.Server.WaitAssertion(() =>
        {
            var maps = entities.System<SharedMapSystem>();
            var tiles = pair.Server.ResolveDependency<ITileDefinitionManager>();
            for (var x = -3; x <= 6; x++)
            for (var y = -4; y <= 6; y++)
                maps.SetTile(map.Grid, new Vector2i(x, y), new Tile(tiles["Plating"].TileId));
            rammer = entities.SpawnEntity("VehiclePizzaVan", map.GridCoords.Offset(new Vector2(0.5f, 0)));
            // Leave a small gap at the inset movement boundary that even a low-speed step can close.
            var inset = entities.GetComponent<GridVehicleMoverComponent>(rammer).MovementCollisionInset;
            struck = entities.SpawnEntity("VehiclePizzaVan", map.GridCoords.Offset(new Vector2(0.5f, 1.755f - inset)));
            driver = entities.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(4.5f, 4.5f)));
            Assert.That(entities.System<VehicleSystem>().TrySetOperator(
                (struck, entities.GetComponent<VehicleComponent>(struck)), driver), Is.True);
            var initialRammerIntegrity = Integrity(rammer);
            var initialStruckIntegrity = Integrity(struck);

            Assert.That(Probe(false), Is.False, "An intact vehicle must block the movement probe.");
            Assert.That(DriverDamage(), Is.Zero, "A probe without effects must not damage the driver.");
            Assert.That(Integrity(rammer), Is.EqualTo(initialRammerIntegrity));
            Assert.That(Integrity(struck), Is.EqualTo(initialStruckIntegrity));

            Assert.That(Probe(), Is.False, "A collision must not treat an intact vehicle as a destroyed wall.");
            firstDriverDamage = DriverDamage();
            firstRammerIntegrity = Integrity(rammer);
            firstStruckIntegrity = Integrity(struck);
            if (speed >= entities.GetComponent<GridVehicleMoverComponent>(rammer).WallSmashMinSpeed)
            {
                Assert.That(firstDriverDamage, Is.InRange(1f, 99f), "A van impact must hurt its driver without applying wall-demolition damage.");
                Assert.That(firstRammerIntegrity, Is.LessThan(initialRammerIntegrity));
                Assert.That(firstStruckIntegrity, Is.LessThan(initialStruckIntegrity), "The impact must reach vehicle hardpoints.");
            }
            else
            {
                Assert.That(firstDriverDamage, Is.Zero, "A low-speed bump must not injure the driver.");
                Assert.That(firstRammerIntegrity, Is.EqualTo(initialRammerIntegrity));
                Assert.That(firstStruckIntegrity, Is.EqualTo(initialStruckIntegrity));
            }

            for (var probe = 0; probe < 20; probe++)
                Assert.That(Probe(), Is.False);
            Assert.That(DriverDamage(), Is.EqualTo(firstDriverDamage));
            Assert.That(Integrity(rammer), Is.EqualTo(firstRammerIntegrity));
            Assert.That(Integrity(struck), Is.EqualTo(firstStruckIntegrity));
        });

        // The contact guard must outlive the old wall-smash cooldown.
        await pair.Server.WaitRunTicks(60);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(Probe(), Is.False);
            Assert.That(DriverDamage(), Is.EqualTo(firstDriverDamage));
            Assert.That(Integrity(rammer), Is.EqualTo(firstRammerIntegrity));
            Assert.That(Integrity(struck), Is.EqualTo(firstStruckIntegrity));

            var transform = entities.System<SharedTransformSystem>();
            transform.SetCoordinates(rammer, map.GridCoords.Offset(new Vector2(0.5f, -3f)));
            transform.SetCoordinates(rammer, map.GridCoords.Offset(new Vector2(0.5f, 0)));
            Assert.That(Probe(), Is.False);
            if (firstDriverDamage > 0f)
            {
                Assert.That(DriverDamage(), Is.GreaterThan(firstDriverDamage), "Separating and colliding again must allow a new impact.");
                Assert.That(Integrity(struck), Is.LessThan(firstStruckIntegrity));
            }
            secondDriverDamage = DriverDamage();
        });
        TestContext.Out.WriteLine($"speed={speed}; first impact driver damage={firstDriverDamage}; after separation and second impact={secondDriverDamage}");
        await pair.CleanReturnAsync();
    }
}
