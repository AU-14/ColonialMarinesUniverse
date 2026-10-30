using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Expeditions;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.OnCollide;

namespace Content.IntegrationTests.CMU14.Expeditions;

[TestFixture]
public sealed class CMUAgentHazardSnapshotTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Dirty = true };

    [Test]
    public async Task RoutesSeeHazardsOnEachMapAndRefreshAfterRemoval()
    {
        var first = await Pair.CreateTestMap();
        var second = await Pair.CreateTestMap();
        EntityUid fire = default;
        EntityUid acid = default;
        await Server.WaitAssertion(() =>
        {
            fire = SEntMan.SpawnEntity(null, first.GridCoords);
            SEntMan.AddComponent<RMCIgniteOnCollideComponent>(fire);
            Assert.That(Safe(first.GridCoords), Is.False);
            Assert.That(Safe(second.GridCoords), Is.True, "A fire on another map cannot block this route.");
        });
        await Server.WaitPost(() =>
        {
            SEntMan.DeleteEntity(fire);
            acid = SEntMan.SpawnEntity(null, second.GridCoords);
#pragma warning disable RA0002 // Fixture creates the same acidic collision sensor as a spray splatter.
            SEntMan.AddComponent<DamageOnCollideComponent>(acid).Acidic = true;
#pragma warning restore RA0002
        });
        await Pair.RunTicksSync(1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Safe(first.GridCoords), Is.True, "Extinguished fire must leave the next snapshot.");
            Assert.That(Safe(second.GridCoords), Is.False, "New acid must enter the next snapshot.");
            SEntMan.DeleteEntity(acid);
        });
        await Pair.RunTicksSync(1);
        await Server.WaitAssertion(() => Assert.That(Safe(second.GridCoords), Is.True));

        bool Safe(EntityCoordinates point) => (bool) typeof(CMUExpeditionAgentSystem)
            .GetMethod("GroundSafe", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(Server.System<CMUExpeditionAgentSystem>(), new object[] { point })!;
    }
}
