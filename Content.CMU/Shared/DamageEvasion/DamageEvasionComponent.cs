using Content.Shared._RMC14.Evasion;
using Content.Shared.FixedPoint;

namespace Content.Shared.CMU;

[RegisterComponent]
[Access(typeof(DamageEvasionSystem), typeof(EvasionSystem))]
public sealed partial class DamageEvasionComponent : Component
{
    /// <summary>
    /// Amount of damage that must be taken within the damage window to activate evasion.
    /// </summary>
    [DataField]
    public FixedPoint2 DamageThreshold = 200;

    /// <summary>
    /// Amount of time over which damage is accumulated toward the threshold.
    /// </summary>
    [DataField]
    public TimeSpan DamageWindow = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Amount of evasion granted when the damage threshold is reached.
    /// </summary>
    [DataField]
    public FixedPoint2 EvasionBonus = 75;

    /// <summary>
    /// Amount of time the temporary evasion bonus lasts.
    /// </summary>
    [DataField]
    public TimeSpan EvasionDuration = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Amount of damage taken during the current damage window.
    /// </summary>
    public FixedPoint2 AccumulatedDamage;

    /// <summary>
    /// When the current damage window began.
    /// </summary>
    public TimeSpan DamageWindowStarted;

    /// <summary>
    /// When the temporary evasion bonus expires.
    /// </summary>
    public TimeSpan EvasionExpires;

    /// <summary>
    /// Whether the temporary evasion bonus is currently active.
    /// </summary>
    public bool EvasionActive;
}
