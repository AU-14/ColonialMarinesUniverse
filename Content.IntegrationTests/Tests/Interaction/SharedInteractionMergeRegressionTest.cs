#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.Interaction;
using Content.Server.Movement.Components;
using Content.Shared._RMC14.CombatMode;
using Content.Shared._RMC14.Interaction;
using Content.Shared._RMC14.Movement;
using Content.Shared._RMC14.Storage;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Components;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Interaction;

[TestFixture]
[TestOf(typeof(SharedInteractionSystem))]
public sealed class SharedInteractionMergeRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: SharedInteractionMergeSmallFixture
          components:
          - type: Physics
            bodyType: Static
          - type: Fixtures
            fixtures:
              fix1:
                shape:
                  !type:PhysShapeAabb
                    bounds: "-0.1,-0.1,0.1,0.1"
                layer:
                - MobLayer
                mask:
                - MobMask

        - type: entity
          id: SharedInteractionMergeLargeFixture
          components:
          - type: Physics
            bodyType: Static
          - type: Fixtures
            fixtures:
              fix1:
                shape:
                  !type:PhysShapeAabb
                    bounds: "-0.75,-0.75,0.75,0.75"
                layer:
                - MobLayer
                mask:
                - MobMask

        - type: entity
          id: SharedInteractionMergeStorage
          components:
          - type: Storage
            grid:
            - 0,0,3,3

        - type: entity
          id: SharedInteractionMergeItem
          components:
          - type: Item
            size: Tiny
          - type: RMCItemKeepUIOpenOnStorageClosed
        """;

    [Test]
    public async Task NearestFixturesLagRewindAndAccessOverridesComposeWithoutQueryLeakage()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;
        var originalAttached = session.AttachedEntity;
        EntityUid largeUser = default;
        EntityUid largeTarget = default;
        EntityUid actor = default;
        EntityUid uiTarget = default;
        EntityUid rewoundTarget = default;
        EntityUid overrideTarget = default;
        EntityUid storageUid = default;
        EntityUid keptItem = default;
        GameTick oldLagTick = default;
        var oldLagSubstep = 0;
        var lagStateCaptured = false;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var interaction = Server.System<InteractionSystem>();
                var lag = Server.System<SharedRMCLagCompensationSystem>();
                var transform = Server.System<SharedTransformSystem>();
                var containers = Server.System<SharedContainerSystem>();

                largeUser = Spawn("SharedInteractionMergeLargeFixture", map, 0);
                largeTarget = Spawn("SharedInteractionMergeLargeFixture", map, 2.9f);
                Assert.That(InUiKeepOpenRange(interaction, largeUser, largeTarget, 1.5f), Is.True,
                    "BUI keep-open must use the 1.4-tile nearest-fixture gap, not the 2.9-tile origins");

                transform.SetCoordinates(largeTarget, new EntityCoordinates(map.Grid, new Vector2(3.2f, 0)));
                Assert.That(InUiKeepOpenRange(interaction, largeUser, largeTarget, 1.5f), Is.False);
                transform.SetCoordinates(largeTarget, new EntityCoordinates(map.Grid, new Vector2(2.9f, 0)));
                Assert.That(InUiKeepOpenRange(interaction, largeUser, largeTarget, 1.5f), Is.True,
                    "alternating queries must not retain the prior query's scratch results");

                actor = Spawn("SharedInteractionMergeSmallFixture", map, 0);
                uiTarget = Spawn("SharedInteractionMergeSmallFixture", map, 2.8f);
                Server.PlayerMan.SetAttachedEntity(session, actor);
                var actorHistory = SEntMan.EnsureComponent<LagCompensationComponent>(actor);
                actorHistory.Positions.Clear();
                var historicalActor = new EntityCoordinates(map.Grid, new Vector2(1f, 0));
                var historicalActorAngle = Angle.FromDegrees(17);
                actorHistory.Positions.Enqueue((SGameTiming.CurTime, historicalActor, historicalActorAngle));
                oldLagTick = lag.GetLastRealTick(session.UserId);
                oldLagSubstep = lag.GetLastRealSubstep(session.UserId);
                lagStateCaptured = true;
                lag.SetLastRealTick(session.UserId, SGameTiming.CurTick);

                var rewind = lag.GetCoordinatesAngle(actor, session);
                Assert.Multiple(() =>
                {
                    Assert.That(rewind.Coordinates, Is.EqualTo(historicalActor));
                    Assert.That(rewind.Angle, Is.EqualTo(historicalActorAngle));
                    Assert.That(InUiKeepOpenRange(interaction, actor, uiTarget, 1.5f), Is.True,
                        "the historical actor fixture gap is 1.6 tiles and requires the 0.25 lag margin");
                });
                actorHistory.Positions.Clear();
                Assert.That(InUiKeepOpenRange(interaction, actor, uiTarget, 1.5f), Is.False,
                    "the same current fixtures are out of range without a historical actor position");

                rewoundTarget = Spawn("SharedInteractionMergeSmallFixture", map, 2.8f);
                var targetHistory = SEntMan.EnsureComponent<LagCompensationComponent>(rewoundTarget);
                targetHistory.Positions.Clear();
                var historicalTarget = new EntityCoordinates(map.Grid, new Vector2(1.85f, 0));
                targetHistory.Positions.Enqueue((SGameTiming.CurTime, historicalTarget, Angle.Zero));
                Assert.That(interaction.InRangeUnobstructed(actor, rewoundTarget, user: actor), Is.True,
                    "a changed historical target coordinate must receive MarginTiles");

                targetHistory.Positions.Clear();
                targetHistory.Positions.Enqueue((SGameTiming.CurTime, Transform(rewoundTarget).Coordinates, Angle.Zero));
                Assert.That(interaction.InRangeUnobstructed(actor, rewoundTarget, user: actor), Is.False,
                    "an unchanged/current target coordinate must not receive MarginTiles");

                overrideTarget = Spawn("SharedInteractionMergeSmallFixture", map, 3f);
                var ignoreRange = SEntMan.EnsureComponent<IgnoreInteractionRangeComponent>(actor);
                ignoreRange.Range = 3.5f;
                Assert.That(interaction.InRangeUnobstructed(actor, overrideTarget, lagCompensate: false), Is.True);
                SEntMan.RemoveComponent<IgnoreInteractionRangeComponent>(actor);
                Assert.That(interaction.InRangeUnobstructed(actor, overrideTarget, lagCompensate: false), Is.False);

                storageUid = SEntMan.SpawnEntity("SharedInteractionMergeStorage", map.GridCoords);
                keptItem = SEntMan.SpawnEntity("SharedInteractionMergeItem", map.GridCoords);
                var storage = SEntMan.GetComponent<StorageComponent>(storageUid);
                Assert.That(containers.Insert(keptItem, storage.Container, force: true), Is.True);
                Assert.That(interaction.CanAccessViaStorage(actor, keptItem, storage.Container), Is.True,
                    "RMC keep-open items remain accessible after their storage UI closes");
                SEntMan.RemoveComponent<RMCItemKeepUIOpenOnStorageClosedComponent>(keptItem);
                Assert.That(interaction.CanAccessViaStorage(actor, keptItem, storage.Container), Is.False);
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                if (lagStateCaptured)
                {
                    Server.System<SharedRMCLagCompensationSystem>()
                        .SetLastRealTick(session.UserId, oldLagTick, oldLagSubstep);
                }

                Server.PlayerMan.SetAttachedEntity(session, originalAttached);
                foreach (var uid in new[]
                         {
                             largeUser, largeTarget, actor, uiTarget, rewoundTarget, overrideTarget, storageUid, keptItem
                         })
                {
                    if (SEntMan.EntityExists(uid))
                        SEntMan.DeleteEntity(uid);
                }
            });
        }
    }

    [Test]
    public async Task UserCancellationAndDropRotationOrderingRemainIntact()
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default;
        EntityUid target = default;
        EntityUid used = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var interaction = Server.System<InteractionSystem>();
                user = SEntMan.SpawnEntity(null, map.GridCoords);
                target = SEntMan.SpawnEntity(null, map.GridCoords);
                used = SEntMan.SpawnEntity(null, map.GridCoords);
                SEntMan.EnsureComponent<ComplexInteractionComponent>(user);
                var userProbe = SEntMan.EnsureComponent<SharedInteractionMergeProbeComponent>(user);
                var targetProbe = SEntMan.EnsureComponent<SharedInteractionMergeProbeComponent>(target);
                var usedProbe = SEntMan.EnsureComponent<SharedInteractionMergeProbeComponent>(used);

                interaction.InteractHand(user, target);
                Assert.Multiple(() =>
                {
                    Assert.That(targetProbe.TargetHand, Is.EqualTo(1));
                    Assert.That(userProbe.UserHand, Is.EqualTo(1),
                        "the user-side cancellation event must still fire after the target event");
                    Assert.That(targetProbe.Activated, Is.Zero,
                        "the handled user event must suppress fallback activation");
                });

                Assert.That(interaction.InteractUsing(
                    user,
                    used,
                    target,
                    Transform(target).Coordinates,
                    checkCanInteract: false,
                    checkCanUse: false), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(targetProbe.TargetUsing, Is.EqualTo(1));
                    Assert.That(userProbe.UserUsing, Is.EqualTo(1),
                        "the user-side using cancellation event must still fire after the target event");
                    Assert.That(usedProbe.AfterInteract, Is.Zero,
                        "the handled user event must suppress the low-priority AfterInteract path");
                });

                userProbe.OverrideCombat = true;
                userProbe.CanCombatInteract = true;
                Assert.That(interaction.CombatModeCanHandInteract(user, target), Is.True);
                userProbe.CanCombatInteract = false;
                Assert.That(interaction.CombatModeCanHandInteract(user, target), Is.False,
                    "the RMC combat-mode user override remains authoritative in both directions");

                var mover = SEntMan.EnsureComponent<InputMoverComponent>(user);
                mover.TargetRelativeRotation = Angle.FromDegrees(73);
                interaction.DroppedInteraction(user, used);
                Assert.Multiple(() =>
                {
                    Assert.That(usedProbe.Dropped, Is.EqualTo(1));
                    Assert.That(usedProbe.RotationAtDrop.Theta, Is.EqualTo(mover.TargetRelativeRotation.Theta).Within(0.0001),
                        "DroppedEvent must observe the final no-lerp target-relative rotation");
                    Assert.That(Transform(used).LocalRotation.Theta, Is.EqualTo(mover.TargetRelativeRotation.Theta).Within(0.0001));
                });
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                foreach (var uid in new[] { user, target, used })
                {
                    if (SEntMan.EntityExists(uid))
                        SEntMan.DeleteEntity(uid);
                }
            });
        }
    }

    private EntityUid Spawn(string prototype, TestMapData map, float x)
    {
        return SEntMan.SpawnEntity(prototype, new EntityCoordinates(map.Grid, new Vector2(x, 0)));
    }

    private TransformComponent Transform(EntityUid uid)
    {
        return SEntMan.GetComponent<TransformComponent>(uid);
    }

    private bool InUiKeepOpenRange(InteractionSystem interaction, EntityUid user, EntityUid target, float range)
    {
        var userTransform = Transform(user);
        var targetTransform = Transform(target);
        return (bool) typeof(SharedInteractionSystem)
            .GetMethod("InUiKeepOpenRange", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(interaction, new object[]
            {
                new Entity<TransformComponent?>(user, userTransform),
                new Entity<TransformComponent?>(target, targetTransform),
                range
            })!;
    }
}

[RegisterComponent]
public sealed partial class SharedInteractionMergeProbeComponent : Component
{
    public int TargetHand;
    public int UserHand;
    public int TargetUsing;
    public int UserUsing;
    public int Activated;
    public int AfterInteract;
    public int Dropped;
    public Angle RotationAtDrop;
    public bool OverrideCombat;
    public bool CanCombatInteract;
}

public sealed class SharedInteractionMergeProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SharedInteractionMergeProbeComponent, InteractHandEvent>(OnTargetHand);
        SubscribeLocalEvent<SharedInteractionMergeProbeComponent, UserInteractHandEvent>(OnUserHand);
        SubscribeLocalEvent<SharedInteractionMergeProbeComponent, InteractUsingEvent>(OnTargetUsing);
        SubscribeLocalEvent<SharedInteractionMergeProbeComponent, UserInteractUsingEvent>(OnUserUsing);
        SubscribeLocalEvent<SharedInteractionMergeProbeComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<SharedInteractionMergeProbeComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<SharedInteractionMergeProbeComponent, DroppedEvent>(OnDropped);
        SubscribeLocalEvent<SharedInteractionMergeProbeComponent, RMCCombatModeInteractOverrideUserEvent>(OnCombatOverride);
    }

    private static void OnTargetHand(Entity<SharedInteractionMergeProbeComponent> ent, ref InteractHandEvent args)
    {
        ent.Comp.TargetHand++;
    }

    private static void OnUserHand(Entity<SharedInteractionMergeProbeComponent> ent, ref UserInteractHandEvent args)
    {
        ent.Comp.UserHand++;
        args.Handled = true;
    }

    private static void OnTargetUsing(Entity<SharedInteractionMergeProbeComponent> ent, ref InteractUsingEvent args)
    {
        ent.Comp.TargetUsing++;
    }

    private static void OnUserUsing(Entity<SharedInteractionMergeProbeComponent> ent, ref UserInteractUsingEvent args)
    {
        ent.Comp.UserUsing++;
        args.Handled = true;
    }

    private static void OnActivate(Entity<SharedInteractionMergeProbeComponent> ent, ref ActivateInWorldEvent args)
    {
        ent.Comp.Activated++;
        args.Handled = true;
    }

    private static void OnAfterInteract(Entity<SharedInteractionMergeProbeComponent> ent, ref AfterInteractEvent args)
    {
        ent.Comp.AfterInteract++;
    }

    private void OnDropped(Entity<SharedInteractionMergeProbeComponent> ent, ref DroppedEvent args)
    {
        ent.Comp.Dropped++;
        ent.Comp.RotationAtDrop = Transform(ent).LocalRotation;
    }

    private static void OnCombatOverride(
        Entity<SharedInteractionMergeProbeComponent> ent,
        ref RMCCombatModeInteractOverrideUserEvent args)
    {
        if (!ent.Comp.OverrideCombat)
            return;

        args.Handled = true;
        args.CanInteract = ent.Comp.CanCombatInteract;
    }
}

#pragma warning restore RA0002
