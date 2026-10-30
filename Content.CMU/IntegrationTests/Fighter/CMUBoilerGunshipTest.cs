using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared.CMU14.Dropship.Integrity;
using Content.Shared.CMU14.Dropship.TacticalLand;
using Content.Shared.CMU14.Fighter;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Fighter;

[TestFixture]
public sealed class CMUBoilerGunshipTest : GameTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task AirCoverDamagesFlyingGunshipButCancelsAcquisitionWhenItLands(bool landDuringWindup)
    {
        var map = await Pair.CreateTestMap();
        EntityUid sky = default;
        EntityUid ship = default;
        EntityUid boiler = default;
        await Server.WaitAssertion(() =>
        {
            sky = SEntMan.System<SharedMapSystem>().CreateMap(out _, runMapInit: true);
            var position = SEntMan.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords).Position;
            ship = SEntMan.SpawnEntity(null, new EntityCoordinates(sky, position));
            SEntMan.EnsureComponent<DropshipIntegrityComponent>(ship);
            SEntMan.EnsureComponent<DropshipTacticalHoverComponent>(ship).GroundMap = map.MapUid;
            boiler = SEntMan.SpawnEntity("RMCXenoBoiler", map.GridCoords);
            var air = SEntMan.GetComponent<FighterBoilerAirDefenseComponent>(boiler);
            air.AcquisitionTime = TimeSpan.FromSeconds(0.5);
            SEntMan.System<XenoPlasmaSystem>().SetPlasma((boiler, SEntMan.GetComponent<XenoPlasmaComponent>(boiler)), 800);
            SEntMan.System<FighterBoilerAirDefenseSystem>().SetWatching((boiler, air), true);
        });
        await RunSeconds(0.2f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<FighterBoilerAirDefenseComponent>(boiler).Target, Is.EqualTo(ship),
                "The Boiler must acquire the gunship on the sky map above its sector.");
            Assert.That(SEntMan.GetComponent<DropshipIntegrityComponent>(ship).Integrity, Is.EqualTo(2000));
            if (landDuringWindup)
                SEntMan.RemoveComponent<DropshipTacticalHoverComponent>(ship);
        });
        await RunSeconds(0.6f);
        await Server.WaitAssertion(() =>
        {
            var air = SEntMan.GetComponent<FighterBoilerAirDefenseComponent>(boiler);
            if (landDuringWindup)
                Assert.That(air.ReadyAt, Is.EqualTo(TimeSpan.Zero), "Cancelled acquisition must not spend the cooldown.");
            else
                Assert.That(air.ReadyAt, Is.GreaterThan(SGameTiming.CurTime), "A launched shot spends the cooldown.");
            SEntMan.System<FighterBoilerAirDefenseSystem>().SetWatching((boiler, air), false);
        });
        await RunSeconds(5.5f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<DropshipIntegrityComponent>(ship).Integrity,
                Is.EqualTo(landDuringWindup ? 2000 : 1200),
                "A launched shot deals 800 damage even after the Boiler stops watching; a cancelled shot deals none.");
            SEntMan.DeleteEntity(sky);
        });
    }
}
