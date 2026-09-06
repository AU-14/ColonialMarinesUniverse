using Content.Shared._CMU14.Dropship.TacticalLand;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class GunshipSpatialQueryBudgetTest
{
    [Test]
    public void BudgetRejectsQueriesPastItsHardLimit()
    {
        var budget = new GunshipSpatialQueryBudget(1);

        Assert.Multiple(() =>
        {
            Assert.That(budget.TryConsume(), Is.True);
            Assert.That(budget.TryConsume(), Is.False);
            Assert.That(budget.Used, Is.EqualTo(1));
        });
    }
}
