using Content.Shared._RMC14.Medical.Stasis;

namespace Content.Server.Body.Systems;

public sealed partial class RespiratorSystem
{
    [Dependency] private CMStasisBagSystem _rmcRespiratorStasis = default!;

    private bool CanRMCBodyMetabolize(EntityUid body)
    {
        return _rmcRespiratorStasis.CanBodyMetabolize(body);
    }
}
