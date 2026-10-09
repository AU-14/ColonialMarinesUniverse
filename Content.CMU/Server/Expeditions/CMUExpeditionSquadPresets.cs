namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    // Execution and completion use the same catalog, including repeatable mixed compositions.
    private static readonly Dictionary<string, string[]> SquadPresets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["regular"] = ["CMUExpeditionScavenger"],
        ["poor"] = ["CMUExpeditionScavengerPoor"],
        ["rich"] = ["CMUExpeditionScavengerRich"],
        ["scout"] = ["CMUExpeditionScavengerScout"],
        ["assault"] = ["CMUExpeditionScavengerAssault"],
        ["support"] = ["CMUExpeditionScavengerSupport"],
        ["marksman"] = ["CMUExpeditionScavengerMarksman"],
        ["rocketeer"] = ["CMUExpeditionScavengerRocketeer"],
        ["medic"] = ["CMUExpeditionScavengerMedic"],
        ["breacher"] = ["CMUExpeditionScavengerBreacher"],
        ["skirmisher"] = ["CMUExpeditionScavengerSkirmisher"],
        ["machinegunner"] = ["CMUExpeditionScavengerMachinegunner"],
        ["veteran"] = ["CMUExpeditionScavengerVeteran"],
        ["mixed"] = ["CMUExpeditionScavenger", "CMUExpeditionScavengerSupport", "CMUExpeditionScavengerSkirmisher",
            "CMUExpeditionScavengerMedic", "CMUExpeditionScavengerBreacher", "CMUExpeditionScavengerMarksman",
            "CMUExpeditionScavengerRocketeer", "CMUExpeditionScavengerVeteran", "CMUExpeditionScavengerMachinegunner"],
        ["specialists"] = ["CMUExpeditionScavengerSupport", "CMUExpeditionScavengerAssault", "CMUExpeditionScavengerMarksman",
            "CMUExpeditionScavengerRocketeer", "CMUExpeditionScavengerMedic", "CMUExpeditionScavengerBreacher"],
        ["medical"] = ["CMUExpeditionScavengerMedic", "CMUExpeditionScavengerSupport",
            "CMUExpeditionScavengerAssault", "CMUExpeditionScavenger"],
        ["raiders"] = ["CMUExpeditionScavengerBreacher", "CMUExpeditionScavengerSkirmisher",
            "CMUExpeditionScavengerAssault", "CMUExpeditionScavengerSupport", "CMUExpeditionScavengerMedic"],
        ["fireteam"] = ["CMUExpeditionScavengerVeteran", "CMUExpeditionScavengerMachinegunner",
            "CMUExpeditionScavengerSkirmisher", "CMUExpeditionScavengerMedic", "CMUExpeditionScavengerMarksman"],
    };

    public static IEnumerable<string> SquadVariants => SquadPresets.Keys;
    public static bool IsSquadVariant(string variant) => SquadPresets.ContainsKey(variant);
}
