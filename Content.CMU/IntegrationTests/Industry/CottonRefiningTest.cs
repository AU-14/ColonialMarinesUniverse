using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Stacks;
using NUnit.Framework;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU.Industry;

public sealed class CottonRefiningTest : InteractionTest
{
    [TestCase("RMCM5Bayonet")]
    [TestCase("CMShardGlass")]
    public async Task SlicingToolsTurnCottonIntoFiber(string tool)
    {
        var source = ToServer(await SpawnTarget("CottonBol"));

        await InteractUsing(tool);
        await RunTicks(5);

        Assert.That(SEntMan.Deleted(source), Is.True, "Successful refining must consume the boll.");
        var fiber = 0;
        foreach (var stack in SEntMan.QueryEntities<StackComponent>())
        {
            if (stack.Comp.StackTypeId == "Cotton")
                fiber += stack.Comp.Count;
        }

        Assert.That(fiber, Is.EqualTo(2), "Slicing must produce usable cotton fiber.");
    }
}
