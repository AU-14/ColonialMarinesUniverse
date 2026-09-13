using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Nutrition;

public sealed class ToolRefinableButcheringTest : InteractionTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: BaseItem
  id: ToolRefinableButcheringTestTool
  components:
  - type: Tool
    qualities:
    - Slicing

- type: entity
  parent: BaseItem
  id: ToolRefinableButcheringTestResult

- type: entity
  parent: BaseSlicingRefinable
  id: ToolRefinableButcheringTestTarget
  components:
  - type: Butcherable
    butcheringType: Knife
    spawned:
    - id: ToolRefinableButcheringTestResult
      amount: 1
  - type: ToolRefinable
    refineTime: 1
    popupType: LargeCaution
    refineResult:
    - id: ToolRefinableButcheringTestResult
      amount: 1

- type: entity
  parent: ToolRefinableButcheringTestTarget
  id: ToolRefinableButcheringWaitTestTarget
  components:
  - type: Butcherable
    waitForRot: true
";

    [Test]
    public async Task RepeatedSameToolAttemptCompletesExactlyOnce()
    {
        var targetNet = await SpawnTarget("ToolRefinableButcheringTestTarget");
        var target = ToServer(targetNet);

        await InteractUsing("ToolRefinableButcheringTestTool", awaitDoAfters: false);
        Assert.That(ActiveDoAfters.Count(), Is.EqualTo(1));

        await Interact(awaitDoAfters: false);
        Assert.That(ActiveDoAfters.Count(), Is.EqualTo(1),
            "Repeating the same tool-target pair must retain the first do-after without adding or cancelling it.");

        await AwaitDoAfters();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(SEntMan.Deleted(target), Is.True);
            Assert.That(CountResults(), Is.EqualTo(1),
                "A repeated interaction produced more than one configured result set.");
        }
    }

    [Test]
    public async Task WaitForRotIsCheckedAtStartAndCompletion()
    {
        var targetNet = await SpawnTarget("ToolRefinableButcheringWaitTestTarget");
        var target = ToServer(targetNet);

        await InteractUsing("ToolRefinableButcheringTestTool", awaitDoAfters: false);
        Assert.That(ActiveDoAfters, Is.Empty,
            "A revivable wait-for-rot victim must not start tool refinement.");

        await Server.WaitPost(() => SEntMan.EnsureComponent<UnrevivableComponent>(target));
        await Interact(awaitDoAfters: false);
        Assert.That(ActiveDoAfters.Count(), Is.EqualTo(1));

        await Server.WaitPost(() => SEntMan.RemoveComponent<UnrevivableComponent>(target));
        await AwaitDoAfters();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(SEntMan.Deleted(target), Is.False,
                "Completion must recheck revivability before consuming the target.");
            Assert.That(CountResults(), Is.Zero);
        }

        await Server.WaitPost(() => SEntMan.EnsureComponent<UnrevivableComponent>(target));
        await Interact(awaitDoAfters: false);
        Assert.That(ActiveDoAfters.Count(), Is.EqualTo(1),
            "A completion-time rejection must clear the active tool-target pair for a later valid attempt.");

        await AwaitDoAfters();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(SEntMan.Deleted(target), Is.True);
            Assert.That(CountResults(), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task EnteringContainerBeforeCompletionPreservesTargetAndProducesNothing()
    {
        var targetNet = await SpawnTarget("ToolRefinableButcheringTestTarget");
        var target = ToServer(targetNet);
        await InteractUsing("ToolRefinableButcheringTestTool", awaitDoAfters: false);
        Assert.That(ActiveDoAfters.Count(), Is.EqualTo(1));
        var doAfter = ActiveDoAfters.Single();

        await Server.WaitPost(() =>
        {
            var holder = SEntMan.SpawnEntity(null, SEntMan.GetCoordinates(TargetCoords));
            var container = SEntMan.System<SharedContainerSystem>()
                .EnsureContainer<Container>(holder, "tool-refinable-test");
            Assert.That(SEntMan.System<SharedContainerSystem>().Insert(target, container), Is.True);
        });

        await RunTicks(1);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(doAfter.Cancelled, Is.True,
                "Entering a container must cancel the movement-sensitive refinement do-after.");
            Assert.That(ActiveDoAfters, Is.Empty);
            Assert.That(SEntMan.Deleted(target), Is.False,
                "A target that enters a container before completion must remain recoverable.");
            Assert.That(CountResults(), Is.Zero);
        }
    }

    private int CountResults()
    {
        return SEntMan.EntityQuery<MetaDataComponent>().Count(metadata =>
            !metadata.Deleted && metadata.EntityPrototype?.ID == "ToolRefinableButcheringTestResult");
    }
}
