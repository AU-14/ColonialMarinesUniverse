using Content.Shared._RMC14.Intel;

namespace Content.Shared._CMU14.Intel;

internal static class CMUFactionTech
{
    public static bool TryNormalizeFaction(string? team, out string faction)
    {
        faction = team?.Trim().ToLowerInvariant() ?? Team.None;
        return faction is Team.GovFor or Team.OpFor or Team.CLF;
    }
}
