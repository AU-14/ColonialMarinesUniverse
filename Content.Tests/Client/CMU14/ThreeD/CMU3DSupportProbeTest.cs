using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DSupportProbeTest
{
    [Test]
    public void SavedOverhangingHatchetUsesActualGripAndKeepsSourcePosition()
    {
        var model = Prop();
        var position = new Vector2(316.6801f, -87.19585f);
        var rack = new CMU3DSceneSurface(new EntityUid(549), new Vector2(316.5f, -87.5f), 0,
            new Vector3(-.4f, -.275f, .92f), new Vector3(.4f, .275f, .948f));
        Assert.That(CMU3DSupportProbe.TryPoint(model, out var point), Is.True);
        Assert.That(point, Is.EqualTo(new Vector2(.171875f, -.125f)));
        Assert.That(CMU3DScenePlacement.Offset(model, new EntityUid(3313), position, [rack]), Is.EqualTo(.95f).Within(.000001));
        Assert.That(position, Is.EqualTo(new Vector2(316.6801f, -87.19585f)));
        model.SupportProbePart = null;
        Assert.That(CMU3DScenePlacement.Offset(model, new EntityUid(3313), position, [rack]), Is.Zero);
    }

    [Test]
    public void ProbeRotatesWithModelAndExistingPivotSupportTakesPriority()
    {
        var model = Prop();
        var probeSupport = new CMU3DSceneSurface(new EntityUid(1), Vector2.Zero, 0,
            new Vector3(.1f, .15f, .7f), new Vector3(.15f, .19f, .8f));
        var pivotSupport = new CMU3DSceneSurface(new EntityUid(3), Vector2.Zero, 0,
            new Vector3(-.03f, -.03f, .5f), new Vector3(.03f, .03f, .6f));
        Assert.That(CMU3DScenePlacement.Offset(model, new EntityUid(2), Vector2.Zero, [probeSupport], yaw: MathF.PI / 2),
            Is.EqualTo(.802f).Within(.000001));
        Assert.That(CMU3DScenePlacement.Offset(model, new EntityUid(2), Vector2.Zero, [probeSupport]), Is.Zero);
        Assert.That(CMU3DScenePlacement.Offset(model, new EntityUid(2), Vector2.Zero, [probeSupport, pivotSupport], yaw: MathF.PI / 2),
            Is.EqualTo(.602f).Within(.000001));
        Assert.That(CMU3DScenePlacement.Offset(model, new EntityUid(1), Vector2.Zero, [probeSupport], yaw: MathF.PI / 2), Is.Zero);
        Assert.That(CMU3DScenePlacement.Offset(model, new EntityUid(2), Vector2.Zero, [], yaw: MathF.PI / 2), Is.Zero);
    }

    [TestCase("missing")]
    [TestCase("duplicate")]
    [TestCase("curve")]
    [TestCase("yaw")]
    [TestCase("pitch")]
    [TestCase("transparent")]
    [TestCase("textured")]
    [TestCase("elevated")]
    [TestCase("animated")]
    [TestCase("multi-state")]
    [TestCase("frame-mismatch")]
    [TestCase("nonrotating")]
    [TestCase("directional")]
    [TestCase("bad-delay")]
    [TestCase("connected")]
    [TestCase("nan")]
    public void InvalidProbeDoesNotInventASupportPoint(string defect)
    {
        var model = Prop();
        var part = model.Parts[0];
        switch (defect)
        {
            case "missing": model.SupportProbePart = "absent"; break;
            case "duplicate": model.Parts.Add(part); break;
            case "curve": part.Shape = CMU3DPartShape.Ellipsoid; break;
            case "yaw": part.Yaw = 90; break;
            case "pitch": part.Pitch = 5; break;
            case "transparent": part.Color = new Color(1f, 1f, 1f, .5f); break;
            case "textured": part.Surface = "Texture"; break;
            case "elevated": model.Parts.Add(new CMU3DModelPart { Label = "lower", Min = new Vector3(0, 0, -.1f), Max = new Vector3(.1f, .1f, 0) }); break;
            case "nonrotating": model.SourceSpriteRotates = false; break;
            case "directional": model.SourceDirections = 4; break;
            case "bad-delay": model.SpriteStates["icon"].Delays[0] = float.NaN; break;
            case "connected": model.ConnectToNeighbours = true; break;
            case "nan": part.Max.Z = float.NaN; break;
        }
        model.SpriteStates["icon"].Frames[0].Parts = new List<CMU3DModelPart>(model.Parts);
        if (defect == "animated")
        {
            model.SpriteStates["icon"].Frames.Add(model.SpriteStates["icon"].Frames[0]);
            model.SpriteStates["icon"].Delays.Add(1);
        }
        if (defect == "multi-state")
            model.SpriteStates["other"] = model.SpriteStates["icon"];
        if (defect == "frame-mismatch")
            model.SpriteStates["icon"].Frames[0].Parts[0] = new CMU3DModelPart { Label = part.Label, Min = part.Min, Max = new Vector3(part.Max.X, part.Max.Y, .2f) };
        Assert.That(CMU3DSupportProbe.TryPoint(model, out _), Is.False);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "RA0039", Justification = "CMU14: This test supplies isolated mutable fixture data; prototype registration and inheritance are not exercised.")]
    private static CMU3DModelPrototype Prop()
    {
        var parts = new List<CMU3DModelPart>
        {
            new() { Label = "grip", Min = new Vector3(.15625f, -.1875f, 0), Max = new Vector3(.1875f, -.0625f, .078f) },
        };
        return new CMU3DModelPrototype
        {
            Placement = "surface", SourceDirections = 1, SourceSpriteRotates = true, UseEntityRotation = true,
            ReferenceState = "icon", SupportProbePart = "grip", Parts = parts,
            SpriteStates = new Dictionary<string, CMU3DSpriteState>
            {
                ["icon"] = new() { Frames = [new() { Parts = new List<CMU3DModelPart>(parts) }], Delays = [1] },
            },
        };
    }
}
