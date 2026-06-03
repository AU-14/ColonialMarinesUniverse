using System.Numerics;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._RMC14.Fireman;
using Content.Shared.Chasm;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Throwing;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.ZLevels.Core.EntitySystems;

public abstract partial class CMUSharedZLevelsSystem
{
    public const int MaxZLevelsBelowRendering = 8;

    private const float ZGravityForce = 9.8f;
    private const float ZVelocityLimit = 20.0f;
    protected const float MinActiveZVelocity = 0.05f;

    /// <summary>
    /// The maximum height at which a player will automatically climb higher when stepping on a highground entity.
    /// </summary>
    private const float MaxStepHeight = 0.5f;
    private const float GroundSnapDistance = 0.05f;

    /// <summary>
    /// How far past a tile edge high ground is allowed to support an entity.
    /// Ramp highground only uses this past its top edge; flat highground uses it on every edge.
    /// </summary>
    private const float HighGroundEdgeSupport = 0.35f;

    /// <summary>
    /// The minimum speed required to trigger LandEvent events.
    /// </summary>
    private const float ImpactVelocityLimit = 4.0f;
    private static readonly ProtoId<DamageTypePrototype> BluntDamageType = "Blunt";

    private EntityQuery<CMUZLevelHighGroundComponent> _highgroundQuery;
    [Dependency] private PullingSystem _pulling = default!;

    private void InitMovement()
    {
        _highgroundQuery = GetEntityQuery<CMUZLevelHighGroundComponent>();

        SubscribeLocalEvent<DamageableComponent, CMUZLevelHitEvent>(OnFallDamage);
        SubscribeLocalEvent<PhysicsComponent, CMUZLevelHitEvent>(OnFallAreaImpact);
    }

    private void OnFallDamage(Entity<DamageableComponent> ent, ref CMUZLevelHitEvent args)
    {
        var knockdownTime = MathF.Min(args.ImpactPower * 0.25f, 5f);
        _stun.TryKnockdown(ent.Owner, TimeSpan.FromSeconds(knockdownTime), true);

        var damageType = _proto.Index(BluntDamageType);
        var damageAmount = MathF.Pow(args.ImpactPower, 2);

        _damage.TryChangeDamage(ent.Owner, new DamageSpecifier(damageType, damageAmount));
    }

    /// <summary>
    /// Cause AoE damage in impact point
    /// </summary>
    private void OnFallAreaImpact(Entity<PhysicsComponent> ent, ref CMUZLevelHitEvent args)
    {
        var entitiesAround = _lookup.GetEntitiesInRange(ent, 0.25f, LookupFlags.Uncontained);

        foreach (var victim in entitiesAround)
        {
            if (victim == ent.Owner)
                continue;

            var knockdownTime = MathF.Min(args.ImpactPower * ent.Comp.Mass * 0.1f, 10f);
            _stun.TryKnockdown(victim, TimeSpan.FromSeconds(knockdownTime), true);

            var damageType = _proto.Index(BluntDamageType);
            var damageAmount = args.ImpactPower * ent.Comp.Mass * 0.25f;

            _damage.TryChangeDamage(victim, new DamageSpecifier(damageType, damageAmount));
        }
    }



