using System;
using Content.Shared.CMU14.Fighter;
using NUnit.Framework;

namespace Content.Tests.CMU14.Fighter;

[TestFixture]
public sealed class FighterFlareEvasionTest
{
    [TestCase(26f, 0, .45f)]
    [TestCase(8f, 0, .225f)]
    [TestCase(26f, 4, .18f)]
    [TestCase(8f, 4, .09f)]
    [TestCase(17f, 2.5, .2109375f)]
    [TestCase(26f, 5, 0f)]
    [TestCase(26f, 6, 0f)]
    public void FastAndEarlyFlaresAreBestButMissilesAreMoreLikelyToHit(float speed, double elapsed, float expected)
    {
        var aircraft = new FighterAircraftComponent { Speed = speed };
        var combat = new FighterAirCombatComponent { IncomingStartedAt = TimeSpan.Zero, IncomingAt = TimeSpan.FromSeconds(5) };
        var chance = FighterAirCombat.FlareEvasion(aircraft, combat, TimeSpan.FromSeconds(elapsed));
        Assert.That(chance, Is.EqualTo(expected).Within(.00001f));
        Assert.That(chance, Is.LessThan(.65f), "all normal cases improve missile hit chance over the old flat 65% evasion");
    }

    [Test]
    public void TimingUsesTheActualMissileFlightWindowAndSpeedIsClamped()
    {
        var aircraft = new FighterAircraftComponent { Speed = 100 };
        var combat = new FighterAirCombatComponent { IncomingStartedAt = TimeSpan.FromSeconds(10), IncomingAt = TimeSpan.FromSeconds(12) };
        Assert.That(FighterAirCombat.FlareEvasion(aircraft, combat, TimeSpan.FromSeconds(11)), Is.EqualTo(.28125f).Within(.00001f));
        aircraft.Speed = 0;
        Assert.That(FighterAirCombat.FlareEvasion(aircraft, combat, TimeSpan.FromSeconds(11)), Is.EqualTo(.140625f).Within(.00001f));
        combat.IncomingAt = combat.IncomingStartedAt;
        Assert.That(FighterAirCombat.FlareEvasion(aircraft, combat, TimeSpan.FromSeconds(10)), Is.Zero);
    }
}
