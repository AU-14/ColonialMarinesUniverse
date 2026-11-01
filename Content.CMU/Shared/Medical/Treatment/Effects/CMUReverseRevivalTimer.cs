using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Medical.Treatment.Effects;

/// <summary>
/// Reverses the revival countdown according to the actual dose metabolized.
/// The reagent prototype supplies the temperature and dead-patient conditions.
/// </summary>
[UsedImplicitly]
public sealed partial class CMUReverseRevivalTimer : EntityEffectBase<CMUReverseRevivalTimer>
{
    [DataField]
    public float SecondsPerUnit = 20f;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("cmu-medical-reverse-revival-timer-guidebook", ("seconds", SecondsPerUnit));
}
