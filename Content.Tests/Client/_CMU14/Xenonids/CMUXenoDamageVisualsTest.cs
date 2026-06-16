using Content.Client._RMC14.Xenonids.Damage;
using NUnit.Framework;

namespace Content.Tests.Client._CMU14.Xenonids;

[TestFixture]
public sealed class CMUXenoDamageVisualsTest
{
    [Test]
    public void XenoDamageVisualStateClampsOverIncapDamageToMostWoundedState()
    {
        Assert.That(RMCXenoDamageVisualsSystem.GetDamageVisualState(3, 4), Is.EqualTo(1));
    }

    [Test]
    public void XenoDamageVisualStateClampsBelowVisibleDamageToLeastWoundedState()
    {
        Assert.That(RMCXenoDamageVisualsSystem.GetDamageVisualState(3, 0), Is.EqualTo(3));
    }
}
