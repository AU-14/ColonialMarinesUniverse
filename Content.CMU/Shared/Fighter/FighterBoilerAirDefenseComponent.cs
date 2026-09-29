using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Fighter;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class FighterBoilerAirDefenseComponent : Component
{
    [DataField] public EntProtoId ActionId = "CMUActionBoilerWatchAirspace";
    [DataField, AutoNetworkedField] public EntityUid? Action;
    [DataField, AutoNetworkedField] public bool Watching;
    [DataField] public FixedPoint2 PlasmaCost = 200;
    [DataField] public TimeSpan AcquisitionTime = TimeSpan.FromSeconds(2);
    [DataField] public TimeSpan Cooldown = TimeSpan.FromSeconds(15);
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField] public TimeSpan ReadyAt;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField] public TimeSpan LaunchAt;
    public EntityUid? Target;
    public EntityUid? WindupVisual;
    public EntityUid? WindupAudio;
    public Vector2 StartedPosition;
}

public sealed partial class FighterBoilerWatchAirspaceEvent : InstantActionEvent;

[ByRefEvent]
public readonly record struct FighterBoilerAirDefenseStoppedEvent;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class FighterPlasmaVisualComponent : Component
{
    [DataField, AutoNetworkedField] public bool Launched;
    [DataField, AutoNetworkedField] public Vector2 Direction = Vector2.UnitY;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan StartedAt;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ExpiresAt;
}
