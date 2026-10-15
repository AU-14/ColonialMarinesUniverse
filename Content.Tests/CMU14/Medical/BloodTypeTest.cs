using System;
using Content.Shared.CMU14.Medical.Blood;
using NUnit.Framework;

namespace Content.Tests.CMU14.Medical;

[TestFixture]
public sealed class BloodTypeTest
{
    [Test]
    public void ONegativeCanGoToAnyoneAndABPositiveCanTakeAnything()
    {
        foreach (var type in Enum.GetValues<CMUBloodType>())
        {
            Assert.That(CMUBloodTypes.CanReceive(type, CMUBloodType.ONegative), Is.True, $"{type} should take O-");
            Assert.That(CMUBloodTypes.CanReceive(CMUBloodType.ABPositive, type), Is.True, $"AB+ should take {type}");
        }
    }

    [TestCase(CMUBloodType.ONegative, CMUBloodType.OPositive, false)]
    [TestCase(CMUBloodType.APositive, CMUBloodType.BPositive, false)]
    [TestCase(CMUBloodType.ANegative, CMUBloodType.APositive, false)]
    [TestCase(CMUBloodType.APositive, CMUBloodType.ANegative, true)]
    [TestCase(CMUBloodType.ABNegative, CMUBloodType.BNegative, true)]
    [TestCase(CMUBloodType.ABNegative, CMUBloodType.ABPositive, false)]
    public void CompatibilityFollowsAboAndRh(CMUBloodType recipient, CMUBloodType donor, bool compatible)
    {
        Assert.That(CMUBloodTypes.CanReceive(recipient, donor), Is.EqualTo(compatible));
    }

    [TestCase(CMUBloodType.ONegative, CMUBloodType.ABPositive, true)]
    [TestCase(CMUBloodType.ABPositive, CMUBloodType.ABNegative, true)]
    [TestCase(CMUBloodType.ABPositive, CMUBloodType.OPositive, false)]
    [TestCase(CMUBloodType.APositive, CMUBloodType.ANegative, true)]
    [TestCase(CMUBloodType.APositive, CMUBloodType.OPositive, false)]
    [TestCase(CMUBloodType.OPositive, CMUBloodType.BNegative, true)]
    [TestCase(CMUBloodType.BNegative, CMUBloodType.APositive, false)]
    public void PlasmaRunsTheOtherWayAndIgnoresRh(CMUBloodType recipient, CMUBloodType donor, bool compatible)
    {
        Assert.That(CMUBloodTypes.CanReceivePlasma(recipient, donor), Is.EqualTo(compatible));
    }

    [Test]
    public void DefaultsAreNeverONegative()
    {
        var random = new System.Random(1);
        for (var i = 0; i < 200; i++)
        {
            Assert.That(CMUBloodTypes.RandomDefault(random), Is.Not.EqualTo(CMUBloodType.ONegative));
            Assert.That(CMUBloodTypes.StableDefault($"Character {i}"), Is.Not.EqualTo(CMUBloodType.ONegative));
        }
    }

    [Test]
    public void StableDefaultDoesNotChange()
    {
        Assert.That(CMUBloodTypes.StableDefault("John Doe"), Is.EqualTo(CMUBloodTypes.StableDefault("John Doe")));
    }
}
