#nullable enable

namespace Content.Shared.CMU.Round;

/// <summary>
/// Converts legacy platoon selections into the typed round-force model during migration.
/// </summary>
public static class LegacyPlatoonAssignmentAdapter
{
    /// <summary>
    /// Uses the selected platoon prototype ID as the canonical force ID.
    /// </summary>
    public static RoundForceAssignment? FromLegacySelection(
        RoundSide side,
        string? legacyPlatoonId,
        string? mainShipId)
    {
        if (string.IsNullOrWhiteSpace(legacyPlatoonId))
            return null;

        return new RoundForceAssignment(
            side,
            new RoundForceId(legacyPlatoonId),
            mainShipId);
    }
}
