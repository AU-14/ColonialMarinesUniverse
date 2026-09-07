using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Clothing;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;

namespace Content.IntegrationTests.CMU14.Clothing;

[TestFixture]
public sealed class FoldableUniformLayerRegressionTest : GameTest
{
    [TestCase("JumpsuitMarine")]
    [TestCase("AU14CamoUSCMFatigues")]
    [TestCase("AU14HAZOPUrbanFatigues")]
    public async Task JacketFoldRevealsUndergarmentTop(string uniformPrototype)
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var wearer = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var uniform = SEntMan.SpawnEntity(uniformPrototype, map.GridCoords);
            var inventory = Server.System<InventorySystem>();
            var clothing = SEntMan.GetComponent<RMCClothingFoldableComponent>(uniform);
            var fold = Server.System<RMCClothingSystem>();
            var jacket = clothing.Types.Single(type => type.Prefix == "jacket");

            Assert.That(inventory.TryEquip(wearer, uniform, "jumpsuit", silent: true, force: true), Is.True);

            var hiddenLayers = SEntMan.GetComponent<HideableHumanoidLayersComponent>(wearer);
            Assert.That(SharedHideableHumanoidLayersSystem.IsLayerOccluded(
                hiddenLayers,
                HumanoidVisualLayers.UndergarmentTop), Is.True);

            fold.TryToggleFold((uniform, clothing), jacket, wearer);
            Assert.That(SharedHideableHumanoidLayersSystem.IsLayerOccluded(
                hiddenLayers,
                HumanoidVisualLayers.UndergarmentTop), Is.False);

            fold.TryToggleFold((uniform, clothing), jacket, wearer);
            Assert.That(SharedHideableHumanoidLayersSystem.IsLayerOccluded(
                hiddenLayers,
                HumanoidVisualLayers.UndergarmentTop), Is.True);

            SEntMan.DeleteEntity(wearer);
        });
    }
}
