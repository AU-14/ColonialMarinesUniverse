using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._RMC14.Tracker.SquadLeader;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class SquadLeaderTrackerComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan UpdateAt;

    [DataField]
    public TimeSpan UpdateEvery = TimeSpan.FromSeconds(1);

    [DataField]
    public FireteamData Fireteams = new();

    // Transient: NetEntities (players) who were granted temporary permission to edit fireteam nicknames
    // when an Overwatch console opened the SquadInfo UI bound to this tracker. This is not networked.
    public HashSet<NetEntity> TemporaryOverwatchEditors = new();

    [DataField]
    public ProtoId<TrackerModePrototype>? Mode;

    [DataField]
    public bool ManualMode;

    [DataField]
    public EntityUid? Target;

    [DataField]
    public EntityUid? BattleBuddy;

    [DataField]
    public HashSet<ProtoId<TrackerModePrototype>> TrackerModes = new();
}
