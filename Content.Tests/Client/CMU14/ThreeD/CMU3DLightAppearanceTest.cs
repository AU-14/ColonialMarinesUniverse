using System;
using System.Numerics;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DLightAppearanceTest
{
    private const string Rsi = "_RMC14/Structures/Wallmounts/LightingOffset/light_bulb.rsi";

    [TestCase(0f)]
    [TestCase(.7f)]
    public void RoomSideClearanceFollowsWallNormalOnRotatedGrids(float gridYaw)
    {
        var model = Model();
        var wall = new CMU3DModelPart { Min = new Vector3(-.5f,-.55f,0), Max = new Vector3(.5f,.55f,2.8f) };
        for (var turn = 0; turn < 4; turn++)
        {
            var yaw = gridYaw + turn * System.MathF.PI / 2;
            var normal = new Vector2(System.MathF.Sin(yaw), -System.MathF.Cos(yaw));
            Assert.That(CMU3DSceneLayout.TryBackWallMountOffset(model.Parts, yaw, [wall], yaw, normal, out var offset, roomSide: true), Is.True);
            Assert.That(Vector2.Dot(offset, normal), Is.EqualTo(-.059f).Within(.00001f));
            Assert.That(Vector2.Dot(offset, new Vector2(-normal.Y, normal.X)), Is.EqualTo(0).Within(.00001f));
            Assert.That(CMU3DSceneLayout.TryBackWallMountOffset(model.Parts, yaw, [wall], yaw, -normal, out _, roomSide: true), Is.False);
        }
    }

    [Test]
    public void SourceBlinkRemovalAndBreakageSelectCurrentPoseAndPreserveWallMount()
    {
        var model = Model();
        var original = model.Parts;
        foreach (var state in new[] { "bulb1", "bulb0", "bulb1", "bulb-empty", "bulb-broken", "bulb-burned", "bulb1" })
        {
            Assert.That(CMU3DLightAppearance.TryParts(model, Layer(state), false, out var parts, out var key), Is.True);
            Assert.That(parts, Is.SameAs(model.PoweredLightStates[state].Parts));
            Assert.That(key, Is.EqualTo(state));
            var mounted = CMU3DSceneLayout.InsideWallParts(parts);
            Assert.That(mounted[0].Min.Y, Is.EqualTo(-1 - parts[0].Max.Y));
            Assert.That(mounted[0].Max.Y, Is.EqualTo(-1 - parts[0].Min.Y));
            Assert.That(model.Parts, Is.SameAs(original));
        }
    }

    [TestCase("missing-state")]
    [TestCase("blue-replacement")]
    [TestCase("wrong-rsi")]
    [TestCase("missing-rsi")]
    [TestCase("frame")]
    [TestCase("hidden")]
    [TestCase("tinted")]
    [TestCase("transformed")]
    [TestCase("extra-layer")]
    [TestCase("invalid-part")]
    [TestCase("empty-pose")]
    [TestCase("over-budget")]
    public void UnknownAppearanceDoesNotShowALitBulb(string defect)
    {
        var model = Model();
        var layer = Layer("bulb1");
        switch (defect)
        {
            case "missing-state": layer = layer with { State = null }; break;
            case "blue-replacement": layer = layer with { State = "bbulb1" }; break;
            case "wrong-rsi": layer = layer with { Rsi = "Textures/other.rsi" }; break;
            case "missing-rsi": layer = layer with { Rsi = null }; break;
            case "frame": layer = layer with { Frame = 1 }; break;
            case "hidden": layer = layer with { Visible = false }; break;
            case "tinted": layer = layer with { Color = Color.Red }; break;
            case "transformed": layer = layer with { IdentityTransform = false }; break;
            case "invalid-part": model.Parts[0].Min = new Vector3(float.NaN); break;
            case "empty-pose": model.Parts.Clear(); break;
            case "over-budget": while (model.Parts.Count <= 128) model.Parts.Add(model.Parts[0]); break;
        }
        Assert.That(CMU3DLightAppearance.TryParts(model, layer, defect == "extra-layer", out _, out var key), Is.False);
        Assert.That(key, Is.Empty);
    }

    private static CMU3DButtonLayer Layer(string state) => new("/Textures/" + Rsi, state, 0, true, Color.White, true);

    [TestCase("tube")]
    [TestCase("ptube")]
    [TestCase("bptube")]
    public void TubeStatesFollowTheirOwnSourceLayerAndRejectOtherFamilies(string prefix)
    {
        const string tubeRsi = "_RMC14/Structures/Wallmounts/LightingOffset/light_tube.rsi";
        var model = Model(prefix, tubeRsi);
        foreach (var suffix in new[] { "1", "0", "-broken", "-empty", "-burned", "1" })
        {
            var state = prefix + suffix;
            var layer = new CMU3DButtonLayer("/Textures/" + tubeRsi, state, 0, true, Color.White, true);
            Assert.That(CMU3DLightAppearance.TryParts(model, layer, false, out var parts, out var key), Is.True);
            Assert.That(parts, Is.SameAs(model.PoweredLightStates[state].Parts));
            Assert.That(key, Is.EqualTo(state));
            Assert.That(CMU3DLightAppearance.TryParts(model, layer with { State = "bulb1" }, false, out _, out _), Is.False);
            Assert.That(CMU3DLightAppearance.TryParts(model, layer with { Rsi = "/Textures/" + Rsi }, false, out _, out _), Is.False);
        }
    }

    private static CMU3DModelPrototype Model(string prefix = "bulb", string rsi = Rsi)
    {
        var model = new CMU3DModelPrototype { ReferenceRsi = rsi, SourceDirections = 4, WallMounted = true };
        foreach (var suffix in new[] { "1", "0", "-empty", "-broken", "-burned" })
            model.PoweredLightStates[prefix + suffix] = new CMU3DModelFrame
            {
                Parts = [new CMU3DModelPart { Min = new Vector3(-.05f, -.7f, 2.2f), Max = new Vector3(.05f, -.501f, 2.4f) }],
            };
        model.Parts = model.PoweredLightStates[prefix + "1"].Parts;
        return model;
    }
}
