using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;

namespace Content.Shared._CMU14.Medical.Trauma;

public enum CMUTraumaMechanism : byte
{
    Generic,
    Ballistic,
    Slash,
    Pierce,
    Blunt,
    Explosive,
}

public enum CMUTraumaDepth : byte
{
    Graze,
    SoftTissue,
    Bone,
    Deep,
    Severe,
}

public readonly record struct CMUTraumaContactResult(
    CMUTraumaMechanism Mechanism,
    CMUTraumaDepth Depth,
    bool BoneContact,
    bool OrganContact,
    bool VascularContact,
    float OrganPassThrough,
    float InternalBleedRate,
    bool HighEnergy)
{
    public static CMUTraumaContactResult SoftTissue(CMUTraumaMechanism mechanism)
        => new(mechanism, CMUTraumaDepth.SoftTissue, false, false, false, 0f, 0f, false);
}

public readonly record struct CMUTraumaContactSettings
{
    public FixedPoint2 BallisticHighDamageThreshold { get; init; }
    public FixedPoint2 MeleeHighDamageThreshold { get; init; }

    public float BallisticHeadBoneChance { get; init; }
    public float BallisticTorsoBoneChance { get; init; }
    public float BallisticArmBoneChance { get; init; }
    public float BallisticLegBoneChance { get; init; }
    public float BallisticOtherBoneChance { get; init; }
    public float BallisticHeadOrganChance { get; init; }
    public float BallisticTorsoOrganChance { get; init; }
    public float BallisticVascularChance { get; init; }

    public float PierceBoneChance { get; init; }
    public float PierceOrganChance { get; init; }
    public float PierceVascularChance { get; init; }

    public float SlashBoneChance { get; init; }
    public float SlashOrganChance { get; init; }
    public float SlashVascularChance { get; init; }

    public float BluntBoneChance { get; init; }
    public float BluntOrganChance { get; init; }
    public float BluntVascularChance { get; init; }

    public float BallisticOrganPassThrough { get; init; }
    public float PierceOrganPassThrough { get; init; }
    public float SlashOrganPassThrough { get; init; }
    public float BluntOrganPassThrough { get; init; }
    public float HighEnergyOrganPassThrough { get; init; }
    public float ExplosiveOrganPassThrough { get; init; }

    public float BallisticInternalBleedRate { get; init; }
    public float PierceInternalBleedRate { get; init; }
    public float SlashInternalBleedRate { get; init; }
    public float BluntInternalBleedRate { get; init; }

    public static CMUTraumaContactSettings Default => new()
    {
        BallisticHighDamageThreshold = FixedPoint2.New(45),
        MeleeHighDamageThreshold = FixedPoint2.New(45),

        BallisticHeadBoneChance = 0.65f,
        BallisticTorsoBoneChance = 0.30f,
        BallisticArmBoneChance = 0.60f,
        BallisticLegBoneChance = 0.60f,
        BallisticOtherBoneChance = 0.35f,
        BallisticHeadOrganChance = 0.08f,
        BallisticTorsoOrganChance = 0.25f,
        BallisticVascularChance = 0.03f,

        PierceBoneChance = 0.20f,
        PierceOrganChance = 0.175f,
        PierceVascularChance = 0.04f,

        SlashBoneChance = 0.10f,
        SlashOrganChance = 0.10f,
        SlashVascularChance = 0.05f,

        BluntBoneChance = 0.50f,
        BluntOrganChance = 0.05f,
        BluntVascularChance = 0.02f,

        BallisticOrganPassThrough = 0.35f,
        PierceOrganPassThrough = 0.30f,
        SlashOrganPassThrough = 0.20f,
        BluntOrganPassThrough = 0.15f,
        HighEnergyOrganPassThrough = 0.50f,
        ExplosiveOrganPassThrough = 1.0f,

        BallisticInternalBleedRate = 0.25f,
        PierceInternalBleedRate = 0.30f,
        SlashInternalBleedRate = 0.25f,
        BluntInternalBleedRate = 0.20f,
    };
}

public static class CMUTraumaContactModel
{
    public static CMUTraumaContactResult Create(
        CMUTraumaMechanism mechanism,
        BodyPartType partType,
        FixedPoint2 bruteDamage,
        bool hasOrgans,
        float roll,
        CMUTraumaContactSettings settings)
    {
        return Create(mechanism, default, partType, bruteDamage, hasOrgans, roll, settings);
    }

