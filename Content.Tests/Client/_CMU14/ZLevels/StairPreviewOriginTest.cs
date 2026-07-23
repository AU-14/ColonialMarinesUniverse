using System.Numerics;
using Content.Client.Viewport;
using Content.Shared._CMU14.ZLevels.Core.Components;
using NUnit.Framework;

namespace Content.Tests.Client._CMU14.ZLevels;

[TestFixture]
public sealed class StairPreviewOriginTest
{
    [Test]
    public void CountMakesWorldOriginAValidPreviewPosition()
    {
#pragma warning disable RA0002 // Test setup constructs the replicated viewer state consumed by the client.
        var viewer = new CMUZLevelViewerComponent
        {
            StairPreviewPositionCount = 1,
            StairPreviewPosition = Vector2.Zero,
        };
#pragma warning restore RA0002

        var found = ScalingViewport.TryGetStairPreviewPosition(viewer, 0, out var position);

        Assert.That(found, Is.True);
        Assert.That(position, Is.EqualTo(Vector2.Zero));
    }

    [Test]
    public void PositionOutsideReplicatedCountIsInvalid()
    {
#pragma warning disable RA0002 // Test setup constructs the replicated viewer state consumed by the client.
        var viewer = new CMUZLevelViewerComponent
        {
            StairPreviewPositionCount = 1,
            StairPreviewPosition2 = new Vector2(4f, 8f),
        };
#pragma warning restore RA0002

        Assert.That(ScalingViewport.TryGetStairPreviewPosition(viewer, 1, out _), Is.False);
    }
}
