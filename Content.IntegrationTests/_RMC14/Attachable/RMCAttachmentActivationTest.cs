#nullable enable

using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Input;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;

namespace Content.IntegrationTests._RMC14.Attachable;

[TestFixture]
public sealed class RMCAttachmentActivationTest : InteractionTest
{
    private const string StockSlot = "rmc-aslot-stock";
    private const string UnderbarrelSlot = "rmc-aslot-underbarrel";

    protected override string PlayerPrototype => "MobHuman";

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: RMCWeaponRifleM54C
          id: RMCAttachmentActivationTestBipodGun
          components:
          - type: AttachableHolder
            slots:
              rmc-aslot-underbarrel:
                startingAttachable: RMCAttachmentBipod
                whitelist:
                  tags:
                  - RMCAttachmentBipod
        """;

    [Test]
    public async Task SlotKeysActivateAttachments()
    {
        await AssertActivation("RMCWeaponRifleM54C", UnderbarrelSlot,
            CMKeyFunctions.RMCActivateAttachableUnderbarrel, 5, true);
        await AssertActivation("RMCAttachmentActivationTestBipodGun", UnderbarrelSlot,
            CMKeyFunctions.RMCActivateAttachableUnderbarrel, 60, false);
        await AssertActivation("RMCWeaponRifleM54C", StockSlot,
            CMKeyFunctions.RMCActivateAttachableStock, 60, false);
    }

    private async Task AssertActivation(
        string gunPrototype,
        string slot,
        BoundKeyFunction key,
        int ticks,
        bool supercedesHolder)
    {
        var gun = await PlaceInHands(gunPrototype);
        var serverGun = ToServer(gun);
        EntityUid attachment = default;
        NetEntity netAttachment = default;

        await Server.WaitAssertion(() =>
        {
            var containerSystem = Server.System<SharedContainerSystem>();
            Assert.That(containerSystem.TryGetContainer(serverGun, slot, out var container), Is.True);
            Assert.That(container, Is.Not.Null);
            Assert.That(container!.ContainedEntities, Has.Count.EqualTo(1));
            attachment = container.ContainedEntities[0];
            netAttachment = SEntMan.GetNetEntity(attachment);

            var toggleable = SEntMan.GetComponent<AttachableToggleableComponent>(attachment);
            Assert.That(toggleable.Attached, Is.True);
            Assert.That(toggleable.Active, Is.False);
        });

        await PressKey(key);
        await RunTicks(ticks);

        var serverActive = false;
        EntityUid? supercedingAttachment = null;
        await Server.WaitPost(() =>
        {
            serverActive = SEntMan.GetComponent<AttachableToggleableComponent>(attachment).Active;
            supercedingAttachment = SEntMan.GetComponent<AttachableHolderComponent>(serverGun).SupercedingAttachable;
        });

        var clientActive = false;
        await Client.WaitPost(() =>
        {
            var clientAttachment = CEntMan.GetEntity(netAttachment);
            clientActive = CEntMan.GetComponent<AttachableToggleableComponent>(clientAttachment).Active;
        });

        Assert.Multiple(() =>
        {
            Assert.That(serverActive, Is.True);
            Assert.That(clientActive, Is.True);
            Assert.That(supercedingAttachment, Is.EqualTo(supercedesHolder ? attachment : null));
        });
    }
}
