using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Weapons.Ranged.AimedShot.FocusedShooting;

// CMU14 Begin: explicit state handling safely resolves references that outlive their entities.
[RegisterComponent, NetworkedComponent]
public sealed partial class RMCFocusedShootingComponent : Component
{
    /// <summary>
    ///     The current entity being focused on.
    /// </summary>
    [DataField]
    public EntityUid? CurrentTarget;

    /// <summary>
    ///     How many focus stacks the gun currently has.
    /// </summary>
    [DataField]
    public int FocusCounter;

    /// <summary>
    ///     The value the bonus damage is multiplied by per focusCounter.
    /// </summary>
    [DataField]
    public float FocusMultiplier = 0.25f;

    /// <summary>
    ///     The multiplier applied to the bonus damage at 0 focus stacks.
    /// </summary>
    [DataField]
    public float BaseFocusMultiplier = 0.25f;

    /// <summary>
    ///     The multiplier to calculate a small xeno's current health damage.
    /// </summary>
    [DataField]
    public float CurrentHealthDamageSmallXeno = 0.1f;

    /// <summary>
    ///     The multiplier to calculate a normal sized xeno's current health damage.
    /// </summary>
    [DataField]
    public float CurrentHealthDamageXeno = 0.2f;

    /// <summary>
    ///     The multiplier to calculate a big xeno's current health damage.
    /// </summary>
    [DataField]
    public float CurrentHealthDamageBigXeno = 0.3f;

    /// <summary>
    ///     The multiplier applied to the bonus damage at 0 focus stacks.
    /// </summary>
    [DataField]
    public float BonusDamageNonXeno = 0.8f;

    /// <summary>
    ///     The bonus damage added based on the base damage of the projectile when hitting a normal sized xeno.
    /// </summary>
    [DataField]
    public float BonusDamageXeno = 0.6f;

    /// <summary>
    ///     The bonus damage added based on the base damage of the projectile when hitting a big xeno.
    /// </summary>
    [DataField]
    public float BonusDamageBigXeno;

    /// <summary>
    ///     The minimum stopping power needed to apply the daze effect.
    /// </summary>
    [DataField]
    public float DazeThreshold = 3f;

    /// <summary>
    ///     The duration of the daze effect.
    /// </summary>
    [DataField]
    public float DazeDuration = 0.2f;

    /// <summary>
    ///     The minimum stopping power needed to slow a target.
    /// </summary>
    [DataField]
    public float SlowThreshold = 2f;

    /// <summary>
    ///     The color of the laser when focused.
    /// </summary>
    [DataField]
    public Color LaserColor = Color.Blue;
}
// CMU14 End
