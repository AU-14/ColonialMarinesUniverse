using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._RMC14.Evacuation;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause] // CMU14 attributes
[Access(typeof(SharedEvacuationSystem))]
public sealed partial class EvacuationProgressComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled;

    [DataField, AutoNetworkedField]
    public bool DropShipCrashed;

    /// CMU14 <summary>The faction whose ship was hijacked. Scopes evacuation announcements; null = all marines.</summary>
    [DataField, AutoNetworkedField]
    public string? VictimFaction;

    [DataField, AutoNetworkedField]
    public bool StartAnnounced;

    [DataField, AutoNetworkedField]
    public double Progress;

    [DataField, AutoNetworkedField]
    public double Required = 100;

    [DataField, AutoNetworkedField]
    public TimeSpan UpdateEvery = TimeSpan.FromSeconds(2);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextUpdate;

    [DataField, AutoNetworkedField]
    public int AnnounceEvery = 25;

    [DataField, AutoNetworkedField]
    public int NextAnnounce;

    [DataField]
    public Dictionary<EntityUid, bool> LastPower = new();

    // CMU14
    [DataField, AutoNetworkedField] public TimeSpan? EnabledAt;
    [DataField, AutoNetworkedField] public TimeSpan AbortCutoff = TimeSpan.FromSeconds(600);
    [DataField, AutoNetworkedField] public TimeSpan? SelfDestructAt;
    [DataField, AutoNetworkedField] public TimeSpan SelfDestructDelay = TimeSpan.FromSeconds(900);
    [DataField, AutoNetworkedField] public bool SelfDestructed;
    // CMU14
}
