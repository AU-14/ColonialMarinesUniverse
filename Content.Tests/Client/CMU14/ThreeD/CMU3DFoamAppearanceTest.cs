using System;
using System.Linq;
using System.Numerics;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DFoamAppearanceTest
{
    [Test]
    public void ActualSourceLayerVisibilitySelectsAllSixteenCompositionsWithoutMutatingSource()
    {
        var model = Model();
        for (var mask = 0; mask < 16; mask++)
        {
            var layers = Layers(mask);
            Assert.That(CMU3DFoamAppearance.TryParts(model, layers, out var parts, out var key), Is.True);
            Assert.That(parts.Select(p => p.Label), Is.EqualTo(new[] { "body" }.Concat(
                CMU3DFoamAppearance.Edges.Where((_, i) => (mask & (1 << i)) != 0))));
            Assert.That(key, Is.EqualTo($"foam:edges-{mask}"));
            Assert.That(parts[0], Is.SameAs(model.FoamAppearance!.BaseParts[0]));
            Assert.That(parts.All(p => p.Color == CMU3DFoamAppearance.SourceTint), Is.True);
        }
        Assert.That(model.Parts.Count, Is.EqualTo(5));
        Assert.That(model.FoamAppearance!.BaseParts.Count, Is.EqualTo(1));
        Assert.That(CMU3DSceneLayout.PresentationTint(model, CMU3DFoamAppearance.SourceTint), Is.EqualTo(Color.White));
        foreach (var yaw in new[] { 0f, -MathF.PI / 2, .3f })
            Assert.That(CMU3DSceneLayout.RenderYaw(model, yaw, false, false), Is.EqualTo(yaw).Within(.00001));
    }

    [TestCase("base-hidden")] [TestCase("state")] [TestCase("rsi")] [TestCase("frame")]
    [TestCase("frame-count")] [TestCase("offset")] [TestCase("transform")] [TestCase("tint")]
    [TestCase("extra-layer")] [TestCase("missing-edge")] [TestCase("budget")] [TestCase("geometry")]
    [TestCase("baked-tint")] [TestCase("rotation")] [TestCase("mapping")]
    public void UnsupportedSourceAppearanceFailsAtomically(string defect)
    {
        var model = Model();
        var layers = Layers(15);
        switch (defect)
        {
            case "base-hidden": layers[0] = layers[0] with { Visible = false }; break;
            case "state": layers[1] = layers[1] with { State = "iron_foam-south" }; break;
            case "rsi": layers[1] = layers[1] with { Rsi = "/Textures/other.rsi" }; break;
            case "frame": layers[1] = layers[1] with { Frame = 1 }; break;
            case "frame-count": layers[1] = layers[1] with { FrameCount = 2 }; break;
            case "offset": layers[1] = layers[1] with { Offset = Vector2.Zero }; break;
            case "transform": layers[1] = layers[1] with { ValidTransform = false }; break;
            case "tint": layers[1] = layers[1] with { Color = CMU3DFoamAppearance.SourceTint }; break;
            case "extra-layer": layers = layers.Append(layers[0]).ToArray(); break;
            case "missing-edge": model.FoamAppearance!.EdgeParts.Remove("north"); break;
            case "budget": model.FoamAppearance!.BaseParts.AddRange(Enumerable.Repeat(Part("extra"), 128)); break;
            case "geometry": model.FoamAppearance!.BaseParts[0].Min = new Vector3(float.NaN); break;
            case "baked-tint": model.BakedSpriteTint = Color.White; break;
            case "rotation": model.UseEntityRotation = false; break;
            case "mapping": model.SourcePrototypes = ["RMCFoamedIronMetal"]; break;
        }
        Assert.That(CMU3DFoamAppearance.TryParts(model, layers, out var parts, out var key), Is.False);
        Assert.That(parts, Is.Empty);
        Assert.That(key, Is.Empty);
    }

    private static CMU3DFoamLayer[] Layers(int mask) => Enumerable.Range(0, 5).Select(i => new CMU3DFoamLayer(
        "/Textures/" + CMU3DFoamAppearance.Rsi,
        i == 0 ? "metal_foam" : "metal_foam-" + CMU3DFoamAppearance.Edges[i - 1], 0, 1,
        i == 0 || (mask & (1 << (i - 1))) != 0, Color.White,
        i == 0 ? Vector2.Zero : CMU3DFoamAppearance.Offsets[i - 1], true)).ToArray();

    private static CMU3DModelPart Part(string label) => new()
    {
        Label = label, Min = Vector3.Zero, Max = new Vector3(.1f), Color = CMU3DFoamAppearance.SourceTint,
    };

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "RA0039", Justification = "CMU14: This test supplies isolated mutable fixture data; prototype registration and inheritance are not exercised.")]
    private static CMU3DModelPrototype Model()
    {
        var definition = new CMU3DFoamAppearanceDefinition { BaseParts = [Part("body")] };
        foreach (var edge in CMU3DFoamAppearance.Edges)
            definition.EdgeParts[edge] = new CMU3DModelFrame { Parts = [Part(edge)] };
        return new CMU3DModelPrototype
        {
            ReferencePrototype = CMU3DFoamAppearance.Prototype, SourcePrototypes = [CMU3DFoamAppearance.Prototype],
            ReferenceRsi = CMU3DFoamAppearance.Rsi, ReferenceState = "metal_foam", UseEntityRotation = true,
            ReferenceTint = CMU3DFoamAppearance.SourceTint, BakedSpriteTint = CMU3DFoamAppearance.SourceTint,
            FoamAppearance = definition,
            Parts = definition.BaseParts.Concat(CMU3DFoamAppearance.Edges.SelectMany(e => definition.EdgeParts[e].Parts)).ToList(),
        };
    }
}
