using Stopwatch = System.Diagnostics.Stopwatch;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Chemistry.Effects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Chemistry;

[TestFixture]
public sealed class CMUChemicalAddictionDeadlineTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task StaggeredDosesAndRedosingKeepIndependentOnsetDeadlines()
    {
        await Server.WaitAssertion(() =>
        {
            var system = SEntMan.System<ChemicalAddictionSystem>();
            var first = SSpawn(null);
            var second = SSpawn(null);
            // A dose must wake an existing empty component after the population became idle.
            SEntMan.AddComponent<ChemicalAddictionComponent>(first);
            var start = SGameTiming.CurTime;
            system.Update(0);
            system.AddOrSatisfy(first, "First");
            var early = SComp<ChemicalAddictionComponent>(first).Addictions["First"];

            At(start + TimeSpan.FromSeconds(60), () =>
            {
                system.AddOrSatisfy(second, "Second");
                system.Update(0);
            });
            var late = SComp<ChemicalAddictionComponent>(second).Addictions["Second"];

            At(start + TimeSpan.FromSeconds(299), () => system.Update(0));
            Assert.That(early.Craving, Is.False);
            Assert.That(late.Craving, Is.False);

            At(start + TimeSpan.FromSeconds(300), () => system.Update(0));
            Assert.That(early.Craving, Is.True);
            Assert.That(late.Craving, Is.False);

            At(start + TimeSpan.FromSeconds(301), () => system.AddOrSatisfy(first, "First"));
            Assert.That(early.Craving, Is.False);

            At(start + TimeSpan.FromSeconds(360), () => system.Update(0));
            Assert.That(early.Craving, Is.False);
            Assert.That(late.Craving, Is.True);

            At(start + TimeSpan.FromSeconds(600), () => system.Update(0));
            Assert.That(early.Craving, Is.False);
            At(start + TimeSpan.FromSeconds(601), () => system.Update(0));
            Assert.That(early.Craving, Is.True);
            Assert.That(early.NextMessage, Is.EqualTo(start + TimeSpan.FromSeconds(691)),
                "A delayed update sends one reminder and skips missed intervals.");
        });
    }

    [Test]
    public async Task UnpauseLoadedEntriesAndRemovalWakeTheRemainingTimers()
    {
        await Server.WaitAssertion(() =>
        {
            var system = SEntMan.System<ChemicalAddictionSystem>();
            var metadata = SEntMan.System<MetaDataSystem>();
            var paused = SSpawn(null);
            var other = SSpawn(null);
            var start = SGameTiming.CurTime;
            system.AddOrSatisfy(paused, "Paused");
            system.AddOrSatisfy(other, "Other");
            metadata.SetEntityPaused(paused, true);
            system.Update(0);

            At(start + TimeSpan.FromSeconds(300), () => system.Update(0));
            Assert.That(SComp<ChemicalAddictionComponent>(paused).Addictions["Paused"].Craving, Is.False);
            Assert.That(SComp<ChemicalAddictionComponent>(other).Addictions["Other"].Craving, Is.True);

            At(start + TimeSpan.FromSeconds(301), () =>
            {
                metadata.SetEntityPaused(paused, false);
                system.Update(0);
                Assert.That(SComp<ChemicalAddictionComponent>(paused).Addictions["Paused"].Craving, Is.True,
                    "These existing absolute timestamps keep counting while the entity is paused.");
                SEntMan.RemoveComponent<ChemicalAddictionComponent>(paused);
                system.Update(0);
                // Simulate a loaded component whose withdrawal deadline already passed.
                var loaded = SEntMan.AddComponent<ChemicalAddictionComponent>(paused);
                loaded.Addictions["Loaded"] = new ChemicalAddictionEntry
                {
                    LastDose = start,
                    NextMessage = start + TimeSpan.FromSeconds(300),
                };
                system.Update(0);
                Assert.That(loaded.Addictions["Loaded"].Craving, Is.True);
                SEntMan.RemoveComponent<ChemicalAddictionComponent>(paused);
                system.Update(0);
            });

            At(start + TimeSpan.FromSeconds(390), () => system.Update(0));
            Assert.That(SComp<ChemicalAddictionComponent>(other).Addictions["Other"].NextMessage,
                Is.EqualTo(start + TimeSpan.FromSeconds(480)));
        });
    }

    [Test]
    public async Task IdleFiveHundredEntitiesKeepTheirDeadlinesWithoutAllocating()
    {
        await Server.WaitAssertion(() =>
        {
            var system = SEntMan.System<ChemicalAddictionSystem>();
            var entries = new List<(ChemicalAddictionEntry Entry, TimeSpan Deadline)>();
            for (var i = 0; i < 500; i++)
            {
                var uid = SSpawn(null);
                system.AddOrSatisfy(uid, "Chemical");
                var entry = SComp<ChemicalAddictionComponent>(uid).Addictions["Chemical"];
                entries.Add((entry, entry.NextMessage));
            }

            for (var i = 0; i < 8; i++)
                system.Update(0);
            var before = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            for (var i = 0; i < 4096; i++)
                system.Update(0);
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            TestContext.Out.WriteLine($"CMU_CHEMICAL_ADDICTION population=500 idleUpdates=4096 ms={elapsed:F4} allocated={allocated}");
            Assert.That(allocated, Is.LessThan(1024));
            foreach (var (entry, deadline) in entries)
            {
                Assert.That(entry.Craving, Is.False);
                Assert.That(entry.NextMessage, Is.EqualTo(deadline));
            }
        });
    }

    private void At(TimeSpan time, Action action)
    {
        // Only this subsystem runs at the synthetic time; the dirty pair is discarded afterwards.
        var original = SGameTiming.TimeBase;
        SGameTiming.TimeBase = (time - (SGameTiming.InSimulation ? TimeSpan.Zero : SGameTiming.TickRemainder), SGameTiming.CurTick);
        try
        {
            action();
        }
        finally
        {
            SGameTiming.TimeBase = original;
        }
    }
}
