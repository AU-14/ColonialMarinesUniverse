using Content.Server.Medical.SuitSensors;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.Medical.SuitSensors;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Medical;

[TestFixture]
[TestOf(typeof(SuitSensorSystem))]
public sealed class SuitSensorRetryThrottleTest
{
    [Test]
    public async Task UnassignedSensorSchedulesNextRetry()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var sensors = server.System<SuitSensorSystem>();
        var timing = server.ResolveDependency<IGameTiming>();

        await server.WaitAssertion(() =>
        {
            var uid = entMan.SpawnEntity(
                "ClothingUniformJumpsuitColorWhite",
                MapCoordinates.Nullspace);
            var sensor = entMan.GetComponent<SuitSensorComponent>(uid);
            var device = entMan.GetComponent<DeviceNetworkComponent>(uid);

            Assert.Multiple(() =>
            {
                Assert.That(device.TransmitFrequency, Is.Not.Null);
                Assert.That(sensor.StationId, Is.Null);
                Assert.That(sensor.NextUpdate, Is.EqualTo(TimeSpan.Zero));
            });

            var expected = timing.CurTime + sensor.UpdateRate;

            sensors.Update(0f);
            Assert.Multiple(() =>
            {
                Assert.That(sensor.StationId, Is.Null);
                Assert.That(sensor.NextUpdate, Is.EqualTo(expected));
            });

            // The immediate second pass must be throttled.
            sensors.Update(0f);
            Assert.That(sensor.NextUpdate, Is.EqualTo(expected));
        });

        await pair.CleanReturnAsync();
    }
}
