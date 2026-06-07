using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared.AU14.Threats;
using Content.Shared.AU14.util;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server.AU14.Threats;

public readonly record struct ThreatVoteBodyCount(int Leaders, int Members)
{
    public int Total => Leaders + Members;
}

public readonly record struct ThreatVoteAssignment(NetUserId Player, ProtoId<JobPrototype> Job);

public static class ThreatVoteSelection
{
    public static readonly ProtoId<JobPrototype> ThreatLeaderJobId = new("AU14JobThreatLeader");
    public static readonly ProtoId<JobPrototype> ThreatMemberJobId = new("AU14JobThreatMember");
    public const string GenericThreatDisplayNameLocId = "au14-threat-vote-option-generic";

    public static ThreatVoteBodyCount CalculateBodyCount(
        IReadOnlyDictionary<string, int> leaders,
        IReadOnlyDictionary<string, int> members,
        IReadOnlyDictionary<string, JobScaleEntry> scaling,
        int playerCount)
    {
        var leaderCount = CalculateEntries(leaders, scaling, playerCount);
        var memberCount = CalculateEntries(members, scaling, playerCount);
        return new ThreatVoteBodyCount(leaderCount, memberCount);
    }

    public static ThreatVoteBodyCount CalculateBodyCount(PartySpawnPrototype spawn, int playerCount)
    {
        return CalculateBodyCount(spawn.LeadersToSpawn, spawn.GruntsToSpawn, spawn.Scaling, playerCount);
    }

    public static bool IsThreatAllowed(
        IReadOnlyCollection<string> blacklistedGamemodes,
        IReadOnlyCollection<string> whitelistedGamemodes,
        int minPlayers,
        int maxPlayers,
        IReadOnlyCollection<string> blacklistedPlatoons,
        IReadOnlyCollection<string> whitelistedPlatoons,
        string preset,
        string? govforId,
        string? opforId,
        int playerCount)
    {
        if (ContainsIgnoreCase(blacklistedGamemodes, preset))
            return false;

        if (whitelistedGamemodes.Count > 0 && !ContainsIgnoreCase(whitelistedGamemodes, preset))
            return false;

        if (minPlayers > playerCount)
            return false;

        if (maxPlayers > 0 && maxPlayers < playerCount)
            return false;

        if (govforId != null && ContainsIgnoreCase(blacklistedPlatoons, govforId))
            return false;

        if (opforId != null && ContainsIgnoreCase(blacklistedPlatoons, opforId))
            return false;

        if (whitelistedPlatoons.Count > 0 &&
            ((govforId != null && !ContainsIgnoreCase(whitelistedPlatoons, govforId)) ||
             (opforId != null && !ContainsIgnoreCase(whitelistedPlatoons, opforId))))
        {
            return false;
        }

        return true;
    }

    public static bool IsThreatAllowed(
        ThreatPrototype threat,
        string preset,
        string? govforId,
        string? opforId,
        int playerCount)
    {
        return IsThreatAllowed(
            threat.BlacklistedGamemodes,
            threat.whitelistedgamemodes,
            threat.MinPlayers,
            threat.MaxPlayers,
            threat.BlacklistedPlatoons,
            threat.WhitelistedPlatoons,
            preset,
            govforId,
            opforId,
            playerCount);
    }

    public static bool CanEnterThreatVotePool(
        HumanoidCharacterProfile profile,
        string? presetId,
        IEnumerable<ProtoId<ThreatPrototype>> candidateThreatIds)
    {
        return CanEnterThreatVotePoolForJob(profile, presetId, candidateThreatIds, ThreatLeaderJobId) ||
               CanEnterThreatVotePoolForJob(profile, presetId, candidateThreatIds, ThreatMemberJobId);
    }

