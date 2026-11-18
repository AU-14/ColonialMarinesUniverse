using Content.Shared.CMU14.Medical.Anatomy.Bones;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;

/// <summary>
/// ECS-05 (node N11): Desecrate's numbers, granted at level 2 with the action. A targeted punch:
/// heavy Blunt, a guaranteed fracture on the Joe's selected zone (falling back to the torso, then any
/// limb), and a knockback armed with the RMC obstacle slam. Boneless bodies (synthetics, xenofauna)
/// take <see cref="BonelessBonusDamage"/> instead of the fracture; xenofauna also lose armor.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeDesecrateComponent : Component
{
    [DataField]
    public float Damage = 50f;

    /// <summary>Extra Blunt for a body no fracture could be seeded on.</summary>
    [DataField]
    public float BonelessBonusDamage = 20f;

    [DataField]
    public FractureSeverity Severity = FractureSeverity.Simple;

    /// <summary>The fracture an empowered Desecrate (combo finisher) seeds.</summary>
    [DataField]
    public FractureSeverity EmpoweredSeverity = FractureSeverity.Compound;

    [DataField]
    public float KnockbackDistance = 2.5f;

    /// <summary>Same as the Joe's slam throw speed; RMC's impact damage scales with it.</summary>
    [DataField]
    public float KnockbackSpeed = 15f;

    /// <summary>Paralyze the RMC obstacle slam adds if the flight ends in a wall, table or barricade.</summary>
    [DataField]
    public TimeSpan SlamParalyze = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan SlamSlow = TimeSpan.FromSeconds(2);

    /// <summary>Armor shred on boneless xenofauna (the W7 2A Chitin Crack debuff).</summary>
    [DataField]
    public int XenoArmorShred = 10;

    [DataField]
    public TimeSpan XenoArmorShredDuration = TimeSpan.FromSeconds(6);
}
