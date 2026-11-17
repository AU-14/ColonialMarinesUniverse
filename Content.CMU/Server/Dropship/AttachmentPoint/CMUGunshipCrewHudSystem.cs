using System.Collections.Generic;
using System.Linq;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Emplacements;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Dropship.AttachmentPoint;
using Content.Shared.CMU14.Dropship.Integrity;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Dropship.AttachmentPoint;

public sealed partial class CMUGunshipCrewHudSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedWeaponMountSystem _mounts = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private TimeSpan _nextUpdate;
    private readonly HashSet<EntityUid> _wearers = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUGunshipCrewVisorComponent, ComponentShutdown>(OnVisorShutdown);
        SubscribeLocalEvent<CMUGunshipCrewVisorComponent, GotUnequippedEvent>(OnUnequipped);
    }

    private void OnVisorShutdown(Entity<CMUGunshipCrewVisorComponent> ent, ref ComponentShutdown args)
    {
        var wearer = Transform(ent).ParentUid;
        if (_inventory.TryGetSlotEntity(wearer, "head", out var helmet) && helmet == ent.Owner)
            RemCompDeferred<CMUGunshipCrewHudComponent>(wearer);
    }

    private void OnUnequipped(Entity<CMUGunshipCrewVisorComponent> ent, ref GotUnequippedEvent args)
    {
        if (args.Slot == "head")
            RemCompDeferred<CMUGunshipCrewHudComponent>(args.EquipTarget);
    }

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + TimeSpan.FromMilliseconds(100);
        _wearers.Clear();
        var visors = EntityQueryEnumerator<CMUGunshipCrewVisorComponent, TransformComponent>();
        while (visors.MoveNext(out var helmet, out _, out var transform))
        {
            var wearer = transform.ParentUid;
            if (!_inventory.TryGetSlotEntity(wearer, "head", out var worn) || worn != helmet)
                continue;

            _wearers.Add(wearer);
            UpdateHud(wearer);
        }

        var huds = EntityQueryEnumerator<CMUGunshipCrewHudComponent>();
        while (huds.MoveNext(out var wearer, out _))
        {
            if (!_wearers.Contains(wearer))
                RemCompDeferred<CMUGunshipCrewHudComponent>(wearer);
        }
    }

    private bool TryGetShip(EntityUid gun, out EntityUid ship)
    {
        ship = default;
        if (!HasComp<CMUDoorGunVisualComponent>(gun)
            || !TryComp(gun, out ActiveGunshipDoorGunnerComponent? active)
            || active.AttachmentPoint is not { } point
            || !HasComp<GunshipHardpointAttachmentPointComponent>(point)
            || Transform(point).GridUid is not { } grid
            || Transform(gun).GridUid != grid || !HasComp<DropshipComponent>(grid))
            return false;

        ship = grid;
        return true;
    }

    private void UpdateHud(EntityUid wearer)
    {
        EntityUid? ship = null;
        EntityUid? currentGun = null;
        var gunType = string.Empty;
        var ammo = 0;
        var capacity = 0;
        if (TryComp(wearer, out BuckleComponent? buckle) && buckle.BuckledTo is { } mountedGun
            && TryComp(mountedGun, out WeaponMountComponent? currentMount) && currentMount.User == wearer
            && TryGetShip(mountedGun, out var grid))
        {
            ship = grid;
            currentGun = mountedGun;
            gunType = Name(mountedGun);
            ReadAmmo(mountedGun, out ammo, out capacity);
        }

        var others = new List<CMUGunshipCrewGunStatus>();
        if (ship is { } linked)
        {
            var guns = EntityQueryEnumerator<ActiveGunshipDoorGunnerComponent, WeaponMountComponent>();
            while (guns.MoveNext(out var gun, out _, out var mount))
            {
                if (gun == currentGun || mount.MountedEntity == null
                    || !TryGetShip(gun, out var otherShip) || otherShip != linked)
                    continue;

                ReadAmmo(gun, out var rounds, out var maximum);
                var facing = Angle.ShortestDistance(
                    _transform.GetWorldRotation(linked), _transform.GetWorldRotation(gun));
                others.Add(new CMUGunshipCrewGunStatus(facing.GetCardinalDir(), Name(gun), rounds, maximum));
            }
            others.Sort((a, b) => a.Side != b.Side
                ? a.Side.CompareTo(b.Side)
                : string.Compare(a.GunType, b.GunType, StringComparison.CurrentCulture));
        }

        var integrity = ship is { } uid ? CompOrNull<DropshipIntegrityComponent>(uid) : null;
        var malfunctions = integrity?.ActiveMalfunctions.ToList() ?? new List<DropshipMalfunction>();
        var alarms = new List<DropshipAlarm>();
        if (integrity?.ProximityAlarmActive == true)
            alarms.Add(DropshipAlarm.Proximity);
        if (integrity?.LowIntegrityAlarmActive == true)
            alarms.Add(DropshipAlarm.LowIntegrity);

        var hud = EnsureComp<CMUGunshipCrewHudComponent>(wearer);
        if (hud.Dropship == ship && hud.GunType == gunType && hud.Ammo == ammo && hud.Capacity == capacity
            && hud.OtherGuns.SequenceEqual(others) && hud.Integrity == (integrity?.Integrity ?? 0)
            && hud.MaxIntegrity == (integrity?.MaxIntegrity ?? 0) && hud.Malfunctions.SequenceEqual(malfunctions)
            && hud.Alarms.SequenceEqual(alarms) && hud.MasterAlarmSilenced == (integrity?.MasterAlarmSilenced ?? false))
            return;

        hud.Dropship = ship;
        hud.GunType = gunType;
        hud.Ammo = ammo;
        hud.Capacity = capacity;
        hud.OtherGuns = others;
        hud.Integrity = integrity?.Integrity ?? 0;
        hud.MaxIntegrity = integrity?.MaxIntegrity ?? 0;
        hud.Malfunctions = malfunctions;
        hud.Alarms = alarms;
        hud.MasterAlarmSilenced = integrity?.MasterAlarmSilenced ?? false;
        Dirty(wearer, hud);
    }

    private void ReadAmmo(EntityUid gun, out int ammo, out int capacity)
    {
        _mounts.TryGetWeaponAmmo(gun, out var count, out var maximum);
        ammo = Math.Max(0, count ?? 0);
        capacity = Math.Max(0, maximum ?? 0);
    }
}
