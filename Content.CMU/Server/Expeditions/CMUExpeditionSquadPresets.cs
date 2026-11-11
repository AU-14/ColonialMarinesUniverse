namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    // Execution and completion use the same catalog, including repeatable mixed compositions.
    private static readonly Dictionary<string, string[]> SquadPresets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["rifleman"] = ["CMUExpeditionScavenger", "CMUExpeditionScavengerRich", "CMUExpeditionScavengerScout"],
        ["regular"] = ["CMUExpeditionScavenger"],
        ["poor"] = ["CMUExpeditionScavengerPoor"],
        ["rich"] = ["CMUExpeditionScavengerRich"],
        ["scout"] = ["CMUExpeditionScavengerScout"],
        ["assault"] = ["CMUExpeditionScavengerAssault", "CMUExpeditionScavengerBreacher"],
        ["support"] = ["CMUExpeditionScavengerSupport", "CMUExpeditionScavengerMachinegunner"],
        ["marksman"] = ["CMUExpeditionScavengerMarksman", "CMUExpeditionScavengerSniper", "CMUExpeditionScavengerVeteran"],
        ["sniper"] = ["CMUExpeditionScavengerSniper"],
        ["rocketeer"] = ["CMUExpeditionScavengerRocketeer"],
        ["medic"] = ["CMUExpeditionScavengerMedic"],
        ["breacher"] = ["CMUExpeditionScavengerBreacher"],
        ["skirmisher"] = ["CMUExpeditionScavengerSkirmisher"],
        ["machinegunner"] = ["CMUExpeditionScavengerMachinegunner"],
        ["veteran"] = ["CMUExpeditionScavengerVeteran"],
        ["mixed"] = ["CMUExpeditionScavengerVeteran", "CMUExpeditionScavengerMachinegunner", "CMUExpeditionScavengerMedic",
            "CMUExpeditionScavengerBreacher", "CMUExpeditionScavengerRocketeer", "CMUExpeditionScavengerScout",
            "CMUExpeditionScavengerAssault", "CMUExpeditionScavengerMarksman", "CMUExpeditionScavengerSkirmisher"],
        ["specialists"] = ["CMUExpeditionScavengerSupport", "CMUExpeditionScavengerAssault", "CMUExpeditionScavengerMarksman",
            "CMUExpeditionScavengerRocketeer", "CMUExpeditionScavengerMedic", "CMUExpeditionScavengerBreacher"],
        ["medical"] = ["CMUExpeditionScavengerMedic", "CMUExpeditionScavengerSupport",
            "CMUExpeditionScavengerAssault", "CMUExpeditionScavenger"],
        ["raiders"] = ["CMUExpeditionScavengerBreacher", "CMUExpeditionScavengerSkirmisher",
            "CMUExpeditionScavengerAssault", "CMUExpeditionScavengerSupport", "CMUExpeditionScavengerMedic"],
        ["fireteam"] = ["CMUExpeditionScavengerVeteran", "CMUExpeditionScavengerMachinegunner",
            "CMUExpeditionScavengerSkirmisher", "CMUExpeditionScavengerMedic", "CMUExpeditionScavengerMarksman"],
        ["patrol"] = ["CMUExpeditionScavengerScout", "CMUExpeditionScavenger", "CMUExpeditionScavengerMedic",
            "CMUExpeditionScavengerSkirmisher", "CMUExpeditionScavengerSupport", "CMUExpeditionScavengerRocketeer"],
        ["defense"] = ["CMUExpeditionScavengerMachinegunner", "CMUExpeditionScavengerMarksman", "CMUExpeditionScavengerMedic",
            "CMUExpeditionScavengerBreacher", "CMUExpeditionScavengerRocketeer", "CMUExpeditionScavengerVeteran"],
    };

    // The panel groups equipment variants into six roles. Historical console names
    // remain valid above, as do every existing mob and starting-gear prototype.
    private static readonly string[] PanelSquadVariants =
    [
        "mixed", "fireteam", "patrol", "defense", "raiders", "medical",
        "rifleman", "assault", "support", "marksman", "rocketeer", "medic",
    ];

    public static IEnumerable<string> SquadVariants => SquadPresets.Keys;
    public static bool IsSquadVariant(string variant) => SquadPresets.ContainsKey(variant);
}
