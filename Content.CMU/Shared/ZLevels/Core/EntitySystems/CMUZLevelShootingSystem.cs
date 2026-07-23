using System.Numerics;
using System.Threading;
using Content.Shared._CMU14.Input;
using Content.Shared._CMU14.ZLevels;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Input.Binding;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.ZLevels.Core.EntitySystems;

public sealed partial class CMUZLevelShootingSystem : EntitySystem
{
    private const float CrossZShotRange = 4f;
    private const float CrossZOpeningSourceEdgeRangeTiles = 2f;
    private const float CrossZOpeningSourceNudge = 0.30f;
    public const int DefaultSourceCollisionMask =
        (int) (CollisionGroup.Impassable | CollisionGroup.BulletImpassable | CollisionGroup.Opaque);

    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;
    private readonly Dictionary<EntProtoId, int> _projectileCollisionMasks = new();
    private int _clearShootDownRequested;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunComponent, ItemUnwieldedEvent>(OnGunUnwielded);
        SubscribeLocalEvent<CMUZLevelViewerComponent, CMUZLevelLookUpEnabledEvent>(OnLookUpEnabled);
        Subs.CVar(_config, CMUZLevelsCVars.Enabled, OnZLevelsEnabledChanged, true);

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

    private void OnZLevelsEnabledChanged(bool enabled)
    {
        if (enabled)
            return;

        Interlocked.Exchange(ref _clearShootDownRequested, 1);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (Interlocked.Exchange(ref _clearShootDownRequested, 0) == 0)
            return;

        var query = EntityQueryEnumerator<CMUZLevelShooterComponent>();
        while (query.MoveNext(out var uid, out var shooter))
        {
            if (shooter.ShootDown)
                SetShootDown(uid, false);
        }
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
        if (!_config.GetCVar(CMUZLevelsCVars.Enabled))
        {
            TryDisableShootDown(user);
            return;
        }

        if (!CanAimAcrossZWithoutGun(user) &&
            !TryGetReadyGun(user, "cmu-zlevel-shoot-down-no-gun", "cmu-zlevel-shoot-down-requires-wield"))
        {
            return;
        }

        var shootDown = !IsShootDownEnabled(user);
        if (shootDown && !CanShootAtOffset(user, -1))
        {
            PopupSelf(user, "cmu-zlevel-shoot-down-no-level");
            return;
        }

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
        return _gun.TryGetGun(user, out gunUid, out _);
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

    public bool SetShootDown(EntityUid user, bool enabled)
    {
        if (enabled &&
            (!_config.GetCVar(CMUZLevelsCVars.Enabled) ||
             !CanShootAtOffset(user, -1)))
        {
            return false;
        }

        CMUZLevelShooterComponent shooter;
        if (TryComp<CMUZLevelShooterComponent>(user, out var existing))
        {
            shooter = existing;
        }
        else
        {
            if (!enabled)
                return false;

            shooter = EnsureComp<CMUZLevelShooterComponent>(user);
        }

        if (shooter.ShootDown == enabled)
            return false;

        shooter.ShootDown = enabled;
        DirtyField(user, shooter, nameof(CMUZLevelShooterComponent.ShootDown));

        if (enabled)
            _zLevels.TryDisableLookUp(user);

        return true;
    }

    public bool TryAdjustShotCoordinates(
        EntityUid shooter,
        EntityCoordinates fromCoordinates,
        EntityCoordinates toCoordinates,
        out EntityCoordinates adjustedFromCoordinates,
        out EntityCoordinates adjustedToCoordinates,
        bool requireReadyGunForLookUp = true,
        int sourceCollisionMask = DefaultSourceCollisionMask)
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
            ClearStaleShotOffset(shooter, offset);
            return true;
        }

