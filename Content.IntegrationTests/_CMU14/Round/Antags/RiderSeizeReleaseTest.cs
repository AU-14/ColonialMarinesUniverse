#nullable enable
using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Round.Antags.Rider;
using Content.Shared.Actions;
using Content.Shared.CMU14.Round.Antags.Rider;
using Content.Shared.Mind;
using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Round.Antags;

// a rider mid-seize has to be able to hand the body back before the timer runs out
[TestFixture]
public sealed class RiderSeizeReleaseTest : GameTest
{
    [Test]
    public async Task RiderCanEndSeizeEarlyFromTheHostBody()
    {
        var map = await Pair.CreateTestMap();
        EntityUid rider = default;
        EntityUid host = default;

        await Server.WaitPost(() =>
        {
            var minds = SEntMan.System<SharedMindSystem>();
            host = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            rider = SEntMan.SpawnEntity("CMURiderHatchling", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));

            var riderMind = minds.GetOrCreateMind(Pair.Player!.UserId).Owner;
            minds.TransferTo(riderMind, rider);
            var hostMind = minds.CreateMind(null, "host");
            minds.TransferTo(hostMind, host);

            var system = SEntMan.System<RiderSystem>();
            var latch = typeof(RiderSystem).GetMethod("LatchOnto", BindingFlags.Instance | BindingFlags.NonPublic)!;
            latch.Invoke(system, [new Entity<RiderComponent>(rider, SComp<RiderComponent>(rider)), host, true]);

            var comp = SComp<RiderComponent>(rider);
            Assert.That(comp.Host, Is.EqualTo(host), "latch didn't take");
            SEntMan.System<SharedActionsSystem>().PerformAction(rider, SEntMan.System<SharedActionsSystem>().GetAction(comp.SeizeAction)!.Value);
            Assert.That(comp.SeizeActive, Is.True, "seize didn't start");
        });

        await RunTicksSync(10);

        NetEntity exitNet = default;
        await Server.WaitAssertion(() =>
        {
            var exit = SComp<RiderComponent>(rider).SeizeExitAction;
            Assert.That(exit, Is.Not.Null, "host has no way to end the seize");
            // the old button was a copy of "Abandon Host", which reads like it throws you out, so nobody pressed it
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(exit!.Value).EntityPrototype?.ID,
                Is.EqualTo("ActionRiderSeizeRelease"));
            exitNet = SEntMan.GetNetEntity(exit.Value);
        });

        await Client.WaitPost(() =>
        {
            var attached = Client.ResolveDependency<IPlayerManager>().LocalEntity;
            Assert.That(attached, Is.EqualTo(ToClientUid(host)), "player should be driving the host body");
            CEntMan.RaisePredictiveEvent(new RequestPerformActionEvent(exitNet, Client.ResolveDependency<IGameTiming>().CurTick));
        });

        await RunTicksSync(10);

        await Server.WaitAssertion(() =>
        {
            var comp = SComp<RiderComponent>(rider);
            Assert.That(comp.SeizeActive, Is.False, "pressing the exit button mid-seize should hand the body back");
            Assert.That(comp.Host, Is.EqualTo(host), "ending a seize shouldn't throw the rider out of the host");
        });
    }
}
