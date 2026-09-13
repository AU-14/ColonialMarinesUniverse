namespace Content.Shared.CMU14.Medical.Injuries.Pain;

/// <summary>
/// Integrates the existing pain profile rules. Between profile expiry, effectiveness,
/// tier-ranking and suppression-clamp boundaries, the velocity is A + B * pain.
/// Work depends on crossed profile boundaries, not the number of elapsed frames.
/// </summary>
public static class CMUPainIntegrator
{
    private const double Epsilon = 0.001f; // Same winner tolerance as the public resolver.
    private const double BoundaryTolerance = 0.0000001;

    public static double Integrate(
        double pain,
        double maximum,
        double target,
        double riseRate,
        double decayRate,
        double sensitivity,
        IReadOnlyList<PainSuppressionEntry> profiles,
        TimeSpan from,
        TimeSpan until)
    {
        if (maximum <= 0)
            return 0;
        target = Math.Clamp(target, 0, maximum);
        pain = Math.Clamp(pain, 0, maximum);
        while (from < until && pain != target)
        {
            var nextExpiry = until;
            foreach (var profile in profiles)
            {
                if (profile.ExpiresAt > from && profile.ExpiresAt < nextExpiry)
                    nextExpiry = profile.ExpiresAt;
            }

            pain = IntegrateActiveProfiles(pain, maximum, target, riseRate, decayRate,
                sensitivity, profiles, from, (nextExpiry - from).TotalSeconds);
            from = nextExpiry;
        }

        return pain;
    }

    private static double IntegrateActiveProfiles(
        double pain,
        double maximum,
        double target,
        double riseRate,
        double decayRate,
        double sensitivity,
        IReadOnlyList<PainSuppressionEntry> profiles,
        TimeSpan now,
        double seconds)
    {
        var direction = Math.Sign(target - pain);
        while (seconds > 0 && direction * (target - pain) > BoundaryTolerance)
        {
            // Evaluate the side we are entering without moving the integrated value.
            // This prevents repeatedly stopping on the same integer-tier boundary.
            var sample = Math.Clamp(pain + direction * BoundaryTolerance, 0, maximum);
            Resolve(profiles, now, sample, maximum, out var accumulation, out var decay);
            var next = NextBoundary(profiles, now, sample, maximum, target, direction);
            ConsiderRoot(accumulation, 0, sample, direction, ref next);
            ConsiderRoot(accumulation, 1, sample, direction, ref next);

            Linear velocity;
            if (direction > 0)
            {
                var suppression = accumulation.At(sample);
                velocity = suppression >= 1
                    ? new Linear(0, 0)
                    : suppression <= 0
                        ? new Linear(riseRate * sensitivity, 0)
                        : new Linear(riseRate * sensitivity * (1 - accumulation.Constant),
                            -riseRate * sensitivity * accumulation.Slope);
            }
            else
            {
                velocity = new Linear(-decayRate - decay.Constant, -decay.Slope);
            }

            var speed = velocity.At(pain);
            if (direction * speed <= 0)
                return pain;

            var boundarySpeed = velocity.At(next);
            var boundaryTime = Math.Abs(velocity.Slope) < 1e-12
                ? (next - pain) / speed
                : direction * boundarySpeed <= 0
                    ? double.PositiveInfinity
                    : Math.Log(boundarySpeed / speed) / velocity.Slope;

            if (boundaryTime <= seconds)
            {
                pain = next;
                seconds -= Math.Max(0, boundaryTime);
                continue;
            }

            var exponent = velocity.Slope * seconds;
            // Avoid cancellation when the exponential is close to one.
            var exponentialMinusOne = Math.Abs(exponent) < 1e-5
                ? exponent * (1 + exponent * (0.5 + exponent / 6))
                : Math.Exp(exponent) - 1;
            var change = Math.Abs(velocity.Slope) < 1e-12
                ? speed * seconds
                : speed * exponentialMinusOne / velocity.Slope;
            return Math.Clamp(pain + change, Math.Min(pain, target), Math.Max(pain, target));
        }

        return Math.Abs(target - pain) <= BoundaryTolerance ? target : pain;
    }

