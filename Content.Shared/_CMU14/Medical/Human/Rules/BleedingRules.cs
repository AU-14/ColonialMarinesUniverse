using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared.FixedPoint;

namespace Content.Shared._CMU14.Medical.Human.Rules;

public static class BleedingRules
{
    public static BleedSeverity GetSeverity(FixedPoint2 rate)
    {
        if (rate <= FixedPoint2.Zero)
            return BleedSeverity.None;

        if (rate < FixedPoint2.New(1))
            return BleedSeverity.Trace;

        if (rate < FixedPoint2.New(2))
            return BleedSeverity.Light;

        if (rate < FixedPoint2.New(4))
            return BleedSeverity.Moderate;

        if (rate < FixedPoint2.New(6))
            return BleedSeverity.Heavy;

        return BleedSeverity.Critical;
    }
}
