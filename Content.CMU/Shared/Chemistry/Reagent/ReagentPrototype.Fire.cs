// ReSharper disable CheckNamespace

namespace Content.Shared.Chemistry.Reagent;

public partial class ReagentPrototype
{
    /// <summary>
    /// Fires fed by this reagent ignore the target's damage modifier set, including synthetic and Yautja heat
    /// resistance. Emitted by the reagent generator for the Firepenetrating property and read by
    /// <c>CMUFuelProfileSystem</c> when a flamer tank blend is evaluated.
    /// </summary>
    [DataField]
    public bool FirePenetrating;

    // CMU14: FLOW-23 fire-penetrating levels.
    /// <summary>
    /// Level of the Firepenetrating property (1-5), emitted by the reagent generator as
    /// <c>firePenetratingLevel</c>. 0 means unset; see <see cref="EffectiveFirePenetratingLevel"/>.
    /// From level 3 the reagent's tile fires melt structures (<c>CMUStructureMeltingFireComponent</c>).
    /// </summary>
    [DataField]
    public int FirePenetratingLevel;

    // CMU14: FLOW-23 fire-penetrating levels.
    /// <summary>
    /// The penetration level to use: <see cref="FirePenetratingLevel"/> when set, otherwise 1 for reagents that
    /// only carry the legacy <see cref="FirePenetrating"/> flag, otherwise 0.
    /// </summary>
    public int EffectiveFirePenetratingLevel => FirePenetratingLevel > 0 ? FirePenetratingLevel : (FirePenetrating ? 1 : 0);
}
