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
            Assert.That(prices["Toolkit"], Is.EqualTo(150).Within(1));
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

    private static RequisitionsPriceSource Source(int cost, params (string Prototype, int Amount)[] items)
    {
        return new RequisitionsPriceSource(
            cost,
            items.ToDictionary(item => (EntProtoId) item.Prototype, item => item.Amount));
    }
}
