using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared.FixedPoint;

namespace Content.Shared._CMU14.Medical.Human.Rules;

public static class InjuryRules
{
    public static InjuryStage GetStage(InjuryKind kind, FixedPoint2 damage)
    {
        if (damage <= FixedPoint2.Zero)
            return InjuryStage.None;

        return kind switch
        {
            InjuryKind.Cut or InjuryKind.Puncture => GetCutStage(damage),
            InjuryKind.Bruise => GetBruiseStage(damage),
            InjuryKind.Burn => GetBurnStage(damage),
            InjuryKind.InternalBleed => InjuryStage.InternalBleed,
            InjuryKind.Stump => InjuryStage.Stump,
            InjuryKind.SurgicalIncision => InjuryStage.Deep,
            _ => InjuryStage.None,
        };
    }

    private static InjuryStage GetCutStage(FixedPoint2 damage)
    {
        if (damage < FixedPoint2.New(15))
            return InjuryStage.Small;

        if (damage < FixedPoint2.New(25))
            return InjuryStage.Deep;

        if (damage < FixedPoint2.New(50))
            return InjuryStage.Flesh;

        if (damage < FixedPoint2.New(60))
            return InjuryStage.Gaping;

        if (damage < FixedPoint2.New(70))
            return InjuryStage.GapingBig;

        return InjuryStage.Massive;
    }

    private static InjuryStage GetBruiseStage(FixedPoint2 damage)
    {
        if (damage < FixedPoint2.New(5))
            return InjuryStage.None;

        if (damage < FixedPoint2.New(10))
            return InjuryStage.Tiny;

        if (damage < FixedPoint2.New(20))
            return InjuryStage.Small;

        if (damage < FixedPoint2.New(30))
            return InjuryStage.Moderate;

        if (damage < FixedPoint2.New(50))
            return InjuryStage.Large;

        if (damage < FixedPoint2.New(80))
            return InjuryStage.Huge;

        return InjuryStage.Monumental;
    }

    private static InjuryStage GetBurnStage(FixedPoint2 damage)
    {
        if (damage < FixedPoint2.New(15))
            return InjuryStage.Moderate;

        if (damage < FixedPoint2.New(30))
            return InjuryStage.Large;

        if (damage < FixedPoint2.New(40))
            return InjuryStage.Severe;

        if (damage < FixedPoint2.New(50))
            return InjuryStage.Deep;

        return InjuryStage.Carbonised;
    }
}
