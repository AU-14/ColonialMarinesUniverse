using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DSlabOpeningTest
{
    private static readonly Box2 Tile = new(-.5f, -.5f, .5f, .5f);

    [TestCase("null")]
    [TestCase("nan")]
    [TestCase("infinity")]
    [TestCase("outside-min")]
    [TestCase("outside-max")]
    [TestCase("zero-width")]
    [TestCase("zero-height")]
    [TestCase("reversed")]
    public void InvalidMetadataDoesNotProduceAnOpening(string defect)
    {
        CMU3DSlabOpeningBounds? bounds = Bounds();
        switch (defect)
        {
            case "null": bounds = null; break;
            case "nan": bounds.Min.X = float.NaN; break;
            case "infinity": bounds.Max.Y = float.PositiveInfinity; break;
            case "outside-min": bounds.Min.X = -.50001f; break;
            case "outside-max": bounds.Max.Y = .50001f; break;
            case "zero-width": bounds.Max.X = bounds.Min.X; break;
            case "zero-height": bounds.Max.Y = bounds.Min.Y; break;
            case "reversed": bounds.Min.Y = bounds.Max.Y + .01f; break;
        }

        Assert.That(CMU3DSlabOpening.ValidBounds(bounds), Is.False);
        Assert.That(CMU3DSlabOpening.TryTransform(bounds, Vector2.Zero, 0, 0, out var result), Is.False);
        Assert.That(result, Is.EqualTo(default(Box2)));
    }

    [TestCase(0f)]
    [TestCase(.37f)]
    [TestCase(-1.21f)]
    [TestCase(2.9f)]
    public void ArbitrarilyRotatedGridPreservesAllFourRelativeDirectionsAndOffsets(float gridYaw)
    {
        var bounds = Bounds();
        var offset = new Vector2(.03f, -.02f);
        var expected = new[]
        {
            new Box2(-.37f, -.32f, .13f, .18f),
            new Box2(-.17f, -.42f, .33f, .08f),
            new Box2(-.07f, -.22f, .43f, .28f),
            new Box2(-.27f, -.12f, .23f, .38f),
        };
        for (var facing = 0; facing < 4; facing++)
        {
            var entityYaw = gridYaw + facing * MathF.PI / 2;
            Assert.That(CMU3DSlabOpening.TryTransform(bounds, offset, entityYaw, gridYaw, out var result), Is.True);
            AssertRectangle(result, expected[facing]);
            Assert.That(CMU3DSlabOpening.TryTransform(bounds, offset, entityYaw - MathF.Tau, gridYaw, out var wrapped), Is.True);
            AssertRectangle(wrapped, expected[facing]);
        }
        Assert.That(bounds.Min, Is.EqualTo(new Vector2(-.4f, -.3f)));
        Assert.That(bounds.Max, Is.EqualTo(new Vector2(.1f, .2f)));
    }

    [Test]
    public void OnlyNearCardinalYawIsSnappedAndNoOffTileApertureIsTruncated()
    {
        var bounds = Bounds();
        Assert.That(CMU3DSlabOpening.TryTransform(bounds, Vector2.Zero,
            MathF.PI / 2 + CMU3DSlabOpening.CardinalTolerance / 2, 0, out var snapped), Is.True);
        AssertRectangle(snapped, new Box2(-.2f, -.4f, .3f, .1f));
        Assert.That(CMU3DSlabOpening.TryTransform(bounds, Vector2.Zero,
            MathF.PI / 2 + CMU3DSlabOpening.CardinalTolerance * 2, 0, out _), Is.False);
        Assert.That(CMU3DSlabOpening.TryTransform(bounds, Vector2.Zero, .4f, 0, out _), Is.False);
        Assert.That(CMU3DSlabOpening.TryTransform(bounds, new Vector2(-.2f, 0), 0, 0, out var offTile), Is.False);
        Assert.That(offTile, Is.EqualTo(default(Box2)));
        Assert.That(CMU3DSlabOpening.TryTransform(bounds, new Vector2(.6f, 0), 0, 0, out _), Is.False);
    }

    [TestCase(float.NaN, 0f, 0f)]
    [TestCase(0f, float.PositiveInfinity, 0f)]
    [TestCase(0f, 0f, float.NegativeInfinity)]
    public void NonfiniteSourceTransformFailsConservatively(float offset, float entityYaw, float gridYaw)
    {
        Assert.That(CMU3DSlabOpening.TryTransform(Bounds(), new Vector2(offset, 0), entityYaw, gridYaw, out var result), Is.False);
        Assert.That(result, Is.EqualTo(default(Box2)));
    }

    [Test]
    public void CentralAperturePreservesFourDisjointEdgeStrips()
    {
        Box2[] openings = [new(-.25f, -.25f, .25f, .25f)];
        Assert.That(CMU3DSlabOpening.TrySubtract(Tile, openings, out var fragments), Is.True);
        Assert.That(fragments.Count, Is.EqualTo(4));
        Assert.That(Area(fragments), Is.EqualTo(.75).Within(.000001));
        AssertCoverage(Tile, openings, fragments);
    }

    [Test]
    public void OverlapDuplicatesContainedAndOutsideCutsSubtractTheirUnionExactlyOnce()
    {
        Box2[] openings =
        [
            new(-.2f, -.8f, .2f, .8f), new(-.8f, -.15f, .8f, .15f),
            new(-.2f, -.8f, .2f, .8f), new(-.05f, -.05f, .05f, .05f),
            new(.5f, -.5f, .7f, .5f),
        ];
        Assert.That(CMU3DSlabOpening.TrySubtract(Tile, openings, out var fragments), Is.True);
        Assert.That(Area(fragments), Is.EqualTo(.42).Within(.000001));
        AssertCoverage(Tile, openings, fragments);
        Array.Reverse(openings);
        Assert.That(CMU3DSlabOpening.TrySubtract(Tile, openings, out var reversed), Is.True);
        AssertCoverage(Tile, openings, reversed);
        Assert.That(Area(reversed), Is.EqualTo(Area(fragments)).Within(.000001));
    }

    [Test]
    public void SixteenCrossingCutsRetainEverySmallIslandWithinBudget()
    {
        var openings = new List<Box2>();
        for (var index = 0; index < 8; index++)
        {
            var position = -.4f + index * .1f;
            openings.Add(new Box2(position, -.5f, position + .015f, .5f));
        }
        for (var index = 0; index < 8; index++)
        {
            var position = -.4f + index * .1f;
            openings.Add(new Box2(-.5f, position, .5f, position + .015f));
        }
        Assert.That(openings.Count, Is.EqualTo(CMU3DSlabOpening.MaxOpenings));
        Assert.That(CMU3DSlabOpening.TrySubtract(Tile, openings, out var fragments), Is.True);
        Assert.That(fragments.Count, Is.EqualTo(81));
        AssertCoverage(Tile, openings, fragments);
    }

    [Test]
    public void ASubTileCladdingSlabKeepsItsOwnBoundary()
    {
        var slab = new Box2(-.45f, -.3f, .3f, .4f);
        Box2[] openings = [new(-.5f, -.4f, -.2f, .1f), new(.1f, -.1f, .5f, .5f)];
        Assert.That(CMU3DSlabOpening.TrySubtract(slab, openings, out var fragments), Is.True);
        AssertCoverage(slab, openings, fragments);
    }

    [Test]
    public void NoIntersectionsKeepTheSlabAndFullCoverageRemovesIt()
    {
        Assert.That(CMU3DSlabOpening.TrySubtract(Tile, [], out var unchanged), Is.True);
        Assert.That(unchanged, Is.EqualTo(new[] { Tile }));
        Assert.That(CMU3DSlabOpening.TrySubtract(Tile, [new Box2(.5f, -.5f, .8f, .5f)], out var touching), Is.True);
        Assert.That(touching, Is.EqualTo(new[] { Tile }));
        Assert.That(CMU3DSlabOpening.TrySubtract(Tile, [Tile], out var removed), Is.True);
        Assert.That(removed, Is.Empty);
    }

    [Test]
    public void LaterFragmentBudgetFailureRestoresTheWholeOriginalSlab()
    {
        Box2[] cuts = [new(-.1f, -.5f, .1f, .5f), new(-.5f, -.1f, .5f, .1f)];
        Assert.That(CMU3DSlabOpening.TrySubtract(Tile, cuts, out var fragments, maxFragments: 3), Is.False);
        Assert.That(fragments, Is.EqualTo(new[] { Tile }));
        Assert.That(cuts[0], Is.EqualTo(new Box2(-.1f, -.5f, .1f, .5f)));
        Assert.That(cuts[1], Is.EqualTo(new Box2(-.5f, -.1f, .5f, .1f)));
    }

    [TestCase("too-many")]
    [TestCase("invalid-later")]
    [TestCase("nonfinite-later")]
    [TestCase("zero-budget")]
    [TestCase("excess-budget")]
    public void InvalidInputNeverLeaksAPartialCut(string defect)
    {
        var openings = new List<Box2> { new(-.25f, -.25f, .25f, .25f) };
        var budget = CMU3DSlabOpening.MaxFragments;
        switch (defect)
        {
            case "too-many": while (openings.Count <= CMU3DSlabOpening.MaxOpenings) openings.Add(Tile); break;
            case "invalid-later": openings.Add(new Box2(0, 0, 0, .1f)); break;
            case "nonfinite-later": openings.Add(new Box2(float.NaN, 0, .1f, .1f)); break;
            case "zero-budget": budget = 0; break;
            case "excess-budget": budget++; break;
        }
        Assert.That(CMU3DSlabOpening.TrySubtract(Tile, openings, out var fragments, budget), Is.False);
        Assert.That(fragments, Is.EqualTo(new[] { Tile }));
    }

    [Test]
    public void InvalidSlabProducesNoGeometry()
    {
        Assert.That(CMU3DSlabOpening.TrySubtract(new Box2(0, 0, 0, 1), [], out var zero), Is.False);
        Assert.That(zero, Is.Empty);
        Assert.That(CMU3DSlabOpening.TrySubtract(new Box2(float.NegativeInfinity, 0, 1, 1), [], out var infinite), Is.False);
        Assert.That(infinite, Is.Empty);
    }

    private static CMU3DSlabOpeningBounds Bounds() => new() { Min = new Vector2(-.4f, -.3f), Max = new Vector2(.1f, .2f) };

    private static double Area(IEnumerable<Box2> rectangles) =>
        rectangles.Sum(rectangle => ((double) rectangle.Right - rectangle.Left) * (rectangle.Top - rectangle.Bottom));

    private static void AssertRectangle(Box2 actual, Box2 expected)
    {
        Assert.That(actual.Left, Is.EqualTo(expected.Left).Within(.000001));
        Assert.That(actual.Bottom, Is.EqualTo(expected.Bottom).Within(.000001));
        Assert.That(actual.Right, Is.EqualTo(expected.Right).Within(.000001));
        Assert.That(actual.Top, Is.EqualTo(expected.Top).Within(.000001));
    }

    // Independent arrangement oracle: every edge interval has constant union membership.
    // It detects holes, overlap, lost islands and area changes without reproducing the subtraction algorithm.
    private static void AssertCoverage(Box2 slab, IReadOnlyList<Box2> openings, IReadOnlyList<Box2> fragments)
    {
        var xs = new SortedSet<float> { slab.Left, slab.Right };
        var ys = new SortedSet<float> { slab.Bottom, slab.Top };
        foreach (var rectangle in openings.Concat(fragments))
        {
            xs.Add(Math.Clamp(rectangle.Left, slab.Left, slab.Right));
            xs.Add(Math.Clamp(rectangle.Right, slab.Left, slab.Right));
            ys.Add(Math.Clamp(rectangle.Bottom, slab.Bottom, slab.Top));
            ys.Add(Math.Clamp(rectangle.Top, slab.Bottom, slab.Top));
        }
        foreach (var fragment in fragments)
        {
            Assert.That(fragment.Left, Is.GreaterThanOrEqualTo(slab.Left));
            Assert.That(fragment.Right, Is.LessThanOrEqualTo(slab.Right));
            Assert.That(fragment.Bottom, Is.GreaterThanOrEqualTo(slab.Bottom));
            Assert.That(fragment.Top, Is.LessThanOrEqualTo(slab.Top));
            Assert.That(fragment.Width, Is.GreaterThan(0));
            Assert.That(fragment.Height, Is.GreaterThan(0));
        }
        var x = xs.ToArray();
        var y = ys.ToArray();
        var expectedArea = 0d;
        for (var xi = 0; xi < x.Length - 1; xi++)
        for (var yi = 0; yi < y.Length - 1; yi++)
        {
            var px = ((double) x[xi] + x[xi + 1]) / 2;
            var py = ((double) y[yi] + y[yi + 1]) / 2;
            bool Contains(Box2 rectangle) => px > rectangle.Left && px < rectangle.Right && py > rectangle.Bottom && py < rectangle.Top;
            var expected = openings.Any(Contains) ? 0 : 1;
            Assert.That(fragments.Count(Contains), Is.EqualTo(expected), $"Union membership at ({px},{py})");
            if (expected == 1)
                expectedArea += ((double) x[xi + 1] - x[xi]) * (y[yi + 1] - y[yi]);
        }
        Assert.That(Area(fragments), Is.EqualTo(expectedArea).Within(.000001));
    }
}
