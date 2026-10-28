using System.Numerics;
using Content.Shared.CMU14.Input;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared._RMC14.Projectiles;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.ZLevels.Core.EntitySystems;

public sealed partial class CMUZLevelShootingSystem : EntitySystem
{
    private const float CrossZShotRange = 4f;
    private const float CrossZOpeningSourceEdgeRangeTiles = 2f;
    private const float CrossZOpeningSourceNudge = 0.30f;
    // how far one floor counts for falloff/range, keeps buckshot from point blanking through a hole
    private const float CrossZLevelShotDistance = 3f;

    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private RMCProjectileSystem _projectile = default!;
    [Dependency] private RMCSizeStunSystem _sizeStun = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunComponent, ItemUnwieldedEvent>(OnGunUnwielded);
        SubscribeLocalEvent<CMUZLevelViewerComponent, CMUZLevelLookUpEnabledEvent>(OnLookUpEnabled);
        SubscribeLocalEvent<CMUZLevelViewerComponent, CMUZLevelAdjustShotEvent>(OnAdjustShot);
        SubscribeLocalEvent<CMUZLevelShooterComponent, CMUZLevelAdjustShotEvent>(OnAdjustShot);
        SubscribeAllEvent<CMUZLevelSetAimDownEvent>(OnSetAimDown);

        // the old manual toggle, for people who turned off shoot where you look
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

    private void OnSetAimDown(CMUZLevelSetAimDownEvent ev, EntitySessionEventArgs args)
    {
        if (_timing.ApplyingState ||
            args.SenderSession.AttachedEntity is not { } user)
        {
            return;
        }

        SetAimDown(user, ev.AimDown);
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

    private void OnAdjustShot<T>(Entity<T> ent, ref CMUZLevelAdjustShotEvent args)
        where T : IComponent
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (!TryAdjustShotCoordinates(
                args.User,
                args.FromCoordinates,
                args.ToCoordinates,
                out var adjustedFrom,
                out var adjustedTo))
        {
            args.Cancel();
            return;
        }

        args.FromCoordinates = adjustedFrom;
        args.ToCoordinates = adjustedTo;
    }

    private void ToggleShootDown(EntityUid user)
    {
        if (!CanAimAcrossZWithoutGun(user) &&
            !TryGetReadyGun(user, "cmu-zlevel-shoot-down-no-gun", "cmu-zlevel-shoot-down-requires-wield"))
        {
            return;
        }

        var shootDown = !IsShootDownEnabled(user);
        SetShootDown(user, shootDown);

        var message = shootDown
            ? "cmu-zlevel-shoot-down-enabled"
            : "cmu-zlevel-shoot-down-disabled";

        PopupSelf(user, message);
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
        if (_gun.TryGetGun(user, out var gun))
        {
            gunUid = gun.Owner;
            return true;
        }

        gunUid = default;
        return false;
    }

    private bool IsReadyGun(EntityUid gunUid)
    {
        return !TryComp<WieldableComponent>(gunUid, out var wieldable) || wieldable.Wielded;
    }

    private bool CanAimAcrossZWithoutGun(EntityUid user)
    {
        return HasComp<XenoComponent>(user);
    }

