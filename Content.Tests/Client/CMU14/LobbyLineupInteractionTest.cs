using System;
using System.Linq;
using System.Numerics;
using Content.Client.CMU14.Lobby;
using Content.Shared.CMU14.Lobby;
using NUnit.Framework;
using Robust.Shared.Network;

namespace Content.Tests.Client.CMU14;

[TestFixture]
public sealed class LobbyLineupInteractionTest
{
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(60)]
    [TestCase(256)]
    public void TargetsStayInTheReadyRosterAndNeverShootThemselves(int count)
    {
        var roster = Enumerable.Range(0, count).Select(_ => new NetUserId(Guid.NewGuid())).ToArray();
        foreach (var seed in new[] { 0, 123, int.MaxValue, int.MinValue })
        {
            var targets = LobbyLineupInteractions.SelectTargets(LobbyLineupEmote.SquadVolley, roster, roster, seed);
            Assert.That(targets, Is.EqualTo(LobbyLineupInteractions.SelectTargets(LobbyLineupEmote.SquadVolley, roster, roster, seed)));
            Assert.That(targets.Count, Is.EqualTo(count > 1 ? count : 0));
            for (var i = 0; i < targets.Count; i++)
            {
                Assert.That(targets[i], Is.Not.EqualTo(roster[i]));
                Assert.That(roster, Does.Contain(targets[i]));
            }
            Assert.That(LobbyLineupInteractions.SelectTargets(LobbyLineupEmote.Wave, roster, roster, seed), Is.Empty);
        }
        if (count > 2)
            Assert.That(Enumerable.Range(0, 40).Select(seed =>
                LobbyLineupInteractions.SelectTargets(LobbyLineupEmote.BurstFire, roster.Take(1).ToArray(), roster, seed)[0]).Distinct().Count(),
                Is.GreaterThan(1), "Repeated bursts should not always pick the same person.");
    }

    [Test]
    public void ExistingMovesChangeIncomingReactionsAndReducedMotionStaysStill()
    {
        Assert.That(LobbyLineupInteractionChoreography.Reaction(LobbyLineupEmote.BurstFire, LobbyLineupEmote.Dance, 1),
            Is.EqualTo(LobbyLineupReaction.Duck));
        Assert.That(LobbyLineupInteractionChoreography.Reaction(LobbyLineupEmote.BurstFire, null, 1),
            Is.EqualTo(LobbyLineupReaction.Tumble));
        Assert.That(LobbyLineupInteractionChoreography.Reaction(LobbyLineupEmote.BananaPeel, LobbyLineupEmote.Backflip, 0),
            Is.EqualTo(LobbyLineupReaction.Duck));
        Assert.That(LobbyLineupInteractionChoreography.Reaction(LobbyLineupEmote.BananaPeel, null, 0),
            Is.EqualTo(LobbyLineupReaction.Slip));
        foreach (var reaction in Enum.GetValues<LobbyLineupReaction>())
        for (var age = 0f; age < LobbyLineupInteractionChoreography.ReactionDuration; age += 0.07f)
        {
            var pose = LobbyLineupInteractionChoreography.Sample(reaction, age, true);
            Assert.That(pose.Offset, Is.EqualTo(Vector2.Zero));
            Assert.That(pose.Rotation, Is.Zero);
        }
    }

    [TestCase(1)]
    [TestCase(60)]
    [TestCase(256)]
    public void SupplyScrambleBringsEveryoneOnstageAndReturnsThemSafely(int count)
    {
        foreach (var size in new[] { new Vector2(640, 480), new Vector2(1920, 1080), new Vector2(3440, 1440) })
        for (var index = 0; index < count; index++)
        {
            var start = LobbyPartyChoreography.SupplyCrew(0, index, count, size, false, 72);
            var arrival = LobbyPartyChoreography.SupplyCrew(3, index, count, size, false, 72);
            var end = LobbyPartyChoreography.SupplyCrew(LobbyPartyChoreography.SupplyDuration, index, count, size, false, 72);
            Assert.That(start.Position.X, Is.LessThan(0));
            Assert.That(arrival.Position.X, Is.InRange(0, size.X));
            Assert.That(arrival.Position.Y, Is.InRange(0, size.Y));
            Assert.That(end.Position.X, Is.GreaterThan(size.X));
            Assert.That(LobbyPartyChoreography.SupplyCrew(2, index, count, size, true, 72),
                Is.EqualTo(LobbyPartyChoreography.SupplyCrew(21, index, count, size, true, 72)));
        }
        Assert.That(LobbyPartyChoreography.SupplyImpact(LobbyPartyChoreography.SupplyCrates - 1) + 2.2f,
            Is.LessThan(18), "All cargo reactions must finish before the conga exit.");
        Assert.That(LobbyPartyChoreography.SupplyDuration, Is.LessThan(LobbyPartyShowEvent.Cooldown));
    }

    [Test]
    public void AutomaticShowsCycleThroughAllThreeRoutines()
    {
        var next = LobbyPartyShow.Flyby;
        var visited = new System.Collections.Generic.HashSet<LobbyPartyShow>();
        for (var i = 0; i < 3; i++)
        {
            Assert.That(visited.Add(next), Is.True);
            next = LobbyPartySettings.Next(next);
        }
        Assert.That(visited, Does.Contain(LobbyPartyShow.SupplyScramble));
        Assert.That(next, Is.EqualTo(LobbyPartyShow.Flyby));
    }
}
