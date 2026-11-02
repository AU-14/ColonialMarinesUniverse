using Content.Shared._RMC14.PowerLoader;
using Content.Shared._RMC14.Sentry;
using Content.Shared.CMU14.Fob;
using Content.Shared.CMU14.Sentry;
using Content.Shared.Item;
using Content.Shared.Interaction;
using Content.Shared.Examine;
using Content.Shared._RMC14.Construction;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.GameObjects;
using Content.Shared.Physics;

namespace Content.Server.CMU14.Fob;

public sealed partial class CMUFobDefenseSystem
{
    [Dependency] private RMCConstructionSystem _construction = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private CMUFobMapSystem _sites = default!;

    private void InitializeModules()
    {
        SubscribeLocalEvent<CMUFobSupportGunComponent, PowerLoaderInteractEvent>(OnModuleInstall);
        SubscribeLocalEvent<CMUFobSupportGunComponent, GettingPickedUpAttemptEvent>(OnModulePickup);
        SubscribeLocalEvent<CMUFobGunMountComponent, InteractUsingEvent>(OnPlatformWrench,
            before: [typeof(SentrySystem), typeof(CMUSentryRepairSystem)]);
    }

    private void OnPlatformWrench(Entity<CMUFobGunMountComponent> mount, ref InteractUsingEvent args)
    {
        if (args.Handled || !_reserve.Enabled || !_tools.HasQuality(args.Used, Anchoring)) return;
        args.Handled = true;
        if (!Transform(mount).Anchored)
        {
            if (!TryDeployPlatform(mount, args.User))
                _popup.PopupEntity(Loc.GetString("cmu-fob-platform-site-required"), mount, args.User);
            return;
        }
        if (mount.Comp.Network is not { } uid || !TryComp<CMUFobNetworkComponent>(uid, out var bank)
            || bank.State is CMUFobReserveState.Active or CMUFobReserveState.Starting or CMUFobReserveState.Closed
            || mount.Comp.Gun != null || !_controls.CanOperate(mount, args.User, bank.Faction)
            || Transform(mount).MapUid != bank.Map
            || System.Numerics.Vector2.DistanceSquared(_transform.GetWorldPosition(mount), mount.Comp.Position) > 0.01f) return;
        _transform.Unanchor(mount);
        _appearance.SetData(mount, CMUFobVisuals.Stowed, !Transform(mount).Anchored);
        _effects.Play(mount, CMUFobPresentationCue.Pack);
    }

    /// <summary>Deploy or relocate a physical platform within the selected LZ, preserving its two-slot allocation.</summary>
    public bool TryDeployPlatform(Entity<CMUFobGunMountComponent> platform, EntityUid user)
    {
        if (!_reserve.Enabled || Transform(platform).Anchored || _containers.IsEntityInContainer(platform)
            || platform.Comp.Gun != null || !_reserve.TryGetNetwork("govfor", out var bank)
            || bank.Comp.State is CMUFobReserveState.Active or CMUFobReserveState.Starting or CMUFobReserveState.Closed
            || platform.Comp.Network != null && platform.Comp.Network != bank.Owner
            || Transform(platform).MapUid != bank.Comp.Map || Transform(platform).GridUid is not { } grid
            || !TryComp<MapGridComponent>(grid, out var mapGrid) || !_controls.CanOperate(platform, user, bank.Comp.Faction)) return false;
        var tile = _maps.GetTileRef(grid, mapGrid, Transform(platform).Coordinates);
        var position = _maps.GridTileToWorld(grid, mapGrid, tile.GridIndices).Position;
        if (tile.Tile.IsEmpty || System.Numerics.Vector2.DistanceSquared(position, _transform.GetWorldPosition(bank))
                > bank.Comp.DeploymentRange * bank.Comp.DeploymentRange || !_sites.CanPlaceUtility(bank, position)
            || !_construction.CanBuildAt(Transform(platform).Coordinates, Name(platform), out _, anchoring: true,
                collision: CollisionGroup.Impassable | CollisionGroup.MidImpassable)) return false;
        var defense = EnsureComp<CMUFobDefenseNetworkComponent>(bank);
        var registered = defense.Mounts.Contains(platform);
        if (!registered && (platform.Comp.Network != null || defense.Mounts.Count >= 2)) return false;
        foreach (var existing in defense.Mounts)
            if (existing != platform.Owner && Exists(existing)
                && System.Numerics.Vector2.DistanceSquared(_transform.GetWorldPosition(existing), position) < 1) return false;
        if (!_transform.AnchorEntity(platform)) return false;
        if (!registered) defense.Mounts.Add(platform);
        platform.Comp.Network = bank;
        platform.Comp.Position = position;
        platform.Comp.Facing = _transform.GetWorldRotation(platform);
        platform.Comp.MagazineIssued = true;
        platform.Comp.RemovedByOperator = true;
        _appearance.SetData(platform, CMUFobVisuals.Stowed, false);
        EnsureComp<CMUFobInstructionsComponent>(platform);
        _effects.Play(platform, CMUFobPresentationCue.Deploy);
        return true;
    }

