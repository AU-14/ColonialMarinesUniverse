namespace Content.Shared._RMC14.Weapons.Ranged.Stacks;

public sealed partial class GunStacksSystem
{
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _cmuGunsByTarget = new();

    private void CMUInitializeTargetLifetime()
    {
        if (!_net.IsServer)
            return;

        SubscribeLocalEvent<GunStacksActiveComponent, ComponentShutdown>(CMUOnStacksShutdown);
        EntityManager.EntityDeleted += CMUOnTargetDeleted;
    }

    public override void Shutdown()
    {
        EntityManager.EntityDeleted -= CMUOnTargetDeleted;
        _cmuGunsByTarget.Clear();
        base.Shutdown();
    }

    private void CMUSetLastHit(Entity<GunStacksActiveComponent> gun, EntityUid target)
    {
        if (_net.IsServer)
        {
            CMUUntrack(gun);
            if (!_cmuGunsByTarget.TryGetValue(target, out var guns))
                _cmuGunsByTarget.Add(target, guns = new HashSet<EntityUid>());
            guns.Add(gun);
        }

        gun.Comp.LastHitEntity = target;
        Dirty(gun);
    }

    private void CMUOnStacksShutdown(Entity<GunStacksActiveComponent> gun, ref ComponentShutdown args) => CMUUntrack(gun);

    private void CMUUntrack(Entity<GunStacksActiveComponent> gun)
    {
        if (gun.Comp.LastHitEntity is not { } target || !_cmuGunsByTarget.TryGetValue(target, out var guns))
            return;

        guns.Remove(gun);
        if (guns.Count == 0)
            _cmuGunsByTarget.Remove(target);
    }

    private void CMUOnTargetDeleted(Entity<MetaDataComponent> target)
    {
        if (!_cmuGunsByTarget.Remove(target, out var guns))
            return;

        foreach (var uid in guns)
        {
            if (!TryComp<GunStacksActiveComponent>(uid, out var active) || active.LastHitEntity != target.Owner)
                continue;

            active.LastHitEntity = null;
            Dirty(uid, active);
        }
    }
}
