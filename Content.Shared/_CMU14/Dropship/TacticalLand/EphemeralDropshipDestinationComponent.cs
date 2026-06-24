using System;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;

namespace Content.Shared._CMU14.Dropship.TacticalLand;

[RegisterComponent, NetworkedComponent]
public sealed partial class EphemeralDropshipDestinationComponent : Component
{
    [DataField]
    public bool TacticalHover;

    [DataField]
    public EntityUid? ReturnDestination;

    [DataField]
    public TimeSpan MaxHoverTime = TimeSpan.FromMinutes(3);

    [DataField]
    public Vector2i Footprint;
}
