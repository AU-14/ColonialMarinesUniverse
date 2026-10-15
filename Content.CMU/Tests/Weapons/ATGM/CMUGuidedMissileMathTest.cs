using System;
using System.Numerics;
using Content.Shared.CMU14.Weapons.ATGM;
using NUnit.Framework;

namespace Content.Tests.CMU14.Weapons.ATGM;

[TestFixture]
public sealed class CMUGuidedMissileMathTest
{
    private const double Epsilon = 1e-9;
    private static readonly double MaxOff = Math.PI / 2;

    [Test]
    public void HeadingChangePerStepIsBoundedByTurnRateAndCone()
    {
        var random = new Random(1234);
        for (var i = 0; i < 20000; i++)
        {
            var launch = (random.NextDouble() * 2 - 1) * Math.PI * 3;
            var current = launch + (random.NextDouble() * 2 - 1) * MaxOff;
            var desired = (random.NextDouble() * 2 - 1) * Math.PI * 3;
            var maxTurn = random.NextDouble() * 0.2;

            var next = CMUGuidedMissileMath.StepHeading(current, desired, launch, maxTurn, MaxOff);

            Assert.That(Math.Abs(CMUGuidedMissileMath.Wrap(next - current)), Is.LessThanOrEqualTo(maxTurn + Epsilon),
                "A single update must never turn the missile faster than its turn rate.");
            Assert.That(Math.Abs(CMUGuidedMissileMath.Wrap(next - launch)), Is.LessThanOrEqualTo(MaxOff + Epsilon),
                "The missile must stay within its off-boresight cone.");
        }
    }

    [Test]
    public void SteersOntoTargetInsideCone()
    {
        const double launch = 0;
        var desired = Math.PI / 3;
        var heading = launch;
        var steps = 0;
        while (Math.Abs(CMUGuidedMissileMath.Wrap(desired - heading)) > Epsilon && steps++ < 1000)
            heading = CMUGuidedMissileMath.StepHeading(heading, desired, launch, Math.PI / 180, MaxOff);

        Assert.That(steps, Is.InRange(60, 61), "60 degrees at 1 degree per step should take 60 steps, give or take rounding.");
    }

    [Test]
    public void TargetBehindIsClampedToNearestConeEdge()
    {
        const double launch = 0;
        var heading = Math.PI * 4 / 9; // 80 degrees left of boresight.
        var desired = -Math.PI * 17 / 18; // Behind and to the right.
        for (var i = 0; i < 1000; i++)
            heading = CMUGuidedMissileMath.StepHeading(heading, desired, launch, 0.05, MaxOff);

        Assert.That(heading, Is.EqualTo(-MaxOff).Within(1e-6),
            "A target behind the missile swings it to the cone edge on the target's side.");
    }

    [Test]
    public void NonFiniteInputsDoNotPropagate()
    {
        Assert.That(CMUGuidedMissileMath.StepHeading(0.5, double.NaN, 0, 0.1, MaxOff), Is.EqualTo(0.5).Within(Epsilon));
        Assert.That(CMUGuidedMissileMath.StepHeading(double.PositiveInfinity, 1, 0.25, 0.1, MaxOff), Is.EqualTo(0.25));
    }

    [Test]
    public void UnlimitedConeTakesShortestTurn()
    {
        var next = CMUGuidedMissileMath.StepHeading(Math.PI - 0.1, -Math.PI + 0.1, 0, 1, Math.PI);
        Assert.That(CMUGuidedMissileMath.Wrap(next - (-Math.PI + 0.1)), Is.EqualTo(0).Within(1e-9),
            "Crossing the +-pi seam must take the short way round.");
    }

    private const float MaxSteer = 4f;
    private const float SteerPerDistance = 0.5f;
    private const float Terminal = 2.5f;
    private const float Fade = 3f;

    private static Vector2 Aim(Vector2 missile, Vector2 target, Vector2? cursor)
    {
        return CMUGuidedMissileMath.ComputeAimPoint(missile, target, cursor, MaxSteer, SteerPerDistance, Terminal, Fade);
    }

    [Test]
    public void CursorOnTargetAimsAtTarget()
    {
        var target = new Vector2(20, 5);
        var aim = Aim(Vector2.Zero, target, target);
        Assert.That(Vector2.Distance(aim, target), Is.LessThan(1e-5f));
    }

    [Test]
    public void StaleOrMissingCursorAimsAtTarget()
    {
        var target = new Vector2(20, 5);
        Assert.That(Aim(Vector2.Zero, target, null), Is.EqualTo(target),
            "Without a fresh cursor the missile homes straight on the target.");
        Assert.That(Aim(Vector2.Zero, target, new Vector2(float.NaN, 0)), Is.EqualTo(target));
    }

    [Test]
    public void CursorOffsetIsClampedToSteerRadius()
    {
        // 20 tiles out: R = min(4, 0.5 * 20) = 4, fade = 1.
        var target = new Vector2(20, 0);
        var aim = Aim(Vector2.Zero, target, target + new Vector2(0, 50));
        Assert.That(Vector2.Distance(aim, target), Is.EqualTo(MaxSteer).Within(1e-4f));
        Assert.That(aim.Y, Is.GreaterThan(0), "The pull is toward the cursor.");

        // A small offset inside the radius is followed exactly.
        var small = Aim(Vector2.Zero, target, target + new Vector2(0, 1.5f));
        Assert.That(Vector2.Distance(small, target + new Vector2(0, 1.5f)), Is.LessThan(1e-4f));

        // 6 tiles out: R = min(4, 0.5 * 6) = 3, fade = (6 - 2.5) / 3 = 1.
        var near = Aim(new Vector2(14, 0), target, target + new Vector2(0, 50));
        Assert.That(Vector2.Distance(near, target), Is.EqualTo(3f).Within(1e-4f),
            "The steering radius shrinks with distance to the target.");
    }

    [Test]
    public void CursorFadesOutNearTarget()
    {
        var target = new Vector2(10, 0);
        var cursor = target + new Vector2(0, 50);

        var terminal = Aim(target - new Vector2(Terminal, 0), target, cursor);
        Assert.That(Vector2.Distance(terminal, target), Is.LessThan(1e-5f),
            "Inside the terminal distance the missile ignores the cursor.");

        // 4 tiles out: R = 2, fade = (4 - 2.5) / 3 = 0.5, so the pull is 1 tile.
        var fading = Aim(target - new Vector2(4, 0), target, cursor);
        Assert.That(Vector2.Distance(fading, target), Is.EqualTo(1f).Within(1e-4f));
    }

    [Test]
    public void LeadIsCapped()
    {
        var lead = CMUGuidedMissileMath.ComputeLead(new Vector2(3, 0), 9, 9, 2);
        Assert.That(lead.X, Is.EqualTo(2f).Within(1e-5f), "3 tiles/s for 1 s of flight is capped at 2 tiles.");

        var small = CMUGuidedMissileMath.ComputeLead(new Vector2(1, 0), 4.5f, 9, 2);
        Assert.That(small.X, Is.EqualTo(0.5f).Within(1e-5f));

        Assert.That(CMUGuidedMissileMath.ComputeLead(new Vector2(1, 0), 5, 0, 2), Is.EqualTo(Vector2.Zero));
    }
}
