using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Rounding;

namespace Content.Client._RMC14.Medical.HUD;

/// <summary>
/// Converts a xenonid's current damage into the RSI state used by the xenonid health HUD.
/// </summary>
public static class CMXenoHealthIconState
{
    public static string? GetState(
        FixedPoint2 damage,
        MobState state,
        FixedPoint2? criticalThreshold,
        FixedPoint2? deadThreshold)
    {
        if (state == MobState.Dead)
            return null;

        if (state == MobState.Critical ||
            state == MobState.Alive && criticalThreshold is { } aliveCriticalThreshold && damage > aliveCriticalThreshold)
        {
            if (criticalThreshold is not { } critical || deadThreshold is not { } dead)
                return null;

            dead -= critical;
            damage -= critical;
            var level = ContentHelpers.RoundToLevels(damage.Double(), dead.Double(), 11);
            var name = level > 0 ? $"{level * 10}" : "1";
            return $"xenohealth-{name}";
        }

        criticalThreshold ??= deadThreshold;
        if (criticalThreshold is not { } healthyThreshold)
            return null;

        var healthyLevel = ContentHelpers.RoundToLevels(
            (healthyThreshold - damage).Double(),
            healthyThreshold.Double(),
            11);
        var healthyName = healthyLevel > 0 ? $"{healthyLevel * 10}" : "0";
        return $"xenohealth{healthyName}";
    }
}