    private static void Resolve(IReadOnlyList<PainSuppressionEntry> profiles, TimeSpan now,
        double pain, double maximum, out Linear accumulation, out Linear decay)
    {
        accumulation = default;
        decay = default;
        var bestAccumulation = default(Linear);
        var bestDecay = default(Linear);
        var bestTier = 0;
        foreach (var profile in profiles)
        {
            if (profile.ExpiresAt <= now)
                continue;
            var effectiveness = Effectiveness(profile, pain, maximum);
            var candidateAccumulation = effectiveness * profile.AccumulationSuppression;
            var candidateDecay = effectiveness * profile.DecayBonus;
            var tier = (int)Math.Floor(profile.TierSuppression * effectiveness.At(pain) + Epsilon);
            if (profile.Additive)
            {
                accumulation += candidateAccumulation;
                decay += candidateDecay;
                continue;
            }

            if (!CMUPainSuppressionResolver.IsProfileStronger(
                    candidateAccumulation.At(pain), tier, candidateDecay.At(pain),
                    bestAccumulation.At(pain), bestTier, bestDecay.At(pain)))
                continue;
            bestAccumulation = candidateAccumulation;
            bestDecay = candidateDecay;
            bestTier = tier;
        }

        accumulation += bestAccumulation;
        decay += bestDecay;
    }

    private static double NextBoundary(IReadOnlyList<PainSuppressionEntry> profiles, TimeSpan now,
        double pain, double maximum, double target, int direction)
    {
        var next = target;
        for (var i = 0; i < profiles.Count; i++)
        {
            var profile = profiles[i];
            if (profile.ExpiresAt <= now)
                continue;
            var effectiveness = Effectiveness(profile, pain, maximum);
            if (profile.ReductionDecreaseRate > 0)
            {
                Consider(maximum / profile.ReductionDecreaseRate, pain, direction, ref next);
                if (!profile.Additive && profile.TierSuppression > 0 && effectiveness.Slope != 0)
                {
                    var tier = profile.TierSuppression * effectiveness.At(pain) + Epsilon;
                    var nextInteger = direction > 0 ? Math.Floor(tier) : Math.Floor(tier) + 1;
                    ConsiderRoot(effectiveness * profile.TierSuppression, nextInteger - Epsilon,
                        pain, direction, ref next);
                }
            }

            if (profile.Additive)
                continue;
            // The zero profile is also a candidate when an ineffective tier-zero
            // drug has no decay benefit and falls below the resolver tolerance.
            ConsiderRoot(effectiveness * profile.AccumulationSuppression, Epsilon,
                pain, direction, ref next);
            for (var j = i + 1; j < profiles.Count; j++)
            {
                var other = profiles[j];
                if (other.ExpiresAt <= now || other.Additive)
                    continue;
                var otherEffectiveness = Effectiveness(other, pain, maximum);
                var accumulationDifference = effectiveness * profile.AccumulationSuppression -
                    otherEffectiveness * other.AccumulationSuppression;
                ConsiderRoot(accumulationDifference, Epsilon, pain, direction, ref next);
                ConsiderRoot(accumulationDifference, -Epsilon, pain, direction, ref next);
                ConsiderRoot(effectiveness * profile.DecayBonus - otherEffectiveness * other.DecayBonus,
                    0, pain, direction, ref next);
            }
        }

        return next;
    }

    private static Linear Effectiveness(PainSuppressionEntry profile, double pain, double maximum)
    {
        if (profile.ReductionDecreaseRate <= 0 || maximum <= 0)
            return new Linear(1, 0);
        var slope = -profile.ReductionDecreaseRate / maximum;
        return 1 + slope * pain <= 0 ? default : new Linear(1, slope);
    }

    private static void ConsiderRoot(Linear expression, double value, double pain, int direction, ref double next)
    {
        if (expression.Slope != 0)
            Consider((value - expression.Constant) / expression.Slope, pain, direction, ref next);
    }

    private static void Consider(double boundary, double pain, int direction, ref double next)
    {
        if (direction * (boundary - pain) > 0 && direction * (next - boundary) > 0)
            next = boundary;
    }

    private readonly record struct Linear(double Constant, double Slope)
    {
        public double At(double pain) => Constant + Slope * pain;
        public static Linear operator +(Linear a, Linear b) => new(a.Constant + b.Constant, a.Slope + b.Slope);
        public static Linear operator -(Linear a, Linear b) => new(a.Constant - b.Constant, a.Slope - b.Slope);
        public static Linear operator *(Linear a, double scale) => new(a.Constant * scale, a.Slope * scale);
    }
}
