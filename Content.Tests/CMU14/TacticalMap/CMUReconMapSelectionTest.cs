using Content.Shared.CMU14.TacticalMap.Reconstruction;
using NUnit.Framework;

namespace Content.Tests.CMU14.TacticalMap;

[TestFixture]
public sealed class CMUReconMapSelectionTest
{
    [TestCase(false, false, CMUReconMapChoice.Planet)]
    [TestCase(false, true, CMUReconMapChoice.Planet)]
    [TestCase(true, false, CMUReconMapChoice.Ship)]
    [TestCase(true, true, CMUReconMapChoice.Planet)]
    public void OpeningUsesLocationAndOnlyAppliesTheRememberedChoiceAboardShip(bool aboardShip, bool rememberedPlanet, CMUReconMapChoice expected)
    {
        Assert.That(CMUReconMapSelection.Choose(CMUReconMapChoice.Automatic, aboardShip, rememberedPlanet, true, true), Is.EqualTo(expected));
    }

    [Test]
    public void MissingDestinationFallsBackAndExplicitSelectionOverridesTheOpeningDefault()
    {
        Assert.That(CMUReconMapSelection.Choose(CMUReconMapChoice.Automatic, true, true, false, true), Is.EqualTo(CMUReconMapChoice.Ship));
        Assert.That(CMUReconMapSelection.Choose(CMUReconMapChoice.Automatic, true, false, true, false), Is.EqualTo(CMUReconMapChoice.Planet));
        Assert.That(CMUReconMapSelection.Choose(CMUReconMapChoice.Ship, false, true, true, true), Is.EqualTo(CMUReconMapChoice.Ship));
        Assert.That(CMUReconMapSelection.Choose(CMUReconMapChoice.Planet, true, false, true, true), Is.EqualTo(CMUReconMapChoice.Planet));
        Assert.That(CMUReconMapSelection.Choose(CMUReconMapChoice.Ship, true, false, false, false), Is.EqualTo(CMUReconMapChoice.Automatic));
    }
}
