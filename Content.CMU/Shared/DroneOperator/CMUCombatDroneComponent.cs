using System.Numerics;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Tools;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.DroneOperator;

[Serializable, NetSerializable]
public enum CMUCombatDroneVisuals : byte
{
    Turret,
    Wrecked,
    DamageState,
}

[Serializable, NetSerializable]
public enum CMUCombatDroneDamageState : byte
{
    Healthy,
    Damaged,
    Destroyed,
}

public enum CMUCombatDroneWeapon : byte
{
    Pulse,
    Flamer,
}

/// <summary>A remotely driven tracked chassis with a gun restricted to its forward hemisphere.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUCombatDroneComponent : Component
{
    [DataField]
    public float FireArcDegrees = 180;

    [DataField]
    public EntProtoId? TurretVisualPrototype;

    [DataField, AutoNetworkedField]
    public EntityUid? TurretVisual;

    /// <summary>Turret mount positions in sprite space, ordered south, north, east, west.</summary>
    [DataField]
    public List<Vector2> TurretMountOffsets = new()
    {
        new(0, 17f / 32), new(0, -14f / 32), new(-11f / 32, -6f / 32), new(10f / 32, -6f / 32),
    };

    /// <summary>Barrel tips relative to the turret pivot in each RSI direction.</summary>
    [DataField]
    public List<Vector2> TurretMuzzleOffsets = new()
    {
        new(0, -12f / 32), new(0, 18f / 32), new(24f / 32, 16f / 32), new(-24f / 32, 16f / 32),
    };

    [DataField, AutoNetworkedField]
    public bool Wrecked;

    [DataField]
    public FixedPoint2 WreckDamageThreshold = 350;

    [DataField]
    public FixedPoint2 WreckRecoveryThreshold = 200;

    [DataField]
    public FixedPoint2 DamagedVisualThreshold = 100;

    [DataField]
    public string? PreWreckName;

    [DataField]
    public ProtoId<ToolQualityPrototype> WeldQuality = "Welding";

    [DataField]
    public TimeSpan RepairDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public float WeldFuel = 5;

    [DataField]
    public int RepairWireCost = 5;

    [DataField]
    public FixedPoint2 RepairAmount = 30;

    [DataField]
    public List<ProtoId<DamageTypePrototype>> FrameDamageTypes = new() { "Blunt", "Slash", "Piercing" };

    [DataField]
    public List<ProtoId<DamageTypePrototype>> WiringDamageTypes = new() { "Heat", "Shock", "Caustic", "Cold" };

    [DataField]
    public FixedPoint2 SparkDamageThreshold = 20;

    [DataField]
    public float SparkIntervalMin = 4;

    [DataField]
    public float SparkIntervalMax = 9;

    [DataField]
    public EntProtoId SparkEffect = "EffectSparks";
}

[RegisterComponent]
public sealed partial class CMUCombatDroneHullComponent : Component
{
    [DataField]
    public CMUCombatDroneWeapon Weapon;

    [DataField]
    public EntProtoId DronePrototype = "CMUCombatDrone";

    [DataField]
    public TimeSpan AssemblyDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public string TurretContainerId = "cmu-combat-drone-turret";
}

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUCombatDroneTurretAssemblyComponent : Component
{
    [DataField]
    public CMUCombatDroneWeapon Weapon;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUCombatDroneAmmoBoxComponent : Component;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CMUCombatDroneSparkingComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextSpark;
}
