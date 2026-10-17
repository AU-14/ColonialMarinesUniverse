using Content.IntegrationTests.Tests.Interaction;

namespace Content.IntegrationTests.Tests.Construction.Interaction;

public sealed class WallConstruction : InteractionTest
{
    public const string Girder = "Girder";
    public const string WallSolid = "WallSolid";
    public const string Wall = "Wall";

    [Test]
    public async Task ConstructWall()
    {
        await StartConstruction(Wall);
        await InteractUsing(CMSteel, 2); // CMU14
        Assert.That(HandSys.GetActiveItem((SEntMan.GetEntity(Player), Hands)), Is.Null);
        ClientAssertPrototype(Girder, Target);
        await InteractUsing(CMSteel, 2); // CMU14
        Assert.That(HandSys.GetActiveItem((SEntMan.GetEntity(Player), Hands)), Is.Null);
        AssertPrototype(WallSolid);
    }

    [Test]
    public async Task DeconstructWall()
    {
        await StartDeconstruction(WallSolid);
        await InteractUsing(Weld);
        AssertPrototype(Girder);
        await Interact(Wrench, Screw);
        AssertDeleted();
        await AssertEntityLookup((CMSteel, 4)); // CMU14
    }
}
