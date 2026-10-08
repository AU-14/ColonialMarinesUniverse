using System;
using System.Collections.Generic;

namespace Content.Shared.CMU14.Expeditions;

public enum CMUExpeditionBiome : byte { Woodland, Swamp, Tundra, Beach, Mountain, SwampJungle, BurnedWoodland }
public enum CMUExpeditionLandform : byte
{
    RiverValley, LakeCountry, Ridgeline, Wetlands, Coast, Archipelago, Caldera, Fjord, Delta, Highlands,
}
public enum CMUExpeditionStory : byte { CrashRecovery, SurveyCamp, BrokenConvoy, LostRelay }
public enum CMUExpeditionTerrain : byte { Ground, Scrub, Mud, Water, Stone, Trail, Deck, Structure, Beach, Cliff }
public enum CMUExpeditionProp : byte
{
    None, Tree, Rock, Hull, Supply, Relay, Recovery, Boundary, Boulder,
    CharredTree, FallenLog, Campfire, BurntFrame, DiscardedPack, CargoDebris,
}
// A separate, walk-through layer: vegetation must not consume a tree's slot or obstruct a route.
public enum CMUExpeditionDetail : byte
{
    None, Grass, Bush, Fern, Reeds, Litter, Pebbles, Deadwood, Flowers,
    LeafLitter, Moss, Fungus, DryGrass, Ash,
}
public enum CMUExpeditionFeatureKind : byte { BurnScar, Windthrow, AbandonedCamp, Rockfall, CargoSpill, BogRemains }
public readonly record struct CMUExpeditionFeature(CMUExpeditionFeatureKind Kind, CMUExpeditionPoint Center);
public enum CMUExpeditionCrashImpact : byte { ForestSkid, Burnout, CliffStrike, ShoreBreak, Breakup }
public readonly record struct CMUExpeditionWreckObject(string Prototype, int QuarterTurns);
public readonly record struct CMUExpeditionWreckFloor(string Prototype, byte Rotation);
public enum CMUExpeditionWaterKind : byte { None, Shallow, Deep, Edge, Corner, InnerCorner }
public readonly record struct CMUExpeditionWaterTile(CMUExpeditionWaterKind Kind, int QuarterTurns = 0);
public enum CMUExpeditionSiteKind : byte { LandingZone, Recovery, Camp, Wreck, Relay, Cache, Grove, Outcrop, Deadfall, Hollow }

public readonly record struct CMUExpeditionPoint(int X, int Y);
public readonly record struct CMUExpeditionSite(CMUExpeditionSiteKind Kind, CMUExpeditionPoint Center,
    int Variant = 0, int Rotation = 0);
public readonly record struct CMUExpeditionRoute(int From, int To);
public readonly record struct CMUExpeditionBridge(CMUExpeditionPoint From, CMUExpeditionPoint To);

/// <summary>
/// A complete, engine-independent layout. Generation never consumes the game's random stream.
/// Coordinates are tile indices; world objects belong at their tile centers.
/// </summary>
public sealed class CMUExpeditionPlan
{
    public const int GeneratorVersion = 7;
    public const int DefaultSize = 140;
    public const int LandingRadius = 13;
    public const int MaximumBridgeSpan = 12;

    public int Size { get; }
    public int Seed { get; }
    public CMUExpeditionBiome Biome { get; }
    public CMUExpeditionLandform Landform { get; }
    public CMUExpeditionStory Story { get; }
    public CMUExpeditionTerrain[] Terrain { get; }
    public CMUExpeditionTerrain[] BaseTerrain { get; }
    public CMUExpeditionProp[] Props { get; }
    public CMUExpeditionDetail[] Details { get; }
    /// <summary>Distance from a dry bank, capped at three. Zero means no exposed water.</summary>
    public byte[] WaterDepth { get; }
    public bool[] Reserved { get; }
    public bool[] Paths { get; }
    public bool[] Scorched { get; }
    public Dictionary<int, CMUExpeditionWreckObject> WreckObjects { get; } = new();
    public Dictionary<int, CMUExpeditionWreckFloor> WreckFloors { get; } = new();
    public string? WreckName { get; set; }
    public CMUExpeditionCrashImpact CrashImpact { get; set; }
    public List<CMUExpeditionSite> Sites { get; } = new();
    public List<CMUExpeditionRoute> Routes { get; } = new();
    public List<CMUExpeditionBridge> Bridges { get; } = new();
    public List<CMUExpeditionFeature> Features { get; } = new();
    public List<CMUExpeditionPoint> FirePockets { get; } = new();
    public int TerrainAttempt { get; set; }
    public CMUExpeditionPoint LandingZone => Sites[0].Center;
    public CMUExpeditionPoint Objective => Sites[1].Center;

    public CMUExpeditionPlan(int size, int seed, CMUExpeditionBiome biome,
        CMUExpeditionLandform landform, CMUExpeditionStory story)
    {
        if (size is < 128 or > 196 || !Enum.IsDefined(biome) || !Enum.IsDefined(landform) || !Enum.IsDefined(story))
            throw new ArgumentException("Invalid expedition size or variant.");

        Size = size;
        Seed = seed;
        Biome = biome;
        Landform = landform;
        Story = story;
        Terrain = new CMUExpeditionTerrain[size * size];
        BaseTerrain = new CMUExpeditionTerrain[size * size];
        Props = new CMUExpeditionProp[size * size];
        Details = new CMUExpeditionDetail[size * size];
        WaterDepth = new byte[size * size];
        Reserved = new bool[size * size];
        Paths = new bool[size * size];
        Scorched = new bool[size * size];
    }

    public int Index(int x, int y) => y * Size + x;
}
