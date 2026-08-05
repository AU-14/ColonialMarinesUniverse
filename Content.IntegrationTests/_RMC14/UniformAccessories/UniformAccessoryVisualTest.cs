using System.Linq;
using Content.Client.Inventory;
using Content.Shared._RMC14.UniformAccessories;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._RMC14.UniformAccessories;

[TestFixture]
public sealed class UniformAccessoryVisualTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: MobHuman
  id: UniformAccessoryVisualTestHuman
  components:
  - type: HumanoidProfile
    sex: Female
";

    [Test]
    public async Task AccessoryOnDisplacedClothingAddsSpriteAndDisplacementLayers()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var entMan = client.ResolveDependency<IEntityManager>();
            var container = client.System<SharedContainerSystem>();
            var inventory = client.System<InventorySystem>();
            var sprite = client.System<SpriteSystem>();

            var wearer = entMan.SpawnEntity("UniformAccessoryVisualTestHuman", MapCoordinates.Nullspace);
            var uniform = entMan.SpawnEntity("RMCJumpsuitMarinePatch", MapCoordinates.Nullspace);
            var patch = entMan.SpawnEntity("RMCPatchUNMC", MapCoordinates.Nullspace);

            Assert.That(inventory.TryEquip(wearer, uniform, "jumpsuit", force: true), Is.True);

            var inventorySlots = entMan.GetComponent<InventorySlotsComponent>(wearer);
            var layersBeforeAccessory = inventorySlots.VisualLayerKeys["jumpsuit"].ToHashSet();
            var holder = entMan.GetComponent<UniformAccessoryHolderComponent>(uniform);
            var accessories = container.EnsureContainer<Container>(uniform, holder.ContainerId);

            Assert.That(container.Insert(patch, accessories), Is.True);

            var addedLayers = inventorySlots.VisualLayerKeys["jumpsuit"]
                .Except(layersBeforeAccessory)
                .ToArray();
            var accessoryLayer = addedLayers.Single(key => !key.EndsWith("-displacement"));

            Assert.Multiple(() =>
            {
                Assert.That(accessoryLayer, Does.Not.StartWith("enum."));
                Assert.That(addedLayers, Does.Contain($"{accessoryLayer}-displacement"));
                Assert.That(sprite.LayerMapTryGet(wearer, accessoryLayer, out _, false), Is.True);
                Assert.That(sprite.LayerMapTryGet(wearer, $"{accessoryLayer}-displacement", out _, false), Is.True);
            });

            entMan.DeleteEntity(wearer);
            entMan.DeleteEntity(uniform);
            entMan.DeleteEntity(patch);
        });

        await pair.CleanReturnAsync();
    }
}
