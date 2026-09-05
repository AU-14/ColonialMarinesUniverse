using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Lathe;
using Content.Server.Lathe.Components;
using Content.Server.Power.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Lathe;
using Content.Shared.Materials;
using Content.Shared.Power;
using Content.Shared.Research.Prototypes;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Lathe;

[TestFixture]
[TestOf(typeof(LatheSystem))]
public sealed class LatheQueueMergeRegressionTest : GameTest
{
    private const string Recipe = "LatheMergeRecipe";
    private const string OtherRecipe = "LatheMergeOtherRecipe";
    private const string InsufficientRecipe = "LatheMergeInsufficientRecipe";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: LatheMergeResult
  components:
  - type: Item
    size: Large

- type: latheRecipe
  id: LatheMergeRecipe
  result: LatheMergeResult
  completetime: 99
  materials:
    Steel: 10

- type: latheRecipe
  id: LatheMergeOtherRecipe
  result: LatheMergeResult
  completetime: 99
  materials:
    Steel: 20

- type: latheRecipe
  id: LatheMergeInsufficientRecipe
  result: LatheMergeResult
  materials:
    Steel: 10
    Plastic: 20

- type: latheRecipePack
  id: LatheMergePack
  recipes:
  - LatheMergeRecipe
  - LatheMergeOtherRecipe
  - LatheMergeInsufficientRecipe

- type: entity
  id: LatheMergeMachine
  components:
  - type: Lathe
    staticPacks: [ LatheMergePack ]
    maxQueue: 6
    materialUseMultiplier: 0.5
    timeMultiplier: 1.5
  - type: MaterialStorage
    storage:
      Steel: 100
      Plastic: 5
  - type: ApcPowerReceiver
  - type: Appearance
  - type: ReagentSpeed
    solution: speed
    cost: 1
    modifiers:
      Water: 0.5
  - type: SolutionContainerManager
    solutions:
      speed:
        maxVol: 2
        reagents:
        - ReagentId: Water
          Quantity: 2
  - type: UserInterface
    interfaces:
      enum.LatheUiKey.Key:
        type: LatheBoundUserInterface
";

    [SidedDependency(Side.Server)] private LatheSystem _lathe = default!;
    [SidedDependency(Side.Server)] private SharedMaterialStorageSystem _materials = default!;
    [SidedDependency(Side.Server)] private SharedSolutionContainerSystem _solutions = default!;
    [SidedDependency(Side.Server)] private UserInterfaceSystem _ui = default!;

    [Test]
    public async Task QueueCapCountsItemsAcrossBatchesAndStartFreesExactlyOneSlot()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var lathe = SEntMan.SpawnEntity("LatheMergeMachine", map.GridCoords);
            var user = SEntMan.SpawnEntity(null, map.GridCoords);
            var component = SEntMan.GetComponent<LatheComponent>(lathe);
            var power = SEntMan.GetComponent<ApcPowerReceiverComponent>(lathe);

            Queue(lathe, user, Recipe, 10);
            Assert.Multiple(() =>
            {
                Assert.That(component.Queue, Has.Count.EqualTo(1),
                    "same-recipe batching must not turn the six-item cap into six batches");
                Assert.That(component.Queue.First!.Value.ItemsRequested, Is.EqualTo(6));
                Assert.That(component.Queue.First.Value.ItemsPrinted, Is.Zero);
                Assert.That(Pending(component), Is.EqualTo(6));
                Assert.That(Material(lathe, "Steel"), Is.EqualTo(70),
                    "the accepted six discounted items must debit exactly 6 * 5 material");
            });

            AssertUi(lathe, Recipe, Recipe, 0, 6);

            Queue(lathe, user, Recipe, 10);
            Assert.Multiple(() =>
            {
                Assert.That(Pending(component), Is.EqualTo(6));
                Assert.That(Material(lathe, "Steel"), Is.EqualTo(70),
                    "a full queue request must not debit any material");
            });

            power.NeedsPower = false;
            power.Powered = true;
            Assert.That(_lathe.TryStartProducing(lathe, component), Is.True);
            var producing = SEntMan.GetComponent<LatheProducingComponent>(lathe);
            Assert.Multiple(() =>
            {
                Assert.That(component.CurrentRecipe, Is.EqualTo((ProtoId<LatheRecipePrototype>) Recipe));
                Assert.That(Pending(component), Is.EqualTo(5));
                Assert.That(producing.ProductionLength, Is.EqualTo(TimeSpan.FromSeconds(3)),
                    "Large LatheTime 4 must override recipe time 99, then Water x0.5 and TimeMultiplier x1.5 apply");
                Assert.That(_solutions.TryGetSolution(lathe, "speed", out _, out var speed), Is.True);
                Assert.That(speed!.GetTotalPrototypeQuantity("Water"), Is.EqualTo(FixedPoint2.New(1)));
            });

