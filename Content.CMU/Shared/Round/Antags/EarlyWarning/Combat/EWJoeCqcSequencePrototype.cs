using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning.Combat;

/// <summary>
/// ARCH-05/06: one close-quarters input the Liturgy meter counts and the sequence buffer records.
/// </summary>
[Serializable, NetSerializable]
public enum EWJoeCqcInput : byte
{
    /// <summary>An unarmed or machete hit on a living body.</summary>
    Hit,

    /// <summary>A wide swing that hit at least one living body.</summary>
    Wide,

    /// <summary>A disarm (tackle) attempt on a body.</summary>
    Disarm,

    /// <summary>A pull started. A sequence step <c>Grab</c> also accepts an aggressive grab.</summary>
    Grab,

    /// <summary>A pull upgraded to an aggressive grab (the Machine Grip or the second Ctrl-click).</summary>
    AggressiveGrab,

    /// <summary>A close-quarters hotbar ability that landed.</summary>
    Ability,
}

/// <summary>
/// One recorded input: what, on whom (null for an untargeted ability, which matches any target) and when.
/// <paramref name="FromBlade"/> (ARCH-12): the swing came from a bladed weapon, not the Joe's fists.
/// </summary>
public readonly record struct EWJoeCqcBufferEntry(EWJoeCqcInput Input, EntityUid? Target, TimeSpan Time, bool FromBlade = false);

/// <summary>
/// ARCH-06 (node N13): a CQC sequence. When the Joe's last inputs, all on one target and each within the
/// meter's max gap of the previous, end in <see cref="Steps"/>, the sequence runs <see cref="Effect"/> (an id in
/// the server's effect registry, which calls the same public methods the hotbar abilities use). Sequences are
/// free (ARCH-12): they cost no Liturgy stacks and rely only on the input. Longest sequences are tried first.
/// </summary>
[Prototype("ewJoeCqcSequence")]
public sealed partial class EWJoeCqcSequencePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<EWJoeCqcInput> Steps = new();

    /// <summary>Effect id: Sweep, Grip, Desecrate, Slam, ChokeOpen or Unmaking.</summary>
    [DataField(required: true)]
    public string Effect = string.Empty;

    /// <summary>The lowest <see cref="EWJoeComponent.Level"/> the sequence works at.</summary>
    [DataField]
    public int MinLevel;

    /// <summary>The Joe must hold the target in an aggressive grab when the last step lands.</summary>
    [DataField]
    public bool RequiresGrab;

    /// <summary>
    /// ARCH-12: every <see cref="EWJoeCqcInput.Wide"/> step must come from a blade (the machete, or a weapon that
    /// deals Slash). Fists cannot wide-swing.
    /// </summary>
    [DataField]
    public bool RequiresBlade;

    /// <summary>
    /// Hard crowd control (FLOW-06): refused on a target still inside its CC-immunity window, and opens that
    /// window when it fires.
    /// </summary>
    [DataField]
    public bool HardCc;

    /// <summary>A tighter gap between this sequence's steps than the meter's own max gap, in seconds.</summary>
    [DataField]
    public float? MaxGapOverride;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public LocId? Hint;

    // ---- effect parameters (each effect reads the ones it needs) ----

    /// <summary>Sweep knockdown, Unmaking's knockdown of the main target. Null: the effect's default.</summary>
    [DataField]
    public TimeSpan? Duration;

    /// <summary>Desecrate: always the empowered (compound fracture) version, even without an Exalted Joe.</summary>
    [DataField]
    public bool Empowered;

    /// <summary>Unmaking: cone reach in tiles.</summary>
    [DataField]
    public float Range = 2.5f;

    /// <summary>Unmaking: cone width in degrees, centred on the target.</summary>
    [DataField]
    public float ConeDegrees = 90f;

    /// <summary>Unmaking: daze on everyone alive in the cone.</summary>
    [DataField]
    public TimeSpan DazeDuration = TimeSpan.FromSeconds(4);
}
