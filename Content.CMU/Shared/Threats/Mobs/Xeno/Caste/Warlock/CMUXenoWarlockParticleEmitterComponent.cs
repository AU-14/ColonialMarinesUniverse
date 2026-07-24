using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Threats.Mobs.Xeno.Caste.Warlock;

[Serializable, NetSerializable]
public enum CMUXenoWarlockParticleEffect : byte
{
    PsychicCrushCharge,
    PsychicBlastCharge,
    CrushWarning,
    DroneOperatorTransfer,
    DroneAndroidDormant,
    DroneTransferConnect,
    DroneTransferDisconnect
}

public readonly record struct CMUXenoWarlockParticleProfile(
    string Color,
    int Count,
    float Spawning,
    float Lifespan,
    float Fade,
    float Grow,
    Vector2 Velocity,
    Vector2 Gravity,
    Vector2 DriftMin,
    Vector2 DriftMax,
    Vector2 PositionRadius,
    Vector2 ScaleMin,
    Vector2 ScaleMax,
    Vector2 HolderOffset,
    float MaxDirectedTravelPixels = 250f
);

public readonly record struct CMUXenoWarlockParticleMotion(Vector2 Velocity, Vector2 Gravity);

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(CMUXenoWarlockSystem))]
public sealed partial class CMUXenoWarlockParticleEmitterComponent : Component
{
    [DataField, AutoNetworkedField]
    public CMUXenoWarlockParticleEffect Effect;

    [DataField, AutoNetworkedField]
    public Vector2 MotionGravity;

    [DataField, AutoNetworkedField]
    public Vector2 MotionVelocity;

    [DataField, AutoNetworkedField]
    public bool UseMotionOverride;

    // Runtime cap (in pixels) on how far a directed particle can travel from origin. Set by the
    // spawning system when the effect needs to hug a specific in-world distance (e.g. the psychic
    // blast wind-up matching the projectile's actual impact point). null = use profile default.
    [DataField, AutoNetworkedField]
    public float? MaxDirectedTravelPixelsOverride;
}
