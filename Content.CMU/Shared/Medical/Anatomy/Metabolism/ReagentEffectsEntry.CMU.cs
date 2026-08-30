using Content.Shared.CMU14.Medical.Anatomy.Metabolism.Events;

namespace Content.Shared.Chemistry.Reagent;

public sealed partial class ReagentEffectsEntry
{
    /// <summary>
    /// Explicit CMU toxicity classifications carried over from the pre-stage metabolism groups.
    /// These must not be inferred from the broader upstream metabolism stage.
    /// </summary>
    [DataField("cmuToxicity")]
    public HashSet<CMUMetabolismClass> CMUToxicity = [];
}
