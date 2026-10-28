using System;
using Content.Shared._RMC14.Medical.Wounds;

namespace Content.Shared.CMU14.Medical.Injuries.Wounds;

public static class WoundSizeProfile
{
    private readonly record struct WoundStage(float Threshold, string LocId);

    private static readonly WoundStage[] CutSmallStages =
    {
        new(20f, "cmu-wound-stage-cutsmall-0"),
        new(10f, "cmu-wound-stage-cutsmall-1"),
        new(5f, "cmu-wound-stage-cutsmall-2"),
        new(2f, "cmu-wound-stage-cutsmall-3"),
        new(0f, "cmu-wound-stage-cutsmall-4"),
    };

    private static readonly WoundStage[] CutDeepStages =
    {
        new(25f, "cmu-wound-stage-cutdeep-0"),
        new(20f, "cmu-wound-stage-cutdeep-1"),
        new(15f, "cmu-wound-stage-cutdeep-2"),
        new(8f, "cmu-wound-stage-cutdeep-3"),
        new(2f, "cmu-wound-stage-cutdeep-4"),
        new(0f, "cmu-wound-stage-cutdeep-5"),
    };

    private static readonly WoundStage[] CutFleshStages =
    {
        new(35f, "cmu-wound-stage-cutflesh-0"),
        new(30f, "cmu-wound-stage-cutflesh-1"),
        new(25f, "cmu-wound-stage-cutflesh-2"),
        new(15f, "cmu-wound-stage-cutflesh-3"),
        new(5f, "cmu-wound-stage-cutflesh-4"),
        new(0f, "cmu-wound-stage-cutflesh-5"),
    };

    private static readonly WoundStage[] CutGapingStages =
    {
        new(50f, "cmu-wound-stage-cutgaping-0"),
        new(25f, "cmu-wound-stage-cutgaping-1"),
        new(15f, "cmu-wound-stage-cutgaping-2"),
        new(5f, "cmu-wound-stage-cutgaping-3"),
        new(0f, "cmu-wound-stage-cutgaping-4"),
    };

    private static readonly WoundStage[] CutGapingBigStages =
    {
        new(60f, "cmu-wound-stage-cutgapingbig-0"),
        new(40f, "cmu-wound-stage-cutgapingbig-1"),
        new(10f, "cmu-wound-stage-cutgapingbig-2"),
        new(0f, "cmu-wound-stage-cutgapingbig-3"),
    };

    private static readonly WoundStage[] CutMassiveStages =
    {
        new(70f, "cmu-wound-stage-cutmassive-0"),
        new(50f, "cmu-wound-stage-cutmassive-1"),
        new(10f, "cmu-wound-stage-cutmassive-2"),
        new(0f, "cmu-wound-stage-cutmassive-3"),
    };

    private static readonly WoundStage[] BruiseStages =
    {
        new(80f, "cmu-wound-stage-bruise-0"),
        new(50f, "cmu-wound-stage-bruise-1"),
        new(30f, "cmu-wound-stage-bruise-2"),
        new(20f, "cmu-wound-stage-bruise-3"),
        new(10f, "cmu-wound-stage-bruise-4"),
        new(5f, "cmu-wound-stage-bruise-5"),
        new(0f, "cmu-wound-stage-bruise-6"),
    };

    private static readonly WoundStage[] BurnModerateStages =
    {
        new(10f, "cmu-wound-stage-burnmoderate-0"),
        new(5f, "cmu-wound-stage-burnmoderate-1"),
        new(2f, "cmu-wound-stage-burnmoderate-2"),
        new(0f, "cmu-wound-stage-burnmoderate-3"),
    };

    private static readonly WoundStage[] BurnLargeStages =
    {
        new(20f, "cmu-wound-stage-burnlarge-0"),
        new(15f, "cmu-wound-stage-burnlarge-1"),
        new(5f, "cmu-wound-stage-burnlarge-2"),
        new(0f, "cmu-wound-stage-burnlarge-3"),
    };

    private static readonly WoundStage[] BurnSevereStages =
    {
        new(35f, "cmu-wound-stage-burnsevere-0"),
        new(30f, "cmu-wound-stage-burnsevere-1"),
        new(10f, "cmu-wound-stage-burnsevere-2"),
        new(0f, "cmu-wound-stage-burnsevere-3"),
    };

    private static readonly WoundStage[] BurnDeepStages =
    {
        new(45f, "cmu-wound-stage-burndeep-0"),
        new(40f, "cmu-wound-stage-burndeep-1"),
        new(15f, "cmu-wound-stage-burndeep-2"),
        new(0f, "cmu-wound-stage-burndeep-3"),
    };

    private static readonly WoundStage[] BurnCarbonisedStages =
    {
        new(50f, "cmu-wound-stage-burncarbonised-0"),
        new(20f, "cmu-wound-stage-burncarbonised-1"),
        new(0f, "cmu-wound-stage-burncarbonised-2"),
    };

    private static readonly WoundStage[] InternalBleedingStages =
    {
        new(0f, "cmu-wound-stage-internalbleeding-0"),
    };

    private static readonly WoundStage[] LostLimbSmallStages =
    {
        new(40f, "cmu-wound-stage-lostlimbsmall-0"),
        new(30f, "cmu-wound-stage-lostlimbsmall-1"),
        new(15f, "cmu-wound-stage-lostlimbsmall-2"),
        new(0f, "cmu-wound-stage-lostlimbsmall-3"),
    };

    private static readonly WoundStage[] LostLimbStages =
    {
        new(65f, "cmu-wound-stage-lostlimb-0"),
        new(50f, "cmu-wound-stage-lostlimb-1"),
        new(25f, "cmu-wound-stage-lostlimb-2"),
        new(0f, "cmu-wound-stage-lostlimb-3"),
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

    public static string TierName(WoundSize size) => Loc.GetString(size switch
    {
        WoundSize.CutSmall => "cmu-wound-tier-cutsmall",
        WoundSize.CutDeep => "cmu-wound-tier-cutdeep",
        WoundSize.CutFlesh => "cmu-wound-tier-cutflesh",
        WoundSize.CutGaping => "cmu-wound-tier-cutgaping",
        WoundSize.CutGapingBig => "cmu-wound-tier-cutgapingbig",
        WoundSize.CutMassive => "cmu-wound-tier-cutmassive",
        WoundSize.Bruise => "cmu-wound-tier-bruise",
        WoundSize.BurnModerate => "cmu-wound-tier-burnmoderate",
        WoundSize.BurnLarge => "cmu-wound-tier-burnlarge",
        WoundSize.BurnSevere => "cmu-wound-tier-burnsevere",
        WoundSize.BurnDeep => "cmu-wound-tier-burndeep",
        WoundSize.BurnCarbonised => "cmu-wound-tier-burncarbonised",
        WoundSize.InternalBleeding => "cmu-wound-tier-internalbleeding",
        WoundSize.LostLimbSmall or WoundSize.LostLimb => "cmu-wound-tier-stump",
        _ => "cmu-wound-tier-default",
    });

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
                return Loc.GetString(stage.LocId);
        }

        return Loc.GetString(stages[^1].LocId);
    }
}
