using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Vehicle;

namespace Content.IntegrationTests.CMU14.Vehicle;

[TestFixture]
public sealed class CMUSnowplowFaultTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [TestCase("VehicleTankSnowplow")]
    [TestCase("VehicleTankSnowplowTWE")]
    [TestCase("VehicleHumveeSnowplow")]
    public async Task SevereHitsDamageSnowplowsWithoutFaultingThem(string prototype)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var frame = SEntMan.SpawnEntity(null, map.GridCoords);
            var frameIntegrity = SEntMan.AddComponent<HardpointIntegrityComponent>(frame);
            var plow = SEntMan.SpawnEntity(prototype, map.GridCoords);
            var integrity = SEntMan.GetComponent<HardpointIntegrityComponent>(plow);
            integrity.Integrity = integrity.MaxIntegrity * 0.5f;
            integrity.FailureChance = 1f;
            var before = integrity.Integrity;
            for (var i = 0; i < 3; i++)
            {
                frameIntegrity.NextFailureRoll = TimeSpan.Zero;
                Server.System<HardpointSystem>().DamageHardpoint(frame, plow, integrity.MaxIntegrity * 0.05f);
            }
            Assert.That(integrity.Integrity, Is.LessThan(before));
            Assert.That(SEntMan.HasComponent<VehicleHardpointFailureComponent>(plow), Is.False,
                "snowplows take damage but must never develop mechanical faults");
        });
    }
}
