using Content.Shared.CMU14.GasMask;

// ReSharper disable CheckNamespace

namespace Content.Server.Fluids.EntitySystems;

// CMU14: gas-blocking gear (pred clan mask) keeps chem smoke off the wearer completely, like cmss13's chemsmoke.dm
public sealed partial class SmokeSystem
{
    [Dependency] private SharedGasMaskSystem _cmuGasMask = default!;

    private bool CMUBlocksSmoke(EntityUid entity)
    {
        return _cmuGasMask.IsGasBlocked(entity);
    }
}
