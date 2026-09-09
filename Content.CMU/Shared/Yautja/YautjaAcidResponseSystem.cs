namespace Content.Shared.CMU14.Yautja;

/// <summary>
/// Central policy for acid movement effects on regular Yautja.
/// </summary>
public sealed partial class YautjaAcidResponseSystem : EntitySystem
{
    public bool ShouldSkipAcidMoveEffects(EntityUid target)
    {
        return HasComp<YautjaComponent>(target) && !HasComp<YautjaBadBloodComponent>(target);
    }
}
