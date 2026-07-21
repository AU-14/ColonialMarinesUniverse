#nullable enable

using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Input;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._RMC14.Attachable;

[TestFixture]
public sealed class RMCAttachmentActivationTest : InteractionTest
{
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
    public async Task UnderbarrelKeyActivatesAttachments()
    {
        await AssertActivation("RMCWeaponRifleM54C", 5, true);
        await AssertActivation("RMCAttachmentActivationTestBipodGun", 60, false);
    }

    private async Task AssertActivation(string gunPrototype, int ticks, bool supercedesHolder)
    {
        var gun = await PlaceInHands(gunPrototype);
        var serverGun = ToServer(gun);
        EntityUid attachment = default;
        NetEntity netAttachment = default;

        await Server.WaitAssertion(() =>
        {
            var containerSystem = Server.System<SharedContainerSystem>();
            Assert.That(containerSystem.TryGetContainer(serverGun, UnderbarrelSlot, out var container), Is.True);
            Assert.That(container, Is.Not.Null);
            Assert.That(container!.ContainedEntities, Has.Count.EqualTo(1));
            attachment = container.ContainedEntities[0];
            netAttachment = SEntMan.GetNetEntity(attachment);

            var toggleable = SEntMan.GetComponent<AttachableToggleableComponent>(attachment);
            Assert.That(toggleable.Attached, Is.True);
            Assert.That(toggleable.Active, Is.False);
        });

        await PressKey(CMKeyFunctions.RMCActivateAttachableUnderbarrel);
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
