using System.Numerics;
using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Fighter;

/// <summary>A shoulder-fired launcher that covers its operator's sector while wielded and aimed.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class FighterManpadComponent : Component
{
    [DataField] public EntProtoId AimActionId = "CMUActionManpadAim";
    [DataField, AutoNetworkedField] public EntityUid? AimAction;
    [DataField, AutoNetworkedField] public EntityUid? AimingUser;
    // The current operator's side, authenticated when the launcher is aimed.
    [DataField, AutoNetworkedField] public string? Faction;
    [DataField, AutoNetworkedField] public bool IgnoreIFF;
    [DataField] public TimeSpan AcquisitionTime = TimeSpan.FromSeconds(1.2);
    [DataField] public TimeSpan ReloadTime = TimeSpan.FromSeconds(15);
    [DataField] public TimeSpan MissileFlightTime = TimeSpan.FromSeconds(5);
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan ReadyAt;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan LaunchAt;
    public EntityUid? Target;
    public EntityUid? WindupVisual;
    public EntityUid? WindupAudio;
    public EntityUid? TrackingAudio;
}

public sealed partial class FighterManpadAimActionEvent : InstantActionEvent;

/// <summary>Ends any pending acquisition immediately when the operator lowers the launcher.</summary>
[ByRefEvent]
public readonly record struct FighterManpadAimStoppedEvent;

/// <summary>One replicated launch, rendered locally without spawning individual smoke or spark entities.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class FighterManpadVisualComponent : Component
{
    public const float AscentSeconds = 1.6f;
    public const float Lifetime = 8;
    [DataField, AutoNetworkedField] public bool Launched;
    [DataField, AutoNetworkedField] public float WindupSeconds = 1.2f;
    [DataField, AutoNetworkedField] public Vector2 Direction = Vector2.UnitY;
    [DataField, AutoNetworkedField] public Vector2 TubeDirection = Vector2.UnitY;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan StartedAt;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ExpiresAt;
}
