using Content.Shared._RMC14.Ghost;

namespace Content.Server.Ghost;

public sealed partial class GhostSystem
{
    private bool TryReturnToRMCBody(EntityUid ghost)
    {
        if (!_mind.TryGetMind(ghost, out var mindId, out _) ||
            CompOrNull<RMCGhostReturnComponent>(ghost)?.Target is not { } target ||
            TerminatingOrDeleted(target))
        {
            return false;
        }

        _mind.TransferTo(mindId, target);
        return true;
    }
}
