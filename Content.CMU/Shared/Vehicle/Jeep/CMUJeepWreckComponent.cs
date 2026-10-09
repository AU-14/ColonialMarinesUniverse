using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// Blows the jeep up once damage takes its hull to zero: its engine bursts into flames, with smoke,
/// sparks and the crackle of the fire, then its riders are thrown clear, the blast from its
/// <c>Explosive</c> goes off, and the jeep and everything fitted to it give way to a burnt-out wreck,
/// debris and fire. Server only.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CMUJeepWreckComponent : Component
{
    /// <summary>
    /// The engine fire between the hull giving out and the blast: time enough to bail out.
    /// </summary>
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(6);

    /// <summary>
    /// The fire's crackle, looped on the jeep until it goes up.
    /// </summary>
    [DataField]
    public SoundSpecifier? WarningSound = new SoundPathSpecifier("/Audio/Effects/burning.ogg");

    /// <summary>
    /// The engine fire's flickering glow, spawned on the jeep's engine bay.
    /// </summary>
    [DataField]
    public EntProtoId FireLightPrototype = "CMUJeepFireLight";

    /// <summary>
    /// Where the engine bay is, in vehicle space (facing south).
    /// </summary>
    [DataField]
    public Vector2 EngineBay = new(0f, -0.6f);

    /// <summary>
    /// Sparks and the bang of something giving way in the burning engine bay, every so often.
    /// </summary>
    [DataField]
    public EntProtoId SparkPrototype = "EffectSparks";

    [DataField]
    public SoundSpecifier? SparkSound = new SoundCollectionSpecifier("sparks");

    [DataField]
    public SoundSpecifier? PopSound = new SoundCollectionSpecifier("MetalBreak");

    [DataField]
    public TimeSpan MinSparkDelay = TimeSpan.FromSeconds(0.6);

    [DataField]
    public TimeSpan MaxSparkDelay = TimeSpan.FromSeconds(1.5);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextSpark;

    [DataField]
    public LocId WarningPopup = "cmu-jeep-wreck-warning";

    [DataField]
    public EntProtoId WreckPrototype = "CMUJeepWreckCargo";

    [DataField]
    public List<EntProtoId> DebrisPrototypes = new()
    {
        "CMUJeepDebrisWheel",
        "CMUJeepDebrisWindshield",
        "CMUJeepDebrisHeadlight",
        "CMUJeepDebrisEngine",
    };

    [DataField]
    public int MinDebris = 2;

    [DataField]
    public int MaxDebris = 4;

    [DataField]
    public EntProtoId OilSpawnerPrototype = "RMCDecalSpawnerOilSplatters";

    [DataField]
    public int OilSplatters = 3;

    /// <summary>
    /// Fire left burning under the wreck's middle and engine bay, clear of where the crew is thrown.
    /// </summary>
    [DataField]
    public EntProtoId FirePrototype = "RMCTileFire";

    [DataField]
    public int FireDuration = 15;

    /// <summary>
    /// How far, in tiles, and how fast riders are thrown out from their seats' exits.
    /// </summary>
    [DataField]
    public float EjectDistance = 2f;

    [DataField]
    public float EjectSpeed = 6f;

    /// <summary>
    /// How far, in tiles, and how fast a loaded crate is thrown off the back.
    /// </summary>
    [DataField]
    public float CrateThrowDistance = 1.5f;

    [DataField]
    public float CrateThrowSpeed = 4f;

    /// <summary>
    /// When the warned jeep goes up; null while its hull holds.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? DetonateAt;

    /// <summary>
    /// Whoever last damaged the jeep, credited with the blast in the admin logs.
    /// </summary>
    [ViewVariables]
    public EntityUid? LastAttacker;

    /// <summary>
    /// Hull integrity and capacity when last checked. Only a drop in integrity at the same capacity is
    /// damage; stripping the last working part off a jeep also reads zero but must not blow it up.
    /// </summary>
    [ViewVariables]
    public float LastHull = -1f;

    [ViewVariables]
    public float LastMaxHull = -1f;

    /// <summary>
    /// Damaged since the last check, so a hull already at zero still goes up when hit again.
    /// </summary>
    [ViewVariables]
    public bool Hit;
}
