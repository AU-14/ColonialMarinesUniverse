using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Requisitions.Components;

namespace Content.IntegrationTests.CMU14.Requisitions;

[TestFixture]
public sealed class ASRSItemPricingRegressionTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
    };

    [Test]
    public async Task UppLauncherKeepsValueWhenItsAmmoCostsMoreThanTheBundle()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var computer = SEntMan.SpawnEntity("UPPCargoCatalog", map.GridCoords);
            var requisitions = SEntMan.GetComponent<RequisitionsComputerComponent>(computer);
            var prices = requisitions.ItemCatalog.ToDictionary(item => item.Prototype.Id, item => item.Cost);

            Assert.Multiple(() =>
            {
                Assert.That(prices["AU14WeaponLauncherOG60"], Is.EqualTo(400));
                Assert.That(prices["AU14BoxHEDPUPP"], Is.EqualTo(1500));
                Assert.That(prices["AU14BoxHIDPUPP"], Is.EqualTo(1300));
                Assert.That(requisitions.Categories.SelectMany(category => category.Entries)
                    .Single(entry => entry.Crate.Id == "AU14CrateOG60").Cost, Is.EqualTo(1200));
            });
        });
    }
}
