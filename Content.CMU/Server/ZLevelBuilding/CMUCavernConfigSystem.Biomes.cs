using System.Linq;
using Content.Shared.CMU14.ZLevelBuilding;

namespace Content.Server.CMU14.ZLevelBuilding;

public enum CMUCavernBiomePreset : byte
{
    Balanced,
    Dry,
    Flooded,
    Volcanic,
    Biological,
}

public sealed partial class CMUCavernConfigSystem
{
    private static Dictionary<CMUCavernVariant, int>? PresetWeights(CMUCavernBiomePreset preset) => preset switch
    {
        CMUCavernBiomePreset.Balanced => null,
        CMUCavernBiomePreset.Dry => new()
        {
            [CMUCavernVariant.BraidedGalleries] = 4,
            [CMUCavernVariant.PillarCathedral] = 4,
            [CMUCavernVariant.BiologicalDeadZone] = 1,
            [CMUCavernVariant.VentLabyrinth] = 1,
        },
        CMUCavernBiomePreset.Flooded => new()
        {
            [CMUCavernVariant.FloodedFork] = 5,
            [CMUCavernVariant.LeakingCavern] = 3,
            [CMUCavernVariant.ThermalSprings] = 2,
            [CMUCavernVariant.PillarCathedral] = 1,
        },
        CMUCavernBiomePreset.Volcanic => new()
        {
            [CMUCavernVariant.LavaTubes] = 5,
            [CMUCavernVariant.VentLabyrinth] = 3,
            [CMUCavernVariant.BraidedGalleries] = 1,
            [CMUCavernVariant.PillarCathedral] = 1,
            [CMUCavernVariant.ThermalSprings] = 1,
        },
        CMUCavernBiomePreset.Biological => new()
        {
            [CMUCavernVariant.BiologicalDeadZone] = 6,
            [CMUCavernVariant.LeakingCavern] = 2,
            [CMUCavernVariant.FloodedFork] = 1,
            [CMUCavernVariant.BraidedGalleries] = 1,
        },
        _ => throw new ArgumentException("Unknown cavern biome preset."),
    };

    private string DescribeBiomes()
    {
        var entries = Enum.GetValues<CMUCavernVariant>().Select(biome =>
        {
            var weight = _settings.BiomeWeights == null ? 1 : _settings.BiomeWeights.GetValueOrDefault(biome);
            if (_settings.Variant is { } forced)
                weight = biome == forced ? 1 : 0;
            return $"{biome}={weight}";
        });
        return Loc.GetString("cmu-caverns-biome-weights", ("weights", string.Join(", ", entries)));
    }

    /// <summary>Sample a bounded region plan without creating maps, chunks, or generation-world components.</summary>
    public bool Preview(string value, out string feedback)
    {
        feedback = Loc.GetString("cmu-caverns-preview-seed");
        if (_seed == 0)
            return false;
        var depth = _settings.StartDepth;
        if (value.Length > 0 && (!int.TryParse(value, out depth) || depth > _settings.StartDepth || depth < -4096))
        {
            feedback = Loc.GetString("cmu-caverns-preview-depth");
            return false;
        }
        var field = new CMUCavernField(_seed, _settings);
        feedback = Loc.GetString("cmu-caverns-preview", ("seed", _seed), ("depth", depth),
            ("size", _settings.RegionSize)) + "\n";
        var counts = new int[Enum.GetValues<CMUCavernVariant>().Length];
        for (var y = 2; y >= -2; y--)
        {
            var row = new List<string>();
            for (var x = -2; x <= 2; x++)
            {
                var plan = field.GetRegionPlan(new Vector2i(x, y), depth);
                row.Add(((char) ('A' + (int) plan.Chambers[0].Variant)).ToString());
                foreach (var chamber in plan.Chambers)
                    counts[(int) chamber.Variant]++;
            }
            feedback += string.Join(" ", row) + "\n";
        }
        var legend = Enum.GetValues<CMUCavernVariant>().Select(biome =>
            $"{(char) ('A' + (int) biome)}={biome} ({counts[(int) biome]})");
        feedback += Loc.GetString("cmu-caverns-preview-legend", ("legend", string.Join(", ", legend)));
        return true;
    }
}