    private void OnModulePickup(Entity<CMUFobSupportGunComponent> ent, ref GettingPickedUpAttemptEvent args)
    {
        if (ent.Comp.Mount != null) args.Cancel();
    }

    public bool TryRemoveModule(Entity<CMUFobSupportGunComponent> gun, EntityUid user)
    {
        if (!CanService(gun, user) || !TryGetMount(gun, out var mount, out var bank)
            || bank.Comp.State is CMUFobReserveState.Active or CMUFobReserveState.Starting
            || _doAfter.IsRunning(gun.Comp.Reload)) return false;
        ClearTarget(gun);
        SetMode(gun, SentryMode.Off);
        gun.Comp.RequiresRestart = true;
        gun.Comp.Mount = null;
        mount.Comp.Gun = null;
        mount.Comp.RemovedByOperator = true;
        _transform.Unanchor(gun);
        SetCollision(gun, false);
        _appearance.SetData(gun, CMUFobGunVisuals.Installed, false);
        UpdatePresentation(gun);
        _effects.Play(gun, CMUFobPresentationCue.Pack);
        return true;
    }

    private void OnModuleInstall(Entity<CMUFobSupportGunComponent> gun, ref PowerLoaderInteractEvent args)
    {
        if (args.Handled || !_reserve.Enabled || !HasComp<CMUFobGunMountComponent>(args.Target)) return;
        args.Handled = true;
        foreach (var user in args.Buckled)
        {
            if (TryInstallModule(gun, args.Target, user, args.PowerLoader)) break;
        }
    }

    public bool TryInstallModule(Entity<CMUFobSupportGunComponent> gun, EntityUid mountId, EntityUid user, EntityUid loader)
    {
        if (!_reserve.Enabled || gun.Comp.Mount != null || !TryComp<CMUFobGunMountComponent>(mountId, out var comp)
            || comp.Gun != null || !ValidMount((mountId, comp), out var bank)
            || bank.Comp.State is CMUFobReserveState.Active or CMUFobReserveState.Starting
            || !_controls.CanOperate(mountId, user, bank.Comp.Faction)
            || !TryComp<PowerLoaderComponent>(loader, out _) || !_hands.IsHolding(loader, gun)
            || !_interaction.InRangeUnobstructed(loader, mountId, 2.5f)) return false;
        if (!_hands.TryDrop(loader, gun)) return false;
        _transform.SetCoordinates(gun, Transform(mountId).Coordinates);
        if (!_transform.AnchorEntity(gun)) return false;
        comp.Gun = gun;
        comp.RemovedByOperator = false;
        comp.WeaponPrototype = gun.Comp.RequiresSensor ? "CMUFobRocketLauncher" : "CMUFobSupportGun";
        gun.Comp.Mount = mountId;
        gun.Comp.RequiresRestart = true;
        _transform.SetWorldRotation(gun, comp.Facing);
        _targeting.TryApplyDefaultFaction(gun, bank.Comp.Faction);
        _appearance.SetData(gun, CMUFobGunVisuals.Installed, true);
        EnsureComp<CMUFobInstructionsComponent>(gun);
        SetCollision(gun, true);
        UpdatePresentation(gun);
        _effects.Play(gun, CMUFobPresentationCue.Deploy);
        return true;
    }
}
