using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Dropship.TacticalLand;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class GunshipPilotVisorTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: GunshipVisorTestWearer
  components:
  - type: Inventory
  - type: InventorySlots
  - type: ContainerContainer
  - type: Sprite

- type: entity
  id: GunshipVisorTestHelmet
  components:
  - type: Clothing
    slots: [head]
  - type: Item
    size: Tiny
";

    [TestCase(false)]
    [TestCase(true)]
    public async Task FlightVisorTracksActivationBeforeAndAfterEquipping(bool activateBeforeEquipping)
    {
        var map = await Pair.CreateTestMap();
        EntityUid wearer = default;
        EntityUid helmet = default;

        await Server.WaitAssertion(() =>
        {
            wearer = SEntMan.SpawnEntity("GunshipVisorTestWearer", map.GridCoords);
            helmet = SEntMan.SpawnEntity("GunshipVisorTestHelmet", map.GridCoords);

            if (activateBeforeEquipping)
                SEntMan.AddComponent<GunshipPilotVisorComponent>(helmet);

            Assert.That(Server.System<InventorySystem>().TryEquip(wearer, helmet, "head"), Is.True);

            // VisorSystem adds the flight component to the helmet when lowered.
            if (!activateBeforeEquipping)
                SEntMan.AddComponent<GunshipPilotVisorComponent>(helmet);
        });

        await Pair.RunTicksSync(30);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.TryGetComponent<GunshipPilotHudComponent>(wearer, out var hud), Is.True);
            Assert.That(hud!.Visor, Is.EqualTo(helmet));
            SEntMan.RemoveComponent<GunshipPilotVisorComponent>(helmet);
        });

        await Pair.RunTicksSync(30);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<GunshipPilotHudComponent>(wearer), Is.False);
            SEntMan.AddComponent<GunshipPilotVisorComponent>(helmet);
        });

        await Pair.RunTicksSync(30);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.TryGetComponent<GunshipPilotHudComponent>(wearer, out var hud), Is.True);
            Assert.That(hud!.Visor, Is.EqualTo(helmet));
            Assert.That(Server.System<InventorySystem>().TryUnequip(wearer, "head"), Is.True);
        });

        await Pair.RunTicksSync(30);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<GunshipPilotHudComponent>(wearer), Is.False);
            SEntMan.DeleteEntity(helmet);
            SEntMan.DeleteEntity(wearer);
        });
    }
}
