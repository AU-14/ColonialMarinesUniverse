using System;
using System.Numerics;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DAnchorStateTest
{
    [TestCase("DisposalJunction", "j1")]
    [TestCase("DisposalJunctionFlipped", "j2")]
    [TestCase("DisposalXJunction", "x")]
    public void ExactSourceSelectsReciprocalAnchorPosesWithoutChangingCachedMapping(string source, string suffix)
    {
        var (installed, loose) = Pair(source, suffix);
        var catalog = new CMU3DSceneCatalog([installed, loose], _ => null);
        var original = catalog.Resolve(source);
        Assert.That(catalog.WithAnchorState(original, true)!.Value.Model, Is.SameAs(installed));
        var selected = catalog.WithAnchorState(original, false);
        Assert.That(selected!.Value.Model, Is.SameAs(loose));
        Assert.That(selected.Value.Reference, Is.EqualTo(source));
        Assert.That(catalog.WithAnchorState(selected, true), Is.EqualTo(original));
        Assert.That(catalog.Resolve(source), Is.EqualTo(original));
    }

    [Test]
    public void AncestorCandidateCannotOpenFloor()
    {
        var (installed, loose) = Pair();
        var catalog = new CMU3DSceneCatalog([installed, loose], id => id == "Child" ? ["DisposalJunction"] : null);
        Assert.That(catalog.Resolve("Child"), Is.Not.Null);
        Assert.That(catalog.WithAnchorState(catalog.Resolve("Child"), true), Is.Null);
    }

    [TestCase("missing")]
    [TestCase("oneWay")]
    [TestCase("family")]
    [TestCase("state")]
    [TestCase("cladding")]
    [TestCase("looseOpening")]
    [TestCase("rotation")]
    public void MalformedPairCannotBorrowOpeningOrLooseGeometry(string problem)
    {
        var (installed, loose) = Pair();
        switch (problem)
        {
            case "oneWay": loose.AlternateAnchorModel = "Missing"; break;
            case "family": loose.ReferencePrototype = "DisposalXJunction"; break;
            case "state": loose.ReferenceState = "conpipe-j2"; break;
            case "cladding": installed.PreserveSlabCladding = false; break;
            case "looseOpening": loose.FloorOpening = installed.FloorOpening; break;
            case "rotation": installed.SourceSpriteRotates = true; break;
        }
        var catalog = new CMU3DSceneCatalog(problem == "missing" ? [installed] : [installed, loose], _ => null);
        Assert.That(catalog.WithAnchorState(catalog.Resolve("DisposalJunction"), true), Is.Null);
        Assert.That(catalog.WithAnchorState(catalog.Resolve("DisposalJunction"), false), Is.Null);
    }

    private static (CMU3DModelPrototype, CMU3DModelPrototype) Pair(string source = "DisposalJunction", string suffix = "j1")
    {
        var installed = Model("Installed", source, suffix, true);
        var loose = Model("Loose", source, suffix, false);
        installed.AlternateAnchorModel = "Loose";
        loose.AlternateAnchorModel = "Installed";
        return (installed, loose);
    }

    private static CMU3DModelPrototype Model(string id, string source, string suffix, bool anchored)
    {
        var state = (anchored ? "pipe-" : "conpipe-") + suffix;
        var model = new CMU3DModelPrototype
        {
            ReferencePrototype = source,
            SourcePrototypes = anchored ? [source] : [],
            ReferenceRsi = "Structures/Piping/disposal.rsi",
            ReferenceState = state,
            ReferenceDirection = 0,
            SourceDirections = 1,
            Anchored = anchored,
            SourceSpriteRotates = !anchored,
            UseEntityRotation = true,
            PreserveSlabCladding = anchored,
            FloorOpening = anchored ? new CMU3DSlabOpeningBounds { Min = new Vector2(-.5f, -.5f), Max = new Vector2(.3f, .5f) } : null,
        };
        typeof(CMU3DModelPrototype).GetProperty(nameof(CMU3DModelPrototype.ID))!.SetValue(model, id);
        model.SpriteStates[state] = new CMU3DSpriteState { Frames = [new CMU3DModelFrame()], Delays = [1f] };
        return model;
    }
}
