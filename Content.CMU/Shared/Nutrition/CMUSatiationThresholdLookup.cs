using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Prototypes;

namespace Content.Shared.CMU14.Nutrition;

/// <summary>Resolves the surrounding satiation thresholds without sorting or allocating.</summary>
public static class CMUSatiationThresholdLookup
{
    public static bool TryGetValue<T>(
        SatiationPrototype prototype,
        float currentValue,
        Dictionary<SatiationValue, T> values,
        out T? result,
        out int? nextHigherThreshold,
        out int? nextLowerThreshold)
    {
        result = default;
        nextHigherThreshold = null;
        nextLowerThreshold = null;

        foreach (var (key, value) in values)
        {
            if (prototype.GetValueOrNull(key) is not { } threshold)
                continue;

            if (currentValue > threshold)
            {
                if (nextLowerThreshold == null || threshold > nextLowerThreshold)
                    nextLowerThreshold = threshold;
            }
            else if (nextHigherThreshold == null || threshold <= nextHigherThreshold)
            {
                // Stable descending order selects the last entry when resolved keys tie.
                nextHigherThreshold = threshold;
                result = value;
            }
        }

        return nextHigherThreshold != null;
    }
}
