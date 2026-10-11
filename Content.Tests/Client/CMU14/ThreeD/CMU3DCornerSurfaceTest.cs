using System.Linq;
using System.Numerics;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;
using Robust.Shared.Prototypes;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DCornerSurfaceTest
{
    [TestCase(0, 0, 0, 0, 0)]
    [TestCase(1, 0, 1, 4, 0)]
    [TestCase(2, 4, 0, 0, 1)]
    [TestCase(4, 1, 4, 0, 0)]
    [TestCase(8, 0, 0, 1, 4)]
    [TestCase(16, 0, 2, 0, 0)]
    [TestCase(32, 2, 0, 0, 0)]
    [TestCase(64, 0, 0, 0, 2)]
    [TestCase(128, 0, 0, 2, 0)]
    [TestCase(5, 1, 5, 4, 0)]
    [TestCase(21, 1, 7, 4, 0)]
    [TestCase(15, 5, 5, 5, 5)]
    [TestCase(255, 7, 7, 7, 7)]
    public void BordersAndDiagonalHolesUseSourceCornerFill(int mask, int se, int ne, int nw, int sw)
    {
        Assert.That(CMU3DSceneLayout.CornerStates(mask), Is.EqualTo(new[] { se, ne, nw, sw }));
    }

    [Test]
    public void SourceDirectionSlotsStayInTheirWorldQuadrants()
    {
        var model = new CMU3DModelPrototype
        {
            CornerSurfaces = Enumerable.Range(0, 32).Select(i => new ProtoId<CMU3DSurfacePrototype>($"Surface{i}")).ToArray(),
            Parts =
            [
                new() { Min = new Vector3(0, -.5f, 0), Max = new Vector3(.5f, 0, .012f), Label = "SE" },
                new() { Min = Vector3.Zero, Max = new Vector3(.5f, .5f, .012f), Label = "NE" },
                new() { Min = new Vector3(-.5f, 0, 0), Max = new Vector3(0, .5f, .012f), Label = "NW" },
                new() { Min = new Vector3(-.5f, -.5f, 0), Max = new Vector3(0, 0, .012f), Label = "SW" },
            ],
        };
        // N + E without the NE diagonal creates an inward corner, not a filled center.
        var parts = CMU3DSceneLayout.CornerParts(model, 5);
        Assert.That(parts.Select(p => p.Surface!.Value.Id), Is.EqualTo(new[] { "Surface4", "Surface22", "Surface17", "Surface3" }));
        for (var i = 0; i < 4; i++)
        {
            Assert.That(parts[i].Min, Is.EqualTo(model.Parts[i].Min));
            Assert.That(parts[i].Max, Is.EqualTo(model.Parts[i].Max));
            Assert.That(parts[i].SurfaceAxis, Is.EqualTo(CMU3DSurfaceAxis.XY));
        }
        Assert.That(model.Parts.All(p => p.Surface == null), Is.True, "Shared prototype parts must not be mutated.");
        Assert.That(CMU3DSceneLayout.CornerParts(model, 21)[1].Surface!.Value.Id, Is.EqualTo("Surface30"));
        Assert.That(CMU3DSceneLayout.CornerParts(new CMU3DModelPrototype(), 255), Is.Empty);
    }

    [Test]
    public void UnequalSourcePatchesRetainAllStructuralParts()
    {
        var body = new CMU3DModelPart { Min = new Vector3(-.5f,-.5f,0), Max = new Vector3(.5f,.5f,2.78f), Label = "body" };
        var model = new CMU3DModelPrototype
        {
            CornerSurfaces = Enumerable.Range(0,32).Select(i => new ProtoId<CMU3DSurfacePrototype>($"Surface{i}")).ToArray(),
            Parts = [
                new() { Min = new Vector3(0,-.5f,2.78f), Max = new Vector3(.5f,.3125f,2.8f) },
                new() { Min = new Vector3(0,.3125f,2.78f), Max = new Vector3(.5f,.5f,2.8f) },
                new() { Min = new Vector3(-.5f,.3125f,2.78f), Max = new Vector3(0,.5f,2.8f) },
                new() { Min = new Vector3(-.5f,-.5f,2.78f), Max = new Vector3(0,.3125f,2.8f) }, body],
        };
        for (var mask = 0; mask < 256; mask++)
        {
            var parts = CMU3DSceneLayout.CornerParts(model,mask);
            Assert.That(parts.Length, Is.EqualTo(5));
            Assert.That(parts[4], Is.SameAs(body));
            Assert.That(parts[0].Max.Y, Is.EqualTo(.3125f));
            Assert.That(parts[1].Min.Y, Is.EqualTo(.3125f));
            Assert.That(parts.Take(4).All(p => p.Max.Z == 2.8f), Is.True);
            Assert.That(model.Parts.Take(4).All(p => p.Surface == null), Is.True);
        }
    }
}