        var fromMap = _transform.ToMapCoordinates(fromCoordinates);
        var toMap = _transform.ToMapCoordinates(toCoordinates);
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
            out var projectileFrom,
            out var projectileTo);

        if (ResolveCrossZShotPath(
                hasRequestedOffset: true,
                hasTargetMap: true,
                hasOpening: true,
                sourcePathBlocked: IsSourcePathBlocked(fromMap, projectileFrom, shooter, sourceCollisionMask)) ==
            CrossZShotPathDecision.SameLevel)
        {
            return true;
        }

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
        int sourceCollisionMask = DefaultSourceCollisionMask)
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
            ClearStaleShotOffset(shooter, offset);
            return true;
        }

        var clampedTo = ClampCrossZShotTarget(fromCoordinates.Position, toCoordinates.Position);
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
            out var projectileFrom,
            out var projectileTo);

        if (ResolveCrossZShotPath(
                hasRequestedOffset: true,
                hasTargetMap: true,
                hasOpening: true,
                sourcePathBlocked: IsSourcePathBlocked(fromCoordinates, projectileFrom, shooter, sourceCollisionMask)) ==
            CrossZShotPathDecision.SameLevel)
        {
            return true;
        }

        adjustedFromCoordinates = new MapCoordinates(projectileFrom, map.MapId);
        adjustedToCoordinates = new MapCoordinates(projectileTo, map.MapId);
        return true;
    }

    public bool IsCrossZSourcePathBlocked(
        EntityUid shooter,
        EntityCoordinates sourceCoordinates,
        EntityCoordinates projectedSourceCoordinates,
        int collisionMask)
    {
        if (collisionMask == 0)
            return false;

        var source = _transform.ToMapCoordinates(sourceCoordinates);
        var projectedSource = _transform.ToMapCoordinates(projectedSourceCoordinates);
        if (source.MapId == MapId.Nullspace ||
            projectedSource.MapId == MapId.Nullspace ||
            source.MapId == projectedSource.MapId)
        {
            return false;
        }

        return IsSourcePathBlocked(source, projectedSource.Position, shooter, collisionMask);
    }

    public int GetProjectileCollisionMask(EntProtoId projectile)
    {
        if (_projectileCollisionMasks.TryGetValue(projectile, out var cached))
            return cached;

        if (!ProtoMan.TryIndex<EntityPrototype>(projectile, out var prototype))
            return DefaultSourceCollisionMask;

        var mask = 0;
        if (prototype.TryComp<FixturesComponent>(out var fixtures, Factory))
        {
            foreach (var fixture in fixtures.Fixtures.Values)
            {
                mask |= fixture.CollisionMask;
            }
        }

        mask = mask == 0 ? DefaultSourceCollisionMask : mask;
        _projectileCollisionMasks[projectile] = mask;
        return mask;
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
            projectileFromCoordinates.MapId == MapId.Nullspace ||
            sourceFromCoordinates.MapId == projectileFromCoordinates.MapId)
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

    private static void GetCrossZProjectilePath(
        Vector2 from,
        Vector2 to,
        Vector2 clampedTo,
        Vector2 opening,
        out Vector2 projectileFrom,
        out Vector2 projectileTo)
    {
        projectileFrom = NudgeOpeningTowardSource(opening, from);
        var direction = to - from;
        if (direction.LengthSquared() <= 0.001f)
            direction = clampedTo - projectileFrom;

        if (direction.LengthSquared() <= 0.001f)
        {
            projectileTo = clampedTo;
            return;
        }

        var distance = Math.Max(1f, Vector2.Distance(projectileFrom, clampedTo));
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

    private static Vector2 ClampCrossZShotTarget(Vector2 from, Vector2 to)
    {
        var delta = to - from;
        var distance = delta.Length();

        if (distance <= CrossZShotRange || distance <= 0.001f)
            return to;

        return from + delta / distance * CrossZShotRange;
    }

    private bool IsSourcePathBlocked(
        MapCoordinates source,
        Vector2 target,
        EntityUid shooter,
        int collisionMask)
    {
        if (collisionMask == 0)
            return false;

        var direction = target - source.Position;
        var distance = direction.Length();
        if (distance <= 0.001f)
            return false;

        var ray = new CollisionRay(
            source.Position,
            direction / distance,
            collisionMask);

        foreach (var _ in _physics.IntersectRay(
                     source.MapId,
                     ray,
                     distance,
                     ignoredEnt: shooter,
                     returnOnFirstHit: true))
        {
            return true;
        }

        return false;
    }

    internal static CrossZShotPathDecision ResolveCrossZShotPath(
        bool hasRequestedOffset,
        bool hasTargetMap,
        bool hasOpening,
        bool sourcePathBlocked)
    {
        if (!hasRequestedOffset ||
            !hasTargetMap ||
            sourcePathBlocked)
        {
            return CrossZShotPathDecision.SameLevel;
        }

        return hasOpening
            ? CrossZShotPathDecision.CrossLevel
            : CrossZShotPathDecision.BlockedFloor;
    }

    private void PopupSelf(EntityUid user, string message)
    {
        _popup.PopupClient(Loc.GetString(message), user, user, PopupType.SmallCaution);
    }

    private int GetRequestedShotOffset(EntityUid shooter, bool requireReadyGunForLookUp = false)
    {
        if (!_config.GetCVar(CMUZLevelsCVars.Enabled))
        {
            TryDisableShootDown(shooter);
            _zLevels.TryDisableLookUp(shooter);
            return 0;
        }

        var offset = 0;
        if (TryComp<CMUZLevelShooterComponent>(shooter, out var shooterComp) &&
            shooterComp.ShootDown)
        {
            offset = -1;
        }
        else if (TryComp<CMUZLevelViewerComponent>(shooter, out var viewer) &&
                 viewer.LookUp &&
                 (!requireReadyGunForLookUp || HasReadyGun(shooter)))
        {
            offset = 1;
        }

        if (offset == 0 ||
            CanShootAtOffset(shooter, offset))
        {
            return offset;
        }

        ClearStaleShotOffset(shooter, offset);
        return 0;
    }

    private bool CanShootAtOffset(EntityUid shooter, int offset)
    {
        return Transform(shooter).MapUid is { } map &&
               _zLevels.TryMapOffset(map, offset, out _);
    }

    private void ClearStaleShotOffset(EntityUid shooter, int offset)
    {
        if (offset < 0)
            TryDisableShootDown(shooter);
        else
            _zLevels.TryDisableLookUp(shooter);
    }

    internal enum CrossZShotPathDecision : byte
    {
        SameLevel,
        BlockedFloor,
        CrossLevel,
    }
}
