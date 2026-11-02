using Robust.Shared.Maths;

namespace Content.Shared.CMU14.ZLevelBuilding;

public sealed partial class CMUCavernField
{
    private readonly int[]? _biomeWeights;

    private static int[]? ReadBiomeWeights(Dictionary<CMUCavernVariant, int>? weights)
    {
        if (weights == null)
            return null;

        // Copy in enum order: dictionary insertion order and later edits must not change sampled terrain.
        var result = new int[Enum.GetValues<CMUCavernVariant>().Length];
        var total = 0;
        foreach (var (biome, weight) in weights)
        {
            if (!Enum.IsDefined(biome) || weight is < 0 or > 1000)
                throw new ArgumentException("Cavern biome weights must name a valid biome and range from 0 to 1000.");
            result[(int) biome] = weight;
            total += weight;
        }
        if (total == 0)
            throw new ArgumentException("At least one cavern biome must have a positive weight.");
        return result;
    }

    private CMUCavernVariant WeightedBiome(Vector2i region, int top, int salt)
    {
        var weights = _biomeWeights!;
        var total = 0;
        foreach (var weight in weights)
            total += weight;
        var roll = (int) (Hash(region, top, salt) % (uint) total);
        for (var i = 0; i < weights.Length; i++)
        {
            if (roll < weights[i])
                return (CMUCavernVariant) i;
            roll -= weights[i];
        }
        throw new InvalidOperationException("Validated cavern biome weights could not be sampled.");
    }

    private CMUCavernVariant SatelliteBiome(Vector2i region, int top, int salt, CMUCavernVariant family)
    {
        if (_settings.Variant is { } forced)
            return forced;
        if (_biomeWeights != null)
            return WeightedBiome(region, top, salt);
        // Preserve existing seeds when no palette is configured.
        return (CMUCavernVariant) (((int) family + 1 + Hash(region, top, salt) % 7) % 8);
    }
}
