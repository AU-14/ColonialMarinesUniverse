using System.Numerics;
using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Weapons.Ranged.Vulture;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedVultureAimSystem))]
public sealed partial class VultureAimComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId ScopeForwardActionId = "RMCActionVultureScopeForward";

    [DataField, AutoNetworkedField]
    public EntityUid? ScopeForwardAction;

    [DataField, AutoNetworkedField]
    public EntProtoId ScopeBackwardActionId = "RMCActionVultureScopeBackward";

    [DataField, AutoNetworkedField]
    public EntityUid? ScopeBackwardAction;

    [DataField, AutoNetworkedField]
    public EntProtoId ScopeLeftActionId = "RMCActionVultureScopeLeft";

    [DataField, AutoNetworkedField]
    public EntityUid? ScopeLeftAction;

    [DataField, AutoNetworkedField]
    public EntProtoId ScopeRightActionId = "RMCActionVultureScopeRight";

    [DataField, AutoNetworkedField]
    public EntityUid? ScopeRightAction;

    [DataField, AutoNetworkedField]
    public EntProtoId WindageLeftActionId = "RMCActionVultureWindageLeft";

    [DataField, AutoNetworkedField]
    public EntityUid? WindageLeftAction;

    [DataField, AutoNetworkedField]
    public EntProtoId WindageRightActionId = "RMCActionVultureWindageRight";

    [DataField, AutoNetworkedField]
    public EntityUid? WindageRightAction;

    [DataField, AutoNetworkedField]
    public EntProtoId ElevationUpActionId = "RMCActionVultureElevationUp";

    [DataField, AutoNetworkedField]
    public EntityUid? ElevationUpAction;

    [DataField, AutoNetworkedField]
    public EntProtoId ElevationDownActionId = "RMCActionVultureElevationDown";

    [DataField, AutoNetworkedField]
    public EntityUid? ElevationDownAction;

    [DataField, AutoNetworkedField]
    public EntProtoId BreathActionId = "RMCActionVultureBreath";

    [DataField, AutoNetworkedField]
    public EntityUid? BreathAction;

    [AutoNetworkedField]
    public EntityUid? Sniper;

    [AutoNetworkedField]
    public EntityUid? RifleScope;

    [AutoNetworkedField]
    public EntityUid? Spotter;

    [AutoNetworkedField]
    public EntityUid? SpotterScope;

    [AutoNetworkedField]
    public EntityUid? SpotterTripod;

    [AutoNetworkedField]
    public Direction? Direction;

    [AutoNetworkedField]
    public MapCoordinates? ViewCenter;

    [AutoNetworkedField]
    public MapCoordinates? CrosshairCoordinates;

    [AutoNetworkedField]
    public int ViewForwardOffset;

    [AutoNetworkedField]
    public int ViewSideOffset;

    [AutoNetworkedField]
    public Vector2i CrosshairAdjust;

    [AutoNetworkedField]
    public Vector2i SwayOffset;

    [AutoNetworkedField]
    public Vector2 DriftOffset;

    [AutoNetworkedField]
    public Vector2 DriftTarget;

    [AutoNetworkedField]
    public TimeSpan BreathEndsAt;

    [AutoNetworkedField]
    public TimeSpan BreathCooldownEndsAt;

    [AutoNetworkedField]
    public TimeSpan NextSwayAt;

    [DataField, AutoNetworkedField]
    public int BaseViewDistance = 15;

    [DataField, AutoNetworkedField]
    public int MaxForwardOffset = 6;

    [DataField, AutoNetworkedField]
    public int MaxSideOffset = 6;

    [DataField, AutoNetworkedField]
    public int BoxRadius = 2;

    [DataField, AutoNetworkedField]
    public float SpotterLinkRange = 16f;

    [DataField, AutoNetworkedField]
    public TimeSpan SwayEvery = TimeSpan.FromSeconds(2);

    [DataField, AutoNetworkedField]
    public float DriftRadius = 1f;

    [DataField, AutoNetworkedField]
    public float DriftSpeed = 8f;

    [DataField, AutoNetworkedField]
    public TimeSpan BreathDuration = TimeSpan.FromSeconds(8);

    [DataField, AutoNetworkedField]
    public TimeSpan BreathCooldown = TimeSpan.FromSeconds(8);

    public bool BreathActive(TimeSpan time)
    {
        return BreathEndsAt > time;
    }
}
