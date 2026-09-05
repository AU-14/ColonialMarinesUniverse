using System.Linq;
using Content.Shared._RMC14.Requisitions;
using NUnit.Framework;

namespace Content.Tests.Shared._RMC14.Requisitions;

[TestFixture]
[TestOf(typeof(RequisitionsPackingPlan))]
public sealed class RequisitionsPackingPlanTest
{
    [Test]
    public void ExactFitUsesOneCrate()
    {
        var plan = RequisitionsPackingPlan.Build(new[]
        {
            (Item("Heavy", 20), 1),
            (Item("Light", 12), 1),
        }, 32);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Crates, Has.Count.EqualTo(1));
            Assert.That(plan.Crates[0].Weight, Is.EqualTo(32));
            Assert.That(plan.ShipmentCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void SpillAndLooseFreightUseAdditionalSlots()
    {
        var plan = RequisitionsPackingPlan.Build(new[]
        {
            (Item("Bulky", 18), 2),
            (Item("Unpackable", 4, false), 1),
            (Item("Overweight", 40), 1),
        }, 32);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Crates.Select(crate => crate.Weight), Is.EqualTo(new[] { 18, 18 }));
            Assert.That(plan.Loose.Select(item => item.Prototype.Id), Is.EqualTo(new[] { "Overweight", "Unpackable" }));
            Assert.That(plan.ShipmentCount, Is.EqualTo(4));
        });
    }

    [Test]
    public void EqualWeightsAreOrderedByPrototype()
    {
        var plan = RequisitionsPackingPlan.Build(new[]
        {
            (Item("Zulu", 8), 1),
            (Item("Alpha", 8), 1),
        }, 32);

        Assert.That(plan.Crates[0].Items.Select(item => item.Id), Is.EqualTo(new[] { "Alpha", "Zulu" }));
    }

    private static RequisitionsItemEntry Item(string prototype, int weight, bool packable = true)
    {
        return new RequisitionsItemEntry
        {
            Prototype = prototype,
            Weight = weight,
            Packable = packable,
        };
    }
}
