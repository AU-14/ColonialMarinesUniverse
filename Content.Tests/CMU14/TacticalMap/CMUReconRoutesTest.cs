using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.CMU14.TacticalMap;

[TestFixture]
public sealed class CMUReconRoutesTest
{
    [Test]
    public void FreehandAllowsFractionalPointsDotsAndMapWideLines()
    {
        Assert.That(CMUReconRoutes.ValidStroke([new(0.25f, 0.75f)], Vector2i.Zero, 1024, 1024, CMUReconInk.Red), Is.True);
        Assert.That(CMUReconRoutes.ValidStroke([new(0, 0), new(1024, 1024), new(0, 0)], Vector2i.Zero, 1024, 1024, CMUReconInk.Blue), Is.True);
        Assert.That(CMUReconRoutes.ValidStroke([new(-12.5f, -3.25f), new(10, 10)], new(-16, -16), 32, 32, CMUReconInk.White), Is.True);
    }

    [Test]
    public void RejectsMalformedOrOutOfBoundsPayloads()
    {
        Assert.That(CMUReconRoutes.ValidStroke([], Vector2i.Zero, 32, 32, CMUReconInk.Yellow), Is.False);
        Assert.That(CMUReconRoutes.ValidStroke(new Vector2[CMUReconRoutes.MaxPoints + 1], Vector2i.Zero, 32, 32, CMUReconInk.Yellow), Is.False);
        foreach (var point in new Vector2[] { new(float.NaN, 0), new(0, float.PositiveInfinity), new(-0.1f, 0), new(33, 0) })
            Assert.That(CMUReconRoutes.ValidStroke([point], Vector2i.Zero, 32, 32, CMUReconInk.Yellow), Is.False);
        Assert.That(CMUReconRoutes.ValidStroke([new(1, 1)], Vector2i.Zero, 32, 32, (CMUReconInk) 255), Is.False);
    }

    [Test]
    public void LongStrokeKeepsBeginningAndEndWithoutStoppingAtSampleLimit()
    {
        var points = new List<Vector2>();
        for (var i = 0; i < 6000; i++) CMUReconRoutes.AddPoint(points, new Vector2(i * 0.1f, MathF.Sin(i * 0.01f)));
        Assert.That(points.Count, Is.LessThanOrEqualTo(CMUReconRoutes.MaxPoints));
        Assert.That(points[0], Is.EqualTo(Vector2.Zero));
        Assert.That(points[^1].X, Is.EqualTo(599.9f).Within(0.001));
        Assert.That(points.Any(p => p.X > 200 && p.X < 400), Is.True, "Compaction must preserve the middle of the stroke too.");
    }

    [Test]
    public void TextAndWidthsAreBoundedWithoutInterpretingMarkup()
    {
        var annotation = new CMUReconAnnotation(0, [new(2, 3)], CMUReconInk.Red, 5, "[Hold] here");
        bool Valid(CMUReconAnnotation value) => CMUReconRoutes.ValidAnnotation(value, Vector2i.Zero, 32, 32, -1, 2);
        Assert.That(Valid(annotation), Is.True);
        Assert.That(Valid(annotation with { Width = float.PositiveInfinity }), Is.False);
        Assert.That(Valid(annotation with { Width = 0 }), Is.False);
        Assert.That(Valid(annotation with { Width = 9 }), Is.False);
        Assert.That(Valid(annotation with { Text = new string('x', 121) }), Is.False);
        Assert.That(Valid(annotation with { Text = "bad\ntext" }), Is.False);
        Assert.That(Valid(annotation with { Text = " " }), Is.False);
        Assert.That(Valid(annotation with { Depth = int.MaxValue }), Is.False);
        Assert.That(Valid(annotation with { Points = [new(2, 3), new(4, 5)] }), Is.False);
    }
}
