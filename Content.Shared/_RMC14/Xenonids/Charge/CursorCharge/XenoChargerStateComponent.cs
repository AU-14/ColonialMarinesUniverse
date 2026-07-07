using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.Charge.CursorCharge;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class XenoChargerStateComponent : Component
{

    // --- StateComp stuff ---
    [DataField] [AutoNetworkedField] public XenoChargerMoveState MoveState = XenoChargerMoveState.Idle;
    [DataField] [AutoNetworkedField] public Angle TargetHeading = Angle.Zero;
    [DataField] [AutoNetworkedField] public Angle CurrentHeading = Angle.Zero;
    [DataField] [AutoNetworkedField] public float DistanceTraveled = 0f;
    [DataField] [AutoNetworkedField] public Vector2 LungeDirection = Vector2.UnitX;
    public int Stage = 0;
    public float SoundDistanceAccumulator = 0f;
    public float LungeDistanceRemaining = 0f;
    public Dictionary<EntityUid, TimeSpan> HitEntities = new();
    public bool HeadingInitialized = false;

}
