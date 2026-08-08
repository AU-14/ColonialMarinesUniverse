using System.Linq;
using Content.Server.Voting;
using Content.Shared._RMC14.Rules;
using Content.Shared._CMU14.Threats;
using Content.Shared.AU14.util;

namespace Content.Server.AU14.Round;

internal static class AuRoundSelectionRules
{
    public static AuRoundVoteBranch GetRequiredFactionBranches(
        bool requiresGovforSelection,
        bool requiresOpforSelection)
    {
        var required = AuRoundVoteBranch.None;
        if (requiresGovforSelection)
            required |= AuRoundVoteBranch.Govfor;
        if (requiresOpforSelection)
            required |= AuRoundVoteBranch.Opfor;

        return required;
    }

    public static AuRoundVoteBranch GetActiveFactionBranches(
        bool requiresGovforSelection,
        bool requiresOpforSelection,
        bool usesGovforPlatoon,
        bool usesOpforPlatoon)
    {
        return GetRequiredFactionBranches(
            requiresGovforSelection || usesGovforPlatoon,
            requiresOpforSelection || usesOpforPlatoon);
    }

    public static string? SelectCandidate(
        IReadOnlyList<string> candidates,
        string? selected,
        string? preferred = null)
    {
        if (TryGetCandidate(candidates, selected, out var match))
            return match;

        if (TryGetCandidate(candidates, preferred, out match))
            return match;

        return candidates.Count > 0 ? candidates[0] : null;
    }

    public static bool IsPlayerCountAllowed(int playerCount, int minimum, int maximum)
    {
        return (minimum <= 0 || playerCount >= minimum) &&
               (maximum <= 0 || playerCount <= maximum);
    }

    public static bool IsExplicitlyWhitelistedForGamemode(ThirdPartyPrototype proto, string gamemode)
    {
        return ContainsIgnoreCase(proto.whitelistedgamemodes, gamemode);
    }

    public static bool IsThirdPartyAllowed(
        ThirdPartyPrototype proto,
        string currentGamemode,
        string? currentThreat,
        string? govforPlatoon,
        string? opforPlatoon,
        int playerCount)
    {
        if (ContainsIgnoreCase(proto.BlacklistedGamemodes, currentGamemode))
            return false;

        if (proto.whitelistedgamemodes.Count > 0 &&
            !ContainsIgnoreCase(proto.whitelistedgamemodes, currentGamemode))
            return false;

        if (proto.MaxPlayers < playerCount || proto.MinPlayers > playerCount)
            return false;

        if (currentThreat != null && ContainsIgnoreCase(proto.BlacklistedThreats, currentThreat))
            return false;

        if (proto.WhitelistedThreats.Count > 0 &&
            (currentThreat == null || !ContainsIgnoreCase(proto.WhitelistedThreats, currentThreat)))
            return false;

        if (govforPlatoon != null && ContainsIgnoreCase(proto.BlacklistedPlatoons, govforPlatoon))
            return false;

        if (opforPlatoon != null && ContainsIgnoreCase(proto.BlacklistedPlatoons, opforPlatoon))
            return false;

        if (proto.WhitelistedPlatoons.Any() &&
            ((govforPlatoon != null && !ContainsIgnoreCase(proto.WhitelistedPlatoons, govforPlatoon)) ||
             (opforPlatoon != null && !ContainsIgnoreCase(proto.WhitelistedPlatoons, opforPlatoon))))
            return false;

        return true;
    }

    public static VoteOptions BuildPlanetVoteOptions(
        string presetId,
        IReadOnlyList<RMCPlanetMapPrototypeComponent> planets,
        TimeSpan duration)
    {
        var options = new List<(string text, object data)>();
        foreach (var planet in planets)
        {
            var displayName = string.IsNullOrWhiteSpace(planet.VoteName)
                ? planet.MapId
                : planet.VoteName;
            options.Add((displayName, planet.MapId));
        }

        return new VoteOptions
        {
            Title = "Select Planet",
            Options = options,
            Duration = duration,
            CarryoverEnabled = true,
            CarryoverKey = BuildPlanetVoteCarryoverKey(presetId, planets),
        };
    }

    public static VoteOptions BuildPlatoonVoteOptions(
        string faction,
        string presetId,
        string planetId,
        IReadOnlyList<PlatoonPrototype> platoons,
        TimeSpan duration)
    {
        var options = platoons
            .Select(platoon => (platoon.Name, (object) platoon))
            .ToList();

        return new VoteOptions
        {
            Title = $"{faction} Vote",
            Options = options,
            Duration = duration,
            CarryoverEnabled = true,
            CarryoverKey = BuildContextVoteCarryoverKey(
                $"platoon:{faction.ToLowerInvariant()}",
                presetId,
                planetId,
                platoons.Select(platoon => platoon.ID)),
        };
    }

    public static VoteOptions BuildShipVoteOptions(
        string faction,
        string presetId,
        string planetId,
        PlatoonPrototype platoon,
        IReadOnlyList<string> ships,
        TimeSpan duration)
    {
        var options = ships
            .Select(ship => (ship, (object) ship))
            .ToList();

        return new VoteOptions
        {
            Title = $"{faction} Ship Vote",
            Options = options,
            Duration = duration,
            CarryoverEnabled = true,
            CarryoverKey = BuildContextVoteCarryoverKey(
                $"ship:{faction.ToLowerInvariant()}:{platoon.ID}",
                presetId,
                planetId,
                ships),
        };
    }

    private static string BuildPlanetVoteCarryoverKey(
        string presetId,
        IEnumerable<RMCPlanetMapPrototypeComponent> planets)
    {
        var mapIds = planets
            .Select(planet => planet.MapId)
            .Order(StringComparer.OrdinalIgnoreCase);

        return $"au14-planet:{presetId}:{string.Join(",", mapIds)}";
    }

    private static string BuildContextVoteCarryoverKey(
        string voteType,
        string presetId,
        string planetId,
        IEnumerable<string> optionIds)
    {
        var options = optionIds.Order(StringComparer.OrdinalIgnoreCase);
        return $"au14-{voteType}:{presetId}:{planetId}:{string.Join(",", options)}";
    }

    private static bool ContainsIgnoreCase(IEnumerable<string> values, string value)
    {
        return values.Any(candidate => candidate.Equals(value, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryGetCandidate(
        IReadOnlyList<string> candidates,
        string? requested,
        out string? match)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            foreach (var candidate in candidates)
            {
                if (!candidate.Equals(requested, StringComparison.OrdinalIgnoreCase))
                    continue;

                match = candidate;
                return true;
            }
        }

        match = null;
        return false;
    }
}
