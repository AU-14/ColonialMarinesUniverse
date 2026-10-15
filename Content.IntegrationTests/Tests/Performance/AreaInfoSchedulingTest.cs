#pragma warning disable RA0002 // Controlled component data changes verify scheduling and live permissions.

using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.Alert;
using Content.Shared.Timing;

namespace Content.IntegrationTests.Tests.Performance;

[TestFixture]
public sealed class AreaInfoSchedulingTest : GameTest
{
    [Test]
    public async Task ReschedulingMovesBothEarlierAndLaterWithoutDuplicatingWork()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var system = Server.System<AreaInfoSystem>();
            var uid = SEntMan.SpawnEntity(null, map.GridCoords);
            var area = SEntMan.AddComponent<AreaInfoComponent>(uid);
            var pending = Pending(system);
            var now = SGameTiming.CurTime;
            system.SetNextUpdateTime((uid, area), now + TimeSpan.FromHours(1));
            system.Update(0);
            Assert.That(pending, Is.Empty);
            system.SetNextUpdateTime((uid, area), now);
            system.SetNextUpdateTime((uid, area), now - TimeSpan.FromSeconds(1));
            system.Update(0);
            Assert.That(pending.Select(e => e.Owner), Is.EqualTo(new[] { uid }));
            Assert.That(area.NextUpdateTime, Is.EqualTo(now + area.UpdateInterval));

