#nullable enable

using Content.Shared.CMU.Round;
using NUnit.Framework;

namespace Content.Tests.Shared._CMU14.Round.Forces;

[TestFixture]
public sealed class RoundForceAssignmentTest
{
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
