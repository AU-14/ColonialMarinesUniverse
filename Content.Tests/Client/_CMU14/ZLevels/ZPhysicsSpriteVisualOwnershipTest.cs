using System.Numerics;
using Content.Client._CMU14.ZLevels.Core;
using NUnit.Framework;

namespace Content.Tests.Client._CMU14.ZLevels;

[TestFixture]
public sealed class ZPhysicsSpriteVisualOwnershipTest
{
    [TestCase(0f, false)]
    [TestCase(0.0009f, false)]
    [TestCase(-0.0009f, false)]
    [TestCase(0.001f, false)]
    [TestCase(-0.001f, false)]
    [TestCase(0.0011f, true)]
    [TestCase(-0.0011f, true)]
    [TestCase(0.5f, true)]
    public void OnlyNonzeroVisualOffsetsStayActive(float localPosition, bool expected)
    {
        Assert.That(CMUZPhysicsSpriteVisuals.IsActive(localPosition), Is.EqualTo(expected));
    }

    [Test]
    public void ActiveVisualsComposeWithCapturedBaseline()
    {
        var baseline = new CMUZPhysicsSpriteState(
            NoRotation: false,
            DrawDepth: 3,
            Offset: new Vector2(2f, 4f));

        var active = CMUZPhysicsSpriteVisuals.GetActiveState(
            baseline,
            localPosition: 0.5f,
            zLevelOffset: 0.5f,
            elevatedDrawDepth: 8);

        Assert.That(active.NoRotation, Is.True);
        Assert.That(active.DrawDepth, Is.EqualTo(8));
        Assert.That(active.Offset, Is.EqualTo(new Vector2(2f, 4.25f)));
    }

    [Test]
    public void ExternalChangesWhileActiveBecomeNewBaseline()
    {
        var baseline = new CMUZPhysicsSpriteState(false, 3, new Vector2(2f, 4f));
        var applied = new CMUZPhysicsSpriteState(true, 8, new Vector2(2f, 4.25f));
        var current = new CMUZPhysicsSpriteState(true, 6, new Vector2(5f, 7f));

        var refreshed = CMUZPhysicsSpriteVisuals.RefreshBaseline(baseline, applied, current);

        Assert.That(refreshed.NoRotation, Is.False);
        Assert.That(refreshed.DrawDepth, Is.EqualTo(6));
        Assert.That(refreshed.Offset, Is.EqualTo(new Vector2(5f, 7f)));
    }

    [Test]
    public void RestoreOnlyRevertsPropertiesStillOwnedByZVisuals()
    {
        var baseline = new CMUZPhysicsSpriteState(false, 3, new Vector2(2f, 4f));
        var applied = new CMUZPhysicsSpriteState(true, 8, new Vector2(2f, 4.25f));
        var current = new CMUZPhysicsSpriteState(true, 8, new Vector2(5f, 7f));

        var restored = CMUZPhysicsSpriteVisuals.RestoreOwnedState(baseline, applied, current);

        Assert.That(restored.NoRotation, Is.False);
        Assert.That(restored.DrawDepth, Is.EqualTo(3));
        Assert.That(restored.Offset, Is.EqualTo(new Vector2(5f, 7f)));
    }
}
