using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.Damage;

public enum DamageImpactDelivery : byte
{
    Unspecified,
    Generic,
    Melee,
    Projectile,
    Thrown,
    Contact,
    Environment,
    Explosion,
}

public enum DamageImpactContact : byte
{
    Unspecified,
    Generic,
    Slash,
    Stab,
    Crush,
    Snag,
    Fragment,
    Burn,
    Blast,
}

public enum DamageImpactPenetration : byte
{
    Unspecified,
    None,
    Low,
    Medium,
    High,
    Forced,
}

public enum DamageImpactEnergy : byte
{
    Unspecified,
    Low,
    Medium,
    High,
    Severe,
}

public readonly record struct DamageImpact(
    DamageImpactDelivery Delivery,
    DamageImpactContact Contact,
    DamageImpactPenetration Penetration,
    DamageImpactEnergy Energy)
{
    private static readonly FixedPoint2 MediumEnergyThreshold = FixedPoint2.New(20);
    private static readonly FixedPoint2 HighEnergyThreshold = FixedPoint2.New(45);

    public bool IsSpecified =>
        Delivery != DamageImpactDelivery.Unspecified ||
        Contact != DamageImpactContact.Unspecified ||
        Penetration != DamageImpactPenetration.Unspecified ||
        Energy != DamageImpactEnergy.Unspecified;

    public static DamageImpact Generic => default;

    public static DamageImpact Projectile =>
        new(DamageImpactDelivery.Projectile, DamageImpactContact.Stab, DamageImpactPenetration.High, DamageImpactEnergy.High);

    public static DamageImpact Explosion =>
        new(DamageImpactDelivery.Explosion, DamageImpactContact.Blast, DamageImpactPenetration.Forced, DamageImpactEnergy.Severe);

    public static DamageImpact MeleeSlash =>
        new(DamageImpactDelivery.Melee, DamageImpactContact.Slash, DamageImpactPenetration.Low, DamageImpactEnergy.Medium);

    public static DamageImpact SnaggingContact =>
        new(DamageImpactDelivery.Contact, DamageImpactContact.Snag, DamageImpactPenetration.None, DamageImpactEnergy.Low);

    public static DamageImpact ForMelee(DamageSpecifier damage, bool heavy = false)
    {
        var contact = GetDominantContact(damage);
        var penetration = contact switch
        {
            DamageImpactContact.Stab => DamageImpactPenetration.Medium,
            DamageImpactContact.Crush => DamageImpactPenetration.None,
            DamageImpactContact.Burn => DamageImpactPenetration.None,
            _ => DamageImpactPenetration.Low,
        };

        return new DamageImpact(
            DamageImpactDelivery.Melee,
            contact,
            penetration,
            GetEnergy(damage, heavy));
    }

    public static DamageImpact ForThrown(DamageSpecifier damage)
    {
        var contact = GetDominantContact(damage);
        if (contact == DamageImpactContact.Slash)
            contact = DamageImpactContact.Fragment;

        var energy = GetEnergy(damage);
        var penetration = contact switch
        {
            DamageImpactContact.Stab => energy >= DamageImpactEnergy.High
                ? DamageImpactPenetration.Medium
                : DamageImpactPenetration.Low,
            DamageImpactContact.Fragment => DamageImpactPenetration.Low,
            DamageImpactContact.Crush => DamageImpactPenetration.None,
            DamageImpactContact.Burn => DamageImpactPenetration.None,
            _ => DamageImpactPenetration.Low,
        };

        return new DamageImpact(DamageImpactDelivery.Thrown, contact, penetration, energy);
    }

    public static DamageImpact ForContact(DamageSpecifier damage)
    {
        var contact = GetDominantContact(damage);
        if (contact == DamageImpactContact.Slash)
            contact = DamageImpactContact.Snag;

        var penetration = contact switch
        {
            DamageImpactContact.Burn => DamageImpactPenetration.None,
            DamageImpactContact.Crush => DamageImpactPenetration.None,
            DamageImpactContact.Snag => DamageImpactPenetration.None,
            _ => DamageImpactPenetration.Low,
        };

        return new DamageImpact(DamageImpactDelivery.Contact, contact, penetration, GetEnergy(damage));
    }

    public static DamageImpact XenoRendingSlash(int tier)
    {
        var penetration = tier >= 3
            ? DamageImpactPenetration.High
            : tier >= 2
                ? DamageImpactPenetration.Medium
                : DamageImpactPenetration.Low;

        return new DamageImpact(DamageImpactDelivery.Melee, DamageImpactContact.Slash, penetration, DamageImpactEnergy.High);
    }

    public DamageImpact WithMinimumPenetration(DamageImpactPenetration penetration)
    {
        if (Penetration == DamageImpactPenetration.Unspecified || Penetration < penetration)
            return this with { Penetration = penetration };

        return this;
    }

    public DamageImpact WithMinimumEnergy(DamageImpactEnergy energy)
    {
        if (Energy == DamageImpactEnergy.Unspecified || Energy < energy)
            return this with { Energy = energy };

        return this;
    }

    public DamageImpact FillUnspecifiedFrom(DamageImpact fallback)
    {
        return new DamageImpact(
            Delivery == DamageImpactDelivery.Unspecified ? fallback.Delivery : Delivery,
            Contact == DamageImpactContact.Unspecified ? fallback.Contact : Contact,
            Penetration == DamageImpactPenetration.Unspecified ? fallback.Penetration : Penetration,
            Energy == DamageImpactEnergy.Unspecified ? fallback.Energy : Energy);
    }

    private static DamageImpactContact GetDominantContact(DamageSpecifier damage)
    {
        var piercing = GetPositive(damage, "Piercing");
        var slash = GetPositive(damage, "Slash");
        var blunt = GetPositive(damage, "Blunt");
        var heat = GetPositive(damage, "Heat") + GetPositive(damage, "Caustic") + GetPositive(damage, "Shock");

        if (heat > FixedPoint2.Zero && heat >= piercing && heat >= slash && heat >= blunt)
            return DamageImpactContact.Burn;
        if (piercing > FixedPoint2.Zero && piercing > slash && piercing > blunt)
            return DamageImpactContact.Stab;
        if (slash > FixedPoint2.Zero && slash >= blunt)
            return DamageImpactContact.Slash;
        if (blunt > FixedPoint2.Zero)
            return DamageImpactContact.Crush;

        return DamageImpactContact.Generic;
    }

    private static DamageImpactEnergy GetEnergy(DamageSpecifier damage, bool heavy = false)
    {
        var total = damage.GetTotal();
        if (total >= HighEnergyThreshold)
            return DamageImpactEnergy.High;
        if (heavy || total >= MediumEnergyThreshold)
            return DamageImpactEnergy.Medium;

        return DamageImpactEnergy.Low;
    }

    private static FixedPoint2 GetPositive(DamageSpecifier damage, string type)
        => damage.DamageDict.TryGetValue(type, out var amount) && amount > FixedPoint2.Zero
            ? amount
            : FixedPoint2.Zero;
}

public readonly record struct DamageInstance(
    DamageSpecifier Damage,
    EntityUid? Origin = null,
    EntityUid? Tool = null,
    DamageImpact Impact = default,
    int ArmorPiercing = 0);

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class DamageImpactProfile
{
    [DataField]
    public DamageImpactDelivery Delivery = DamageImpactDelivery.Unspecified;

    [DataField]
    public DamageImpactContact Contact = DamageImpactContact.Unspecified;

    [DataField]
    public DamageImpactPenetration Penetration = DamageImpactPenetration.Unspecified;

    [DataField]
    public DamageImpactEnergy Energy = DamageImpactEnergy.Unspecified;

    public bool IsSpecified =>
        Delivery != DamageImpactDelivery.Unspecified ||
        Contact != DamageImpactContact.Unspecified ||
        Penetration != DamageImpactPenetration.Unspecified ||
        Energy != DamageImpactEnergy.Unspecified;

    public DamageImpact ApplyTo(DamageImpact fallback, DamageImpactDelivery delivery)
    {
        if (!IsSpecified)
            return fallback;

        var profile = new DamageImpact(Delivery, Contact, Penetration, Energy)
            .FillUnspecifiedFrom(fallback);

        return profile with { Delivery = delivery };
    }
}
