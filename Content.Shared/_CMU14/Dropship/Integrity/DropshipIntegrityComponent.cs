using System.Collections.Generic;
using System.Numerics;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Dropship.Integrity;

/// <summary>
/// One shared hull-integrity pool for every structural part anchored to a dropship grid.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DropshipIntegrityComponent : Component
{
    [DataField, AutoNetworkedField]
    public float MaxIntegrity = 2000f;

    [DataField, AutoNetworkedField]
    public float Integrity = 2000f;

    [DataField, AutoNetworkedField]
    public bool Crashing;

    [DataField, AutoNetworkedField]
    public bool Wrecked;

    /// <summary>
    /// Authoritative lifecycle state replicated for policy and presentation consumers.
    /// </summary>
    [DataField, AutoNetworkedField]
    public DropshipFlightState FlightState = DropshipFlightState.Landed;

    [DataField, AutoNetworkedField]
    public List<DropshipMalfunction> ActiveMalfunctions = new();

    [DataField, AutoNetworkedField]
    public bool MasterAlarmSilenced;

    [DataField, AutoNetworkedField]
    public bool ProximityAlarmActive;

    [DataField, AutoNetworkedField]
    public List<Vector2> ProximityHazards = new();

    [DataField, AutoNetworkedField]
    public bool LowIntegrityAlarmActive;

    [DataField]
    public int MaxActiveMalfunctions = 3;

    [DataField]
    public float[] MalfunctionThresholds = [0.6f, 0.4f, 0.25f];

    [DataField]
    public float ImpactDamageMultiplier = 5f;

    [DataField]
    public float ObstacleDamageMultiplier = 150f;

    [DataField]
    public float MinimumDamagingImpactSpeed = 1.5f;

    /// <summary>
    /// Damage multiplier applied to xeno acid projectiles which strike the hull.
    /// </summary>
    [DataField]
    public float XenoAcidProjectileDamageMultiplier = 4f;

    [DataField]
    public SoundSpecifier ImpactSound = new SoundPathSpecifier("/Audio/_RMC14/Effects/metal_crash.ogg",
        AudioParams.Default.WithVolume(-2));

    [DataField]
    public SoundSpecifier ProximityAlarmSound = new SoundPathSpecifier("/Audio/_CMU14/Dropship/ssmasteralarm.wav");

    [DataField]
    public SoundSpecifier LowIntegrityAlarmSound = new SoundPathSpecifier("/Audio/_CMU14/Dropship/726ppalm.wav");

    [DataField]
    public TimeSpan ImpactSoundCooldown = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan CrashWarningTime = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Angular velocity added each second while the dropship is crashing. This
    /// preserves its existing rotational momentum instead of snapping it to a
    /// fixed spin speed.
    /// </summary>
    [DataField]
    public float CrashSpinAccelerationDegrees = 180f;

    [DataField]
    public float RepairAmount = 100f;

    [DataField]
    public TimeSpan RepairTime = TimeSpan.FromSeconds(5);

    [DataField]
    public FixedPoint2 RepairFuel = FixedPoint2.New(2);

    [DataField]
    public SoundSpecifier RepairSound = new SoundPathSpecifier("/Audio/Items/welder.ogg");

    public TimeSpan CrashAt;

    public EntityUid? CrashMap;

    public TimeSpan? CrashAftermathAt;

    /// <summary>
    /// A bounded set of startup scans catches map children initialized shortly
    /// after their grid without permanently polling the dropship contents.
    /// </summary>
    public byte HullInitializationScansRemaining;
    public TimeSpan NextHullInitializationScan;

    public TimeSpan NextImpactSound;

    public TimeSpan NextStationaryProximityScan;
    public EntityUid? LastProximityMap;
    public Vector2 LastProximityPosition;
    public Angle LastProximityRotation;
    public bool HasLastProximityPose;

    [NonSerialized]
    public EntityUid? ProximityAlarmStream;

    [NonSerialized]
    public EntityUid? LowIntegrityAlarmStream;

    public int TriggeredMalfunctionThresholds;

    [NonSerialized]
    public HashSet<DropshipMalfunction> RepairingMalfunctions = new();

    [NonSerialized]
    public Dictionary<DropshipMalfunction, int> MalfunctionRepairProgress = new();
}

/// <summary>
/// Marks an anchored wall or damageable entity as part of a dropship's shared hull.
/// Keeping the damage subscription on this marker avoids claiming the global
/// DamageableComponent event pair used by other systems.
/// </summary>
[RegisterComponent]
public sealed partial class DropshipHullComponent : Component;

/// <summary>
/// Stores the damage components that are added to an otherwise invincible
/// dropship wall after the ship becomes a wreck.
/// </summary>
[RegisterComponent]
public sealed partial class DropshipCrashDestructibleWallComponent : Component
{
    [DataField(required: true)]
    public ComponentRegistry Components = new();
}

[Serializable, NetSerializable]
public sealed partial class DropshipIntegrityRepairDoAfterEvent : SimpleDoAfterEvent;
