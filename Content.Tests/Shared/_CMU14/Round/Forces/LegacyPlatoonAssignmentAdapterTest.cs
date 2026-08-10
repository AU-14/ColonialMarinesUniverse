#nullable enable

using Content.Shared.CMU.Round;
using NUnit.Framework;

namespace Content.Tests.Shared._CMU14.Round.Forces;

[TestFixture]
public sealed class LegacyPlatoonAssignmentAdapterTest
{
    [Test]
    public void ConvertsASelectedPlatoonIntoAForceAssignment()
    {
        var assignment = LegacyPlatoonAssignmentAdapter.FromLegacySelection(
            RoundSide.Opfor,
            "UPP",
            "USSBushRedux");

        Assert.That(
            assignment,
            Is.EqualTo(
                new RoundForceAssignment(
                    RoundSide.Opfor,
                    new RoundForceId("UPP"),
                    "USSBushRedux")));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void MissingLegacyPlatoonHasNoForceAssignment(string? legacyPlatoonId)
    {
        var assignment = LegacyPlatoonAssignmentAdapter.FromLegacySelection(
            RoundSide.Govfor,
            legacyPlatoonId,
            "USSBushRedux");

        Assert.That(assignment, Is.Null);
    }
}
