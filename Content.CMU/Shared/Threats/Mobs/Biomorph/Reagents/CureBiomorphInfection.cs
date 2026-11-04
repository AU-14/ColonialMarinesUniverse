using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Threats.Mobs.Biomorph.Reagents;

/// <summary>
///     Reagent effect that purges <see cref="BiomorphInfectionComponent" />
///     from the target on metabolism. Used by the WeYu counteragent: the
///     expensive sure-thing alternative to gambling a limb on amputation.
/// </summary>
public sealed partial class CureBiomorphInfection : EntityEffectBase<CureBiomorphInfection>
{
    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-cure-biomorph-infection");
}
