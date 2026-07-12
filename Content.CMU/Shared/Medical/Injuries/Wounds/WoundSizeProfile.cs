using System;
using Content.Shared._RMC14.Medical.Wounds;

namespace Content.Shared._CMU14.Medical.Injuries.Wounds;

public static class WoundSizeProfile
{
    private readonly record struct WoundStage(float Threshold, string Name);

    private static readonly WoundStage[] CutSmallStages =
    {
        new(20f, "ugly ripped cut"),
        new(10f, "ripped cut"),
        new(5f, "cut"),
        new(2f, "healing cut"),
        new(0f, "small scab"),
    };

    private static readonly WoundStage[] CutDeepStages =
    {
        new(25f, "ugly deep ripped cut"),
        new(20f, "deep ripped cut"),
        new(15f, "deep cut"),
        new(8f, "clotted cut"),
        new(2f, "scab"),
        new(0f, "fresh skin"),
    };

    private static readonly WoundStage[] CutFleshStages =
    {
        new(35f, "ugly ripped flesh wound"),
        new(30f, "ugly flesh wound"),
        new(25f, "flesh wound"),
        new(15f, "blood soaked clot"),
        new(5f, "large scab"),
        new(0f, "fresh skin"),
    };

    private static readonly WoundStage[] CutGapingStages =
    {
        new(50f, "gaping wound"),
        new(25f, "large blood soaked clot"),
        new(15f, "large clot"),
        new(5f, "small angry scar"),
        new(0f, "small straight scar"),
    };

    private static readonly WoundStage[] CutGapingBigStages =
    {
        new(60f, "big gaping wound"),
        new(40f, "healing gaping wound"),
        new(10f, "large angry scar"),
        new(0f, "large straight scar"),
    };

    private static readonly WoundStage[] CutMassiveStages =
    {
        new(70f, "massive wound"),
        new(50f, "massive healing wound"),
        new(10f, "massive angry scar"),
        new(0f, "massive jagged scar"),
    };

    private static readonly WoundStage[] BruiseStages =
    {
        new(80f, "monumental bruise"),
        new(50f, "huge bruise"),
        new(30f, "large bruise"),
        new(20f, "moderate bruise"),
        new(10f, "small bruise"),
        new(5f, "tiny bruise"),
        new(0f, "fading bruise"),
    };

    private static readonly WoundStage[] BurnModerateStages =
    {
        new(10f, "ripped burn"),
        new(5f, "moderate burn"),
        new(2f, "healing moderate burn"),
        new(0f, "fresh skin"),
    };

    private static readonly WoundStage[] BurnLargeStages =
    {
        new(20f, "ripped large burn"),
        new(15f, "large burn"),
        new(5f, "healing large burn"),
        new(0f, "fresh skin"),
    };

    private static readonly WoundStage[] BurnSevereStages =
    {
        new(35f, "ripped severe burn"),
        new(30f, "severe burn"),
        new(10f, "healing severe burn"),
        new(0f, "burn scar"),
    };

    private static readonly WoundStage[] BurnDeepStages =
    {
        new(45f, "ripped deep burn"),
        new(40f, "deep burn"),
        new(15f, "healing deep burn"),
        new(0f, "large burn scar"),
    };

    private static readonly WoundStage[] BurnCarbonisedStages =
    {
        new(50f, "carbonised area"),
        new(20f, "healing carbonised area"),
        new(0f, "massive burn scar"),
    };

    private static readonly WoundStage[] InternalBleedingStages =
    {
        new(0f, "bruised artery"),
    };

    private static readonly WoundStage[] LostLimbSmallStages =
    {
        new(40f, "ripped stump"),
        new(30f, "bloody stump"),
        new(15f, "clotted stump"),
        new(0f, "scarred stump"),
    };

    private static readonly WoundStage[] LostLimbStages =
    {
        new(65f, "ripped stump"),
        new(50f, "bloody stump"),
        new(25f, "clotted stump"),
        new(0f, "scarred stump"),
    };

    public static WoundSize FromDamage(float damage)
    {
        return CutFromDamage(damage);
    }

    public static WoundSize FromDamage(WoundType type, WoundMechanism mechanism, float damage)
    {
        if (type == WoundType.Burn || mechanism == WoundMechanism.Burn)
            return BurnFromDamage(damage);

        if (mechanism == WoundMechanism.Crush)
            return WoundSize.Bruise;

        return CutFromDamage(damage);
    }

    public static WoundSize CutFromDamage(float damage)
    {
        if (damage >= 70f)
            return WoundSize.CutMassive;
        if (damage >= 60f)
            return WoundSize.CutGapingBig;
        if (damage >= 50f)
            return WoundSize.CutGaping;
        if (damage >= 25f)
            return WoundSize.CutFlesh;
        if (damage >= 15f)
            return WoundSize.CutDeep;
        return WoundSize.CutSmall;
    }

    public static WoundSize BurnFromDamage(float damage)
    {
        if (damage >= 50f)
            return WoundSize.BurnCarbonised;
        if (damage >= 40f)
            return WoundSize.BurnDeep;
        if (damage >= 30f)
            return WoundSize.BurnSevere;
        if (damage >= 15f)
            return WoundSize.BurnLarge;
        return WoundSize.BurnModerate;
    }

