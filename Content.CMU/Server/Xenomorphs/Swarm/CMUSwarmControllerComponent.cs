using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Xenomorphs.Swarm;

/// <summary>Hive-owned orders survive camera changes and replacement queens.</summary>
[RegisterComponent]
public sealed partial class CMUSwarmControllerComponent : Component
{
    [DataField] public EntProtoId Lesser = "CMUSwarmLesser";
    [DataField] public EntProtoId MarkerPrototype = "CMUSwarmGuardMarker";
    [DataField] public int SpawnCount = 15;
    [DataField] public TimeSpan Cooldown = TimeSpan.FromMinutes(15);
    [DataField] public float SightRange = 8;
    [DataField] public float LeashRange = 12;
    [DataField] public float FormationRadius = 2;
    [DataField] public TimeSpan ThinkInterval = TimeSpan.FromSeconds(0.5);
    [DataField] public TimeSpan TargetMemory = TimeSpan.FromSeconds(3);
    public TimeSpan NextSummon;
    public TimeSpan NextThink;
    public bool Passive;
    public EntityUid? Anchor;
    public EntityUid? Marker;
    public readonly List<EntityUid> Members = new();
    public readonly Dictionary<EntityUid, TimeSpan> Sightings = new();
}
