using Robust.Shared.Timing;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;

/// <summary>
/// What an <see cref="EWJoeTallyConditionComponent"/> directive counts. ARCH-11: the old destroy kinds
/// (cameras, telecomms, threat structures) became Offer directives (<c>EWJoeOfferConditionComponent</c>,
/// Meld or consume), so only Bear Witness is left here.
/// </summary>
public enum EWJoeTallyKind : byte
{
    /// <summary>Bear Witness: distinct living victims of the Joe's Judgement or Test of Faith.</summary>
    Witness,
}

/// <summary>
/// ARCH-03 batch 2: an Early Warning Joe counter directive with its own count, so the assignment check can
/// refuse it when the planet cannot supply it (the upstream NumberObjective rolls its target in an order this
/// cannot rely on). Each target counts once.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeTallyConditionComponent : Component
{
    [DataField(required: true)]
    public EWJoeTallyKind Kind;

    /// <summary>Inclusive bounds the target is rolled from.</summary>
    [DataField]
    public int Min = 1;

    [DataField]
    public int Max = 1;

    /// <summary>The rolled target. Set on assignment.</summary>
    [DataField]
    public int Target;

    /// <summary>Progress so far.</summary>
    [DataField]
    public int Count;

    /// <summary>Everything already credited, so a target never counts twice.</summary>
    [DataField]
    public HashSet<EntityUid> Counted = new();

    /// <summary>
    /// Witness only: refused unless at least this many living organic people stand on the planet, so a
    /// near-empty colony is not asked for a crowd's worth of judgement.
    /// </summary>
    [DataField]
    public int MinPopulation;

    /// <summary>
    /// RB-33, Witness only: at most this many people counted per cast of Test of Faith or Judgement (0: no cap).
    /// A cast flashes its whole cone in one tick, so the cap is kept per server tick (<see cref="CastTick"/>).
    /// </summary>
    [DataField]
    public int MaxPerCast;

    /// <summary>The tick of the cast last credited. Server bookkeeping.</summary>
    [ViewVariables]
    public GameTick CastTick;

    /// <summary>How many that cast has counted so far. Server bookkeeping.</summary>
    [ViewVariables]
    public int CastCredits;

    /// <summary>Title locale id; receives "count".</summary>
    [DataField(required: true)]
    public LocId Title;

    /// <summary>Description locale id; receives "count".</summary>
    [DataField(required: true)]
    public LocId Description;
}
