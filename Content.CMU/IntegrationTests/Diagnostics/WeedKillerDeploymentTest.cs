#pragma warning disable RA0002 // Seed the deployment deadline and geometry without waiting for a shuttle flight.

using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.WeedKiller;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Diagnostics;

public sealed class WeedKillerDeploymentTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task DeploymentDestroysWeedsAndExpiresAreaSuppressionWithoutSmokeEntities()
    {
        var map = await Pair.CreateTestMap();
        EntityUid weed = default;
        EntityUid untouched = default;
        EntityUid killer = default;
        EntityUid area = default;
        await Server.WaitAssertion(() =>
        {
            var transform = SEntMan.System<SharedTransformSystem>();
            weed = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.AddComponent<DeletedByWeedKillerComponent>(weed);
            Assert.That(transform.AnchorEntity(weed), Is.True);
            untouched = SEntMan.SpawnEntity(null, map.GridCoords);
            Assert.That(transform.AnchorEntity(untouched), Is.True);
            area = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.AddComponent<AreaComponent>(area);
            killer = SEntMan.SpawnEntity(null, map.GridCoords);
            var component = SEntMan.AddComponent<WeedKillerComponent>(killer);
            component.LinkedAreas.Add(area);
            var grid = transform.GetGrid(map.GridCoords)!.Value;
            component.Positions.Add(((grid, SEntMan.GetComponent<MapGridComponent>(grid)), Vector2i.Zero));
            component.DisableAt = TimeSpan.MaxValue;

            SEntMan.System<WeedKillerSystem>().Update(0);
            Assert.That(SEntMan.GetComponent<AreaComponent>(area).WeedKilling, Is.True);
            Assert.That(SEntMan.EntityQuery<MetaDataComponent>().Any(meta => meta.EntityPrototype?.ID == "RMCGasWeedKiller"), Is.False);
            Assert.That(component.Positions, Is.Empty, "completed work must release grid references");
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(weed), Is.True);
            Assert.That(SEntMan.Deleted(untouched), Is.False, "ordinary anchored objects must survive weedkiller");
            var component = SEntMan.GetComponent<WeedKillerComponent>(killer);
            component.DisableAt = Server.ResolveDependency<IGameTiming>().CurTime;
            SEntMan.System<WeedKillerSystem>().Update(0);
            Assert.That(SEntMan.GetComponent<AreaComponent>(area).WeedKilling, Is.False);
        });
    }

    [Test]
    public async Task SmokeArrivesAsClientCosmeticsAndExpires()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var observer = SEntMan.SpawnEntity("MobObserver", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, observer);
        });
        await Pair.RunUntilSynced();
        await Server.WaitAssertion(() =>
        {
            var killer = SEntMan.SpawnEntity(null, map.GridCoords);
            var component = SEntMan.AddComponent<WeedKillerComponent>(killer);
            component.Positions.Add((map.Grid, Vector2i.Zero));
            component.DisableAt = TimeSpan.MaxValue;
            SEntMan.System<WeedKillerSystem>().Update(0);
            Assert.That(SEntMan.EntityQuery<MetaDataComponent>().Any(meta => meta.EntityPrototype?.ID == "RMCGasWeedKiller"), Is.False);
        });
        await Pair.RunTicksSync(10);
        await Client.WaitAssertion(() =>
            Assert.That(CEntMan.EntityQuery<MetaDataComponent>().Count(meta => meta.EntityPrototype?.ID == "RMCGasWeedKiller"), Is.EqualTo(1)));
        await RunSeconds(2);
        await Client.WaitAssertion(() =>
            Assert.That(CEntMan.EntityQuery<MetaDataComponent>().Any(meta => meta.EntityPrototype?.ID == "RMCGasWeedKiller"), Is.False));
    }
}
