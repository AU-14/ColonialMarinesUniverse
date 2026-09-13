using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._RMC14.Vendors;
using Content.Shared.Containers.ItemSlots;

namespace Content.IntegrationTests.Tests.Vending;

public sealed class MacheteScabbardVendorTest : InteractionTest
{
    private const string VendorPrototype = "MacheteScabbardTestVendor";

    [TestPrototypes]
    private const string TestPrototypes = $"""
- type: entity
  id: {VendorPrototype}
  components:
  - type: Sprite
    sprite: error.rsi
  - type: UserInterface
    interfaces:
      enum.CMAutomatedVendorUI.Key:
        type: CMAutomatedVendorBui
        interactionRange: 2.5
  - type: ActivatableUI
    key: enum.CMAutomatedVendorUI.Key
  - type: CMAutomatedVendor
    sections:
    - name: Test
      entries:
      - id: RMCScabbardMacheteFilled
        amount: 1
""";

    [Test]
    public async Task VendedScabbardContainsMachete()
    {
        await SpawnTarget(VendorPrototype);
        await Activate();

        Assert.That(IsUiOpen(CMAutomatedVendorUI.Key), Is.True, "Vendor UI did not open.");

        await SendBui(CMAutomatedVendorUI.Key, new CMVendorVendBuiMsg(0, 0, new List<int>()));

        var scabbard = HandSys.GetActiveItem((SPlayer, Hands));
        Assert.That(scabbard, Is.Not.Null, "Vendor did not place the scabbard in the user's hand.");
        AssertPrototype("RMCScabbardMacheteFilled", SEntMan.GetNetEntity(scabbard));

        var slot = SEntMan.GetComponent<ItemSlotsComponent>(scabbard.Value).Slots["item"];
        Assert.That(slot.Item, Is.Not.Null, "Vended machete scabbard was empty.");
        AssertPrototype("CMM2132Machete", SEntMan.GetNetEntity(slot.Item));
    }
}
