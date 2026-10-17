#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Tackle;
using Content.Shared.Inventory;
using Content.Shared.Physics;
using Content.Shared.Standing;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Components;

namespace Content.IntegrationTests.Tests.Movement;

[TestFixture]
[TestOf(typeof(StandingStateSystem))]
public sealed class StandingStateMergeRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: StandingStateMergeCollision
          components:
          - type: StandingState
          - type: Appearance
          - type: Physics
            bodyType: Dynamic
          - type: Fixtures
            fixtures:
              hard:
                shape:
                  !type:PhysShapeCircle
                  radius: 0.35
                mask:
                - MidImpassable
                - HighImpassable
                layer:
                - MobLayer
              soft:
                shape:
                  !type:PhysShapeCircle
                  radius: 0.2
                hard: false
                mask:
                - MidImpassable
                - LowImpassable
                layer:
                - MobLayer
          - type: StandingStateMergeProbe
        """;

    [Test]
    public async Task CollisionChangesAreExplicitHardOnlyAndReversible()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var standing = Server.System<StandingStateSystem>();
            var entity = SEntMan.SpawnEntity("StandingStateMergeCollision", map.GridCoords);

            try
            {
                var fixtures = SEntMan.GetComponent<FixturesComponent>(entity);
                var state = SEntMan.GetComponent<StandingStateComponent>(entity);
                var probe = SEntMan.GetComponent<StandingStateMergeProbeComponent>(entity);
                var mid = (int) CollisionGroup.MidImpassable;
                var high = (int) CollisionGroup.HighImpassable;
                var low = (int) CollisionGroup.LowImpassable;
                var hardOriginal = fixtures.Fixtures["hard"].CollisionMask;
                var softOriginal = fixtures.Fixtures["soft"].CollisionMask;

                Assert.That(standing.Down(entity, false, false, true), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(fixtures.Fixtures["hard"].CollisionMask, Is.EqualTo(hardOriginal),
                        "ordinary Down(changeCollision:false) must retain MidImpassable");
                    Assert.That(fixtures.Fixtures["soft"].CollisionMask, Is.EqualTo(softOriginal));
                    Assert.That(state.ChangedFixtures, Is.Empty);
                    Assert.That(probe.Downed, Is.EqualTo(1));
                    Assert.That(standing.IsDown(entity, state), Is.True);
                    Assert.That(standing.IsDown((entity, state)), Is.True,
                        "the legacy EntityUid wrapper and Entity<T> overload must agree");
                });

                Assert.That(standing.Down(entity, false, false, true, changeCollision: true), Is.True);
                Assert.That(probe.Downed, Is.EqualTo(1),
                    "an already-down request returns successfully without a duplicate DownedEvent");

                Assert.That(standing.Stand(entity, state, force: true), Is.True);
                Assert.That(probe.Stood, Is.EqualTo(1));
                Assert.That(standing.Down(entity, false, false, true, changeCollision: true), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(fixtures.Fixtures["hard"].CollisionMask & mid, Is.Zero,
                        "explicit collision change removes MidImpassable for climb, buckle, fireman-carry, nest, and sentinel traversal");
                    Assert.That(fixtures.Fixtures["hard"].CollisionMask & high, Is.EqualTo(high),
                        "unrelated hard-fixture mask bits must survive the down transition");
                    Assert.That(fixtures.Fixtures["soft"].CollisionMask & mid, Is.EqualTo(mid));
                    Assert.That(fixtures.Fixtures["soft"].CollisionMask & low, Is.EqualTo(low),
                        "soft fixtures are never recorded or rewritten");
                    Assert.That(state.ChangedFixtures, Is.EqualTo(new[] { "hard" }));
                    Assert.That(probe.Downed, Is.EqualTo(2));
                });

                Assert.That(standing.Stand(entity, state, force: true), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(fixtures.Fixtures["hard"].CollisionMask, Is.EqualTo(hardOriginal));
                    Assert.That(fixtures.Fixtures["soft"].CollisionMask, Is.EqualTo(softOriginal));
                    Assert.That(state.ChangedFixtures, Is.Empty);
                    Assert.That(probe.Stood, Is.EqualTo(2));
                    Assert.That(standing.IsDown(entity, state), Is.False);
                    Assert.That(standing.IsDown((entity, state)), Is.False);
                });

                IInventoryRelayEvent downedRelay = new DownedEvent();
                IInventoryRelayEvent stoodRelay = new StoodEvent();
                Assert.Multiple(() =>
                {
                    Assert.That(downedRelay.TargetSlots, Is.EqualTo(SlotFlags.FEET));
                    Assert.That(stoodRelay.TargetSlots, Is.EqualTo(SlotFlags.FEET));
                });
            }
            finally
            {
                SEntMan.DeleteEntity(entity);
            }
        });
    }

    [Test]
    public async Task SelfRestPreservesTackleAttributionButExternalDowningClearsIt()
    {
        var map = await Pair.CreateTestMap();
        EntityUid target = default;
        EntityUid tackler = default;
        EntityUid other = default;

        try
        {
            await Server.WaitPost(() =>
            {
                var standing = Server.System<StandingStateSystem>();
                target = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
                tackler = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
                other = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
                var tackled = SEntMan.EnsureComponent<TackledRecentlyByComponent>(target);
                tackled.Tacklers.Add(tackler);

                Assert.That(standing.Down(target, false, false, true, downedBy: target), Is.True);
                Assert.That(SEntMan.HasComponent<TackledRecentlyByComponent>(target), Is.True,
                    "resting oneself must preserve accumulated tackle attribution");
                Assert.That(standing.Stand(target, force: true), Is.True);
                Assert.That(standing.Down(target, false, false, true), Is.True);
            });
            await Server.WaitRunTicks(2);

            await Server.WaitAssertion(() =>
            {
                var standing = Server.System<StandingStateSystem>();
                Assert.That(SEntMan.HasComponent<TackledRecentlyByComponent>(target), Is.False,
                    "a null downing source retains upstream tackle-reset behavior");

                Assert.That(standing.Stand(target, force: true), Is.True);
                var tackled = SEntMan.EnsureComponent<TackledRecentlyByComponent>(target);
                tackled.Tacklers.Add(tackler);
                Assert.That(standing.Down(target, false, false, true, downedBy: other), Is.True);
            });
            await Server.WaitRunTicks(2);

            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.HasComponent<TackledRecentlyByComponent>(target), Is.False,
                    "an external downing source must also clear tackle attribution");
            });
        }
        finally
        {
            if (other.Valid)
                await Pair.DeleteEntityTreeLeafFirst(other);
            if (tackler.Valid)
                await Pair.DeleteEntityTreeLeafFirst(tackler);
            if (target.Valid)
                await Pair.DeleteEntityTreeLeafFirst(target);
        }
    }
}

[RegisterComponent]
public sealed partial class StandingStateMergeProbeComponent : Component
{
    public int Downed;
    public int Stood;
}

public sealed partial class StandingStateMergeProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StandingStateMergeProbeComponent, DownedEvent>(OnDowned);
        SubscribeLocalEvent<StandingStateMergeProbeComponent, StoodEvent>(OnStood);
    }

    private static void OnDowned(Entity<StandingStateMergeProbeComponent> entity, ref DownedEvent args)
    {
        entity.Comp.Downed++;
    }

    private static void OnStood(Entity<StandingStateMergeProbeComponent> entity, ref StoodEvent args)
    {
        entity.Comp.Stood++;
    }
}

#pragma warning restore RA0002
