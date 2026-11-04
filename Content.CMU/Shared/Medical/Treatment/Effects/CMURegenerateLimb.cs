using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Medical.Treatment.Effects;

/// <summary>
/// Regenerates one missing native limb per five units of metabolized Necrosol.
/// The reagent prototype supplies the cryogenic temperature condition.
/// </summary>
[UsedImplicitly]
public sealed partial class CMURegenerateLimb : EntityEffectBase<CMURegenerateLimb>
{
    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("cmu-medical-regenerate-limb-guidebook");
}