            // A changed deadline must not drop work already queued by an earlier wave.
            system.SetNextUpdateTime((uid, area), now + TimeSpan.FromHours(1));
            system.Update(0);
            Assert.That(pending, Is.Empty);
            Assert.That(Deadlines(system).Count, Is.EqualTo(1));
            SEntMan.DeleteEntity(uid);
            Assert.That(Deadlines(system).Count, Is.Zero);
        });
    }

    [Test]
    public async Task PausingRemovalAndReplacementMaintainTheSchedule()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var system = Server.System<AreaInfoSystem>();
            var metadata = Server.System<MetaDataSystem>();
            var uid = SEntMan.SpawnEntity(null, map.GridCoords);
            metadata.SetEntityPaused(uid, true);
            var area = SEntMan.AddComponent<AreaInfoComponent>(uid);
            system.SetNextUpdateTime((uid, area), SGameTiming.CurTime);
            system.Update(0);
            Assert.That(Deadlines(system).Count, Is.Zero);
            Assert.That(Pending(system), Is.Empty);

            metadata.SetEntityPaused(uid, false);
            Assert.That(Deadlines(system).Count, Is.EqualTo(1));
            metadata.SetEntityPaused(uid, true);
            Assert.That(Deadlines(system).Count, Is.Zero);
            metadata.SetEntityPaused(uid, false);
            system.Update(0);
            Assert.That(Pending(system), Has.Count.EqualTo(1), "Overdue work becomes eligible on unpause.");
            SEntMan.RemoveComponent<AreaInfoComponent>(uid);
            Assert.That(Deadlines(system).Count, Is.Zero);
            var replacement = SEntMan.AddComponent<AreaInfoComponent>(uid);
            system.SetNextUpdateTime((uid, replacement), SGameTiming.CurTime);
            system.Update(0);
            Assert.That(Pending(system).Single().Comp, Is.SameAs(replacement));
            Assert.That(Deadlines(system).Count, Is.EqualTo(1));
            SEntMan.DeleteEntity(uid);
            system.Update(0);
            Assert.That(Pending(system), Is.Empty);
            Assert.That(Deadlines(system).Count, Is.Zero);
        });
    }

    [TestCase(0)]
    [TestCase(-1)]
    public async Task NonPositiveIntervalsProduceOnlyOneWavePerUpdate(int seconds)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var system = Server.System<AreaInfoSystem>();
            var uid = SEntMan.SpawnEntity(null, map.GridCoords);
            var area = SEntMan.AddComponent<AreaInfoComponent>(uid);
            area.UpdateInterval = TimeSpan.FromSeconds(seconds);
            for (var i = 0; i < 4; i++)
            {
                system.Update(0);
                Assert.That(Pending(system), Has.Count.EqualTo(1));
                Assert.That(Deadlines(system).Count, Is.EqualTo(1));
                Assert.That(area.NextUpdateTime, Is.EqualTo(SGameTiming.CurTime + area.UpdateInterval));
            }
            SEntMan.DeleteEntity(uid);
            system.Update(0);
            Assert.That(Pending(system), Is.Empty);
        });
    }

    [Test]
    public async Task IdleUpdatesDoNotScanOrAllocatePerScheduledEntity()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            const int population = 512;
            const int iterations = 100;
            var system = Server.System<AreaInfoSystem>();
            var entities = new List<EntityUid>();
            for (var i = 0; i < population; i++)
            {
                var uid = SEntMan.SpawnEntity(null, map.GridCoords);
                entities.Add(uid);
                var area = SEntMan.AddComponent<AreaInfoComponent>(uid);
                system.SetNextUpdateTime((uid, area), SGameTiming.CurTime + TimeSpan.FromHours(1));
            }
            for (var i = 0; i < 100; i++) system.Update(0);
            var beforeBytes = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < iterations; i++) system.Update(0);
            var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - beforeBytes;
            Assert.That(allocatedBytes, Is.Zero, "Future deadlines must not allocate during idle updates.");
            Assert.That(Pending(system), Is.Empty);
            Assert.That(Deadlines(system).Count, Is.EqualTo(population));

            foreach (var uid in entities) SEntMan.DeleteEntity(uid);
            Assert.That(Deadlines(system).Count, Is.Zero);
        });
    }

    [Test]
    public async Task RestrictionTextIsReusedButLivePermissionsAndMissingAreasStillRefresh()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var system = Server.System<AreaInfoSystem>();
            var areas = Server.System<AreaSystem>();
            var grid = SEntMan.AddComponent<AreaGridComponent>(map.Grid.Owner);
            areas.ReplaceArea(grid, Vector2i.Zero, "RMCAreaBosenmoriBashoOob");
            var areaEntity = grid.AreaEntities.Values.Single();
            var permissions = SEntMan.GetComponent<AreaComponent>(areaEntity);
            var uid = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.AddComponent<AlertsComponent>(uid);
            var area = SEntMan.AddComponent<AreaInfoComponent>(uid);
            void Refresh()
            {
                system.SetNextUpdateTime((uid, area), SGameTiming.CurTime);
                system.Update(0);
                system.Update(0);
            }
            Refresh();
            var initialText = area.LastRestrictionText;
            var initialMessage = area.LastMessage;
            Assert.That(initialText, Does.Contain("Tunneling"));
            Refresh();
            Assert.That(area.LastRestrictionText, Is.SameAs(initialText));
            Assert.That(area.LastMessage, Is.SameAs(initialMessage));
            var alerts = Server.System<AlertsSystem>();
            alerts.ClearAlert((uid, null), area.Alert);
            Refresh();
            Assert.That(alerts.IsShowingAlert((uid, null), area.Alert), Is.True);
            Assert.That(area.LastRestrictionText, Is.SameAs(initialText));

            permissions.Medevac = true;
            permissions.NoTunnel = false;
            Refresh();
            Assert.That(area.LastRestrictionText, Is.Not.SameAs(initialText));
            Assert.That(area.LastRestrictionText, Does.Contain("Allowed:\n• Casualty Evacuation"));
            Assert.That(area.LastRestrictionText, Does.Not.Contain("Tunneling"));
            var changedText = area.LastRestrictionText;
            areas.RemoveArea(grid, Vector2i.Zero);
            Refresh();
            Assert.That(area.LastPresentation!.Value.Restrictions, Is.Empty);
            areas.ReplaceArea(grid, Vector2i.Zero, "RMCAreaBosenmoriBashoOob");
            Refresh();
            Assert.That(area.LastPresentation!.Value.Restrictions, Is.SameAs(changedText));
            SEntMan.DeleteEntity(uid);
            // Remove the networked references before deleting their globally visible area.
            SEntMan.RemoveComponent<AreaGridComponent>(map.Grid.Owner);
            SEntMan.DeleteEntity(areaEntity);
        });
    }

    [Test]
    public async Task ClientDoesNotCreatePeriodicServerWork()
    {
        var map = await Pair.CreateTestMap();
        await Client.WaitAssertion(() =>
        {
            var system = Client.System<AreaInfoSystem>();
            var uid = CEntMan.SpawnEntity(null, map.CGridCoords);
            var area = CEntMan.AddComponent<AreaInfoComponent>(uid);
            system.SetNextUpdateTime((uid, area), TimeSpan.Zero);
            system.Update(0);
            Assert.That(Deadlines(system).Count, Is.Zero);
            Assert.That(Pending(system), Is.Empty);
            CEntMan.DeleteEntity(uid);
        });
    }

    private static Queue<Entity<AreaInfoComponent>> Pending(AreaInfoSystem system) =>
        Field<Queue<Entity<AreaInfoComponent>>>(system, "_marineAlertCopyQueue");

    private static DeadlineQueue<Entity<AreaInfoComponent>> Deadlines(AreaInfoSystem system) =>
        Field<DeadlineQueue<Entity<AreaInfoComponent>>>(system, "_refreshDeadlines");

    private static T Field<T>(AreaInfoSystem system, string name) =>
        (T) typeof(AreaInfoSystem).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(system)!;
}
