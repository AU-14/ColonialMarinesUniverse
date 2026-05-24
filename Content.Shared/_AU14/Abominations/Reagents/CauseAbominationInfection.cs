using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._AU14.Abominations.Reagents;

/// <summary>
/// Reagent effect that applies <see cref="AbominationInfectionComponent"/> to
/// the target on metabolism. Used by the AbominationVenom chemical.
/// </summary>
public sealed partial class CauseAbominationInfection : EventEntityEffect<CauseAbominationInfection>
{
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-cause-abomination-infection", ("chance", Probability));
}
