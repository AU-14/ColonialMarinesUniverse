using System.Numerics;

namespace Content.Shared._RMC14.Buckle;

// CMU14
public sealed partial class RMCBuckleSystem
{
    /// <summary>
    ///     Sets the per-buckle offset added to a strap's own offset, or removes it when <paramref name="offset"/> is null.
    ///     Set it before buckling: the buckle transform check unbuckles anything that drifts from the expected position.
    /// </summary>
    public void CMUSetOffset(EntityUid uid, Vector2? offset)
    {
        if (offset is not { } value)
        {
            RemComp<RMCBuckleOffsetComponent>(uid);
            return;
        }

        var comp = EnsureComp<RMCBuckleOffsetComponent>(uid);
        if (comp.Offset == value)
            return;

        comp.Offset = value;
        Dirty(uid, comp);
    }
}
