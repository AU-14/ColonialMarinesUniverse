using Content.Shared._RMC14.Vendors;
using Robust.Shared.Prototypes;

namespace Content.Shared._AU14.Insurgency;

/// <summary>
///     Built-in factions that ship with the game rather than being authored in the editor or the DB.
///     Right now that is just the vanilla CLF: the plain Colonial Liberation Front, compatible with
///     every GOVFOR faction and stocked with exactly the equipment the original CLF cell kit deployed.
///
///     Kept in code (not the DB) so it is always present and never needs seeding. The selection popup
///     offers it above the DB Default factions, and picking it applies this definition directly. The
///     editor also lists it: because its vendor arsenals are real <see cref="CMVendorSection"/> data
///     (not baked into a prototype), a host can open it, tweak any item, and save the result as a new
///     Default faction.
/// </summary>
public static class InsurgencyBuiltinFactions
{
    /// <summary>
    ///     Sentinel id used on the wire to mark "the built-in vanilla CLF faction was picked", so it is
    ///     never confused with a real DB row id (which are non-negative).
    /// </summary>
    public const int VanillaClfId = -1;

    // ---------------------------------------------------------------------
    // Tunables for the vanilla CLF supply vendors. One place to change the
    // shared sprite base and the default stock/price the old crates handed out.
    // ---------------------------------------------------------------------

    /// <summary>
    ///     Vendor entity reused as the sprite and collision base for every code-built vanilla vendor.
    ///     Only the sprite/collision are borrowed; the arsenal is replaced by the sections below, so a
    ///     host can edit them in the faction editor.
    /// </summary>
    private static readonly EntProtoId VanillaVendorBase = "AU14CLFObjectiveWeaponsVendor";

    // The old supply crates were free, so everything the vanilla vendors stock costs zero points.
    private const int FreePoints = 0;

    // Default stock the old crates carried: most goods came two-per, small consumables a few more.
    private const int DefaultStock = 2;

    /// <summary>
    ///     The non-vendor machines the original AU14CLFCellKit deployed. The supply crates it used to
    ///     drop are now vendors instead (see VanillaClf below), so only the machines and the lamp are
    ///     free-placed from the Heavy Cell Kit.
    /// </summary>
    private static readonly EntProtoId[] VanillaClfPlaceables =
    {
        "RMCComputerIntelCLF",
        "ComputerObjectivesCLF",
        "RMCTechTreeConsoleCLF",
        "CMFaxCLF",
        "AU14AnalyzerMachineCLF",
        "RMCLampTripod",
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

        // Reuse the real CLF requisitions vendor prototype as-is: it is a fully authored GOVFOR/CLF
        // vendor, so keep its own sections, points mode, and access untouched.
        def.CellKit.VendorDefinitions.Add(new FactionVendorDefinition
        {
            Name = "CLF Requisitions Rack",
            BaseModel = "AU14CLFObjectiveWeaponsVendor",
            UseBaseModelSections = true,
        });

        // The old supply crates, rebuilt as free vendors whose contents are real, editable sections.
        def.CellKit.VendorDefinitions.Add(MedicalVendor());
        def.CellKit.VendorDefinitions.Add(ToolVendor());
        def.CellKit.VendorDefinitions.Add(RecruitmentVendor());
        def.CellKit.VendorDefinitions.Add(ClothingVendor());

        return def;
    }

    // ----- vendor builders ------------------------------------------------
    // Each mirrors the contents of the crate it replaces. Free (points 0) since the crates were free.
    // Contents live here as data so the faction editor can add, change, or remove any of them.

    private static FactionVendorDefinition MedicalVendor() => Vendor("CLF medical cache",
        Section("First Aid",
            Entry("CMFirstAidKitFilled"),
            Entry("CMBurnAidKitFilled"),
            Entry("CMToxinAidKitFilled"),
            Entry("CMFirstAidO2KitFilled"),
            Entry("CMAdvFirstAidKitFilled")));

    private static FactionVendorDefinition ToolVendor() => Vendor("CLF tool cache",
        Section("Field Tools",
            Entry("ANPRC117GRadioCLFFilled", 1),
            Entry("AU14CLFBaseStation", 1),
            Entry("AU14GunCaseRifleMar30"),
            Entry("RMCGunCasePistolZHNK72", 1),
            Entry("RMCGunCasePistolMK80", 1),
            Entry("RMCGunCasePistolM44", 1),
            Entry("CMPackFlare", 3),
            Entry("AU14CMDefibrillator", 1),
            Entry("RMCBinocularsCiv"),
            Entry("AU14BeltUtilityFilled"),
            Entry("RMCBoxFlashlights")));

    private static FactionVendorDefinition RecruitmentVendor() => Vendor("CLF recruitment cache",
        Section("Recruitment",
            Entry("AU14CLFHeadset", 5),
            Entry("RMCCLFArmband", 5),
            Entry("CMMaskCoif", 5),
            Entry("AU14TattooInkCartridge", 6),
            Entry("AU14TattooGun", 1)));

