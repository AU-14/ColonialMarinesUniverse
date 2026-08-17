using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._RMC14.Evacuation;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
[Access(typeof(SharedEvacuationSystem))]
public sealed partial class EvacuationProgressComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled;

    [DataField, AutoNetworkedField]
    public bool DropShipCrashed;

    /// <summary>
    /// The faction whose ship was hijacked. Used to scope evacuation announcements.
    /// Null = default marine (govfor).
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? VictimFaction;

    /// <summary>
    /// Whether this was a human-vs-human hijack. Suppresses xeno announcements.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsHumanHijack;

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

    [DataField, AutoNetworkedField] public TimeSpan? EnabledAt;
    [DataField, AutoNetworkedField] public TimeSpan AbortCutoff = TimeSpan.FromSeconds(600);
    [DataField, AutoNetworkedField] public TimeSpan? SelfDestructAt;
    [DataField, AutoNetworkedField] public TimeSpan SelfDestructDelay = TimeSpan.FromSeconds(900);
    [DataField, AutoNetworkedField] public bool SelfDestructed;
}
