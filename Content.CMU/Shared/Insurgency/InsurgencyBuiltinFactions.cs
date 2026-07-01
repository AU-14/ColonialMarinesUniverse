using Content.Shared._RMC14.Vendors;
using Robust.Shared.Prototypes;

namespace Content.Shared._AU14.Insurgency;

/// <summary>
///     Built-in factions that ship with the game rather than being authored in the editor or the DB.
///     Right now that is just the vanilla CLF: the plain Colonial Liberation Front, compatible with
///     every GOVFOR faction and stocked with exactly the equipment the original CLF cell kit deployed.
///
///     Kept in code (not the DB) so it is always present and never needs seeding. The selection popup
///     offers it above the DB Default factions, and picking it applies this definition directly.
/// </summary>
public static class InsurgencyBuiltinFactions
{
    /// <summary>
    ///     Sentinel id used on the wire to mark "the built-in vanilla CLF faction was picked", so it is
    ///     never confused with a real DB row id (which are non-negative).
    /// </summary>
    public const int VanillaClfId = -1;

    /// <summary>
    ///     The non-vendor machines and crates the original AU14CLFCellKit deployed, minus the vendor.
    ///     The leader free-places these from the Heavy Cell Kit, matching the old deployment.
    /// </summary>
    private static readonly EntProtoId[] VanillaClfPlaceables =
    {
        "RMCComputerIntelCLF",
        "ComputerObjectivesCLF",
        "RMCTechTreeConsoleCLF",
        "CMFaxCLF",
        "AU14AnalyzerMachineCLF",
        "RMCLampTripod",
        "RMCCrateMedicalFirstAid",
        "AU14CrateCivilianClothingRandom",
        "AU14CrateCivilianClothingRandom",
        "AU14CrateCLFinitalTools",
        "AU14CrateCLFRecruitmentKit",
    };

    /// <summary>
    ///     The vanilla CLF faction definition. Built fresh each call so a caller can never mutate the
    ///     shared instance.
    /// </summary>
    public static FactionDefinition VanillaClf()
    {
        var def = new FactionDefinition
        {
            Metadata = new FactionMetadata
            {
                Title = "Colonial Liberation Front",
                Description = "The standard CLF cell. No special doctrine, no custom arsenal.",
                RoleplayText = "Play as a classic CLF insurgent cell.",
                StatusIcon = "CLFFaction",
            },
        };

        foreach (var placeable in VanillaClfPlaceables)
            def.CellKit.PlaceableEntities.Add(placeable);

        // Reuse the real CLF requisitions vendor prototype as-is: keep its own sections, points mode,
        // and access exactly as it ships.
        def.CellKit.VendorDefinitions.Add(new FactionVendorDefinition
        {
            Name = "CLF Requisitions Rack",
            BaseModel = "AU14CLFObjectiveWeaponsVendor",
            UseBaseModelSections = true,
        });

        return def;
    }
}
