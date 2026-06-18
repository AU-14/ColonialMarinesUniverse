using System;
using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared.FixedPoint;

namespace Content.Shared._CMU14.Medical.Human.Rules;

public readonly record struct SkeletalRuleInput(
    BodyRegion Region,
    FixedPoint2 BruteDamage,
    bool BoneContact,
    bool AlreadyBroken,
    bool Splinted);

public readonly record struct SkeletalRuleResult(
    bool ShouldBreak,
    int ChancePercent,
    FixedPoint2 RollDamage,
    FractureSeverity Severity);

public static class SkeletalRules
{
    public static SkeletalRuleResult EvaluateFracture(
        SkeletalRuleInput input,
        MedicalRngContext rng)
    {
        var rollDamage = GetFractureRollDamage(input.BruteDamage);
        var chancePercent = GetFractureChancePercent(rollDamage);

        if (!input.BoneContact || input.AlreadyBroken || input.BruteDamage <= FixedPoint2.Zero)
            return new SkeletalRuleResult(false, chancePercent, rollDamage, FractureSeverity.None);

        var chance = Math.Clamp(chancePercent / 100f, 0f, 1f);
        var roll = Math.Clamp(rng.BoneRoll, 0f, 1f);

        var shouldBreak = roll < chance;
        return new SkeletalRuleResult(
            shouldBreak,
            chancePercent,
            rollDamage,
            shouldBreak ? GetSeverity(rollDamage) : FractureSeverity.None);
    }

    public static FixedPoint2 GetFractureRollDamage(FixedPoint2 bruteDamage)
    {
        if (bruteDamage <= FixedPoint2.Zero)
            return FixedPoint2.Zero;

        return FixedPoint2.New(bruteDamage.Float() * 3f);
    }

    public static int GetFractureChancePercent(FixedPoint2 rollDamage)
    {
        if (rollDamage <= FixedPoint2.Zero)
            return 0;

        return Math.Max(0, (int) MathF.Round(rollDamage.Float() * 2f));
    }

    public static FractureSeverity GetSeverity(FixedPoint2 rollDamage)
    {
        if (rollDamage >= FixedPoint2.New(120))
            return FractureSeverity.Shattered;
        if (rollDamage >= FixedPoint2.New(90))
            return FractureSeverity.Compound;
        if (rollDamage >= FixedPoint2.New(45))
            return FractureSeverity.Simple;

        return FractureSeverity.Hairline;
    }
}
