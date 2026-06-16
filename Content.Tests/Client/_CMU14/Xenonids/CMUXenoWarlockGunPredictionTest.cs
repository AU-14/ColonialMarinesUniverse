using Content.Client._RMC14.Weapons.Ranged.Prediction;
using NUnit.Framework;

namespace Content.Tests.Client._CMU14.Xenonids;

[TestFixture]
public sealed class CMUXenoWarlockGunPredictionTest
{
    [Test]
    public void FrozenWarlockShieldProjectilesDoNotRestorePreSolveCoordinates()
    {
        Assert.That(GunPredictionSystem.ShouldRestorePredictedProjectileCoordinates(frozenByWarlockShield: false), Is.True);
        Assert.That(GunPredictionSystem.ShouldRestorePredictedProjectileCoordinates(frozenByWarlockShield: true), Is.False);
    }

    [Test]
    public void OnlyLocalClientPredictedProjectileCopiesAreRetired()
    {
        Assert.That(
            GunPredictionSystem.ShouldRetirePredictedProjectileCopy(
                serverProjectileBelongsToLocalPlayer: true,
                clientCopyExists: true,
                clientCopyIsClientSide: true,
                clientCopyIsPredicted: true),
            Is.True);

        Assert.That(
            GunPredictionSystem.ShouldRetirePredictedProjectileCopy(
                serverProjectileBelongsToLocalPlayer: false,
                clientCopyExists: true,
                clientCopyIsClientSide: true,
                clientCopyIsPredicted: true),
            Is.False);

        Assert.That(
            GunPredictionSystem.ShouldRetirePredictedProjectileCopy(
                serverProjectileBelongsToLocalPlayer: true,
                clientCopyExists: true,
                clientCopyIsClientSide: false,
                clientCopyIsPredicted: true),
            Is.False);

        Assert.That(
            GunPredictionSystem.ShouldRetirePredictedProjectileCopy(
                serverProjectileBelongsToLocalPlayer: true,
                clientCopyExists: true,
                clientCopyIsClientSide: true,
                clientCopyIsPredicted: false),
            Is.False);
    }
}
