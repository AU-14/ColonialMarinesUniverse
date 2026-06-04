using Content.Client.CombatMode;
using NUnit.Framework;

namespace Content.Tests.Client.CombatMode;

[TestFixture]
public sealed class ZLevelCrosshairIndicatorTest
{
    [Test]
    public void LookUpUsesCaret()
    {
        var indicator = ZLevelCrosshairIndicatorHelper.Get(shootUp: true, shootDown: false);

        Assert.That(indicator, Is.EqualTo(ZLevelCrosshairIndicator.Up));
        Assert.That(ZLevelCrosshairIndicatorHelper.GetGlyph(indicator), Is.EqualTo("^"));
    }

    [Test]
    public void ShootDownUsesV()
    {
        var indicator = ZLevelCrosshairIndicatorHelper.Get(shootUp: false, shootDown: true);

        Assert.That(indicator, Is.EqualTo(ZLevelCrosshairIndicator.Down));
        Assert.That(ZLevelCrosshairIndicatorHelper.GetGlyph(indicator), Is.EqualTo("v"));
    }

    [Test]
    public void NeutralAimHasNoGlyph()
    {
        var indicator = ZLevelCrosshairIndicatorHelper.Get(shootUp: false, shootDown: false);

        Assert.That(indicator, Is.EqualTo(ZLevelCrosshairIndicator.None));
        Assert.That(ZLevelCrosshairIndicatorHelper.GetGlyph(indicator), Is.Null);
    }

    [Test]
    public void ShootDownWinsIfStatesBrieflyOverlap()
    {
        var indicator = ZLevelCrosshairIndicatorHelper.Get(shootUp: true, shootDown: true);

        Assert.That(indicator, Is.EqualTo(ZLevelCrosshairIndicator.Down));
        Assert.That(ZLevelCrosshairIndicatorHelper.GetGlyph(indicator), Is.EqualTo("v"));
    }
}
