using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Combat.CQC;

/// <summary>
/// A CQC sequence. When a body's last inputs, all on one target and each within its
/// <see cref="CMUCqcComponent.MaxGap"/> of the previous, end in <see cref="Steps"/>, the sequence runs
/// <see cref="Effect"/> (an id in the server's CQC effect registry). Sequences are free: they rely only on the
/// input. Longest sequences are tried first. A body knows the sequences its <see cref="CMUCqcComponent.Sequences"/>
/// lists.
/// </summary>
[Prototype("cmuCqcSequence")]
public sealed partial class CMUCqcSequencePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<CMUCqcInput> Steps = new();

    /// <summary>Effect id in the server's registry, such as Grip (generic) or Sweep, Desecrate, Slam, ChokeOpen and Unmaking (Early Warning).</summary>
    [DataField(required: true)]
    public string Effect = string.Empty;

    /// <summary>
    /// A rank for owners that gate their sequences, interpreted by the owner through
    /// <see cref="CMUCqcSequenceAttemptEvent"/> (the Early Warning Joe refuses a sequence above its level). The
    /// generic system ignores it.
    /// </summary>
    [DataField]
    public int Tier;

    /// <summary>The body must hold the target in an aggressive grab when the last step lands.</summary>
    [DataField]
    public bool RequiresGrab;

    /// <summary>
    /// Every <see cref="CMUCqcInput.Wide"/> step must come from a blade (the Early Warning Joe's machete, or a weapon
    /// that deals Slash). Fists cannot wide-swing.
    /// </summary>
    [DataField]
    public bool RequiresBlade;

    /// <summary>
    /// Hard crowd control: refused on a target still inside its CC-immunity window, and opens that window when it
    /// fires.
    /// </summary>
    [DataField]
    public bool HardCc;

    /// <summary>A tighter gap between this sequence's steps than the body's own max gap, in seconds.</summary>
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
