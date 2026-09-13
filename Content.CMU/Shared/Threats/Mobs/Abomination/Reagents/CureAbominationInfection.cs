using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Threats.Mobs.Abomination.Reagents;

/// <summary>
///     Reagent effect that purges <see cref="AbominationInfectionComponent" />
///     from the target on metabolism. Used by the WeYu counteragent: the
///     expensive sure-thing alternative to gambling a limb on amputation.
/// </summary>
public sealed partial class CureAbominationInfection : EntityEffectBase<CureAbominationInfection>
{
    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-cure-abomination-infection");
}
