using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared.CMU14.Fighter;
using Content.Shared.Light.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Fighter;

public sealed partial class FighterSystem
{
    [Dependency] private SharedDropshipWeaponSystem _payloads = default!;
    [Dependency] private FighterHardpointSystem _hardpoints = default!;
    [Dependency] private AreaSystem _areas = default!;
    [Dependency] private SharedContainerSystem _containers = default!;

    private void InitializeWeapons()
    {
        SubscribeNetworkEvent<FighterSelectWeaponEvent>(OnSelectWeapon);
        SubscribeNetworkEvent<FighterSelectTargetEvent>(OnSelectTarget);
        SubscribeLocalEvent<FighterLaserComponent, ComponentShutdown>(OnLaserShutdown);
    }

    private void CreateHardpoints(Entity<FighterAircraftComponent> aircraft)
    {
        var weapons = AddComp<FighterWeaponsComponent>(aircraft);
        for (var i = 0; i <= FighterWeaponsComponent.GauSlot; i++)
        {
            var cannon = i == FighterWeaponsComponent.GauSlot;
            var x = cannon ? .5f : .5f + (i < 3 ? -1 : 1) * (2.5f + i % 3 * 1.8f);
            var point = SpawnAttachedTo(cannon ? "CMUFighterGauFeed" : "CMUFighterHardpoint",
                new EntityCoordinates(aircraft, new Vector2(x, cannon ? 5.5f : -1.5f - i % 3 * .7f)));
            var component = Comp<FighterHardpointComponent>(point);
            component.Aircraft = aircraft;
            component.Index = i;
            component.Internal = cannon;
            weapons.Hardpoints.Add(point);
            Dirty(point, component);
        }
        RefreshWeapons(aircraft, weapons);
    }

    private void LoadStartingAmmo(FighterGroundComponent ground, Entity<FighterAircraftComponent> aircraft)
    {
        if (ground.StartingMissiles.Count == 0 && ground.StartingGauAmmo == null)
            return;

        var weapons = Comp<FighterWeaponsComponent>(aircraft);
        for (var i = 0; i < Math.Min(ground.StartingMissiles.Count, FighterWeaponsComponent.ExternalPoints); i++)
            MountStartingAmmo(weapons.Hardpoints[i], ground.StartingMissiles[i]);
        if (ground.StartingGauAmmo is { } gauAmmo)
            MountStartingAmmo(weapons.Hardpoints[FighterWeaponsComponent.GauSlot], gauAmmo);
        RefreshWeapons(aircraft, weapons);
    }

    private void MountStartingAmmo(EntityUid pointUid, EntProtoId prototype)
    {
        var point = new Entity<FighterHardpointComponent>(pointUid, Comp<FighterHardpointComponent>(pointUid));
        var ammo = Spawn(prototype, Transform(point).Coordinates);
        if (_hardpoints.TryMount(point, ammo))
            return;

        Log.Error($"Could not mount starting ammunition {prototype} on {ToPrettyString(pointUid)}.");
        QueueDel(ammo);
    }

    private FighterWeaponStatus GetWeaponStatus(Entity<FighterHardpointComponent> point)
    {
        var kind = _hardpoints.Kind(point);
        var ammo = point.Comp.Ammo is { } uid && !TerminatingOrDeleted(uid) ? CompOrNull<DropshipAmmoComponent>(uid) : null;
        var name = kind switch
        {
            FighterWeaponKind.Gau => Loc.GetString("cmu-fighter-internal-gau"),
            FighterWeaponKind.Rockets => Loc.GetString("cmu-fighter-rocket-pod"),
            FighterWeaponKind.Missile when point.Comp.Ammo is { } missile => Name(missile),
            _ => Loc.GetString("cmu-fighter-empty-pylon"),
        };
        return new(point.Comp.Index, kind, name, ammo?.Rounds ?? 0, ammo?.RoundsPerShot ?? 1, point.Comp.ReadyAt);
    }

