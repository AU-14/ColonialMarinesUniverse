using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Traits.BadLeg;

/// <summary>
///     An old leg wound that never healed right. Walking without a cane strains it, and once the
///     strain builds up far enough the leg gives out and they fall.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUBadLegComponent : Component
{
    /// <summary>
    ///     Strain built up so far; the leg gives out at <see cref="MaxStrain"/>.
    /// </summary>
    [ViewVariables]
    public float Strain;

    /// <summary>
    ///     Strain added per tile walked without a cane.
    /// </summary>
    [DataField]
    public float StrainPerTile = 1f;

    /// <summary>
    ///     About how many tiles they can walk without a cane before falling.
    /// </summary>
    [DataField]
    public float MaxStrain = 60f;

    /// <summary>
    ///     Strain lost per second while resting the leg or walking with a cane.
    /// </summary>
    [DataField]
    public float RecoveryPerSecond = 1.5f;

    [DataField]
    public TimeSpan KnockdownTime = TimeSpan.FromSeconds(4);

    /// <summary>
    ///     Which warning (0 none, 1 aching, 2 about to give out) they were last shown, so each is
    ///     only shown once per build-up.
    /// </summary>
    [ViewVariables]
    public int WarningsShown;
}
