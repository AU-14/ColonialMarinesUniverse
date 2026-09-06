using Robust.Shared.GameStates;
using Robust.Shared.Maths;

namespace Content.Shared._CMU14.Dropship.TacticalLand;

[RegisterComponent, NetworkedComponent]
public sealed partial class EphemeralDropshipDestinationComponent : Component
{
    [DataField]
    public bool TacticalHover;

    [DataField]
    public Vector2i Footprint;
}
