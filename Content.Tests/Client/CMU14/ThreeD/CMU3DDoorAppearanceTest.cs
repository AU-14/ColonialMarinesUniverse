using System.Numerics;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using Content.Shared.Doors.Components;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DDoorAppearanceTest
{
    private const string Rsi = "_RMC14/Structures/Doors/Shutters/Hybrisa/window_shutter.rsi";

    [Test]
    public void OwnerFrameSurvivesCollisionCompletionAndInterruption()
    {
        var model = Model();
        var poses = new[]
        {
            (DoorState.Closed, "closed", 0), (DoorState.Opening, "opening", 2),
            (DoorState.Open, "opening", 5), (DoorState.Open, "open", 0),
            (DoorState.Closing, "closing", 3), (DoorState.Closed, "closing", 5),
            (DoorState.Closing, "opening", 1), (DoorState.Closed, "closed", 0),
        };
        foreach (var (state, spriteState, frame) in poses)
        {
            Assert.That(CMU3DDoorAppearance.TryParts(model, state, Layer(spriteState, frame), false, out var parts, out var key), Is.True);
            Assert.That(parts, Is.SameAs(model.DoorSpriteStates[spriteState].Frames[frame].Parts));
            Assert.That(key, Is.EqualTo($"{spriteState}:{frame}"));
        }
    }

    [TestCase(DoorState.Closed)]
    [TestCase(DoorState.Open)]
    [TestCase(DoorState.Opening)]
    [TestCase(DoorState.Closing)]
    public void CatalogRetainsAnimatedFamilyThroughStableAndTransitionStates(DoorState state)
    {
        var model = Model();
        var catalog = new CMU3DSceneCatalog([model], _ => null);
        var original = catalog.Resolve("TestShutter");
        Assert.That(catalog.WithDoorState(original, state), Is.EqualTo(original));
        Assert.That(catalog.WithDoorState(original, DoorState.Emagging), Is.Null);
        Assert.That(catalog.WithDoorState(original, DoorState.Denying), Is.Null);
        model.DoorSpriteStates.Clear();
        Assert.That(catalog.WithDoorState(original, DoorState.Opening), Is.Null);
    }

    [TestCase("foreign-rsi")]
    [TestCase("unknown-state")]
    [TestCase("unknown-frame")]
    [TestCase("negative-frame")]
    [TestCase("hidden")]
    [TestCase("color")]
    [TestCase("transform")]
    [TestCase("overlay")]
    [TestCase("empty")]
    [TestCase("invalid-part")]
    [TestCase("denying")]
    public void UnsupportedAppearancesRemainVisibleAsUnsupported(string defect)
    {
        var model = Model();
        var layer = Layer("opening", 2);
        switch (defect)
        {
            case "foreign-rsi": layer = layer with { Rsi = "/Textures/other.rsi" }; break;
            case "unknown-state": layer = layer with { State = "welded" }; break;
            case "unknown-frame": layer = layer with { Frame = 6 }; break;
            case "negative-frame": layer = layer with { Frame = -1 }; break;
            case "hidden": layer = layer with { Visible = false }; break;
            case "color": layer = layer with { Color = Color.Red }; break;
            case "transform": layer = layer with { IdentityTransform = false }; break;
            case "empty": model.DoorSpriteStates["opening"].Frames[2].Parts.Clear(); break;
            case "invalid-part": model.DoorSpriteStates["opening"].Frames[2].Parts[0].Max = Vector3.Zero; break;
        }
        Assert.That(CMU3DDoorAppearance.TryParts(model, defect == "denying" ? DoorState.Denying : DoorState.Opening,
            layer, defect == "overlay", out var parts, out var key), Is.False);
        Assert.That(parts, Is.Empty);
        Assert.That(key, Is.Empty);
    }

    private static CMU3DButtonLayer Layer(string state, int frame) => new("/Textures/" + Rsi, state, frame, true, Color.White, true);

    private static CMU3DModelPrototype Model()
    {
        var model = new CMU3DModelPrototype { SourcePrototypes = ["TestShutter"], ReferenceRsi = Rsi, SourceDirections = 4 };
        foreach (var state in new[] { "closed", "open", "opening", "closing" })
        {
            var definition = new CMU3DDoorSpriteState();
            for (var i = 0; i < (state is "closed" or "open" ? 1 : 6); i++)
            {
                definition.Frames.Add(new CMU3DModelFrame { Parts = [new CMU3DModelPart
                {
                    Min = new Vector3(-.5f, -.0625f, .1f + i * .3f), Max = new Vector3(.5f, .0625f, 2.74f),
                }] });
                definition.Delays.Add(.1f);
            }
            model.DoorSpriteStates.Add(state, definition);
        }
        return model;
    }
}
