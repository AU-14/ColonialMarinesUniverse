using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Content.Server.CMU14.Threats;
using Content.Server.CMU14.Round;
using Content.Server.GameTicking;
using Content.Shared.Body;
using Content.Shared._RMC14.Rules;
using Content.Shared.CMU14.Threats;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using NUnit.Framework;
using Robust.Shared.IoC;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Tests.Server.CMU14.Round;

[TestFixture]
[NonParallelizable]
public sealed class AuJobSelectionTest : ContentUnitTest
{
    protected override Type[] ExtraComponents => [typeof(InitialBodyComponent)];

    private const string ProfilePrototypes = """
        - type: entity
          id: TestHuman

        - type: skinColoration
          id: HumanToned
          strategy: !type:HumanTonedSkinColoration {}

        - type: species
          id: Human
          name: test-species-human
          roundStart: true
          prototype: TestHuman
          dollPrototype: TestHuman
          skinColoration: HumanToned
        """;

    [OneTimeSetUp]
    public void InitializeProfilePrototypes()
    {
        IoCManager.Resolve<ISerializationManager>().Initialize();

        var prototypeManager = IoCManager.Resolve<IPrototypeManager>();
        prototypeManager.Initialize();
        prototypeManager.LoadString(ProfilePrototypes);
        prototypeManager.ResolveResults();
    }

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
        var vote = new ThreatVoteSystem();

        vote.BlockRoundJoinsForHeldPlayers([held]);

        Assert.That(vote.IsRoundJoinBlocked(held), Is.True);
        Assert.That(vote.IsRoundJoinBlocked(other), Is.False);

        vote.ClearRoundJoinBlocks();

