using System;
using System.Numerics;
using Content.Client.CMU14.Lobby;
using Content.Shared.CMU14.Lobby;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.CMU14;

[TestFixture]
public sealed class LobbyLineupChoreographyTest
{
    [Test]
    public void MovesStayInTheirSpaceAndRespectReducedMotion()
    {
        foreach (var emote in Enum.GetValues<LobbyLineupEmote>())
        {
            for (var phase = 0f; phase < LobbyLineupChoreography.Duration(emote); phase += 0.07f)
            {
                var pose = LobbyLineupChoreography.Sample(emote, phase, false);
                Assert.That(float.IsFinite(pose.Rotation), Is.True);
                Assert.That(Math.Abs(pose.Offset.X), Is.LessThanOrEqualTo(0.2f));
                Assert.That(Math.Abs(pose.Offset.Y), Is.LessThanOrEqualTo(0.23f));
                var reduced = LobbyLineupChoreography.Sample(emote, phase, true);
                Assert.That(reduced.Offset, Is.EqualTo(Vector2.Zero));
                Assert.That(reduced.Rotation, Is.Zero);
                Assert.That(reduced.Facing, Is.EqualTo(Direction.South));
            }
        }
    }

    [Test]
    public void TeamRoutinesHaveIntentionalTimingAndStayIndependent()
    {
        for (var member = 0; member < 60; member++)
        {
            var workout = LobbyLineupChoreography.TeamMember(LobbyLineupEmote.SquadWorkout, member);
            Assert.That(workout.Move, Is.EqualTo(LobbyLineupEmote.PushUps));
            Assert.That(workout.Delay, Is.Zero, "Workout reps must begin on the same beat.");
            var disco = LobbyLineupChoreography.TeamMember(LobbyLineupEmote.SquadDisco, member);
            Assert.That(disco.Delay, Is.Zero);
            var wave = LobbyLineupChoreography.TeamMember(LobbyLineupEmote.SquadWave, member);
            Assert.That(wave.Move, Is.EqualTo(LobbyLineupEmote.Wave));
            Assert.That(wave.Delay, Is.InRange(0f, 2.4f));
        }
        Assert.That(LobbyLineupChoreography.TeamMember(LobbyLineupEmote.SquadWave, 1).Delay,
            Is.GreaterThan(LobbyLineupChoreography.TeamMember(LobbyLineupEmote.SquadWave, 0).Delay));
        foreach (var emote in LobbyLineupChoreography.TeamMoves)
            Assert.That(LobbyLineupEmoteEvent.IsTeamEmote(emote), Is.True);
        foreach (var emote in LobbyLineupChoreography.SoloMoves)
            Assert.That(LobbyLineupEmoteEvent.IsTeamEmote(emote), Is.False);
    }
}
