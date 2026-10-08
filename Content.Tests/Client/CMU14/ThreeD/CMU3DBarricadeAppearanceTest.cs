using System.Numerics;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DBarricadeAppearanceTest
{
    [Test]
    public void AcidReadsOwnerFramesAndClearsWithoutResettingDamageOrWire()
    {
        var model = AcidModel();
        var original = model.Parts;
        var acid = new CMU3DBarricadeLayer("/Textures/acid.rsi", "acid", 0, true, Color.White, true);
        var wire = new CMU3DBarricadeLayer("/Textures/wire.rsi", "plasteel_wire", 0, true, Color.White, true);
        foreach (var suffix in new[] { "0", "4", "8", "12", "8", "0" })
        foreach (var wired in new[] { false, true })
        foreach (var frameIndex in new[] { 0, 1, 4, 0, 3, 2 })
        {
            Assert.That(CMU3DBarricadeAppearance.TryParts(model, Layer("DamageOverlay_" + suffix),
                Layer("AdditionalDamageOverlay_" + suffix, true), false, out var parts, out var key,
                wire with { Visible = wired }, acid with { Frame = frameIndex }), Is.True);
            var frames = wired ? model.BarricadeAcidStates[suffix].WiredFrames : model.BarricadeAcidStates[suffix].Frames;
            Assert.That(parts, Is.SameAs(frames[frameIndex].Parts));
            Assert.That(key, Is.EqualTo("barricade-damage:" + suffix + (wired ? ":wire" : "") + ":acid:" + frameIndex));
            // Expiry/water clears the original layer. Its stale frame is irrelevant after hiding.
            Assert.That(CMU3DBarricadeAppearance.TryParts(model, Layer("DamageOverlay_" + suffix),
                Layer("AdditionalDamageOverlay_" + suffix, true), false, out parts, out key,
                wire with { Visible = wired }, acid with { Frame = 99, Visible = false }), Is.True);
            Assert.That(parts, Is.SameAs((wired ? model.BarricadeWiredStates : model.BarricadeDamageStates)[suffix].Parts));
            Assert.That(key, Does.Not.Contain(":acid:"));
            Assert.That(model.Parts, Is.SameAs(original));
        }
    }

    [TestCase("rsi")]
    [TestCase("state")]
    [TestCase("frame-negative")]
    [TestCase("frame-overflow")]
    [TestCase("tint")]
    [TestCase("transform")]
    [TestCase("missing-context")]
    [TestCase("missing-frame")]
    [TestCase("missing-delay")]
    [TestCase("empty")]
    [TestCase("invalid")]
    [TestCase("budget")]
    [TestCase("fire")]
    public void UnsupportedAcidNeverFallsBackToCleanGeometry(string defect)
    {
        var model = AcidModel();
        var acid = new CMU3DBarricadeLayer("/Textures/acid.rsi", "acid", 3, true, Color.White, true);
        var frames = model.BarricadeAcidStates["8"].Frames;
        switch (defect)
        {
            case "rsi": acid = acid with { Rsi = "/Textures/other.rsi" }; break;
            case "state": acid = acid with { State = "fire" }; break;
            case "frame-negative": acid = acid with { Frame = -1 }; break;
            case "frame-overflow": acid = acid with { Frame = 5 }; break;
            case "tint": acid = acid with { Color = Color.Red }; break;
            case "transform": acid = acid with { IdentityTransform = false }; break;
            case "missing-context": model.BarricadeAcidStates.Remove("8"); break;
            case "missing-frame": frames.RemoveAt(4); break;
            case "missing-delay": model.BarricadeAcidDelays.Clear(); break;
            case "empty": frames[3].Parts.Clear(); break;
            case "invalid": frames[3].Parts[^1].Min = new Vector3(float.NaN); break;
            case "budget":
                for (var i = 0; i < CMU3DBarricadeAppearance.AcidPartLimit; i++)
                    frames[3].Parts.Add(model.Parts[0]);
                break;
        }
        Assert.That(CMU3DBarricadeAppearance.TryParts(model, Layer("DamageOverlay_8"),
            Layer("AdditionalDamageOverlay_8", true), defect == "fire", out var parts, out var key, acid: acid), Is.False);
        Assert.That(parts, Is.Empty);
        Assert.That(key, Is.Empty);
    }

    [TestCase(0, 1, 2, 3, true)]
    [TestCase(0, 1, null, 3, true)]
    [TestCase(0, 1, 2, null, true)]
    [TestCase(0, 1, null, null, true)]
    [TestCase(0, 1, 3, 2, false)]
    [TestCase(0, 1, 1, 2, false)]
    [TestCase(0, 1, 2, 2, false)]
    [TestCase(0, 1, 0, null, false)]
    [TestCase(1, 0, 2, 3, false)]
    [TestCase(-1, 0, 1, 2, false)]
    public void OnlyTheSourceLayerOrderIsAccepted(int body, int reinforcement, int? acid, int? wire, bool valid)
    {
        Assert.That(CMU3DBarricadeAppearance.ValidLayerOrder(body, reinforcement, acid, wire), Is.EqualTo(valid));
    }

    private static CMU3DModelPrototype AcidModel()
    {
        var model = Model();
        model.BarricadeWireRsi = "wire.rsi";
        model.BarricadeWireState = "plasteel_wire";
        model.BarricadeAcidRsi = "acid.rsi";
        model.BarricadeAcidState = "acid";
        model.BarricadeAcidDelays = [.1f, .1f, .1f, .1f, .1f];
        foreach (var (key, body) in model.BarricadeDamageStates)
        {
            model.BarricadeWiredStates[key] = new CMU3DModelFrame { Parts = [..body.Parts] };
            var effect = new CMU3DBarricadeAcidState();
            for (var index = 0; index < 5; index++)
            {
                effect.Frames.Add(new CMU3DModelFrame { Parts = [..body.Parts,
                    new CMU3DModelPart { Min = new Vector3(-.1f, -.49f, .1f), Max = new Vector3(.1f, -.48f, .2f + index * .01f) }] });
                effect.WiredFrames.Add(new CMU3DModelFrame { Parts = [..effect.Frames[index].Parts] });
            }
            model.BarricadeAcidStates[key] = effect;
        }
        return model;
    }

    [Test]
    public void WireInstallDamageRepairAndCutFollowTheOriginalLayerWithoutChangingPoses()
    {
        var model = Model();
        var original = model.Parts;
        model.BarricadeWireRsi = "wire.rsi";
        model.BarricadeWireState = "plasteel_wire";
        foreach (var (suffix, damage) in model.BarricadeDamageStates)
            model.BarricadeWiredStates[suffix] = new CMU3DModelFrame
            {
                Parts = [..damage.Parts, new CMU3DModelPart { Min = new Vector3(-.4f, -.4f, .62f), Max = new Vector3(.4f, -.35f, .66f) }],
            };
        var wire = new CMU3DBarricadeLayer("/Textures/wire.rsi", "plasteel_wire", 0, true, Color.White, true);
        foreach (var suffix in new[] { "0", "4", "8", "12", "8", "4", "0" })
        foreach (var visible in new[] { false, true, false })
        {
            Assert.That(CMU3DBarricadeAppearance.TryParts(model, Layer("DamageOverlay_" + suffix),
                Layer("AdditionalDamageOverlay_" + suffix, true), false, out var parts, out var key,
                wire with { Visible = visible }), Is.True);
            Assert.That(parts, Is.SameAs((visible ? model.BarricadeWiredStates : model.BarricadeDamageStates)[suffix].Parts));
            Assert.That(parts.Count, Is.EqualTo(visible ? 2 : 1));
            Assert.That(key, Is.EqualTo("barricade-damage:" + suffix + (visible ? ":wire" : "")));
            Assert.That(model.Parts, Is.SameAs(original));
            Assert.That(original.Count, Is.EqualTo(1));
        }
    }

    [TestCase("resource")]
    [TestCase("state")]
    [TestCase("frame")]
    [TestCase("tint")]
    [TestCase("transform")]
    [TestCase("metadata")]
    [TestCase("missing")]
    [TestCase("empty")]
    [TestCase("invalid")]
    [TestCase("budget")]
    [TestCase("acid")]
    public void UnsupportedWireNeverFallsBackToDryGeometry(string defect)
    {
        var model = Model();
        model.BarricadeWireRsi = "wire.rsi";
        model.BarricadeWireState = "plasteel_wire";
        model.BarricadeWiredStates["8"] = new CMU3DModelFrame { Parts = [..model.BarricadeDamageStates["8"].Parts] };
        var wire = new CMU3DBarricadeLayer("/Textures/wire.rsi", "plasteel_wire", 0, true, Color.White, true);
        switch (defect)
        {
            case "resource": wire = wire with { Rsi = "/Textures/wrong.rsi" }; break;
            case "state": wire = wire with { State = "wire_open" }; break;
            case "frame": wire = wire with { Frame = 1 }; break;
            case "tint": wire = wire with { Color = Color.Red }; break;
            case "transform": wire = wire with { IdentityTransform = false }; break;
            case "metadata": model.BarricadeWireState = null; break;
            case "missing": model.BarricadeWiredStates.Clear(); break;
            case "empty": model.BarricadeWiredStates["8"].Parts.Clear(); break;
            case "invalid": model.BarricadeWiredStates["8"].Parts[0].Min = new Vector3(float.NaN); break;
            case "budget":
                for (var i = 0; i < 128; i++)
                    model.BarricadeWiredStates["8"].Parts.Add(model.Parts[0]);
                break;
        }
        Assert.That(CMU3DBarricadeAppearance.TryParts(model, Layer("DamageOverlay_8"),
            Layer("AdditionalDamageOverlay_8", true), defect == "acid", out var parts, out var key, wire), Is.False);
        Assert.That(parts, Is.Empty);
        Assert.That(key, Is.Empty);
    }

    [Test]
    public void DamageAndRepairUseTheOwnersPairedStateWithoutChangingDefaultGeometry()
    {
        var model = Model();
        var original = model.Parts;
        foreach (var suffix in new[] { "0", "4", "8", "12", "8", "4", "0" })
        {
            Assert.That(CMU3DBarricadeAppearance.TryParts(model, Layer("DamageOverlay_" + suffix),
                Layer("AdditionalDamageOverlay_" + suffix, true), false, out var parts, out var key), Is.True);
            Assert.That(parts, Is.SameAs(model.BarricadeDamageStates[suffix].Parts));
            Assert.That(key, Is.EqualTo("barricade-damage:" + suffix));
            Assert.That(model.Parts, Is.SameAs(original));
        }
    }

    [TestCase("unpaired")]
    [TestCase("unknown")]
    [TestCase("body-resource")]
    [TestCase("reinforcement-resource")]
    [TestCase("body-hidden")]
    [TestCase("reinforcement-hidden")]
    [TestCase("body-color")]
    [TestCase("reinforcement-color")]
    [TestCase("body-transform")]
    [TestCase("reinforcement-transform")]
    [TestCase("frame")]
    [TestCase("overlay")]
    [TestCase("missing-pose")]
    [TestCase("invalid-geometry")]
    [TestCase("empty-pose")]
    public void UnsupportedCompositionDoesNotPretendToBeAnIntactBarricade(string defect)
    {
        var model = Model();
        var body = Layer("DamageOverlay_8");
        var reinforcement = Layer("AdditionalDamageOverlay_8", true);
        switch (defect)
        {
            case "unpaired": reinforcement = reinforcement with { State = "AdditionalDamageOverlay_4" }; break;
            case "unknown": body = body with { State = "DamageOverlay_16" }; break;
            case "body-resource": body = body with { Rsi = "/Textures/metal.rsi" }; break;
            case "reinforcement-resource": reinforcement = reinforcement with { Rsi = null }; break;
            case "body-hidden": body = body with { Visible = false }; break;
            case "reinforcement-hidden": reinforcement = reinforcement with { Visible = false }; break;
            case "body-color": body = body with { Color = Color.Red }; break;
            case "reinforcement-color": reinforcement = reinforcement with { Color = Color.Red }; break;
            case "body-transform": body = body with { IdentityTransform = false }; break;
            case "reinforcement-transform": reinforcement = reinforcement with { IdentityTransform = false }; break;
            case "frame": body = body with { Frame = 1 }; break;
            case "missing-pose": model.BarricadeDamageStates.Remove("8"); break;
            case "invalid-geometry": model.BarricadeDamageStates["8"].Parts[0].Min = new Vector3(float.NaN); break;
            case "empty-pose": model.BarricadeDamageStates["8"].Parts.Clear(); break;
        }
        Assert.That(CMU3DBarricadeAppearance.TryParts(model, body, reinforcement, defect == "overlay", out var parts, out var key), Is.False);
        Assert.That(parts, Is.Empty);
        Assert.That(key, Is.Empty);
    }

    private static CMU3DBarricadeLayer Layer(string state, bool reinforcement = false) => new(
        reinforcement ? "/Textures/reinforcement.rsi" : "/Textures/body.rsi", state, 0, true, Color.White, true);

    private static CMU3DModelPrototype Model()
    {
        var model = new CMU3DModelPrototype
        {
            ReferenceRsi = "body.rsi", ReferenceState = "DamageOverlay_0", SourceDirections = 4,
            BarricadeReinforcementRsi = "reinforcement.rsi",
        };
        foreach (var suffix in new[] { "0", "4", "8", "12" })
            model.BarricadeDamageStates[suffix] = new CMU3DModelFrame
            {
                Parts = [new CMU3DModelPart { Min = new Vector3(-.5f, -.45f, 0), Max = new Vector3(.5f, -.3f, .625f) }],
            };
        model.Parts = model.BarricadeDamageStates["0"].Parts;
        return model;
    }
}
