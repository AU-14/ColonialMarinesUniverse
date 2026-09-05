using System.Linq;
using Content.Shared._RMC14.Requisitions;
using NUnit.Framework;
using Robust.Shared.Prototypes;

namespace Content.Tests.Shared._RMC14.Requisitions;

[TestFixture]
[TestOf(typeof(RequisitionsPriceCalculator))]
public sealed class RequisitionsPriceCalculatorTest
{
    [Test]
    public void RepeatedStacksDivideLegacyBundlePrice()
    {
        var prices = RequisitionsPriceCalculator.Calculate(new[]
        {
            Source(1200, ("MetalStack50", 6)),
        });

        Assert.That(prices["MetalStack50"], Is.EqualTo(200));
    }

    [Test]
    public void HomogeneousBundlesAnchorMixedBundleContents()
    {
        var prices = RequisitionsPriceCalculator.Calculate(new[]
        {
            Source(400, ("PowerCell", 4)),
            Source(500, ("PowerCell", 2), ("Toolkit", 2)),
        });

        Assert.Multiple(() =>
        {
            Assert.That(prices["PowerCell"], Is.EqualTo(100));
            Assert.That(prices["Toolkit"], Is.EqualTo(150));
        });
    }

    [Test]
    public void UnanchoredMixedBundleSplitsPriceByDeliveredQuantity()
    {
        var prices = RequisitionsPriceCalculator.Calculate(new[]
        {
            Source(900, ("Helmet", 1), ("Armor", 2)),
        });

        Assert.Multiple(() =>
        {
            Assert.That(prices["Helmet"], Is.EqualTo(300));
            Assert.That(prices["Armor"], Is.EqualTo(300));
        });
    }

    [Test]
    public void MultiplePureSourcesUseTheirMedianUnitPrice()
    {
        var prices = RequisitionsPriceCalculator.Calculate(new[]
        {
            Source(1200, ("Rocket", 6)),
            Source(1000, ("Rocket", 4)),
        });

        Assert.That(prices["Rocket"], Is.EqualTo(225));
    }

    [TestCase(1200, 400)]
    [TestCase(2800, 933)]
    public void AmmoAnchorsThatExhaustLauncherBundleUseEqualShareFallback(int bundleCost, int expectedLauncherCost)
    {
        var prices = RequisitionsPriceCalculator.Calculate(new[]
        {
            Source(1500, ("AU14BoxHEDPUPP", 1)),
            Source(1300, ("AU14BoxHIDPUPP", 1)),
            Source(bundleCost, ("AU14WeaponLauncherOG60", 1), ("AU14BoxHEDPUPP", 1), ("AU14BoxHIDPUPP", 1)),
        });

        Assert.Multiple(() =>
        {
            Assert.That(prices["AU14WeaponLauncherOG60"], Is.EqualTo(expectedLauncherCost));
            Assert.That(prices["AU14BoxHEDPUPP"], Is.EqualTo(1500));
            Assert.That(prices["AU14BoxHIDPUPP"], Is.EqualTo(1300));
        });
    }

    [Test]
    public void ExpensiveUnanchoredBundleDoesNotErodeAnotherItemsPrice()
    {
        var sources = new[]
        {
            Source(10000, ("Ammo", 1), ("Armor", 1)),
            Source(1000, ("Ammo", 1), ("Launcher", 1)),
        };
        var prices = RequisitionsPriceCalculator.Calculate(sources);

        Assert.Multiple(() =>
        {
            Assert.That(prices["Ammo"], Is.EqualTo(2750));
            Assert.That(prices["Armor"], Is.EqualTo(5000));
            Assert.That(prices["Launcher"], Is.EqualTo(500));
            Assert.That(RequisitionsPriceCalculator.Calculate(sources.Reverse()), Is.EquivalentTo(prices));
        });
    }

    [Test]
    public void MixedBundleSharesRemainingValueByUnanchoredQuantity()
    {
        var prices = RequisitionsPriceCalculator.Calculate(new[]
        {
            Source(400, ("PowerCell", 4)),
            Source(1100, ("PowerCell", 2), ("Toolkit", 2), ("Helmet", 1)),
        });

        Assert.Multiple(() =>
        {
            Assert.That(prices["PowerCell"], Is.EqualTo(100));
            Assert.That(prices["Toolkit"], Is.EqualTo(300));
            Assert.That(prices["Helmet"], Is.EqualTo(300));
        });
    }

    private static RequisitionsPriceSource Source(int cost, params (string Prototype, int Amount)[] items)
    {
        return new RequisitionsPriceSource(
            cost,
            items.ToDictionary(item => (EntProtoId) item.Prototype, item => item.Amount));
    }
}