    private bool TryGetFlare(EntityUid uid, FighterAircraftComponent aircraft, FighterWeaponsComponent weapons, out FighterTarget flare)
    {
        flare = default!;
        if (TerminatingOrDeleted(uid) || !HasComp<FlareSignalComponent>(uid) ||
            !TryComp(uid, out DropshipTargetComponent? target) || !target.IsTargetableByWeapons ||
            !TryComp(uid, out ExpendableLightComponent? light) || !light.Activated ||
            _containers.IsEntityInContainer(uid) || Transform(uid).MapUid != aircraft.TerrainMap ||
            !FighterIFFSystem.Same(weapons.Faction, target.CreatorFaction)) return false;
        var position = _transform.GetWorldPosition(uid);
        if (!aircraft.Battlefield.Contains(position)) return false;
        flare = new(GetNetEntity(uid), target.Abbreviation, position, _areas.CanCAS(Transform(uid).Coordinates));
        return true;
    }

    private void RefreshWeapons(Entity<FighterAircraftComponent> aircraft, FighterWeaponsComponent weapons)
    {
        weapons.NextRefresh = _timing.CurTime + TimeSpan.FromSeconds(.5);
        weapons.Loadout.Clear();
        foreach (var uid in weapons.Hardpoints)
            if (!TerminatingOrDeleted(uid) && TryComp(uid, out FighterHardpointComponent? point))
                weapons.Loadout.Add(GetWeaponStatus((uid, point)));
        weapons.Targets.Clear();
        var flares = EntityQueryEnumerator<FlareSignalComponent, DropshipTargetComponent>();
        while (flares.MoveNext(out var uid, out _, out _))
            if (TryGetFlare(uid, aircraft.Comp, weapons, out var flare)) weapons.Targets.Add(flare);
        foreach (var seatUid in new[] { aircraft.Comp.FrontSeat, aircraft.Comp.RearSeat })
            if (seatUid is { } seat && TryComp(seat, out FighterSeatComponent? operatorSeat) &&
                operatorSeat.Laser is { } laser && TryGetTarget(laser, aircraft, weapons, out var designation))
                weapons.Targets.Add(designation);
        weapons.Targets.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
        foreach (var seatUid in new[] { aircraft.Comp.FrontSeat, aircraft.Comp.RearSeat })
        {
            if (seatUid is not { } uid || !TryComp(uid, out FighterSeatComponent? seat) || seat.Target is not { } target) continue;
            var current = weapons.Targets.FirstOrDefault(flare => flare.Id == target);
            if (current == null)
            {
                seat.Target = null;
                seat.TargetPosition = null;
                CancelLaserLock((uid, seat));
            }
            else
            {
                var offset = seat.SensorLock.GetValueOrDefault(current.Position) - seat.TargetPosition.GetValueOrDefault(current.Position);
                seat.TargetPosition = current.Position;
                seat.SensorLock = current.Position + offset;
            }
            Dirty(uid, seat);
        }
        Dirty(aircraft.Owner, weapons);
    }

    private void OnSelectWeapon(FighterSelectWeaponEvent ev, EntitySessionEventArgs args) => TrySelectWeapon(args.SenderSession.AttachedEntity, ev.Slot);

    public bool TrySelectWeapon(EntityUid? user, int slot)
    {
        if (!TryGetSeat(user, out var seat, out var aircraft) || !TryComp(aircraft, out FighterWeaponsComponent? weapons) ||
            slot < 0 || slot >= weapons.Hardpoints.Count) return false;
        if (seat.Comp.WeaponSlot != slot)
        {
            CancelLaserLock(seat);
            CancelQueuedFire(seat);
        }
        seat.Comp.WeaponSlot = slot;
        Dirty(seat);
        return true;
    }

    private void OnSelectTarget(FighterSelectTargetEvent ev, EntitySessionEventArgs args) => TryLockTarget(args.SenderSession.AttachedEntity, ev.Target);

