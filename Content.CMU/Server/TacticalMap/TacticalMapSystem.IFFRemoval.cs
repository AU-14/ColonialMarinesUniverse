using Content.Shared._RMC14.TacticalMap;

namespace Content.Server._RMC14.TacticalMap;

public sealed partial class TacticalMapSystem
{
    public void OnIFFRemoved(EntityUid target)
    {
        if (TryComp<ActiveTacticalMapTrackedComponent>(target, out var tracked))
            BreakTracking((target, tracked));
    }
}
