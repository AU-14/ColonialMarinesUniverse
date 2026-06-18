using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared.FixedPoint;

namespace Content.Shared._CMU14.Medical.Human.Rules;

public static class LimbSeveranceThresholdRules
{
    private static readonly FixedPoint2 HeadSeveranceThreshold = FixedPoint2.New(185);
    private static readonly FixedPoint2 LimbSeveranceThreshold = FixedPoint2.New(120);

    public static bool ShouldSever(
        BodyRegion region,
        RegionState previous,
        RegionState current,
        FixedPoint2 bruteDelta)
    {
        if (bruteDelta <= FixedPoint2.Zero ||
            previous.Presence != LimbPresence.Present ||
            current.Presence != LimbPresence.Present)
        {
            return false;
        }

        var threshold = GetThreshold(region);
        if (threshold <= FixedPoint2.Zero)
            return false;

        return current.BruteDamage >= threshold;
    }

    public static FixedPoint2 GetThreshold(BodyRegion region)
    {
        return region switch
        {
            BodyRegion.Head => HeadSeveranceThreshold,
            BodyRegion.LeftArm or
            BodyRegion.RightArm or
            BodyRegion.LeftHand or
            BodyRegion.RightHand or
            BodyRegion.LeftLeg or
            BodyRegion.RightLeg or
            BodyRegion.LeftFoot or
            BodyRegion.RightFoot => LimbSeveranceThreshold,
            _ => FixedPoint2.Zero,
        };
    }
}
