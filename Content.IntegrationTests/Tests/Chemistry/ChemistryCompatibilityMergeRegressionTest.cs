using System.Linq;
using Content.Client.Chemistry.EntitySystems;
using Content.IntegrationTests.Fixtures;
using Content.Server.Cargo.Components;
using Content.Server.Nutrition.Components;
using Content.Shared._RMC14.Chemistry;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Events;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using ClientPopupSystem = Content.Client.Popups.PopupSystem;

namespace Content.IntegrationTests.Tests.Chemistry;

[TestFixture]
public sealed class ChemistryCompatibilityMergeRegressionTest : GameTest
{
    private static readonly Robust.Shared.Prototypes.EntProtoId ChemistryMergeCollisionManagerPrototype = "ChemistryMergeCollisionManager";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: ChemistryMergeInlineManager
  components:
  - type: SolutionContainerManager
    solutions:
      inline:
        maxVol: 10
        reagents:
        - ReagentId: Water
          Quantity: 4

- type: entity
  id: ChemistryMergeStoredManager
  components:
  - type: SolutionContainerManager
    containers:
    - stored

- type: entity
  id: ChemistryMergeStoredSolution
  components:
  - type: Solution
    id: stored
    solution:
      maxVol: 12
      reagents:
      - ReagentId: WeldingFuel
        Quantity: 6

- type: entity
  id: ChemistryMergeCollisionNewSolution
  components:
  - type: Solution
    id: collision
    solution:
      maxVol: 20
      reagents:
      - ReagentId: Water
        Quantity: 1

- type: entity
  id: ChemistryMergeCollisionManager
  components:
  - type: SolutionManager
    solutions: [ChemistryMergeCollisionNewSolution]
  - type: SolutionContainerManager
    solutions:
      collision:
        maxVol: 12
        reagents:
        - ReagentId: WeldingFuel
          Quantity: 6

- type: entity
  id: ChemistryMergeSourceSolution
  components:
  - type: Solution
    id: source
    solution:
      maxVol: 20
      reagents:
      - ReagentId: Water
        Quantity: 10

- type: entity
  id: ChemistryMergeMixedSolution
  components:
  - type: Solution
    id: mixed
    solution:
      maxVol: 20
      reagents:
      - ReagentId: Water
        Quantity: 1
      - ReagentId: WeldingFuel
        Quantity: 1

- type: entity
  id: ChemistryMergeEmptySolution
  components:
  - type: Solution
    id: target
    solution:
      maxVol: 20

- type: entity
  id: ChemistryMergeSource
  components:
  - type: Item
  - type: SolutionManager
    solutions: [ChemistryMergeSourceSolution]
  - type: DrainableSolution
    solution: source
    drainTime: 0.2
  - type: SolutionTransfer
    transferAmount: 7
    canChangeTransferAmount: true
    transferAmounts: [2, 7, 11]
  - type: NoMixingReagents

- type: entity
  id: ChemistryMergeMixedTarget
  components:
  - type: SolutionManager
    solutions: [ChemistryMergeMixedSolution]
  - type: RefillableSolution
    solution: mixed

- type: entity
  id: ChemistryMergeEmptyTarget
  components:
  - type: SolutionManager
    solutions: [ChemistryMergeEmptySolution]
  - type: RefillableSolution
    solution: target
    refillTime: 0.1
  - type: ChemistryMergeTransferProbe

- type: entity
  id: ChemistryMergeRefillHeldSolution
  components:
  - type: Solution
    id: held
    solution:
      maxVol: 20

- type: entity
  id: ChemistryMergeRefillHeld
  components:
  - type: Item
  - type: SolutionManager
    solutions: [ChemistryMergeRefillHeldSolution]
  - type: RefillableSolution
    solution: held
    refillTime: 0.1
  - type: SolutionTransfer
    transferAmount: 7
    canSend: false
    canReceive: true

- type: entity
  id: ChemistryMergeHypospraySolution
  components:
  - type: Solution
    id: hypospray
    solution:
      maxVol: 20
      reagents:
      - ReagentId: Water
        Quantity: 10

- type: entity
  id: ChemistryMergeHypospray
  components:
  - type: SolutionManager
    solutions: [ChemistryMergeHypospraySolution]
  - type: Hypospray
    transferAmount: 5
    onlyAffectsMobs: false
    canContainerDraw: false

- type: entity
  id: ChemistryMergeInjectableSolution
  components:
  - type: Solution
    id: inject
    solution:
      maxVol: 20

- type: entity
  id: ChemistryMergeInjectableTarget
  components:
  - type: SolutionManager
    solutions: [ChemistryMergeInjectableSolution]
  - type: InjectableSolution
    solution: inject
  - type: ChemistryMergeBeforeInjectProbe

- type: injectorMode
  parent: [BaseHyposprayMode, BaseInjectMode]
  id: ChemistryMergeLongInjectMode
  mobTime: 4

- type: entity
  parent: BaseHypoInjector
  id: ChemistryMergeLongInjector
  components:
  - type: Injector
    activeModeProtoId: ChemistryMergeLongInjectMode
    allowedModes: [ChemistryMergeLongInjectMode]
  - type: MedicallyUnskilledDoAfter
    min: 2
    doAfter: 3
  - type: ChemistryMergeInjectorProbe
";