    private bool TryDisableShootDown(EntityUid user)
    {
        if (!IsShootDownEnabled(user))
            return false;

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

    public bool IsAimingDown(EntityUid user)
    {
        return TryComp<CMUZLevelShooterComponent>(user, out var shooter) && shooter.AimDown;
    }

    public void SetAimDown(EntityUid user, bool enabled)
    {
        if (!TryComp<CMUZLevelShooterComponent>(user, out var shooter))
        {
            if (!enabled)
                return;

            shooter = EnsureComp<CMUZLevelShooterComponent>(user);
        }

        if (shooter.AimDown == enabled)
            return;

        shooter.AimDown = enabled;
        DirtyField(user, shooter, nameof(CMUZLevelShooterComponent.AimDown));
    }

    // the client needs the same answer the server will get, wield check included
    public int GetShotOffset(EntityUid shooter)
    {
        return GetRequestedShotOffset(shooter, requireReadyGunForLookUp: true);
    }

    public bool TryAdjustShotCoordinates(
        EntityUid shooter,
        EntityCoordinates fromCoordinates,
        EntityCoordinates toCoordinates,
        out EntityCoordinates adjustedFromCoordinates,
        out EntityCoordinates adjustedToCoordinates,
        bool requireReadyGunForLookUp = true)
    {
        adjustedFromCoordinates = fromCoordinates;
        adjustedToCoordinates = toCoordinates;

        var offset = GetRequestedShotOffset(shooter, requireReadyGunForLookUp);
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

        // Open air between shooter and target: nothing to shoot through, so skip the
        // opening hunt and range clamp (straight shots at hovering dropships).
        if (_zLevels.IsZShotPathOpen(offset < 0 ? shooterMap.Value : targetMap.Value,
                fromMap.Position, toMap.Position, offset < 0 ? targetMap.Value : null))
        {
            adjustedFromCoordinates = _transform.ToCoordinates(new MapCoordinates(fromMap.Position, map.MapId));
            adjustedToCoordinates = _transform.ToCoordinates(new MapCoordinates(toMap.Position, map.MapId));
            return true;
        }

        var clampedTo = ClampCrossZShotTarget(fromMap.Position, toMap.Position);
        if (!_zLevels.TryFindZShotOpening(
                shooterMap.Value,
                targetMap.Value,
                offset,
                fromMap.Position,
                clampedTo,
                out var opening,
                preferOpeningAwayFromSource: true,
                maxSourceDistanceFromOpeningEdgeTiles: CrossZOpeningSourceEdgeRangeTiles))
        {
            PopupSelf(shooter, offset > 0
                ? "cmu-zlevel-shoot-up-blocked-floor"
                : "cmu-zlevel-shoot-down-blocked-floor");
            return false;
        }

        GetCrossZProjectilePath(
            fromMap.Position,
            toMap.Position,
            clampedTo,
            opening,
            offset,
            out var projectileFrom,
            out var projectileTo);

        var targetFrom = new MapCoordinates(projectileFrom, map.MapId);
        var targetTo = new MapCoordinates(projectileTo, map.MapId);

        adjustedFromCoordinates = _transform.ToCoordinates(targetFrom);
        adjustedToCoordinates = _transform.ToCoordinates(targetTo);
        return true;
    }

    public bool TryAdjustShotMapCoordinates(
        EntityUid shooter,
        MapCoordinates fromCoordinates,
        MapCoordinates toCoordinates,
        out MapCoordinates adjustedFromCoordinates,
        out MapCoordinates adjustedToCoordinates,
        float? maximumRange = null)
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

        // Open air between shooter and target: nothing to shoot through, so skip the
        // opening hunt and range clamp (straight shots at hovering dropships).
        if (_zLevels.IsZShotPathOpen(offset < 0 ? shooterMap.Value : targetMap.Value,
                fromCoordinates.Position, toCoordinates.Position, offset < 0 ? targetMap.Value : null))
        {
            adjustedFromCoordinates = new MapCoordinates(fromCoordinates.Position, map.MapId);
            adjustedToCoordinates = new MapCoordinates(toCoordinates.Position, map.MapId);
            return true;
        }

        var clampedTo = ClampCrossZShotTarget(fromCoordinates.Position, toCoordinates.Position, maximumRange ?? CrossZShotRange);
        if (!_zLevels.TryFindZShotOpening(
                shooterMap.Value,
                targetMap.Value,
                offset,
                fromCoordinates.Position,
                clampedTo,
                out var opening,
                preferOpeningAwayFromSource: true,
                maxSourceDistanceFromOpeningEdgeTiles: CrossZOpeningSourceEdgeRangeTiles))
        {
            PopupSelf(shooter, offset > 0
                ? "cmu-zlevel-shoot-up-blocked-floor"
                : "cmu-zlevel-shoot-down-blocked-floor");
            return false;
        }

        GetCrossZProjectilePath(
            fromCoordinates.Position,
            toCoordinates.Position,
            clampedTo,
            opening,
            offset,
            out var projectileFrom,
            out var projectileTo);

        adjustedFromCoordinates = new MapCoordinates(projectileFrom, map.MapId);
        // Lobbed bombard shots retain their aimed landing point beyond the opening.
        adjustedToCoordinates = new MapCoordinates(maximumRange != null ? clampedTo : projectileTo, map.MapId);
        return true;
    }

