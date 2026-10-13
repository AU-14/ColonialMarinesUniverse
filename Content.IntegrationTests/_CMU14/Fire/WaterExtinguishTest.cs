using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.Atmos;
using Content.Shared.Atmos.Components;
using ServerFlammableSystem = Content.Server.Atmos.EntitySystems.FlammableSystem;

namespace Content.IntegrationTests._CMU14.Fire;

[TestFixture]
public sealed class WaterExtinguishTest : GameTest
{
    [Test]
    public async Task BurningMobOnWaterIsExtinguishedWithoutResisting()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            SEntMan.SpawnEntity("CMFloorShallowWaterEntity", map.GridCoords);
            var human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var flammable = SEntMan.GetComponent<FlammableComponent>(human);
            Assert.That(Server.System<RMCFlammableSystem>().Ignite((human, flammable), 10, 10, 10), Is.True);
            flammable.NextUpdate = SGameTiming.CurTime;

            var system = Server.System<ServerFlammableSystem>();
            system.RefreshUpdateSnapshot();
            system.Update(0f);

            Assert.That(flammable.OnFire, Is.False, "standing in water must put the fire out on the next fire update");
        });
    }

    [Test]
    public async Task TileFireDoesNotBurnOnWater()
    {
        var map = await Pair.CreateTestMap();
        EntityUid wet = default;
        EntityUid dry = default;
        await Server.WaitAssertion(() =>
        {
            SEntMan.SpawnEntity("CMFloorShallowWaterEntity", map.GridCoords);
            wet = SEntMan.SpawnEntity("RMCTileFire", map.GridCoords);
            dry = SEntMan.SpawnEntity("RMCTileFire", map.GridCoords.Offset(new Vector2(3, 0)));
        });

        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(wet), Is.True, "flames on water must be put out");
            Assert.That(SEntMan.Deleted(dry), Is.False, "flames off water must keep burning");
        });
    }
}
