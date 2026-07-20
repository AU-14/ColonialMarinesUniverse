using Content.Shared._RMC14.Hands;

namespace Content.Shared.Hands.EntitySystems;

public abstract partial class SharedHandsSystem
{
    [Dependency] private RMCHandsSystem _rmcHandsInteractions = default!;

    private bool TryRMCStorageEjectHand(EntityUid user, string handName)
    {
        return _rmcHandsInteractions.TryStorageEjectHand(user, handName);
    }
}
