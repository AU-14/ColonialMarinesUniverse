using System.Numerics;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Engineering.EntitySystems;
using Content.Shared.Stacks;

namespace Content.IntegrationTests.CMU14.Engineering;

[TestFixture]
[TestOf(typeof(SpawnAfterInteractSystem))]
public sealed class CMUSpawnAfterInteractTest : InteractionTest
{
    public override async Task DoSetup()
    {
        await base.DoSetup();
        await Server.WaitPost(() =>
            TargetCoords = SEntMan.GetNetCoordinates(MapData.GridCoords.Offset(new Vector2(1.5f, 0.5f))));
    }

    [Test]
    public async Task CompletedPlacementConsumesOneItem()
    {
        var stack = await PlaceInHands(InflatableWallStack.Id, 2);
        await Interact(awaitDoAfters: false);
        Assert.That(ActiveDoAfters, Has.Exactly(1).Items);

        await AwaitDoAfters();

        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<StackComponent>(SEntMan.GetEntity(stack)).Count, Is.EqualTo(1)));
        await AssertEntityLookup(new EntitySpecifier(InflatableWall, 1));
    }

    [Test]
    public async Task CancelledPlacementPreservesItems()
    {
        var stack = await PlaceInHands(InflatableWallStack.Id, 2);
        await Interact(awaitDoAfters: false);

        await CancelDoAfters();
        await RunTicks(90);

        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<StackComponent>(SEntMan.GetEntity(stack)).Count, Is.EqualTo(2)));
        await AssertEntityLookup();
    }

    [Test]
    public async Task OccupiedTileAtCompletionPreservesItems()
    {
        var stack = await PlaceInHands(InflatableWallStack.Id, 2);
        await Interact(awaitDoAfters: false);
        Assert.That(ActiveDoAfters, Has.Exactly(1).Items);
        await SpawnTarget(InflatableWall);

        await AwaitDoAfters();

        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<StackComponent>(SEntMan.GetEntity(stack)).Count, Is.EqualTo(2)));
        // The lookup excludes the blocking target; no second wall should have been created.
        await AssertEntityLookup();
    }
}