    [Test]
    public async Task LegacyManagerMigratesInlineAndStoredSolutionsWithoutLosingNewManager()
    {
        EntityUid inline = default;
        EntityUid stored = default;
        EntityUid storedSolution = default;
        EntityUid collision = default;

        await Server.WaitPost(() =>
        {
            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            var collisionPrototype = SProtoMan.Index<EntityPrototype>(ChemistryMergeCollisionManagerPrototype);
            Assert.That(solutions.TryGetSolution(collisionPrototype, "collision", out var collisionSolution), Is.True);
            var enumerated = solutions.EnumerateSolutions(collisionPrototype).ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(collisionSolution!.MaxVolume, Is.EqualTo(FixedPoint2.New(12)));
                Assert.That(collisionSolution.Volume, Is.EqualTo(FixedPoint2.New(6)));
                Assert.That(collisionSolution.ContainsReagent(new ReagentId("WeldingFuel", null)), Is.True);
                Assert.That(collisionSolution.ContainsReagent(new ReagentId("Water", null)), Is.False);
                Assert.That(enumerated, Has.Length.EqualTo(1));
                Assert.That(enumerated[0].Id, Is.EqualTo("collision"));
                Assert.That(enumerated[0].Solution.MaxVolume, Is.EqualTo(FixedPoint2.New(12)));
                Assert.That(enumerated[0].Solution.ContainsReagent(new ReagentId("WeldingFuel", null)), Is.True);
            });

            inline = SEntMan.Spawn("ChemistryMergeInlineManager");
            collision = SEntMan.Spawn("ChemistryMergeCollisionManager");

            stored = SEntMan.CreateEntityUninitialized("ChemistryMergeStoredManager");
            var storedMeta = SEntMan.GetComponent<MetaDataComponent>(stored);
            SEntMan.InitializeAndStartEntity(stored, doMapInit: false);
            storedSolution = SEntMan.Spawn("ChemistryMergeStoredSolution", doMapInit: false);

            var containers = SEntMan.System<SharedContainerSystem>();
            var oldSlot = containers.EnsureContainer<ContainerSlot>(stored, "solution@stored");
            Assert.That(containers.Insert(storedSolution, oldSlot), Is.True);

            SEntMan.RunMapInit(stored, storedMeta);
        });
        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            var solutions = SEntMan.System<SharedSolutionContainerSystem>();

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<SolutionManagerComponent>(inline), Is.True);
#pragma warning disable CS0612 // CMU14: Regression coverage intentionally inspects the legacy solution schema.
                Assert.That(SEntMan.HasComponent<SolutionContainerManagerComponent>(inline), Is.False);
