using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.SceneAnimation;

/// <summary>
/// A mapper-facing marker that periodically launches a scene-animation wave in its facing direction.
/// </summary>
[RegisterComponent]
public sealed partial class CMUSceneSpawnMarkerComponent : Component
{
    [DataField]
    public List<CMUSceneSpawnGroup> SpawnGroups = [];

    [DataField]
    public TimeSpan MinSpawnInterval = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan MaxSpawnInterval = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan MinGroupMemberInterval = TimeSpan.FromSeconds(0.2);

    [DataField]
    public TimeSpan MaxGroupMemberInterval = TimeSpan.FromSeconds(0.45);

    [DataField]
    public float ActorLateralVariance = 0.7f;

    [DataField]
    public float ActorLongitudinalVariance = 0.25f;

    [DataField]
    public float TankRiderChance = 0.5f;

    /// <summary>
    /// If true, a new wave waits for every member of the previous wave to finish its route.
    /// Disable for continuous background traffic streams.
    /// </summary>
    [DataField]
    public bool WaitForGroupClear = true;

    public TimeSpan NextSpawnAt;
    public CMUSceneSpawnGroup? CurrentGroup;
    public int GroupMembersRemaining;
    public EntProtoId? FixedGroupPrototype;
    public EntProtoId? LastGroupPrototype;
    public EntityUid? LastGroupMember;
    public readonly HashSet<EntityUid> ActiveGroupMembers = [];
}

[DataDefinition]
public sealed partial class CMUSceneSpawnGroup
{
    [DataField(required: true)]
    public List<EntProtoId> Spawns = [];

    [DataField]
    public int MinSize = 2;

    [DataField]
    public int MaxSize = 4;

    /// <summary>
    /// If true, every member independently chooses a prototype. If false, one prototype is used by the whole wave.
    /// </summary>
    [DataField]
    public bool MixPrototypes = true;

    [DataField]
    public bool Marine;

    [DataField]
    public bool Tank;

    [DataField]
    public bool Apc;

    /// <summary>
    /// Moves decorative vehicle props directly instead of relying on functional vehicle wheels and drive systems.
    /// </summary>
    [DataField]
    public float DirectMoveSpeed;

    /// <summary>
    /// Restricts decorative road traffic to east/west travel regardless of an accidentally vertical marker rotation.
    /// </summary>
    [DataField]
    public bool HorizontalOnly;

    [DataField]
    public float MaxTravelDistance = 40f;

    /// <summary>
    /// Minimum distance the previous vehicle must clear before the next member is spawned.
    /// </summary>
    [DataField]
    public float MemberClearance = 4f;
}

/// <summary>
/// Runtime state for an entity travelling through a mapper-authored scene.
/// </summary>
[RegisterComponent]
public sealed partial class CMUSceneMoverComponent : Component
{
    public EntityUid Spawner;
    public Vector2i Direction;
    public Vector2 StartPosition;
    public MapId StartMap;
    public float MaxTravelDistance = 40f;
    public bool IsVehicle;
    public bool IsTank;
    public bool IgnoreMobCollisions = true;
    public bool GibMobsOnContact;
    public float DirectMoveSpeed;
    public readonly List<EntityUid> LinkedEntities = [];
}

[RegisterComponent]
public sealed partial class CMUSceneTankRiderComponent : Component
{
    public EntityUid Tank;
    public Vector2 LocalPosition;
}

[RegisterComponent]
public sealed partial class CMUSceneDeleteMarkerComponent : Component
{
    [DataField]
    public float Radius = 2.5f;
}
