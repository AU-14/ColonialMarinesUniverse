using Content.Shared._CMU14.Medical.Items;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Bones;

public abstract class SharedFractureSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;

    /// <summary>
    ///     Pass <see cref="FractureSeverity.None"/> to clear the fracture entirely.
    ///     Without <paramref name="forceUpgrade"/> a request that doesn't strictly
    ///     raise the tier is ignored — that keeps the bone-spawn path's
    ///     "compute then assign" cycle from accidentally downgrading a Compound
    ///     to a Simple on a small subsequent hit. Surgical downgrades opt in by
    ///     passing <c>forceUpgrade: false</c> after explicitly checking the new
    ///     tier is lower than the current one.
    /// </summary>
    public void SetSeverity(Entity<FractureComponent> ent, FractureSeverity newSev, bool forceUpgrade = true)
    {
        var current = ent.Comp.Severity;

        if (newSev == FractureSeverity.None)
        {
            RemComp<FractureComponent>(ent);
            return;
        }

        if (forceUpgrade && newSev <= current)
            return;

        ent.Comp.Severity = newSev;
        ent.Comp.AppearedAt = Timing.CurTime;
        ent.Comp.IsBleeding = FractureProfile.Get(newSev).BloodlossPerSecond > 0;
        Dirty(ent);
    }

    /// <summary>
    ///     Reads the part's fracture severity through any suppression sources.
    ///     A cast (stronger) wins over a splint when both are present. Underlying
    ///     fracture data is untouched, so removing the suppressor restores the
    ///     real severity.
    /// </summary>
    public FractureSeverity GetEffectiveSeverity(Entity<FractureComponent?> part)
    {
        if (!Resolve(part.Owner, ref part.Comp, logMissing: false))
            return FractureSeverity.None;

        var sev = part.Comp.Severity;
        FractureSeverity? suppressorMax = null;
        if (TryComp<CMUCastComponent>(part.Owner, out var cast))
            suppressorMax = cast.MaxSuppressed;
        else if (TryComp<CMUSplintedComponent>(part.Owner, out var splint))
            suppressorMax = splint.MaxSuppressed;

        if (suppressorMax is { } max && (byte)sev <= (byte)max)
            return FractureSeverity.None;

        return sev;
    }
}
