using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._RMC14.Tracker.SquadLeader;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
public sealed partial class SquadLeaderTrackerComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan UpdateAt;

    [DataField, AutoNetworkedField]
    public TimeSpan UpdateEvery = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public FireteamData Fireteams = new();

    // Transient: NetEntities (players) who were granted temporary permission to edit fireteam nicknames
    // when an Overwatch console opened the SquadInfo UI bound to this tracker. This is not networked.
    public HashSet<NetEntity> TemporaryOverwatchEditors = new();

    [DataField, AutoNetworkedField]
    public ProtoId<TrackerModePrototype>? Mode;

    [DataField, AutoNetworkedField]
    public bool ManualMode;

    [DataField, AutoNetworkedField]
    public EntityUid? Target;

    [DataField, AutoNetworkedField]
    public EntityUid? BattleBuddy;

    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<TrackerModePrototype>> TrackerModes = new();
}
