using System.Numerics;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.OnCollide;
using Content.Shared._RMC14.Projectiles;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Projectile;
using Content.Shared._RMC14.Xenonids.Projectile.Spit;
using Content.Shared._RMC14.Xenonids.Projectile.Spit.Charge;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Damage;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Robust.Shared.Network;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Venator;

public sealed partial class XenoVenatorSystem : EntitySystem
{
    private static readonly EntProtoId VenatorSpitCooldownId = "ActionXenoVenatorCorrosiveSpit";
    private static readonly TimeSpan StoredAcidCooldown = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan StoreAcidLockout = TimeSpan.FromSeconds(4);
    private static readonly Vector2[] XPattern =
    [
        new(-1, -1),
        new(-1, 1),
        new(1, -1),
        new(1, 1),
    ];
    private static readonly Vector2[] PlusPattern =
    [
        new(0, -1),
        new(-1, 0),
        new(1, 0),
        new(0, 1),
    ];

    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private CMArmorSystem _armor = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    [Dependency] private XenoProjectileSystem _xenoProjectile = default!;
    [Dependency] private XenoSpitSystem _xenoSpit = default!;

    private EntityQuery<ActionSharedCooldownComponent> _actionSharedCooldownQuery;
    private EntityQuery<DamageOnCollideComponent> _damageOnCollideQuery;

    public override void Initialize()
    {
        _actionSharedCooldownQuery = GetEntityQuery<ActionSharedCooldownComponent>();
        _damageOnCollideQuery = GetEntityQuery<DamageOnCollideComponent>();

        SubscribeLocalEvent<XenoVenatorComponent, CMGetArmorEvent>(OnGetArmor);
        SubscribeLocalEvent<XenoVenatorComponent, DamageModifyEvent>(OnDamageModify, after: [typeof(CMArmorSystem)]);
        SubscribeLocalEvent<XenoVenatorComponent, XenoStoreAcidActionEvent>(OnStoreAcidAction);
        SubscribeLocalEvent<XenoVenatorComponent, XenoVenatorSpitActionEvent>(OnSpitAction);
        SubscribeLocalEvent<XenoVenatorPoolOnHitComponent, ProjectileHitEvent>(OnProjectileHit, before: [typeof(RMCProjectileSystem)]);
        SubscribeLocalEvent<XenoVenatorPoolOnHitComponent, ProjectileFixedDistanceStopEvent>(OnProjectileFixedDistanceStop);
    }

    private void OnGetArmor(Entity<XenoVenatorComponent> xeno, ref CMGetArmorEvent args)
    {
        args.XenoArmor -= xeno.Comp.AcidCharges * xeno.Comp.ArmorPenaltyPerCharge;
    }

    private void OnDamageModify(Entity<XenoVenatorComponent> xeno, ref DamageModifyEvent args)
    {
        if (xeno.Comp.AcidCharges <= 0 || xeno.Comp.DamageTakenMultiplierPerAcidCharge <= 0)
            return;

        args.Damage *= 1 + xeno.Comp.AcidCharges * xeno.Comp.DamageTakenMultiplierPerAcidCharge;
    }

    private void OnStoreAcidAction(Entity<XenoVenatorComponent> xeno, ref XenoStoreAcidActionEvent args)
    {
        if (args.Handled)
            return;

        if (_timing.CurTime < xeno.Comp.StoreAcidLockedUntil)
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-venator-store-acid-locked"), xeno, xeno);
            return;
        }