    public static CMUTraumaContactResult Create(
        CMUTraumaMechanism mechanism,
        DamageImpact impact,
        BodyPartType partType,
        FixedPoint2 bruteDamage,
        bool hasOrgans,
        float roll,
        CMUTraumaContactSettings settings)
    {
        roll = Math.Clamp(roll, 0f, 1f);

        if (mechanism == CMUTraumaMechanism.Explosive)
        {
            return new CMUTraumaContactResult(
                mechanism,
                CMUTraumaDepth.Severe,
                true,
                hasOrgans,
                false,
                hasOrgans ? settings.ExplosiveOrganPassThrough : 0f,
                0f,
                true);
        }

        if (bruteDamage <= FixedPoint2.Zero || mechanism == CMUTraumaMechanism.Generic)
            return CMUTraumaContactResult.SoftTissue(mechanism);

        if (IsHighEnergy(mechanism, impact, bruteDamage, settings))
        {
            return new CMUTraumaContactResult(
                mechanism,
                CMUTraumaDepth.Severe,
                true,
                hasOrgans,
                false,
                hasOrgans ? settings.HighEnergyOrganPassThrough : 0f,
                0f,
                true);
        }

        var bone = roll < Chance(GetBoneChance(mechanism, impact, partType, settings));
        var organ = hasOrgans && roll < Chance(GetOrganChance(mechanism, impact, partType, settings));
        var vascular = roll < Chance(GetVascularChance(mechanism, impact, settings));

        var depth = CMUTraumaDepth.SoftTissue;
        if (bone)
            depth = CMUTraumaDepth.Bone;
        if (organ || vascular)
            depth = CMUTraumaDepth.Deep;

        return new CMUTraumaContactResult(
            mechanism,
            depth,
            bone,
            organ,
            vascular,
            organ ? GetOrganPassThrough(mechanism, impact, settings) : 0f,
            vascular ? GetInternalBleedRate(mechanism, impact, settings) : 0f,
            false);
    }

    private static bool IsHighEnergy(
        CMUTraumaMechanism mechanism,
        DamageImpact impact,
        FixedPoint2 bruteDamage,
        CMUTraumaContactSettings settings)
    {
        if (impact.IsSpecified && impact.Contact is DamageImpactContact.Burn or DamageImpactContact.Snag)
            return false;

        if (impact is { Delivery: DamageImpactDelivery.Explosion } ||
            impact is { Energy: DamageImpactEnergy.Severe })
        {
            return true;
        }

        var threshold = mechanism == CMUTraumaMechanism.Ballistic
            ? settings.BallisticHighDamageThreshold
            : settings.MeleeHighDamageThreshold;

        return threshold > FixedPoint2.Zero && bruteDamage >= threshold;
    }

    private static float GetBoneChance(
        CMUTraumaMechanism mechanism,
        DamageImpact impact,
        BodyPartType partType,
        CMUTraumaContactSettings settings)
    {
        if (mechanism == CMUTraumaMechanism.Ballistic)
        {
            return partType switch
            {
                BodyPartType.Head => settings.BallisticHeadBoneChance,
                BodyPartType.Torso => settings.BallisticTorsoBoneChance,
                BodyPartType.Arm or BodyPartType.Hand => settings.BallisticArmBoneChance,
                BodyPartType.Leg or BodyPartType.Foot => settings.BallisticLegBoneChance,
                _ => settings.BallisticOtherBoneChance,
            };
        }

        var chance = mechanism switch
        {
            CMUTraumaMechanism.Pierce => settings.PierceBoneChance,
            CMUTraumaMechanism.Slash => settings.SlashBoneChance,
            CMUTraumaMechanism.Blunt => settings.BluntBoneChance,
            _ => 0f,
        };

        chance *= GetBoneDepthMultiplier(mechanism, impact);

        return partType switch
        {
            BodyPartType.Head => chance * 1.25f,
            BodyPartType.Arm or BodyPartType.Hand => chance * 1.15f,
            BodyPartType.Leg or BodyPartType.Foot => chance * 1.15f,
            _ => chance,
        };
    }

    private static float GetOrganChance(
        CMUTraumaMechanism mechanism,
        DamageImpact impact,
        BodyPartType partType,
        CMUTraumaContactSettings settings)
    {
        if (mechanism == CMUTraumaMechanism.Ballistic)
        {
            return partType switch
            {
                BodyPartType.Head => settings.BallisticHeadOrganChance,
                BodyPartType.Torso => settings.BallisticTorsoOrganChance,
                _ => 0f,
            };
        }

        if (partType is not (BodyPartType.Head or BodyPartType.Torso))
            return 0f;

        var chance = mechanism switch
        {
            CMUTraumaMechanism.Pierce => settings.PierceOrganChance,
            CMUTraumaMechanism.Slash => settings.SlashOrganChance,
            CMUTraumaMechanism.Blunt => settings.BluntOrganChance,
            _ => 0f,
        };

        return chance * GetOrganDepthMultiplier(mechanism, impact);
    }

