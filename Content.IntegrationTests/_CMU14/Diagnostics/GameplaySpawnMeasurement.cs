using System.Diagnostics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Station.Systems;
using Content.Server._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.Preferences;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture, Explicit("Controlled measurement of the gameplay spawn workloads seen in server incidents.")]
public sealed class GameplaySpawnMeasurement : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task MeasureGameplaySpawns()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var spawning = Server.System<StationSpawningSystem>();
            Measure("physician", () => spawning.SpawnPlayerMob(map.GridCoords,
                "AU14JobCivilianPhysician", HumanoidCharacterProfile.DefaultWithSpecies("Human"), null), 8);
            Measure("flare-batch", () =>
            {
                for (var i = 0; i < 40; i++)
                    SEntMan.SpawnEntity("CMPackFlare", map.GridCoords);
            }, 8);
        });
    }

    [Test]
    public async Task MeasureDeliveryUpdates()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var system = Server.System<RequisitionsSystem>();
            var timing = Server.ResolveDependency<Robust.Shared.Timing.IGameTiming>();
            for (var iteration = 0; iteration < 8; iteration++)
            {
                var elevator = SEntMan.SpawnEntity("CMCargoElevator", map.GridCoords);
                var component = SEntMan.GetComponent<RequisitionsElevatorComponent>(elevator);
                component.RoundStartFreeCrateGiven = true;
                component.Orders.Add(new RequisitionsEntry
                {
                    Crate = "CMUASRSShipmentCrate",
                    Entities = Enumerable.Repeat<Robust.Shared.Prototypes.EntProtoId>("CMPackFlare", 40).ToList(),
                });
                component.Mode = RequisitionsElevatorMode.Raising;
                component.Busy = true;
                component.RaiseDelay = TimeSpan.Zero;
                component.LowerDelay = TimeSpan.Zero;
                component.ToggledAt = timing.CurTime - TimeSpan.FromSeconds(1);
                var maxMs = 0d;
                var totalMs = 0d;
                var maxBytes = 0L;
                var steps = 0;
                while (component.Mode != RequisitionsElevatorMode.Raised && steps++ < 200)
                {
                    var allocated = GC.GetAllocatedBytesForCurrentThread();
                    var start = Stopwatch.GetTimestamp();
                    system.Update(0f);
                    var ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                    maxBytes = Math.Max(maxBytes, GC.GetAllocatedBytesForCurrentThread() - allocated);
                    maxMs = Math.Max(maxMs, ms);
                    totalMs += ms;
                }
                Assert.That(component.Mode, Is.EqualTo(RequisitionsElevatorMode.Raised));
                TestContext.Progress.WriteLine($"workload=delivery iteration={iteration} steps={steps} maxMs={maxMs:F3} totalMs={totalMs:F3} maxBytes={maxBytes}");
                SEntMan.DeleteEntity(elevator);
            }
        });
    }

    private static void Measure(string name, Action action, int repetitions)
    {
        for (var i = 0; i < repetitions; i++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            action();
            var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            TestContext.Out.WriteLine($"workload={name} iteration={i} milliseconds={elapsed:F3} allocatedBytes={allocated}");
        }
    }
}
