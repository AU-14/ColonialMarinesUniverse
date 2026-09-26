using Content.Shared.CMU14.Hijack;
using NUnit.Framework;

namespace Content.Tests.Shared.CMU14.Hijack;

[TestFixture]
public sealed class CMUHijackMathTest
{
    [TestCase(0, 900)]
    [TestCase(1, 864)]
    [TestCase(9, 600)]
    [TestCase(18, 300)]
    [TestCase(30, 300)]
    public void GeneratorCountMatchesSourceTimings(int count, double seconds)
        => Assert.That(CMUHijackMath.SelfDestructDuration(count), Is.EqualTo(seconds).Within(0.001));

    [Test]
    public void LosingAndRestoringGeneratorsPreservesCompletedFraction()
    {
        var paused = CMUHijackMath.RescaleSelfDestruct(150, 18, 0);
        Assert.That(paused, Is.EqualTo(450));
        Assert.That(CMUHijackMath.RescaleSelfDestruct(paused, 0, 9), Is.EqualTo(300));
        Assert.That(CMUHijackMath.RescaleSelfDestruct(300, 9, 18), Is.EqualTo(150));
    }
}
