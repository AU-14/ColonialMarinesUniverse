using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;
using Robust.Shared.GameObjects;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DScenePlacementTest
{
    [Test]
    public void ConnectedTableSupportsSeamItemsWhileKeepingUnjoinedEdgesInset()
    {
        var table = new CMU3DModelPrototype { SupportSurface = "top", ConnectToNeighbours = true };
        var top = new CMU3DModelPart { Label = "top", Min = new Vector3(-.47f, -.44f, .73f), Max = new Vector3(.47f, .44f, .835f) };
        table.Parts.Add(top);
        table.Parts.Add(new CMU3DModelPart { Label = "east rim", Min = new Vector3(.42f, -.43f, .833f), Max = new Vector3(.46f, .43f, .836f), OmitWhenConnected = 4 });
        table.Parts.Add(new CMU3DModelPart { Label = "west rim", Min = new Vector3(-.46f, -.43f, .833f), Max = new Vector3(-.42f, .43f, .836f), OmitWhenConnected = 8 });
        for (var mask = 0; mask < 16; mask++)
        {
            var parts = CMU3DSceneLayout.ConnectedParts(table.Parts, mask, table.SupportSurface);
            var expectedMin = new Vector3((mask & 8) != 0 ? -.5f : -.47f, (mask & 2) != 0 ? -.5f : -.44f, .73f);
            var expectedMax = new Vector3((mask & 4) != 0 ? .5f : .47f, (mask & 1) != 0 ? .5f : .44f, .835f);
            Assert.That(parts[0].Min, Is.EqualTo(expectedMin));
            Assert.That(parts[0].Max, Is.EqualTo(expectedMax));
            Assert.That(parts.Length, Is.EqualTo(1 + ((mask & 4) == 0 ? 1 : 0) + ((mask & 8) == 0 ? 1 : 0)));
            Assert.That(CMU3DScenePlacement.TrySurface(table, new EntityUid(1), Vector2.Zero, MathF.PI / 2, out var surface, parts), Is.True);
            var offset = CMU3DScenePlacement.Offset(Prop(), new EntityUid(2), new Vector2(0, .49f), [surface]);
            Assert.That(offset, Is.EqualTo((mask & 4) != 0 ? .737f : 0).Within(.00001), $"mask {mask}");
        }
        Assert.That(top.Min, Is.EqualTo(new Vector3(-.47f, -.44f, .73f)));
        Assert.That(top.Max, Is.EqualTo(new Vector3(.47f, .44f, .835f)));
    }

    [Test]
    public void RotatedOffCenterTableSupportsOnlyPropsInsideItsActualTop()
    {
        var table = new CMU3DModelPrototype { SupportSurface = "top" };
        table.Parts.Add(new CMU3DModelPart { Label = "top", Min = new Vector3(0, -.2f, .7f), Max = new Vector3(2, .2f, .86f) });
        Assert.That(CMU3DScenePlacement.TrySurface(table, new EntityUid(1), new Vector2(4, 5), MathF.PI / 2, out var surface), Is.True);
        var prop = Prop();
        Assert.That(CMU3DScenePlacement.Offset(prop, new EntityUid(2), new Vector2(4, 6), [surface]), Is.EqualTo(.762f).Within(.00001));
        Assert.That(CMU3DScenePlacement.Offset(prop, new EntityUid(2), new Vector2(5, 5), [surface]), Is.Zero);
        Assert.That(CMU3DScenePlacement.Offset(prop, new EntityUid(2), new Vector2(4, 4), [surface]), Is.Zero);
    }

    [Test]
    public void NoSupportFloorObjectsAndSelfSupportDoNotFloat()
    {
        var prop = Prop();
        var id = new EntityUid(1);
        var support = new CMU3DSceneSurface(id, Vector2.Zero, 0, new Vector3(-1), Vector3.One);
        Assert.That(CMU3DScenePlacement.Offset(prop, id, Vector2.Zero, [support]), Is.Zero);
        Assert.That(CMU3DScenePlacement.Offset(prop, new EntityUid(2), Vector2.Zero, []), Is.Zero);
        prop.Placement = "floor";
        Assert.That(CMU3DScenePlacement.Offset(prop, new EntityUid(2), Vector2.Zero, [support]), Is.Zero);
    }

    [Test]
    public void AmbiguousSurfaceLabelsAreNotUsableSupports()
    {
        var model = Prop(); model.SupportSurface = "top";
        var part = new CMU3DModelPart { Label = "top", Min = Vector3.Zero, Max = Vector3.One };
        model.Parts.Add(part); model.Parts.Add(part);
        Assert.That(CMU3DScenePlacement.TrySurface(model, new EntityUid(1), Vector2.Zero, 0, out _), Is.False);
    }

    [Test]
    public void SeparateRotatedBoardsSupportPropsWithoutFillingTheirGap()
    {
        var model = Boards();
        var surfaces = new List<CMU3DSceneSurface>();
        CMU3DScenePlacement.CollectSurfaces(model, new EntityUid(1), new Vector2(4, 5), MathF.PI / 2, surfaces);
        Assert.That(surfaces.Count, Is.EqualTo(2));
        foreach (var y in new[] { 4.7f, 5.3f })
            Assert.That(CMU3DScenePlacement.Offset(Prop(), new EntityUid(2), new Vector2(4, y), surfaces),
                Is.EqualTo(.052f).Within(.00001));
        Assert.That(CMU3DScenePlacement.Offset(Prop(), new EntityUid(2), new Vector2(4, 5), surfaces), Is.Zero);
        Assert.That(CMU3DScenePlacement.Offset(Prop(), new EntityUid(2), new Vector2(5, 5.3f), surfaces), Is.Zero);
    }

    [Test]
    public void OverlappingBoardsChooseHighestTopIndependentOfDeclarationOrder()
    {
        var model = Boards();
        model.Parts[1].Min = new Vector3(-.4f, -.4f, .2f);
        model.Parts[1].Max = new Vector3(-.2f, .4f, .3f);
        foreach (var labels in new[] { new[] { "left", "right" }, new[] { "right", "left" } })
        {
            model.SupportSurfaces = labels;
            var surfaces = new List<CMU3DSceneSurface>();
            CMU3DScenePlacement.CollectSurfaces(model, new EntityUid(1), Vector2.Zero, 0, surfaces);
            Assert.That(CMU3DScenePlacement.Offset(Prop(), new EntityUid(2), new Vector2(-.3f, 0), surfaces),
                Is.EqualTo(.202f).Within(.00001));
        }
    }

    [TestCase("missing")]
    [TestCase("duplicate")]
    [TestCase("ambiguous")]
    [TestCase("curved")]
    [TestCase("singleAndMultiple")]
    [TestCase("connected")]
    public void InvalidBoardDeclarationsLeaveExistingSupportsUntouched(string condition)
    {
        var model = Boards();
        switch (condition)
        {
            case "missing": model.SupportSurfaces = ["left", "missing"]; break;
            case "duplicate": model.SupportSurfaces = ["left", "left"]; break;
            case "ambiguous": model.Parts.Add(model.Parts[1]); break;
            case "curved": model.Parts[1].Shape = CMU3DPartShape.Ellipsoid; break;
            case "singleAndMultiple": model.SupportSurface = "left"; break;
            case "connected": model.ConnectToNeighbours = true; break;
        }
        var existing = new CMU3DSceneSurface(new EntityUid(9), Vector2.Zero, 0, Vector3.Zero, Vector3.One);
        var surfaces = new List<CMU3DSceneSurface> { existing };
        CMU3DScenePlacement.CollectSurfaces(model, new EntityUid(1), Vector2.Zero, 0, surfaces);
        Assert.That(surfaces, Is.EqualTo(new[] { existing }));
        Assert.That(CMU3DScenePlacement.TrySurface(model, new EntityUid(1), Vector2.Zero, 0, out _), Is.False);
    }

    [Test]
    public void BoardSupportsUseResolvedGeometryWithoutChangingPrototype()
    {
        var model = Boards();
        var parts = new[]
        {
            new CMU3DModelPart { Label = "left", Min = new Vector3(-.4f, -.4f, .4f), Max = new Vector3(-.2f, .4f, .5f) },
            model.Parts[1],
        };
        var surfaces = new List<CMU3DSceneSurface>();
        CMU3DScenePlacement.CollectSurfaces(model, new EntityUid(1), Vector2.Zero, 0, surfaces, parts);
        Assert.That(CMU3DScenePlacement.Offset(Prop(), new EntityUid(2), new Vector2(-.3f, 0), surfaces),
            Is.EqualTo(.402f).Within(.00001));
        Assert.That(model.Parts[0].Max.Z, Is.EqualTo(.15f));
    }

    private static CMU3DModelPrototype Boards() => new()
    {
        SupportSurfaces = ["left", "right"],
        Parts =
        [
            new() { Label = "left", Min = new Vector3(-.4f, -.4f, .1f), Max = new Vector3(-.2f, .4f, .15f) },
            new() { Label = "right", Min = new Vector3(.2f, -.4f, .1f), Max = new Vector3(.4f, .4f, .15f) },
        ],
    };

    private static CMU3DModelPrototype Prop()
    {
        var prop = new CMU3DModelPrototype { Placement = "surface" };
        prop.Parts.Add(new CMU3DModelPart { Min = new Vector3(-.1f, -.1f, .1f), Max = new Vector3(.1f, .1f, .5f) });
        return prop;
    }
}
