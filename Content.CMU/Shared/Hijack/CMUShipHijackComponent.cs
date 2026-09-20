using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Hijack;

/// <summary>
/// Opts a ship map into the CM-SS13 hijack objectives. ShipMaps is captured before
/// a ground impact joins the ship to the colony's Z network; the colony must never
/// become a ship for power, evacuation, pipe damage or launch restrictions.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUShipHijackComponent : Component
{
    [DataField, AutoNetworkedField] public CMUShipHijackStage Stage;
    [DataField, AutoNetworkedField] public HashSet<EntityUid> ShipMaps = new();
    [DataField] public HashSet<EntityUid> GroundMaps = new();
    [DataField] public List<EntityUid> ShipGrids = new();
    [DataField] public Dictionary<EntityUid, EntityUid?> Pumps = new();
    [DataField] public List<int> CrackPositions = new() { 0 };
    [DataField] public bool ContinueOnGroundCrash;
    [DataField] public bool AdminSelfDestructBlocked;
    [DataField, AutoNetworkedField] public bool SelfDestructUnlocked;
    [DataField, AutoNetworkedField] public bool InFTL;
    [DataField, AutoNetworkedField] public bool GroundImpacted;
    [DataField, AutoNetworkedField] public double LastProgress;
    [DataField, AutoNetworkedField] public int OverloadedGenerators;
    [DataField, AutoNetworkedField] public double SelfDestructRemaining = 900;
    [DataField] public int MaximumOverloadedGenerators = 18;
    [DataField] public bool GeneratorEverOverloaded;
    [DataField] public bool Heated;
    [DataField] public bool Superheated;
    [DataField] public bool HalfwayAnnounced;
    [DataField] public bool CheckMarinePresence = true;
    [DataField] public bool ObjectivesStopped;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? FTLEnteredAt;
    [DataField] public string? VictimFaction;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextUpdate;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? TransitionAt;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? PumpExplosionAt;
    [DataField] public List<MapCoordinates> PumpBlastTargets = new();
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? NextPumpBlastAt;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? SelfDestructUnlockAt;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? DetonationStartedAt;
    [DataField] public int DetonationStep;
    [DataField] public EntityUid? CrashGroundMap;
    [DataField] public System.Numerics.Vector2 CrashOffset;
    [DataField] public Dictionary<EntityUid, string> OriginalParallax = new();
    [DataField] public HashSet<EntityUid> CinematicMobs = new();
}

[Serializable, NetSerializable]
public enum CMUShipHijackStage : byte
{
    Idle,
    Sublight,
    FTL,
    Arriving,
    Docked,
    GroundCrash,
    FTLCrash,
    Detonating,
    Destroyed,
}
