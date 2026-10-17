using Content.Shared._RMC14.Attachable.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Attachable.Components;

[RegisterComponent, NetworkedComponent]
[Access(typeof(AttachableToggleableSystem))]
public sealed partial class AttachableDirectionLockedComponent : Component
{
    [DataField]
    public List<EntityUid> AttachableList = new();

    [DataField]
    public Direction? LockedDirection;
}

