using Content.Shared.AU14.util;

namespace Content.Shared.AU14.Scenario;

public static class ScenarioSpawnDefinitionCalculations
{
    public static int CalculateBodyCount(this ScenarioSpawnBodyBucketDefinition bucket, int playerCount)
    {
        if (bucket.Bodies.Count == 0)
            return bucket.Count;

        var count = 0;
        foreach (var (bodyId, staticCount) in bucket.Bodies)
        {
            count += bucket.Scaling.TryGetValue(bodyId, out var scaling)
                ? JobScaling.CalculateScaledSlots(playerCount, staticCount, scaling)
                : staticCount;
        }

        return count;
    }
}
