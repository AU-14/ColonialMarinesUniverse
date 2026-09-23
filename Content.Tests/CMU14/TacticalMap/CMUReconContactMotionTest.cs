using System;
using System.Numerics;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.CMU14.TacticalMap;

[TestFixture]
public sealed class CMUReconContactMotionTest
{
    private static CMUReconContact Contact(int x, int id = 1, int depth = 0, bool live = true) =>
        new(depth, new TacticalMapBlip { Indices = new Vector2i(x, 0) }, id, live);
    private static TimeSpan At(double seconds) => TimeSpan.FromSeconds(seconds);

    [Test]
    public void LiveMotionIsContinuousBoundedAndSettlesWhenUpdatesStop()
    {
        var motion = new CMUReconContactMotion();
        motion.Update([Contact(0)], At(0));
        var next = Contact(2);
        motion.Update([next], At(0.5));
        Assert.That(motion.Position(next, At(0.5)), Is.EqualTo(Vector2.Zero));
        Assert.That(motion.Position(next, At(0.6)).X, Is.InRange(0.1f, 2f));
        Assert.That(motion.Position(next, At(0.75)).X, Is.EqualTo(3).Within(0.001));
        Assert.That(motion.Position(next, At(5)), Is.EqualTo(new Vector2(2, 0)));
    }

    [Test]
    public void PublishedIntelNeverPredictsAndRemovedContactsLoseTheirHistory()
    {
        var motion = new CMUReconContactMotion();
        motion.Update([Contact(0)], At(0));
        var published = Contact(2, live: false);
        motion.Update([published], At(0.5));
        Assert.That(motion.Position(published, At(0.6)), Is.EqualTo(new Vector2(2, 0)));
        motion.Update([], At(1));
        var returned = Contact(5);
        motion.Update([returned], At(1.1));
        Assert.That(motion.Position(returned, At(1.1)), Is.EqualTo(new Vector2(5, 0)));
    }

    [TestCase(30, 0, 0.5)]
    [TestCase(2, 1, 0.5)]
    [TestCase(2, 0, 5)]
    public void TeleportsFloorChangesAndLongGapsSnap(int x, int depth, double seconds)
    {
        var motion = new CMUReconContactMotion();
        motion.Update([Contact(0)], At(0));
        var next = Contact(x, depth: depth);
        motion.Update([next], At(seconds));
        Assert.That(motion.Position(next, At(seconds + 0.1)), Is.EqualTo(new Vector2(x, 0)));
    }

    [Test]
    public void IdenticalRoleSpritesDoNotShareMovementHistory()
    {
        var motion = new CMUReconContactMotion();
        motion.Update([Contact(0, 1), Contact(10, 2)], At(0));
        var first = Contact(2, 1);
        var second = Contact(8, 2);
        motion.Update([second, first], At(0.5));
        Assert.That(motion.Position(first, At(0.75)).X, Is.EqualTo(3).Within(0.001));
        Assert.That(motion.Position(second, At(0.75)).X, Is.EqualTo(7).Within(0.001));
    }
}