        Assert.That(vote.IsRoundJoinBlocked(held), Is.False);
    }

    [Test]
    public void ThirdPartyBodyBudgetUsesRatioAndThreatBodyCap()
    {
        Assert.That(
            AuRoundSystem.CalculateThirdPartyBodyBudget(35, 0.15f),
            Is.EqualTo(5));
        Assert.That(
            AuRoundSystem.CalculateThirdPartyBodyBudget(35, 0.15f, new ThreatVoteBodyCount(1, 4)),
            Is.EqualTo(5));
        Assert.That(
            AuRoundSystem.CalculateThirdPartyBodyBudget(35, 0.15f, new ThreatVoteBodyCount(1, 2)),
            Is.EqualTo(3));
    }

    [Test]
    public void ThirdPartySelectionSkipsPartiesThatDoNotFitRemainingBodyBudget()
    {
        var large = CreateThirdPartyPrototype("Large");
        var medium = CreateThirdPartyPrototype("Medium");
        var small = CreateThirdPartyPrototype("Small");
        var bodyCounts = new Dictionary<ThirdPartyPrototype, int>
        {
            [large] = 8,
            [medium] = 4,
            [small] = 2,
        };

        var selected = AuRoundSystem.SelectThirdPartiesWithinBodyBudget(
            [large, medium, small],
            maxThirdParties: 7,
            bodyBudget: 5,
            PickFirst,
            party => bodyCounts[party],
            out var selectedBodies);

        Assert.That(selected, Is.EqualTo(new List<ThirdPartyPrototype> { medium }));
        Assert.That(selectedBodies, Is.EqualTo(4));
        return;

        static ThirdPartyPrototype PickFirst(IReadOnlyList<ThirdPartyPrototype> candidates)
            => candidates[0];
    }

    [Test]
    public void ThirdPartySelectionDoesNotSelectDuplicatePrototypeIds()
    {
        var duplicate = CreateThirdPartyPrototype("Duplicate");
        var duplicateWithDifferentCase = CreateThirdPartyPrototype("duplicate");
        var other = CreateThirdPartyPrototype("Other");

        var selected = AuRoundSystem.SelectThirdPartiesWithinBodyBudget(
            [duplicate, duplicateWithDifferentCase, other],
            maxThirdParties: 3,
            bodyBudget: 3,
            PickFirst,
            _ => 1,
            out var selectedBodies);

        Assert.That(selected, Is.EqualTo(new[] { duplicate, other }));
        Assert.That(selectedBodies, Is.EqualTo(2));
        return;

        static ThirdPartyPrototype PickFirst(IReadOnlyList<ThirdPartyPrototype> candidates)
            => candidates[0];
    }

    [Test]
    public void DistressSignalThirdPartyFillUsesRemainingThreatCapacity()
    {
        var lockedLarge = CreateThirdPartyPrototype("LockedLarge");
        var lockedSmall = CreateThirdPartyPrototype("LockedSmall");
        var tooLarge = CreateThirdPartyPrototype("TooLarge");
        var fits = CreateThirdPartyPrototype("Fits");
        var tiny = CreateThirdPartyPrototype("Tiny");
        var bodyCounts = new Dictionary<ThirdPartyPrototype, int>
        {
            [lockedLarge] = 4,
            [lockedSmall] = 2,
            [tooLarge] = 4,
            [fits] = 2,
            [tiny] = 1,
        };

        var additional = AuRoundSystem.SelectAdditionalDistressSignalThirdParties(
            [lockedLarge, lockedSmall, tooLarge, fits, tiny],
            [lockedLarge, lockedSmall],
            maxThirdParties: 3,
            bodyBudget: 9,
            PickFirst,
            party => bodyCounts[party],
            out var lockedBodies,
            out var additionalBodies);

        Assert.That(additional, Is.EqualTo(new[] { fits }));
        Assert.That(lockedBodies, Is.EqualTo(6));
        Assert.That(additionalBodies, Is.EqualTo(2));
        return;

        static ThirdPartyPrototype PickFirst(IReadOnlyList<ThirdPartyPrototype> candidates)
            => candidates[0];
    }

    [Test]
    public void DistressSignalThirdPartyFillPreservesAnnouncedSurvivorCount()
    {
        var lockedSurvivor = CreateThirdPartyPrototype(
            "LockedSurvivor",
            roundStart: true,
            announceAsSurvivors: true);
        var duplicateLockedParty = CreateThirdPartyPrototype("LockedSurvivor");
        var additionalSurvivor = CreateThirdPartyPrototype(
            "AdditionalSurvivor",
            roundStart: true,
            announceAsSurvivors: true);
        var delayedSurvivor = CreateThirdPartyPrototype(
            "DelayedSurvivor",
            announceAsSurvivors: true);
        var unrelatedRoundStart = CreateThirdPartyPrototype("UnrelatedRoundStart", roundStart: true);
        var bodyCounts = new Dictionary<ThirdPartyPrototype, int>
        {
            [lockedSurvivor] = 3,
            [duplicateLockedParty] = 3,
            [additionalSurvivor] = 2,
            [delayedSurvivor] = 2,
            [unrelatedRoundStart] = 1,
        };

        var additional = AuRoundSystem.SelectAdditionalDistressSignalThirdParties(
            [duplicateLockedParty, additionalSurvivor, delayedSurvivor, unrelatedRoundStart],
            [lockedSurvivor],
            maxThirdParties: 3,
            bodyBudget: 10,
            PickFirst,
            party => bodyCounts[party],
            out var lockedBodies,
            out var additionalBodies);

        Assert.That(additional, Is.EqualTo(new[] { delayedSurvivor, unrelatedRoundStart }));
        Assert.That(lockedBodies, Is.EqualTo(3));
        Assert.That(additionalBodies, Is.EqualTo(3));
        Assert.That(
            AuRoundSystem.CalculateAnnouncedSurvivorCount(
                new[] { lockedSurvivor }.Concat(additional),
                party => bodyCounts[party]),
            Is.EqualTo(3));
        return;

        static ThirdPartyPrototype PickFirst(IReadOnlyList<ThirdPartyPrototype> candidates)
            => candidates[0];
    }

    [Test]
    public void DistressSignalThirdPartyLimitsUseMostRestrictiveThreat()
    {
        var limits = AuRoundSystem.GetConservativeThirdPartyLimits(
        [
            (MaxThirdParties: 7, BodyBudget: 8),
            (MaxThirdParties: 3, BodyBudget: 10),
            (MaxThirdParties: 5, BodyBudget: 4),
        ]);

        Assert.That(limits.MaxThirdParties, Is.EqualTo(3));
        Assert.That(limits.BodyBudget, Is.EqualTo(4));
        Assert.That(AuRoundSystem.GetConservativeThirdPartyLimits([]), Is.EqualTo(default((int, int))));
    }

    [Test]
    public void SurvivorCountIncludesOnlyApprovedPartyFamilies()
    {
        var approvedRoundStart = CreateThirdPartyPrototype(roundStart: true, announceAsSurvivors: true);
        var approvedDelayed = CreateThirdPartyPrototype(announceAsSurvivors: true);
        var unrelated = CreateThirdPartyPrototype(roundStart: true);
        var bodyCounts = new Dictionary<ThirdPartyPrototype, int>
        {
            [approvedRoundStart] = 3,
            [approvedDelayed] = 2,
            [unrelated] = 8,
        };

        var count = AuRoundSystem.CalculateAnnouncedSurvivorCount(
            [approvedRoundStart, approvedDelayed, unrelated],
            party => bodyCounts[party]);

        Assert.That(count, Is.EqualTo(3));
    }

    [Test]
    public void ResettingDistressSignalLockPreservesOrdinaryThirdPartySelection()
    {
        var state = new AuRoundSelectionState();
        var ordinaryParty = CreateThirdPartyPrototype();
        state.SelectedThirdParties.Add(ordinaryParty);

        state.ResetDistressSignalThirdPartyLock();

        Assert.That(state.SelectedThirdParties, Is.EqualTo(new[] { ordinaryParty }));

        state.DistressSignalThirdPartiesLocked = true;
        state.DistressSignalThirdPartyFillCompleted = true;
        state.DistressSignalSurvivorCount = 3;
        state.ResetDistressSignalThirdPartyLock();

        Assert.That(state.SelectedThirdParties, Is.Empty);
        Assert.That(state.DistressSignalThirdPartiesLocked, Is.False);
        Assert.That(state.DistressSignalThirdPartyFillCompleted, Is.False);
        Assert.That(state.DistressSignalSurvivorCount, Is.Zero);
    }

    [Test]
    public void DistressSignalSurvivorAnnouncementTriggersOnceAtOneMinute()
    {
        var roundStart = TimeSpan.FromMinutes(10);

        Assert.That(
            GameTicker.IsDistressSignalSurvivorAnnouncementDue(
                true,
                GameRunLevel.PreRoundLobby,
                false,
                false,
                roundStart,
                roundStart - TimeSpan.FromSeconds(61)),
            Is.False);
        Assert.That(
            GameTicker.IsDistressSignalSurvivorAnnouncementDue(
                true,
                GameRunLevel.PreRoundLobby,
                false,
                false,
                roundStart,
                roundStart - TimeSpan.FromSeconds(60)),
            Is.True);
        Assert.That(
            GameTicker.IsDistressSignalSurvivorAnnouncementDue(
                true,
                GameRunLevel.PreRoundLobby,
                false,
                false,
                roundStart,
                roundStart - TimeSpan.FromSeconds(59.5)),
            Is.True);
        Assert.That(
            GameTicker.IsDistressSignalSurvivorAnnouncementDue(
                true,
                GameRunLevel.PreRoundLobby,
                false,
                true,
                roundStart,
                roundStart - TimeSpan.FromSeconds(30)),
            Is.False);
        Assert.That(
            GameTicker.IsDistressSignalSurvivorAnnouncementDue(
                true,
                GameRunLevel.PreRoundLobby,
                true,
                false,
                roundStart,
                roundStart - TimeSpan.FromSeconds(30)),
            Is.False);
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

    private static ThirdPartyPrototype CreateThirdPartyPrototype(
        string id = "TestThirdParty",
        bool roundStart = false,
        bool announceAsSurvivors = false)
    {
        var party = (ThirdPartyPrototype) RuntimeHelpers.GetUninitializedObject(typeof(ThirdPartyPrototype));
        typeof(ThirdPartyPrototype)
            .GetField($"<{nameof(ThirdPartyPrototype.ID)}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(party, id);
        typeof(ThirdPartyPrototype)
            .GetField($"<{nameof(ThirdPartyPrototype.RoundStart)}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(party, roundStart);
        typeof(ThirdPartyPrototype)
            .GetField($"<{nameof(ThirdPartyPrototype.AnnounceAsSurvivors)}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(party, announceAsSurvivors);
        return party;
    }
}
