using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Atmos;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedRMCFlammableSystem), Other = AccessPermissions.Read)]
public sealed partial class OnFireComponent : Component
{
    /// <summary>
    /// RMC fire intensity used by the server damage formula while this entity is burning.
    /// </summary>
    [DataField]
    public int Intensity;

    /// <summary>
    /// RMC fire duration used to normalize stack-based damage while this entity is burning.
    /// </summary>
    [DataField]
    public int Duration;

    /// <summary>
    /// Fire stacks removed by one stop-drop-roll resist tick.
    /// </summary>
    [DataField]
    public int ResistStacks = -10;
}
