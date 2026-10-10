using System.Numerics;
using Robust.Shared.Configuration;
using Robust.Shared.GameStates;
using Robust.Shared.Physics;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Fighter;

/// <summary>The physical cockpit owns the aircraft's position in the airspace over a terrain map.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class FighterAircraftComponent : Component
{
    [DataField, AutoNetworkedField] public EntityUid? GroundEntity;
    [DataField, AutoNetworkedField] public FighterGroundState GroundState;
    [DataField, AutoNetworkedField] public bool RecoveryHandoff;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan GroundStateStartedAt;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan GroundStateEndsAt;
    [DataField, AutoNetworkedField] public EntityUid TerrainMap;
    [DataField, AutoNetworkedField] public EntityUid ViewMap;
    [DataField, AutoNetworkedField] public Vector2 Position;
    [DataField, AutoNetworkedField] public Vector2 Home;
    [DataField, AutoNetworkedField] public float Heading;
    [DataField, AutoNetworkedField] public float Speed = 12f;
    [DataField, AutoNetworkedField] public float TargetSpeed = 12f;
    [DataField, AutoNetworkedField] public float Bank;
    [DataField, AutoNetworkedField] public FighterAltitude Altitude = FighterAltitude.Low;
    [DataField, AutoNetworkedField] public bool Flying;
    [DataField, AutoNetworkedField] public bool ForcedRetreat;
    [DataField, AutoNetworkedField] public FighterPhase Phase;
    [DataField, AutoNetworkedField] public Vector2 Entry;
    [DataField, AutoNetworkedField] public Vector2 Exit;
    [DataField, AutoNetworkedField] public Vector2 LegStart;
    [DataField, AutoNetworkedField] public Vector2 LegControl1;
    [DataField, AutoNetworkedField] public Vector2 LegControl2;
    [DataField, AutoNetworkedField] public Vector2 LegEnd;
    [DataField, AutoNetworkedField] public float Progress;
    [DataField, AutoNetworkedField] public float Height = 350;
    [DataField, AutoNetworkedField] public float TargetHeight = 350;
    [DataField, AutoNetworkedField] public Box2 Battlefield = new(-120, -120, 120, 120);
    [DataField, AutoNetworkedField] public Vector2? Mark;
    [DataField, AutoNetworkedField] public float MarkAge;
    [DataField, AutoNetworkedField] public Vector2? TrainingImpact;
    [DataField, AutoNetworkedField] public int PassNumber;
    [DataField, AutoNetworkedField] public int MarkPass;
    [DataField, AutoNetworkedField] public EntityUid? FrontSeat;
    [DataField, AutoNetworkedField] public EntityUid? RearSeat;
    [DataField, AutoNetworkedField] public EntityUid? Hull;
    [DataField, AutoNetworkedField] public EntityUid? Canopy;
    [DataField, AutoNetworkedField] public float MinimumSpeed = 8f;
    [DataField, AutoNetworkedField] public float MaximumSpeed = 26f;
    [DataField] public float Acceleration = 4f;
    [DataField, AutoNetworkedField] public float AirspaceRadius = 500f;
    [DataField, AutoNetworkedField] public float MinimumSensorRange = 24f;
    [DataField, AutoNetworkedField] public float MaximumSensorRange = 120f;
    [DataField, AutoNetworkedField] public float MinimumDesignationRangeFactor = .95f;
    [DataField, AutoNetworkedField] public float MaximumDesignationRangeFactor = 1.8f;
    [DataField, AutoNetworkedField] public FighterSensorMode SensorMode;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ThermalUntil;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ThermalReadyAt;
    public float Accumulator;
    public float NetworkAccumulator;
    public float[] LegDistances = [];
    public float LegTravel;
    public EntityUid? Flyby;
    public EntityUid? FlybyAudio;
}

