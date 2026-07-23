using System.Numerics;
using Content.Client._CMU14.ZLevels.Core;
using NUnit.Framework;

namespace Content.Tests.Client._CMU14.ZLevels;

[TestFixture]
public sealed class ProjectileSpriteVisualOwnershipTest
{
    [Test]
    public void PredictionHandoffDoesNotApplyOffsetTwice()
    {
        var baseline = new Vector2(2f, 3f);
        var visualOffset = new Vector2(0f, 0.7f);
        Vector2? predictedOriginal = null;
        var predictedApplied = Vector2.Zero;
        var current = CMUZProjectileSpriteVisuals.Apply(
            baseline,
            visualOffset,
            ref predictedOriginal,
            ref predictedApplied);

        Vector2? replicatedOriginal = null;
        var replicatedApplied = Vector2.Zero;
        CMUZProjectileSpriteVisuals.TransferOwnership(
            predictedOriginal,
            predictedApplied,
            ref replicatedOriginal,
            ref replicatedApplied);

        current = CMUZProjectileSpriteVisuals.Apply(
            current,
            visualOffset,
            ref replicatedOriginal,
            ref replicatedApplied);

        Assert.Multiple(() =>
        {
            Assert.That(current, Is.EqualTo(baseline + visualOffset));
            Assert.That(replicatedOriginal, Is.EqualTo(baseline));
            Assert.That(replicatedApplied, Is.EqualTo(visualOffset));
        });
    }

    [Test]
    public void RestoreRemovesOnlyOwnedOffset()
    {
        var original = new Vector2(2f, 3f);
        var applied = new Vector2(0f, 0.7f);
        var externallyAdjusted = original + applied + new Vector2(1f, 0f);

        var restored = CMUZProjectileSpriteVisuals.Restore(
            externallyAdjusted,
            original,
            applied);

        Assert.That(restored, Is.EqualTo(original + new Vector2(1f, 0f)));
    }
}
