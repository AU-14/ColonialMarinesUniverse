using System.Diagnostics.CodeAnalysis;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Items.Components;
using Content.Shared.Botany.Systems;
using Content.Shared.Botany.Traits.Components;
using Content.Shared.Destructible.Thresholds;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.CMU14.Botany;

/// <summary>
/// One place for the seed-extraction rules shared by the plant-bag verb and the batch seed extractor:
/// how many packets one produce gives, and whether a produce has seeds at all.
/// </summary>
public static class CMUSeedYield
{
    /// <summary>
    /// Rolls the number of packets for one produce: an integer in [Min, Max], both inclusive
    /// (the same as <see cref="MinMax.Next"/>, 1-3 by default).
    /// </summary>
    public static int RollPacketCount(MinMax baseSeeds, IRobustRandom random)
    {
        var min = Math.Max(0, (int) baseSeeds.Min);
        var max = Math.Max(min, (int) baseSeeds.Max);
        return random.Next(min, max + 1);
    }

    /// <summary>
    /// The most packets one produce can give, used to reserve output room before a batch starts.
    /// </summary>
    public static int MaxPacketCount(MinMax baseSeeds)
    {
        var min = Math.Max(0, (int) baseSeeds.Min);
        return Math.Max(min, (int) baseSeeds.Max);
    }

    /// <summary>
    /// True when the produce's plant (its snapshot, or else its prototype) has the seedless trait.
    /// </summary>
    public static bool IsSeedless(BotanySystem botany, ProduceComponent produce)
    {
        return produce.PlantProtoId is { } plantProtoId &&
               botany.TryGetPlantComponent<PlantTraitSeedlessComponent>(produce.PlantData, plantProtoId, out _);
    }

    /// <summary>
    /// Gets what is needed to spawn seed packets from a produce, the same way the upstream seed extractor does.
    /// Fails for produce without a plant, seedless produce, or a plant without <see cref="PlantDataComponent"/>.
    /// </summary>
    public static bool TryGetSeedSource(
        BotanySystem botany,
        ProduceComponent produce,
        out EntProtoId plantProtoId,
        [NotNullWhen(true)] out PlantDataComponent? plantData)
    {
        plantProtoId = default;
        plantData = null;

        if (produce.PlantProtoId is not { } protoId || IsSeedless(botany, produce))
            return false;

        if (!botany.TryGetPlantComponent<PlantDataComponent>(produce.PlantData, protoId, out plantData))
            return false;

        plantProtoId = protoId;
        return true;
    }
}
