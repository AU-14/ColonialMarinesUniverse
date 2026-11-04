using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Expeditions;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class CMUSquadMemberLifetimeTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task RemovingASquadMemberReleasesItsReservedBlastAreaImmediately()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var walker = SEntMan.SpawnEntity("CMUExpeditionScavenger", map.GridCoords);
            var thrower = SEntMan.SpawnEntity("CMUExpeditionScavenger", map.GridCoords.Offset(new Vector2(0, 2)));
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(walker);
            var buddy = SEntMan.GetComponent<CMUExpeditionAgentComponent>(thrower);
            agent.Squad = buddy.Squad = 1;
            var system = Server.System<CMUExpeditionAgentSystem>();
            var now = SGameTiming.CurTime;
            var methods = BindingFlags.Instance | BindingFlags.NonPublic;
            // Build the same cached plan used between the one-second squad refreshes.
            typeof(CMUExpeditionAgentSystem).GetMethod("UpdateSquadPlans", methods)!.Invoke(system, new object[] { now });
            agent.LastHit = now - TimeSpan.FromSeconds(10);
            agent.CoverDestination = map.GridCoords.Offset(new Vector2(10, 0));
            buddy.SmokeGrenade = false;
            buddy.GrenadeTarget = map.GridCoords.Offset(new Vector2(3, 0));
            buddy.GrenadeReservationUntil = now + TimeSpan.FromSeconds(10);
            var wait = typeof(CMUExpeditionAgentSystem).GetMethod("WaitForSquadOrdnance", methods)!;
            Assert.That(wait.Invoke(system, new object[] { walker, agent, now }), Is.True,
                "A live squadmate's reserved blast area must block the crossing.");

            SEntMan.RemoveComponent<CMUExpeditionAgentComponent>(thrower);

            Assert.That(wait.Invoke(system, new object[] { walker, agent, now }), Is.False,
                "Removing AI control must release its cached squad reservation before the next plan refresh.");
        });
    }
}
