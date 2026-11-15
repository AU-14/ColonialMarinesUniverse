using Content.Shared._RMC14.Xenonids;
using Robust.Shared.GameObjects;

namespace Content.Shared._RMC14.Xenonids.Hive;

public abstract partial class SharedXenoHiveSystem
{
    /// <summary>
    /// Empty setup hives are not playable factions. Keep a hive available while it has living
    /// members, or banked larvae with a spawn point, so temporary lack of larvae does not hide it.
    /// </summary>
    public bool CMUIsHiveActive(Entity<HiveComponent> hive)
    {
        if (hive.Comp.BurrowedLarva > 0 && HasBurrowedLarvaSpawnPoint(hive))
            return true;

        var members = EntityQueryEnumerator<XenoComponent, HiveMemberComponent>();
        while (members.MoveNext(out var uid, out _, out var member))
        {
            if (member.Hive == hive.Owner && !_mobState.IsDead(uid) &&
                !TerminatingOrDeleted(uid) && !EntityManager.IsQueuedForDeletion(uid))
                return true;
        }

        return false;
    }
}