    public static bool CanEnterThreatVotePoolForJob(
        HumanoidCharacterProfile profile,
        string? presetId,
        IEnumerable<ProtoId<ThreatPrototype>> candidateThreatIds,
        ProtoId<JobPrototype> threatJobId)
    {
        var priority = GetThreatJobPriority(profile, presetId, threatJobId);
        if (priority == JobPriority.Never)
            return false;

        var threatPreferences = profile.GetThreatPreferencesForGamemode(presetId);
        if (threatPreferences.Count == 0)
            return true;

        var candidates = candidateThreatIds
            .Select(id => id.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return threatPreferences.Any(preference => candidates.Contains(preference.Id));
    }

    private static JobPriority GetThreatJobPriority(
        HumanoidCharacterProfile profile,
        string? presetId,
        ProtoId<JobPrototype> threatJobId)
    {
        var priorities = profile.GetJobPrioritiesForGamemode(presetId);
        return priorities.TryGetValue(threatJobId, out var priority)
            ? priority
            : JobPriority.Never;
    }

    public static List<ThreatVoteAssignment> BuildHeldAssignments(
        IReadOnlyList<NetUserId> shuffledPlayers,
        IReadOnlyDictionary<NetUserId, HumanoidCharacterProfile> profiles,
        IReadOnlyList<ProtoId<ThreatPrototype>> candidateThreatIds,
        int leaderSlots,
        int memberSlots,
        string? presetId)
    {
        var assignments = new List<ThreatVoteAssignment>(Math.Max(0, leaderSlots) + Math.Max(0, memberSlots));
        var assigned = new HashSet<NetUserId>();

        AssignJob(ThreatLeaderJobId, leaderSlots);
        AssignJob(ThreatMemberJobId, memberSlots);

        return assignments;

        void AssignJob(ProtoId<JobPrototype> jobId, int slots)
        {
            if (slots <= 0)
                return;

            var candidates = shuffledPlayers
                .Select((player, index) => (player, index))
                .Where(candidate =>
                    !assigned.Contains(candidate.player) &&
                    profiles.TryGetValue(candidate.player, out var profile) &&
                    CanEnterThreatVotePoolForJob(profile, presetId, candidateThreatIds, jobId))
                .Select(candidate =>
                {
                    var profile = profiles[candidate.player];
                    return (candidate.player, candidate.index, priority: GetThreatJobPriority(profile, presetId, jobId));
                })
                .OrderByDescending(candidate => candidate.priority)
                .ThenBy(candidate => candidate.index)
                .Take(slots);

            foreach (var candidate in candidates)
            {
                assignments.Add(new ThreatVoteAssignment(candidate.player, jobId));
                assigned.Add(candidate.player);
            }
        }
    }

    public static List<ThreatVoteAssignment> BuildHeldAssignments(
        IReadOnlyList<NetUserId> shuffledEligiblePlayers,
        int heldCount)
    {
        var assignments = new List<ThreatVoteAssignment>(Math.Min(heldCount, shuffledEligiblePlayers.Count));
        for (var i = 0; i < heldCount && i < shuffledEligiblePlayers.Count; i++)
        {
            assignments.Add(new ThreatVoteAssignment(shuffledEligiblePlayers[i], ThreatMemberJobId));
        }

        return assignments;
    }

    public static List<ThreatVoteAssignment> BuildSpawnAssignments(
        IReadOnlyList<NetUserId> shuffledHeldPlayers,
        int leaderBodies,
        int memberBodies)
    {
        var heldAssignments = shuffledHeldPlayers
            .Select(player => new ThreatVoteAssignment(player, ThreatMemberJobId))
            .ToList();

        return BuildSpawnAssignments(heldAssignments, leaderBodies, memberBodies);
    }

    public static List<ThreatVoteAssignment> BuildSpawnAssignments(
        IReadOnlyList<ThreatVoteAssignment> shuffledHeldAssignments,
        int leaderBodies,
        int memberBodies)
    {
        var totalBodies = Math.Max(0, leaderBodies) + Math.Max(0, memberBodies);
        var assignments = new List<ThreatVoteAssignment>(Math.Min(totalBodies, shuffledHeldAssignments.Count));
        var assigned = new HashSet<NetUserId>();
        var assignedLeaders = 0;
        var assignedMembers = 0;

        foreach (var held in shuffledHeldAssignments)
        {
            if (assignedLeaders >= leaderBodies)
                break;

            if (held.Job != ThreatLeaderJobId)
                continue;

            assignments.Add(new ThreatVoteAssignment(held.Player, ThreatLeaderJobId));
            assigned.Add(held.Player);
            assignedLeaders++;
        }

        foreach (var held in shuffledHeldAssignments)
        {
            if (assignedMembers >= memberBodies)
                break;

            if (!assigned.Add(held.Player))
                continue;

            assignments.Add(new ThreatVoteAssignment(held.Player, ThreatMemberJobId));
            assignedMembers++;
        }

        return assignments;
    }

    public static string GetThreatDisplayName(string threatId)
    {
        if (string.IsNullOrWhiteSpace(threatId))
            return "Threat";

        if (threatId.Contains("cultist", StringComparison.OrdinalIgnoreCase))
            return "Cultist + Xeno Threat";

        if (threatId.Contains("tribal", StringComparison.OrdinalIgnoreCase))
            return "Tribal Threat";

        var normalized = threatId.Trim();
        normalized = RemoveSuffix(normalized, "Distress");
        normalized = RemoveSuffix(normalized, "OnMarker");
        normalized = RemoveSuffix(normalized, "CF");

        var hasThreatSuffix = normalized.EndsWith("Threat", StringComparison.OrdinalIgnoreCase);
        if (hasThreatSuffix)
            normalized = normalized[..^"Threat".Length];

        var words = SplitIdentifierWords(normalized)
            .Select(ToTitleWord)
            .ToList();

        if (hasThreatSuffix || words.Count == 0)
            words.Add("Threat");

        return string.Join(" ", words);
    }

    public static string GetThreatDisplayNameLocId(string threatId)
    {
        if (string.IsNullOrWhiteSpace(threatId))
            return GenericThreatDisplayNameLocId;

        if (threatId.Contains("cultist", StringComparison.OrdinalIgnoreCase))
            return "au14-threat-vote-option-cultist-xeno";

        if (threatId.Contains("tribal", StringComparison.OrdinalIgnoreCase))
            return "au14-threat-vote-option-tribal";

        if (threatId.Contains("abomination", StringComparison.OrdinalIgnoreCase))
            return "au14-threat-vote-option-abominations";

        if (threatId.Contains("xeno", StringComparison.OrdinalIgnoreCase))
            return "au14-threat-vote-option-xeno";

        if (threatId.Contains("ape", StringComparison.OrdinalIgnoreCase))
            return "au14-threat-vote-option-ape";

        if (threatId.Contains("wendigo", StringComparison.OrdinalIgnoreCase))
            return "au14-threat-vote-option-wendigo";

        return GenericThreatDisplayNameLocId;
    }

    private static int CalculateEntries(
        IReadOnlyDictionary<string, int> entries,
        IReadOnlyDictionary<string, JobScaleEntry> scaling,
        int playerCount)
    {
        var count = 0;
        foreach (var (protoId, staticCount) in entries)
        {
            count += scaling.TryGetValue(protoId, out var entry)
                ? JobScaling.CalculateScaledSlots(playerCount, staticCount, entry)
                : staticCount;
        }

        return count;
    }

    private static bool ContainsIgnoreCase(IEnumerable<string> values, string value)
    {
        return values.Any(candidate => candidate.Equals(value, StringComparison.OrdinalIgnoreCase));
    }

    private static string RemoveSuffix(string value, string suffix)
    {
        return value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? value[..^suffix.Length]
            : value;
    }

    private static IEnumerable<string> SplitIdentifierWords(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            yield break;

        var start = 0;
        for (var i = 1; i < value.Length; i++)
        {
            if (!char.IsUpper(value[i]) || !char.IsLower(value[i - 1]))
                continue;

            yield return value[start..i];
            start = i;
        }

        yield return value[start..];
    }

    private static string ToTitleWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return word;

        return word.Length == 1
            ? word.ToUpperInvariant()
            : $"{char.ToUpperInvariant(word[0])}{word[1..].ToLowerInvariant()}";
    }
}