/// <summary>Static terrain chart, separate from frequently changing flight state.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FighterChartComponent : Component
{
    public const int Resolution = 64;
    [DataField, AutoNetworkedField] public byte[] Terrain = [];
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class FighterSeatComponent : Component
{
    [DataField, AutoNetworkedField] public bool Pilot;
    [DataField, AutoNetworkedField] public EntityUid? Aircraft;
    [DataField, AutoNetworkedField] public EntityUid? Occupant;
    [DataField, AutoNetworkedField] public EntityUid? Camera;
    [DataField, AutoNetworkedField] public EntityUid? ExteriorCamera;
    [DataField, AutoNetworkedField] public Vector2 Aim;
    /// <summary>World-space camera anchor; aircraft motion never drags it along.</summary>
    [DataField, AutoNetworkedField] public Vector2? SensorFocus;
    [DataField, AutoNetworkedField] public Vector2? SensorLock;
    [DataField, AutoNetworkedField] public bool Zoomed;
    [DataField, AutoNetworkedField] public NetEntity? Target;
    [DataField, AutoNetworkedField] public Vector2? TargetPosition;
    [DataField, AutoNetworkedField] public int WeaponSlot;
    [DataField, AutoNetworkedField] public EntityUid? Laser;
    [DataField, AutoNetworkedField] public NetEntity? LaserLockTarget;
    [DataField, AutoNetworkedField] public int LaserLockSlot;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan LaserLockReadyAt;
    public EntityUid? LaserLockAmmo;
    [DataField, AutoNetworkedField] public bool FireQueued;
    public NetEntity? QueuedTarget;
    public int QueuedSlot;
    public EntityUid? QueuedAmmo;
    public int QueuedPass;
    [DataField, AutoNetworkedField] public bool CameraControl;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan EjectConfirmUntil;
    public FighterInput Input;
    public TimeSpan LastInput;
    public TimeSpan NextCommand;
    // Crew are rigid attachments while seated; their ordinary physics resumes on exit.
    public BodyType? OccupantBodyType;
    public bool OccupantCanCollide;
}

[Serializable, NetSerializable]
public enum FighterAltitude : byte { Low, Cloud, High }

[Serializable, NetSerializable]
public enum FighterPhase : byte { Holding, Approach, Pass, Return }

[Serializable, NetSerializable]
public enum FighterSensorMode : byte { Normal, NightVision, Thermal }

[Flags, Serializable, NetSerializable]
public enum FighterInput : byte { None = 0, Forward = 1, Back = 2, Left = 4, Right = 8 }

[Serializable, NetSerializable]
public enum FighterCommand : byte
{
    Launch, Ascend, Descend, Mark, Zoom, Center, SwapSeat, LeaveSeat, Return, TrainingRelease, Lock,
    VisionNormal, VisionNight, VisionThermal, Fire, Laser, Flares, PrepareRun, QueueFire, Takeoff,
    FormUp, ConfirmEject, CancelEject,
}

[Serializable, NetSerializable]
public sealed class FighterInputEvent(FighterInput input, bool cameraControl = false) : EntityEventArgs
{
    public FighterInput Input = input;
    public bool CameraControl = cameraControl;
}

[Serializable, NetSerializable]
public sealed class FighterCommandEvent(FighterCommand command) : EntityEventArgs
{
    public FighterCommand Command = command;
}

[Serializable, NetSerializable]
public sealed class FighterStopSpectatingEvent : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class FighterPlanEvent(Vector2 entry, Vector2 exit) : EntityEventArgs
{
    public Vector2 Entry = entry;
    public Vector2 Exit = exit;
}

[Serializable, NetSerializable]
public sealed class FighterSettingsEvent(float height, float speed) : EntityEventArgs
{
    public float Height = height;
    public float Speed = speed;
}

[CVarDefs]
public sealed class FighterCVars
{
    public static readonly CVarDef<bool> Development = CVarDef.Create("fighter.development", false, CVar.SERVERONLY);
    public static readonly CVarDef<bool> AirCombatTrial = CVarDef.Create("fighter.air_combat_development", false, CVar.SERVERONLY);
    public static readonly CVarDef<bool> ManpadTrial = CVarDef.Create("fighter.manpad_development", false, CVar.SERVERONLY);
    public static readonly CVarDef<bool> GroundTrial = CVarDef.Create("fighter.ground_development", false, CVar.SERVERONLY);
}
