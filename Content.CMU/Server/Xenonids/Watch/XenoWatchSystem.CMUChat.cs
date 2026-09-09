// ReSharper disable CheckNamespace

using Content.Shared._RMC14.Xenonids;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._RMC14.Xenonids.Watch;

public sealed partial class XenoWatchSystem
{
    public bool CanXenoWatch(ICommonSession session, out EntityUid entity)
    {
        if (session.AttachedEntity is not { Valid: true } sessionEntity ||
            !HasComp<XenoComponent>(sessionEntity))
        {
            entity = default;
            return false;
        }

        entity = sessionEntity;
        return true;
    }

    public void XenoWatchRequest(ICommonSession player, NetEntity target)
    {
        if (!CanXenoWatch(player, out var watcher))
        {
            Log.Warning($"User {player.Name} tried to xeno watch {target} without being a xeno.");
            return;
        }

        var targetEntity = GetEntity(target);
        if (!Exists(targetEntity) || !HasComp<XenoComponent>(targetEntity))
        {
            Log.Warning($"User {player.Name} tried to xeno watch an invalid xeno entity id: {target}");
            return;
        }

        Watch(watcher, targetEntity);
    }
}
