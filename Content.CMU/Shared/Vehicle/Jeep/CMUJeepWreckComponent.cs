using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// Blows the jeep up once damage takes its hull to zero: after a short warning its riders are thrown
/// clear, the blast from its <c>Explosive</c> goes off, and the jeep and everything fitted to it give
/// way to a burnt-out wreck, debris and fire. Server only.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CMUJeepWreckComponent : Component
{
    /// <summary>
    /// Warning between the hull giving out and the blast.
    /// </summary>
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(2);

    [DataField]
    public SoundSpecifier? WarningSound = new SoundPathSpecifier("/Audio/Effects/burning.ogg");

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
