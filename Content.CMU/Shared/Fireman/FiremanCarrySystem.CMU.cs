// ReSharper disable CheckNamespace
namespace Content.Shared._RMC14.Fireman;

public sealed partial class FiremanCarrySystem
{
    // CanThrow lives on the carried mob and humans have it off, so preds couldn't throw who they were carrying.
    // false means nothing changed and there's nothing to put back
    public bool TryAllowCarriedThrow(EntityUid carrier, EntityUid carried)
    {
        if (!TryComp(carried, out FiremanCarriableComponent? carriable) ||
            carriable.CanThrow ||
            !carriable.BeingCarried ||
            !TryComp(carrier, out CanFiremanCarryComponent? carry) ||
            carry.Carrying != carried)
        {
            return false;
        }

        carriable.CanThrow = true;
        Dirty(carried, carriable);
        return true;
    }

    public void SetCarriedThrowable(EntityUid carried, bool canThrow)
    {
        if (!TryComp(carried, out FiremanCarriableComponent? carriable) || carriable.CanThrow == canThrow)
            return;

        carriable.CanThrow = canThrow;
        Dirty(carried, carriable);
    }
}
