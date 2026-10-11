using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.CMU14.Round.Antags.Cannibal; // CMU14
using Content.Shared.Traits.Assorted;
using Content.Shared.Tools.Components; // CMU14
using Content.Shared.Verbs; // CMU14
using Robust.Shared.Localization; // CMU14
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

        await PlaceInHands("ToolRefinableButcheringTestTool"); // CMU14
        await Interact(awaitDoAfters: false); // CMU14
        Assert.That(ActiveDoAfters, Is.Empty, "Left clicks must not start butchering."); // CMU14
        await Slice(); // CMU14
        Assert.That(ActiveDoAfters.Count(), Is.EqualTo(1));

        await Slice(); // CMU14: butchering is verb-only.
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

        await PlaceInHands("ToolRefinableButcheringTestTool"); // CMU14
        await Slice(); // CMU14
        Assert.That(ActiveDoAfters, Is.Empty,
            "A revivable wait-for-rot victim must not start tool refinement.");

        await Server.WaitPost(() => SEntMan.EnsureComponent<UnrevivableComponent>(target));
        await Slice(); // CMU14: butchering is verb-only.
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
        await Slice(); // CMU14: butchering is verb-only.
        Assert.That(ActiveDoAfters.Count(), Is.EqualTo(1),
            "A completion-time rejection must clear the active tool-target pair for a later valid attempt.");

        await AwaitDoAfters();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(SEntMan.Deleted(target), Is.True);
            Assert.That(CountResults(), Is.EqualTo(1));
        }
    }

    // CMU14: cannibals bypass the wait-for-rot gate, a fresh corpse is their meal
    [Test]
    public async Task CannibalCarvesWaitForRotTarget()
    {
        var targetNet = await SpawnTarget("ToolRefinableButcheringWaitTestTarget");
        var target = ToServer(targetNet);

        await Server.WaitPost(() => SEntMan.EnsureComponent<CannibalComponent>(SPlayer));

        await PlaceInHands("ToolRefinableButcheringTestTool"); // CMU14
        await Slice(); // CMU14
        Assert.That(ActiveDoAfters.Count(), Is.EqualTo(1),
            "A cannibal must start carving a fresh wait-for-rot victim.");

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
        await PlaceInHands("ToolRefinableButcheringTestTool"); // CMU14
        await Slice(); // CMU14
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

    // CMU14: exercise the same right-click verb used by players.
    private async Task Slice()
    {
        await Server.WaitPost(() =>
        {
            var target = STarget!.Value;
            var label = Loc.GetString(SEntMan.GetComponent<ToolRefinableComponent>(target).VerbText!);
            var verbs = Server.System<SharedVerbSystem>();
            var verb = verbs.GetLocalVerbs(target, SPlayer, typeof(InteractionVerb)).Single(verb => verb.Text == label);
            if (!verb.Disabled)
                verbs.ExecuteVerb(verb, SPlayer, target);
        });
        await RunTicks(1);
    }

    private int CountResults()
    {
        return SEntMan.EntityQuery<MetaDataComponent>().Count(metadata =>
            !metadata.Deleted && metadata.EntityPrototype?.ID == "ToolRefinableButcheringTestResult");
    }
}
