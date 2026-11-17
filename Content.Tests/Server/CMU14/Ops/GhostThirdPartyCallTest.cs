using Content.Shared.CMU14.Threats;
using NUnit.Framework;

namespace Content.Tests.Server.CMU14.Ops;

[TestFixture]
public sealed class GhostThirdPartyCallTest
{
    [TestCase(0, 0, 0.3f, false)]
    [TestCase(1, 0, 0.3f, true)]
    [TestCase(0, 10, 0.3f, false)]
    [TestCase(2, 10, 0.3f, false)]
    [TestCase(3, 10, 0.3f, true)]
    [TestCase(30, 100, 0.3f, true)]
    [TestCase(29, 100, 0.3f, false)]
    [TestCase(5, 1, 0f, true)]
    [TestCase(0, 0, 0f, false)]
    public void DeadThresholdRequiresRatioOfLiving(int dead, int living, float ratio, bool expected)
        => Assert.That(GhostThirdPartyCall.MeetsDeadThreshold(dead, living, ratio), Is.EqualTo(expected));

}
