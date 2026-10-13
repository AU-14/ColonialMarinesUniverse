using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Vehicle.Open;
using Content.Shared._RMC14.Stun;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Vehicle;
using Content.Shared.CMU14.Vehicle.Open;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Vehicle;

/// <summary>
/// A hard head on crash blacks the jeep's riders out and knocks them out, more surely the faster it
/// hit, and the horn blares while the driver is out on the wheel. The gun's close and distant shots and the horns load.
/// </summary>
[TestFixture]
public sealed class CMUJeepCrashKnockoutTest : GameTest
{
    private async Task<(EntityUid Jeep, EntityUid Driver)> SeatedJeep()
    {
        var map = await Pair.CreateTestMap();
        EntityUid jeep = default;
        EntityUid driver = default;
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

            jeep = SEntMan.SpawnEntity("CMUVehicleJeepCargo", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            driver = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 1.45f, 0.75f));
        });
        await RunTicksSync(5);

        EntityUid seat = default;
        await Server.WaitPost(() =>
        {
            seat = SEntMan.GetComponent<CMUVehicleSeatsComponent>(jeep).SeatEntities
                .First(s => SEntMan.GetComponent<CMUVehicleSeatComponent>(s).Driver);
            SEntMan.System<CMUVehicleSeatSystem>().ClickSeat(seat, driver);
        });
        await RunSeconds(3f);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<BuckleComponent>(driver).BuckledTo, Is.EqualTo(seat), "The driver should be seated."));

        return (jeep, driver);
    }

    private void Crash(EntityUid jeep, bool frontal, float speed)
    {
        var ev = new CMUVehicleCrashedEvent(30f, frontal, speed);
        SEntMan.EventBus.RaiseLocalEvent(jeep, ref ev);
    }

    [Test]
    public void ChanceRisesWithSpeedHeadOnOnly()
    {
        var comp = new CMUVehicleCrashKnockoutComponent { MinSpeed = 0.7f, MinChance = 0.2f };
        Assert.That(CMUVehicleCrashKnockoutSystem.Chance(comp, true, 0.69f), Is.Zero);
        Assert.That(CMUVehicleCrashKnockoutSystem.Chance(comp, true, 0.7f), Is.EqualTo(0.2f).Within(0.001f));
        Assert.That(CMUVehicleCrashKnockoutSystem.Chance(comp, true, 0.85f), Is.EqualTo(0.6f).Within(0.001f));
        Assert.That(CMUVehicleCrashKnockoutSystem.Chance(comp, true, 1f), Is.EqualTo(1f).Within(0.001f));
        Assert.That(CMUVehicleCrashKnockoutSystem.Chance(comp, false, 1f), Is.Zero, "Side and rear crashes knock no one out.");
    }

    [Test]
    public async Task TopSpeedCrashKnocksTheDriverOutAndBlaresTheHorn()
    {
        var (jeep, driver) = await SeatedJeep();

        await Server.WaitPost(() =>
        {
            Crash(jeep, false, 1f);
            Assert.That(SEntMan.HasComponent<RMCUnconsciousComponent>(driver), Is.False, "A side crash shouldn't knock anyone out.");
            Crash(jeep, true, 0.5f);
            Assert.That(SEntMan.HasComponent<RMCUnconsciousComponent>(driver), Is.False, "A slow crash shouldn't knock anyone out.");

            Crash(jeep, true, 1f);
            Assert.That(SEntMan.HasComponent<RMCUnconsciousComponent>(driver), Is.True, "A top speed head on crash always knocks out.");
            Assert.That(SEntMan.GetComponent<CMUVehicleCrashKnockoutComponent>(jeep).Horn, Is.Not.Null, "The horn should blare.");
        });

        await RunSeconds(2f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<RMCUnconsciousComponent>(driver), Is.True, "Out for at least three seconds.");
            Assert.That(SEntMan.GetComponent<CMUVehicleCrashKnockoutComponent>(jeep).Horn, Is.Not.Null);
        });

        await RunSeconds(4f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<RMCUnconsciousComponent>(driver), Is.False, "Back up within five seconds.");
            Assert.That(SEntMan.GetComponent<CMUVehicleCrashKnockoutComponent>(jeep).Horn, Is.Null, "The horn stops when the driver comes to.");
        });
    }

    [Test]
    public async Task HardCrashBlacksTheDriverOutForAMoment()
    {
        var (jeep, driver) = await SeatedJeep();

        await Server.WaitPost(() =>
        {
            var knockout = SEntMan.GetComponent<CMUVehicleCrashKnockoutComponent>(jeep);
            knockout.MinChance = 0f;
            Crash(jeep, true, 0.7f);
            Assert.That(SEntMan.HasComponent<RMCUnconsciousComponent>(driver), Is.True, "A hard crash blacks the driver out.");
            Assert.That(knockout.Horn, Is.Null, "A blackout doesn't hold the horn down.");
        });

        await RunSeconds(3f);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.HasComponent<RMCUnconsciousComponent>(driver), Is.False, "Only for a moment."));
    }

    [Test]
    public async Task GunAndHornSoundsLoad()
    {
        await Server.WaitAssertion(() =>
        {
            var prototypes = Server.ResolveDependency<IPrototypeManager>();
            var resources = Server.ResolveDependency<IResourceManager>();
            var factory = Server.ResolveDependency<IComponentFactory>();

            var gun = prototypes.Index<EntityPrototype>("CMUJeepMG");
            Assert.That(gun.TryGetComponent(out CMUGunDistantSoundComponent? distant, factory), Is.True);
            var jeep = prototypes.Index<EntityPrototype>("CMUVehicleJeepCargo");
            Assert.That(jeep.TryGetComponent(out CMUVehicleCrashKnockoutComponent? knockout, factory), Is.True);

            foreach (var path in new[]
                     {
                         "/Audio/CMU14/Jeep/jeep_mg_close.wav", "/Audio/CMU14/Jeep/jeep_mg_far.wav",
                         "/Audio/CMU14/Jeep/jeep_horn.wav", "/Audio/CMU14/Jeep/jeep_horn_long.wav",
                     })
            {
                Assert.That(resources.ContentFileExists(new ResPath(path)), Is.True, path);
            }
        });
    }
}
