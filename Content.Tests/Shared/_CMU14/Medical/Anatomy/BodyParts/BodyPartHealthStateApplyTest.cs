using System.Reflection;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.Damage;
using NUnit.Framework;

namespace Content.Tests.Shared._CMU14.Medical.Anatomy.BodyParts;

[TestFixture]
public sealed class BodyPartHealthStateApplyTest
{
    [Test]
    public void DamageChangedIsIgnoredDuringStateApply()
    {
        var damage = new DamageSpecifier();

        Assert.That(ShouldProcessDamageChanged(
            medicalEnabled: true,
            bodyPartEnabled: true,
            applyingState: true,
            damage), Is.False);
    }

    [Test]
    public void DamageChangedNeedsDamageDelta()
    {
        Assert.That(ShouldProcessDamageChanged(
            medicalEnabled: true,
            bodyPartEnabled: true,
            applyingState: false,
            damageDelta: null), Is.False);
    }

    [Test]
    public void DamageChangedProcessesWhenEnabledAndNotApplyingState()
    {
        var damage = new DamageSpecifier();

        Assert.That(ShouldProcessDamageChanged(
            medicalEnabled: true,
            bodyPartEnabled: true,
            applyingState: false,
            damage), Is.True);
    }

    private static bool ShouldProcessDamageChanged(
        bool medicalEnabled,
        bool bodyPartEnabled,
        bool applyingState,
        DamageSpecifier damageDelta)
    {
        var method = typeof(SharedBodyPartHealthSystem).GetMethod(
            "ShouldProcessDamageChanged",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);
        return (bool) method!.Invoke(
            null,
            new object[] { medicalEnabled, bodyPartEnabled, applyingState, damageDelta })!;
    }
}
