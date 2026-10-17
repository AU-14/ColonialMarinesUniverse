#pragma warning disable RA0002 // Exercise deadline edits without going through gameplay helpers.

using Content.IntegrationTests.Fixtures;
using Content.Shared.Metabolism;

namespace Content.IntegrationTests.Tests.Performance;

[TestFixture]
public sealed class MetabolizerScheduleTest : GameTest
{
    [Test]
    public async Task EditsReorderDeadlinesAndOverdueOrgansOnlyRunOncePerUpdate()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var system = Server.System<MetabolizerSystem>();
            var now = SGameTiming.CurTime;
            var entities = new List<EntityUid>();
            for (var i = 0; i < 100; i++)
            {
                var uid = SEntMan.SpawnEntity(null, map.GridCoords);
                entities.Add(uid);
                var comp = SEntMan.AddComponent<MetabolizerComponent>(uid);
                comp.Stages.Clear();
                comp.NextUpdate = now + TimeSpan.FromHours(1);
            }
            var due = SEntMan.GetComponent<MetabolizerComponent>(entities[50]);
            due.NextUpdate = now - TimeSpan.FromSeconds(10);
            var before = due.NextUpdate;
            system.Update(0);
            Assert.That(due.NextUpdate, Is.EqualTo(before + due.AdjustedUpdateInterval));
            Assert.That(SEntMan.GetComponent<MetabolizerComponent>(entities[49]).NextUpdate,
                Is.EqualTo(now + TimeSpan.FromHours(1)));

            due.NextUpdate = now + TimeSpan.FromHours(2);
            system.Update(0);
            Assert.That(due.NextUpdate, Is.EqualTo(now + TimeSpan.FromHours(2)));
            SEntMan.RemoveComponent<MetabolizerComponent>(entities[50]);
            var replacement = SEntMan.AddComponent<MetabolizerComponent>(entities[50]);
            replacement.Stages.Clear();
            replacement.NextUpdate = now;
            system.Update(0);
            Assert.That(replacement.NextUpdate, Is.EqualTo(now + replacement.AdjustedUpdateInterval));
            Assert.That(due.NextUpdate, Is.EqualTo(now + TimeSpan.FromHours(2)));
        });
    }
}
