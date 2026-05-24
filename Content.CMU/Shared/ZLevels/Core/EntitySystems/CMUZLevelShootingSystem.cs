using System.Numerics;
using System;
using Content.Shared._CMU14.Blackfoot;
using Content.Shared._CMU14.Input;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Actions;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._CMU14.ZLevels.Core.EntitySystems;

public sealed partial class CMUZLevelShootingSystem : EntitySystem
{
    private const float CrossZShotRange = 4f;
    private const float CrossZRenderOffset = 0.7f;
    private const string BlackfootDoorGunHardpointType = "DoorGun";

    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunComponent, ItemUnwieldedEvent>(OnGunUnwielded);
        SubscribeLocalEvent<CMUZLevelViewerComponent, CMUZLevelLookUpEnabledEvent>(OnLookUpEnabled);

        CommandBinds.Builder
            .Bind(CMUKeyFunctions.CMUToggleShootDownZLevel,
                InputCmdHandler.FromDelegate(session =>
                    {
                        if (session?.AttachedEntity is { } user)
                            ToggleShootDown(user);
                    },
                    handle: false))
            .Register<CMUZLevelShootingSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<CMUZLevelShootingSystem>();
    }

    private void OnLookUpEnabled(Entity<CMUZLevelViewerComponent> ent, ref CMUZLevelLookUpEnabledEvent args)
    {
        TryDisableShootDown(ent);
    }

    private void OnGunUnwielded(Entity<GunComponent> ent, ref ItemUnwieldedEvent args)
    {
        if (TryDisableShootDown(args.User) &&
            !args.Force)
        {
            PopupSelf(args.User, "cmu-zlevel-shoot-down-disabled-unwield");
        }

    }

    private void ToggleShootDown(EntityUid user)
    {
        if (TryToggleBlackfootDoorGunShootDown(user))
            return;

        if (!TryGetReadyGun(user, "cmu-zlevel-shoot-down-no-gun", "cmu-zlevel-shoot-down-requires-wield"))
            return;

        var shootDown = !IsShootDownEnabled(user);
        SetShootDown(user, shootDown);

        var message = shootDown
            ? "cmu-zlevel-shoot-down-enabled"
            : "cmu-zlevel-shoot-down-disabled";

        PopupSelf(user, message);
    }

    private bool TryToggleBlackfootDoorGunShootDown(EntityUid user)
    {
        if (!TryComp(user, out BlackfootDoorGunActionComponent? actions) ||
            actions.Vehicle is not { } vehicle ||
            !HasComp<BlackfootFlightComponent>(vehicle))
        {
            return false;
        }

        if (!IsUsingBlackfootDoorGun(user, vehicle))
        {
            PopupSelfPlain(user, "Select the M866 automatic grenade launcher first.", PopupType.SmallCaution);
            UpdateBlackfootDoorGunAction(user, actions, false);
            return true;
        }

        var shootDown = !IsShootDownEnabled(user);
        SetShootDown(user, shootDown);
        UpdateBlackfootDoorGunAction(user, actions, true);

        var message = shootDown
            ? "M866 set to fire one Z-level below."
            : "M866 set to fire on the current Z-level.";

        PopupSelfPlain(user, message);
        return true;
    }

    private bool IsUsingBlackfootDoorGun(EntityUid user, EntityUid vehicle)
    {
        if (!TryComp(user, out VehicleWeaponsOperatorComponent? weaponsOperator) ||
            weaponsOperator.Vehicle != vehicle ||
            weaponsOperator.SelectedWeapon is not { } selected ||
            !TryComp(selected, out HardpointItemComponent? hardpoint))
        {
            return false;
        }

        return string.Equals(hardpoint.HardpointType, BlackfootDoorGunHardpointType, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateBlackfootDoorGunAction(EntityUid user, BlackfootDoorGunActionComponent actions, bool enabled)
    {
        if (actions.ZModeToggleAction is not { } action)
            return;

        _actions.SetEnabled(action, enabled);
        _actions.SetToggled(action, IsShootDownEnabled(user));
    }

    private bool TryGetReadyGun(EntityUid user, string noGunMessage, string requiresWieldMessage)
    {
        if (!TryGetGun(user, out var gunUid))
        {
            PopupSelf(user, noGunMessage);
            return false;
        }

        if (!IsReadyGun(gunUid))
        {
            PopupSelf(user, requiresWieldMessage);
            return false;
        }

        return true;
    }

    private bool HasReadyGun(EntityUid user)
    {
        return TryGetGun(user, out var gunUid) && IsReadyGun(gunUid);
    }

    private bool TryGetGun(EntityUid user, out EntityUid gunUid)
    {
        return _gun.TryGetGun(user, out gunUid, out _);
    }

    private bool IsReadyGun(EntityUid gunUid)
    {
        return !TryComp<WieldableComponent>(gunUid, out var wieldable) || wieldable.Wielded;
    }

    private bool TryDisableShootDown(EntityUid user)
    {
        if (!IsShootDownEnabled(user))
        {
            return false;
        }

        SetShootDown(user, false);
        return true;
    }

    public bool IsShootDownEnabled(EntityUid user)
    {
        return TryComp<CMUZLevelShooterComponent>(user, out var shooter) && shooter.ShootDown;
    }

    public void SetShootDown(EntityUid user, bool enabled)
    {
        CMUZLevelShooterComponent shooter;
        if (TryComp<CMUZLevelShooterComponent>(user, out var existing))
        {
            shooter = existing;
        }
        else
        {
            if (!enabled)
                return;

            shooter = EnsureComp<CMUZLevelShooterComponent>(user);
        }

        if (shooter.ShootDown == enabled)
            return;

        shooter.ShootDown = enabled;
        DirtyField(user, shooter, nameof(CMUZLevelShooterComponent.ShootDown));

        if (enabled)
            _zLevels.TryDisableLookUp(user);
    }

    public bool TryAdjustShotCoordinates(
        EntityUid shooter,
        EntityCoordinates fromCoordinates,
        EntityCoordinates toCoordinates,
        out EntityCoordinates adjustedFromCoordinates,
        out EntityCoordinates adjustedToCoordinates)
    {
        adjustedFromCoordinates = fromCoordinates;
        adjustedToCoordinates = toCoordinates;

        var offset = GetRequestedShotOffset(shooter, requireReadyGunForLookUp: true);
        if (offset == 0)
            return true;

        var shooterMap = Transform(shooter).MapUid;
        if (shooterMap == null ||
            !_zLevels.TryMapOffset(shooterMap.Value, offset, out var targetMap, out var map))
        {
            PopupSelf(shooter, offset > 0
                ? "cmu-zlevel-shoot-up-no-level"
                : "cmu-zlevel-shoot-down-no-level");
            return false;
        }

        var fromMap = _transform.ToMapCoordinates(fromCoordinates);
        var toMap = _transform.ToMapCoordinates(toCoordinates);
        var clampedTo = ClampCrossZShotTarget(fromMap.Position, toMap.Position);
        if (!_zLevels.TryFindZShotOpening(shooterMap.Value, targetMap.Value, offset, fromMap.Position, clampedTo, out _))
        {
            PopupSelf(shooter, offset > 0
                ? "cmu-zlevel-shoot-up-blocked-floor"
                : "cmu-zlevel-shoot-down-blocked-floor");
            return false;
        }

        var renderOffset = GetCrossZRenderOffset(offset);
        var targetFrom = new MapCoordinates(fromMap.Position - renderOffset, map.MapId);
        var targetTo = new MapCoordinates(clampedTo - renderOffset, map.MapId);

        adjustedFromCoordinates = _transform.ToCoordinates(targetFrom);
        adjustedToCoordinates = _transform.ToCoordinates(targetTo);
        return true;
    }

    public bool TryAdjustShotMapCoordinates(
        EntityUid shooter,
        MapCoordinates fromCoordinates,
        MapCoordinates toCoordinates,
        out MapCoordinates adjustedFromCoordinates,
        out MapCoordinates adjustedToCoordinates)
    {
        adjustedFromCoordinates = fromCoordinates;
        adjustedToCoordinates = toCoordinates;

        var offset = GetRequestedShotOffset(shooter);
        if (offset == 0)
            return true;

        var shooterMap = Transform(shooter).MapUid;
        if (shooterMap == null ||
            !_zLevels.TryMapOffset(shooterMap.Value, offset, out var targetMap, out var map))
        {
            PopupSelf(shooter, offset > 0
                ? "cmu-zlevel-shoot-up-no-level"
                : "cmu-zlevel-shoot-down-no-level");
            return false;
        }

        var clampedTo = ClampCrossZShotTarget(fromCoordinates.Position, toCoordinates.Position);
        if (!_zLevels.TryFindZShotOpening(shooterMap.Value, targetMap.Value, offset, fromCoordinates.Position, clampedTo, out _))
        {
            PopupSelf(shooter, offset > 0
                ? "cmu-zlevel-shoot-up-blocked-floor"
                : "cmu-zlevel-shoot-down-blocked-floor");
            return false;
        }

        var renderOffset = GetCrossZRenderOffset(offset);
        adjustedFromCoordinates = new MapCoordinates(fromCoordinates.Position - renderOffset, map.MapId);
        adjustedToCoordinates = new MapCoordinates(clampedTo - renderOffset, map.MapId);
        return true;
    }

    private static Vector2 GetCrossZRenderOffset(int offset)
    {
        return new Vector2(0f, CrossZRenderOffset * offset);
    }

    private static Vector2 ClampCrossZShotTarget(Vector2 from, Vector2 to)
    {
        var delta = to - from;
        var distance = delta.Length();

        if (distance <= CrossZShotRange || distance <= 0.001f)
            return to;

        return from + delta / distance * CrossZShotRange;
    }

    private void PopupSelf(EntityUid user, string message)
    {
        _popup.PopupClient(Loc.GetString(message), user, user, PopupType.SmallCaution);
    }

    private void PopupSelfPlain(EntityUid user, string message, PopupType type = PopupType.Small)
    {
        _popup.PopupCursor(message, user, type);
    }

    private int GetRequestedShotOffset(EntityUid shooter, bool requireReadyGunForLookUp = false)
    {
        if (TryComp<CMUZLevelShooterComponent>(shooter, out var shooterComp) &&
            shooterComp.ShootDown)
        {
            return -1;
        }

        if (TryComp<CMUZLevelViewerComponent>(shooter, out var viewer) &&
            viewer.LookUp &&
            (!requireReadyGunForLookUp || HasReadyGun(shooter)))
        {
            return 1;
        }

        return 0;
    }
}
