using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Threats.Mobs.Biomorph.Reagents;

/// <summary>
///     Reagent effect that applies <see cref="BiomorphInfectionComponent" /> to
///     the target on metabolism. Used by the AbominationVenom chemical.
/// </summary>
public sealed partial class CauseBiomorphInfection : EntityEffectBase<CauseBiomorphInfection>
{
    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-cause-biomorph-infection", ("chance", Probability));
}
