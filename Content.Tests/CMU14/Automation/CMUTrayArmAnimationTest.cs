using System.Collections.Generic;
using System.Numerics;
using Content.Shared.CMU14.Automation;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.CMU14.Automation;

/// <summary>CMU14 (ECS-04): the tray arm's cosmetic reach pose is pure maths, checked here without a client.</summary>
[TestFixture]
public sealed class CMUTrayArmAnimationTest
{
    private const float Tolerance = 0.001f;

    private static IEnumerable<Vector2> WorkTiles()
    {
        for (var depth = 1; depth <= 3; depth++)
        for (var row = -1; row <= 1; row++)
            yield return new Vector2(depth, row);
    }

    [Test]
    public void RestsOutsideAReach()
    {
        var target = new Vector2(3, 1);
        foreach (var progress in new[] { -1f, 0f, 1f, 1.5f, float.NaN })
        {
            var pose = CMUTrayArmAnimation.GetPose(target, progress);
            Assert.That(Vector2.Distance(pose.Claw, CMUTrayArmAnimation.RestClaw), Is.LessThan(Tolerance), $"progress {progress}");
            Assert.That(pose.Telescope, Is.Zero, $"progress {progress}");
        }
    }

    [Test]
    public void HoldsTheClawOnEveryWorkTile()
    {
        foreach (var tile in WorkTiles())
        {
            var pose = CMUTrayArmAnimation.GetPose(tile, 0.5f);
            Assert.That(Vector2.Distance(pose.Claw, tile), Is.LessThan(Tolerance), $"tile {tile}");
            Assert.That(pose.Telescope > 0f, Is.EqualTo(tile.Length() > CMUTrayArmAnimation.NaturalReach), $"tile {tile}");
            // Elbow up: the elbow is always counter-clockwise of the line from the shoulder to the claw.
            Assert.That(pose.Claw.X * pose.Elbow.Y - pose.Claw.Y * pose.Elbow.X, Is.GreaterThan(0f), $"tile {tile}");
        }
    }

    [Test]
    public void PartRotationsPutTheJointsWhereThePoseSays()
    {
        foreach (var tile in WorkTiles())
        foreach (var progress in new[] { 0.1f, 0.3f, 0.5f, 0.8f })
        {
            var pose = CMUTrayArmAnimation.GetPose(tile, progress);
            // The upper arm part, rotated by Shoulder, ends at the elbow.
            Assert.That(Vector2.Distance(pose.Shoulder.RotateVec(CMUTrayArmAnimation.Upper), pose.Elbow), Is.LessThan(Tolerance));
            // The forearm part, rotated by Forearm about its moved pivot, grips at the claw.
            Assert.That(Vector2.Distance(pose.ForearmPivot + pose.Forearm.RotateVec(CMUTrayArmAnimation.Forearm), pose.Claw), Is.LessThan(Tolerance));
            // The pivot moves out along the forearm by exactly the telescope length.
            Assert.That(Vector2.Distance(pose.ForearmPivot, pose.Elbow), Is.EqualTo(pose.Telescope).Within(Tolerance));
        }
    }

    [Test]
    public void TelescopeStartsSmoothlyAtTheNaturalReach()
    {
        var direction = Vector2.Normalize(new Vector2(1, -1));
        var atReach = CMUTrayArmAnimation.Solve(direction * CMUTrayArmAnimation.NaturalReach);
        var beyond = CMUTrayArmAnimation.Solve(direction * (CMUTrayArmAnimation.NaturalReach + 0.01f));
        Assert.That(atReach.Telescope, Is.EqualTo(0f).Within(Tolerance));
        Assert.That(beyond.Telescope, Is.EqualTo(0.01f).Within(Tolerance));
        Assert.That(Vector2.Distance(atReach.Elbow, beyond.Elbow), Is.LessThan(0.02f));
    }

    [Test]
    public void ReachEasesOutHoldsAndReturns()
    {
        Assert.That(CMUTrayArmAnimation.GetExtension(0f), Is.Zero);
        Assert.That(CMUTrayArmAnimation.GetExtension(CMUTrayArmAnimation.OutEnd - 0.0001f), Is.EqualTo(1f).Within(Tolerance));
        Assert.That(CMUTrayArmAnimation.GetExtension(0.5f), Is.EqualTo(1f));
        Assert.That(CMUTrayArmAnimation.GetExtension(CMUTrayArmAnimation.ReturnStart + 0.0001f), Is.EqualTo(1f).Within(Tolerance));
        Assert.That(CMUTrayArmAnimation.GetExtension(0.9999f), Is.EqualTo(0f).Within(Tolerance));

        var previous = 0f;
        for (var progress = 0.01f; progress < CMUTrayArmAnimation.OutEnd; progress += 0.01f)
        {
            var extension = CMUTrayArmAnimation.GetExtension(progress);
            Assert.That(extension, Is.GreaterThanOrEqualTo(previous));
            previous = extension;
        }
    }

    [Test]
    public void ShowsTheLoadOnItsLegsOnly()
    {
        // Planting: the seed packet goes out and is left in the tray.
        Assert.That(CMUTrayArmAnimation.ShowsCarried(0.2f, true), Is.True);
        Assert.That(CMUTrayArmAnimation.ShowsCarried(0.5f, true), Is.True);
        Assert.That(CMUTrayArmAnimation.ShowsCarried(0.8f, true), Is.False);
        // Harvesting: the produce is picked up at the tray and brought back.
        Assert.That(CMUTrayArmAnimation.ShowsCarried(0.2f, false), Is.False);
        Assert.That(CMUTrayArmAnimation.ShowsCarried(0.5f, false), Is.True);
        Assert.That(CMUTrayArmAnimation.ShowsCarried(0.8f, false), Is.True);
        // At rest nothing shows.
        Assert.That(CMUTrayArmAnimation.ShowsCarried(1f, false), Is.False);
        Assert.That(CMUTrayArmAnimation.ShowsCarried(-0.1f, true), Is.False);
    }

    [Test]
    public void MirrorIsItsOwnInverse()
    {
        var vector = new Vector2(-2.5f, 1.25f);
        Assert.That(CMUTrayArmAnimation.Mirror(vector, false), Is.EqualTo(vector));
        Assert.That(CMUTrayArmAnimation.Mirror(vector, true), Is.EqualTo(new Vector2(2.5f, 1.25f)));
        Assert.That(CMUTrayArmAnimation.Mirror(CMUTrayArmAnimation.Mirror(vector, true), true), Is.EqualTo(vector));
    }
}
