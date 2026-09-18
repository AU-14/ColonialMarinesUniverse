using Content.Client._RMC14.Medical.HUD;
using Content.Shared.Mobs;
using NUnit.Framework;

namespace Content.Tests.Client._RMC14.Medical.HUD;

[TestFixture]
public sealed class CMXenoHealthIconStateTest
{
    [Test]
    public void HealthyXenoUsesTheFullHealthState()
    {
        Assert.That(CMXenoHealthIconState.GetState(0, MobState.Alive, 200, 300), Is.EqualTo("xenohealth100"));
    }

    [Test]
    public void CriticalXenoUsesTheCriticalHealthState()
    {
        Assert.That(CMXenoHealthIconState.GetState(250, MobState.Critical, 200, 300), Is.EqualTo("xenohealth-50"));
    }

    [Test]
    public void DeadXenoDoesNotExposeAHealthState()
    {
        Assert.That(CMXenoHealthIconState.GetState(300, MobState.Dead, 200, 300), Is.Null);
    }
}