    public bool TryGetProjectileVisualOffset(
        EntityUid shooter,
        EntityCoordinates sourceFromCoordinates,
        EntityCoordinates projectileFromCoordinates,
        out Vector2 visualOffset,
        bool requireReadyGunForLookUp = true)
    {
        visualOffset = default;

        var offset = GetRequestedShotOffset(shooter, requireReadyGunForLookUp);
        if (offset == 0)
            return false;

        var sourceFromMap = _transform.ToMapCoordinates(sourceFromCoordinates);
        var projectileFromMap = _transform.ToMapCoordinates(projectileFromCoordinates);
        if (sourceFromMap.MapId == MapId.Nullspace ||
            projectileFromMap.MapId == MapId.Nullspace)
        {
            return false;
        }

        return TryGetProjectileVisualOffset(
            shooter,
            sourceFromMap,
            projectileFromMap,
            out visualOffset,
            requireReadyGunForLookUp);
    }

    public bool TryGetProjectileVisualOffset(
        EntityUid shooter,
        MapCoordinates sourceFromCoordinates,
        MapCoordinates projectileFromCoordinates,
        out Vector2 visualOffset,
        bool requireReadyGunForLookUp = false)
    {
        visualOffset = default;

        var offset = GetRequestedShotOffset(shooter, requireReadyGunForLookUp);
        if (offset == 0)
            return false;

        if (sourceFromCoordinates.MapId == MapId.Nullspace ||
            projectileFromCoordinates.MapId == MapId.Nullspace)
        {
            return false;
        }

        // Keep the projectile physics on the opening path, but shift its sprite to
        // the barrel position in the compensated Z render pass.
        visualOffset = sourceFromCoordinates.Position - GetCrossZRenderOffset(offset) - projectileFromCoordinates.Position;
        return visualOffset.LengthSquared() > 0.001f;
    }

    public void ApplyProjectileVisualOffset(List<EntityUid>? projectiles, Vector2 visualOffset)
    {
        if (projectiles == null ||
            visualOffset.LengthSquared() <= 0.001f)
        {
            return;
        }

        foreach (var projectile in projectiles)
        {
            ApplyProjectileVisualOffset(projectile, visualOffset);
        }
    }

    public void ApplyProjectileVisualOffset(EntityUid projectile, Vector2 visualOffset)
    {
        if (visualOffset.LengthSquared() <= 0.001f)
            return;

        // Do not dirty server-owned entities during client prediction. Server state
        // will add the synced visual offset when the shot is confirmed.
        if (_timing.InPrediction && !IsClientSide(projectile))
        {
            if (!TryComp<CMUZLevelPredictedProjectileVisualOffsetComponent>(projectile, out var predictedVisual))
            {
                predictedVisual = new CMUZLevelPredictedProjectileVisualOffsetComponent
                {
                    Offset = visualOffset,
                };

                AddComp(projectile, predictedVisual);
                return;
            }

            predictedVisual.Offset = visualOffset;
            return;
        }

        if (!TryComp<CMUZLevelProjectileVisualOffsetComponent>(projectile, out var visual))
        {
            visual = new CMUZLevelProjectileVisualOffsetComponent
            {
                Offset = visualOffset,
            };

            AddComp(projectile, visual);
            Dirty(projectile, visual);
            return;
        }

        visual.Offset = visualOffset;
        Dirty(projectile, visual);
    }

