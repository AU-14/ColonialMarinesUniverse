namespace Content.Server.CMU14.Yautja;

// which gamemodes can roll a random hunt. everything else never gets one unless staff force it
public static class YautjaHuntRoll
{
    public const string InsurgencyPreset = "Insurgency";
    public const string DistressSignalPreset = "DistressSignal";
    public const string ForceOnForcePreset = "ForceOnForce";

    public static float ChanceFor(string? presetId, float insurgencyChance, float distressSignalChance)
    {
        if (string.Equals(presetId, InsurgencyPreset, StringComparison.OrdinalIgnoreCase))
            return Math.Clamp(insurgencyChance, 0f, 1f);

        // FOF rolls on the same odds as DS
        if (string.Equals(presetId, DistressSignalPreset, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(presetId, ForceOnForcePreset, StringComparison.OrdinalIgnoreCase))
        {
            return Math.Clamp(distressSignalChance, 0f, 1f);
        }

        return 0f;
    }
}
