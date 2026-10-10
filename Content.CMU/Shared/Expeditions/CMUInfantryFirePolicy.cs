namespace Content.Shared.CMU14.Expeditions;

public readonly record struct CMUInfantryFireContext(int BaseVolley, float BaseDuration, float BaseRecovery,
    int AvailableAmmo, bool Automatic, bool VisibleTarget, bool SafeLane, bool Exposed,
    bool UnderFire, bool EnemyFiring, bool TargetReappeared, float Distance);

public readonly record struct CMUInfantryFirePlan(int Volley, float Duration, float Recovery, bool Sustained);

/// <summary>Finite trigger discipline. Visibility, native cadence and friendly safety remain executor checks.</summary>
public static class CMUInfantryFirePolicy
{
    public static CMUInfantryFirePlan Decide(in CMUInfantryFireContext context)
    {
        var volley = Math.Max(1, context.BaseVolley);
        var baseline = new CMUInfantryFirePlan(volley, context.BaseDuration, context.BaseRecovery, false);
        if (!context.VisibleTarget || !context.SafeLane)
            return baseline;

        var contested = context.UnderFire || context.EnemyFiring || context.TargetReappeared;
        if (!contested && !(context.Exposed && context.Distance <= 6))
            return baseline;

        // Even a nearly empty or semi-automatic weapon should exploit a real peek. Only
        // full auto receives a longer trigger hold; this cannot accelerate its native rate.
        var recovery = Math.Min(context.BaseRecovery, context.Exposed ? 0.1f : 0.15f);
        if (!context.Automatic || context.AvailableAmmo <= volley * 2)
            return baseline with { Recovery = recovery };

        // Reassess after at most 18 rounds in the open, or ten from an established angle.
        // Reserve at least one ordinary volley instead of exhausting a magazine on one peek.
        var limit = context.Exposed ? Math.Min(18, Math.Max(12, volley * 3)) : Math.Min(10, volley * 2);
        limit = Math.Max(volley, Math.Min(limit, context.AvailableAmmo - volley));
        var duration = Math.Max(context.BaseDuration, context.Exposed ? 1.5f : 0.8f);
        return new CMUInfantryFirePlan(limit, duration, recovery, limit > volley);
    }
}
