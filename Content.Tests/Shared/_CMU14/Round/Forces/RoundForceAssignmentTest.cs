#nullable enable

using System;
using Content.Shared.CMU.Round;
using NUnit.Framework;

namespace Content.Tests.Shared._CMU14.Round.Forces;

[TestFixture]
public sealed class RoundForceAssignmentTest
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void ForceIdentifiersCannotBeMissing(string? value)
    {
        Assert.That(
            () => new RoundForceId(value!),
            Throws.InstanceOf<ArgumentException>());
    }

    [Test]
    public void DefaultForceIdentifierCannotBeAssigned()
    {
        Assert.That(
            () => new RoundForceAssignment(
                RoundSide.Govfor,
                default,
                "GovforShip"),
            Throws.ArgumentException.With.Property("ParamName").EqualTo("force"));
    }

    [Test]
    public void AssignmentCopyCannotReplaceForceWithDefault()
    {
        var assignment = new RoundForceAssignment(
            RoundSide.Govfor,
            new RoundForceId("USCM"),
            "GovforShip");

        Assert.That(
            () => assignment with { Force = default },
            Throws.ArgumentException.With.Property("ParamName").EqualTo("value"));
    }

    [Test]
    public void SameForceCanBeAssignedToDifferentRoundSides()
    {
        var force = new RoundForceId("UPP");
        var govfor = new RoundForceAssignment(RoundSide.Govfor, force, "GovforShip");

        var opfor = govfor with
        {
            Side = RoundSide.Opfor,
            MainShipId = "OpforShip",
        };

        Assert.Multiple(() =>
        {
            Assert.That(govfor.Side, Is.EqualTo(RoundSide.Govfor));
            Assert.That(govfor.Force, Is.EqualTo(force));
            Assert.That(govfor.MainShipId, Is.EqualTo("GovforShip"));
            Assert.That(opfor.Side, Is.EqualTo(RoundSide.Opfor));
            Assert.That(opfor.Force, Is.EqualTo(force));
            Assert.That(opfor.MainShipId, Is.EqualTo("OpforShip"));
            Assert.That(opfor, Is.Not.EqualTo(govfor));
        });
    }
}
