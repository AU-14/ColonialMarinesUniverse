using Content.Shared.CMU14.Threats;
using NUnit.Framework;

namespace Content.Tests.Server.CMU14.Round;

[TestFixture]
public sealed class ForceInterestTest
{
    [TestCase(0, 0)]
    [TestCase(1, 1)]
    [TestCase(2, 2)]
    [TestCase(3, 2)]
    [TestCase(4, 3)]
    [TestCase(5, 4)]
    [TestCase(6, 4)]
    [TestCase(9, 6)]
    [TestCase(10, 7)]
    [TestCase(20, 13)]
    public void DeploymentRequiresStrictlyMoreThanSixtyPercent(int roles, int volunteers)
    {
        Assert.That(ForceInterest.RequiredPlayers(roles), Is.EqualTo(volunteers));
    }
}
