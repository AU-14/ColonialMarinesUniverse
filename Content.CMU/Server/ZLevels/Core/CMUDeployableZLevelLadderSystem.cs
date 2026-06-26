using Content.Shared._CMU14.ZLevels.Core;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._RMC14.Ladder;
using Content.Shared.Atmos;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Light.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.ZLevels.Core;

public sealed partial class CMUDeployableZLevelLadderSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ITileDefinitionManager _tile = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private CMUZLevelsSystem _zLevels = default!;

    private static readonly TimeSpan SupportCheckInterval = TimeSpan.FromSeconds(1);

    private readonly HashSet<EntityUid> _nearbyZPhysics = new();
    private TimeSpan _nextSupportCheck;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUDeployableZLevelLadderComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<CMUDeployedZLevelLadderComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
        SubscribeLocalEvent<CMUDeployedZLevelLadderComponent, TimedDespawnEvent>(OnTimedDespawn);
        SubscribeLocalEvent<CMUDeployedZLevelLadderComponent, ComponentRemove>(OnDeployedLadderRemove);
        SubscribeLocalEvent<CMUDeployedZLevelLadderComponent, EntityTerminatingEvent>(OnDeployedLadderRemove);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        ShakeUnsupportedLadders();

        if (_timing.CurTime < _nextSupportCheck)
            return;

        _nextSupportCheck = _timing.CurTime + SupportCheckInterval;

        var query = EntityQueryEnumerator<CMUDeployedZLevelLadderComponent>();
        while (query.MoveNext(out var uid, out var deployed))
        {
            if (!deployed.Retractable ||
                deployed.UnsupportedCollapse ||
                HasRequiredSupport((uid, deployed)))
            {
                continue;
            }

            ScheduleUnsupportedCollapse((uid, deployed));
        }
    }

    private void OnUseInHand(Entity<CMUDeployableZLevelLadderComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        TryDeploy(ent, args.User);
    }

    private void OnGetAlternativeVerbs(Entity<CMUDeployedZLevelLadderComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !ent.Comp.Retractable)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-zlevel-ladder-retract"),
            Act = () => TryRetract(ent, user),
            Priority = 90,
        });
    }

    private bool TryDeploy(Entity<CMUDeployableZLevelLadderComponent> ent, EntityUid user)
    {
        if (!TryGetDeployment(ent, user, out var deployment, out var popup))
        {
            if (popup != null)
                _popup.PopupEntity(popup, ent, user, PopupType.SmallCaution);

            return false;
        }

        var lower = SpawnAtPosition(ent.Comp.UpLadderPrototype, deployment.LowerCoordinates);
        var upper = SpawnAtPosition(ent.Comp.DownLadderPrototype, deployment.UpperCoordinates);

        PrepareSpawnedLadder(lower, deployment.LowerCoordinates);
        PrepareSpawnedLadder(upper, deployment.UpperCoordinates);

        var packed = ent.Comp.PackedPrototype ?? MetaData(ent).EntityPrototype?.ID ?? "CMUDeployableZLevelLadder";
        var lowerDeployed = SetDeployedLadderData(lower,
            upper,
            packed,
            true,
            ent.Comp.SupportCollisionMask,
            ent.Comp.UnsupportedCollapseDelay,
            ent.Comp.ReturnPackedOnUnsupportedCollapse,
            ent.Comp.UnsupportedShakeDegrees,
            ent.Comp.UnsupportedShakeInterval);
        SetDeployedLadderData(upper,
            lower,
            packed,
            false,
            ent.Comp.SupportCollisionMask,
            ent.Comp.UnsupportedCollapseDelay,
            ent.Comp.ReturnPackedOnUnsupportedCollapse,
            ent.Comp.UnsupportedShakeDegrees,
            ent.Comp.UnsupportedShakeInterval);

        _popup.PopupEntity(Loc.GetString("cmu-zlevel-ladder-deploy-finish", ("ladder", ent)), user, user);
        if (!IsDeploymentSupported(deployment, ent.Comp.SupportCollisionMask))
            ScheduleUnsupportedCollapse(lowerDeployed);

        QueueDel(ent);
        return true;
    }

    private bool TryRetract(Entity<CMUDeployedZLevelLadderComponent> ent, EntityUid user)
    {
        if (!ent.Comp.Retractable)
            return false;

        if (!_hands.TryGetEmptyHand(user, out _))
        {
            _popup.PopupEntity(Loc.GetString("cmu-zlevel-ladder-retract-no-hand"), ent, user, PopupType.SmallCaution);
            return false;
        }

        var packed = SpawnAtPosition(ent.Comp.PackedPrototype, _transform.GetMoverCoordinates(user));
        if (!_hands.TryPickupAnyHand(user, packed))
        {
            QueueDel(packed);
            _popup.PopupEntity(Loc.GetString("cmu-zlevel-ladder-retract-no-hand"), ent, user, PopupType.SmallCaution);
            return false;
        }

        if (ent.Comp.OtherLadder is { } other &&
            other != ent.Owner &&
            Exists(other))
        {
            QueueDel(other);
        }

        QueueDel(ent);
        _popup.PopupEntity(Loc.GetString("cmu-zlevel-ladder-retract-finish", ("ladder", packed)), user, user);
        return true;
    }

    private void OnTimedDespawn(Entity<CMUDeployedZLevelLadderComponent> ent, ref TimedDespawnEvent args)
    {
        if (!ent.Comp.UnsupportedCollapse)
            return;

        CollapseUnsupportedLadder(ent);
    }

    private void OnDeployedLadderRemove<T>(Entity<CMUDeployedZLevelLadderComponent> ent, ref T args)
    {
        if (ent.Comp.OtherLadder is not { } other ||
            other == ent.Owner ||
            !Exists(other) ||
            EntityManager.IsQueuedForDeletion(other))
        {
            return;
        }

        QueueDel(other);
    }

    private bool TryGetDeployment(
        Entity<CMUDeployableZLevelLadderComponent> ent,
        EntityUid user,
        out LadderDeployment deployment,
        out string? popup)
    {
        deployment = default;
        popup = null;

        var userXform = Transform(user);
        if (userXform.MapUid is not { } map ||
            !TryComp<CMUZLevelMapComponent>(map, out var zMap) ||
            !TryComp<MapGridComponent>(map, out var grid))
        {
            popup = Loc.GetString("cmu-zlevel-ladder-deploy-no-level");
            return false;
        }

        Entity<CMUZLevelMapComponent?> currentMap = (map, zMap);
        if (!_zLevels.TryMapUp(currentMap, out var upperMap) ||
            !TryComp<MapGridComponent>(upperMap.Value, out var upperGrid))
        {
            popup = Loc.GetString("cmu-zlevel-ladder-deploy-no-level");
            return false;
        }

        var worldPosition = _transform.GetWorldPosition(user);
        var tile = _map.WorldToTile(map, grid, worldPosition);
        if (!_map.TryGetTileRef(map, grid, tile, out var lowerTile) ||
            lowerTile.Tile.IsEmpty)
        {
            popup = Loc.GetString("cmu-zlevel-ladder-deploy-no-floor");
            return false;
        }

        if (HasRoofAbove(currentMap, upperMap.Value, upperGrid, tile))
        {
            popup = Loc.GetString("cmu-zlevel-ladder-deploy-roof");
            return false;
        }

        if (HasLadderAt(map, grid, tile) ||
            HasLadderAt(upperMap.Value, upperGrid, tile))
        {
            popup = Loc.GetString("cmu-zlevel-ladder-deploy-blocked");
            return false;
        }

        deployment = new LadderDeployment(
            _map.GridTileToLocal(map, grid, tile),
            _map.GridTileToLocal(upperMap.Value, upperGrid, tile),
            (map, grid),
            (upperMap.Value, upperGrid),
            tile);

        return true;
    }

    private bool HasRoofAbove(
        Entity<CMUZLevelMapComponent?> currentMap,
        Entity<CMUZLevelMapComponent> upperMap,
        MapGridComponent upperGrid,
        Vector2i tile)
    {
        if (TryComp<RoofComponent>(currentMap, out var roof) &&
            HasDirectRoofFlag(roof, tile))
        {
            return true;
        }

        return !CMUZLevelOpeningCache.IsOpeningTile((upperMap.Owner, upperGrid), tile, _map, _tile);
    }

    private static bool HasDirectRoofFlag(RoofComponent roof, Vector2i tile)
    {
        var chunkOrigin = SharedMapSystem.GetChunkIndices(tile, RoofComponent.ChunkSize);
        if (!roof.Data.TryGetValue(chunkOrigin, out var bitMask))
            return false;

        var chunkRelative = SharedMapSystem.GetChunkRelative(tile, RoofComponent.ChunkSize);
        var bitFlag = (ulong) 1 << (chunkRelative.X + chunkRelative.Y * RoofComponent.ChunkSize);
        return (bitMask & bitFlag) == bitFlag;
    }

    private bool HasLadderAt(EntityUid map, MapGridComponent grid, Vector2i tile)
    {
        var anchored = _map.GetAnchoredEntitiesEnumerator(map, grid, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (HasComp<CMUZLevelLadderComponent>(uid) ||
                HasComp<LadderComponent>(uid))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsDeploymentSupported(LadderDeployment deployment, CollisionGroup mask)
    {
        return HasAdjacentSupport(deployment.LowerGrid, deployment.Tile, mask) ||
               HasAdjacentSupport(deployment.UpperGrid, deployment.Tile, mask);
    }

    private bool HasRequiredSupport(Entity<CMUDeployedZLevelLadderComponent> ent)
    {
        if (!TryGetGridTile(ent.Owner, out var lowerGrid, out var lowerTile))
            return false;

        if (HasAdjacentSupport(lowerGrid, lowerTile, ent.Comp.SupportCollisionMask))
            return true;

        if (ent.Comp.OtherLadder is not { } other ||
            !TryGetGridTile(other, out var upperGrid, out var upperTile))
        {
            return false;
        }

        return HasAdjacentSupport(upperGrid, upperTile, ent.Comp.SupportCollisionMask);
    }

    private bool HasAdjacentSupport(Entity<MapGridComponent> grid, Vector2i tile, CollisionGroup mask)
    {
        for (var i = 0; i < 4; i++)
        {
            var dir = (AtmosDirection) (1 << i);
            var adjacent = tile.Offset(dir);

            if (!_map.TryGetTileRef(grid.Owner, grid.Comp, adjacent, out var tileRef) ||
                tileRef.Tile.IsEmpty)
            {
                continue;
            }

            if (HasSupportFixtureAt(grid, adjacent, mask))
                return true;
        }

        return false;
    }

    private bool HasSupportFixtureAt(Entity<MapGridComponent> grid, Vector2i tile, CollisionGroup mask)
    {
        var anchored = _map.GetAnchoredEntitiesEnumerator(grid.Owner, grid.Comp, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (uid is not { } support ||
                TerminatingOrDeleted(support) ||
                EntityManager.IsQueuedForDeletion(support) ||
                !TryComp<FixturesComponent>(support, out var fixtures))
            {
                continue;
            }

            foreach (var fixture in fixtures.Fixtures.Values)
            {
                if (!fixture.Hard ||
                    (fixture.CollisionLayer & (int) mask) == 0)
                {
                    continue;
                }

                return true;
            }
        }

        return false;
    }

    private bool TryGetGridTile(EntityUid uid, out Entity<MapGridComponent> grid, out Vector2i tile)
    {
        grid = default;
        tile = default;

        var xform = Transform(uid);
        if (xform.MapUid is not { } map ||
            !TryComp<MapGridComponent>(map, out var gridComp))
        {
            return false;
        }

        grid = (map, gridComp);
        tile = _map.WorldToTile(map, gridComp, _transform.GetWorldPosition(uid));
        return true;
    }

    private void ScheduleUnsupportedCollapse(Entity<CMUDeployedZLevelLadderComponent> ent)
    {
        MarkUnsupportedCollapse(ent);

        if (ent.Comp.OtherLadder is { } other &&
            TryComp<CMUDeployedZLevelLadderComponent>(other, out var otherDeployed))
        {
            MarkUnsupportedCollapse((other, otherDeployed));
        }

        var timed = EnsureComp<TimedDespawnComponent>(ent.Owner);
        timed.Lifetime = Math.Max(0.1f, ent.Comp.UnsupportedCollapseDelay);

        _popup.PopupEntity(Loc.GetString("cmu-zlevel-ladder-unstable"), ent.Owner, PopupType.MediumCaution);
    }

    private void MarkUnsupportedCollapse(Entity<CMUDeployedZLevelLadderComponent> ent)
    {
        if (ent.Comp.UnsupportedCollapse)
            return;

        ent.Comp.UnsupportedCollapse = true;
        ent.Comp.UnsupportedOriginalRotation = Transform(ent.Owner).LocalRotation;
        ent.Comp.NextUnsupportedShake = TimeSpan.Zero;
        ent.Comp.UnsupportedShakePositive = false;

        ShakeUnsupportedLadder(ent);
    }

    private void ShakeUnsupportedLadders()
    {
        var query = EntityQueryEnumerator<CMUDeployedZLevelLadderComponent>();
        while (query.MoveNext(out var uid, out var deployed))
        {
            if (!deployed.UnsupportedCollapse)
                continue;

            ShakeUnsupportedLadder((uid, deployed));
        }
    }

    private void ShakeUnsupportedLadder(Entity<CMUDeployedZLevelLadderComponent> ent)
    {
        if (_timing.CurTime < ent.Comp.NextUnsupportedShake)
            return;

        ent.Comp.NextUnsupportedShake = _timing.CurTime +
                                        TimeSpan.FromSeconds(Math.Max(0.05f, ent.Comp.UnsupportedShakeInterval));
        ent.Comp.UnsupportedShakePositive = !ent.Comp.UnsupportedShakePositive;

        var degrees = MathF.Abs(ent.Comp.UnsupportedShakeDegrees);
        if (degrees <= 0)
            return;

        var offset = ent.Comp.UnsupportedShakePositive ? degrees : -degrees;
        _transform.SetLocalRotation(ent.Owner, ent.Comp.UnsupportedOriginalRotation + Angle.FromDegrees(offset));
    }

    private void CollapseUnsupportedLadder(Entity<CMUDeployedZLevelLadderComponent> ent)
    {
        RemoveHighGroundSupport(ent.Owner);
        WakeZPhysicsNear(ent.Owner);

        if (ent.Comp.ReturnPackedOnUnsupportedCollapse)
            SpawnAtPosition(ent.Comp.PackedPrototype, Transform(ent.Owner).Coordinates);

        if (ent.Comp.OtherLadder is { } other &&
            Exists(other))
        {
            RemoveHighGroundSupport(other);
            WakeZPhysicsNear(other);

            if (!EntityManager.IsQueuedForDeletion(other))
                QueueDel(other);
        }

        _popup.PopupEntity(Loc.GetString("cmu-zlevel-ladder-collapse"), ent.Owner, PopupType.MediumCaution);
    }

    private void RemoveHighGroundSupport(EntityUid uid)
    {
        if (HasComp<CMUZLevelHighGroundComponent>(uid))
            RemComp<CMUZLevelHighGroundComponent>(uid);
    }

    private void WakeZPhysicsNear(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.MapID == MapId.Nullspace)
            return;

        _nearbyZPhysics.Clear();
        _lookup.GetEntitiesInRange(
            xform.MapID,
            _transform.GetWorldPosition(uid),
            0.75f,
            _nearbyZPhysics,
            LookupFlags.Dynamic | LookupFlags.Sundries | LookupFlags.Uncontained);

        foreach (var nearby in _nearbyZPhysics)
        {
            if (TryComp<CMUZPhysicsComponent>(nearby, out var zPhysics))
                _zLevels.WakeZPhysics((nearby, zPhysics));
        }

        _nearbyZPhysics.Clear();
    }

    private void PrepareSpawnedLadder(EntityUid ladder, EntityCoordinates coordinates)
    {
        _transform.SetCoordinates(ladder, coordinates);
        _transform.SetLocalRotation(ladder, Angle.Zero);

        var xform = Transform(ladder);
        if (!xform.Anchored)
            _transform.AnchorEntity((ladder, xform));
    }

    private Entity<CMUDeployedZLevelLadderComponent> SetDeployedLadderData(
        EntityUid ladder,
        EntityUid otherLadder,
        EntProtoId packed,
        bool retractable,
        CollisionGroup supportCollisionMask,
        float unsupportedCollapseDelay,
        bool returnPackedOnUnsupportedCollapse,
        float unsupportedShakeDegrees,
        float unsupportedShakeInterval)
    {
        var deployed = EnsureComp<CMUDeployedZLevelLadderComponent>(ladder);
        deployed.OtherLadder = otherLadder;
        deployed.PackedPrototype = packed;
        deployed.Retractable = retractable;
        deployed.SupportCollisionMask = supportCollisionMask;
        deployed.UnsupportedCollapseDelay = unsupportedCollapseDelay;
        deployed.ReturnPackedOnUnsupportedCollapse = returnPackedOnUnsupportedCollapse;
        deployed.UnsupportedShakeDegrees = unsupportedShakeDegrees;
        deployed.UnsupportedShakeInterval = unsupportedShakeInterval;
        return (ladder, deployed);
    }

    private readonly record struct LadderDeployment(
        EntityCoordinates LowerCoordinates,
        EntityCoordinates UpperCoordinates,
        Entity<MapGridComponent> LowerGrid,
        Entity<MapGridComponent> UpperGrid,
        Vector2i Tile);
}
