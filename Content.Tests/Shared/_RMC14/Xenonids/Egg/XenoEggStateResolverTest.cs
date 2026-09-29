using System.Linq;
using Content.Client._RMC14.Xenonids.Egg;
using NUnit.Framework;

namespace Content.Tests.Shared._RMC14.Xenonids.Egg;

[TestFixture]
[TestOf(typeof(XenoEggStateResolver))]
public sealed class XenoEggStateResolverTest
{
    [Test]
    public void ExactStateWinsOverCanonicalAlias()
    {
        var states = new[] { "egg", "Egg" };

        Assert.Multiple(() =>
        {
            Assert.That(XenoEggStateResolver.TryResolve("Egg", states.Contains, out var exact), Is.True);
            Assert.That(exact, Is.EqualTo("Egg"));
        });
    }

    [TestCase("egg", "Egg")]
    [TestCase("egg_opening", "Egg Opening")]
    [TestCase("egg_opened", "Egg Opened")]
    [TestCase("egg_growing", "Egg Growing")]
    [TestCase("egg_item", "egg_item")]
    public void CanonicalStateResolvesHunterShipAlias(string requested, string expected)
    {
        var states = new[] { "Egg", "Egg Opening", "Egg Opened", "Egg Growing", "egg_item" };

        Assert.That(XenoEggStateResolver.TryResolve(requested, states.Contains, out var resolved), Is.True);
        Assert.That(resolved, Is.EqualTo(expected));
    }

    [TestCase("Egg", "egg")]
    [TestCase("Egg Opening", "egg_opening")]
    [TestCase("Egg Opened", "egg_opened")]
    [TestCase("Egg Growing", "egg_growing")]
    public void HunterShipStateResolvesFragileAlias(string requested, string expected)
    {
        var states = new[] { "egg", "egg_opening", "egg_opened", "egg_growing", "egg_item" };

        Assert.That(XenoEggStateResolver.TryResolve(requested, states.Contains, out var resolved), Is.True);
        Assert.That(resolved, Is.EqualTo(expected));
    }

    [Test]
    public void MissingStateIsRejected()
    {
        Assert.That(XenoEggStateResolver.TryResolve("egg_opened", new[] { "Egg" }.Contains, out _), Is.False);
    }
}
