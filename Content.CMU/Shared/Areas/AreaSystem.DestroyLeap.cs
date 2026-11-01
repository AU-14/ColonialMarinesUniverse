namespace Content.Shared._RMC14.Areas;

public sealed partial class AreaSystem
{
    /// <summary>
    /// Destroy leaps can't land in noTunnel areas, unless the area opts back in.
    /// </summary>
    public bool CanDestroyLeapInto(Entity<AreaComponent> area)
    {
        return !area.Comp.NoTunnel || HasComp<CMUDestroyLeapAllowedComponent>(area);
    }
}
