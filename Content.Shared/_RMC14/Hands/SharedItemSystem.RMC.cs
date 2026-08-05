using Content.Shared._RMC14.Hands;

namespace Content.Shared.Item;

public abstract partial class SharedItemSystem
{
    private void RaiseRmcItemPickedUp(EntityUid item, EntityUid user)
    {
        var ev = new ItemPickedUpEvent(user, item);
        RaiseLocalEvent(item, ref ev, broadcast: true);
    }
}
