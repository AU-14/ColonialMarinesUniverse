using Content.IntegrationTests.Fixtures;
using Content.Server.Gravity;
using Content.Server.Stunnable;
using Content.Shared._RMC14.Standing;
using Content.Shared.Alert;
using Content.Shared.Gravity;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.CMU14.Stunnable;

[TestFixture]
public sealed class CMUKnockdownRecoveryTest : GameTest
{
    [Test]
    public async Task LeavingRestResumesRecoveryWithoutClearingActiveParalysis()
    {
        var originalAttached = ServerSession!.AttachedEntity;
        var target = await SpawnPlayer();
        try
        {
            await Server.WaitAssertion(() =>
                Assert.That(Server.System<StunSystem>().TryKnockdown(target, TimeSpan.FromSeconds(0.5), autoStand: false), Is.True));
            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<StandingStateSystem>().IsDown(target), Is.True,
                    "Choosing to crawl must not start automatic recovery.");
                var rest = Server.System<RMCStandingSystem>();
                rest.SetRest(target, true);
                Assert.That(Server.System<StunSystem>().TryParalyze(target, TimeSpan.FromSeconds(2), true), Is.True);
                rest.SetRest(target, false);
                Assert.That(SEntMan.HasComponent<StunnedComponent>(target), Is.True,
                    "Requesting to stand must not clear an active stun.");
                Assert.That(Server.System<StandingStateSystem>().IsDown(target), Is.True);
            });

            await Pair.RunSeconds(5);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(target), Is.False,
                    "Leaving rest must resume recovery when paralysis expires.");
                Assert.That(Server.System<StandingStateSystem>().IsDown(target), Is.False);
            });
            await Client.WaitAssertion(() =>
            {
                var clientTarget = ToClientUid(target);
                Assert.That(CEntMan.HasComponent<KnockedDownComponent>(clientTarget), Is.False);
                Assert.That(Client.System<StandingStateSystem>().IsDown(clientTarget), Is.False);
                Assert.That(Client.System<AlertsSystem>().IsShowingAlert(clientTarget, SharedStunSystem.KnockdownAlert), Is.False);
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession, originalAttached));
        }
    }

    [Test]
    public async Task BlockedStandRetriesAfterObstructionIsRemoved()
    {
        var originalAttached = ServerSession!.AttachedEntity;
        var target = await SpawnPlayer();
        try
        {
            await Server.WaitAssertion(() =>
                Assert.That(Server.System<StunSystem>().TryKnockdown(target, TimeSpan.FromSeconds(0.5), autoStand: false), Is.True));
            await Pair.RunSeconds(1);
            EntityUid table = default;
            await Server.WaitPost(() =>
            {
                table = SSpawnAtPosition("CMTable", TestMap!.GridCoords);
                var coordinates = SEntMan.GetComponent<TransformComponent>(table).Coordinates;
                Server.System<SharedTransformSystem>().SetCoordinates(target, coordinates);
            });
            await Pair.RunTicksSync(2);
            await Pair.RunTicksSync(1);
            await Server.WaitAssertion(() =>
            {
                var stun = Server.System<StunSystem>();
                Assert.That(stun.CanStand((target, SEntMan.GetComponent<KnockedDownComponent>(target))), Is.True,
                    "The player must be able to stand apart from the physical obstruction.");
                Assert.That(Server.System<SharedPhysicsSystem>()
                    .GetEntitiesIntersectingBody(target, StandingStateSystem.StandingCollisionLayer, false), Does.Contain(table));
                stun.ForceStandUp(target);
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(target), Is.True,
                    "The table must prevent standing before it is removed.");
                SEntMan.DeleteEntity(table);
            });

            await Pair.RunSeconds(3);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(target), Is.False,
                    "A temporary obstruction must not permanently disable automatic recovery.");
                Assert.That(Server.System<StandingStateSystem>().IsDown(target), Is.False);
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession, originalAttached));
        }
    }

    private async Task<EntityUid> SpawnPlayer()
    {
        var map = await Pair.CreateTestMap();
        EntityUid target = default;
        await Server.WaitPost(() =>
        {
            Server.System<GravitySystem>().EnableGravity(map.MapUid,
                SEntMan.EnsureComponent<GravityComponent>(map.MapUid));
            target = SSpawnAtPosition("CMMobHuman", map.GridCoords);
        });
        // RMC removes the mover from unoccupied mobs after map initialization.
        await Pair.RunTicksSync(2);
        await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession!, target));
        await Pair.RunTicksSync(3);
        return target;
    }
}
