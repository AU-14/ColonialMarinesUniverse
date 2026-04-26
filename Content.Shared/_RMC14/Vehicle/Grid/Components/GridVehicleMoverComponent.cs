using System;
using System.Numerics;
using Content.Shared._RMC14.Stun;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Shared.Vehicle.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(Content.Shared.Vehicle.GridVehicleMoverSystem), Other = AccessPermissions.ReadWrite)]
public sealed partial class GridVehicleMoverComponent : Component
{
    /// <summary>
    /// current tile occupied by the vehicle on its grid.
    /// </summary>
    [AutoNetworkedField]
    public Vector2i CurrentTile;

    /// <summary>
    /// target tile used by prediction & smoothing
    /// </summary>
    [AutoNetworkedField]
    public Vector2i TargetTile;

    /// <summary>
    /// current local grid position of the vehicle
    /// </summary>
    [AutoNetworkedField]
    public Vector2 Position;

    /// <summary>
    /// target local grid position used by prediction & smoothing
    /// </summary>
    [AutoNetworkedField]
    public Vector2 TargetPosition;

    /// <summary>
    /// current cardinal facing direction of the vehicle
    /// </summary>
    [AutoNetworkedField]
    public Vector2i CurrentDirection;

    /// <summary>
    /// current cardinal direction used when the vehicle is being pushed
    /// </summary>
    [AutoNetworkedField]
    public Vector2i PushDirection;

    /// <summary>
    /// current signed movement speed in grid units / second
    /// </summary>
    [AutoNetworkedField]
    public float CurrentSpeed;

    /// <summary>
    /// maximum forward driving speed in grid units / second
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MaxSpeed = 11f;

    /// <summary>
    /// forward acceleration in grid units / second squared
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Acceleration = 7f;

    /// <summary>
    /// speed loss per second when slowing down or stoping
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Deceleration = 12f;

    /// <summary>
    /// maximum reverse driving speed in grid units / second
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MaxReverseSpeed = 4f;

    /// <summary>
    /// reverse acceleration in grid units / second squared
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ReverseAcceleration = 4f;

    /// <summary>
    /// forward offset used when placing the vehicle on tiles
    /// </summary>
    [DataField, AutoNetworkedField]
    public float FrontOffset = 0f;

    /// <summary>
    /// maximum sideways lane offset used for normal lane correction
    /// </summary>
    [DataField, AutoNetworkedField]
    public float TileOffsetLimit = 1f;

    /// <summary>
    /// sideways lane offset sampling step for finding clear lanes
    /// </summary>
    [DataField, AutoNetworkedField]
    public float TileOffsetStep = 0.05f;

    /// <summary>
    /// number of tiles checked ahead when choosing a clear lane
    /// </summary>
    [DataField, AutoNetworkedField]
    public int TileOffsetLookahead = 3;

    /// <summary>
    /// maximum sideways correction speed in grid units / second
    /// </summary>
    [DataField, AutoNetworkedField]
    public float LaneCorrectionSpeed = 4f;

    /// <summary>
    /// distance between continuous collision probes while moving
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MovementProbeStep = 0.1f;

    /// <summary>
    /// inset applied to non-mob movement collision checks
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MovementCollisionInset = 0.05f;

    /// <summary>
    /// maximum sideways nudge distance used to bypass blocking mobs
    /// </summary>
    [DataField, AutoNetworkedField]
    public float BlockingMobBypassNudgeLimit = 1.75f;

    /// <summary>
    /// sideways sampling step used to find a bypass around blocking mobs
    /// </summary>
    [DataField, AutoNetworkedField]
    public float BlockingMobBypassNudgeStep = 0.1f;

    /// <summary>
    /// delay before the vehicle can be pushed again
    /// </summary>
    [DataField, AutoNetworkedField]
    public float PushCooldown = 2f;

    /// <summary>
    /// minimum speed applied when a xeno shove starts
    /// </summary>
    [DataField, AutoNetworkedField]
    public float PushImpulseSpeed = 0.1f;

    /// <summary>
    /// delay after changing facing direction
    /// </summary>
    [DataField, AutoNetworkedField]
    public float TurnDelay = 0.08f;

    /// <summary>
    /// whether the vehicle can rotate without moving forward
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool TurnInPlace = false;

    /// <summary>
    /// highest speed where in-place turning is allowed
    /// </summary>
    [DataField, AutoNetworkedField]
    public float TurnInPlaceMaxSpeed = 0.35f;

    /// <summary>
    /// maximum local nudge distance when finding room to turn
    /// </summary>
    [DataField, AutoNetworkedField]
    public float TurnNudgeLimit = 0.45f;

    /// <summary>
    /// local sampling step used when finding room to turn
    /// </summary>
    [DataField, AutoNetworkedField]
    public float TurnNudgeStep = 0.1f;

    /// <summary>
    /// forward grace distance used to clear transient turn blockers
    /// </summary>
    [DataField, AutoNetworkedField]
    public float TurnCollisionGraceDistance = 1f;

    /// <summary>
    /// next time this vehicle may be pushed
    /// </summary>
    [AutoNetworkedField]
    public TimeSpan NextPushTime;

    /// <summary>
    /// next time this vehicle may turn
    /// </summary>
    [AutoNetworkedField]
    public TimeSpan NextTurnTime;

    /// <summary>
    /// time until movement is blocked after an in-place turn
    /// </summary>
    [AutoNetworkedField]
    public TimeSpan InPlaceTurnBlockUntil;