    private static float GetVascularChance(CMUTraumaMechanism mechanism, DamageImpact impact, CMUTraumaContactSettings settings)
        => mechanism switch
        {
            CMUTraumaMechanism.Ballistic => settings.BallisticVascularChance,
            CMUTraumaMechanism.Pierce => settings.PierceVascularChance,
            CMUTraumaMechanism.Slash => settings.SlashVascularChance,
            CMUTraumaMechanism.Blunt => settings.BluntVascularChance,
            _ => 0f,
        } * GetVascularDepthMultiplier(mechanism, impact);

    private static float GetOrganPassThrough(CMUTraumaMechanism mechanism, DamageImpact impact, CMUTraumaContactSettings settings)
        => Chance((mechanism switch
        {
            CMUTraumaMechanism.Ballistic => settings.BallisticOrganPassThrough,
            CMUTraumaMechanism.Pierce => settings.PierceOrganPassThrough,
            CMUTraumaMechanism.Slash => settings.SlashOrganPassThrough,
            CMUTraumaMechanism.Blunt => settings.BluntOrganPassThrough,
            _ => 0f,
        }) * GetOrganPassThroughMultiplier(mechanism, impact));

    private static float GetInternalBleedRate(CMUTraumaMechanism mechanism, DamageImpact impact, CMUTraumaContactSettings settings)
        => Chance((mechanism switch
        {
            CMUTraumaMechanism.Ballistic => settings.BallisticInternalBleedRate,
            CMUTraumaMechanism.Pierce => settings.PierceInternalBleedRate,
            CMUTraumaMechanism.Slash => settings.SlashInternalBleedRate,
            CMUTraumaMechanism.Blunt => settings.BluntInternalBleedRate,
            _ => 0f,
        }) * GetVascularDepthMultiplier(mechanism, impact));

    private static float GetBoneDepthMultiplier(CMUTraumaMechanism mechanism, DamageImpact impact)
    {
        if (!impact.IsSpecified || mechanism == CMUTraumaMechanism.Ballistic)
            return 1f;

        if (impact.Contact is DamageImpactContact.Burn or DamageImpactContact.Snag)
            return 0f;

        return impact.Penetration switch
        {
            DamageImpactPenetration.None => impact.Contact == DamageImpactContact.Crush ? 1f : 0f,
            DamageImpactPenetration.Medium => 1.25f,
            DamageImpactPenetration.High => 1.5f,
            DamageImpactPenetration.Forced => 2f,
            _ => 1f,
        };
    }

    private static float GetOrganDepthMultiplier(CMUTraumaMechanism mechanism, DamageImpact impact)
    {
        if (!impact.IsSpecified || mechanism == CMUTraumaMechanism.Ballistic)
            return 1f;

        if (impact.Contact is DamageImpactContact.Burn or DamageImpactContact.Snag)
            return 0f;

        return impact.Penetration switch
        {
            DamageImpactPenetration.None => 0f,
            DamageImpactPenetration.Low => impact.Contact switch
            {
                DamageImpactContact.Slash => 0.25f,
                DamageImpactContact.Fragment => 0.35f,
                _ => 0.5f,
            },
            DamageImpactPenetration.Medium => 1.25f,
            DamageImpactPenetration.High => 1.75f,
            DamageImpactPenetration.Forced => 2f,
            _ => 1f,
        };
    }

    private static float GetVascularDepthMultiplier(CMUTraumaMechanism mechanism, DamageImpact impact)
    {
        if (!impact.IsSpecified || mechanism == CMUTraumaMechanism.Ballistic)
            return 1f;

        if (impact.Contact is DamageImpactContact.Burn or DamageImpactContact.Snag)
            return 0f;

        return impact.Penetration switch
        {
            DamageImpactPenetration.None => 0f,
            DamageImpactPenetration.Low => 0.3f,
            DamageImpactPenetration.Medium => 1.25f,
            DamageImpactPenetration.High => 1.75f,
            DamageImpactPenetration.Forced => 2f,
            _ => 1f,
        };
    }

    private static float GetOrganPassThroughMultiplier(CMUTraumaMechanism mechanism, DamageImpact impact)
    {
        if (!impact.IsSpecified)
            return 1f;

        if (mechanism == CMUTraumaMechanism.Ballistic)
        {
            return impact.Penetration switch
            {
                DamageImpactPenetration.Medium => 1.15f,
                DamageImpactPenetration.High => 1.25f,
                DamageImpactPenetration.Forced => 1.35f,
                _ => 1f,
            };
        }

        return impact.Penetration switch
        {
            DamageImpactPenetration.Low => 0.5f,
            DamageImpactPenetration.Medium => 1.25f,
            DamageImpactPenetration.High => 1.5f,
            DamageImpactPenetration.Forced => 2f,
            _ => 1f,
        };
    }

    private static float Chance(float chance)
        => Math.Clamp(chance, 0f, 1f);
}
