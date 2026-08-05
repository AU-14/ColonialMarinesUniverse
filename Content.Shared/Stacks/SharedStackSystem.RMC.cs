namespace Content.Shared.Stacks;

public abstract partial class SharedStackSystem
{
    /// <summary>
    /// Legacy RMC wrapper for <see cref="TryUse"/>.
    /// </summary>
    public bool Use(EntityUid uid, int amount, StackComponent? stack = null)
    {
        return TryUse((uid, stack), amount);
    }

    /// <summary>
    /// Legacy RMC overload for callers which have not yet adopted entity wrappers.
    /// </summary>
    public int GetCount(EntityUid uid, StackComponent? stack = null)
    {
        return GetCount((uid, stack));
    }
}
