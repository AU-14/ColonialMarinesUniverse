using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.DropPod;

/// <summary>
/// Single-occupant troop insertion pod fired from a <see cref="CMUDropPodConsoleComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUDropPodComponent : Component
{
    [DataField]
    public string ContainerId = "cmu_drop_pod_occupant";

    [ViewVariables]
    public ContainerSlot Container = default!;

    /// <summary>
    /// Networked mirror of the server-only container, used by shared drag-drop checks.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Occupied;

    [DataField, AutoNetworkedField]
    public CMUDropPodState State = CMUDropPodState.Ready;

    /// <summary>
    /// Consoles only fire pods that share this group and the same map or z-network.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string Group = "default";

    [DataField]
    public EntityWhitelist? OccupantWhitelist;

    [DataField]
    public EntityWhitelist? OccupantBlacklist;

    /// <summary>
    /// <see cref="Content.Shared.Containers.ItemSlots.ItemSlotsComponent"/> slot IDs locked during flight and spilled on breakup.
    /// </summary>
    [DataField]
    public List<string> WeaponSlots = new();

    [DataField]
    public TimeSpan InsertDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan ExitStun = TimeSpan.Zero;

    [DataField]
    public bool RequireOccupant = true;

    [DataField]
    public List<CMUDropPodTravelPhase> Phases = new();

    [DataField]
    public float SkyFallDuration = 1.5f;

    [DataField]
    public float CrashDuration = 0.75f;

    [DataField]
    public SoundSpecifier? LaunchSound;

    [DataField]
    public SoundSpecifier? DropSound;

    [DataField]
    public SoundSpecifier? CrashSound;

    [DataField]
    public float ScatterRadius = 60;

    [DataField]
    public int LandingAttempts = 64;

    /// <summary>
    /// Z-level depth pods aim for before climbing over blocking walls.
    /// </summary>
    [DataField]
    public int TargetDepth;

    /// <summary>
    /// Anchored entities matching this are walls a pod lands on top of instead of inside.
    /// </summary>
    [DataField]
    public EntityWhitelist? LandingBlockers;

    /// <summary>
    /// Only count <see cref="LandingBlockers"/> that cannot be damaged.
    /// </summary>
    [DataField]
    public bool BlockersMustBeIndestructible = true;

    [DataField]
    public bool RespectParadropAreas = true;

    [DataField]
    public float LandingDamageRadius = 0.5f;

    [DataField]
    public DamageSpecifier? LandingDamage;

    [DataField]
    public float LandingShakeRange = 8;

    [DataField]
    public int LandingShakes = 3;

    [DataField]
    public int LandingShakeStrength = 5;

    [DataField]
    public float InjuryChance;

    [DataField]
    public DamageSpecifier? InjuryDamage;

    [DataField]
    public TimeSpan InjuryStun = TimeSpan.Zero;

    /// <summary>
    /// Negative damage applied to an injured occupant who stays inside after landing.
    /// </summary>
    [DataField]
    public DamageSpecifier? TreatmentDamage;

    [DataField]
    public TimeSpan TreatmentInterval = TimeSpan.FromSeconds(2);

    [DataField]
    public int TreatmentTicks;

    /// <summary>
    /// Time after landing before an uninjured occupant is pushed out.
    /// </summary>
    [DataField]
    public TimeSpan OpenDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Rolled once per atmospheric travel phase.
    /// </summary>
    [DataField]
    public float AtmosphereBreakupChance;

    [DataField]
    public float ImpactBreakupChance;

    [DataField]
    public float BreakupExtraScatter;

    [DataField]
    public DamageSpecifier? BreakupDamage;

    [DataField]
    public TimeSpan BreakupStun = TimeSpan.Zero;

    [DataField]
    public EntProtoId? BreakupDebris;

    [DataField]
    public int BreakupDebrisCount;

    [DataField]
    public SoundSpecifier? BreakupSound;
}

/// <summary>
/// One leg of the pod's travel cutscene, shown on a private transit map.
/// </summary>
[DataDefinition, Serializable]
public sealed partial class CMUDropPodTravelPhase
{
    [DataField(required: true)]
    public TimeSpan Duration;

    /// <summary>
    /// Client parallax prototype ID.
    /// </summary>
    [DataField]
    public string Parallax = "CMUParallaxSpace";

    [DataField]
    public Vector2 Velocity;

    /// <summary>
    /// Atmospheric phases roll <see cref="CMUDropPodComponent.AtmosphereBreakupChance"/>.
    /// </summary>
    [DataField]
    public bool Atmospheric;

    [DataField]
    public LocId? Message;

    [DataField]
    public SoundSpecifier? Sound;

    [DataField]
    public int Shakes;

    [DataField]
    public int ShakeStrength;
}

[Serializable, NetSerializable]
public enum CMUDropPodState : byte
{
    Ready,
    InFlight,
    Landed,
}

[Serializable, NetSerializable]
public enum CMUDropPodVisuals : byte
{
    Status,
}

[Serializable, NetSerializable]
public enum CMUDropPodVisualLayers : byte
{
    Base,
}

[Serializable, NetSerializable]
public enum CMUDropPodStatus : byte
{
    Empty,
    Occupied,
    Spent,
}

[Serializable, NetSerializable]
public sealed partial class CMUDropPodInsertDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public NetEntity Inserted;

    public CMUDropPodInsertDoAfterEvent()
    {
    }

    public CMUDropPodInsertDoAfterEvent(NetEntity inserted)
    {
        Inserted = inserted;
    }
}
