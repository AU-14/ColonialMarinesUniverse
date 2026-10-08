using System.Linq;
using System.Numerics;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DWindowConnectionTest
{
    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
    [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)]
    [TestCase(12)] [TestCase(13)] [TestCase(14)] [TestCase(15)]
    public void ShortenedFreeEndsExtendOnlyTowardJoinedEdges(int mask)
    {
        var source = new CMU3DModelPart { Min = new Vector3(-.44f,-.1f,0), Max = new Vector3(.44f,.1f,2) };
        var parts = CMU3DSceneLayout.ConnectedParts([source], mask, endInset: .06f);
        Assert.That(parts, Is.Not.Empty);
        Assert.That(parts.All(p => p.Valid && p.Min.X >= -.5f && p.Max.X <= .5f && p.Min.Y >= -.5f && p.Max.Y <= .5f), Is.True);
        if ((mask & 1) != 0) Assert.That(parts.Max(p => p.Max.Y), Is.EqualTo(.5f));
        if ((mask & 2) != 0) Assert.That(parts.Min(p => p.Min.Y), Is.EqualTo(-.5f));
        if ((mask & 4) != 0) Assert.That(parts.Max(p => p.Max.X), Is.EqualTo(.5f));
        if ((mask & 8) != 0) Assert.That(parts.Min(p => p.Min.X), Is.EqualTo(-.5f));
        if (mask is 0 or 4) Assert.That(parts.Min(p => p.Min.X), Is.EqualTo(-.44f));
        if (mask is 0 or 8) Assert.That(parts.Max(p => p.Max.X), Is.EqualTo(.44f));
        if (mask == 1) Assert.That(parts.Min(p => p.Min.Y), Is.EqualTo(-.44f));
        if (mask == 2) Assert.That(parts.Max(p => p.Max.Y), Is.EqualTo(.44f));
        Assert.That(source.Min.X, Is.EqualTo(-.44f), "Shared prototype geometry must remain unchanged.");
        Assert.That(source.Max.X, Is.EqualTo(.44f));
    }
}
