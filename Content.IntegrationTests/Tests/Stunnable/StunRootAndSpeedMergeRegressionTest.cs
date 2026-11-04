using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Movement;
using Content.Shared._RMC14.Slow;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Stunnable;

[TestFixture]
[TestOf(typeof(RMCSlowSystem))]
[TestOf(typeof(TemporarySpeedModifiersSystem))]
public sealed class StunRootAndSpeedMergeRegressionTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
        DummyTicker = false,
    };

    [Test]
    public async Task NativeRootIgnoresChemicalWhileMigratedRootScalesAndStacksFromNow()
    {
        await Server.WaitAssertion(() =>
        {
            _ = Server.System<StunDurationMergeProbeSystem>();
            var slow = Server.System<RMCSlowSystem>();

            var native = SSpawn("StunDurationMergeTarget");
            var nativeProbe = SEntMan.GetComponent<StunDurationMergeProbeComponent>(native);
            nativeProbe.ChemicalMultiplier = 0f;
            var nativeStart = Server.Timing.CurTime;
            Assert.That(slow.TryRoot(native, TimeSpan.FromSeconds(2), applyChemical: false), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<RMCRootedComponent>(native).ExpiresAt,
                    Is.EqualTo(nativeStart + TimeSpan.FromSeconds(2)),
                    "the native root API remains deliberately chemical-insensitive");
                Assert.That(nativeProbe.ChemicalCalls, Is.Zero);
            });

            var blocked = SSpawn("StunDurationMergeTarget");
            var blockedProbe = SEntMan.GetComponent<StunDurationMergeProbeComponent>(blocked);
            blockedProbe.ChemicalMultiplier = 0f;
            Assert.That(slow.TryRoot(blocked, TimeSpan.FromSeconds(2), applyChemical: true), Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<RMCRootedComponent>(blocked), Is.False,
                    "a migrated chemical-sensitive root must not create a zero-duration component");
                Assert.That(blockedProbe.ChemicalCalls, Is.EqualTo(1));
            });

            var stacked = SSpawn("StunDurationMergeTarget");
            var stackedProbe = SEntMan.GetComponent<StunDurationMergeProbeComponent>(stacked);
            stackedProbe.ChemicalMultiplier = 0.5f;
            var start = Server.Timing.CurTime;
            Assert.That(slow.TryRoot(stacked, TimeSpan.FromSeconds(2), refresh: false, applyChemical: true), Is.True);
            var root = SEntMan.GetComponent<RMCRootedComponent>(stacked);
            Assert.That(root.ExpiresAt, Is.EqualTo(start + TimeSpan.FromSeconds(1)),
                "the first non-refresh root stacks from now, not from the zero default expiry");

            Assert.That(slow.TryRoot(stacked, TimeSpan.FromSeconds(2), refresh: false, applyChemical: true), Is.True);
            Assert.That(root.ExpiresAt, Is.EqualTo(start + TimeSpan.FromSeconds(2)));
            Assert.That(slow.TryRoot(stacked, TimeSpan.FromSeconds(1), refresh: true, applyChemical: true), Is.True);
            Assert.That(root.ExpiresAt, Is.EqualTo(start + TimeSpan.FromSeconds(2)),
                "refresh cannot shorten an existing root");
            Assert.That(slow.TryRoot(stacked, TimeSpan.FromSeconds(6), refresh: true, applyChemical: true), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(root.ExpiresAt, Is.EqualTo(start + TimeSpan.FromSeconds(3)),
                    "refresh takes the later chemically adjusted expiry");
                Assert.That(stackedProbe.ChemicalCalls, Is.EqualTo(4));
            });
        });
    }

    [Test]
    public async Task TemporarySpeedSkipsNonpositiveEntriesReplicatesAndExpires()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;
        var originalAttached = session.AttachedEntity;
        EntityUid target = default;
        NetEntity targetNet = default;

        try
        {
            await Server.WaitPost(() =>
            {
                _ = Server.System<StunDurationMergeProbeSystem>();
                target = SSpawnAtPosition("StunDurationMergeTarget", map.GridCoords);
                targetNet = SEntMan.GetNetEntity(target);
                Server.PlayerMan.SetAttachedEntity(session, target);
            });
            await Pair.RunTicksSync(3);

            await Server.WaitAssertion(() =>
            {
                var temporary = Server.System<TemporarySpeedModifiersSystem>();
                var probe = SEntMan.GetComponent<StunDurationMergeProbeComponent>(target);
                probe.ChemicalMultiplier = 0.5f;
                var start = Server.Timing.CurTime;
                temporary.ModifySpeed(target,
                [
                    new(TimeSpan.Zero, 0.1f, 0.1f),
                    new(TimeSpan.FromSeconds(-2), 0.2f, 0.2f),
                    new(TimeSpan.FromSeconds(2), 0.6f, 0.7f),
                ]);

                var component = SEntMan.GetComponent<TemporarySpeedModifiersComponent>(target);
                Assert.Multiple(() =>
                {
                    Assert.That(component.Modifiers, Has.Count.EqualTo(1));
                    Assert.That(component.Modifiers[0].ExpiresAt, Is.EqualTo(start + TimeSpan.FromSeconds(1)));
                    Assert.That(component.Modifiers[0].Walk, Is.EqualTo(0.6f));
                    Assert.That(component.Modifiers[0].Sprint, Is.EqualTo(0.7f));
                    Assert.That(probe.ChemicalCalls, Is.EqualTo(3),
                        "every candidate duration is transformed once before the nonpositive filter");
                });
            });
            await Pair.RunTicksSync(3);

            await Client.WaitAssertion(() =>
            {
                var clientTarget = CEntMan.GetEntity(targetNet);
                var component = CEntMan.GetComponent<TemporarySpeedModifiersComponent>(clientTarget);
                Assert.That(component.Modifiers, Has.Count.EqualTo(1),
                    "the server-owned temporary speed list must be dirtied and replicated");
                Assert.Multiple(() =>
                {
                    Assert.That(component.Modifiers[0].Walk, Is.EqualTo(0.6f));
                    Assert.That(component.Modifiers[0].Sprint, Is.EqualTo(0.7f));
                });
            });

            await Pair.RunSeconds(1.1f);
            await Pair.RunTicksSync(2);
            await Server.WaitAssertion(() =>
                Assert.That(SEntMan.HasComponent<TemporarySpeedModifiersComponent>(target), Is.False));
            await Client.WaitAssertion(() =>
            {
                var clientTarget = CEntMan.GetEntity(targetNet);
                Assert.That(CEntMan.HasComponent<TemporarySpeedModifiersComponent>(clientTarget), Is.False,
                    "expiry removes the networked modifier component on both sides");
            });

            await Server.WaitAssertion(() =>
            {
                var empty = SSpawnAtPosition("StunDurationMergeTarget", map.GridCoords.Offset(new Vector2(1, 0)));
                var probe = SEntMan.GetComponent<StunDurationMergeProbeComponent>(empty);
                probe.ChemicalMultiplier = 0f;
                Server.System<TemporarySpeedModifiersSystem>().ModifySpeed(empty,
                [
                    new(TimeSpan.FromSeconds(1), 0.5f, 0.5f),
                    new(TimeSpan.Zero, 0.5f, 0.5f),
                ]);
                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.HasComponent<TemporarySpeedModifiersComponent>(empty), Is.False,
                        "no component should be created when no transformed duration survives");
                    Assert.That(probe.ChemicalCalls, Is.EqualTo(2));
                });
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, originalAttached));
        }
    }
}
