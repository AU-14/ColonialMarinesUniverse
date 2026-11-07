using Content.Shared.FixedPoint;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;

// ECS-01/ECS-03 (W7, node N10): the xenofauna branches of the Joe's existing close-quarters moves
// (0A, 1B, 2A, 2B). Each is granted by the level table (EWJoe.yml) and applied by the server's
// EWJoeCombatSystem (EWJoeCombatSystem.Xeno.cs); without it the matching move behaves as it always has.
// All of them act only on xenofauna (EWJoeTargetRules.IsXenofauna); humans see no change. The other W7
// abilities (0B, 1A, 3A, 3B, 4A, 4B, 5A, 5B) live in EWJoeXenoKitComponents.cs.

/// <summary>
/// W7 0A Industrial Grip (passive, level 0): the Joe's unarmed hits deal <see cref="BonusMultiplier"/>
/// of their base damage again to xenofauna and slow them for <see cref="Slow"/>. Xenos have no stamina,
/// so the level-0 stamina punch does nothing to them; this is what they feel instead.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeIndustrialGripComponent : Component
{
    /// <summary>Extra damage against xenofauna, as a fraction of the punch's base damage (0.5 = +50%).</summary>
    [DataField]
    public float BonusMultiplier = 0.5f;

    [DataField]
    public TimeSpan Slow = TimeSpan.FromSeconds(1);
}

/// <summary>
/// W7 1B Overpressure (level 1): Drown used on xenofauna is a quick burst instead of a refusal. No
/// grab and no hold: <see cref="Damage"/> Blunt and a <see cref="KnockbackDistance"/>-tile shove, on
/// Drown's own cooldown. Large castes take the damage but are not moved.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeOverpressureComponent : Component
{
    [DataField]
    public float Damage = 40f;

    [DataField]
    public float KnockbackDistance = 2f;

    [DataField]
    public float KnockbackSpeed = 10f;
}

/// <summary>
/// W7 2A Chitin Crack (level 2): Slam on xenofauna also cracks its carapace, taking
/// <see cref="ArmorShred"/> off its xeno armor for <see cref="Duration"/>
/// (<see cref="EWJoeArmorShredComponent"/>).
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeChitinCrackComponent : Component
{
    [DataField]
    public int ArmorShred = 10;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(6);
}

/// <summary>
/// W7 2B Pin (level 2): Choke on grabbed xenofauna becomes a pin. Each second of the hold roots the
/// target for <see cref="RootDuration"/> (so it stays rooted while held and for a moment after) and
/// deals <see cref="DamagePerSecond"/> Blunt, instead of the stamina drain xenos ignore. The pin breaks
/// once the Joe has taken <see cref="BreakDamage"/> while holding it.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoePinComponent : Component
{
    [DataField]
    public TimeSpan RootDuration = TimeSpan.FromSeconds(1.5);

    [DataField]
    public float DamagePerSecond = 10f;

    [DataField]
    public FixedPoint2 BreakDamage = 60;
}

/// <summary>A running pin on the Joe (added with the choke, removed with it).</summary>
[RegisterComponent]
public sealed partial class EWJoePinningComponent : Component
{
    [DataField]
    public EntityUid Target;

    /// <summary>Damage the Joe has taken since the pin began.</summary>
    [DataField]
    public FixedPoint2 DamageTaken;
}
