using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using NUnit.Framework;

namespace Content.Tests.Shared.Chemistry;

[TestFixture, Parallelizable, TestOf(typeof(ReagentQuantity))]
public sealed class ReagentQuantityTests
{
    [Test]
    public void Equals_SameReagentAndQuantity_ReturnsTrue()
    {
        var reagent = new ReagentId("water", null);
        var left = new ReagentQuantity(reagent, FixedPoint2.New(10));
        var right = new ReagentQuantity(reagent, FixedPoint2.New(10));

        Assert.That(left.Equals(right), Is.True);
        Assert.That(left.Equals((object) right), Is.True);
        Assert.That(left == right, Is.True);
        Assert.That(left != right, Is.False);
    }

    [TestCase("water", 11)]
    [TestCase("blood", 10)]
    public void Equals_DifferentValue_ReturnsFalse(string otherReagent, int otherQuantity)
    {
        var left = new ReagentQuantity("water", FixedPoint2.New(10));
        var right = new ReagentQuantity(otherReagent, FixedPoint2.New(otherQuantity));

        Assert.That(left.Equals(right), Is.False);
        Assert.That(left == right, Is.False);
        Assert.That(left != right, Is.True);
    }
}
