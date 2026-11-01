using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;

// ReSharper disable CheckNamespace
namespace Content.Shared._RMC14.Armor;

public sealed partial class CMArmorSystem
{
    // same zone check the armor gathering uses, so CMU can tell which worn pieces counted for a hit
    public bool AppliesTo(CMArmorComponent armor, BodyPartType? targetPart, TargetBodyZone? targetZone)
    {
        return ShouldApplyArmor(armor, targetPart, targetZone);
    }

    // mirrors OnGetExplosionResistance, every worn piece divides the explosion coefficient by this
    public static float ExplosionResistFor(int explosionArmor)
    {
        return explosionArmor <= 0 ? 1f : (float) Math.Pow(1.1, explosionArmor / 5.0);
    }
}
