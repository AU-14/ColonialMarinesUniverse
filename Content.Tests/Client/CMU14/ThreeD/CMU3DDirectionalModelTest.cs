using System;
using System.Linq;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DDirectionalModelTest
{
    [TestCase(4)]
    [TestCase(8)]
    public void EverySourceSlotSelectsItsGeometryWithoutMutatingResolution(int directions)
    {
        var models = Poses(directions);
        var catalog = new CMU3DSceneCatalog(models, id => id == "Child" ? ["Paper"] : null);
        var angles = new[] { 0, 180, 90, -90, 45, -45, 135, -135 };
        var sourceDirections = new[] { Direction.South, Direction.North, Direction.East, Direction.West,
            Direction.SouthEast, Direction.SouthWest, Direction.NorthEast, Direction.NorthWest };
        foreach (var prototype in new[] { "Paper", "Child" })
        {
            var original = catalog.Resolve(prototype);
            for (var i = 0; i < directions; i++)
            foreach (var turns in new[] { -2, 0, 3 })
            {
                var yaw = angles[i] * MathF.PI / 180 + turns * MathF.Tau;
                var selected = catalog.WithDirection(original, yaw)!.Value;
                Assert.That(selected.Model, Is.SameAs(models[i]));
                Assert.That(selected.Exact, Is.EqualTo(prototype == "Paper"));
                Assert.That(selected.Reference, Is.EqualTo("Paper"));
                Assert.That(catalog.WithDirection(selected, yaw), Is.EqualTo(selected));
                Assert.That(CMU3DSceneLayout.ReferenceDirection(selected.Model, Direction.West), Is.EqualTo(sourceDirections[i]));
                Assert.That(MathF.Sin(CMU3DSceneLayout.RenderYaw(selected.Model, yaw, true, false)), Is.EqualTo(0).Within(.00001));
                Assert.That(catalog.Resolve(prototype), Is.EqualTo(original));
            }
        }
    }

    [TestCase("missing")]
    [TestCase("wrong-state")]
    [TestCase("wrong-slot")]
    [TestCase("nan")]
    public void UnsupportedDirectionsNeverBorrowAnotherPose(string defect)
    {
        var models = Poses(4);
        var yaw = MathF.PI / 2;
        if (defect == "missing") models[0].DirectionalModels[2] = "Missing";
        if (defect == "wrong-state") models[2].ReferenceState = "different";
        if (defect == "wrong-slot") models[2].ReferenceDirection = 1;
        if (defect == "nan") yaw = float.NaN;
        var catalog = new CMU3DSceneCatalog(models, _ => null);
        Assert.That(catalog.WithDirection(catalog.Resolve("Paper"), yaw), Is.Null);
        Assert.That(catalog.WithDirection(null, yaw), Is.Null);
    }

    private static CMU3DModelPrototype[] Poses(int directions)
    {
        var names = Enumerable.Range(0, directions).Select(i => $"Paper{i}").ToArray();
        var angles = new[] { 0, 180, 90, -90, 45, -45, 135, -135 };
        return names.Select((name, index) =>
        {
            var model = new CMU3DModelPrototype
            {
                Label = name, SourcePrototypes = index == 0 ? ["Paper"] : [],
                ReferenceRsi = "/Textures/CMU14/N14content/world.rsi", ReferenceState = "scattered_papers",
                ReferenceDirection = index, SourceDirections = directions, DirectionalModels = names.ToArray(),
                YawOffset = -angles[index],
            };
            typeof(CMU3DModelPrototype).GetProperty(nameof(CMU3DModelPrototype.ID))!.SetValue(model, name);
            return model;
        }).ToArray();
    }
}
