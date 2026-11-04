// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using Content.Shared.Procedural;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.CMU14.ZLevelBuilding;

// A layer of depth (for example clay or black stone): which depths it covers, its floor, rock, digging tool, veins, deposits, caves and pools.
[DataDefinition]
public sealed partial class ZLevelBiomeTier
{
    [DataField]
    public string Name = string.Empty;

    [DataField(required: true)]
    public int DepthFrom;

    [DataField(required: true)]
    public int DepthTo;

    [DataField]
    public List<string>? StoneFloorTiles;

    [DataField]
    public string? StoneRockEntity;

    [DataField]
    public EntityWhitelist? RequiredDigTool;

    // The tool that digs a pit down into this tier from the level above it (the lava layer is broken into with a jackhammer only).
    [DataField]
    public EntityWhitelist? RequiredEntryTool;

    [DataField]
    public List<ProtoId<DungeonConfigPrototype>> DungeonConfigs = new();

    [DataField]
    public float DungeonChance;

    // Veins of ore that replace some of the rock, in groups, like the ore layers of the vanilla biomes.
    [DataField]
    public List<ZLevelOreVein> OreVeins = new();

    // Deposits of ore, lying on the floor under the rock: a drill set on one takes the ore out of it.
    [DataField]
    public List<ZLevelDeposit> Deposits = new();

    // Empty caves and underground pools carved out of the rock by noise. A tier without them has solid rock.
    [DataField]
    public ZLevelNoiseFeature? Caves;

    [DataField]
    public ZLevelNoiseFeature? Water;

    // Things scattered over the floor of the caves of this tier.
    [DataField]
    public List<ZLevelCaveDecor> CaveDecor = new();

    // What fills the pools of this tier (lava in the black stone layer); the map's own water when not set.
    [DataField]
    public string? WaterEntity;

    public bool Contains(int depth)
    {
        var lo = Math.Min(DepthFrom, DepthTo);
        var hi = Math.Max(DepthFrom, DepthTo);
        return depth >= lo && depth <= hi;
    }
}

// Something a level carves out of its rock wherever a smooth noise field is high: the field is sampled at the
// tile coordinates, so a cave or a pool is continuous across chunk borders.
[DataDefinition]
public sealed partial class ZLevelNoiseFeature
{
    // Higher means smaller features: the size of a blob is about one over this many tiles.
    [DataField]
    public float Frequency = 0.06f;

    [DataField]
    public int Octaves = 3;

    // The tiles where the noise (from -1 to 1) is above this are carved out. Higher means rarer.
    [DataField]
    public float Threshold = 0.4f;

    // Keeps the caves and the pools of one level from lying on top of each other.
    [DataField]
    public int SeedOffset;
}

// A thing that lies on the floor of a cave: each cave tile holds it with this chance.
[DataDefinition]
public sealed partial class ZLevelCaveDecor
{
    [DataField(required: true)]
    public EntProtoId Entity;

    [DataField]
    public float Chance = 0.05f;
}

// A deposit of ore laid on one tile of a fresh chunk, under the rock that stands there.
[DataDefinition]
public sealed partial class ZLevelDeposit
{
    [DataField(required: true)]
    public EntProtoId Entity;

    // The chance that a freshly made chunk of the level holds a deposit of this kind.
    [DataField]
    public float ChancePerChunk = 0.08f;
}

// A vein of ore in the rock: what it is, its chance in a chunk, the size of the group and its radius.
[DataDefinition]
public sealed partial class ZLevelOreVein
{
    // The wall that replaces the tier's own rock inside the vein.
    [DataField(required: true)]
    public EntProtoId Entity;

    // The chance that a freshly made chunk of the level holds a vein of this kind.
    [DataField]
    public float ChancePerChunk = 0.5f;

    [DataField]
    public int MinGroupSize = 8;

    [DataField]
    public int MaxGroupSize = 12;

    // How far from the middle of the vein its walls can lie.
    [DataField]
    public int Radius = 4;
}