    public static WoundCategory Category(WoundSize size) => size switch
    {
        WoundSize.Bruise => WoundCategory.Bruise,
        WoundSize.BurnModerate
            or WoundSize.BurnLarge
            or WoundSize.BurnSevere
            or WoundSize.BurnDeep
            or WoundSize.BurnCarbonised => WoundCategory.Burn,
        WoundSize.InternalBleeding => WoundCategory.InternalBleeding,
        WoundSize.LostLimbSmall or WoundSize.LostLimb => WoundCategory.LostLimb,
        _ => WoundCategory.Cut,
    };

    public static string StageName(WoundSize size, float damage)
    {
        return StageName(damage, Stages(size));
    }

    public static string TierName(WoundSize size) => size switch
    {
        WoundSize.CutSmall => "small cut",
        WoundSize.CutDeep => "deep cut",
        WoundSize.CutFlesh => "flesh wound",
        WoundSize.CutGaping => "gaping wound",
        WoundSize.CutGapingBig => "big gaping wound",
        WoundSize.CutMassive => "massive wound",
        WoundSize.Bruise => "bruise",
        WoundSize.BurnModerate => "moderate burn",
        WoundSize.BurnLarge => "large burn",
        WoundSize.BurnSevere => "severe burn",
        WoundSize.BurnDeep => "deep burn",
        WoundSize.BurnCarbonised => "carbonised area",
        WoundSize.InternalBleeding => "bruised artery",
        WoundSize.LostLimbSmall or WoundSize.LostLimb => "stump",
        _ => "wound",
    };

    public static int SeverityRank(WoundSize size, float damage = 0f)
    {
        return size switch
        {
            WoundSize.CutSmall or WoundSize.BurnModerate => 0,
            WoundSize.CutDeep or WoundSize.BurnLarge => 1,
            WoundSize.CutFlesh
                or WoundSize.CutGaping
                or WoundSize.BurnSevere
                or WoundSize.BurnDeep
                or WoundSize.LostLimbSmall => 2,
            WoundSize.CutGapingBig
                or WoundSize.CutMassive
                or WoundSize.BurnCarbonised
                or WoundSize.InternalBleeding
                or WoundSize.LostLimb => 3,
            WoundSize.Bruise => BruiseSeverityRank(damage),
            _ => 1,
        };
    }

    public static TimeSpan BandageDelay(WoundSize size, float damage = 0f) => SeverityRank(size, damage) switch
    {
        0 => TimeSpan.FromSeconds(0.5),
        1 => TimeSpan.FromSeconds(1.0),
        2 => TimeSpan.FromSeconds(2.0),
        3 => TimeSpan.FromSeconds(4.0),
        _ => TimeSpan.FromSeconds(1.0),
    };

    public static int BandagesRequired(WoundSize size, float damage = 0f) => SeverityRank(size, damage) switch
    {
        0 => 1,
        1 => 2,
        2 => 3,
        3 => 4,
        _ => 1,
    };

    public static float BleedMultiplier(WoundSize size, float damage = 0f) => SeverityRank(size, damage) switch
    {
        0 => 0.5f,
        1 => 1.0f,
        2 => 1.5f,
        3 => 2.0f,
        _ => 1.0f,
    };

    public static float FieldTreatmentPenalty(WoundSize size, float damage = 0f) => SeverityRank(size, damage) switch
    {
        0 => 0.05f,
        1 => 0.12f,
        2 => 0.20f,
        3 => 0.30f,
        _ => 0.12f,
    };

    public static float PainTarget(WoundSize size, float damage = 0f) => SeverityRank(size, damage) switch
    {
        0 => 5f,
        1 => 15f,
        2 => 30f,
        3 => 50f,
        _ => 0f,
    };

    private static int BruiseSeverityRank(float damage)
    {
        if (damage >= 50f)
            return 3;
        if (damage >= 30f)
            return 2;
        if (damage >= 10f)
            return 1;
        return 0;
    }

    private static WoundStage[] Stages(WoundSize size) => size switch
    {
        WoundSize.CutSmall => CutSmallStages,
        WoundSize.CutDeep => CutDeepStages,
        WoundSize.CutFlesh => CutFleshStages,
        WoundSize.CutGaping => CutGapingStages,
        WoundSize.CutGapingBig => CutGapingBigStages,
        WoundSize.CutMassive => CutMassiveStages,
        WoundSize.Bruise => BruiseStages,
        WoundSize.BurnModerate => BurnModerateStages,
        WoundSize.BurnLarge => BurnLargeStages,
        WoundSize.BurnSevere => BurnSevereStages,
        WoundSize.BurnDeep => BurnDeepStages,
        WoundSize.BurnCarbonised => BurnCarbonisedStages,
        WoundSize.InternalBleeding => InternalBleedingStages,
        WoundSize.LostLimbSmall => LostLimbSmallStages,
        WoundSize.LostLimb => LostLimbStages,
        _ => CutDeepStages,
    };

    private static string StageName(float damage, WoundStage[] stages)
    {
        foreach (var stage in stages)
        {
            if (damage >= stage.Threshold)
                return stage.Name;
        }

        return stages[^1].Name;
    }
}