    /// <summary>
    /// whether movement has committed for the current simulation step
    /// </summary>
    [AutoNetworkedField]
    public bool IsCommittedToMove;

    /// <summary>
    /// whether current movement came from pushing instead of driving
    /// </summary>
    [AutoNetworkedField]
    public bool IsPushMove;

    /// <summary>
    /// whether the vehicle is currently moving
    /// </summary>
    [AutoNetworkedField]
    public bool IsMoving;

    /// <summary>
    /// minimum xeno size that blocks this vehicle
    /// </summary>
    [DataField, AutoNetworkedField]
    public RMCSizes? XenoBlockMinimumSize;

    /// <summary>
    /// whether xenos are allowed to push this vehicle
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool CanXenosPush = true;

    /// <summary>
    /// minimum xeno size needed to push this vehicle
    /// </summary>
    [DataField, AutoNetworkedField]
    public RMCSizes? XenoPushMinimumSize;

    /// <summary>
    /// whether this vehicle can push other grid vehicles
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool CanPushVehicles = false;

    /// <summary>
    /// grid that the mover state is currently synced to
    /// </summary>
    [NonSerialized]
    public EntityUid? SyncedGrid;

    /// <summary>
    /// active multiplier applied to movement speed after smashing objects
    /// </summary>
    [AutoNetworkedField]
    public float SmashSlowdownMultiplier = 1f;

    /// <summary>
    /// time when the active smash slowdown expires
    /// </summary>
    [AutoNetworkedField]
    public TimeSpan SmashSlowdownUntil;

    /// <summary>
    /// when true, this vehicle rams through damageable wall-like structures instead of being blocked.
    /// targets tagged with <c>VehicleSmashIgnore</c> still block.
    /// </summary>
    [DataField]
    public bool CanSmashWalls;

    /// <summary>Damage dealt to the wall per collision tick while ramming.</summary>
    [DataField]
    public float WallSmashDamage = 75f;

    /// <summary>Wheel (or tread) damage dealt back to the vehicle per collision tick while ramming.</summary>
    [DataField]
    public float WallSmashWheelDamage = 0.25f;

    /// <summary>Hull damage dealt back to the vehicle per collision tick while ramming.</summary>
    [DataField]
    public float WallSmashHullDamage = 5f;

    /// <summary>
    /// Multiplier applied to the vehicle's own tread/hull damage when a plow is installed
    /// (any hardpoint item tagged <c>VehiclePlow</c>). Wall damage is unaffected.
    /// </summary>
    [DataField]
    public float WallSmashPlowDamageMultiplier = 0.3f;

    /// <summary>Speed multiplier applied while ramming.</summary>
    [DataField]
    public float WallSmashSlowdownMultiplier = 0.45f;

    /// <summary>How long the slowdown lingers after a ram, seconds.</summary>
    [DataField]
    public float WallSmashSlowdownDuration = 0.4f;

    /// <summary>Minimum seconds between smash damage ticks on the same target.</summary>
    [DataField]
    public float WallSmashCooldown = 0.5f;

    /// <summary>
    /// Minimum absolute current speed required to smash a wall. 0 = no gate (heavy vehicles like tanks/APCs).
    /// Light vehicles set this > 0 so they can only ram at speed.
    /// </summary>
    [DataField]
    public float WallSmashMinSpeed;

    /// <summary>Server-tracked time of the next allowed smash tick. Not for YAML.</summary>
    [AutoNetworkedField]
    public TimeSpan NextWallSmashAt;

    /// <summary>
    /// Hull integrity damage dealt to the vehicle when it rams a mob (not counting the damage to the mob itself).
    /// Light vehicles set this &gt; 0 so hitting people hurts the van; heavy vehicles leave it at 0.
    /// Same smash cooldown as wall-smash applies.
    /// </summary>
    [DataField]
    public float MobCollisionHullDamage;

    /// <summary>
    /// Speed multiplier applied at 0 frame integrity. At full integrity the multiplier is 1; it scales
    /// linearly toward this value as integrity decreases. 1 = no penalty. 0.1 = 90% speed loss at death.
    /// </summary>
    [DataField]
    public float SpeedAtZeroIntegrity = 1f;

    /// <summary>
    /// Seconds the vehicle is rendered immobile after a hard-wall crash (smash succeeded or blocked).
    /// 0 = no immobility. Light vehicles set this high (90-120s) so ramming is a one-shot commit.
    /// Only triggers when the crash happens at or above <see cref="WallSmashMinSpeed"/>.
    /// </summary>
    [DataField]
    public float CrashImmobileDuration;

    [DataField]
    public float CrashImmobileMinSpeed;

    /// <summary>
    /// Seconds the vehicle is rendered immobile after hitting a humanoid mob. 0 = no lockout.
    /// Unlike wall crashes, this does NOT require <see cref="WallSmashMinSpeed"/> — any mob collision triggers.
    /// </summary>
    [DataField]
    public float MobCrashImmobileDuration;

    /// <summary>
    /// Seconds the vehicle is rendered immobile after hitting a xeno mob. 0 = no lockout.
    /// Typically larger than <see cref="MobCrashImmobileDuration"/> — xenos are bigger and more punishing to hit.
    /// </summary>
    [DataField]
    public float XenoMobCrashImmobileDuration;

    /// <summary>Server-tracked: vehicle cannot accelerate until this time. Not for YAML.</summary>
    [AutoNetworkedField]
    public TimeSpan ImmobileUntil;
}
