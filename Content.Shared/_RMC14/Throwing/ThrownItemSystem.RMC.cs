namespace Content.Shared.Throwing;

public sealed partial class ThrownItemSystem
{
    private bool CanRMCThrownItemHitThrower(EntityUid uid)
    {
        return HasComp<Content.Shared._RMC14.Throwing.ThrownHitUserComponent>(uid);
    }
}
