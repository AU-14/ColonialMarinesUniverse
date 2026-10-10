using System;
using System.Numerics;
using System.Linq;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DReagentTankAppearanceTest
{
    [TestCase("tn_color-1")]
    [TestCase("tn_color-2")]
    public void SourceFillTintAndAlphaCompositeOverPermanentVesselWithoutMutatingPrototype(string state)
    {
        var model = Model();
        var layers = Layers();
        layers[2] = layers[2] with { State = state, Visible = true, Color = new Color(.2f, .4f, .8f, .5f) };
        Assert.That(CMU3DReagentTankAppearance.TryParts(model, layers, 2, out var parts, out _), Is.True);
        Assert.That(parts.Count, Is.EqualTo(model.Parts.Count));
        Assert.That(parts[0], Is.SameAs(model.Parts[0]));
        Assert.That(parts[1].Color.R, Is.EqualTo(.6f).Within(.00001));
        Assert.That(parts[1].Color.G, Is.EqualTo(.7f).Within(.00001));
        Assert.That(parts[1].Color.B, Is.EqualTo(.9f).Within(.00001));
        Assert.That(parts[1].Color.A, Is.EqualTo(1));
        Assert.That(parts[1].Surface, Is.EqualTo(model.Parts[1].Surface));
        Assert.That(parts[1].SurfaceFlipU, Is.EqualTo(model.Parts[1].SurfaceFlipU));
        Assert.That(parts[1].Min, Is.EqualTo(model.Parts[1].Min));
        Assert.That(parts[1].Max, Is.EqualTo(model.Parts[1].Max));
        Assert.That(model.Parts[1].Color, Is.EqualTo(Color.White));
        var overall = new Color(.3f, .6f, .8f, 1f);
        Assert.That(CMU3DSceneLayout.PresentationTint(model, overall), Is.EqualTo(overall));
        Assert.That(CMU3DReagentTankAppearance.ValidSpriteColor(overall), Is.True);
        Assert.That(CMU3DReagentTankAppearance.ValidSpriteColor(new Color(.3f, .6f, .8f, .25f)), Is.False);
        foreach (var yaw in new[] { 0f, MathF.PI / 2, MathF.PI, -MathF.PI / 2 })
            Assert.That(CMU3DSceneLayout.RenderYaw(model, yaw, true, true), Is.Zero);
    }

    [TestCase(false, 1f)]
    [TestCase(true, 0f)]
    public void HiddenOrTransparentFillKeepsWhiteVessel(bool visible, float alpha)
    {
        var model = Model();
        var layers = Layers();
        layers[2] = layers[2] with { Visible = visible, Color = new Color(1, 0, 0, alpha) };
        Assert.That(CMU3DReagentTankAppearance.TryParts(model, layers, 2, out var parts, out _), Is.True);
        Assert.That(parts, Is.SameAs(model.Parts));
    }

    [TestCase("active")]
    [TestCase("boom")]
    [TestCase("fill-state")]
    [TestCase("extra-layer")]
    [TestCase("missing-vessel")]
    [TestCase("fixed-tint")]
    [TestCase("layer-transform")]
    [TestCase("rsi")]
    [TestCase("frame")]
    [TestCase("fill-map")]
    [TestCase("nonfinite")]
    [TestCase("negative-alpha")]
    [TestCase("baked")]
    [TestCase("duplicate-label")]
    [TestCase("unknown-part")]
    public void UnknownLayerContractFallsBackInsteadOfInventingDefault(string defect)
    {
        var model = Model();
        var layers = Layers().ToList();
        var fill = 2;
        switch (defect)
        {
            case "active": layers[3] = layers[3] with { State = "t_active" }; break;
            case "boom": layers[3] = layers[3] with { State = "t_boom" }; break;
            case "fill-state": layers[2] = layers[2] with { State = "te_color-1" }; break;
            case "extra-layer": layers.Add(layers[0]); break;
            case "missing-vessel": layers[1] = layers[1] with { Visible = false }; break;
            case "fixed-tint": layers[1] = layers[1] with { Color = Color.Red }; break;
            case "layer-transform": layers[2] = layers[2] with { IdentityTransform = false }; break;
            case "rsi": layers[2] = layers[2] with { Rsi = "/Textures/other.rsi" }; break;
            case "frame": layers[2] = layers[2] with { Frame = 1 }; break;
            case "fill-map": fill = 1; break;
            case "nonfinite": layers[2] = layers[2] with { Color = new Color(float.NaN, 1, 1) }; break;
            case "negative-alpha": layers[2] = layers[2] with { Color = new Color(1f, 1f, 1f, -.1f) }; break;
            case "baked": model.BakedSpriteTint = Color.Red; break;
            case "duplicate-label": model.Parts.Add(model.Parts[1]); break;
            case "unknown-part": model.ReagentTankAppearance!.VesselParts = ["missing"]; break;
        }
        Assert.That(CMU3DReagentTankAppearance.TryParts(model, layers, fill, out var parts, out var key), Is.False);
        Assert.That(parts, Is.Empty);
        Assert.That(key, Is.Empty);
    }

    [TestCase("CMCatwalk")]
    [TestCase("CMCatwalkPrison")]
    [TestCase("RMCCatwalkHybrisaElevator")]
    public void KnownThinCladdingRaisesGroundedWheelsAtEveryCardinalWithoutGuessingElevatedSupports(string prototype)
    {
        var model = Model();
        var support = new CMU3DModelPrototype { SourcePrototypes = [prototype], Parts = [new CMU3DModelPart
        {
            Min = new Vector3(-.5f, -.5f, 0), Max = new Vector3(.5f, .5f, .035f),
        }] };
        foreach (var yaw in new[] { 0f, MathF.PI / 2, MathF.PI, -MathF.PI / 2 })
            Assert.That(CMU3DReagentTankPlacement.Offset(model, support, prototype, Vector2.Zero, yaw), Is.EqualTo(.037f).Within(.00001));
        Assert.That(CMU3DReagentTankPlacement.Offset(model, support, "Unknown", Vector2.Zero, 0), Is.Zero);
        Assert.That(CMU3DReagentTankPlacement.Offset(model, support, prototype, new Vector2(.1f, 0), 0), Is.Zero);
        Assert.That(CMU3DReagentTankPlacement.Offset(model, support, prototype, Vector2.Zero, .4f), Is.Zero);
        support.Parts[0].Max = new Vector3(.5f, .5f, .7f);
        Assert.That(CMU3DReagentTankPlacement.Offset(model, support, prototype, Vector2.Zero, 0), Is.Zero);
    }

    private static CMU3DButtonLayer[] Layers() => new[] { "tank_normal", "tn_color-1", "tn_color-1", "t_inactive" }
        .Select((state, i) => new CMU3DButtonLayer("/Textures/" + CMU3DReagentTankAppearance.Rsi, state, 0,
            i != 2, Color.White, true)).ToArray();

    private static CMU3DModelPrototype Model() => new()
    {
        ReferenceRsi = CMU3DReagentTankAppearance.Rsi, ReferenceState = "tank_normal", SourceDirections = 1,
        ReagentTankAppearance = new CMU3DReagentTankAppearanceDefinition { VesselParts = ["vessel"] },
        Parts = [new CMU3DModelPart { Label = "body", Min = new Vector3(-.4f, -.3f, 0), Max = new Vector3(.4f, .3f, .2f), Color = Color.Gray },
            new CMU3DModelPart { Label = "vessel", Min = new Vector3(-.3f, -.2f, .2f), Max = new Vector3(.3f, .2f, .8f), Surface = "Tank", SurfaceFlipU = true }],
    };
}