    private static FactionVendorDefinition ClothingVendor() => Vendor("CLF civilian clothing rack",
        Section("Footwear",
            Entry("RMCShoesBlack"), Entry("RMCShoesBlue"), Entry("RMCShoesBrown"),
            Entry("RMCShoesGreen"), Entry("RMCShoesOrange"), Entry("RMCShoesPurple"),
            Entry("RMCShoesRed"), Entry("RMCShoesWhite"), Entry("RMCShoesYellow"),
            Entry("RMCBootsPMC"), Entry("RMCBootsVanBandolier"), Entry("RMCShoesLaceupBrown"),
            Entry("RMCShoesLaceup"), Entry("RMCShoesLeather")),
        Section("Jumpsuits",
            Entry("AU14CivilianWorkwearYellow"), Entry("AU14CivilianWorkwearPink"),
            Entry("AU14CivilianWorkwearBlue"), Entry("AU14CivilianWorkwearGreen"),
            Entry("CMJumpsuitLiaisonBlack"), Entry("CMJumpsuitLiaisonBlue"),
            Entry("CMJumpsuitLiaisonBrown"), Entry("CMJumpsuitLiaisonField"),
            Entry("CMJumpsuitLiaisonIvy"), Entry("CMJumpsuitLiaisonCorporateFormal"),
            Entry("CMJumpsuitLiaisonFormal"), Entry("CMJumpsuitLiaison"),
            Entry("CMJumpsuitLiaisonBlazer"), Entry("CMJumpsuitLiaisonCharcoal"),
            Entry("RMCJumpsuitDutchBandolier"), Entry("RMCJumpsuitCivilian"),
            Entry("CMJumpsuitColonist"), Entry("CMJumpsuitTShirtWhite"),
            Entry("CMJumpsuitTShirtGray"), Entry("CMJumpsuitTShirtRed")),
        Section("Jackets and Coats",
            Entry("RMCJacketCorporateKhaki"), Entry("RMCJacketCorporateBlack"),
            Entry("RMCJacketCorporateBlue"), Entry("RMCJacketCorporateBrown"),
            Entry("AU14CivilianJacketGrayPufferVest"), Entry("AU14CivilianJacketKhakiPufferVest"),
            Entry("AU14CivilianJacketTanPufferVest"), Entry("AU14CivilianJacketGrayPufferJacket"),
            Entry("AU14CivilianJacketOrangePufferJacket"), Entry("AU14CivilianJacketKhakiPufferJacket"),
            Entry("AU14WindbreakerBlue"), Entry("AU14WindbreakerGray"),
            Entry("AU14WindbreakerGreen"), Entry("AU14WindbreakerKhakiGreenMix"),
            Entry("AU14WindbreakerKhaki"), Entry("AU14CivilianJacketBlueParka"),
            Entry("AU14CivilianJacketGreenParka"), Entry("AU14CivilianJacketRedParka"),
            Entry("AU14CivilianJacketYellowParka"), Entry("AU14CivilianTanTrenchCoat"),
            Entry("AU14CivilianBrownTrenchCoat"), Entry("AU14CivilianGrayTrenchCoat"),
            Entry("AU14CivilianJacketBomberJacket"), Entry("AU14CivilianJacketOldCoat"),
            Entry("AU14CivilianJacketSnowSuit")),
        Section("Headwear and Eyewear",
            Entry("AU14CivBallCapBlack"), Entry("AU14CivBallCapBlueTrucker"),
            Entry("AU14CivBallCapRedTrucker"), Entry("AU14USCMVeteranCap"),
            Entry("AU14CivFedoraTan"), Entry("AU14CivFedoraBrown"),
            Entry("AU14CivFedoraGray"), Entry("RMCSunglasses"),
            Entry("RMCHipsterGlasses"), Entry("RMCSunglassesBig"),
            Entry("AU14GlassesPersonalOrange"), Entry("RMCGlassesAviators"),
            Entry("RMCGlassesTriMaxYellow"), Entry("RMCGlassesTriMaxBlack"),
            Entry("RMCGlassesTriMaxBronze"), Entry("AU14GlassesBiMexOrange")),
        Section("Bags and Gloves",
            Entry("CMHandsBrown"), Entry("CMHandsLightBrown"),
            Entry("CMSatchel"), Entry("AU14SlingSatchelBlack"),
            Entry("AU14SlingSatchelBlue"), Entry("RMCBeltUtilityGeneral"),
            Entry("RMCPouchGeneral")));

    // ----- builders -------------------------------------------------------

    // A free vanilla vendor sharing the CLF rack sprite, stocking the given sections.
    private static FactionVendorDefinition Vendor(string name, params CMVendorSection[] sections) =>
        new()
        {
            Name = name,
            BaseModel = VanillaVendorBase,
            Sections = new List<CMVendorSection>(sections),
        };

    private static CMVendorSection Section(string name, params CMVendorEntry[] entries) =>
        new()
        {
            Name = name,
            Entries = new List<CMVendorEntry>(entries),
        };

    // One vendor line: an item, its stock, free of charge. Stock defaults to the crate's usual two.
    private static CMVendorEntry Entry(EntProtoId id, int amount = DefaultStock) =>
        new()
        {
            Id = id,
            Amount = amount,
            Points = FreePoints,
        };
}