    public bool TryLockTarget(EntityUid? user, NetEntity target)
    {
        if (!TryGetSeat(user, out var seat, out var aircraft) || !TryComp(aircraft, out FighterWeaponsComponent? weapons) ||
            !TryGetEntity(target, out var uid) || uid == null || !TryGetTarget(uid.Value, aircraft, weapons, out var flare)) return false;
        // A radio designation can be selected out of range or through cloud; the feed
        // and release remain gated. This lets the pilot plan a pass toward it.
        if (seat.Comp.Target != target)
        {
            CancelLaserLock(seat);
            CancelQueuedFire(seat);
        }
        seat.Comp.Target = target;
        seat.Comp.TargetPosition = flare.Position;
        seat.Comp.SensorLock = flare.Position;
        seat.Comp.Aim = Vector2.Zero;
        UpdateCamera(seat, aircraft.Comp);
        Dirty(seat);
        return true;
    }

    public bool TryFire(EntityUid? user, out FighterFireStatus status)
    {
        status = FighterFireStatus.NoWeapon;
        if (!TryGetSeat(user, out var seat, out var aircraft) || !TryComp(aircraft, out FighterWeaponsComponent? weapons) ||
            seat.Comp.WeaponSlot < 0 || seat.Comp.WeaponSlot >= weapons.Hardpoints.Count ||
            !TryComp(weapons.Hardpoints[seat.Comp.WeaponSlot], out FighterHardpointComponent? point)) return false;
        var mount = new Entity<FighterHardpointComponent>(weapons.Hardpoints[seat.Comp.WeaponSlot], point);
        FighterTarget? flare = null;
        EntityUid? target = null;
        if (seat.Comp.Target is { } id && TryGetEntity(id, out target) && target != null &&
            TryGetTarget(target.Value, aircraft, weapons, out var live)) flare = live;
        var weapon = GetWeaponStatus(mount);
        status = FighterWeapons.Status(aircraft.Comp, weapons, seat.Comp, weapon, flare, _timing.CurTime);
        if (status != FighterFireStatus.Ready || target == null || point.Ammo is not { } ammo || !TryComp(ammo, out DropshipAmmoComponent? payload)) return false;
        if (flare is { Laser: true })
        {
            if (seat.Comp.LaserLockTarget == null)
            {
                seat.Comp.LaserLockTarget = flare.Id;
                seat.Comp.LaserLockSlot = weapon.Slot;
                seat.Comp.LaserLockAmmo = ammo;
                seat.Comp.LaserLockReadyAt = _timing.CurTime + weapons.LaserLockDuration;
                PlayEffect(aircraft, FighterEffectKind.LaserLock, seat.Comp.Pilot ? 0 : 1, duration: 2);
                Dirty(seat);
                status = FighterFireStatus.Locking;
                return true;
            }
            if (seat.Comp.LaserLockTarget != flare.Id || seat.Comp.LaserLockSlot != weapon.Slot || seat.Comp.LaserLockAmmo != ammo)
            {
                CancelLaserLock(seat);
                return false;
            }
        }
        TimeSpan? strikeTravelTime = weapon.Kind switch
        {
            FighterWeaponKind.Gau => weapons.GauTravelTime,
            FighterWeaponKind.Rockets => weapons.RocketTravelTime,
            _ => null,
        };
        if (!_payloads.TryFireFighterAmmo((ammo, payload), mount, Transform(target.Value).Coordinates, user!.Value,
                strikeTravelTime, weapon.Kind, aircraft.Comp.Position)) return false;
        var effect = weapon.Kind switch
        {
            FighterWeaponKind.Gau => FighterEffectKind.Gau,
            FighterWeaponKind.Rockets => FighterEffectKind.Rocket,
            _ => FighterEffectKind.Missile,
        };
        PlayEffect(aircraft, effect, weapon.Slot, duration: weapon.Kind == FighterWeaponKind.Gau
            ? Math.Min(6, .1f * payload.RoundsPerShot / Math.Max(1, payload.ShotsPerVolley)) : 2);
        if (flare is { Laser: true }) WarnLaserStrike(target.Value);
        CancelLaserLock(seat);
        point.ReadyAt = _timing.CurTime + (weapon.Kind switch
        {
            FighterWeaponKind.Gau => weapons.GauDelay,
            FighterWeaponKind.Rockets => weapons.RocketDelay,
            _ => weapons.MissileDelay,
        });
        Dirty(mount);
        RefreshWeapons(aircraft, weapons);
        return true;
    }
}
