// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using Content.Shared.Procedural; // RUMC: the noise features of the layers
using Content.Shared.Whitelist; // RUMC: the tools that dig a layer
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes; // RUMC

namespace Content.Shared.CMU14.ZLevelBuilding;

/// <summary>
/// Building overhaul (z-level): per-map opt-out switch for vertical building.
///
/// By DEFAULT the building overhaul treats <b>every</b> map as z-buildable - digging down lazily creates a
/// stone level below and links it into a CMU z-network on demand, so the feature works even on maps that
/// were authored as single-z (not multi-z). This component does not need to be added for the feature to work.
///
/// HOW TO MAKE A MAP "NOT MULTI-Z" (opt a map out of vertical building):
///   Add this component to the map prototype with <c>enabled: false</c>, e.g.
///
///       - type: entity
///         id: MyMapThatShouldStaySingleZ
///         ...
///         components:
///         - type: ZBuildableMap
///           enabled: false
///
///   With <c>enabled: false</c>, <see cref="Content.Server.CMU14.ZLevelBuilding.ZLevelBuildingSystem"/> will
///   refuse to bootstrap a level below or generate stone for that map, leaving it strictly single-z.
///
/// To disable the feature globally in code (all maps), set
/// <c>ZLevelBuildingSystem.GloballyEnabled = false</c> instead.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ZBuildableMapComponent : Component
{
    /// <summary>Whether this specific map allows the building overhaul's dig-down / vertical building.
    /// Networked so the client-side construction-ghost check (<see cref="ZBuildAllowed"/>) matches the server.</summary>
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    /// <summary>Side length (in tiles) of each lazily-generated stone chunk on the level below.</summary>
    [DataField]
    public int ChunkSize = 8;

    /// <summary>
    /// Content tile prototypes used for the floor of generated stone levels. Generation picks one at random per
    /// tile, giving a stone/dirt mix. These are the CM planet tiles used on maps like Barker's Lament, not the
    /// vanilla SS14 asteroid tiles.
    /// </summary>
    [DataField]
    // RUMC: the stone layer is basalt now; the floor that was here (kept): new() { "CMPlanetMarsCave", "CMPlanetMarsDirt" }
    public List<string> StoneFloorTiles = new() { "FloorBasalt" };

    /// <summary>
    /// Mineable rock entity scattered across generated chunks (drops salvageable materials when mined).
    /// </summary>
    [DataField]
    // RUMC: the rock of the stone layer is the one of the mining of this fork; the rock that was here (kept): "mineablesolarisrocksteel"
    public string StoneRockEntity = "RUMCRockWall";

    // RUMC Layers Begin: the veins, the deposits, the caves, the pools and the layers of depth (clay, black stone) of the digging down, and what a cave-in brings down

    /// <summary>
    /// Veins of ore in the plain stone layer (the levels that no depth biome covers): metals in bigger groups,
    /// crystals in small ones, each in about one chunk in ten to twenty.
    /// </summary>
    [DataField]
    public List<ZLevelOreVein> StoneOreVeins = new()
    {
        Metal("RUMCRockWallIron", 0.15f),
        Metal("RUMCRockWallCopper", 0.12f),
        Metal("RUMCRockWallAluminum", 0.10f),
        Metal("RUMCRockWallLead", 0.08f),
        Metal("RUMCRockWallSilver", 0.02f),
        Metal("RUMCRockWallGold", 0.02f),
        Metal("RUMCRockWallChrome", 0.05f),
        Metal("RUMCRockWallCobalt", 0.05f),
        Metal("RUMCRockWallTungsten", 0.04f),
        Metal("RUMCRockWallCokeCoal", 0.10f),
        Metal("RUMCRockWallPlatinum", 0.015f),
        Gem("RUMCRockWallRawDiamond", 0.004f),
        Gem("RUMCRockWallEmerald", 0.005f),
        Gem("RUMCRockWallRuby", 0.005f),
        Gem("RUMCRockWallAlexandrite", 0.004f),
        Crystal("RUMCRockWallQuartz", 0.05f),
        Crystal("RUMCRockWallSaltpeter", 0.04f),
        Crystal("RUMCRockWallCinnabar", 0.03f),
        Crystal("RUMCRockWallAsbestos", 0.03f),
        Crystal("RUMCRockWallFluorite", 0.03f),
        Crystal("RUMCRockWallBeryllium", 0.03f),
        Crystal("RUMCRockWallColtan", 0.03f),
        Crystal("RUMCRockWallPhosphorus", 0.03f),
        Crystal("RUMCRockWallTrimorid", 0.02f),
        Crystal("RUMCRockWallLaxanite", 0.02f),
        Crystal("RUMCRockWallLaytoite", 0.02f),
    };

    /// <summary>Deposits of ore and of oil lying on the floor of the caves of the plain stone layer (only where there is a cave), each with its chance in a chunk.</summary>
    [DataField]
    public List<ZLevelDeposit> StoneDeposits = new()
    {
        new ZLevelDeposit { Entity = "RUMCOreDepositStone", ChancePerChunk = 0.35f },
        new ZLevelDeposit { Entity = "RUMCOilDeposit", ChancePerChunk = 0.12f },
    };

    /// <summary>Empty caves in the plain stone layer: where this noise is high, the rock is left out.</summary>
    [DataField]
    public ZLevelNoiseFeature? StoneCaves = new() { Frequency = 0.05f, Octaves = 3, Threshold = 0.47f, SeedOffset = 1 };

    /// <summary>Underground pools in the plain stone layer: where this noise is high, there is water in place of rock.</summary>
    [DataField]
    public ZLevelNoiseFeature? StoneWater = new() { Frequency = 0.035f, Octaves = 2, Threshold = 0.72f, SeedOffset = 2 };

    /// <summary>What lies about in the caves of the stone layer: rocks, scattered over the floor of the caves.</summary>
    [DataField]
    public List<ZLevelCaveDecor> StoneCaveDecor = new()
    {
        new ZLevelCaveDecor { Entity = "RMCRock07", Chance = 0.05f },
        new ZLevelCaveDecor { Entity = "RUMCOilDeposit", Chance = 0.02f },
    };

    /// <summary>The entity that fills a pool tile.</summary>
    [DataField]
    public string WaterEntity = "RUMCCaveWater";

    private static ZLevelOreVein Metal(string wall, float chance)
        => new() { Entity = wall, ChancePerChunk = chance, MinGroupSize = 4, MaxGroupSize = 8, Radius = 3 };

    // A gem is a single block of rock, not a vein.
    private static ZLevelOreVein Gem(string wall, float chance)
        => new() { Entity = wall, ChancePerChunk = chance, MinGroupSize = 1, MaxGroupSize = 1, Radius = 0 };

    private static ZLevelOreVein Crystal(string wall, float chance)
        => new() { Entity = wall, ChancePerChunk = chance, MinGroupSize = 2, MaxGroupSize = 4, Radius = 2 };

    [DataField]
    public EntityWhitelist? RequiredDigTool = new() { Tags = new() { "Pickaxe" } };

    [DataField]
    public List<ZLevelBiomeTier> DepthBiomes = new()
    {
        new ZLevelBiomeTier
        {
            Name = "Clay Layer",
            DepthFrom = -1,
            DepthTo = -2,
            StoneFloorTiles = new List<string> { "FloorPlanetDirt" },
            StoneRockEntity = "RUMCClayWall",
            RequiredDigTool = new EntityWhitelist { Components = new[] { "Shovel", "RMCShovel" } },
            // From the ground, down into the clay, and through it, a shovel digs; a pickaxe is wanted to break from the clay into the stone.
            RequiredEntryTool = new EntityWhitelist { Components = new[] { "Shovel", "RMCShovel" } },
            OreVeins = new List<ZLevelOreVein>
            {
                new ZLevelOreVein { Entity = "RUMCClayWallCoal", ChancePerChunk = 0.3f },
                new ZLevelOreVein { Entity = "RUMCClayWallCokeCoal", ChancePerChunk = 0.2f },
            },
            CaveDecor = new List<ZLevelCaveDecor>
            {
                new ZLevelCaveDecor { Entity = "RUMCOilDeposit", Chance = 0.01f },
            },
            // Small empty caves made by noise, a lot rarer and smaller than those of the stone layer.
            Caves = new ZLevelNoiseFeature { Frequency = 0.08f, Octaves = 3, Threshold = 0.55f, SeedOffset = 1 },
        },

        // From ten levels down it goes on for ever: dark rock with the precious metals and the titanium, empty
        // caves, and lakes of lava where the stone layer has pools of water.
        new ZLevelBiomeTier
        {
            Name = "Black Stone Layer",
            DepthFrom = -10,
            DepthTo = int.MinValue,
            StoneFloorTiles = new List<string> { "RUMCFloorBlackBasalt" },
            StoneRockEntity = "RUMCBlackRockWall",
            RequiredDigTool = new EntityWhitelist { Tags = new() { "Jackhammer" } },
            RequiredEntryTool = new EntityWhitelist { Tags = new() { "Jackhammer" } },
            OreVeins = new List<ZLevelOreVein>
            {
                Metal("RUMCBlackRockWallSilver", 0.12f),
                Metal("RUMCBlackRockWallGold", 0.10f),
                Metal("RUMCBlackRockWallTitanium", 0.12f),
                Metal("RUMCBlackRockWallPlatinum", 0.08f),
                Metal("RUMCBlackRockWallUranium", 0.08f),
                Metal("RUMCBlackRockWallIron", 0.10f),
                Metal("RUMCBlackRockWallCopper", 0.08f),
                Metal("RUMCBlackRockWallAluminum", 0.07f),
                Metal("RUMCBlackRockWallLead", 0.05f),
                Metal("RUMCBlackRockWallChrome", 0.04f),
                Metal("RUMCBlackRockWallCobalt", 0.04f),
                Metal("RUMCBlackRockWallTungsten", 0.03f),
                Crystal("RUMCBlackRockWallQuartz", 0.04f),
                Crystal("RUMCBlackRockWallSaltpeter", 0.03f),
                Crystal("RUMCBlackRockWallCinnabar", 0.02f),
                Crystal("RUMCBlackRockWallAsbestos", 0.02f),
                Crystal("RUMCBlackRockWallFluorite", 0.02f),
                Crystal("RUMCBlackRockWallBeryllium", 0.02f),
                Crystal("RUMCBlackRockWallColtan", 0.02f),
                Crystal("RUMCBlackRockWallPhosphorus", 0.02f),
                Crystal("RUMCBlackRockWallTrimorid", 0.015f),
                Crystal("RUMCBlackRockWallLaxanite", 0.015f),
                Crystal("RUMCBlackRockWallLaytoite", 0.015f),
                Gem("RUMCBlackRockWallRawDiamond", 0.15f),
                Gem("RUMCBlackRockWallEmerald", 0.20f),
                Gem("RUMCBlackRockWallRuby", 0.20f),
                Gem("RUMCBlackRockWallAlexandrite", 0.15f),
            },
            Deposits = new List<ZLevelDeposit>
            {
                new ZLevelDeposit { Entity = "RUMCOreDepositDarkStone", ChancePerChunk = 0.35f },
                new ZLevelDeposit { Entity = "RUMCOilDeposit", ChancePerChunk = 0.15f },
            },
            CaveDecor = new List<ZLevelCaveDecor>
            {
                new ZLevelCaveDecor { Entity = "RUMCOilDeposit", Chance = 0.03f },
            },
            Caves = new ZLevelNoiseFeature { Frequency = 0.05f, Octaves = 3, Threshold = 0.42f, SeedOffset = 1 },
            Water = new ZLevelNoiseFeature { Frequency = 0.035f, Octaves = 2, Threshold = 0.68f, SeedOffset = 2 },
            WaterEntity = "RUMCCaveLava",
        },
    };

    // RUMC: the debris of a collapse was a loose rock (the prototype AU14RockDebris, switched off); it is the resource of the layer now
    /// <summary>
    /// What a cave-in brings down instead of plain debris: the resource of the layer that stood over the tile, by the prototype of the rock (or one of its parents): clay
    /// where the layer above is clay, black stone where it is black stone. Any other rock gives <see cref="DefaultCollapseResource"/>.
    /// </summary>
    [DataField]
    public Dictionary<string, string> CollapseResources = new()
    {
        ["RUMCClayWall"] = "RUMCClayLump",
        ["RUMCBlackRockWall"] = "RUMCBlackStoneItem",
    };

    /// <summary>The resource a collapse brings down when the rock over it has none of its own: a fragment of stone.</summary>
    [DataField]
    public string DefaultCollapseResource = "RUMCStoneItem";

    // RUMC End

    // RUMC: the field that was here (kept, switched off; the prototype it named is commented out in stone.yml):
    // RUMC: /// <summary>Loose rock debris flung outward at the moment a cave-in begins, before the roof buries the cavern.</summary>
    // RUMC: [DataField]
    // RUMC: public string RockDebris = "AU14RockDebris";

    /// <summary>See-through fog spawned across a cavern while it is caving in (atmosphere; does not block view).</summary>
    [DataField]
    public string CollapseFog = "AU14CollapseFog";

    /// <summary>Animated falling-rock effect spawned on each tile as it caves in.</summary>
    [DataField]
    public string CollapseRockProp = "AU14CollapseRockProp";

    /// <summary>
    /// UNDERGROUND roof stability: the maximum number of tiles any dug-out (open) tile may be from the nearest
    /// solid rock or built pillar before its roof is unstable and caves in. Mine a cavern wider than this with
    /// nothing holding the middle and it collapses; plant a pillar (a vertical <see cref="StructuralSupportComponent"/>)
    /// to make a bigger span safe - exactly like real mine support pillars.
    /// </summary>
    // Networked so the client structural-scanner heat-map uses the map's REAL span instead of assuming the
    // default (otherwise a tuned map would show "stable" where the server will collapse).
    [DataField, AutoNetworkedField]
    public int MaxRoofSpan = 3;

    /// <summary>Rumble/crash sound played to everyone on a level while a cave-in is collapsing.</summary>
    [DataField]
    public string RumbleSound = "/Audio/Effects/explosion3.ogg";
}
