using Content.Server.CMU14.Medical.Blood;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;

namespace Content.Server._RMC14.Medical.IV;

// CMU14: hooks the IV and blood pack transfers into CMU blood: flow speed, blood types, plasma and saline.
public sealed partial class IVDripSystem
{
    [Dependency] private CMUBloodSystem _cmuBlood = default!;

    private float CMUFlowMultiplier(EntityUid? iv, EntityUid pack)
        => _cmuBlood.GetFlowMultiplier(iv, pack);

    private void CMUOnBloodDrawn(EntityUid pack, EntityUid donor)
        => _cmuBlood.OnBloodDrawn(pack, donor);

    private void CMUProcessTransfusion(EntityUid pack, EntityUid recipient, BloodstreamComponent bloodstream, Solution transferred)
        => _cmuBlood.ProcessTransfusion(pack, recipient, bloodstream, transferred);
}
