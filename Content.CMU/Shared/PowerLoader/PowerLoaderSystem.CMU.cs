using Content.Shared.CMU14.PowerLoader;

// ReSharper disable CheckNamespace
namespace Content.Shared._RMC14.PowerLoader;

public sealed partial class PowerLoaderSystem
{
    /// <summary>
    /// CMU14: mobs with <see cref="CMULargeObjectGrabberComponent"/> may hold grabbables by hand
    /// and do not get the held-cargo crawl.
    /// </summary>
    private bool CMUIsLargeObjectGrabber(EntityUid user)
    {
        return HasComp<CMULargeObjectGrabberComponent>(user);
    }
}