#pragma warning restore CS0612
                Assert.That(solutions.TryGetSolution(inline, "inline", out _, out var inlineSolution), Is.True);
                Assert.That(inlineSolution!.Volume, Is.EqualTo(FixedPoint2.New(4)));

                Assert.That(SEntMan.HasComponent<SolutionManagerComponent>(stored), Is.True);
#pragma warning disable CS0612 // CMU14: Regression coverage intentionally inspects the legacy solution schema.
                Assert.That(SEntMan.HasComponent<SolutionContainerManagerComponent>(stored), Is.False);
#pragma warning restore CS0612
                Assert.That(solutions.TryGetSolution(stored, "stored", out var migrated, out var storedState), Is.True);
                Assert.That(migrated!.Value.Owner, Is.EqualTo(storedSolution));
                Assert.That(storedState!.Volume, Is.EqualTo(FixedPoint2.New(6)));

                Assert.That(SEntMan.HasComponent<SolutionManagerComponent>(collision), Is.True);
#pragma warning disable CS0612 // CMU14: Regression coverage intentionally inspects the legacy solution schema.
                Assert.That(SEntMan.HasComponent<SolutionContainerManagerComponent>(collision), Is.False);
#pragma warning restore CS0612
                Assert.That(solutions.TryGetSolution(collision, "collision", out _, out var collisionState), Is.True);
                Assert.That(collisionState!.MaxVolume, Is.EqualTo(FixedPoint2.New(12)));
                Assert.That(collisionState.Volume, Is.EqualTo(FixedPoint2.New(6)));
                Assert.That(collisionState.ContainsReagent(new ReagentId("WeldingFuel", null)), Is.True);
                Assert.That(collisionState.ContainsReagent(new ReagentId("Water", null)), Is.False);
            });
        });
    }

    [Test]
    public async Task NullableTransferUsesDirectionalSolutionsAndDelayedPathsRecheckCapabilities()
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default;
        EntityUid source = default;
        EntityUid mixed = default;
        EntityUid empty = default;
        EntityUid refillHeld = default;

        await Server.WaitPost(() =>
        {
            _ = SEntMan.System<ChemistryMergeTransferProbeSystem>();
            user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            source = SEntMan.SpawnEntity("ChemistryMergeSource", map.GridCoords);
            mixed = SEntMan.SpawnEntity("ChemistryMergeMixedTarget", map.GridCoords);
            empty = SEntMan.SpawnEntity("ChemistryMergeEmptyTarget", map.GridCoords);
            refillHeld = SEntMan.SpawnEntity("ChemistryMergeRefillHeld", map.GridCoords);
            var hands = SEntMan.System<SharedHandsSystem>();
            Assert.That(hands.TryPickupAnyHand(user, source, checkActionBlocker: false), Is.True);
            Assert.That(hands.TryPickupAnyHand(user, refillHeld, checkActionBlocker: false), Is.True);
        });

        await Server.WaitAssertion(() =>
        {
            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            var transfer = SEntMan.System<SolutionTransferSystem>();
            Assert.That(solutions.TryGetSolution(source, "source", out var sourceSolution, out var sourceState), Is.True);
            Assert.That(solutions.TryGetSolution(mixed, "mixed", out var mixedSolution, out var mixedState), Is.True);
            Assert.That(solutions.TryGetSolution(empty, "target", out var emptySolution, out var emptyState), Is.True);

            // This only cancels if FromSolution is the one-reagent source and ToSolution is the two-reagent target.
            Assert.That(transfer.Transfer(null, source, sourceSolution!.Value, mixed, mixedSolution!.Value, 4),
                Is.EqualTo(FixedPoint2.Zero));
            Assert.Multiple(() =>
            {
                Assert.That(sourceState!.Volume, Is.EqualTo(FixedPoint2.New(10)));
                Assert.That(mixedState!.Volume, Is.EqualTo(FixedPoint2.New(2)));
            });

            SEntMan.RemoveComponent<NoMixingReagentsComponent>(source);
            var data = new SolutionTransferData(null, source, sourceSolution.Value, empty, emptySolution!.Value, 4);
            Assert.That(transfer.Transfer(data),
                Is.EqualTo(FixedPoint2.New(4)));
            var probe = SEntMan.GetComponent<ChemistryMergeTransferProbeComponent>(empty);
            Assert.Multiple(() =>
            {
                Assert.That(sourceState.Volume, Is.EqualTo(FixedPoint2.New(6)));
                Assert.That(emptyState!.Volume, Is.EqualTo(FixedPoint2.New(4)));
                Assert.That(probe.Events, Is.EqualTo(1));
                Assert.That(probe.LastUser, Is.Null);
            });

            var transferComp = SEntMan.GetComponent<SolutionTransferComponent>(source);
            var drainable = SEntMan.GetComponent<DrainableSolutionComponent>(source);
            Assert.Multiple(() =>
            {
                Assert.That(transferComp.TransferAmounts, Is.EqualTo(new FixedPoint2[] { 2, 7, 11 }));
                Assert.That(drainable.DrainTime, Is.EqualTo(TimeSpan.FromSeconds(0.2)));
            });

            // Restore the original volumes so the timed paths have exact, independent expectations.
            sourceSolution.Value.Comp.Solution = new Solution("Water", 10) { MaxVolume = 20 };
            emptySolution.Value.Comp.Solution = new Solution { MaxVolume = 20 };

            var drain = new AfterInteractEvent(user, source, empty, default, true);
            SEntMan.EventBus.RaiseLocalEvent(source, drain);
            Assert.That(drain.Handled, Is.True);
            transferComp.CanSend = false;
        });
        await Pair.RunTicksSync(30);

        await Server.WaitAssertion(() =>
        {
            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(source, "source", out _, out var sourceState), Is.True);
            Assert.That(solutions.TryGetSolution(empty, "target", out _, out var emptyState), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(sourceState!.Volume, Is.EqualTo(FixedPoint2.New(10)));
                Assert.That(emptyState!.Volume, Is.EqualTo(FixedPoint2.Zero));
            });

            var transferComp = SEntMan.GetComponent<SolutionTransferComponent>(source);
            transferComp.CanSend = true;
            var drain = new AfterInteractEvent(user, source, empty, default, true);
            SEntMan.EventBus.RaiseLocalEvent(source, drain);
            Assert.That(drain.Handled, Is.True);
        });
        await Pair.RunTicksSync(30);

        await Server.WaitAssertion(() =>
        {
            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(source, "source", out _, out var sourceState), Is.True);
            Assert.That(solutions.TryGetSolution(empty, "target", out _, out var emptyState), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(sourceState!.Volume, Is.EqualTo(FixedPoint2.New(3)));
                Assert.That(emptyState!.Volume, Is.EqualTo(FixedPoint2.New(7)));
            });

            var heldTransfer = SEntMan.GetComponent<SolutionTransferComponent>(refillHeld);
            var hands = SEntMan.System<SharedHandsSystem>();
            Assert.That(hands.IsHolding(user, refillHeld, out var refillHand), Is.True);
            hands.TrySetActiveHand(user, refillHand);
            Assert.That(hands.GetActiveItem(user), Is.EqualTo(refillHeld));
            var refill = new AfterInteractEvent(user, refillHeld, source, default, true);
            SEntMan.EventBus.RaiseLocalEvent(refillHeld, refill);
            Assert.That(refill.Handled, Is.True);
            Assert.That(SEntMan.GetComponent<DoAfterComponent>(user).DoAfters.Values.Any(doAfter =>
                    !doAfter.Cancelled &&
                    !doAfter.Completed &&
                    doAfter.Args.Used == refillHeld &&
                    doAfter.Args.Event is SolutionRefillTransferDoAfterEvent),
                Is.True,
                "refilling must snapshot the held refill container from the active hand");
            heldTransfer.CanReceive = false;
        });
        await Pair.RunTicksSync(30);

        await Server.WaitAssertion(() =>
        {
            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(source, "source", out _, out var sourceState), Is.True);
            Assert.That(solutions.TryGetSolution(refillHeld, "held", out _, out var heldState), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(sourceState!.Volume, Is.EqualTo(FixedPoint2.New(3)));
                Assert.That(heldState!.Volume, Is.EqualTo(FixedPoint2.Zero));
            });

            var heldTransfer = SEntMan.GetComponent<SolutionTransferComponent>(refillHeld);
            heldTransfer.CanReceive = true;
            var hands = SEntMan.System<SharedHandsSystem>();
            Assert.That(hands.IsHolding(user, refillHeld, out var refillHand), Is.True);
            hands.TrySetActiveHand(user, refillHand);
            Assert.That(hands.GetActiveItem(user), Is.EqualTo(refillHeld));
            var refill = new AfterInteractEvent(user, refillHeld, source, default, true);
            SEntMan.EventBus.RaiseLocalEvent(refillHeld, refill);
            Assert.That(refill.Handled, Is.True);
            Assert.That(SEntMan.GetComponent<DoAfterComponent>(user).DoAfters.Values.Any(doAfter =>
                    !doAfter.Cancelled &&
                    !doAfter.Completed &&
                    doAfter.Args.Used == refillHeld &&
                    doAfter.Args.Event is SolutionRefillTransferDoAfterEvent),
                Is.True,
                "the enabled refill must start from the same active held container");
        });
        await Pair.RunTicksSync(30);

        await Server.WaitAssertion(() =>
        {
            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(source, "source", out _, out var sourceState), Is.True);
            Assert.That(solutions.TryGetSolution(refillHeld, "held", out _, out var heldState), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(sourceState!.Volume, Is.EqualTo(FixedPoint2.Zero));
                Assert.That(heldState!.Volume, Is.EqualTo(FixedPoint2.New(3)));
            });
        });
    }

    [Test]
    public async Task RetainedHyposprayUsesUnifiedCancellationRedirectAndOverridePriority()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;
        var originalAttached = session.AttachedEntity;
        EntityUid user = default;
        EntityUid selfCancelledHypospray = default;
        EntityUid targetCancelledHypospray = default;
        EntityUid successfulHypospray = default;
        EntityUid initialTarget = default;
        EntityUid cancelledTarget = default;
        EntityUid redirectedTarget = default;
        EntityUid finalTarget = default;

        try
        {
            await Server.WaitPost(() =>
            {
                _ = SEntMan.System<ChemistryMergeBeforeInjectProbeSystem>();
                user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                SEntMan.EnsureComponent<ChemistryMergeBeforeInjectProbeComponent>(user);
                selfCancelledHypospray = SEntMan.SpawnEntity("ChemistryMergeHypospray", map.GridCoords);
                targetCancelledHypospray = SEntMan.SpawnEntity("ChemistryMergeHypospray", map.GridCoords);
                successfulHypospray = SEntMan.SpawnEntity("ChemistryMergeHypospray", map.GridCoords);
                initialTarget = SEntMan.SpawnEntity("ChemistryMergeInjectableTarget", map.GridCoords);
                cancelledTarget = SEntMan.SpawnEntity("ChemistryMergeInjectableTarget", map.GridCoords);
                redirectedTarget = SEntMan.SpawnEntity("ChemistryMergeInjectableTarget", map.GridCoords);
                finalTarget = SEntMan.SpawnEntity("ChemistryMergeInjectableTarget", map.GridCoords);
                Server.PlayerMan.SetAttachedEntity(session, user);
            });
            await Pair.RunUntilSynced();

            await Server.WaitPost(() =>
            {
                var probe = SEntMan.GetComponent<ChemistryMergeBeforeInjectProbeComponent>(user);
                probe.CancelSelf = true;
                probe.SelfOverride = "chemistry merge localized self cancellation";

                Assert.That(TryInject(selfCancelledHypospray, initialTarget, user), Is.False);
                Assert.Multiple(() =>
                {
                    Assert.That(GetSolutionVolume(selfCancelledHypospray, "hypospray"), Is.EqualTo(FixedPoint2.New(10)));
                    Assert.That(GetSolutionVolume(initialTarget, "inject"), Is.EqualTo(FixedPoint2.Zero));
                    Assert.That(probe.SelfEvents, Is.EqualTo(1));
                    Assert.That(SEntMan.GetComponent<ChemistryMergeBeforeInjectProbeComponent>(initialTarget).TargetEvents,
                        Is.Zero);
                });
            });
            await Pair.RunTicksSync(5);
            await AssertPopup("chemistry merge localized self cancellation", true);

            await Server.WaitPost(() =>
            {
                var selfProbe = SEntMan.GetComponent<ChemistryMergeBeforeInjectProbeComponent>(user);
                selfProbe.CancelSelf = false;
                selfProbe.SelfOverride = "chemistry merge self success override";

                var targetProbe = SEntMan.GetComponent<ChemistryMergeBeforeInjectProbeComponent>(cancelledTarget);
                targetProbe.CancelTarget = true;
                targetProbe.TargetOverride = "chemistry merge localized target cancellation";

                Assert.That(TryInject(targetCancelledHypospray, cancelledTarget, user), Is.False);
                Assert.Multiple(() =>
                {
                    Assert.That(GetSolutionVolume(targetCancelledHypospray, "hypospray"), Is.EqualTo(FixedPoint2.New(10)));
                    Assert.That(GetSolutionVolume(cancelledTarget, "inject"), Is.EqualTo(FixedPoint2.Zero));
                    Assert.That(selfProbe.SelfEvents, Is.EqualTo(2));
                    Assert.That(targetProbe.TargetEvents, Is.EqualTo(1));
                });
            });
            await Pair.RunTicksSync(5);
            await AssertPopup("chemistry merge localized target cancellation", true);

            await Server.WaitPost(() =>
            {
                var selfProbe = SEntMan.GetComponent<ChemistryMergeBeforeInjectProbeComponent>(user);
                selfProbe.SelfRedirect = redirectedTarget;

                var targetProbe = SEntMan.GetComponent<ChemistryMergeBeforeInjectProbeComponent>(redirectedTarget);
                targetProbe.TargetRedirect = finalTarget;
                targetProbe.TargetOverride = "chemistry merge target success override";

                Assert.That(TryInject(successfulHypospray, initialTarget, user), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(GetSolutionVolume(successfulHypospray, "hypospray"), Is.EqualTo(FixedPoint2.New(5)));
                    Assert.That(GetSolutionVolume(initialTarget, "inject"), Is.EqualTo(FixedPoint2.Zero));
                    Assert.That(GetSolutionVolume(redirectedTarget, "inject"), Is.EqualTo(FixedPoint2.Zero));
                    Assert.That(GetSolutionVolume(finalTarget, "inject"), Is.EqualTo(FixedPoint2.New(5)));
                    Assert.That(selfProbe.SelfEvents, Is.EqualTo(3));
                    Assert.That(targetProbe.TargetEvents, Is.EqualTo(1));
                    Assert.That(SEntMan.GetComponent<ChemistryMergeBeforeInjectProbeComponent>(finalTarget).TargetEvents,
                        Is.Zero,
                        "TargetBeforeInjectEvent is raised on the pre-redirect target, then its mutable target is honored.");
                });
            });
            await Pair.RunTicksSync(5);
            await AssertPopup("chemistry merge target success override", true);
            await AssertPopup("chemistry merge self success override", false);
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, originalAttached));
        }
    }

    private bool TryInject(EntityUid injector, EntityUid target, EntityUid user)
    {
        var component = SEntMan.GetComponent<HyposprayComponent>(injector);
        return SEntMan.System<HypospraySystem>().TryDoInject((injector, component), target, user, doAfter: false);
    }

    private FixedPoint2 GetSolutionVolume(EntityUid owner, string name)
    {
        Assert.That(SEntMan.System<SharedSolutionContainerSystem>()
            .TryGetSolution(owner, name, out _, out var solution), Is.True);
        return solution!.Volume;
    }

    private async Task AssertPopup(string message, bool expected)
    {
        await Client.WaitAssertion(() =>
        {
            var labels = CEntMan.System<ClientPopupSystem>().WorldLabels;
            Assert.That(labels.Any(label => label.Text == message), Is.EqualTo(expected), message);
        });
    }

}

