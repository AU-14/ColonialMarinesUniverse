using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Content.Server.AU14.Round;
using Content.Server.AU14.Threats;
using Content.Shared._RMC14.Rules;
using Content.Shared.AU14.Threats;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using NUnit.Framework;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Tests.Server.AU14.Round;

[TestFixture]
public sealed class AuJobSelectionTest
{
    [Test]
    public void ThreatJobEligibilityAllowsEmptyThreatPreferenceList()
    {
        var threatMember = new ProtoId<JobPrototype>("AU14JobThreatMember");
        var xeno = new ProtoId<ThreatPrototype>("XenoThreat");

        var profile = HumanoidCharacterProfile.DefaultWithSpecies()
            .WithGamemodeJobPriority("DistressSignal", threatMember, JobPriority.High);

        Assert.That(
            AuJobSelectionSystem.CanAssignThreatJob(profile, "DistressSignal", threatMember, xeno),
            Is.True);
    }

    [Test]
    public void ThreatJobEligibilityRequiresSelectedThreatPreference()
    {
        var threatMember = new ProtoId<JobPrototype>("AU14JobThreatMember");
        var abomination = new ProtoId<ThreatPrototype>("abominationsThreat");
        var xeno = new ProtoId<ThreatPrototype>("XenoThreat");

        var profile = HumanoidCharacterProfile.DefaultWithSpecies()
            .WithGamemodeJobPriority("DistressSignal", threatMember, JobPriority.High)
            .WithGamemodeThreatPreference("DistressSignal", abomination, false)
            .WithGamemodeThreatPreference("DistressSignal", xeno, true);

        Assert.That(
            AuJobSelectionSystem.CanAssignThreatJob(profile, "DistressSignal", threatMember, abomination),
            Is.False);

        Assert.That(
            AuJobSelectionSystem.CanAssignThreatJob(profile, "DistressSignal", threatMember, xeno),
            Is.True);
    }

    [Test]
    public void ThreatVoteSpawnAssignmentsDoNotPromoteMembersToLeaderSlots()
    {
        var memberOnly = new NetUserId(new Guid("00000000-0000-0000-0000-000000000001"));
        var leader = new NetUserId(new Guid("00000000-0000-0000-0000-000000000002"));
        var heldAssignments = new List<ThreatVoteAssignment>
        {
            new(memberOnly, ThreatVoteSelection.ThreatMemberJobId),
            new(leader, ThreatVoteSelection.ThreatLeaderJobId),
        };

        var assignments = ThreatVoteSelection.BuildSpawnAssignments(heldAssignments, leaderBodies: 1, memberBodies: 1);

        Assert.That(
            assignments,
            Has.Some.EqualTo(new ThreatVoteAssignment(leader, ThreatVoteSelection.ThreatLeaderJobId)));
        Assert.That(
            assignments,
            Has.Some.EqualTo(new ThreatVoteAssignment(memberOnly, ThreatVoteSelection.ThreatMemberJobId)));
        Assert.That(
            assignments,
            Has.None.EqualTo(new ThreatVoteAssignment(memberOnly, ThreatVoteSelection.ThreatLeaderJobId)));
    }

    [Test]
    public void ThreatVoteHeldAssignmentsReserveLeaderSlotsBeforeMembers()
    {
        var member = new NetUserId(new Guid("00000000-0000-0000-0000-000000000001"));
        var leader = new NetUserId(new Guid("00000000-0000-0000-0000-000000000002"));
        var candidateThreats = new List<ProtoId<ThreatPrototype>>
        {
            new("XenoThreat"),
        };
        var shuffledPlayers = new List<NetUserId>
        {
            member,
            leader,
        };
        var profiles = new Dictionary<NetUserId, HumanoidCharacterProfile>
        {
            [member] = HumanoidCharacterProfile.DefaultWithSpecies()
                .WithGamemodeJobPriority("DistressSignal", ThreatVoteSelection.ThreatMemberJobId, JobPriority.High),
            [leader] = HumanoidCharacterProfile.DefaultWithSpecies()
                .WithGamemodeJobPriority("DistressSignal", ThreatVoteSelection.ThreatLeaderJobId, JobPriority.High),
        };

        var assignments = ThreatVoteSelection.BuildHeldAssignments(
            shuffledPlayers,
            profiles,
            candidateThreats,
            leaderSlots: 1,
            memberSlots: 1,
            presetId: "DistressSignal");

        Assert.That(assignments, Is.EqualTo(new List<ThreatVoteAssignment>
        {
            new(leader, ThreatVoteSelection.ThreatLeaderJobId),
            new(member, ThreatVoteSelection.ThreatMemberJobId),
        }));
    }

    [Test]
    public void ThreatVoteRoundJoinBlockTracksHeldPlayersUntilCleared()
    {
        var held = new NetUserId(new Guid("00000000-0000-0000-0000-000000000001"));
        var other = new NetUserId(new Guid("00000000-0000-0000-0000-000000000002"));
        var vote = new AuThreatVoteSystem();

        vote.BlockRoundJoinsForHeldPlayers([held]);

        Assert.That(vote.IsRoundJoinBlocked(held), Is.True);
        Assert.That(vote.IsRoundJoinBlocked(other), Is.False);

        vote.ClearRoundJoinBlocks();

        Assert.That(vote.IsRoundJoinBlocked(held), Is.False);
    }

    [Test]
    public void PlanetVoteOptionsUseStableCarryoverKey()
    {
        var planets = new List<RMCPlanetMapPrototypeComponent>
        {
            CreatePlanet("FirstMap", "First Planet"),
            CreatePlanet("SecondMap", "Second Planet"),
        };

        var vote = AuRoundSystem.BuildPlanetVoteOptions("DistressSignal", planets, TimeSpan.FromSeconds(30));

        Assert.That(vote.CarryoverEnabled, Is.True);
        Assert.That(vote.CarryoverKey, Is.EqualTo("au14-planet:DistressSignal:FirstMap,SecondMap"));
        Assert.That(vote.Options.Select(option => option.text), Is.EqualTo(new[] { "First Planet", "Second Planet" }));
    }

    private static RMCPlanetMapPrototypeComponent CreatePlanet(string mapId, string voteName)
    {
        var planet = new RMCPlanetMapPrototypeComponent();
        typeof(RMCPlanetMapPrototypeComponent)
            .GetField(nameof(RMCPlanetMapPrototypeComponent.MapId), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(planet, mapId);
        typeof(RMCPlanetMapPrototypeComponent)
            .GetField(nameof(RMCPlanetMapPrototypeComponent.VoteName), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(planet, voteName);
        return planet;
    }
}
