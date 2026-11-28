using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Combat.CQC;

/// <summary>
/// A body that fights with CQC sequences (<see cref="CMUCqcSequencePrototype"/>). The server's CQC system records
/// its close-quarters inputs (fist hits, wide swings, disarms, grabs and landed abilities) in <see cref="Buffer"/>
/// and fires the longest listed sequence the buffer ends in. It also keeps the per-target hard-CC immunity windows.
/// Owners hook in through the CQC events (the Early Warning Joe's Liturgy meter, level gate and xenofauna scaling).
/// </summary>
/// <remarks>
/// Not networked: the client lists sequences from the prototypes, and the buffer and the windows are server state.
/// Every tuning default is the Early Warning Joe's, so <c>- type: CMUCqc</c> with only <c>sequences:</c> works.
/// </remarks>
[RegisterComponent]
public sealed partial class CMUCqcComponent : Component
{
    /// <summary>The sequences this body knows. Longest first, then by id, whatever the order here.</summary>
    [DataField]
    public List<ProtoId<CMUCqcSequencePrototype>> Sequences = new();

    // ---- guardrails ----

    /// <summary>
    /// After a hard CC on a target, sequences cannot hard-CC it (and the Early Warning Joe's hotbar CC on it is not
    /// empowered). Every hard CC (knockdown or paralyze) from this body, from any source, opens or refreshes the window.
    /// </summary>
    [DataField]
    public TimeSpan CcImmunity = TimeSpan.FromSeconds(6);

    /// <summary>
    /// A hard CC from this body on a target inside its <see cref="CcImmunity"/> window lasts this fraction of its
    /// duration. Applied after the owner's own scaling (<see cref="CMUCqcHardCcDurationEvent"/>).
    /// </summary>
    [DataField]
    public float CcWindowMultiplier = 0.5f;

    /// <summary>Per target, when its CC immunity ends. Server state.</summary>
    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan> CcImmuneUntil = new();

    // ---- sequences ----

    /// <summary>After any sequence fires, no sequence can fire for this long. Inputs still build the buffer.</summary>
    [DataField]
    public TimeSpan SequenceCooldown = TimeSpan.FromSeconds(4);

    /// <summary>When the next sequence may fire. Server state.</summary>
    [ViewVariables]
    public TimeSpan NextSequence;

    [DataField]
    public int BufferCap = 4;

    /// <summary>Longest pause between two inputs of one sequence; a longer one starts the buffer over.</summary>
    [DataField]
    public TimeSpan MaxGap = TimeSpan.FromSeconds(1.2);

    /// <summary>The last inputs, oldest first. Server state.</summary>
    [ViewVariables]
    public List<CMUCqcBufferEntry> Buffer = new();
}
