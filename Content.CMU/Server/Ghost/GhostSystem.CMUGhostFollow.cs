// ReSharper disable CheckNamespace

using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Ghost;

public sealed partial class GhostSystem
{
    public bool CanGhostFollow(ICommonSession session, out EntityUid entity)
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

    public void GhostFollowRequest(ICommonSession player, NetEntity target)
    {
        if (!CanGhostFollow(player, out var attached))
        {
            Log.Warning($"User {player.Name} tried to follow {target} without being a ghost.");
            return;
        }

        var targetEntity = GetEntity(target);
        if (!Exists(targetEntity))
        {
            Log.Warning($"User {player.Name} tried to follow an invalid entity id: {target}");
            return;
        }

        _followerSystem.StartFollowingEntity(attached, targetEntity);
    }
}
