using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Stunnable;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Mobs.Components;
using Content.Shared.Revenant;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Stunnable;

[TestFixture]
[TestOf(typeof(SharedStunSystem))]
public sealed class StunVisualMergeRegressionTest : GameTest
{
    [Test]
    public async Task SuccessorStartupAndShutdownRefreshXenoAndRevenantVisuals()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;
        var originalAttached = session.AttachedEntity;
        EntityUid xeno = default;
        EntityUid revenant = default;
        NetEntity netXeno = default;
        NetEntity netRevenant = default;

        try
        {
            await Server.WaitPost(() =>
            {
                xeno = SSpawnAtPosition("CMXenoDrone", map.GridCoords);
                revenant = SSpawnAtPosition("MobRevenant", map.GridCoords.Offset(new Vector2(2, 0)));
                Assert.That(SEntMan.HasComponent<MobStateComponent>(revenant), Is.False,
                    "the incorporeal revenant is intentionally ineligible for the MobState-whitelisted stun effect");
                SEntMan.EnsureComponent<MobStateComponent>(revenant);
                netXeno = SEntMan.GetNetEntity(xeno);
                netRevenant = SEntMan.GetNetEntity(revenant);
                Server.PlayerMan.SetAttachedEntity(session, xeno);
            });
            await Pair.RunTicksSync(10);

            EntityUid clientXeno = default;
            EntityUid clientRevenant = default;
            await Client.WaitAssertion(() =>
            {
                clientXeno = CEntMan.GetEntity(netXeno);
                clientRevenant = CEntMan.GetEntity(netRevenant);
                Assert.Multiple(() =>
                {
                    Assert.That(XenoState(clientXeno), Is.EqualTo("alive"));
                    Assert.That(RevenantStunned(CEntMan, clientRevenant), Is.False);
                });
            });

            await Server.WaitAssertion(() =>
            {
                var stun = Server.System<StunSystem>();
                Assert.That(stun.TryKnockdown(xeno, TimeSpan.FromSeconds(10), force: true), Is.True);
                Assert.That(stun.TryStun(revenant, TimeSpan.FromSeconds(10), refresh: true), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.HasComponent<KnockedDownComponent>(xeno), Is.True);
                    Assert.That(SEntMan.HasComponent<StunnedComponent>(revenant), Is.True);
                    Assert.That(RevenantStunned(SEntMan, revenant), Is.True,
                        "the successor Stunned event must drive server revenant appearance immediately");
                });
            });
            await Pair.RunTicksSync(10);

            await Client.WaitAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(XenoState(clientXeno), Is.EqualTo("crit"),
                        "client KnockedDown startup must select the real xeno RSI's critical state");
                    Assert.That(RevenantStunned(CEntMan, clientRevenant), Is.True);
                });
            });

            await Server.WaitAssertion(() =>
            {
                var stun = Server.System<StunSystem>();
                var status = Server.System<StatusEffectsSystem>();
                Assert.That(stun.TryClearStunAndKnockdown(xeno), Is.True);
                Assert.That(status.TryRemoveStatusEffect(revenant, SharedStunSystem.StunId), Is.True);
            });
            await Pair.RunTicksSync(10);

            await Server.WaitAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.HasComponent<KnockedDownComponent>(xeno), Is.False);
                    Assert.That(SEntMan.HasComponent<StunnedComponent>(revenant), Is.False);
                    Assert.That(RevenantStunned(SEntMan, revenant), Is.False,
                        "the keyed Stun shutdown bridge must clear revenant appearance");
                });
            });
            await Client.WaitAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(XenoState(clientXeno), Is.EqualTo("alive"),
                        "client KnockedDown shutdown must synchronously restore the ordinary xeno state");
                    Assert.That(RevenantStunned(CEntMan, clientRevenant), Is.False);
                });
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, originalAttached));
        }

        string? XenoState(EntityUid uid)
        {
            var sprite = CEntMan.GetComponent<SpriteComponent>(uid);
            var sprites = Client.System<SpriteSystem>();
            Assert.That(sprites.LayerMapTryGet((uid, sprite), XenoVisualLayers.Base, out var layer, false), Is.True);
            return sprites.LayerGetRsiState((uid, sprite), layer).Name;
        }

        static bool RevenantStunned(IEntityManager entities, EntityUid uid)
        {
            var appearance = entities.System<SharedAppearanceSystem>();
            Assert.That(appearance.TryGetData(uid, RevenantVisuals.Stunned, out bool stunned), Is.True);
            return stunned;
        }
    }
}
