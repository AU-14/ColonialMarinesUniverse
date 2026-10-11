using System.Collections.Generic;
using System.Numerics;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DDoorButtonAppearanceTest
{
    private const string Rsi = "/Textures/_RMC14/Objects/door_button.rsi";

    [Test]
    public void LiveFrameAndPowerSelectGeometryWithoutMutatingDefaultParts()
    {
        var model = Model();
        var defaults = model.Parts;
        foreach (var frame in new[] { 0, 1, 2, 0, 2 })
        foreach (var powerVisible in new[] { false, true, false })
        {
            Assert.That(CMU3DDoorButtonAppearance.TryParts(model, Layer("doorctrl1", frame),
                Layer("doorctrl-p", visible: powerVisible), false, out var parts, out var key), Is.True);
            var expected = (powerVisible ? model.DoorButtonStates["doorctrl1"].UnpoweredFrames :
                model.DoorButtonStates["doorctrl1"].Frames)[frame].Parts;
            Assert.That(parts, Is.SameAs(expected));
            Assert.That(key, Is.EqualTo($"doorctrl1:{frame}:{(powerVisible ? "unpowered" : "powered")}"));
            Assert.That(model.Parts, Is.SameAs(defaults));
        }
        Assert.That(CMU3DDoorButtonAppearance.TryParts(model, Layer("doorctrl"),
            Layer("doorctrl-p", visible: false), false, out var idle, out _), Is.True);
        Assert.That(idle, Is.SameAs(model.Parts), "Completion returns to the original source idle pose.");
    }

    [TestCase("unknown-state")]
    [TestCase("negative-frame")]
    [TestCase("extra-frame")]
    [TestCase("wrong-rsi")]
    [TestCase("hidden-animation")]
    [TestCase("tinted-animation")]
    [TestCase("transformed-animation")]
    [TestCase("extra-layer")]
    [TestCase("wrong-power-rsi")]
    [TestCase("wrong-power-state")]
    [TestCase("wrong-power-frame")]
    [TestCase("tinted-power")]
    [TestCase("transformed-power")]
    [TestCase("missing-frame-geometry")]
    [TestCase("invalid-geometry")]
    [TestCase("mismatched-power-frames")]
    public void UnmodeledAppearanceNeverFallsBackToPoweredIdle(string defect)
    {
        var model = Model();
        var animation = Layer("doorctrl1");
        var power = Layer("doorctrl-p");
        switch (defect)
        {
            case "unknown-state": animation = animation with { State = "broken" }; break;
            case "negative-frame": animation = animation with { Frame = -1 }; break;
            case "extra-frame": animation = animation with { Frame = 3 }; break;
            case "wrong-rsi": animation = animation with { Rsi = "/Textures/other.rsi" }; break;
            case "hidden-animation": animation = animation with { Visible = false }; break;
            case "tinted-animation": animation = animation with { Color = Color.Red }; break;
            case "transformed-animation": animation = animation with { IdentityTransform = false }; break;
            case "wrong-power-rsi": power = power with { Rsi = null }; break;
            case "wrong-power-state": power = power with { State = "other-overlay" }; break;
            case "wrong-power-frame": power = power with { Frame = 1 }; break;
            case "tinted-power": power = power with { Color = Color.Green }; break;
            case "transformed-power": power = power with { IdentityTransform = false }; break;
            case "missing-frame-geometry": model.DoorButtonStates["doorctrl1"].UnpoweredFrames[0].Parts.Clear(); break;
            case "invalid-geometry": model.DoorButtonStates["doorctrl1"].UnpoweredFrames[0].Parts[0].Min = new Vector3(float.NaN); break;
            case "mismatched-power-frames": model.DoorButtonStates["doorctrl1"].UnpoweredFrames.RemoveAt(0); break;
        }
        Assert.That(CMU3DDoorButtonAppearance.TryParts(model, animation, power, defect == "extra-layer", out _, out var key), Is.False);
        Assert.That(key, Is.Empty);
    }

    [Test]
    public void InvisiblePowerOverlayDoesNotAffectAppearanceAndWallMountingUsesChosenFrame()
    {
        var model = Model();
        var hidden = new CMU3DButtonLayer(null, "unknown", -1, false, Color.Red, false);
        Assert.That(CMU3DDoorButtonAppearance.TryParts(model, Layer("doorctrl1", 2), hidden, false, out var parts, out _), Is.True);
        var mounted = CMU3DSceneLayout.InsideWallParts(parts);
        Assert.That(mounted[0].Min.Y, Is.EqualTo(-1 - parts[0].Max.Y));
        Assert.That(mounted[0].Max.Y, Is.EqualTo(-1 - parts[0].Min.Y));
        Assert.That(mounted[0].Color, Is.EqualTo(parts[0].Color));
        Assert.That(parts[0].Min.Y, Is.EqualTo(-.56f), "Mounting must not mutate cached source-frame geometry.");
    }

    private static CMU3DButtonLayer Layer(string state, int frame = 0, bool visible = true) =>
        new(Rsi, state, frame, visible, Color.White, true);

    private static CMU3DModelPrototype Model()
    {
        var model = new CMU3DModelPrototype { ReferenceRsi = "_RMC14/Objects/door_button.rsi", ReferenceState = "doorctrl" };
        foreach (var (name, count) in new[] { ("doorctrl", 1), ("doorctrl1", 3) })
        {
            var state = new CMU3DDoorButtonState();
            for (var index = 0; index < count; index++)
            {
                state.Frames.Add(Frame(new Color(1f, index / 3f, 0)));
                state.UnpoweredFrames.Add(Frame(new Color(.1f, index / 6f, .1f)));
            }
            model.DoorButtonStates.Add(name, state);
        }
        model.Parts = model.DoorButtonStates["doorctrl"].Frames[0].Parts;
        return model;
    }

    private static CMU3DModelFrame Frame(Color color) => new()
    {
        Parts = [new CMU3DModelPart { Min = new Vector3(-.1f, -.56f, 1.2f), Max = new Vector3(.1f, -.502f, 1.4f), Color = color }],
    };
}
