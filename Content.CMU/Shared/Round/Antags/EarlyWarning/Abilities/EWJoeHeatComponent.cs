using Content.Shared.Alert;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;

/// <summary>
/// The Early Warning Joe's Heat resource (phase E5, balance A-18 / D11), granted by the level-3 table.
/// Abilities add heat; it decays passively; reaching <see cref="Max"/> latches an overheat that hurts
/// and slows the Joe until heat falls below <see cref="OverheatClear"/>. Also carries the Evolution
/// bookkeeping (P3) so stand-down can restore what level 3 changed. Server state; the alert is the UI.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeHeatComponent : Component
{
    [DataField]
    public float Heat;

    [DataField]
    public float Max = 100f;

    /// <summary>Heat lost per second, always, even while Overclock adds its own (so Overclock nets +5 a second).</summary>
    [DataField]
    public float PassiveDecay = 3f;

    /// <summary>Heat gained per second while Overclock runs (D11's +8%/s; 10 in playtest 3, back to 8 in playtest 4).</summary>
    [DataField]
    public float OverclockHeat = 8f;

    [DataField]
    public bool Overheated;

    [DataField]
    public float OverheatClear = 60f;

    /// <summary>Flat Heat damage on overheat, applied ignoring the Joe's own Heat resistance.</summary>
    [DataField]
    public float OverheatDamage = 10f;

    [DataField]
    public TimeSpan OverheatSlow = TimeSpan.FromSeconds(4);

    [DataField]
    public ProtoId<AlertPrototype> Alert = "CMUEWJoeHeat";

    [DataField]
    public TimeSpan NextTick;

    // ---- Evolution (P3): stun resistance and slow modifier changed in code at level 3 ----

    [DataField]
    public bool EvolutionApplied;

    /// <summary>Stun resistance granted by Evolution (balance: ×4 instead of the synth's 2.5).</summary>
    [DataField]
    public float EvolutionStunResistance = 4f;

    /// <summary>What the Joe had before (the synth default); restored on stand-down.</summary>
    [DataField]
    public float RestoreStunResistance = 2.5f;

    /// <summary>Slow modifier granted by Evolution (P3: 0.4, less affected by slows).</summary>
    [DataField]
    public float EvolutionSlowModifier = 0.4f;

    [DataField]
    public float RestoreSlowModifier = 2f;
}
