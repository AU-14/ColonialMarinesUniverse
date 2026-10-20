using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DSceneDetailTest
{
    [Test]
    public void DistantRockRetainsItsSolidFootprintAndCrownHeight()
    {
        CMU3DModelPart[] parts =
        [
            new() { Min = new(-.5f, -.5f, 0), Max = new(.5f, .5f, 2.2f) },
            new() { Min = new(-.2f, -.54f, .5f), Max = new(.2f, -.49f, 1) },
            new() { Min = new(-.5f, -.5f, 2.2f), Max = new(0, .5f, 2.8f) },
        ];
        var near = Encode(parts);
        var distant = Encode(CMU3DSceneDetail.DistantWall(parts));
        Assert.That(distant.AcceptedBoxes, Is.LessThan(near.AcceptedBoxes));
        foreach (var x in new[] { -.45f, .45f })
        {
            var origin = new Vector3(x, -2, 1.5f);
            Assert.That(near.TryPick(origin, Vector3.UnitY, out var fullHit), Is.True);
            Assert.That(distant.TryPick(origin, Vector3.UnitY, out var distantHit), Is.True);
            Assert.That(distantHit.Position, Is.EqualTo(fullHit.Position));
        }
        Assert.That(distant.TryPick(new Vector3(0, 0, 4), -Vector3.UnitZ, out var top), Is.True);
        Assert.That(top.Position.Z, Is.EqualTo(2.8f).Within(.002));
        Assert.That(distant.TryPick(new Vector3(.6f, -2, 1), Vector3.UnitY, out _), Is.False);
        // Detail selection must not overwrite the source used when approaching again.
        Assert.That(near.TryPick(new Vector3(.25f, -2, 2.5f), Vector3.UnitY, out _), Is.False);
        Assert.That(Encode(parts).TryPick(new Vector3(.25f, -2, 2.5f), Vector3.UnitY, out _), Is.False);
    }

    [Test]
    public void DistantFrameKeepsItsOpening()
    {
        CMU3DModelPart[] frame =
        [
            new() { Min = new(-.5f, -.5f, 0), Max = new(-.35f, .5f, 2.8f) },
            new() { Min = new(.35f, -.5f, 0), Max = new(.5f, .5f, 2.8f) },
            new() { Min = new(-.5f, -.5f, 2.5f), Max = new(.5f, .5f, 2.8f) },
        ];
        var distant = Encode(CMU3DSceneDetail.DistantWall(frame));
        Assert.That(distant.TryPick(new Vector3(0, -2, 1.5f), Vector3.UnitY, out _), Is.False);
        Assert.That(distant.TryPick(new Vector3(.4f, -2, 1.5f), Vector3.UnitY, out _), Is.True);
        Assert.That(distant.TryPick(new Vector3(0, -2, 2.7f), Vector3.UnitY, out _), Is.True);
    }

    private static CMU3DSceneEncoding Encode(IReadOnlyList<CMU3DModelPart> parts)
    {
        var encoding = new CMU3DSceneEncoding();
        encoding.Build(parts.Select(part => new CMU3DSceneBox((part.Min + part.Max) / 2,
            (part.Max - part.Min) / 2, part.YawRadians, Color.White, Shape: part.Shape)).ToArray());
        return encoding;
    }
}
