using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Chemistry.EntitySystems;
using Robust.Shared.Containers;

namespace Content.IntegrationTests.CMU14.Kitchen;

/// <summary>
///     A mug put in the coffee machine sits in it while it brews, and stays there full of coffee until it's taken out.
/// </summary>
public sealed class CMUCoffeeMachineTest : InteractionTest
{
    [Test]
    public async Task MugGoesInBrewsAndComesBack()
    {
        await SpawnTarget("RMCCoffeeMachine");
        var machine = SEntMan.GetEntity(Target!.Value);
        var mug = SEntMan.GetEntity(await PlaceInHands("RMCCoffeeCup"));

        await Interact();
        var containers = SEntMan.System<SharedContainerSystem>();
        Assert.That(containers.IsEntityInContainer(mug), Is.True, "The mug goes into the machine.");
        Assert.That(HandSys.GetActiveItem((SEntMan.GetEntity(Player), Hands)), Is.Null);

        await RunSeconds(2);
        Assert.That(containers.IsEntityInContainer(mug), Is.True, "It stays in until the brewing sound finishes.");

        await RunSeconds(4);
        Assert.That(containers.IsEntityInContainer(mug), Is.True, "The brewed mug waits in the machine.");
        Assert.That(HandSys.GetActiveItem((SEntMan.GetEntity(Player), Hands)), Is.Null);

        await Interact();
        Assert.That(HandSys.GetActiveItem((SEntMan.GetEntity(Player), Hands)), Is.EqualTo(mug), "Clicking the machine takes it out.");

        var solutions = SEntMan.System<SharedSolutionContainerSystem>();
        Assert.That(solutions.TryGetFitsInDispenser(mug, out _, out var coffee), Is.True);
        Assert.That(coffee!.GetTotalPrototypeQuantity("RMCCoffee").Float(), Is.GreaterThan(0));
        Assert.That(containers.TryGetContainer(machine, "cmu_coffee_cup", out var slot) && slot.ContainedEntities.Count == 0, Is.True);
    }
}
