namespace Content.Shared._RMC14.Xenonids.Leap;

public sealed partial class XenoLeapSystem
{
    /// <summary>
    /// Removes innate leap protection while retaining and recalculating equipped shield protection.
    /// </summary>
    public void RemoveInherentLeapProtection(Entity<RMCLeapProtectionComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        ent.Comp.InherentStunDuration = null;
        if (RefreshLeapProtection(ent, ent.Comp))
            RemComp<RMCLeapProtectionComponent>(ent);
    }
}
