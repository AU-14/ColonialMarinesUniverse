using Content.Shared._RMC14.Emplacements;
using Content.Shared.CMU14.Dropship.AttachmentPoint;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Foldable;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Dropship.AttachmentPoint;

/// <summary>
/// Feeds magazines from installed <see cref="GunshipAmmoRackComponent"/>s into empty, deployed hardpoint weapons
/// on the same grid.
/// </summary>
public sealed partial class GunshipHardpointAutoloadSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private MountableWeaponSystem _mountable = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private GunshipAmmoRackSystem _rack = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const string MagazineSlot = "gun_magazine";

    // Catches weapons that went dry without firing, such as a freshly deployed empty launcher.
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(1);
    private TimeSpan _nextScan;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunshipHardpointAutoloadComponent, GunShotEvent>(OnGunShot);
    }

    private void OnGunShot(Entity<GunshipHardpointAutoloadComponent> ent, ref GunShotEvent args)
    {
        if (ent.Comp.ReloadAt == null && _gun.GetAmmoCount(ent) <= 0)
            ent.Comp.ReloadAt = _timing.CurTime + ent.Comp.ReloadDelay;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var time = _timing.CurTime;
        var scan = time >= _nextScan;
        if (scan)
            _nextScan = time + ScanInterval;

        var query = EntityQueryEnumerator<GunshipHardpointAutoloadComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.ReloadAt is { } reloadAt)
            {
                if (time < reloadAt)
                    continue;

                comp.ReloadAt = null;
                TryAutoload(uid);
                continue;
            }

            if (scan && IsDryDeployedWeapon(uid, out _))
                comp.ReloadAt = time + comp.ReloadDelay;
        }
    }

    /// <summary>
    /// Reloads a deployed, empty hardpoint weapon from an ammunition rack installed on the same grid.
    /// </summary>
    /// <returns>True if the weapon was reloaded.</returns>
    public bool TryAutoload(EntityUid gun)
    {
        if (!IsDryDeployedWeapon(gun, out var mount)
            || Transform(mount).GridUid is not { } grid)
        {
            return false;
        }

        var racks = EntityQueryEnumerator<GunshipAmmoRackComponent, StorageComponent>();
        while (racks.MoveNext(out var rack, out _, out var storage))
        {
            if (!_rack.TryGetInstalledPoint(rack, out var point) || Transform(point).GridUid != grid)
                continue;

            foreach (var ammo in storage.Container.ContainedEntities)
            {
                // Loading moves the ammo out of this container, so stop enumerating it right away.
                if (!TryLoad(gun, ammo))
                    continue;

                var ammoLayer = TryComp(mount, out FoldableComponent? foldable) && foldable.IsFolded
                    ? WeaponMountComponentVisualLayers.FoldedAmmo
                    : WeaponMountComponentVisualLayers.MountedAmmo;
                _appearance.SetData(mount, ammoLayer, true);
                _popup.PopupEntity(Loc.GetString("cmu-gunship-autoload-reloaded", ("weapon", mount)), mount, PopupType.Small);
                return true;
            }
        }

        return false;
    }

    private bool IsDryDeployedWeapon(EntityUid gun, out EntityUid mount)
    {
        mount = default;
        if (!_mountable.TryGetWeaponMount(gun, out var mounted)
            || _container.IsEntityInContainer(mounted.Value)
            || _gun.GetAmmoCount(gun) > 0)
        {
            return false;
        }

        mount = mounted.Value;
        return true;
    }

    private bool TryLoad(EntityUid gun, EntityUid ammo)
    {
        if (TryComp(gun, out ItemSlotsComponent? slots) && _itemSlots.TryGetSlot((gun, slots), MagazineSlot, out var slot))
        {
            if (_gun.GetAmmoCount(ammo) <= 0 || !_itemSlots.CanInsert(gun, slot, ammo, null, swap: true))
                return false;

            // Weapons that keep their spent magazine drop it at the mount.
            if (slot.HasItem && !_itemSlots.TryEject(gun, slot, null, out _))
                return false;

            return _itemSlots.TryInsert(gun, slot, ammo, null);
        }

        return TryComp(gun, out BallisticAmmoProviderComponent? ballistic)
            && _gun.CanInsertBallistic((gun, ballistic), ammo)
            && _gun.TryBallisticInsert((gun, ballistic), ammo, null);
    }
}
