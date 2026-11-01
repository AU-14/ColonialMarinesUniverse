using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Weapons.CookOff;

/// <summary>
/// Added at runtime to a mortar shell or ammo box that caught fire, ported from cmss13-devs/cmss13#6243.
/// It can't be picked up or emptied while it burns, and explodes once the timer runs out.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUCookingOffComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ExplodeAt;

    /// <summary>
    /// How full the ammo box was when it caught fire, from 0 to 1. Scales the blast.
    /// Unused for mortar shells, which use their own warhead.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Fill = 1;
}
