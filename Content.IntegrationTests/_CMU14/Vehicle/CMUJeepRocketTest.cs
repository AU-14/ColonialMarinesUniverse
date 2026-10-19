using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Vehicle.Open;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Vehicle;

/// <summary>
/// An engine killed far past what it had left goes up at once: one M7-SMAW rocket wrecks a jeep on
/// the spot, and a burning engine shot on goes up before its time. While it burns, its fire spreads
/// onto the ground round the jeep.
/// </summary>
[TestFixture]
public sealed class CMUJeepRocketTest : GameTest
{
    [TestCase("RMCProjectileRocket84mm")]
    [TestCase("RMCProjectileRocket84mmAntiArmor")]
    public async Task OneRocketWrecksAJeep(string rocket)
    {
        var map = await Pair.CreateTestMap();
        EntityUid jeep = default;
        await Server.WaitPost(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            for (var x = -8; x <= 8; x++)
            {
                for (var y = -8; y <= 8; y++)
                {
                    maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);
                }
            }

            jeep = SEntMan.SpawnEntity("CMUVehicleJeepCargo", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
        });
        await RunTicksSync(5);

        await Server.WaitPost(() =>
        {
            // From the side, four tiles out: the shot that leaves the engine the least.
            var shot = SEntMan.SpawnEntity(rocket, new EntityCoordinates(map.Grid, 4.5f, 0.5f));
            SEntMan.System<SharedGunSystem>().ShootProjectile(shot, -Vector2.UnitX, Vector2.Zero, null, speed: 20);
        });
        await RunSeconds(1f);

        await Server.WaitAssertion(() =>
        {
            var gone = SEntMan.Deleted(jeep);
            var engine = gone ? 0f : SEntMan.GetComponent<CMUOpenVehicleComponent>(jeep).EngineIntegrity;
            var burning = !gone && SEntMan.GetComponent<CMUOpenVehicleComponent>(jeep).Burning;
            var hull = gone ? 0f : SEntMan.GetComponent<HardpointIntegrityComponent>(jeep).Integrity;
            var wreck = SEntMan.EntityQuery<MetaDataComponent>()
                .Any(m => m.EntityPrototype?.ID?.StartsWith("CMUJeepWreck") == true);
            TestContext.Out.WriteLine($"{rocket}: deleted {gone}, wreck {wreck}, engine {engine}, burning {burning}, hull {hull}");
            Assert.That(gone || wreck, Is.True, $"One {rocket} should leave the jeep a wreck.");
        });
    }

    private async Task<(TestMapData Map, EntityUid Jeep)> BurningJeep()
    {
        var map = await Pair.CreateTestMap();
        EntityUid jeep = default;
        await Server.WaitPost(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            for (var x = -8; x <= 8; x++)
            {
                for (var y = -8; y <= 8; y++)
                {
                    maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);
                }
            }

            jeep = SEntMan.SpawnEntity("CMUVehicleJeepCargo", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
        });
        await RunTicksSync(5);

        // Exactly the engine's integrity onto the hood: dead, with nothing past it.
        await Server.WaitPost(() => Hit(map, jeep, SEntMan.GetComponent<CMUOpenVehicleComponent>(jeep).EngineIntegrity));
        await RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(jeep), Is.False, "Killed with nothing to spare, the engine burns first.");
            Assert.That(SEntMan.GetComponent<CMUOpenVehicleComponent>(jeep).Burning, Is.True);
        });
        return (map, jeep);
    }

    private void Hit(TestMapData map, EntityUid jeep, float amount)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict["Structural"] = amount;
        // Facing south: ahead is -y, a hit on the hood.
        SEntMan.System<DamageableSystem>().TryChangeDamage(jeep, damage,
            origin: SEntMan.SpawnEntity(null, new EntityCoordinates(map.Grid, 0.5f, -2.5f)));
    }

    [Test]
    public async Task ABurningEngineShotOnGoesUpEarly()
    {
        var (map, jeep) = await BurningJeep();
        var limit = 0f;
        await Server.WaitPost(() =>
        {
            limit = SEntMan.GetComponent<CMUVehicleWreckComponent>(jeep).OverkillLimit;
            Hit(map, jeep, limit + 10f);
        });
        await RunSeconds(1f);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.Deleted(jeep), Is.True, "Shot past the limit while burning, it goes up at once."));
    }

    [Test]
    public async Task ItsFireSpreadsBeforeItGoesUp()
    {
        var (_, jeep) = await BurningJeep();
        await RunSeconds(2f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(jeep), Is.False, "Not yet: it burns for a while first.");
            var fires = SEntMan.EntityQuery<MetaDataComponent>().Count(m => m.EntityPrototype?.ID == "RMCTileFire");
            Assert.That(fires, Is.GreaterThan(0), "The engine's fire spreads onto the ground round the jeep.");
        });
    }
}
