using System;
using System.Linq;
using System.Numerics;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DSlabCladdingTest
{
    [TestCase(0f)]
    [TestCase(.37f)]
    [TestCase(-1.2f)]
    public void EveryCardinalTargetOnRotatedGridPreservesMaterialSourceAndWorldArea(float gridYaw)
    {
        var tile = new Vector2(3, -2);
        var c = MathF.Cos(gridYaw);
        var s = MathF.Sin(gridYaw);
        var offset = new Vector2(.08f, -.03f);
        var center = tile + new Vector2(c * offset.X - s * offset.Y, s * offset.X + c * offset.Y);
        var color = new Color(.2f, .4f, .6f, .8f);
        var source = new EntityUid(137);
        for (var facing = 0; facing < 4; facing++)
        {
            var box = new CMU3DSceneBox(new Vector3(center, .0175f), new Vector3(.5f, .25f, .0175f),
                gridYaw + facing * MathF.PI / 2, color, source);
            Assert.That(CMU3DSlabCladding.TryClip(box, tile, gridYaw, [new Box2(-.1f, -.1f, .1f, .1f)], out var fragments), Is.True);
            Assert.That(fragments.Count, Is.EqualTo(4));
            Assert.That(fragments.Sum(p => p.HalfSize.X * p.HalfSize.Y * 4), Is.EqualTo(.46f).Within(.000002));
            foreach (var fragment in fragments)
            {
                Assert.That(fragment.Source, Is.EqualTo(source));
                Assert.That(fragment.Color, Is.EqualTo(color));
                Assert.That(fragment.Center.Z, Is.EqualTo(box.Center.Z));
                Assert.That(fragment.HalfSize.Z, Is.EqualTo(box.HalfSize.Z));
                Assert.That(fragment.Yaw, Is.EqualTo(gridYaw));
                Assert.That(fragment.SurfaceIndex, Is.Zero);
                Assert.That(fragment.Pitch, Is.Zero);
                var delta = new Vector2(fragment.Center.X, fragment.Center.Y) - tile;
                var gx = c * delta.X + s * delta.Y;
                var gy = -s * delta.X + c * delta.Y;
                var overlapX = Math.Min(gx + fragment.HalfSize.X, .1f) - Math.Max(gx - fragment.HalfSize.X, -.1f);
                var overlapY = Math.Min(gy + fragment.HalfSize.Y, .1f) - Math.Max(gy - fragment.HalfSize.Y, -.1f);
                Assert.That(overlapX <= .000001f || overlapY <= .000001f, Is.True, "No cladding remains inside the aperture");
            }
        }
    }

    [Test]
    public void UnaffectedBoxIsByteForValueUnchangedAndCoveredBoxDisappears()
    {
        var box = new CMU3DSceneBox(new Vector3(0, 0, .01f), new Vector3(.1f, .1f, .01f), MathF.PI, Color.Gray, new EntityUid(23));
        Assert.That(CMU3DSlabCladding.TryClip(box, Vector2.Zero, 0, [new Box2(.2f, .2f, .4f, .4f)], out var unchanged), Is.True);
        Assert.That(unchanged, Is.EqualTo(new[] { box }));
        Assert.That(CMU3DSlabCladding.TryClip(box, Vector2.Zero, 0, [new Box2(-.2f, -.2f, .2f, .2f)], out var removed), Is.True);
        Assert.That(removed, Is.Empty);
    }

    [TestCase("texture")]
    [TestCase("shape")]
    [TestCase("pitch")]
    [TestCase("noncardinal")]
    [TestCase("high")]
    [TestCase("low")]
    [TestCase("nonfinite")]
    [TestCase("zero-size")]
    [TestCase("invalid-grid")]
    [TestCase("invalid-tile")]
    [TestCase("too-many-openings")]
    public void UnsupportedGeometryKeepsOriginalCapturedBoxAtomically(string defect)
    {
        var box = new CMU3DSceneBox(new Vector3(0, 0, .0175f), new Vector3(.5f, .5f, .0175f), 0, Color.Gray, new EntityUid(23));
        var gridYaw = 0f;
        var tile = Vector2.Zero;
        var openings = new[] { new Box2(-.2f, -.2f, .2f, .2f) };
        switch (defect)
        {
            case "texture": box = box with { SurfaceIndex = 12 }; break;
            case "shape": box = box with { Shape = CMU3DPartShape.Ellipsoid }; break;
            case "pitch": box = box with { Pitch = .01f }; break;
            case "noncardinal": box = box with { Yaw = .1f }; break;
            case "high": box = box with { Center = new Vector3(0, 0, .1f) }; break;
            case "low": box = box with { Center = new Vector3(0, 0, -.1f) }; break;
            case "nonfinite": box = box with { HalfSize = new Vector3(float.PositiveInfinity, .5f, .0175f) }; break;
            case "zero-size": box = box with { HalfSize = new Vector3(0, .5f, .0175f) }; break;
            case "invalid-grid": gridYaw = float.NaN; break;
            case "invalid-tile": tile.X = float.PositiveInfinity; break;
            case "too-many-openings": openings = Enumerable.Repeat(openings[0], 17).ToArray(); break;
        }
        Assert.That(CMU3DSlabCladding.TryClip(box, tile, gridYaw, openings, out var fragments), Is.False);
        Assert.That(fragments, Is.EqualTo(new[] { box }));
    }
}
