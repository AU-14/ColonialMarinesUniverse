using Robust.Shared.Player;

namespace Content.Server.Construction;

public sealed partial class ConstructionSystem
{
    // Manual construction and a UGV can both receive orders for the same client ghost.
    internal bool TryReserveGhost(ICommonSession session, int ghostId)
    {
        if (!_beingBuilt.TryGetValue(session, out var ghosts))
            _beingBuilt.Add(session, ghosts = new());

        return ghosts.Add(ghostId);
    }

    internal void ReleaseGhost(ICommonSession session, int ghostId)
    {
        if (_beingBuilt.TryGetValue(session, out var ghosts) && ghosts.Remove(ghostId) && ghosts.Count == 0)
            _beingBuilt.Remove(session);
    }
}
