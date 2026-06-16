// ReSharper disable CheckNamespace

using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Ghost;

public sealed partial class GhostSystem
{
    public bool CanGhostWarp(ICommonSession session, out EntityUid entity)
    {
        if (session.AttachedEntity is not { Valid: true } sessionEntity ||
            !_ghostQuery.HasComp(sessionEntity))
        {
            entity = default;
            return false;
        }

        entity = sessionEntity;
        return true;
    }

    public void GhostWarpRequest(ICommonSession player, NetEntity target)
    {
        if (!CanGhostWarp(player, out var attached))
        {
            Log.Warning($"User {player.Name} tried to warp to {target} without being a ghost.");
            return;
        }

        var targetEntity = GetEntity(target);
        if (!Exists(targetEntity))
        {
            Log.Warning($"User {player.Name} tried to warp to an invalid entity id: {target}");
            return;
        }

        WarpTo(attached, targetEntity);
    }
}
