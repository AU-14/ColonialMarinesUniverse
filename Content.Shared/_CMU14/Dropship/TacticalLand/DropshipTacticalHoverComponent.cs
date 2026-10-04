using System;
using System.Collections.Generic;
using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Dropship.TacticalLand;

[RegisterComponent]
public sealed partial class DropshipTacticalHoverComponent : Component
{
    [DataField]
    public EntityUid? ReturnDestination;

    [DataField]
    public EntityUid? HoverDestination;

    [DataField]
    public TimeSpan ReturnAt;

    [DataField]
    public TimeSpan NextReturnAttempt;

    [DataField]
    public Vector2i Footprint = new(9, 17);

    [DataField]
    public int GroundMapOffset = -1;

    [DataField]
    public EntityUid? Shadow;

    [DataField]
    public List<EntityUid> Downwashes = new();

    [DataField]
    public EntProtoId ShadowPrototype = "CMUDropshipTacticalHoverShadow";

    [DataField]
    public EntProtoId DownwashPrototype = "CMUDropshipTacticalHoverDownwash";
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DropshipTacticalHoverShadowComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Dropship;

    [DataField, AutoNetworkedField]
    public Vector2i Footprint = new(9, 17);

    [DataField, AutoNetworkedField]
    public int ProjectedMapOffset = -1;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DropshipTacticalHoverDownwashComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Dropship;

    [DataField, AutoNetworkedField]
    public Vector2 Offset;

    [DataField, AutoNetworkedField]
    public int ProjectedMapOffset = -1;
}
