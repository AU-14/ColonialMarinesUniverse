using System;
using System.Collections.Generic;
using System.Numerics;

using System.Linq;
using System.Reflection;
using Content.Client.CMU14.ThreeD.Scene;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DSlabAdmissionTest
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestCase(0f)]
    [TestCase(.37f)]
    public void OnlyAdmittedSourceCutsBothRegisteredSlabsWithoutMovingSource(float yaw)
    {
        var system = Fixture(yaw, out var source, out var floor, out var ceiling);
        var boxes = Field<List<CMU3DSceneBox>>(system, "_boxes");
        Apply(system, out var exact, out var fallback);
        Assert.That(boxes, Has.Count.EqualTo(3), "A planned but omitted source must not leave a hole.");
        Field<HashSet<EntityUid>>(system, "_slabAdmitted").Add(source);
        Apply(system, out exact, out fallback);
        Assert.That(boxes, Has.Count.EqualTo(9));
        Assert.That(exact, Is.EqualTo(1));
        Assert.That(fallback, Is.Zero);
        Assert.That(boxes[^1].Source, Is.EqualTo(source));
        Assert.That(boxes[^1].Center, Is.EqualTo(new Vector3(10, 20, 1)));
        AssertFragments(boxes.Take(4), floor);
        AssertFragments(boxes.Skip(4).Take(4), ceiling);
    }

    [Test]
    public void InsufficientSnapshotSpaceRetainsWholeSlabsAndFallsBackToOriginalSprite()
    {
        var system = Fixture(0, out var source, out var floor, out var ceiling);
        var boxes = Field<List<CMU3DSceneBox>>(system, "_boxes");
        while (boxes.Count < 8192)
            boxes.Add(new CMU3DSceneBox(Vector3.Zero, Vector3.One, 0, Color.White));
        Field<HashSet<EntityUid>>(system, "_slabAdmitted").Add(source);
        Field<List<EntityUid>>(system, "_animatedSprites").Add(source);
        Apply(system, out var exact, out var fallback);
        Assert.That(boxes[0], Is.EqualTo(floor));
        Assert.That(boxes[1], Is.EqualTo(ceiling));
        Assert.That(boxes.Any(b => b.Source == source), Is.False);
        Assert.That(Field<List<EntityUid>>(system, "_fallbackSprites"), Is.EqualTo(new[] { source }));
        Assert.That(Field<List<EntityUid>>(system, "_animatedSprites"), Is.Empty);
        Assert.That(exact, Is.Zero);
        Assert.That(fallback, Is.EqualTo(1));
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void CladdingAndSlabsPublishTogetherOrReturnWholeOnUnsupportedMaterial(bool textured, bool preserve)
    {
        var system = Fixture(0, out var source, out var floor, out var ceiling, preserve);
        var target = new EntityUid(4);
        var grate = new CMU3DSceneBox(new Vector3(10, 20, .01f), new Vector3(.5f, .5f, .02f),
            0, Color.White, target, SurfaceIndex: (ushort) (textured ? 1 : 0));
        var boxes = Field<List<CMU3DSceneBox>>(system, "_boxes");
        boxes.Add(grate);
        Field<HashSet<EntityUid>>(system, "_slabAdmitted").Add(source);
        Field<Dictionary<EntityUid, (EntityUid, Vector2i, Vector2, float)>>(system, "_slabCladdings")[target] =
            (new EntityUid(2), new Vector2i(5, 6), new Vector2(10, 20), 0);
        Apply(system, out var exact, out var fallback);
        if (preserve)
        {
            Assert.That(boxes, Has.Count.EqualTo(10));
            Assert.That(boxes.Where(b => b.Source == target), Is.EqualTo(new[] { grate }));
            AssertFragments(boxes.Take(4), floor);
            AssertFragments(boxes.Skip(4).Take(4), ceiling);
            Assert.That(exact, Is.EqualTo(1));
            Assert.That(fallback, Is.Zero);
        }
        else if (textured)
        {
            Assert.That(boxes, Is.EqualTo(new[] { floor, ceiling, grate }));
            Assert.That(exact, Is.Zero);
            Assert.That(fallback, Is.EqualTo(1));
        }
        else
        {
            Assert.That(boxes, Has.Count.EqualTo(13));
            var fragments = boxes.Where(b => b.Source == target).ToArray();
            Assert.That(fragments, Has.Length.EqualTo(4));
            AssertFragments(fragments, grate);
            Assert.That(exact, Is.EqualTo(1));
            Assert.That(fallback, Is.Zero);
        }
    }

    private static CMU3DLiveSceneSystem Fixture(float yaw, out EntityUid source,
        out CMU3DSceneBox floor, out CMU3DSceneBox ceiling, bool preserve = false)
    {
        var system = new CMU3DLiveSceneSystem();
        source = new EntityUid(3);
        var grid = new EntityUid(2);
        var tile = new Vector2i(5, 6);
        floor = new CMU3DSceneBox(new Vector3(10, 20, -.035f), new Vector3(.5f, .5f, .035f), yaw, Color.White);
        ceiling = new CMU3DSceneBox(new Vector3(10, 20, 2.85f), new Vector3(.5f, .5f, .1f), yaw, Color.Gray);
        Field<List<CMU3DSceneBox>>(system, "_boxes").AddRange(new[] { floor, ceiling,
            new CMU3DSceneBox(new Vector3(10, 20, 1), new Vector3(.1f), yaw, Color.White, source) });
        Field<List<(int, EntityUid, Vector2i, bool)>>(system, "_slabTiles").AddRange(new[] { (0, grid, tile, false), (1, grid, tile, true) });
        Field<Dictionary<EntityUid, (EntityUid, Vector2i, Box2?, Box2?, bool)>>(system, "_slabPlans")[source] =
            (grid, tile, new Box2(-.25f, -.4f, .25f, -.1f), new Box2(-.25f, -.4f, .25f, -.1f), preserve);
        return system;
    }

    private static void AssertFragments(IEnumerable<CMU3DSceneBox> fragments, CMU3DSceneBox original)
    {
        var area = 0f;
        foreach (var box in fragments)
        {
            area += box.HalfSize.X * box.HalfSize.Y * 4;
            Assert.That(box.Yaw, Is.EqualTo(original.Yaw));
            Assert.That(box.Color, Is.EqualTo(original.Color));
            Assert.That(box.Center.Z, Is.EqualTo(original.Center.Z));
            Assert.That(box.HalfSize.Z, Is.EqualTo(original.HalfSize.Z));
            var delta = box.Center - original.Center;
            var x = MathF.Cos(box.Yaw) * delta.X + MathF.Sin(box.Yaw) * delta.Y;
            var y = -MathF.Sin(box.Yaw) * delta.X + MathF.Cos(box.Yaw) * delta.Y;
            Assert.That(MathF.Abs(x) < box.HalfSize.X && MathF.Abs(y + .25f) < box.HalfSize.Y, Is.False);
        }
        Assert.That(area, Is.EqualTo(.85f).Within(.00001));
    }

    private static T Field<T>(CMU3DLiveSceneSystem system, string name) =>
        (T) typeof(CMU3DLiveSceneSystem).GetField(name, Private)!.GetValue(system)!;

    private static void Apply(CMU3DLiveSceneSystem system, out int exact, out int fallback)
    {
        object[] args = [1, 0];
        typeof(CMU3DLiveSceneSystem).GetMethod("ApplySlabOpenings", Private)!.Invoke(system, args);
        exact = (int) args[0];
        fallback = (int) args[1];
    }
}