            Queue(lathe, user, Recipe, 10);
            Assert.Multiple(() =>
            {
                Assert.That(component.Queue, Has.Count.EqualTo(1));
                Assert.That(component.Queue.First!.Value.ItemsRequested, Is.EqualTo(7));
                Assert.That(component.Queue.First.Value.ItemsPrinted, Is.EqualTo(1));
                Assert.That(Pending(component), Is.EqualTo(6),
                    "starting one item must make room for exactly one new pending item");
                Assert.That(Material(lathe, "Steel"), Is.EqualTo(65));
            });
            AssertUi(lathe, Recipe, Recipe, 1, 7);
        });
    }

    [Test]
    public async Task InsufficientMaterialsAreAtomicAndDeleteMoveRefundOnlyPendingItems()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var insufficientLathe = SEntMan.SpawnEntity("LatheMergeMachine", map.GridCoords);
            var insufficient = SEntMan.GetComponent<LatheComponent>(insufficientLathe);
            var recipe = SProtoMan.Index<LatheRecipePrototype>(InsufficientRecipe);
            Assert.Multiple(() =>
            {
                Assert.That(_lathe.TryAddToQueue(insufficientLathe, recipe, 1, insufficient), Is.False);
                Assert.That(insufficient.Queue, Is.Empty);
                Assert.That(Material(insufficientLathe, "Steel"), Is.EqualTo(100));
                Assert.That(Material(insufficientLathe, "Plastic"), Is.EqualTo(5),
                    "failure on the second material must not partially debit the first");
            });

            var lathe = SEntMan.SpawnEntity("LatheMergeMachine", map.GridCoords);
            var user = SEntMan.SpawnEntity(null, map.GridCoords);
            var component = SEntMan.GetComponent<LatheComponent>(lathe);
            Assert.That(_lathe.TryAddToQueue(lathe, SProtoMan.Index<LatheRecipePrototype>(Recipe), 2, component), Is.True);
            Assert.That(_lathe.TryAddToQueue(lathe, SProtoMan.Index<LatheRecipePrototype>(OtherRecipe), 3, component), Is.True);
            Assert.That(Material(lathe, "Steel"), Is.EqualTo(60));

            var move = new LatheMoveRequestMessage(1, -1) { Actor = user };
            _lathe.OnLatheMoveRequestMessage(lathe, component, ref move);
            Assert.That(component.Queue.Select(batch => (string) batch.Recipe),
                Is.EqualTo(new[] { OtherRecipe, Recipe }));
            AssertUi(lathe, OtherRecipe, OtherRecipe, 0, 3, Recipe, 0, 2);

            var delete = new LatheDeleteRequestMessage(0) { Actor = user };
            _lathe.OnLatheDeleteRequestMessage(lathe, component, ref delete);
            Assert.Multiple(() =>
            {
                Assert.That(component.Queue.Select(batch => (string) batch.Recipe),
                    Is.EqualTo(new[] { Recipe }));
                Assert.That(Material(lathe, "Steel"), Is.EqualTo(90),
                    "deleting the three-item adjusted-cost10 batch must refund exactly 30");
            });
            AssertUi(lathe, Recipe, Recipe, 0, 2);
        });
    }

    [Test]
    public async Task PowerLossRequeuesWithoutRefundAndUserAbortRefundsWithoutRequeue()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity(null, map.GridCoords);
            var powerLossLathe = SEntMan.SpawnEntity("LatheMergeMachine", map.GridCoords);
            var powerLoss = SEntMan.GetComponent<LatheComponent>(powerLossLathe);
            Power(powerLossLathe, true);
            Assert.That(_lathe.TryAddToQueue(
                powerLossLathe,
                SProtoMan.Index<LatheRecipePrototype>(Recipe),
                1,
                powerLoss), Is.True);
            Assert.That(_lathe.TryStartProducing(powerLossLathe, powerLoss), Is.True);
            Assert.That(powerLoss.Queue, Is.Empty,
                "starting a one-item batch must exercise the empty-queue power-loss branch");
            Assert.That(Material(powerLossLathe, "Steel"), Is.EqualTo(95));

            Power(powerLossLathe, false);
            var lostPower = new PowerChangedEvent(false, 0);
            SEntMan.EventBus.RaiseLocalEvent(powerLossLathe, ref lostPower);
            Assert.Multiple(() =>
            {
                Assert.That(powerLoss.CurrentRecipe, Is.Null);
                Assert.That(powerLoss.Queue, Has.Count.EqualTo(1));
                Assert.That(Pending(powerLoss), Is.EqualTo(1));
                Assert.That(Material(powerLossLathe, "Steel"), Is.EqualTo(95),
                    "power loss pauses and requeues the paid item without refunding it");
            });

            var delete = new LatheDeleteRequestMessage(0) { Actor = user };
            _lathe.OnLatheDeleteRequestMessage(powerLossLathe, powerLoss, ref delete);
            Assert.Multiple(() =>
            {
                Assert.That(powerLoss.Queue, Is.Empty);
                Assert.That(Material(powerLossLathe, "Steel"), Is.EqualTo(100),
                    "deleting the paused item must return exactly the original debit, with no arbitrage");
            });

            var resumeLathe = SEntMan.SpawnEntity("LatheMergeMachine", map.GridCoords);
            var resume = SEntMan.GetComponent<LatheComponent>(resumeLathe);
            Power(resumeLathe, true);
            Assert.That(_lathe.TryAddToQueue(
                resumeLathe,
                SProtoMan.Index<LatheRecipePrototype>(Recipe),
                1,
                resume), Is.True);
            Assert.That(_lathe.TryStartProducing(resumeLathe, resume), Is.True);
            _lathe.AbortProduction(resumeLathe, resume);
            Assert.That(Material(resumeLathe, "Steel"), Is.EqualTo(95));
            Assert.That(Pending(resume), Is.EqualTo(1));
            Assert.That(_lathe.TryStartProducing(resumeLathe, resume), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(resume.Queue, Is.Empty);
                Assert.That(resume.CurrentRecipe, Is.EqualTo((ProtoId<LatheRecipePrototype>) Recipe));
                Assert.That(Material(resumeLathe, "Steel"), Is.EqualTo(95),
                    "resuming a paid item must not debit it a second time");
            });

            var userAbortLathe = SEntMan.SpawnEntity("LatheMergeMachine", map.GridCoords);
            var userAbort = SEntMan.GetComponent<LatheComponent>(userAbortLathe);
            Power(userAbortLathe, true);
            Assert.That(_lathe.TryAddToQueue(
                userAbortLathe,
                SProtoMan.Index<LatheRecipePrototype>(Recipe),
                1,
                userAbort), Is.True);
            Assert.That(_lathe.TryStartProducing(userAbortLathe, userAbort), Is.True);
            var abort = new LatheAbortFabricationMessage { Actor = user };
            _lathe.OnLatheAbortFabricationMessage(userAbortLathe, userAbort, ref abort);
            Assert.Multiple(() =>
            {
                Assert.That(userAbort.CurrentRecipe, Is.Null);
                Assert.That(userAbort.Queue, Is.Empty,
                    "manual cancellation refunds the in-flight item instead of requeuing it");
                Assert.That(Material(userAbortLathe, "Steel"), Is.EqualTo(100));
            });
        });
    }

    private void Queue(EntityUid lathe, EntityUid user, string recipe, int quantity)
    {
        var message = new LatheQueueRecipeMessage(recipe, quantity) { Actor = user };
        SEntMan.EventBus.RaiseLocalEvent(lathe, message);
    }

    private int Material(EntityUid lathe, string material)
    {
        return _materials.GetMaterialAmount(lathe, material);
    }

    private static int Pending(LatheComponent component)
    {
        return component.Queue.Sum(batch => Math.Max(0, batch.ItemsRequested - batch.ItemsPrinted));
    }

    private void AssertUi(
        EntityUid lathe,
        string current,
        string firstRecipe,
        int firstPrinted,
        int firstRequested,
        string? secondRecipe = null,
        int secondPrinted = 0,
        int secondRequested = 0)
    {
        Assert.That(_ui.TryGetUiState<LatheUpdateState>(lathe, LatheUiKey.Key, out var state), Is.True);
        Assert.That(state!.CurrentlyProducing, Is.EqualTo((ProtoId<LatheRecipePrototype>) current));
        Assert.That(state.Queue[0].Recipe, Is.EqualTo((ProtoId<LatheRecipePrototype>) firstRecipe));
        Assert.That(state.Queue[0].ItemsPrinted, Is.EqualTo(firstPrinted));
        Assert.That(state.Queue[0].ItemsRequested, Is.EqualTo(firstRequested));
        if (secondRecipe == null)
        {
            Assert.That(state.Queue, Has.Length.EqualTo(1));
            return;
        }

        Assert.That(state.Queue, Has.Length.EqualTo(2));
        Assert.That(state.Queue[1].Recipe, Is.EqualTo((ProtoId<LatheRecipePrototype>) secondRecipe));
        Assert.That(state.Queue[1].ItemsPrinted, Is.EqualTo(secondPrinted));
        Assert.That(state.Queue[1].ItemsRequested, Is.EqualTo(secondRequested));
    }

    private void Power(EntityUid lathe, bool powered)
    {
        var receiver = SEntMan.GetComponent<ApcPowerReceiverComponent>(lathe);
        receiver.NeedsPower = !powered;
        receiver.Powered = powered;
    }
}
