using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DTerrainCutoutTest
{
    private static CMU3DModelPart Rock() => new() { Label = "rock", Min = new Vector3(-.5f, -.5f, 0), Max = new Vector3(.5f, .5f, 2.8f), Color = Color.Brown };

    [Test]
    public void AlcoveRetainsUpperRockOutsideStripsAndPickingIdentity()
    {
        var rock = Rock();
        var cut = new CMU3DTerrainVolume(new Vector3(-.49f, -.49f, 0), new Vector3(.49f, .49f, 1.1f));
        Assert.That(CMU3DTerrainCutout.TryClip([rock], Vector2.Zero, 0, [cut], out var parts), Is.True);
        Assert.That(parts.Count, Is.EqualTo(5));
        Assert.That(parts.Sum(p => Volume(p.Min, p.Max)), Is.EqualTo(2.8f - .98f * .98f * 1.1f).Within(.00001f));
        Assert.That(rock.Min, Is.EqualTo(new Vector3(-.5f, -.5f, 0)));
        Assert.That(parts.All(p => p.Color == Color.Brown), Is.True);
        var uid = new EntityUid(421);
        var encoding = new CMU3DSceneEncoding();
        encoding.Build(parts.Select(p => new CMU3DSceneBox((p.Min + p.Max) / 2, (p.Max - p.Min) / 2, 0, p.Color, uid)).ToArray());
        Assert.That(encoding.OmittedBoxes, Is.Zero);
        Assert.That(encoding.TryPick(new Vector3(0, 0, .5f), -Vector3.UnitZ, out _), Is.False);
        Assert.That(encoding.TryPick(new Vector3(0, 0, .5f), Vector3.UnitZ, out var hit), Is.True);
        Assert.That(hit.Source, Is.EqualTo(uid));
        Assert.That(hit.Position.Z, Is.EqualTo(1.1f).Within(.001f));
    }

    [Test]
    public void FullyInternalVolumeProducesSixDisjointSlabs()
    {
        var source = new CMU3DModelPart { Min = new Vector3(-2), Max = new Vector3(2) };
        Assert.That(CMU3DTerrainCutout.TryClip([source], Vector2.Zero, 0,
            [new CMU3DTerrainVolume(new Vector3(-1), new Vector3(1))], out var parts), Is.True);
        Assert.That(parts.Count, Is.EqualTo(6));
        Assert.That(parts.Sum(p => Volume(p.Min, p.Max)), Is.EqualTo(56));
        for (var i = 0; i < parts.Count; i++)
        for (var j = i + 1; j < parts.Count; j++)
            Assert.That(CMU3DTerrainCutout.Overlaps(parts[i].Min, parts[i].Max, parts[j].Min, parts[j].Max), Is.False);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void EntityAndPartQuarterTurnsPreserveSubtractedVolume(int entityTurn)
    {
        var position = new Vector2(10, -7);
        var yaw = entityTurn * MathF.PI / 2;
        Assert.That(CMU3DTerrainCutout.TryWorldBounds(new Vector3(-.2f, -.2f, 0), new Vector3(.2f, .2f, 1), position, yaw, out var cut), Is.True);
        for (var partTurn = 0; partTurn < 4; partTurn++)
        {
            var source = new CMU3DModelPart { Min = new Vector3(-1, -.5f, 0), Max = new Vector3(1, .5f, 2), Yaw = partTurn * 90 };
            Assert.That(CMU3DTerrainCutout.TryClip([source], position, yaw, [cut], out var parts), Is.True);
            Assert.That(parts.Sum(p => Volume(p.Min, p.Max)), Is.EqualTo(3.84f).Within(.00001f));
            foreach (var part in parts)
            {
                part.Bounds(out var low, out var high);
                Assert.That(CMU3DTerrainCutout.Overlaps(low, high, new Vector3(-.2f, -.2f, 0), new Vector3(.2f, .2f, 1)), Is.False);
            }
        }
    }

    [Test]
    public void UnsupportedIntersectingGeometryAndBudgetFailWithoutPartialMutation()
    {
        var cut = new CMU3DTerrainVolume(new Vector3(-.25f, -.25f, 0), new Vector3(.25f, .25f, 1));
        foreach (var unsupported in new[]
                 {
                     new CMU3DModelPart { Min = Rock().Min, Max = Rock().Max, Shape = CMU3DPartShape.Ellipsoid },
                     new CMU3DModelPart { Min = Rock().Min, Max = Rock().Max, Yaw = 12 },
                     new CMU3DModelPart { Min = Rock().Min, Max = Rock().Max, Pitch = 10 },
                     new CMU3DModelPart { Min = Rock().Min, Max = Rock().Max, Surface = "Texture" },
                 })
        {
            IReadOnlyList<CMU3DModelPart> original = new[] { Rock(), unsupported };
            Assert.That(CMU3DTerrainCutout.TryClip(original, Vector2.Zero, 0, [cut], out var parts), Is.False);
            Assert.That(parts, Is.SameAs(original));
        }
        IReadOnlyList<CMU3DModelPart> many = Enumerable.Range(0, 128).Select(_ => Rock()).ToArray();
        Assert.That(CMU3DTerrainCutout.TryClip(many, Vector2.Zero, 0, [cut], out var unchanged), Is.False);
        Assert.That(unchanged, Is.SameAs(many));
    }

    [Test]
    public void DuplicateCutsDoNotConsumeBudgetAndTinySliversFailConservatively()
    {
        var cut = new CMU3DTerrainVolume(new Vector3(-1, -1, 0), new Vector3(1, 1, 1.1f));
        Assert.That(CMU3DTerrainCutout.TryClip([Rock()], Vector2.Zero, 0, Enumerable.Repeat(cut, 20).ToArray(), out var parts), Is.True);
        Assert.That(parts.Count, Is.EqualTo(1));
        IReadOnlyList<CMU3DModelPart> source = new[] { Rock() };
        var thin = new CMU3DTerrainVolume(new Vector3(-.4999f, -1, 0), new Vector3(1, 1, 1));
        Assert.That(CMU3DTerrainCutout.TryClip(source, Vector2.Zero, 0, [thin], out var unchanged), Is.False);
        Assert.That(unchanged, Is.SameAs(source));
        Assert.That(CMU3DTerrainCutout.TryWorldBounds(cut.Min, cut.Max, Vector2.Zero, .1f, out _), Is.False);
    }

    private static float Volume(Vector3 min, Vector3 max) => (max.X - min.X) * (max.Y - min.Y) * (max.Z - min.Z);
}
