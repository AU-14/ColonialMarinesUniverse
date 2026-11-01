using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Doors;

/// <summary>
///     An airlock that bolts itself while it's shut against unsafe air, using the same test as a
///     <see cref="CMUFirelockSealComponent"/> firelock, and unbolts once the air on every side is safe again. Bolts
///     someone sets or lifts by hand are left alone.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUAtmosBoltComponent : Component
{
    /// <summary>Below this pressure, in kPa, on either side, the door bolts.</summary>
    [DataField]
    public float LowPressure = 50f;

    /// <summary>A pressure difference across the door bigger than this, in kPa, means air would escape.</summary>
    [DataField]
    public float PressureDifference = 20f;

    /// <summary>
    ///     Whether the bolts are down because of the air, so they come back up when it's safe. Examining the door shows
    ///     the pressure warning while this is set.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool BoltedByAtmos;

    /// <summary>Someone lifted the bolts by hand while the air was bad; they stay up until it's safe again.</summary>
    [DataField]
    public bool Overridden;
}
