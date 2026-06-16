using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Xenonids.ZJump;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUXenoZJumpSystem))]
public sealed partial class CMUXenoZJumpComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId ActionId = "CMUActionXenoZJump";

    [DataField, AutoNetworkedField]
    public EntityUid? Action;

    [DataField, AutoNetworkedField]
    public float Range = 7f;

    [DataField, AutoNetworkedField]
    public float TakeoffDashDistance = 0.75f;

    [DataField, AutoNetworkedField]
    public float TakeoffDashSpeed = 4f;

    [DataField, AutoNetworkedField]
    public float ZVelocity = 7.5f;

    [DataField, AutoNetworkedField]
    public TimeSpan Windup = TimeSpan.FromSeconds(0.35);

    [DataField, AutoNetworkedField]
    public FixedPoint2 PlasmaCost = 50;

    [DataField]
    public LocId NotXenoPopup = "cmu-xeno-zjump-fail-not-xeno";

    [DataField]
    public LocId NoZPhysicsPopup = "cmu-xeno-zjump-fail-no-z-physics";

    [DataField]
    public LocId CancelledPopup = "cmu-xeno-zjump-cancelled";
}
