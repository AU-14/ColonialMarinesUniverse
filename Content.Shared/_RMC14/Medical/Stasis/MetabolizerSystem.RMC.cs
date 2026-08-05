using Content.Shared._RMC14.Medical.Stasis;
using Content.Shared.Body;
using Content.Shared.Chemistry.Components.SolutionManager;

namespace Content.Shared.Metabolism;

public sealed partial class MetabolizerSystem
{
    [Dependency] private CMStasisBagSystem _rmcMetabolismStasis = default!;

    private bool CanRMCOrganMetabolize(
        Entity<MetabolizerComponent, OrganComponent?, SolutionManagerComponent?> organ)
    {
        return _rmcMetabolismStasis.CanOrganMetabolize((organ, organ.Comp2));
    }
}
