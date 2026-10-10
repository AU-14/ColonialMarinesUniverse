using System;
using System.Numerics;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DSpriteAppearanceTest
{
    private const string Rsi = "_RMC14/Structures/hybrisa_machine_props.rsi";
    private static readonly Vector2 Offset = new(.25f, .5f);

    [Test]
    public void RotatingSourceRequiresOptInAndRetainsSourceAnimationAndYaw()
    {
        var model = Model();
        Assert.That(CMU3DSpriteAppearance.SupportsRotation(model, false, false, false), Is.False);
        Assert.That(CMU3DSpriteAppearance.SupportsRotation(model, true, false, false), Is.True);
        model.SourceSpriteRotates = true;
        Assert.That(CMU3DSpriteAppearance.SupportsRotation(model, false, false, false), Is.True);
        Assert.That(CMU3DSpriteAppearance.SupportsRotation(model, true, false, false), Is.False);
        Assert.That(CMU3DSpriteAppearance.SupportsRotation(model, false, true, false), Is.False);
        Assert.That(CMU3DSpriteAppearance.SupportsRotation(model, false, false, true), Is.False);
        foreach (var yaw in new[] { 0f, MathF.PI / 2, MathF.PI, 3 * MathF.PI / 2 })
        {
            Assert.That(CMU3DSceneLayout.RenderYaw(model, yaw, false, false), Is.EqualTo(yaw));
            foreach (var frame in new[] { 4, 0, 3, 3, 1 })
            {
                Assert.That(CMU3DSpriteAppearance.TryParts(model, Layer(frame), Offset, Vector2.One,
                    0, 4, 5, false, out var parts, out var key), Is.True);
                Assert.That(parts, Is.SameAs(model.SpriteStates[model.ReferenceState!].Frames[frame].Parts));
                Assert.That(key, Is.EqualTo($"buildingventbig12:{frame}"));
            }
        }
        model.SpriteStates.Clear();
        Assert.That(CMU3DSpriteAppearance.SupportsRotation(model, false, false, false), Is.False);
    }

    [TestCase("")]
    [TestCase("/Textures/")]
    public void OwnerFrameCanLoopPauseOrJumpWithoutASecondClock(string resourcePrefix)
    {
        var model = Model();
        model.ReferenceRsi = resourcePrefix + Rsi;
        foreach (var frame in new[] { 0, 1, 4, 0, 0, 3, 2 })
        {
            Assert.That(CMU3DSpriteAppearance.TryParts(model, Layer(frame), Offset, Vector2.One, 0, 4, 5, false,
                out var parts, out var key), Is.True);
            Assert.That(parts, Is.SameAs(model.SpriteStates["buildingventbig12"].Frames[frame].Parts));
            Assert.That(parts[0].Surface?.Id, Is.EqualTo($"Frame{frame}"));
            Assert.That(key, Is.EqualTo($"buildingventbig12:{frame}"));
        }
    }

    [Test]
    public void SourceFadeAndTintArePreservedWithoutChangingFrameSelection()
    {
        var model = Model();
        var sourceColor = new Color(.8f, .6f, .4f, .4f);
        Assert.That(CMU3DSceneLayout.PresentationTint(model, sourceColor), Is.EqualTo(sourceColor));
        Assert.That(CMU3DSpriteAppearance.TryParts(model, Layer(2), Offset, Vector2.One, 0, 4, 5, false,
            out _, out _), Is.True);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "RA0039", Justification = "CMU14: This test supplies isolated mutable fixture data; prototype registration and inheritance are not exercised.")]
    [TestCase(3, 4, .25f)]
    [TestCase(5, 5, .25f)]
    [TestCase(6, 8, 4f)]
    [TestCase(7, 7, 4f)]
    public void SingleDirectionMachineryRetainsSourceOnOffFramesWithRotatedSavedTransforms(
        int design, int frameCount, float finalDelay)
    {
        var offset = new Vector2(.5f, .5f);
        var onName = $"buildingventbig{design}";
        var offName = onName + "_off";
        var on = new CMU3DSpriteState();
        for (var frame = 0; frame < frameCount; frame++)
        {
            on.Delays.Add(frame == frameCount - 1 ? finalDelay : .25f);
            on.Frames.Add(new CMU3DModelFrame { Parts = [new CMU3DModelPart
            {
                Min = new Vector3(-.3125f, -.249f, .04f), Max = new Vector3(1.3125f, .2f, 1.13f),
                Surface = $"Wide{design}On{frame}",
            }] });
        }
        var off = new CMU3DSpriteState
        {
            Delays = [1],
            Frames = [new CMU3DModelFrame { Parts = [new CMU3DModelPart
            {
                Min = new Vector3(-.3125f, -.249f, .04f), Max = new Vector3(1.3125f, .2f, 1.13f),
                Surface = $"Wide{design}Off",
            }] }],
        };
        var model = new CMU3DModelPrototype
        {
            ReferenceRsi = Rsi, ReferenceState = onName, SourceDirections = 1,
            SourceSpriteOffset = offset, ReferenceDirection = 0,
            SpriteStates = { [onName] = on, [offName] = off },
        };

        foreach (var savedYaw in new[] { 0f, MathF.PI / 2, MathF.PI })
        {
            Assert.That(CMU3DSceneLayout.RenderYaw(model, savedYaw, true, true), Is.Zero);
            for (var frame = 0; frame < frameCount; frame++)
            {
                var layer = new CMU3DButtonLayer("/Textures/" + Rsi, onName, frame, true, Color.White, true);
                Assert.That(CMU3DSpriteAppearance.TryParts(model, layer, offset, Vector2.One, 0, 1,
                    frameCount, false, out var parts, out var key), Is.True);
                Assert.That(parts, Is.SameAs(on.Frames[frame].Parts));
                Assert.That(key, Is.EqualTo($"{onName}:{frame}"));
            }

            var offLayer = new CMU3DButtonLayer("/Textures/" + Rsi, offName, 0, true, Color.White, true);
            Assert.That(CMU3DSpriteAppearance.TryParts(model, offLayer, offset, Vector2.One, 0, 1,
                1, false, out var offParts, out var offKey), Is.True);
            Assert.That(offParts, Is.SameAs(off.Frames[0].Parts));
            Assert.That(offKey, Is.EqualTo($"{offName}:0"));
            // The horizontal source offset is already embedded in authored geometry.
            Assert.That(CMU3DSpriteAppearance.TryParts(model, offLayer, Vector2.Zero, Vector2.One, 0, 1,
                1, false, out var fallback, out _), Is.False);
            Assert.That(fallback, Is.Empty);
            Assert.That(CMU3DSpriteAppearance.TryParts(model, offLayer, offset, Vector2.One, 0, 1,
                1, true, out fallback, out _), Is.False);
            Assert.That(fallback, Is.Empty);
        }
    }

    [TestCase("state")]
    [TestCase("rsi")]
    [TestCase("frame")]
    [TestCase("negative-frame")]
    [TestCase("directions")]
    [TestCase("missing-direction-models")]
    [TestCase("merged-timeline")]
    [TestCase("hidden")]
    [TestCase("layer-tint")]
    [TestCase("baked-tint")]
    [TestCase("baked-alpha")]
    [TestCase("layer-transform")]
    [TestCase("offset")]
    [TestCase("scale")]
    [TestCase("rotation")]
    [TestCase("invalid-rotation")]
    [TestCase("overlay")]
    [TestCase("empty-parts")]
    [TestCase("invalid-parts")]
    [TestCase("mismatched-delays")]
    [TestCase("invalid-delay")]
    public void UnsupportedSourceAppearanceDoesNotSilentlyBecomeDefaultGeometry(string defect)
    {
        var model = Model();
        var layer = Layer(2);
        var offset = Offset;
        var scale = Vector2.One;
        var rotation = 0f;
        var directions = 4;
        var frameCount = 5;
        var state = model.SpriteStates["buildingventbig12"];
        switch (defect)
        {
            case "state": layer = layer with { State = "unmodeled-off" }; break;
            case "rsi": layer = layer with { Rsi = "/Textures/other.rsi" }; break;
            case "frame": layer = layer with { Frame = 5 }; break;
            case "negative-frame": layer = layer with { Frame = -1 }; break;
            case "directions": directions = 8; break;
            case "missing-direction-models": model.DirectionalModels = []; break;
            case "merged-timeline": frameCount = 7; break;
            case "hidden": layer = layer with { Visible = false }; break;
            case "layer-tint": layer = layer with { Color = Color.Red }; break;
            case "baked-tint": model.BakedSpriteTint = Color.Red; break;
            case "baked-alpha": model.BakedSpriteTint = new Color(1f, 1f, 1f, .5f); break;
            case "layer-transform": layer = layer with { IdentityTransform = false }; break;
            case "offset": offset = new Vector2(-.25f, .5f); break;
            case "scale": scale = new Vector2(2, 1); break;
            case "rotation": rotation = MathF.PI; break;
            case "invalid-rotation": rotation = float.NaN; break;
            case "empty-parts": state.Frames[2].Parts.Clear(); break;
            case "invalid-parts": state.Frames[2].Parts[0].Max = Vector3.Zero; break;
            case "mismatched-delays": state.Delays.RemoveAt(0); break;
            case "invalid-delay": state.Delays[1] = float.NaN; break;
        }
        Assert.That(CMU3DSpriteAppearance.TryParts(model, layer, offset, scale, rotation, directions, frameCount,
            defect == "overlay", out var parts, out var key), Is.False);
        Assert.That(parts, Is.Empty);
        Assert.That(key, Is.Empty);
    }

    private static CMU3DButtonLayer Layer(int frame) => new("/Textures/"+Rsi, "buildingventbig12", frame, true, Color.White, true);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "RA0039", Justification = "CMU14: This test supplies isolated mutable fixture data; prototype registration and inheritance are not exercised.")]
    private static CMU3DModelPrototype Model()
    {
        var state = new CMU3DSpriteState { Delays = [.4f, .25f, .4f, .25f, 2.5f] };
        for (var i = 0; i < 5; i++)
            state.Frames.Add(new CMU3DModelFrame { Parts = [new CMU3DModelPart
            {
                Min = new Vector3(-.4f, -.14f, 0), Max = new Vector3(.4f, .14f, 2), Surface = $"Frame{i}",
            }] });
        return new CMU3DModelPrototype
        {
            ReferenceRsi = Rsi, ReferenceState = "buildingventbig12", SourceDirections = 4, SourceSpriteOffset = Offset,
            ReferenceDirection = 0, DirectionalModels = ["South", "North", "East", "West"],
            SpriteStates = { ["buildingventbig12"] = state },
        };
    }
}
