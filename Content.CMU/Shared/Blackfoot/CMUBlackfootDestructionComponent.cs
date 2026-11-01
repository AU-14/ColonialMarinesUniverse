using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Blackfoot;

[RegisterComponent]
public sealed partial class CMUBlackfootDestructionComponent : Component
{
    [DataField]
    public TimeSpan FallDelay = TimeSpan.FromSeconds(4);

    [DataField]
    public float SpinAcceleration = 120f;

    [DataField]
    public EntProtoId CrashEffect = "CMUBlackfootCrashEffect";

    [DataField]
    public int CrashEffectCount = 4;

    [DataField]
    public float CrashEffectSpread = 1.8f;

    [DataField]
    public SoundSpecifier CrashSound = new SoundPathSpecifier("/Audio/_RMC14/Explosion/bigboom3.ogg");

    [DataField]
    public float SoundRange = 35f;

    [DataField]
    /// <summary>Tiles.</summary>
    public float CameraShakeRange = 14f;

    [DataField]
    public string ExplosionType = "RMC";

    [DataField]
    public float ExplosionTotalIntensity = 100f;

    [DataField]
    public float ExplosionSlope = 10f;

    [DataField]
    public float ExplosionMaxTileIntensity = 12f;

    [DataField]
    public EntProtoId FireProto = "RMCTileFire";

    [DataField]
    /// <summary>Tiles around the wreck.</summary>
    public int FireRange = 2;

    [DataField]
    public float EjectionDistance = 4f;

    [DataField]
    public float EjectionSpeed = 8f;

    [DataField]
    public int CrewFireIntensity = 30;

    [DataField]
    public int CrewFireDuration = 40;
}
