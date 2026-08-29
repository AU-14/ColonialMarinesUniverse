using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._CMU14.Fishing;
using Content.Shared._CMU14.Fishing.Components;
using Content.Shared._CMU14.Fishing.Events;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests;

[TestFixture]
[TestOf(typeof(FishingSystem))]
public sealed class FishingJointVisualsRegressionTest : GameTest
{
    [Test]
    public async Task ThrownLureTargetsLocalRodAndLureTerminationClearsRod()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;
        var originalAttached = session.AttachedEntity;
        EntityUid fisher = default;
        EntityUid serverRod = default;
        EntityUid serverLure = default;
        NetEntity rodNet = default;
        NetEntity lureNet = default;

        await Server.WaitPost(() =>
        {
            fisher = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(session, fisher);
        });
        await Pair.RunUntilSynced();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var rod = SEntMan.SpawnEntity("FishingRod", map.GridCoords);
                Assert.That(
                    Server.System<SharedHandsSystem>()
                        .TryPickupAnyHand(fisher, rod, checkActionBlocker: false),
                    Is.True);
                var target = map.GridCoords.Offset(new Vector2(2f, 0f));
                var throwLure = new ThrowFishingLureActionEvent
                {
                    Performer = fisher,
                    Target = target,
                };

                SEntMan.EventBus.RaiseLocalEvent(rod, throwLure);

                var rodComp = SEntMan.GetComponent<FishingRodComponent>(rod);
                Assert.That(throwLure.Handled, Is.True);
                Assert.That(rodComp.FishingLure, Is.Not.Null);
                var lure = rodComp.FishingLure!.Value;
                var lureComp = SEntMan.GetComponent<FishingLureComponent>(lure);
                var visuals = SEntMan.GetComponent<JointVisualsComponent>(lure);
                Server.System<SharedPhysicsSystem>().SetLinearVelocity(lure, Vector2.Zero);
                Assert.Multiple(() =>
                {
                    Assert.That(lureComp.FishingRod, Is.EqualTo(rod));
                    Assert.That(visuals.Target, Is.EqualTo(rod),
                        "JointVisuals.Target is a local EntityUid, not a NetEntity");
                });

                serverRod = rod;
                serverLure = lure;
            });

            await Pair.RunUntilSynced();
            await Server.WaitAssertion(() =>
            {
                rodNet = SEntMan.GetNetEntity(serverRod);
                lureNet = SEntMan.GetNetEntity(serverLure);
                Assert.Multiple(() =>
                {
                    Assert.That(rodNet, Is.Not.EqualTo(NetEntity.Invalid));
                    Assert.That(lureNet, Is.Not.EqualTo(NetEntity.Invalid));
                });
            });
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() =>
            {
                Assert.That(CEntMan.TryGetEntity(rodNet, out var rod), Is.True);
                Assert.That(CEntMan.TryGetEntity(lureNet, out var lure), Is.True);
                var rodUid = rod!.Value;
                var lureUid = lure!.Value;
                var visuals = CEntMan.GetComponent<JointVisualsComponent>(lureUid);
                Assert.Multiple(() =>
                {
                    Assert.That(CEntMan.HasComponent<TransformComponent>(rodUid), Is.True);
                    Assert.That(visuals.Target, Is.EqualTo(rodUid),
                        "the networked rod target must deserialize to the client's local EntityUid");
                });
            });

            await Server.WaitAssertion(() =>
            {
                var rod = SEntMan.GetEntity(rodNet);
                var lure = SEntMan.GetEntity(lureNet);
                var rodComp = SEntMan.GetComponent<FishingRodComponent>(rod);
                SEntMan.DeleteEntity(lure);

                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.Deleted(lure), Is.True);
                    Assert.That(rodComp.FishingLure, Is.Null,
                        "lure termination must clear the owning rod's lifecycle state");
                });
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, originalAttached));
        }
    }
}
