using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.CMU14.Insurgency.Sapper;
using Content.Server.CMU14.Yautja;
using Content.Shared.CMU14.Insurgency.Sapper;
using Content.Shared.CMU14.Yautja;
using Content.Shared._RMC14.Slow;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Movement;

[TestFixture]
[TestOf(typeof(YautjaAbominationSystem))]
[TestOf(typeof(SapperSnareSystem))]
public sealed class ForkMovementOwnerRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: ForkMovementOwnerTestTarget
  components:
  - type: MovementSpeedModifier
  - type: ForkMovementOwnerProbe
";

    [Test]
    public async Task RushExpiryAndRoarRecipientRefreshUseCurrentMovementOwner()
    {
        var map = await Pair.CreateTestMap();
        EntityUid source = default;
        EntityUid recipient = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                _ = Server.System<ForkMovementOwnerProbeSystem>();
                var movement = Server.System<MovementSpeedModifierSystem>();
                source = SEntMan.SpawnEntity("ForkMovementOwnerTestTarget", map.GridCoords);
                recipient = SEntMan.SpawnEntity("ForkMovementOwnerTestTarget", map.GridCoords);

                var rush = SEntMan.EnsureComponent<YautjaAbominationRushComponent>(recipient);
                rush.SpeedMultiplier = 1.4f;
                rush.ExpiresAt = Server.Timing.CurTime + TimeSpan.FromMinutes(1);

                var roar = SEntMan.EnsureComponent<YautjaAbominationRoarBuffComponent>(recipient);
                roar.SpeedMultiplier = 1.25f;
                roar.ExpiresAt = Server.Timing.CurTime + TimeSpan.FromMinutes(1);

                var probe = SEntMan.GetComponent<ForkMovementOwnerProbeComponent>(recipient);
                probe.Reset();
                movement.RefreshMovementSpeedModifiers(recipient);

                var speed = SEntMan.GetComponent<MovementSpeedModifierComponent>(recipient);
                var sourceSpeed = SEntMan.GetComponent<MovementSpeedModifierComponent>(source);
                Assert.Multiple(() =>
                {
                    Assert.That(probe.Snapshots, Has.Count.EqualTo(1));
                    Assert.That(probe.Snapshots[0].RushRunning, Is.True);
                    Assert.That(probe.Snapshots[0].RoarRunning, Is.True);
                    Assert.That(probe.Snapshots[0].Walk, Is.EqualTo(1.75f).Within(0.0001f));
                    Assert.That(probe.Snapshots[0].Sprint, Is.EqualTo(1.75f).Within(0.0001f));
                    Assert.That(speed.WalkSpeedModifier, Is.EqualTo(1.75f).Within(0.0001f));
                    Assert.That(speed.SprintSpeedModifier, Is.EqualTo(1.75f).Within(0.0001f));
                    Assert.That(sourceSpeed.WalkSpeedModifier, Is.EqualTo(1f));
                    Assert.That(sourceSpeed.SprintSpeedModifier, Is.EqualTo(1f),
                        "the roar recipient owns the refresh; the source must remain unchanged");
                });

                probe.Reset();
                rush.ExpiresAt = Server.Timing.CurTime;
            });

            await Pair.RunTicksSync(1);
            await Server.WaitAssertion(() =>
            {
                var probe = SEntMan.GetComponent<ForkMovementOwnerProbeComponent>(recipient);
                var speed = SEntMan.GetComponent<MovementSpeedModifierComponent>(recipient);
                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.HasComponent<YautjaAbominationRushComponent>(recipient), Is.False);
                    Assert.That(SEntMan.HasComponent<YautjaAbominationRoarBuffComponent>(recipient), Is.True);
                    Assert.That(probe.Snapshots, Has.Count.EqualTo(1));
                    Assert.That(probe.Snapshots[0].RushRunning, Is.False,
                        "rush must stop before its expiry refresh is raised");
                    Assert.That(probe.Snapshots[0].RoarRunning, Is.True);
                    Assert.That(probe.Snapshots[0].Walk, Is.EqualTo(1.25f).Within(0.0001f));
                    Assert.That(probe.Snapshots[0].Sprint, Is.EqualTo(1.25f).Within(0.0001f));
                    Assert.That(speed.WalkSpeedModifier, Is.EqualTo(1.25f).Within(0.0001f));
                    Assert.That(speed.SprintSpeedModifier, Is.EqualTo(1.25f).Within(0.0001f));
                });

                probe.Reset();
                SEntMan.GetComponent<YautjaAbominationRoarBuffComponent>(recipient).ExpiresAt = Server.Timing.CurTime;
            });

            await Pair.RunTicksSync(1);
            await Server.WaitAssertion(() =>
            {
                var probe = SEntMan.GetComponent<ForkMovementOwnerProbeComponent>(recipient);
                var speed = SEntMan.GetComponent<MovementSpeedModifierComponent>(recipient);
                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.HasComponent<YautjaAbominationRoarBuffComponent>(recipient), Is.False);
                    Assert.That(probe.Snapshots, Has.Count.EqualTo(1));
                    Assert.That(probe.Snapshots[0].RushRunning, Is.False);
                    Assert.That(probe.Snapshots[0].RoarRunning, Is.False,
                        "roar must stop before its expiry refresh is raised");
                    Assert.That(probe.Snapshots[0].Walk, Is.EqualTo(1f));
                    Assert.That(probe.Snapshots[0].Sprint, Is.EqualTo(1f));
                    Assert.That(speed.WalkSpeedModifier, Is.EqualTo(1f));
                    Assert.That(speed.SprintSpeedModifier, Is.EqualTo(1f));
                });
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                if (SEntMan.EntityExists(source))
                    SEntMan.DeleteEntity(source);
                if (SEntMan.EntityExists(recipient))
                    SEntMan.DeleteEntity(recipient);
            });
        }
    }

    [Test]
    public async Task SapperCutFreeRemovesRootBeforeEveryRestoringRefresh()
    {
        var map = await Pair.CreateTestMap();
        EntityUid target = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                _ = Server.System<ForkMovementOwnerProbeSystem>();
                var movement = Server.System<MovementSpeedModifierSystem>();
                var slow = Server.System<RMCSlowSystem>();
                target = SEntMan.SpawnEntity("ForkMovementOwnerTestTarget", map.GridCoords);

                SEntMan.EnsureComponent<SapperSnaredComponent>(target);
                Assert.That(slow.TryRoot(target, TimeSpan.FromMinutes(1)), Is.True);
                movement.RefreshMovementSpeedModifiers(target);

                var speed = SEntMan.GetComponent<MovementSpeedModifierComponent>(target);
                var probe = SEntMan.GetComponent<ForkMovementOwnerProbeComponent>(target);
                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.GetComponent<RMCRootedComponent>(target).Running, Is.True);
                    Assert.That(speed.WalkSpeedModifier, Is.Zero);
                    Assert.That(speed.SprintSpeedModifier, Is.Zero);
                });

                probe.Reset();
                var completion = CutFreeCompletion(target);
                SEntMan.EventBus.RaiseLocalEvent(target, completion);

                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.HasComponent<SapperSnaredComponent>(target), Is.False);
                    Assert.That(SEntMan.HasComponent<RMCRootedComponent>(target), Is.False);
                    Assert.That(probe.Snapshots, Is.Not.Empty);
                    Assert.That(probe.Snapshots.All(snapshot => !snapshot.RootRunning), Is.True,
                        "the early-cut shutdown must remove the root before any restoring refresh");
                    Assert.That(probe.Snapshots.All(snapshot => snapshot.Walk == 1f && snapshot.Sprint == 1f),
                        Is.True);
                    Assert.That(speed.WalkSpeedModifier, Is.EqualTo(1f));
                    Assert.That(speed.SprintSpeedModifier, Is.EqualTo(1f));
                });
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                if (SEntMan.EntityExists(target))
                    SEntMan.DeleteEntity(target);
            });
        }
    }

    private SapperCutFreeDoAfterEvent CutFreeCompletion(EntityUid target)
    {
        var completion = new SapperCutFreeDoAfterEvent();
        var args = new DoAfterArgs(SEntMan, target, TimeSpan.Zero, completion, target, target);
        completion.DoAfter = new Content.Shared.DoAfter.DoAfter(0, args, TimeSpan.Zero);
        return completion;
    }
}

