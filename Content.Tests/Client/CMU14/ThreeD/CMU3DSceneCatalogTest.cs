using System.Collections.Generic;
using System.Numerics;
using System;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using Content.Shared.Doors.Components;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DSceneCatalogTest
{
    [Test]
    public void RandomTreeChoiceSelectsEachExactVariantWithoutChangingStaticResolution()
    {
        var models = new CMU3DModelPrototype[6];
        for (var i = 0; i < models.Length; i++)
            models[i] = RandomTree($"tree0{i + 1}");
        var catalog = new CMU3DSceneCatalog(models, id => id == "Child" ? ["RandomTree"] : null);
        Assert.That(catalog.HasRandomSpriteVariants("RandomTree"), Is.True);
        Assert.That(catalog.HasRandomSpriteVariants("Child"), Is.False);
        for (var i = 0; i < models.Length; i++)
        {
            var choice = new Dictionary<string, (string, Color?)> { ["random"] = ($"tree0{i + 1}", null) };
            var match = catalog.ResolveRandomSprite("RandomTree", choice)!.Value;
            Assert.That(match.Model, Is.SameAs(models[i]));
            Assert.That(match.Exact, Is.True);
            Assert.That(match.Reference, Is.EqualTo("RandomTree"));
            Assert.That(catalog.ResolveRandomSprite("Child", choice), Is.Null);
            Assert.That(catalog.Resolve($"StaticTree_tree0{i + 1}")!.Value.Model, Is.SameAs(models[i]));
        }
        Assert.That(catalog.Resolve("RandomTree"), Is.Null, "No random choice is implied by a prototype reference.");
    }

    [Test]
    public void UnknownExtraAndColoredRandomLayersNeverBorrowDefaultGeometry()
    {
        var catalog = new CMU3DSceneCatalog([RandomTree("tree01")], _ => null);
        var choice = new Dictionary<string, (string, Color?)>();
        Assert.That(catalog.ResolveRandomSprite("RandomTree", choice), Is.Null);
        choice["random"] = ("missing", null);
        Assert.That(catalog.ResolveRandomSprite("RandomTree", choice), Is.Null);
        choice["random"] = ("tree01", Color.Red);
        Assert.That(catalog.ResolveRandomSprite("RandomTree", choice), Is.Null);
        choice["random"] = ("tree01", Color.White);
        Assert.That(catalog.ResolveRandomSprite("RandomTree", choice), Is.Not.Null);
        choice["other"] = ("tree01", null);
        Assert.That(catalog.ResolveRandomSprite("RandomTree", choice), Is.Null);
        choice.Remove("random");
        Assert.That(catalog.ResolveRandomSprite("RandomTree", choice), Is.Null);
    }

    [Test]
    public void AmbiguousOrIncompleteRandomReferencesFailClosed()
    {
        var model = RandomTree("tree01");
        var choice = new Dictionary<string, (string, Color?)> { ["random"] = ("tree01", null) };
        var duplicate = RandomTree("tree01");
        Assert.That(new CMU3DSceneCatalog([model, duplicate], _ => null).ResolveRandomSprite("RandomTree", choice), Is.Null);
        model.ReferenceRsi = null;
        var invalid = new CMU3DSceneCatalog([model], _ => null);
        Assert.That(invalid.HasRandomSpriteVariants("RandomTree"), Is.True);
        Assert.That(invalid.ResolveRandomSprite("RandomTree", choice), Is.Null);
    }

    private static CMU3DModelPrototype RandomTree(string state)
    {
        var model = Model("StaticTree_" + state);
        typeof(CMU3DModelPrototype).GetProperty(nameof(CMU3DModelPrototype.ID))!.SetValue(model, state);
        model.RandomSpritePrototypes = ["RandomTree"];
        model.RandomSpriteLayer = "random";
        model.ReferenceRsi = "Trees.rsi";
        model.ReferenceState = state;
        return model;
    }

    [Test]
    public void FoldPosesSwitchBothWaysAndRetainExactOrInheritedProvenance()
    {
        var (unfolded, folded) = FoldPair();
        var catalog = new CMU3DSceneCatalog([unfolded, folded], id => id == "Child" ? ["Seat"] : null);
        foreach (var id in new[] { "Seat", "Child" })
        {
            var original = catalog.Resolve(id);
            var posed = catalog.WithFoldState(original, true);
            Assert.That(posed!.Value.Model, Is.SameAs(folded));
            Assert.That(posed.Value.Exact, Is.EqualTo(id == "Seat"));
            Assert.That(posed.Value.Reference, Is.EqualTo("Seat"));
            Assert.That(catalog.WithFoldState(posed, true), Is.EqualTo(posed));
            Assert.That(catalog.WithFoldState(posed, false), Is.EqualTo(original));
            Assert.That(catalog.Resolve(id), Is.EqualTo(original), "Pose selection must not change cached prototype resolution.");
        }
        Assert.That(catalog.WithFoldState(catalog.Resolve("FoldedSeat"), false)!.Value.Model, Is.SameAs(unfolded));
    }

    [TestCase("missing")]
    [TestCase("wrongPose")]
    [TestCase("oneWay")]
    public void MissingOrInvalidFoldPoseNeverBorrowsTheWrongAssembly(string defect)
    {
        var (unfolded, folded) = FoldPair();
        switch (defect)
        {
            case "missing": unfolded.AlternateFoldModel = "Missing"; break;
            case "wrongPose": folded.Folded = false; break;
            case "oneWay": folded.AlternateFoldModel = null; break;
        }
        var catalog = new CMU3DSceneCatalog([unfolded, folded], _ => null);
        Assert.That(catalog.WithFoldState(catalog.Resolve("Seat"), true), Is.Null);
        Assert.That(catalog.WithFoldState(null, true), Is.Null);
        Assert.That(catalog.WithFoldState(null, false), Is.Null);
        Assert.That(catalog.WithFoldState(catalog.Resolve("Seat"), false)!.Value.Model, Is.SameAs(unfolded));
    }

    [Test]
    public void InitiallyFoldedModelWithoutAnUnfoldedPartnerCannotStayFoldedAfterUnfolding()
    {
        var (_, folded) = FoldPair();
        folded.AlternateFoldModel = null;
        var catalog = new CMU3DSceneCatalog([folded], _ => null);
        Assert.That(catalog.WithFoldState(catalog.Resolve("FoldedSeat"), true)!.Value.Model, Is.SameAs(folded));
        Assert.That(catalog.WithFoldState(catalog.Resolve("FoldedSeat"), false), Is.Null);
    }

    [Test]
    public void FoldingChangesFacingAndAttachmentUsingTheChosenGeometry()
    {
        var (unfolded, folded) = FoldPair();
        unfolded.SourceDirections = 4;
        folded.SourceDirections = 1;
        folded.Placement = "surface";
        folded.Parts.Add(new CMU3DModelPart { Min = new Vector3(-.2f, -.1f, .03f), Max = new Vector3(.2f, .1f, .12f) });
        var catalog = new CMU3DSceneCatalog([unfolded, folded], _ => null);
        var model = catalog.WithFoldState(catalog.Resolve("Seat"), true)!.Value.Model;
        Assert.That(CMU3DSceneLayout.RenderYaw(model, MathF.PI / 2, true, false), Is.Zero);
        var surface = new CMU3DSceneSurface(new Robust.Shared.GameObjects.EntityUid(1), Vector2.Zero, 0,
            new Vector3(-.5f, -.5f, .7f), new Vector3(.5f, .5f, .948f));
        var source = new Robust.Shared.GameObjects.EntityUid(2);
        Assert.That(CMU3DScenePlacement.Offset(model, source, Vector2.Zero, [surface]), Is.EqualTo(.920f).Within(.00001));
        model = catalog.WithFoldState(catalog.Resolve("FoldedSeat"), false)!.Value.Model;
        Assert.That(MathF.Abs(CMU3DSceneLayout.RenderYaw(model, MathF.PI / 2, true, false)), Is.EqualTo(MathF.PI / 2).Within(.00001));
        Assert.That(CMU3DScenePlacement.Offset(model, source, Vector2.Zero, [surface]), Is.Zero);
    }

    private static (CMU3DModelPrototype Unfolded, CMU3DModelPrototype Folded) FoldPair()
    {
        var unfolded = Model("Seat");
        var folded = Model("FoldedSeat");
        typeof(CMU3DModelPrototype).GetProperty(nameof(CMU3DModelPrototype.ID))!.SetValue(unfolded, "Unfolded");
        typeof(CMU3DModelPrototype).GetProperty(nameof(CMU3DModelPrototype.ID))!.SetValue(folded, "Folded");
        unfolded.AlternateFoldModel = "Folded";
        folded.AlternateFoldModel = "Unfolded";
        folded.Folded = true;
        return (unfolded, folded);
    }

    [Test]
    public void StableDoorPosesSwitchBothWaysWithoutChangingMatchProvenance()
    {
        var closed = Model("Door");
        var open = Model("InitiallyOpenDoor");
        typeof(CMU3DModelPrototype).GetProperty(nameof(CMU3DModelPrototype.ID))!.SetValue(closed, "Closed");
        typeof(CMU3DModelPrototype).GetProperty(nameof(CMU3DModelPrototype.ID))!.SetValue(open, "Open");
        closed.AlternateDoorModel = "Open";
        open.AlternateDoorModel = "Closed";
        open.DoorState = DoorState.Open;
        var catalog = new CMU3DSceneCatalog([closed, open], id => id == "Child" ? ["Door"] : null);
        var posed = catalog.WithDoorState(catalog.Resolve("Child"), DoorState.Open);
        Assert.That(posed!.Value.Model, Is.SameAs(open));
        Assert.That(posed.Value.Exact, Is.False);
        Assert.That(posed.Value.Reference, Is.EqualTo("Door"));
        Assert.That(catalog.WithDoorState(catalog.Resolve("InitiallyOpenDoor"), DoorState.Closed)!.Value.Model, Is.SameAs(closed));
        Assert.That(catalog.WithDoorState(posed, DoorState.Open), Is.EqualTo(posed));
        Assert.That(catalog.WithDoorState(posed, DoorState.Opening), Is.Null);
        Assert.That(catalog.WithDoorState(posed, DoorState.Closing), Is.Null);
        closed.AlternateDoorModel = "Absent";
        Assert.That(catalog.WithDoorState(catalog.Resolve("Door"), DoorState.Open), Is.Null);
    }

    [Test]
    public void ExactDraftTakesPrecedenceOverAReviewedAncestor()
    {
        var exact = Model("ChairRed");
        var parent = Model("Chair", "reviewed");
        var catalog = new CMU3DSceneCatalog([parent, exact], id => id == "ChairRed" ? ["Chair"] : null);
        var match = catalog.Resolve("ChairRed");
        Assert.That(match, Is.Not.Null);
        Assert.That(match!.Value.Model, Is.SameAs(exact));
        Assert.That(match.Value.Exact, Is.True);
        Assert.That(match.Value.Model.Status, Is.EqualTo("draft"));
    }

    [Test]
    public void FirstParentLineageWinsAndIsStillAnInheritedCandidate()
    {
        var deep = Model("BaseFirst");
        var later = Model("Second");
        var parents = new Dictionary<string, string[]>
        {
            ["Child"] = ["First", "Second"],
            ["First"] = ["BaseFirst"],
        };
        var catalog = new CMU3DSceneCatalog([later, deep], id => parents.GetValueOrDefault(id));
        var match = catalog.Resolve("Child");
        Assert.That(match!.Value.Model, Is.SameAs(deep));
        Assert.That(match.Value.Reference, Is.EqualTo("BaseFirst"));
        Assert.That(match.Value.Exact, Is.False);
    }

    [Test]
    public void CyclesAndMissingParentsDoNotHideAnAvailableLaterParent()
    {
        var model = Model("Valid");
        var parents = new Dictionary<string, string[]>
        {
            ["Child"] = ["Cycle", "Missing", "Valid"],
            ["Cycle"] = ["Child"],
        };
        var catalog = new CMU3DSceneCatalog([model], id => parents.GetValueOrDefault(id));
        Assert.That(catalog.Resolve("Child")!.Value.Model, Is.SameAs(model));
        Assert.That(catalog.Resolve("Missing"), Is.Null);
    }

    [Test]
    public void ReviewedSourceReferenceWinsWithinTheSamePrototypeOnly()
    {
        var draft = Model("Chair");
        var reviewed = Model("Chair", "reviewed");
        var catalog = new CMU3DSceneCatalog([draft, reviewed], _ => null);
        Assert.That(catalog.Resolve("Chair")!.Value.Model, Is.SameAs(reviewed));
    }

    private static CMU3DModelPrototype Model(string reference, string status = "draft") => new()
    {
        SourcePrototypes = [reference],
        Status = status,
    };
}
