using Content.Shared._RMC14.Medical.Stasis;

namespace Content.Shared.Body.Systems;

public sealed partial class BloodstreamSystem
{
    [Dependency] private CMStasisBagSystem _rmcBloodstreamStasis = default!;

    private bool CanRMCBodyMetabolize(EntityUid body)
    {
        return _rmcBloodstreamStasis.CanBodyMetabolize(body);
    }
}