[RegisterComponent]
public sealed partial class ForkMovementOwnerProbeComponent : Component
{
    public readonly List<ForkMovementOwnerRefreshSnapshot> Snapshots = new();

    public void Reset()
    {
        Snapshots.Clear();
    }
}

public readonly record struct ForkMovementOwnerRefreshSnapshot(
    float Walk,
    float Sprint,
    bool RushRunning,
    bool RoarRunning,
    bool RootRunning);

public sealed class ForkMovementOwnerProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ForkMovementOwnerProbeComponent, RefreshMovementSpeedModifiersEvent>(
            OnRefresh,
            after: [typeof(YautjaAbominationSystem), typeof(RMCSlowSystem)]);
    }

    private void OnRefresh(
        Entity<ForkMovementOwnerProbeComponent> ent,
        ref RefreshMovementSpeedModifiersEvent args)
    {
        var rushRunning = TryComp(ent, out YautjaAbominationRushComponent? rush) && rush.Running;
        var roarRunning = TryComp(ent, out YautjaAbominationRoarBuffComponent? roar) && roar.Running;
        var rootRunning = TryComp(ent, out RMCRootedComponent? root) && root.Running;
        ent.Comp.Snapshots.Add(new ForkMovementOwnerRefreshSnapshot(
            args.WalkSpeedModifier,
            args.SprintSpeedModifier,
            rushRunning,
            roarRunning,
            rootRunning));
    }
}
