using System.Linq;
using Content.Server.Voting;
using Content.Shared._RMC14.Rules;
using Content.Shared._CMU14.Threats;

namespace Content.Server.AU14.Round;

internal static class AuRoundSelectionRules
{
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

    private static string BuildPlanetVoteCarryoverKey(
        string presetId,
        IEnumerable<RMCPlanetMapPrototypeComponent> planets)
    {
        var mapIds = planets
            .Select(planet => planet.MapId)
            .Order(StringComparer.OrdinalIgnoreCase);

        return $"au14-planet:{presetId}:{string.Join(",", mapIds)}";
    }

    private static bool ContainsIgnoreCase(IEnumerable<string> values, string value)
    {
        return values.Any(candidate => candidate.Equals(value, StringComparison.OrdinalIgnoreCase));
    }
}
