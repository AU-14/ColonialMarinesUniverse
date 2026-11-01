using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.CMU14.ThreeD;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DModelRendererTest
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "RA0039", Justification = "CMU14: This test supplies isolated mutable fixture data; prototype registration and inheritance are not exercised.")]
    [Test]
    public void LowForegroundDetailDrawsAfterLargeBackgroundPanel()
    {
        var body = new CMU3DModelPart { Min = new Vector3(-1, 0, 0), Max = new Vector3(1, 1, 3) };
        var detail = new CMU3DModelPart { Min = new Vector3(-0.4f, -0.04f, 0.1f), Max = new Vector3(0.4f, -0.01f, 0.3f) };
        var renderer = new CMU3DModelRenderer();
        renderer.SetModel(new CMU3DModelPrototype { Parts = [body, detail] });
        Assert.That(renderer.WithinBudget, Is.True);
        var eye = new Vector3(0, -5, 4);
        var ray = Vector3.Normalize(new Vector3(0, -0.04f, 0.2f) - eye);
        var faces = new List<CMU3DModelFace>();
        renderer.CollectVisibleFaces(eye, faces);
        var hits = faces.Where(face => TryHit(face, eye, ray, out _)).ToArray();
        Assert.That(hits.Length, Is.GreaterThanOrEqualTo(2));
        Assert.That(hits[^1].Position, Is.EqualTo(-0.04f));
        Assert.That(hits[^1].Axis, Is.EqualTo(1));
        // This is the actual failure case: center-depth sorting would paint the big panel last.
        var panel = hits.First(face => face.Axis == 1 && face.Position == 0);
        Assert.That(Vector3.Dot(panel.Center - eye, ray), Is.LessThan(Vector3.Dot(hits[^1].Center - eye, ray)));
    }

    [Test]
    public void CrossingPlanesAreSplitAndAgreeWithRayDepthFromEveryOctant()
    {
        var source = new List<CMU3DModelFace>
        {
            new(new Vector3(-2, 0, -2), new Vector3(2, 0, 2), 1, 1, Color.Red, 0),
            new(new Vector3(-2, 0, -2), new Vector3(2, 0, 2), 1, -1, Color.Red, 1),
            new(new Vector3(0, -2, -2), new Vector3(0, 2, 2), 0, 1, Color.Blue, 2),
            new(new Vector3(0, -2, -2), new Vector3(0, 2, 2), 0, -1, Color.Blue, 3),
            new(new Vector3(-2, -2, 0), new Vector3(2, 2, 0), 2, 1, Color.Green, 4),
            new(new Vector3(-2, -2, 0), new Vector3(2, 2, 0), 2, -1, Color.Green, 5),
        };
        var bsp = new CMU3DModelBsp();
        Assert.That(bsp.TryBuild(source), Is.True);
        Assert.That(bsp.FragmentCount, Is.GreaterThan(source.Count), "Crossing planes require geometric splits.");
        var ordered = new List<CMU3DModelFace>();
        foreach (var x in new[] { -1, 1 })
        foreach (var y in new[] { -1, 1 })
        foreach (var z in new[] { -1, 1 })
        {
            var eye = new Vector3(x * 5, y * 6, z * 7);
            bsp.CollectVisibleFaces(eye, ordered);
            for (var i = -3; i <= 3; i++)
            for (var j = -3; j <= 3; j++)
            {
                var target = new Vector3(i * 0.27f + 0.11f, j * 0.23f + 0.07f, (i - j) * 0.13f + 0.03f);
                var ray = Vector3.Normalize(target - eye);
                var hitDistances = new List<float>();
                foreach (var face in ordered)
                {
                    if (TryHit(face, eye, ray, out var distance))
                        hitDistances.Add(distance);
                }
                for (var hit = 1; hit < hitDistances.Count; hit++)
                    Assert.That(hitDistances[hit], Is.LessThanOrEqualTo(hitDistances[hit - 1] + 0.0001f),
                        "Every overlapping fragment must be painted far-to-near along the same ray.");
            }
        }
    }

    [Test]
    public void CoincidentFacesHaveDeterministicAuthoredPrecedence()
    {
        var first = new CMU3DModelFace(new Vector3(-1, 0, -1), new Vector3(1, 0, 1), 1, -1, Color.Red, 3);
        var last = first with { Color = Color.Blue, SourceOrder = 9 };
        var bsp = new CMU3DModelBsp();
        var ordered = new List<CMU3DModelFace>();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            Assert.That(bsp.TryBuild([last, first]), Is.True);
            bsp.CollectVisibleFaces(new Vector3(0, -3, 1), ordered);
            Assert.That(ordered.Select(face => face.SourceOrder), Is.EqualTo(new[] { 3, 9 }));
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "RA0039", Justification = "CMU14: This test supplies isolated mutable fixture data; prototype registration and inheritance are not exercised.")]
    [Test]
    public void OverBudgetModelIsRejectedWithoutDrawingPartialGeometry()
    {
        var part = new CMU3DModelPart { Min = Vector3.Zero, Max = Vector3.One };
        var renderer = new CMU3DModelRenderer();
        var model = new CMU3DModelPrototype();
        for (var i = 0; i <= CMU3DModelBsp.MaxInputFaces / 6; i++)
            model.Parts.Add(part);
        renderer.SetModel(model);
        var faces = new List<CMU3DModelFace>();
        renderer.CollectVisibleFaces(new Vector3(-5, -5, 5), faces);
        Assert.Multiple(() =>
        {
            Assert.That(renderer.WithinBudget, Is.False);
            Assert.That(faces, Is.Empty);
        });
        renderer.SetModel(new CMU3DModelPrototype { Parts = [part] });
        renderer.CollectVisibleFaces(new Vector3(-5, -5, 5), faces);
        Assert.That(renderer.WithinBudget, Is.True);
        Assert.That(faces, Is.Not.Empty, "A later small model must recover after a budget rejection.");
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "RA0039", Justification = "CMU14: This test supplies isolated mutable fixture data; prototype registration and inheritance are not exercised.")]
    [Test]
    public void InvalidPartsCannotCorruptCameraBounds()
    {
        var valid = new CMU3DModelPart { Min = new Vector3(-2, -1, 0), Max = new Vector3(2, 1, 3) };
        var model = new CMU3DModelPrototype
        {
            Parts =
            [
                valid,
                new CMU3DModelPart { Min = Vector3.One, Max = Vector3.Zero },
                new CMU3DModelPart { Min = Vector3.Zero, Max = new Vector3(float.NaN) },
                new CMU3DModelPart { Min = Vector3.Zero, Max = Vector3.Zero },
            ],
        };
        var renderer = new CMU3DModelRenderer();
        renderer.SetModel(model);
        Assert.Multiple(() =>
        {
            Assert.That(renderer.PartCount, Is.EqualTo(1));
            Assert.That(renderer.Min, Is.EqualTo(valid.Min));
            Assert.That(renderer.Max, Is.EqualTo(valid.Max));
            Assert.That(model.Parts, Has.Count.EqualTo(4), "The preview must not modify prototype data.");
        });
        renderer.SetModel(null);
        Assert.That(renderer.PartCount, Is.Zero);
        Assert.That(renderer.Max.Z, Is.GreaterThan(renderer.Min.Z), "An empty catalog still needs finite fit bounds.");
    }

    private static bool TryHit(CMU3DModelFace face, Vector3 eye, Vector3 direction, out float distance)
    {
        distance = 0;
        var denominator = CMU3DModelFace.Coordinate(direction, face.Axis);
        if (Math.Abs(denominator) < 0.00001f)
            return false;
        distance = (face.Position - CMU3DModelFace.Coordinate(eye, face.Axis)) / denominator;
        if (distance <= 0)
            return false;
        var hit = eye + direction * distance;
        for (var axis = 0; axis < 3; axis++)
        {
            if (axis == face.Axis)
                continue;
            var value = CMU3DModelFace.Coordinate(hit, axis);
            if (value < CMU3DModelFace.Coordinate(face.Min, axis) - 0.00001f ||
                value > CMU3DModelFace.Coordinate(face.Max, axis) + 0.00001f)
                return false;
        }
        return true;
    }
}