[RegisterComponent]
public sealed partial class ChemistryMergeTransferProbeComponent : Component
{
    public int Events;
    public EntityUid? LastUser;
}

public sealed class ChemistryMergeTransferProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<ChemistryMergeTransferProbeComponent, SolutionTransferredEvent>(OnTransferred);
    }

    private void OnTransferred(Entity<ChemistryMergeTransferProbeComponent> entity, ref SolutionTransferredEvent args)
    {
        entity.Comp.Events++;
        entity.Comp.LastUser = args.User;
    }
}

[RegisterComponent]
public sealed partial class ChemistryMergeInjectorProbeComponent : Component
{
    public int Events;
    public TimeSpan LastAdjustedDelay;
}

public sealed class ChemistryMergeInjectorProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<ChemistryMergeInjectorProbeComponent, AttemptHyposprayUseEvent>(
            OnAttempt,
            after: [typeof(SkillsSystem)]);
    }

    private void OnAttempt(Entity<ChemistryMergeInjectorProbeComponent> entity, ref AttemptHyposprayUseEvent args)
    {
        entity.Comp.Events++;
        entity.Comp.LastAdjustedDelay = args.DoAfter;
    }
}

[RegisterComponent]
public sealed partial class ChemistryMergeBeforeInjectProbeComponent : Component
{
    public bool CancelSelf;
    public bool CancelTarget;
    public EntityUid? SelfRedirect;
    public EntityUid? TargetRedirect;
    public string? SelfOverride;
    public string? TargetOverride;
    public int SelfEvents;
    public int TargetEvents;
}

public sealed class ChemistryMergeBeforeInjectProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<ChemistryMergeBeforeInjectProbeComponent, SelfBeforeInjectEvent>(OnSelf);
        SubscribeLocalEvent<ChemistryMergeBeforeInjectProbeComponent, TargetBeforeInjectEvent>(OnTarget);
    }

    private void OnSelf(Entity<ChemistryMergeBeforeInjectProbeComponent> entity, ref SelfBeforeInjectEvent args)
    {
        entity.Comp.SelfEvents++;
        if (entity.Comp.SelfRedirect != null)
            args.TargetGettingInjected = entity.Comp.SelfRedirect.Value;

        args.OverrideMessage = entity.Comp.SelfOverride;
        if (entity.Comp.CancelSelf)
            args.Cancel();
    }

    private void OnTarget(Entity<ChemistryMergeBeforeInjectProbeComponent> entity, ref TargetBeforeInjectEvent args)
    {
        entity.Comp.TargetEvents++;
        if (entity.Comp.TargetRedirect != null)
            args.TargetGettingInjected = entity.Comp.TargetRedirect.Value;

        args.OverrideMessage = entity.Comp.TargetOverride;
        if (entity.Comp.CancelTarget)
            args.Cancel();
    }
}