    protected void UpdateZMovement(float frameTime)
    {
        using var profile = Prof.Group("CMU Z Movement");

        var query = EntityQueryEnumerator<CMUZPhysicsComponent, CMUZFallingComponent, TransformComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var zPhys, out _, out var xform, out var physics))
        {
            if (xform.ParentUid != xform.MapUid)
            {
                StopZMovement(uid, zPhys);
                continue;
            }

            if (!_zMapQuery.HasComp(xform.MapUid))
            {
                StopZMovement(uid, zPhys);
                continue;
            }

            var oldVelocity = zPhys.Velocity;
            var oldHeight = zPhys.LocalPosition;

            //Gravity force application
            if (physics.BodyStatus == BodyStatus.OnGround || zPhys.Velocity > 0)
                zPhys.Velocity -= ZGravityForce * frameTime;

            //Movement application
            zPhys.LocalPosition += zPhys.Velocity * frameTime;

            var distanceToGround = DistanceToGround((uid, zPhys), out var stickyGround);

            if (ShouldSnapToGround(distanceToGround, stickyGround))
            {
                zPhys.LocalPosition -= distanceToGround;
                if (stickyGround)
                {
                    zPhys.Velocity = 0;
                }
            }
            if (distanceToGround <= GroundSnapDistance) //Theres a ground
            {
                if (MathF.Abs(zPhys.Velocity) >= ImpactVelocityLimit)
                {
                    RaiseLocalEvent(uid, new CMUZLevelHitEvent(MathF.Abs(zPhys.Velocity)));
                    var land = new LandEvent(null, true);
                    RaiseLocalEvent(uid, ref land);
                }

                zPhys.Velocity = -zPhys.Velocity * zPhys.Bounciness;

                if (MathF.Abs(zPhys.Velocity) <= MinActiveZVelocity)
                {
                    zPhys.Velocity = 0;
                    if (zPhys.LocalPosition >= 0f && zPhys.LocalPosition < 1f)
                    {
                        DirtyZPhysics(uid, zPhys, oldVelocity, oldHeight);
                        if (!stickyGround && MathF.Abs(zPhys.LocalPosition) <= MinActiveZVelocity)
                            RemComp<CMUZFallingComponent>(uid);

                        continue;
                    }
                }
            }

            if (zPhys.LocalPosition < 0) //We wanna fall down on ZLevel below
            {
                if (CanProcessZLevelTransition(uid, -1))
                {
                    if (TryMoveDownOrChasm(uid))
                    {
                        zPhys.LocalPosition += 1;

                        if (!stickyGround)
                        {
                            var fallEv = new CMUZLevelFallEvent();
                            RaiseLocalEvent(uid, fallEv);
                        }
                    }
                }
                else
                {
                    zPhys.LocalPosition = 0;
                }
            }
            else if (zPhys.LocalPosition >= 1) //Going up
            {
                var onHighGround = false;
                var worldPosI = _transform.GetGridOrMapTilePosition(uid);
                if (_zMapQuery.TryComp(xform.MapUid, out var zMapComp) &&
                    _gridQuery.TryComp(xform.MapUid, out var mapGrid))
                {
                    var queryHigh = _map.GetAnchoredEntitiesEnumerator(xform.MapUid.Value, mapGrid, worldPosI);
                    while (queryHigh.MoveNext(out var anchoredUid))
                    {
                        if (_highgroundQuery.HasComp(anchoredUid))
                        {
                            onHighGround = true;
                            break;
                        }
                    }
                }

                if (HasTileAbove(uid) && !onHighGround) //Hit roof
                {
                    if (MathF.Abs(zPhys.Velocity) >= ImpactVelocityLimit)
                    {
                        RaiseLocalEvent(uid, new CMUZLevelHitEvent(MathF.Abs(zPhys.Velocity)));
                        var land = new LandEvent(null, true);
                        RaiseLocalEvent(uid, ref land);
                    }

                    zPhys.LocalPosition = 1;
                    zPhys.Velocity = -zPhys.Velocity * zPhys.Bounciness;
                }
                else //Move up
                {
                    if (CanProcessZLevelTransition(uid, 1))
                    {
                        if (TryMoveUp(uid))
                            zPhys.LocalPosition -= 1;
                    }
                    else
                    {
                        zPhys.LocalPosition = 1;
                    }
                }
            }

            if (Math.Abs(zPhys.Velocity) > ZVelocityLimit)
                zPhys.Velocity = MathF.Sign(zPhys.Velocity) * ZVelocityLimit;

            DirtyZPhysics(uid, zPhys, oldVelocity, oldHeight);
        }
    }

    private static bool ShouldSnapToGround(float distanceToGround, bool stickyGround)
    {
        if (stickyGround)
            return true;

        return distanceToGround <= GroundSnapDistance && distanceToGround <= MaxStepHeight;
    }

    private void StopZMovement(EntityUid uid, CMUZPhysicsComponent zPhys)
    {
        var oldVelocity = zPhys.Velocity;
        var oldHeight = zPhys.LocalPosition;

        zPhys.Velocity = 0;
        zPhys.LocalPosition = 0;
        DirtyZPhysics(uid, zPhys, oldVelocity, oldHeight);
        RemComp<CMUZFallingComponent>(uid);
    }

    private void DirtyZPhysics(EntityUid uid, CMUZPhysicsComponent zPhys, float oldVelocity, float oldHeight)
    {
        if (Math.Abs(oldVelocity - zPhys.Velocity) > 0.01f)
            DirtyField(uid, zPhys, nameof(CMUZPhysicsComponent.Velocity));

        if (Math.Abs(oldHeight - zPhys.LocalPosition) > 0.01f)
            DirtyField(uid, zPhys, nameof(CMUZPhysicsComponent.LocalPosition));
    }

    protected virtual bool CanProcessZLevelTransition(EntityUid ent, int offset)
    {
        return true;
    }

    [PublicAPI]
    public virtual void WakeZPhysics(Entity<CMUZPhysicsComponent?> ent)
    {
    }

    /// <summary>
    /// Returns the distance to the floor. Returns <see cref="maxFloors"/> if the distance is too great.
    /// </summary>
    /// <param name="target">The entity, the distance to the floor which we calculate</param>
    /// <param name="stickyGround">true in situations where the entity smoothly descends along a sticky diagonal descent like a staircase</param>
    /// <param name="maxFloors">How many z-levels down are we prepared to check? The default is 1, since in most cases we don't need to check more than that.</param>
    /// <returns></returns>
    public float DistanceToGround(Entity<CMUZPhysicsComponent?> target, out bool stickyGround, int maxFloors = 1)
    {
        stickyGround = false;
        if (!Resolve(target, ref target.Comp, false))
            return 0; //maybe in future: simpler distance calculation for entities without zPhysComp?

        var xform = Transform(target);
        if (!_zMapQuery.TryComp(xform.MapUid, out var zMapComp))
            return 0;
        if (!_gridQuery.TryComp(xform.MapUid, out var mapGrid))
            return 0;

        var worldPos = _transform.GetWorldPosition(target);

        //Select current map by default
        Entity<CMUZLevelMapComponent> checkingMap = (xform.MapUid.Value, zMapComp);
        MapGridComponent checkingGrid = mapGrid;

        for (var floor = 0; floor <= maxFloors; floor++)
        {
            if (floor != 0) //Select map below
            {
                if (!TryMapDown((checkingMap.Owner, checkingMap.Comp), out var tempCheckingMap))
                    break;

                checkingMap = tempCheckingMap.Value;
                if (!_gridQuery.TryComp(checkingMap.Owner, out var tempCheckingGrid))
                    continue;

                checkingGrid = tempCheckingGrid;
            }

            var checkingTile = _map.WorldToTile(checkingMap, checkingGrid, worldPos);

            if (TryGetHighGroundDistance(target, checkingMap, checkingGrid, checkingTile, worldPos, floor, out var highGroundDistance, ref stickyGround))
                return highGroundDistance;

            //No ZEntities found, check floor tiles
            if (_map.TryGetTileRef(checkingMap, checkingGrid, checkingTile, out var tileRef) &&
                !tileRef.Tile.IsEmpty)
                return target.Comp.LocalPosition + floor;
        }

        return maxFloors;
    }

    private bool TryGetHighGroundDistance(
        Entity<CMUZPhysicsComponent?> target,
        Entity<CMUZLevelMapComponent> checkingMap,
        MapGridComponent checkingGrid,
        Vector2i checkingTile,
        Vector2 worldPos,
        int floor,
        out float distance,
        ref bool stickyGround)
    {
        distance = 0f;
        var found = false;
        var bestDistance = 0f;
        var bestSticky = false;
        var bestScore = float.MaxValue;
        var gridLocal = _map.WorldToLocal(checkingMap, checkingGrid, worldPos) / checkingGrid.TileSize;

        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                var tile = checkingTile + new Vector2i(x, y);
                var isCurrentTile = x == 0 && y == 0;
                var query = _map.GetAnchoredEntitiesEnumerator(checkingMap, checkingGrid, tile);

                while (query.MoveNext(out var uid))
                {
                    if (!_highgroundQuery.TryComp(uid, out var heightComp))
                        continue;

                    if (floor == 0 && heightComp.SupportOnlyFromAbove)
                        continue;

                    if (heightComp.HeightCurve.Count == 0)
                        continue;

                    var local = gridLocal - new Vector2(tile.X, tile.Y);
                    if (!TryGetHighGroundCurveT(uid.Value, heightComp, local, isCurrentTile, out var t))
                        continue;

                    var candidateDistance = GetHighGroundDistance(target.Comp!, heightComp, t, floor);
                    var score = MathF.Abs(candidateDistance);
                    if (isCurrentTile)
                        score -= 0.001f;

                    if (score >= bestScore)
                        continue;

                    found = true;
                    bestScore = score;
                    bestDistance = candidateDistance;
                    bestSticky = target.Comp!.Velocity <= 0.01f && target.Comp.Velocity > -4f && heightComp.Stick;
                }
            }
        }

        if (!found)
            return false;

        distance = bestDistance;
        stickyGround = bestSticky;
        return true;
    }

    private bool TryGetHighGroundCurveT(
        EntityUid highGround,
        CMUZLevelHighGroundComponent heightComp,
        Vector2 local,
        bool isCurrentTile,
        out float t)
    {
        t = 0f;

        if (isCurrentTile)
        {
            if (local.X < 0f || local.X > 1f || local.Y < 0f || local.Y > 1f)
                return false;

            t = GetHighGroundCurveT(highGround, heightComp, local);
            return true;
        }

        if (IsFlatHighGround(heightComp))
        {
            if (local.X < -HighGroundEdgeSupport ||
                local.X > 1f + HighGroundEdgeSupport ||
                local.Y < -HighGroundEdgeSupport ||
                local.Y > 1f + HighGroundEdgeSupport)
            {
                return false;
            }

            t = GetHighGroundCurveT(highGround, heightComp, local);
            return true;
        }

        if (heightComp.Corner)
        {
            if (local.X < -HighGroundEdgeSupport ||
                local.X > 1f + HighGroundEdgeSupport ||
                local.Y < -HighGroundEdgeSupport ||
                local.Y > 1f + HighGroundEdgeSupport)
            {
                return false;
            }

            t = GetHighGroundCurveT(highGround, heightComp, local);
            return IsNearHighGroundTopEdge(heightComp, t);
        }

        if (!TryGetHighGroundRampAxes(highGround, local, out var ramp, out var side))
            return false;

        if (!IsNearHighGroundTopEdge(heightComp, ramp) ||
            side < -HighGroundEdgeSupport ||
            side > 1f + HighGroundEdgeSupport)
        {
            return false;
        }

        t = ramp;
        return true;
    }

    private float GetHighGroundCurveT(EntityUid highGround, CMUZLevelHighGroundComponent heightComp, Vector2 local)
    {
        if (heightComp.Corner)
        {
            var dir = _transform.GetWorldRotation(highGround).GetCardinalDir();
            return dir switch
            {
                Direction.East => (local.X + 1f - local.Y) / 2f,
                Direction.West => (1f - local.X + local.Y) / 2f,
                Direction.North => (local.X + local.Y) / 2f,
                Direction.South => (1f - local.X + 1f - local.Y) / 2f,
                _ => 0.5f,
            };
        }

        if (TryGetHighGroundRampAxes(highGround, local, out var ramp, out _))
            return ramp;

        return 0.5f;
    }

    private bool TryGetHighGroundRampAxes(EntityUid highGround, Vector2 local, out float ramp, out float side)
    {
        var dir = _transform.GetWorldRotation(highGround).GetCardinalDir();

        (ramp, side) = dir switch
        {
            Direction.East => (local.X, local.Y),
            Direction.West => (1f - local.X, local.Y),
            Direction.North => (local.Y, local.X),
            Direction.South => (1f - local.Y, local.X),
            _ => (0.5f, 0.5f),
        };

        return dir is Direction.East or Direction.West or Direction.North or Direction.South;
    }

    private static bool IsFlatHighGround(CMUZLevelHighGroundComponent heightComp)
    {
        if (heightComp.HeightCurve.Count <= 1)
            return true;

        var first = heightComp.HeightCurve[0];
        for (var i = 1; i < heightComp.HeightCurve.Count; i++)
        {
            if (MathF.Abs(heightComp.HeightCurve[i] - first) > 0.01f)
                return false;
        }

        return true;
    }

    private static bool IsNearHighGroundTopEdge(CMUZLevelHighGroundComponent heightComp, float t)
    {
        if (heightComp.HeightCurve.Count <= 1)
            return t >= -HighGroundEdgeSupport && t <= 1f + HighGroundEdgeSupport;

        var first = heightComp.HeightCurve[0];
        var last = heightComp.HeightCurve[^1];

        if (first > last + 0.01f)
            return t >= -HighGroundEdgeSupport && t <= 0f;

        return t >= 1f && t <= 1f + HighGroundEdgeSupport;
    }

    private float GetHighGroundDistance(
        CMUZPhysicsComponent zPhysics,
        CMUZLevelHighGroundComponent heightComp,
        float t,
        int floor)
    {
        t = Math.Clamp(t, 0f, 1f);

        var curve = heightComp.HeightCurve;
        if (curve.Count == 1)
            return zPhysics.LocalPosition + floor - curve[0];

        var step = 1f / (curve.Count - 1);
        var index = (int)(t / step);
        var frac = (t - index * step) / step;

        var y0 = curve[Math.Clamp(index, 0, curve.Count - 1)];
        var y1 = curve[Math.Clamp(index + 1, 0, curve.Count - 1)];

        return zPhysics.LocalPosition + floor - MathHelper.Lerp(y0, y1, frac);
    }

    /// <summary>
    /// Checks whether there is a ceiling above the specified entity (tiles on the layer above).
    /// If there are no Z-levels above, false will be returned.
    /// </summary>
    [PublicAPI]
    public bool HasTileAbove(EntityUid ent, Entity<CMUZLevelMapComponent?>? currentMapUid = null)
    {
        currentMapUid ??= Transform(ent).MapUid;

        if (currentMapUid is null)
            return false;

        if (!TryMapUp(currentMapUid.Value, out var mapAboveUid))
            return false;

        if (!_gridQuery.TryComp(mapAboveUid.Value, out var mapAboveGrid))
            return false;

        if (_map.TryGetTileRef(mapAboveUid.Value, mapAboveGrid, _transform.GetWorldPosition(ent), out var tileRef) &&
            !tileRef.Tile.IsEmpty)
            return true;

        return false;
    }

    /// <summary>
    /// Checks whether there is a ceiling above the specified entity (tiles on the layer above).
    /// If there are no Z-levels above, false will be returned.
    /// </summary>
    [PublicAPI]
    public bool HasTileAbove(Vector2i indices, Entity<CMUZLevelMapComponent?> map)
    {
        if (!Resolve(map, ref map.Comp, false))
            return false;

        if (!TryMapUp(map, out var mapAboveUid))
            return false;

        if (!_gridQuery.TryComp(mapAboveUid.Value, out var mapAboveGrid))
            return false;

        if (_map.TryGetTileRef(mapAboveUid.Value, mapAboveGrid, indices, out var tileRef) &&
            !tileRef.Tile.IsEmpty)
            return true;

        return false;
    }

    [PublicAPI]
    public bool TryProjectToGround(EntityCoordinates coordinates, out EntityCoordinates projected, int maxFloors = MaxZLevelsBelowRendering)
    {
        projected = coordinates;

        var mapCoordinates = _transform.ToMapCoordinates(coordinates);
        if (!_map.TryGetMap(mapCoordinates.MapId, out var mapUid) ||
            mapUid is not { } resolvedMapUid ||
            !_zMapQuery.TryComp(resolvedMapUid, out var zMap) ||
            !_gridQuery.TryComp(resolvedMapUid, out var grid))
        {
            return true;
        }

        var worldPosition = mapCoordinates.Position;
        Entity<CMUZLevelMapComponent?> checkingMap = (resolvedMapUid, zMap);
        var checkingGrid = grid;

        for (var floor = 0; floor <= maxFloors; floor++)
        {
            var tile = _map.WorldToTile(checkingMap, checkingGrid, worldPosition);
            if (_map.TryGetTileRef(checkingMap, checkingGrid, tile, out var tileRef) &&
                !tileRef.Tile.IsEmpty)
            {
                if (!_mapQuery.TryComp(checkingMap.Owner, out var map))
                    return false;

                projected = _transform.ToCoordinates(new MapCoordinates(worldPosition, map.MapId));
                return true;
            }

            if (!TryMapDown(checkingMap, out var belowMap) ||
                !_gridQuery.TryComp(belowMap.Value, out checkingGrid))
            {
                break;
            }

            checkingMap = (belowMap.Value.Owner, belowMap.Value.Comp);
        }

        return false;
    }

    /// <summary>
    /// Sets the vertical velocity for the entity. Positive values make the entity fly upward. Negative values make it fly downward.
    /// </summary>
    [PublicAPI]
    public void SetZVelocity(Entity<CMUZPhysicsComponent?> ent, float newVelocity)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        ent.Comp.Velocity = newVelocity;
        DirtyField(ent, ent.Comp, nameof(CMUZPhysicsComponent.Velocity));
        WakeZPhysics(ent);
    }

    /// <summary>
    /// Sets the local vertical position for an entity inside its current Z-level.
    /// </summary>
    [PublicAPI]
    public void SetZLocalPosition(Entity<CMUZPhysicsComponent?> ent, float localPosition)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        if (Math.Abs(ent.Comp.LocalPosition - localPosition) <= 0.01f)
            return;

        ent.Comp.LocalPosition = localPosition;
        DirtyField(ent, ent.Comp, nameof(CMUZPhysicsComponent.LocalPosition));
        WakeZPhysics(ent);
    }

    /// <summary>
    /// Add the vertical velocity for the entity. Positive values make the entity fly upward. Negative values make it fly downward.
    /// </summary>
    [PublicAPI]
    public void AddZVelocity(Entity<CMUZPhysicsComponent?> ent, float newVelocity)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return;

        ent.Comp.Velocity += newVelocity;
        DirtyField(ent, ent.Comp, nameof(CMUZPhysicsComponent.Velocity));
        WakeZPhysics(ent);
    }

    [PublicAPI]
    public bool TryMove(EntityUid ent, int offset, Entity<CMUZLevelMapComponent?>? map = null, Vector2? worldPosition = null)
    {
        map ??= Transform(ent).MapUid;

        if (map is null)
            return false;

        if (!TryMapOffset(map.Value, offset, out _, out var targetMapComp))
            return false;

        var target = new MapCoordinates(worldPosition ?? _transform.GetWorldPosition(ent), targetMapComp.MapId);
        MoveEntityAndPulledTarget(ent, target, offset);

        return true;
    }

    private void MoveEntityAndPulledTarget(EntityUid ent, MapCoordinates target, int offset)
    {
        if (TryComp(ent, out PullableComponent? otherPullable) &&
            otherPullable.Puller != null)
        {
            _pulling.TryStopPull(ent, otherPullable, otherPullable.Puller.Value);
        }

        if (TryComp(ent, out PullerComponent? puller) &&
            TryComp(puller.Pulling, out PullableComponent? pullable))
        {
            var pulled = puller.Pulling.Value;

            if (HasComp<BeingFiremanCarriedComponent>(pulled))
            {
                _transform.SetMapCoordinates(ent, target);
                RaiseLocalEvent(ent, new CMUZLevelMoveEvent(offset));
                MoveFiremanCarriedTarget(ent, target, offset);
                return;
            }

            if (TryComp(pulled, out PullerComponent? otherPullingPuller) &&
                TryComp(otherPullingPuller.Pulling, out PullableComponent? otherPullingPullable))
            {
                _pulling.TryStopPull(otherPullingPuller.Pulling.Value, otherPullingPullable, pulled);
            }

            _pulling.TryStopPull(pulled, pullable, ent);
            _transform.SetMapCoordinates(ent, target);
            _transform.SetMapCoordinates(pulled, target);
            _pulling.TryStartPull(ent, pulled);

            RaiseLocalEvent(ent, new CMUZLevelMoveEvent(offset));
            RaiseLocalEvent(pulled, new CMUZLevelMoveEvent(offset));
            return;
        }

        _transform.SetMapCoordinates(ent, target);
        RaiseLocalEvent(ent, new CMUZLevelMoveEvent(offset));
        MoveFiremanCarriedTarget(ent, target, offset);
    }

    private void MoveFiremanCarriedTarget(EntityUid carrier, MapCoordinates target, int offset)
    {
        if (!TryComp<CanFiremanCarryComponent>(carrier, out var carry) ||
            carry.Carrying is not { } carried ||
            TerminatingOrDeleted(carried))
        {
            return;
        }

        if (Transform(carried).ParentUid != carrier)
            _transform.SetMapCoordinates(carried, target);

        RaiseLocalEvent(carried, new CMUZLevelMoveEvent(offset));
    }

    [PublicAPI]
    public bool TryMoveUp(EntityUid ent)
    {
        return TryMove(ent, 1);
    }

    [PublicAPI]
    public bool TryMoveDown(EntityUid ent)
    {
        return TryMove(ent, -1);
    }

    [PublicAPI]
    public bool TryMoveDownOrChasm(EntityUid ent)
    {
        if (TryMoveDown(ent))
            return true;

        //welp, that default Chasm behavior. Not really good, but ok for now.
        if (HasComp<ChasmFallingComponent>(ent))
            return false; //Already falling

        var audio = new SoundPathSpecifier("/Audio/Effects/falling.ogg");
        _audio.PlayPredicted(audio, Transform(ent).Coordinates, ent);
        var falling = AddComp<ChasmFallingComponent>(ent);
        falling.NextDeletionTime = _timing.CurTime + falling.DeletionTime;
        _blocker.UpdateCanMove(ent);

        return false;
    }
}

/// <summary>
/// Is called on an entity when it moves between z-levels.
/// </summary>
/// <param name="offset">How many levels were crossed. If negative, it means there was a downward movement. If positive, it means an upward movement.</param>
public sealed class CMUZLevelMoveEvent(int offset) : EntityEventArgs
{
    public int Offset = offset;
}

/// <summary>
/// Is triggered when an entity falls to the lower z-levels under the force of gravity
/// </summary>
public sealed class CMUZLevelFallEvent : EntityEventArgs;

/// <summary>
/// It is called on an entity when it hits the floor or ceiling with force.
/// </summary>
/// <param name="impactPower">The speed at the moment of impact. Always positive</param>
public sealed class CMUZLevelHitEvent(float impactPower) : EntityEventArgs
{
    public float ImpactPower = impactPower;
}
