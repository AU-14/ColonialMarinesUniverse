using System.Linq;
using System.Numerics;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DConnectedPanelCapTest
{
    [TestCase(0, 2)]
    [TestCase(1, 1)]
    [TestCase(2, 1)]
    [TestCase(3, 0)]
    [TestCase(4, 1)]
    [TestCase(5, 0)]
    [TestCase(6, 0)]
    [TestCase(7, 0)]
    [TestCase(8, 1)]
    [TestCase(9, 0)]
    [TestCase(10, 0)]
    [TestCase(11, 0)]
    [TestCase(12, 0)]
    [TestCase(13, 0)]
    [TestCase(14, 0)]
    [TestCase(15, 0)]
    public void JoinedCapsRotateWithThePanelAndPreserveItsOpenPane(int mask, int caps)
    {
        CMU3DModelPart[] source =
        [
            new() { Label = "pane", Min = new Vector3(-.5f,-.06f,.5f), Max = new Vector3(.5f,.06f,2) },
            new() { Label = "west cap", Min = new Vector3(-.5f,-.12f,0), Max = new Vector3(-.4f,.12f,2.1f), OmitWhenConnected = 8 },
            new() { Label = "east cap", Min = new Vector3(.4f,-.12f,0), Max = new Vector3(.5f,.12f,2.1f), OmitWhenConnected = 4 },
        ];
        var parts = CMU3DSceneLayout.ConnectedParts(source, mask);
        var ends = parts.Where(p => p.Label.EndsWith("cap")).ToArray();
        Assert.That(ends, Has.Length.EqualTo(caps));
        Assert.That(parts.Any(p => p.Label == "pane"), Is.True);
        if (mask is 1 or 4)
            Assert.That(ends[0].Label, Is.EqualTo("west cap"));
        if (mask is 2 or 8)
            Assert.That(ends[0].Label, Is.EqualTo("east cap"));
        if (mask == 1)
            Assert.That(ends[0].Max.Y, Is.LessThan(0));
        if (mask == 2)
            Assert.That(ends[0].Min.Y, Is.GreaterThan(0));
        Assert.That(source[1].Min, Is.EqualTo(new Vector3(-.5f,-.12f,0)));
        Assert.That(source[1].OmitWhenConnected, Is.EqualTo(8));
    }
}
