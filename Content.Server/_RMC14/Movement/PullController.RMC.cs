using Content.Shared._RMC14.Fireman;

namespace Content.Server.Movement.Systems;

public sealed partial class PullController
{
    private bool IsRMCFiremanCarried(EntityUid? entity)
    {
        return HasComp<BeingFiremanCarriedComponent>(entity);
    }
}
