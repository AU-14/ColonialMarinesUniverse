using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using NUnit.Framework;

namespace Content.Tests.Shared._CMU14.ZLevels;

[TestFixture]
public sealed class CMUZLevelShootingTest
{
    [Test]
    public void SourceObstructionKeepsShotOnSourceLevel()
    {
        Assert.That(
            ResolveCrossZShotPath(
                hasRequestedOffset: true,
                hasTargetMap: true,
                hasOpening: true,
                sourcePathBlocked: true),
            Is.EqualTo("SameLevel"));
    }

    [Test]
    public void MissingAdjacentLevelKeepsShotOnSourceLevel()
    {
        Assert.That(
            ResolveCrossZShotPath(
                hasRequestedOffset: true,
                hasTargetMap: false,
                hasOpening: false,
                sourcePathBlocked: false),
            Is.EqualTo("SameLevel"));
    }

    [Test]
    public void SolidFloorBlocksCrossLevelShot()
    {
        Assert.That(
            ResolveCrossZShotPath(
                hasRequestedOffset: true,
                hasTargetMap: true,
                hasOpening: false,
                sourcePathBlocked: false),
            Is.EqualTo("BlockedFloor"));
    }

    [Test]
    public void ClearSourcePathAndOpeningProjectsShot()
    {
        Assert.That(
            ResolveCrossZShotPath(
                hasRequestedOffset: true,
                hasTargetMap: true,
                hasOpening: true,
                sourcePathBlocked: false),
            Is.EqualTo("CrossLevel"));
    }

    [Test]
    public void LookUpRequiresAnUpperLevel()
    {
        Assert.That(CanEnableLookUp(hasUpperMap: false, opaqueAbove: false), Is.False);
    }

    [Test]
    public void InvalidLookUpCanStillBeDisabled()
    {
        Assert.That(CanToggleLookUp(currentlyLookingUp: true, canEnable: false), Is.True);
        Assert.That(CanToggleLookUp(currentlyLookingUp: false, canEnable: false), Is.False);
    }

    private static string ResolveCrossZShotPath(
        bool hasRequestedOffset,
        bool hasTargetMap,
        bool hasOpening,
        bool sourcePathBlocked)
    {
        return CMUZLevelShootingSystem.ResolveCrossZShotPath(
                hasRequestedOffset,
                hasTargetMap,
                hasOpening,
                sourcePathBlocked)
            .ToString();
    }

    private static bool CanEnableLookUp(bool hasUpperMap, bool opaqueAbove)
    {
        return CMUSharedZLevelsSystem.CanEnableLookUp(hasUpperMap, opaqueAbove);
    }

    private static bool CanToggleLookUp(bool currentlyLookingUp, bool canEnable)
    {
        return CMUSharedZLevelsSystem.CanToggleLookUp(currentlyLookingUp, canEnable);
    }
}