    // the bullet spawns at the opening (or right under you over open air), so falloff, range and
    // point blank stuns all thought the target was next to you. buckshot through a floor deleted
    // xenos. move those origins back by the distance the shot already covered
    public void ApplyCrossZShotOrigin(List<EntityUid>? projectiles, EntityCoordinates sourceFrom, EntityCoordinates projectileFrom)
    {
        if (projectiles == null)
            return;

        var source = _transform.ToMapCoordinates(sourceFrom);
        var spawn = _transform.ToMapCoordinates(projectileFrom);
        if (source.MapId == spawn.MapId ||
            spawn.MapId == MapId.Nullspace)
        {
            return;
        }

        var horizontal = spawn.Position - source.Position;
        var travelled = MathF.Sqrt(horizontal.LengthSquared() + CrossZLevelShotDistance * CrossZLevelShotDistance);

        foreach (var projectile in projectiles)
        {
            // same as the visual offset, server state brings it over for server-owned shots
            if (_timing.InPrediction && !IsClientSide(projectile))
                continue;

            var direction = TryComp<PhysicsComponent>(projectile, out var physics) ? physics.LinearVelocity : Vector2.Zero;
            if (direction.LengthSquared() <= 0.001f)
                direction = horizontal;
            if (direction.LengthSquared() <= 0.001f)
                direction = Vector2.UnitY;

            var origin = new MapCoordinates(spawn.Position - Vector2.Normalize(direction) * travelled, spawn.MapId);
            var parent = _transform.GetMoverCoordinates(projectile).EntityId;
            _projectile.SetShotOrigin(projectile, _transform.ToCoordinates(parent, origin));
            _sizeStun.SetShotOrigin(projectile, origin);
        }
    }

    private static void GetCrossZProjectilePath(
        Vector2 from,
        Vector2 to,
        Vector2 clampedTo,
        Vector2 opening,
        int offset,
        out Vector2 projectileFrom,
        out Vector2 projectileTo)
    {
        projectileFrom = opening;
        var direction = to - opening;
        if (direction.LengthSquared() <= 0.001f)
            direction = clampedTo - opening;

        if (direction.LengthSquared() <= 0.001f)
        {
            projectileTo = clampedTo;
            return;
        }

        var distance = Math.Max(1f, direction.Length());
        projectileTo = projectileFrom + Vector2.Normalize(direction) * distance;
    }

    private static Vector2 GetCrossZRenderOffset(int offset)
    {
        return new Vector2(0f, CMUSharedZLevelsSystem.ZLevelVisualOffset * offset);
    }

    private static Vector2 NudgeOpeningTowardSource(Vector2 opening, Vector2 source)
    {
        var sourceDirection = source - opening;
        if (sourceDirection.LengthSquared() <= 0.001f)
            return opening;

        return opening + Vector2.Normalize(sourceDirection) * CrossZOpeningSourceNudge;
    }

    private static Vector2 ClampCrossZShotTarget(Vector2 from, Vector2 to, float range = CrossZShotRange)
    {
        var delta = to - from;
        var distance = delta.Length();

        if (distance <= range || distance <= 0.001f)
            return to;

        return from + delta / distance * range;
    }

    private void PopupSelf(EntityUid user, string message)
    {
        _popup.PopupEntity(Loc.GetString(message), user, user, PopupType.SmallCaution);
    }

    private int GetRequestedShotOffset(EntityUid shooter, bool requireReadyGunForLookUp = false)
    {
        TryComp<CMUZLevelShooterComponent>(shooter, out var shooterComp);
        if (shooterComp is { ShootDown: true })
            return -1;

        if (TryComp<CMUZLevelViewerComponent>(shooter, out var viewer) &&
            viewer.LookUp &&
            (!requireReadyGunForLookUp || HasReadyGun(shooter)))
        {
            return 1;
        }

        if (shooterComp is { AimDown: true } &&
            (!requireReadyGunForLookUp || HasReadyGun(shooter) || CanAimAcrossZWithoutGun(shooter)))
        {
            return -1;
        }

        return 0;
    }
}

[Serializable, NetSerializable]
public sealed class CMUZLevelSetAimDownEvent(bool aimDown) : EntityEventArgs
{
    public readonly bool AimDown = aimDown;
}

[ByRefEvent]
public sealed class CMUZLevelAdjustShotEvent(
    EntityUid user,
    EntityCoordinates fromCoordinates,
    EntityCoordinates toCoordinates) : CancellableEntityEventArgs
{
    public EntityUid User = user;
    public EntityCoordinates FromCoordinates = fromCoordinates;
    public EntityCoordinates ToCoordinates = toCoordinates;
    public bool Handled;
}