        if (xeno.Comp.AcidCharges >= xeno.Comp.MaxAcidCharges)
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-venator-store-acid-full"), xeno, xeno);
            return;
        }

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        xeno.Comp.AcidCharges++;
        Dirty(xeno);
        _armor.UpdateArmorValue((xeno, null));
        _popup.PopupClient(Loc.GetString("cm-xeno-venator-store-acid", ("charges", xeno.Comp.AcidCharges)), xeno, xeno);
    }

    private void OnSpitAction(Entity<XenoVenatorComponent> xeno, ref XenoVenatorSpitActionEvent args)
    {
        if (args.Handled || !_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        var shot = _xenoProjectile.TryShoot(
            xeno,
            args.Target,
            0,
            args.Projectile,
            args.Sound,
            args.Shots,
            args.Deviation,
            args.Speed,
            stopAtTarget: args.StopAtTarget);

        if (!shot)
            return;

        xeno.Comp.StoreAcidLockedUntil = _timing.CurTime + StoreAcidLockout;
        if (args.UseStoreCharge && xeno.Comp.AcidCharges > 0)
        {
            xeno.Comp.AcidCharges--;
            _armor.UpdateArmorValue((xeno, null));

            var owner = xeno.Owner;
            Timer.Spawn(0, () =>
            {
                if (!Deleted(owner))
                    ShortenSharedCooldown(owner);
            });
        }

        Dirty(xeno);
    }

    private void ShortenSharedCooldown(EntityUid xeno)
    {
        var start = _timing.CurTime;
        var end = start + StoredAcidCooldown;
        foreach (var action in _actions.GetActions(xeno))
        {
            if (!_actionSharedCooldownQuery.TryComp(action, out var shared) ||
                !MatchesVenatorSharedCooldown(shared))
            {
                continue;
            }

            _actions.SetCooldown(action.AsNullable(), start, end);
        }
    }

    private static bool MatchesVenatorSharedCooldown(ActionSharedCooldownComponent shared)
    {
        return shared.Id == VenatorSpitCooldownId ||
               shared.Ids.Contains(VenatorSpitCooldownId) ||
               shared.ActiveIds.Contains(VenatorSpitCooldownId);
    }

    private void OnProjectileHit(Entity<XenoVenatorPoolOnHitComponent> projectile, ref ProjectileHitEvent args)
    {
        if (_net.IsClient)
            return;

        var directTargetHadAcid = projectile.Comp.UpgradeDirectHit && HasComp<UserAcidedComponent>(args.Target);
        var coords = Transform(args.Target).Coordinates;
        var centerPool = TrySpawnPools(projectile, coords, args.Target);
        if (centerPool != null)
            TryUpgradeDirectHit(projectile, centerPool.Value, args.Target, directTargetHadAcid);
    }

    private void OnProjectileFixedDistanceStop(Entity<XenoVenatorPoolOnHitComponent> projectile, ref ProjectileFixedDistanceStopEvent args)
    {
        if (_net.IsClient)
            return;

        TrySpawnPools(projectile, Transform(projectile).Coordinates);
        QueueDel(projectile);
    }

    private EntityUid? TrySpawnPools(Entity<XenoVenatorPoolOnHitComponent> projectile, EntityCoordinates coords, EntityUid? directTarget = null)
    {
        if (projectile.Comp.Pooled)
            return null;

        projectile.Comp.Pooled = true;
        Dirty(projectile);

        var centerPool = SpawnAttachedTo(projectile.Comp.Pool, coords);
        _hive.SetSameHive(projectile.Owner, centerPool);
        if (directTarget != null)
            TryIgnoreDirectTarget(projectile, centerPool, directTarget.Value);

        SpawnPoolPattern(projectile, coords, directTarget);
        return centerPool;
    }

    private void SpawnPoolPattern(Entity<XenoVenatorPoolOnHitComponent> projectile, EntityCoordinates coords, EntityUid? directTarget)
    {
        if (projectile.Comp.Rings <= 0)
            return;

        if (projectile.Comp.RandomCrossPattern && projectile.Comp.Rings == 1)
        {
            var pattern = _random.Prob(0.5f) ? XPattern : PlusPattern;
            foreach (var offset in pattern)
            {
                SpawnPool(projectile, coords.Offset(offset), directTarget);
            }

            return;
        }

        for (var x = -projectile.Comp.Rings; x <= projectile.Comp.Rings; x++)
        {
            for (var y = -projectile.Comp.Rings; y <= projectile.Comp.Rings; y++)
            {
                if (x == 0 && y == 0 || Math.Max(Math.Abs(x), Math.Abs(y)) > projectile.Comp.Rings)
                    continue;

                SpawnPool(projectile, coords.Offset(new Vector2(x, y)), directTarget);
            }
        }
    }

    private EntityUid SpawnPool(Entity<XenoVenatorPoolOnHitComponent> projectile, EntityCoordinates coords, EntityUid? directTarget)
    {
        var pool = SpawnAtPosition(projectile.Comp.Pool, coords);
        _hive.SetSameHive(projectile.Owner, pool);
        if (directTarget != null)
            TryIgnoreDirectTarget(projectile, pool, directTarget.Value);

        return pool;
    }

    private void TryIgnoreDirectTarget(Entity<XenoVenatorPoolOnHitComponent> projectile, EntityUid pool, EntityUid target)
    {
        if (!projectile.Comp.IgnoreDirectTarget ||
            !_damageOnCollideQuery.TryComp(pool, out var damage))
        {
            return;
        }

        damage.Damaged.Add(target);
        Dirty(pool, damage);
    }

    private void TryUpgradeDirectHit(Entity<XenoVenatorPoolOnHitComponent> projectile, EntityUid pool, EntityUid target, bool directTargetHadAcid)
    {
        if (!projectile.Comp.UpgradeDirectHit ||
            !directTargetHadAcid ||
            !_damageOnCollideQuery.TryComp(pool, out var damage))
        {
            return;
        }

        _xenoSpit.SetAcidCombo(target, damage.AcidComboDuration, damage.AcidComboDamage, damage.AcidComboParalyze, damage.AcidComboResists);
    }
}
