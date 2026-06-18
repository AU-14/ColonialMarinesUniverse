using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared.FixedPoint;

namespace Content.Shared._CMU14.Medical.Human.Rules;

public static class LimbLossRules
{
    public static bool CanTraumaticallySever(BodyRegion region)
    {
        return region is
            BodyRegion.Head or
            BodyRegion.LeftArm or
            BodyRegion.RightArm or
            BodyRegion.LeftHand or
            BodyRegion.RightHand or
            BodyRegion.LeftLeg or
            BodyRegion.RightLeg or
            BodyRegion.LeftFoot or
            BodyRegion.RightFoot;
    }

    public static MedicalTransaction CreateTraumaticSeverance(
        BodyRegion region,
        BleedKind bleedKind,
        FixedPoint2 bleedRate,
        BleedFlags bleedFlags = BleedFlags.None)
    {
        var stumpRegion = GetStumpAnchorRegion(region);
        var transaction = new MedicalTransaction(stumpRegion != BodyRegion.None ? stumpRegion : region);

        if (!CanTraumaticallySever(region))
            return transaction;

        transaction.Add(MedicalEffect.SetRegionPresence(region, LimbPresence.Missing));
        if (stumpRegion != BodyRegion.None)
        {
            transaction.Add(MedicalEffect.AddInjury(
                stumpRegion,
                InjuryKind.Stump,
                InjuryStage.Stump,
                FixedPoint2.Zero));
            transaction.Add(MedicalEffect.AddBleedSource(stumpRegion, bleedKind, bleedRate, flags: bleedFlags));
        }

        transaction.Add(MedicalEffect.AddDetachedLimb(region));
        return transaction;
    }

    public static BodyRegion GetStumpAnchorRegion(BodyRegion severedRegion)
    {
        return severedRegion switch
        {
            BodyRegion.Head => BodyRegion.Chest,
            BodyRegion.LeftArm => BodyRegion.LeftArm,
            BodyRegion.RightArm => BodyRegion.RightArm,
            BodyRegion.LeftHand => BodyRegion.LeftArm,
            BodyRegion.RightHand => BodyRegion.RightArm,
            BodyRegion.LeftLeg => BodyRegion.LeftLeg,
            BodyRegion.RightLeg => BodyRegion.RightLeg,
            BodyRegion.LeftFoot => BodyRegion.LeftLeg,
            BodyRegion.RightFoot => BodyRegion.RightLeg,
            _ => BodyRegion.None,
        };
    }
}
