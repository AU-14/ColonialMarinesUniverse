using Content.Shared._RMC14.Inventory;

namespace Content.Shared.Hands.EntitySystems;

public abstract partial class SharedHandsSystem
{
    private void RaiseRmcDropped(EntityUid item, EntityUid user)
    {
        var ev = new RMCDroppedEvent(user);
        RaiseLocalEvent(item, ref ev, broadcast: true);
    }
}
