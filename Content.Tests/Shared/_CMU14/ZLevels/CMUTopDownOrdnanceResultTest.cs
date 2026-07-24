using System.Numerics;
using Content.Shared._CMU14.ZLevels.Ordnance;
using NUnit.Framework;
using Robust.Shared.Map;

namespace Content.Tests.Shared._CMU14.ZLevels;

[TestFixture]
public sealed class CMUTopDownOrdnanceResultTest
{
    [Test]
    public void ResetClearsOutcomeAndRetainsSurfaceBuffer()
    {
        var first = new MapCoordinates(new Vector2(1, 2), new MapId(4));
        var second = new MapCoordinates(new Vector2(3, 5), new MapId(7));
        var result = new CMUTopDownOrdnanceResult(first)
        {
            UsesZLevels = true,
            BlockReason = CMUTopDownOrdnanceBlockReason.Roofed,
        };
        var surfaces = result.Surfaces;
        result.Surfaces.Add(new CMUTopDownOrdnanceSurface(first, 3));

        result.Reset(second);

        Assert.Multiple(() =>
        {
            Assert.That(result.Selected, Is.EqualTo(second));
            Assert.That(result.UsesZLevels, Is.False);
            Assert.That(result.BlockReason, Is.EqualTo(CMUTopDownOrdnanceBlockReason.None));
            Assert.That(result.Surfaces, Is.SameAs(surfaces));
            Assert.That(result.Surfaces, Is.Empty);
            Assert.That(result.FirstImpact, Is.Null);
            Assert.That(result.TerminalImpact, Is.Null);
            Assert.That(result.Redirected, Is.False);
        });
    }
}
