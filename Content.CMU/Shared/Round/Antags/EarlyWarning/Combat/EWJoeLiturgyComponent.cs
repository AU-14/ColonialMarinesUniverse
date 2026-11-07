using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning.Combat;

/// <summary>
/// ARCH-05 (node N12): the Early Warning Joe's "Liturgy" momentum meter, granted at level 0 by the level
/// table. Hits, disarms, grabs and landed close-quarters abilities build <see cref="Stacks"/>; the meter
/// empties when <see cref="Window"/> passes with no new input, and breaks when the Joe is stunned or knocked
/// down. Each stack speeds the Joe's melee and shortens incoming slows (ARCH-12). At <see cref="MaxStacks"/> the
/// Joe is <see cref="Exalted"/>: its next close-quarters move plays its empowered version. Also holds the sequence
/// buffer (ARCH-06) and the per-target CC-immunity windows (FLOW-06), which are server state.
/// </summary>
/// <remarks>
/// Networked to the Joe only (only the stacks and the Exalted flag), like <see cref="EWJoeComponent"/>, so its
/// presence never identifies the antag to other clients. What others see is <see cref="EWJoeLiturgyGlowComponent"/>,
/// which exists only while the meter is up. The meter runs on the server (<c>EWJoeLiturgySystem</c>); the passive
/// buffs are shared (<see cref="EWJoeLiturgyBuffsSystem"/>) so the owner predicts its own attack rate.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EWJoeLiturgyComponent : Component
{
    public override bool SendOnlyToOwner => true;

    [DataField, AutoNetworkedField]
    public int Stacks;

    [DataField, AutoNetworkedField]
    public int MaxStacks = 5;

    /// <summary>The next close-quarters move (hotbar or sequence) is empowered. Reached at max stacks.</summary>
    [DataField, AutoNetworkedField]
    public bool Exalted;

    /// <summary>ARCH-12: spending Exalted leaves the Joe this many stacks (or fewer, if it had fewer).</summary>
    [DataField]
    public int StacksAfterEmpower = 2;

    // ---- passive buffs (ARCH-12, EWJoeLiturgyBuffsSystem; predicted from the owner-networked stacks) ----

    /// <summary>Melee attack rate multiplier per stack, on fists and the machete: 1 + this x stacks.</summary>
    [DataField, AutoNetworkedField]
    public float AttackRatePerStack = 0.04f;

    /// <summary>Incoming slow, superslow and root durations are cut by this per stack (floored at zero).</summary>
    [DataField, AutoNetworkedField]
    public float SlowReductionPerStack = 0.08f;

    /// <summary>How long the meter holds after the last gain.</summary>
    [DataField]
    public TimeSpan Window = TimeSpan.FromSeconds(3);

    /// <summary>The window is this much longer while Overclock runs.</summary>
    [DataField]
    public float OverclockWindowMultiplier = 1.5f;

    /// <summary>When the stacks fall to zero. Server bookkeeping.</summary>
    [ViewVariables]
    public TimeSpan ExpiresAt;

    // ---- gains ----

    [DataField]
    public int HitGain = 1;

    /// <summary>Per body hit by a wide swing, up to <see cref="WideGainCap"/>.</summary>
    [DataField]
    public int WideGain = 1;

    [DataField]
    public int WideGainCap = 2;

    [DataField]
    public int DisarmGain = 1;

    /// <summary>A passive pull builds nothing; it only feeds the sequence buffer.</summary>
    [DataField]
    public int GrabGain;

    [DataField]
    public int AggressiveGrabGain = 1;

    [DataField]
    public int AbilityGain = 2;

    // ---- rewards ----

    /// <summary>Each landed hit takes this off the remaining cooldown of every close-quarters action.</summary>
    [DataField]
    public TimeSpan CooldownCutPerHit = TimeSpan.FromSeconds(0.5);

    /// <summary>The close-quarters actions the cooldown cut applies to, by prototype.</summary>
    [DataField]
    public List<EntProtoId> CqcActions = new()
    {
        "CMUActionEWJoeDrown",
        "CMUActionEWJoeSweep",
        "CMUActionEWJoeSlam",
        "CMUActionEWJoeChoke",
        "CMUActionEWJoeThrow",
        "CMUActionEWJoeJudgement",
        "CMUActionEWJoeDesecrate",
    };

    /// <summary>Empowered Sweep: the knockdown is this much longer.</summary>
    [DataField]
    public float EmpoweredSweepMultiplier = 1.5f;

    /// <summary>Empowered Slam: damage multiplier (+50%).</summary>
    [DataField]
    public float EmpoweredSlamDamageMultiplier = 1.5f;

    /// <summary>Empowered Slam: the paralyze is this much longer.</summary>
    [DataField]
    public float EmpoweredSlamParalyzeMultiplier = 1.5f;

    /// <summary>Empowered Throw: the RMC obstacle slam bonus is always armed with these.</summary>
    [DataField]
    public TimeSpan EmpoweredThrowSlamParalyze = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan EmpoweredThrowSlamSlow = TimeSpan.FromSeconds(2);

    /// <summary>Empowered Judgement: the cone is this much wider (capped at a full circle).</summary>
    [DataField]
    public float EmpoweredJudgementConeMultiplier = 1.5f;

    // ---- guardrails (FLOW-06) ----

    /// <summary>After a hard CC on a target, sequences cannot hard-CC it and hotbar CC on it is not empowered.</summary>
    [DataField]
    public TimeSpan CcImmunity = TimeSpan.FromSeconds(6);

    /// <summary>Per target, when its CC immunity ends. Server state.</summary>
    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan> CcImmuneUntil = new();

    // ---- sequences (ARCH-06) ----

    [DataField]
    public int BufferCap = 4;

    /// <summary>Longest pause between two inputs of one sequence; a longer one starts the buffer over.</summary>
    [DataField]
    public TimeSpan MaxGap = TimeSpan.FromSeconds(1.2);

    /// <summary>The last inputs, oldest first. Server state.</summary>
    [ViewVariables]
    public List<EWJoeCqcBufferEntry> Buffer = new();

    // ---- feedback ----

    [DataField]
    public ProtoId<AlertPrototype> Alert = "CMUEWJoeLiturgy";
}

/// <summary>
/// ARCH-05 (node N12): the visible half of the Liturgy meter. Added by the server while the Joe has stacks or is
/// Exalted, removed when the meter empties, and networked to everyone so the Joe's eyes can step towards full
/// orange (CqcSequences §6.4). It carries nothing that outlives a fight.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class EWJoeLiturgyGlowComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Stacks;

    [DataField, AutoNetworkedField]
    public bool Exalted;

    /// <summary>Eye colour per stack count (index 0 = one stack). The last is Exalted: full orange.</summary>
    [DataField]
    public List<Color> EyeColors = new()
    {
        Color.FromHex("#ffd9a0"),
        Color.FromHex("#ffbf66"),
        Color.FromHex("#ffa033"),
        Color.FromHex("#ff8000"),
        Color.FromHex("#ff5a00"),
    };
}
