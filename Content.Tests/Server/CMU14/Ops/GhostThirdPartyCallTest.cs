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

    [TestCase(0, 0.3f, 1)]
    [TestCase(1, 0.3f, 1)]
    [TestCase(10, 0.3f, 3)]
    [TestCase(100, 0.3f, 30)]
    [TestCase(7, 0.5f, 4)]
    [TestCase(10, 0f, 0)]
    [TestCase(10, -1f, 0)]
    public void RequiredDeadRoundsUpRatio(int living, float ratio, int required)
        => Assert.That(GhostThirdPartyCall.RequiredDead(living, ratio), Is.EqualTo(required));
}
