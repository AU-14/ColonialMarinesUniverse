using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Threats.Mobs.CLF;

/// <summary>
///     Temporary component placed on an entity that is being offered a CLF tattoo.
///     Stores references to the tattoo artist and tattoo gun for the accept handler.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TattooPendingComponent : Component
{
    public EntityUid TattooGun;
    public EntityUid User;
}
