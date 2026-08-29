using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Threats.Mobs.Abomination.Reagents;

/// <summary>
///     Reagent effect that applies <see cref="AbominationInfectionComponent" /> to
///     the target on metabolism. Used by the AbominationVenom chemical.
/// </summary>
public sealed partial class CauseAbominationInfection : EntityEffectBase<CauseAbominationInfection>
{
    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-cause-abomination-infection", ("chance", Probability));
}
