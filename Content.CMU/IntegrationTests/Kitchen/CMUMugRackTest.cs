using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.CMU14.Kitchen;

namespace Content.IntegrationTests.CMU14.Kitchen;

/// <summary>
///     The mug rack hands you a mug with an empty hand and takes one back when you use it on the rack.
/// </summary>
public sealed class CMUMugRackTest : InteractionTest
{
    [Test]
    public async Task TakeAndReturnMug()
    {
        await SpawnTarget("CMUMugRack");
        var rack = SEntMan.GetEntity(Target!.Value);
        var start = SEntMan.GetComponent<CMUMugRackComponent>(rack).Amount;

        await Interact();
        var held = HandSys.GetActiveItem((SEntMan.GetEntity(Player), Hands));
        Assert.That(held, Is.Not.Null, "Clicking the rack empty-handed hands you a mug.");
        AssertPrototype("RMCCoffeeCup", SEntMan.GetNetEntity(held));
        Assert.That(SEntMan.GetComponent<CMUMugRackComponent>(rack).Amount, Is.EqualTo(start - 1));

        await Interact();
        Assert.That(HandSys.GetActiveItem((SEntMan.GetEntity(Player), Hands)), Is.Null, "Using the mug on the rack puts it back.");
        Assert.That(SEntMan.GetComponent<CMUMugRackComponent>(rack).Amount, Is.EqualTo(start));
    }
}
