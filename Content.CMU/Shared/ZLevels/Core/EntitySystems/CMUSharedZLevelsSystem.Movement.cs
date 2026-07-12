using System.Numerics;
using Content.Shared._CMU14.ZLevels;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._CMU14.ZLevels.Vehicles;
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
using Robust.Shared.Physics;
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
    private const float ZPhysicsSleepDistance = 0.05f;
    private const float StickyMoveSnapUpTransitionHeight = 0.95f;
    private const float MoveGroundSnapSweepStep = 0.125f;
    private const int MaxMoveGroundSnapSweepSamples = 8;

    /// <summary>
    /// How far past a tile edge high ground is allowed to support an entity.
    /// Ramp highground only uses this past its top edge; flat highground uses it on every edge.
    /// </summary>
    private const float HighGroundEdgeSupport = 0.35f;

    /// <summary>
    /// The minimum speed required to trigger LandEvent events.
    /// </summary>
    private const float ImpactVelocityLimit = 4.0f;
    private const string FallDebugTag = "[DEBUG-CMUZ-FALL]";
    private static readonly ProtoId<DamageTypePrototype> BluntDamageType = "Blunt";

    private EntityQuery<FixturesComponent> _fixturesQuery;
    private EntityQuery<CMUZLevelHighGroundComponent> _highgroundQuery;
    private EntityQuery<CMUVehicleZTraversalComponent> _vehicleTraversalQuery;
    private readonly HashSet<EntityUid> _moveSnapSuppressed = new();
    private readonly HashSet<EntityUid> _fallImpactVictims = new();
    private readonly HashSet<(EntityUid Puller, EntityUid Pulled)> _deferredPullJointRefreshes = new();
    private readonly List<(EntityUid Puller, EntityUid Pulled)> _deferredPullJointRefreshBuffer = new();
    private readonly List<Vector2> _vehicleSupportSamples = new();
    private readonly List<EntityUid> _zMovementUpdateQueue = new();
    private int _profileZMovementStoppedParent;
    private int _profileZMovementStoppedNoMap;
    private int _profileZMovementGroundContacts;
    private int _profileZMovementBoundaryChecks;
    private int _profileZMovementLandEvents;
    private int _profileZDistanceFloors;
    private int _profileZDistanceHighGroundHits;
    private int _profileZDistanceTileHits;
    private int _profileZDistanceMisses;
    private int _profileZHighGroundTiles;
    private int _profileZHighGroundAnchoredEntities;
    private int _profileZHighGroundCandidates;
    private int _profileZHighGroundAccepted;
    private int _profileZMoveSnapSweepSamples;
    private int _profileZMoveSnapSweepHighGroundChecks;
    private bool _debugFalling;
    [Dependency] private PullingSystem _pulling = default!;

    private void InitMovement()
    {
        _fixturesQuery = GetEntityQuery<FixturesComponent>();
        _highgroundQuery = GetEntityQuery<CMUZLevelHighGroundComponent>();
        _vehicleTraversalQuery = GetEntityQuery<CMUVehicleZTraversalComponent>();
        Subs.CVar(_configuration, CMUZLevelsCVars.DebugFalling, value => _debugFalling = value, true);

        SubscribeLocalEvent<DamageableComponent, CMUZLevelHitEvent>(OnFallDamage);
        SubscribeLocalEvent<PhysicsComponent, CMUZLevelHitEvent>(OnFallAreaImpact);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        FlushDeferredPullJointRefreshes();
    }

    protected void OnZPhysicsMoveGroundSnap(Entity<CMUZPhysicsComponent> ent, ref MoveEvent args)
    {
        if (!Prof.IsEnabled)
        {
            OnZPhysicsMoveGroundSnapCore(ent, ref args);
            return;
        }

        using var profile = Prof.Group("CMU Z Move Snap");
        OnZPhysicsMoveGroundSnapCore(ent, ref args);
    }

    private void OnZPhysicsMoveGroundSnapCore(Entity<CMUZPhysicsComponent> ent, ref MoveEvent args)
    {
        if (_moveSnapSuppressed.Contains(ent.Owner))
            return;

        if (!ShouldProcessMoveGroundSnap(_net.IsClient, _timing.ApplyingState))
            return;

        var oldVelocity = ent.Comp.Velocity;
        var oldHeight = ent.Comp.LocalPosition;
        Entity<CMUZPhysicsComponent?> nullableEnt = (ent.Owner, ent.Comp);
        var distanceToGround = DistanceToGround(nullableEnt, out var stickyGround);

        if (TryGetSweptStickyMoveGroundSnapDistance(ent, args, distanceToGround, stickyGround, out var sweptDistance))
        {
            distanceToGround = sweptDistance;
            stickyGround = true;
        }

        var groundSnapDistance = GetMoveGroundSnapDistance(distanceToGround, stickyGround);

        if (MathF.Abs(groundSnapDistance) <= 0.001f)
            return;

        ent.Comp.LocalPosition -= groundSnapDistance;
        if (stickyGround)
            ent.Comp.Velocity = 0f;

        if (ShouldProcessImmediateMoveSnapZLevelTransition(_net.IsClient, ent.Comp.LocalPosition, stickyGround))
        {
            if (ShouldAdvanceStickyMoveSnapToUpperBoundary(ent.Comp.LocalPosition, stickyGround))
                ent.Comp.LocalPosition = 1f;

            TryProcessZLevelBoundary(ent.Owner, ent.Comp, stickyGround);
        }

        DirtyZPhysics(ent.Owner, ent.Comp, oldVelocity, oldHeight);
        WakeZPhysics(nullableEnt);
    }

    private void OnFallDamage(Entity<DamageableComponent> ent, ref CMUZLevelHitEvent args)
    {
        var knockdownTime = MathF.Min(args.ImpactPower * 0.25f, 5f);
        _stun.TryKnockdown(ent.Owner, TimeSpan.FromSeconds(knockdownTime), true);

        var damageType = _proto.Index(BluntDamageType);
        var damageAmount = MathF.Pow(args.ImpactPower, 2);
        if (_vehicleTraversalQuery.TryComp(ent.Owner, out var vehicleTraversal))
            damageAmount *= vehicleTraversal.LandingHullDamageMultiplier;

        if (damageAmount <= 0f)
            return;

        _damage.TryChangeDamage(ent.Owner, new DamageSpecifier(damageType, damageAmount));
    }

    /// <summary>
    /// Cause AoE damage in impact point
    /// </summary>
    private void OnFallAreaImpact(Entity<PhysicsComponent> ent, ref CMUZLevelHitEvent args)
    {
        if (_vehicleTraversalQuery.HasComp(ent.Owner))
            return;

        _fallImpactVictims.Clear();
        _lookup.GetEntitiesInRange(ent, 0.25f, _fallImpactVictims, LookupFlags.Uncontained);

        foreach (var victim in _fallImpactVictims)
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
        var profiling = Prof.IsEnabled;
        if (profiling)
            ResetZMovementProfileCounters();

        var processed = 0;
        _zMovementUpdateQueue.Clear();
        var query = EntityQueryEnumerator<CMUZPhysicsComponent, CMUZFallingComponent, TransformComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out _, out _, out _, out _))
        {
            _zMovementUpdateQueue.Add(uid);
        }

        foreach (var uid in _zMovementUpdateQueue)
        {
            if (!TryComp<CMUZPhysicsComponent>(uid, out var zPhys) ||
                !HasComp<CMUZFallingComponent>(uid) ||
                !TryComp(uid, out TransformComponent? xform) ||
                !TryComp<PhysicsComponent>(uid, out var physics))
            {
                continue;
            }

            processed++;

            if (xform.ParentUid != xform.MapUid)
            {
                if (profiling)
                    _profileZMovementStoppedParent++;

                DebugLogFalling(uid, "stop-parent", $"parent={xform.ParentUid} map={xform.MapUid}");
                StopZMovement(uid, zPhys);
                continue;
            }

            if (!_zMapQuery.HasComp(xform.MapUid))
            {
                if (profiling)
                    _profileZMovementStoppedNoMap++;

                DebugLogFalling(uid, "stop-no-z-map", $"map={xform.MapUid}");
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
            var hasGroundContact = ShouldTreatAsGroundContact(distanceToGround, stickyGround);
            var fallingGroundContact = !hasGroundContact &&
                ShouldTreatAsFallingGroundContact(distanceToGround, zPhys.Velocity);
            var groundSnapDistance = fallingGroundContact
                ? distanceToGround
                : GetGroundSnapDistance(distanceToGround, stickyGround);
            hasGroundContact |= fallingGroundContact;
            var isVehicle = HasComp<CMUVehicleZTraversalComponent>(uid);
            DebugLogFalling(
                uid,
                "tick",
                $"frame={frameTime:F4} oldLocal={oldHeight:F3} local={zPhys.LocalPosition:F3} oldVel={oldVelocity:F3} vel={zPhys.Velocity:F3} distance={distanceToGround:F3} sticky={stickyGround} hasGround={hasGroundContact} fallingGround={fallingGroundContact} snap={groundSnapDistance:F3} body={physics.BodyStatus}");

            if (hasGroundContact &&
                physics.BodyStatus != BodyStatus.OnGround)
            {
                _physics.SetBodyStatus(uid, physics, BodyStatus.OnGround);
            }

            if (hasGroundContact)
            {
                if (profiling)
                    _profileZMovementGroundContacts++;

                DebugLogFalling(
                    uid,
                    "ground-contact",
                    $"preSnapLocal={zPhys.LocalPosition:F3} snap={groundSnapDistance:F3} distance={distanceToGround:F3} sticky={stickyGround} vel={zPhys.Velocity:F3}");
                zPhys.LocalPosition -= groundSnapDistance;
                if (stickyGround)
                {
                    zPhys.Velocity = 0;
                }
            }

            if (hasGroundContact && ShouldBounceOnGroundContact(zPhys.Velocity)) //Theres a ground
            {
                if (MathF.Abs(zPhys.Velocity) >= ImpactVelocityLimit)
                {
                    if (profiling)
                        _profileZMovementLandEvents++;

                    DebugLogFalling(uid, "land-event", $"impact={MathF.Abs(zPhys.Velocity):F3}");
                    RaiseLocalEvent(uid, new CMUZLevelHitEvent(MathF.Abs(zPhys.Velocity)));
                    var land = new LandEvent(null, true);
                    RaiseLocalEvent(uid, ref land);
                }

                var velocityBeforeBounce = zPhys.Velocity;
                zPhys.Velocity = -zPhys.Velocity * zPhys.Bounciness;
                DebugLogFalling(
                    uid,
                    "ground-bounce",
                    $"velocityBefore={velocityBeforeBounce:F3} bounciness={zPhys.Bounciness:F3} velocityAfter={zPhys.Velocity:F3} local={zPhys.LocalPosition:F3}");

                if (MathF.Abs(zPhys.Velocity) <= MinActiveZVelocity)
                {
                    zPhys.Velocity = 0;
                    if (zPhys.LocalPosition >= 0f && zPhys.LocalPosition < 1f)
                    {
                        DirtyZPhysics(uid, zPhys, oldVelocity, oldHeight);
                        var settledDistanceToGround = hasGroundContact
                            ? 0f
                            : distanceToGround;
                        if (ShouldSleepZPhysics(
                                settledDistanceToGround,
                                stickyGround,
                                zPhys.LocalPosition,
                                zPhys.Velocity,
                                isVehicle))
                        {
                            DebugLogFalling(
                                uid,
                                "sleep",
                                $"settledDistance={settledDistanceToGround:F3} sticky={stickyGround} local={zPhys.LocalPosition:F3}");
                            RemComp<CMUZFallingComponent>(uid);
                        }

                        continue;
                    }
                }
            }
            else if (hasGroundContact &&
                     ShouldSettleNonBouncingGroundContact(isVehicle, zPhys.Velocity))
            {
                var velocityBeforeSettle = zPhys.Velocity;
                zPhys.Velocity = 0f;
                DebugLogFalling(
                    uid,
                    "ground-settle",
                    $"velocityBefore={velocityBeforeSettle:F3} local={zPhys.LocalPosition:F3} sticky={stickyGround}");

                if (zPhys.LocalPosition >= 0f &&
                    zPhys.LocalPosition < 1f &&
                    ShouldSleepZPhysics(0f, stickyGround, zPhys.LocalPosition, zPhys.Velocity, isVehicle))
                {
                    DebugLogFalling(uid, "sleep", $"settledDistance=0.000 sticky={stickyGround} local={zPhys.LocalPosition:F3}");
                    RemComp<CMUZFallingComponent>(uid);
                    DirtyZPhysics(uid, zPhys, oldVelocity, oldHeight);
                    continue;
                }
            }

            if (profiling)
                _profileZMovementBoundaryChecks++;

            DebugLogFalling(uid, "boundary-check", $"local={zPhys.LocalPosition:F3} vel={zPhys.Velocity:F3} sticky={stickyGround}");
            TryProcessZLevelBoundary(uid, zPhys, stickyGround);

            if (Math.Abs(zPhys.Velocity) > ZVelocityLimit)
                zPhys.Velocity = MathF.Sign(zPhys.Velocity) * ZVelocityLimit;

            DirtyZPhysics(uid, zPhys, oldVelocity, oldHeight);
        }
        _zMovementUpdateQueue.Clear();

        if (profiling)
        {
            Prof.WriteValue("CMU Z Movement Entities", processed);
            WriteZMovementProfileCounters();
        }
    }

    private void ResetZMovementProfileCounters()
    {
        _profileZMovementStoppedParent = 0;
        _profileZMovementStoppedNoMap = 0;
        _profileZMovementGroundContacts = 0;
        _profileZMovementBoundaryChecks = 0;
        _profileZMovementLandEvents = 0;
        _profileZDistanceFloors = 0;
        _profileZDistanceHighGroundHits = 0;
        _profileZDistanceTileHits = 0;
        _profileZDistanceMisses = 0;
        _profileZHighGroundTiles = 0;
        _profileZHighGroundAnchoredEntities = 0;
        _profileZHighGroundCandidates = 0;
        _profileZHighGroundAccepted = 0;
        _profileZMoveSnapSweepSamples = 0;
        _profileZMoveSnapSweepHighGroundChecks = 0;
    }

    private void WriteZMovementProfileCounters()
    {
        Prof.WriteValue("CMU Z Movement Stopped Parent", _profileZMovementStoppedParent);
        Prof.WriteValue("CMU Z Movement Stopped No Map", _profileZMovementStoppedNoMap);
        Prof.WriteValue("CMU Z Movement Ground Contacts", _profileZMovementGroundContacts);
        Prof.WriteValue("CMU Z Movement Boundary Checks", _profileZMovementBoundaryChecks);
        Prof.WriteValue("CMU Z Movement Land Events", _profileZMovementLandEvents);
        Prof.WriteValue("CMU Z Distance Floors", _profileZDistanceFloors);
        Prof.WriteValue("CMU Z Distance HighGround Hits", _profileZDistanceHighGroundHits);
        Prof.WriteValue("CMU Z Distance Tile Hits", _profileZDistanceTileHits);
        Prof.WriteValue("CMU Z Distance Misses", _profileZDistanceMisses);
        Prof.WriteValue("CMU Z HighGround Tiles", _profileZHighGroundTiles);
        Prof.WriteValue("CMU Z HighGround Anchored Entities", _profileZHighGroundAnchoredEntities);
        Prof.WriteValue("CMU Z HighGround Candidates", _profileZHighGroundCandidates);
        Prof.WriteValue("CMU Z HighGround Accepted", _profileZHighGroundAccepted);
        Prof.WriteValue("CMU Z Move Snap Sweep Samples", _profileZMoveSnapSweepSamples);
        Prof.WriteValue("CMU Z Move Snap Sweep HighGround Checks", _profileZMoveSnapSweepHighGroundChecks);
    }

    private bool ShouldDebugFalling(EntityUid uid)
    {
        return _debugFalling && HasComp<CMUZFallingComponent>(uid);
    }

    private void DebugLogFalling(EntityUid uid, string stage, string details)
    {
        if (!ShouldDebugFalling(uid))
            return;

        Log.Info($"{FallDebugTag} side={GetDebugNetSide()} stage={stage} ent={uid} pretty=\"{ToPrettyString(uid)}\" {GetDebugFallingLocation(uid)} {details}");
    }

    private string GetDebugNetSide()
    {
        if (_net.IsClient && _net.IsServer)
            return "client+server";

        return _net.IsClient ? "client" : "server";
    }

    private string GetDebugFallingLocation(EntityUid uid)
    {
        if (!_xformQuery.TryComp(uid, out var xform))
            return "xform=missing";

        var world = _transform.GetWorldPosition(xform, _xformQuery);
        if (xform.MapUid is not { } mapUid)
            return $"map=null mapId={xform.MapID} parent={xform.ParentUid} world={world}";

        if (!_gridQuery.TryComp(mapUid, out var grid))
            return $"map={mapUid} mapId={xform.MapID} parent={xform.ParentUid} world={world} grid=missing";

        var tile = _map.WorldToTile(mapUid, grid, world);
        var tileFound = _map.TryGetTileRef(mapUid, grid, tile, out var tileRef);
        var tileEmpty = !tileFound || tileRef.Tile.IsEmpty;
        var tileType = tileFound ? tileRef.Tile.TypeId.ToString() : "none";
        return $"map={mapUid} mapId={xform.MapID} parent={xform.ParentUid} world={world} tile={tile} tileFound={tileFound} tileEmpty={tileEmpty} tileType={tileType}";
    }

    private static bool ShouldSnapToGround(float distanceToGround, bool stickyGround)
    {
        if (stickyGround)
            return true;

        return distanceToGround >= -MaxStepHeight && distanceToGround <= GroundSnapDistance;
    }

    private static bool ShouldTreatAsGroundContact(float distanceToGround, bool stickyGround)
    {
        return ShouldSnapToGround(distanceToGround, stickyGround);
    }

    private static bool ShouldTreatAsFallingGroundContact(float distanceToGround, float velocity)
    {
        return velocity <= 0f &&
               distanceToGround < -MaxStepHeight;
    }

    private static bool ShouldBounceOnGroundContact(float velocity)
    {
        return velocity <= 0f;
    }

    private static bool ShouldSettleNonBouncingGroundContact(bool isVehicle, float velocity)
    {
        return isVehicle && velocity > 0f;
    }

    private static bool ShouldProcessDownBoundary(float localPosition)
    {
        return localPosition < -ZPhysicsSleepDistance;
    }

    private static bool ShouldClampAfterDownTransition(
        float localPosition,
        float distanceToGround,
        bool stickyGround,
        float velocity)
    {
        if (localPosition >= 0f)
            return false;

        if (distanceToGround > GroundSnapDistance)
            return false;

        if (stickyGround)
            return true;

        return ShouldTreatAsGroundContact(distanceToGround, false) ||
               ShouldTreatAsFallingGroundContact(distanceToGround, velocity);
    }

    protected static bool ShouldSleepZPhysics(
        float distanceToGround,
        bool stickyGround,
        float localPosition,
        float velocity,
        bool isVehicle = false)
    {
        if (MathF.Abs(velocity) > MinActiveZVelocity)
            return false;

        if (MathF.Abs(distanceToGround) > ZPhysicsSleepDistance)
            return false;

        return isVehicle ||
               stickyGround ||
               MathF.Abs(localPosition) <= ZPhysicsSleepDistance;
    }

    private static float GetGroundSnapDistance(float distanceToGround, bool stickyGround)
    {
        if (!ShouldSnapToGround(distanceToGround, stickyGround))
            return 0f;

        return distanceToGround;
    }

    private static float GetMoveGroundSnapDistance(float distanceToGround, bool stickyGround)
    {
        return GetGroundSnapDistance(distanceToGround, stickyGround);
    }

    private static bool ShouldProcessMoveGroundSnap(bool isClient, bool applyingState)
    {
        return !isClient || !applyingState;
    }

    private static bool ShouldProcessMoveSnapZLevelTransition(float localPosition, bool stickyGround)
    {
        return stickyGround && (localPosition < 0f ||
            localPosition >= StickyMoveSnapUpTransitionHeight);
    }

    private static bool ShouldAdvanceStickyMoveSnapToUpperBoundary(float localPosition, bool stickyGround)
    {
        return stickyGround &&
            localPosition >= StickyMoveSnapUpTransitionHeight &&
            localPosition < 1f;
    }

    private static bool ShouldProcessImmediateMoveSnapZLevelTransition(
        bool isClient,
        float localPosition,
        bool stickyGround)
    {
        if (!stickyGround)
            return false;

        if (isClient)
            return localPosition >= StickyMoveSnapUpTransitionHeight;

        return ShouldProcessMoveSnapZLevelTransition(localPosition, stickyGround);
    }

    private static bool ShouldUseStickyGround(
        bool isCurrentTile,
        float velocity,
        CMUZLevelHighGroundComponent heightComp)
    {
        return velocity <= 0.01f &&
            velocity > -4f &&
            heightComp.Stick;
    }

    private static bool ShouldUseSweptStickyHighGround(
        CMUZLevelHighGroundComponent heightComp,
        float oldT,
        float newT,
        float velocity)
    {
        if (!heightComp.Stick ||
            velocity > 0.01f ||
            velocity <= -4f ||
            heightComp.HeightCurve.Count <= 1)
        {
            return false;
        }

        var first = heightComp.HeightCurve[0];
        var last = heightComp.HeightCurve[^1];

        if (first > last + 0.01f)
            return newT < oldT;

        if (last > first + 0.01f)
            return newT > oldT;

        return false;
    }

    private static bool ShouldUseSweptStickyMoveSnap(float candidateSnappedLocalPosition, float bestSnappedLocalPosition)
    {
        return candidateSnappedLocalPosition >= StickyMoveSnapUpTransitionHeight &&
            candidateSnappedLocalPosition > bestSnappedLocalPosition + 0.001f;
    }

    private static bool ShouldReplaceHighGroundCandidate(
        bool isCurrentTile,
        float score,
        bool found,
        bool bestIsCurrentTile,
        float bestScore)
    {
        if (!found)
            return true;

        if (isCurrentTile != bestIsCurrentTile)
            return isCurrentTile;

        return score < bestScore;
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

    private void TryProcessZLevelBoundary(EntityUid uid, CMUZPhysicsComponent zPhys, bool stickyGround)
    {
        if (!Prof.IsEnabled)
        {
            TryProcessZLevelBoundaryCore(uid, zPhys, stickyGround);
            return;
        }

        using var profile = Prof.Group("CMU Z Boundary");
        TryProcessZLevelBoundaryCore(uid, zPhys, stickyGround);
    }

    private void TryProcessZLevelBoundaryCore(EntityUid uid, CMUZPhysicsComponent zPhys, bool stickyGround)
    {
        if (zPhys.LocalPosition < 0f &&
            !ShouldProcessDownBoundary(zPhys.LocalPosition))
        {
            DebugLogFalling(
                uid,
                "boundary-down-epsilon",
                $"local={zPhys.LocalPosition:F3} vel={zPhys.Velocity:F3} sticky={stickyGround}");
            return;
        }

        if (zPhys.LocalPosition < 0) //We wanna fall down on ZLevel below
        {
            var canTransitionDown = CanProcessZLevelTransition(uid, -1);
            DebugLogFalling(
                uid,
                "boundary-down-enter",
                $"canTransition={canTransitionDown} local={zPhys.LocalPosition:F3} vel={zPhys.Velocity:F3} sticky={stickyGround}");

            if (canTransitionDown)
            {
                var movedDown = TryMoveDownOrChasm(uid);
                DebugLogFalling(uid, "boundary-down-result", $"moved={movedDown} localBeforeNormalize={zPhys.LocalPosition:F3}");
                if (movedDown)
                {
                    zPhys.LocalPosition += 1;
                    DebugLogFalling(uid, "boundary-down-normalized", $"local={zPhys.LocalPosition:F3} vel={zPhys.Velocity:F3}");

                    if (!stickyGround)
                    {
                        DebugLogFalling(uid, "fall-event", "raised=true");
                        var fallEv = new CMUZLevelFallEvent();
                        RaiseLocalEvent(uid, fallEv);
                    }

                    TryClampToGroundAfterDownTransition(uid, zPhys);
                }
            }
            else
            {
                DebugLogFalling(uid, "boundary-down-denied", $"localBeforeClamp={zPhys.LocalPosition:F3}");
                zPhys.LocalPosition = 0;
            }

            return;
        }

        if (zPhys.LocalPosition < 1)
            return;

        if (HasTileAbove(uid) && !HasCurrentTileHighGround(uid)) //Hit roof
        {
            if (MathF.Abs(zPhys.Velocity) >= ImpactVelocityLimit)
            {
                RaiseLocalEvent(uid, new CMUZLevelHitEvent(MathF.Abs(zPhys.Velocity)));
                var land = new LandEvent(null, true);
                RaiseLocalEvent(uid, ref land);
            }

            zPhys.LocalPosition = 1;
            zPhys.Velocity = -zPhys.Velocity * zPhys.Bounciness;
            return;
        }

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

    private bool TryClampToGroundAfterDownTransition(EntityUid uid, CMUZPhysicsComponent zPhys)
    {
        var distanceToGround = DistanceToGround((uid, zPhys), out var stickyGround);
        var shouldClamp = ShouldClampAfterDownTransition(
                zPhys.LocalPosition,
                distanceToGround,
                stickyGround,
                zPhys.Velocity);
        DebugLogFalling(
            uid,
            "post-down-distance",
            $"local={zPhys.LocalPosition:F3} vel={zPhys.Velocity:F3} distance={distanceToGround:F3} sticky={stickyGround} shouldClamp={shouldClamp}");
        if (!shouldClamp)
        {
            return false;
        }

        var localBefore = zPhys.LocalPosition;
        var velocityBefore = zPhys.Velocity;
        zPhys.LocalPosition -= distanceToGround;
        if (TryComp<PhysicsComponent>(uid, out var physics) &&
            physics.BodyStatus != BodyStatus.OnGround)
        {
            _physics.SetBodyStatus(uid, physics, BodyStatus.OnGround);
        }

        if (MathF.Abs(zPhys.Velocity) >= ImpactVelocityLimit)
        {
            RaiseLocalEvent(uid, new CMUZLevelHitEvent(MathF.Abs(zPhys.Velocity)));
            var land = new LandEvent(null, true);
            RaiseLocalEvent(uid, ref land);
        }

        if (stickyGround)
            zPhys.Velocity = 0f;
        else
            zPhys.Velocity = -zPhys.Velocity * zPhys.Bounciness;

        DebugLogFalling(
            uid,
            "post-down-clamp",
            $"localBefore={localBefore:F3} localAfter={zPhys.LocalPosition:F3} velocityBefore={velocityBefore:F3} velocityAfter={zPhys.Velocity:F3} distance={distanceToGround:F3} sticky={stickyGround}");

        if (MathF.Abs(zPhys.Velocity) <= MinActiveZVelocity)
        {
            zPhys.Velocity = 0f;
            if (ShouldSleepZPhysics(
                    0f,
                    stickyGround,
                    zPhys.LocalPosition,
                    zPhys.Velocity,
                    HasComp<CMUVehicleZTraversalComponent>(uid)))
            {
                DebugLogFalling(uid, "post-down-sleep", $"local={zPhys.LocalPosition:F3} sticky={stickyGround}");
                RemComp<CMUZFallingComponent>(uid);
            }
        }

        return true;
    }

    private bool HasCurrentTileHighGround(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.MapUid is not { } mapUid ||
            !_zMapQuery.TryComp(mapUid, out _) ||
            !_gridQuery.TryComp(mapUid, out var mapGrid))
        {
            return false;
        }

        var worldPosI = _transform.GetGridOrMapTilePosition(uid);
        var queryHigh = _map.GetAnchoredEntitiesEnumerator(mapUid, mapGrid, worldPosI);
        while (queryHigh.MoveNext(out var anchoredUid))
        {
            if (_highgroundQuery.HasComp(anchoredUid))
                return true;
        }

        return false;
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
        if (!Prof.IsEnabled)
            return DistanceToGroundCore(target, out stickyGround, maxFloors);

        using var profile = Prof.Group("CMU Z DistanceToGround");
        return DistanceToGroundCore(target, out stickyGround, maxFloors);
    }

    private float DistanceToGroundCore(Entity<CMUZPhysicsComponent?> target, out bool stickyGround, int maxFloors)
    {
        if (!Resolve(target, ref target.Comp, false))
        {
            stickyGround = false;
            return 0; //maybe in future: simpler distance calculation for entities without zPhysComp?
        }

        var xform = Transform(target);
        if (_vehicleTraversalQuery.TryComp(target, out var vehicleTraversal) &&
            _fixturesQuery.TryComp(target, out var fixtures) &&
            TryGetVehicleFootprintDistanceToGround(
                target,
                xform,
                vehicleTraversal,
                fixtures,
                out var vehicleDistance,
                out stickyGround,
                maxFloors))
        {
            return vehicleDistance;
        }

        var worldPos = _transform.GetWorldPosition(xform);
        return DistanceToGroundAtWorldPositionCore(target, xform.MapUid, worldPos, out stickyGround, maxFloors);
    }

    private bool TryGetVehicleFootprintDistanceToGround(
        Entity<CMUZPhysicsComponent?> target,
        TransformComponent xform,
        CMUVehicleZTraversalComponent vehicleTraversal,
        FixturesComponent fixtures,
        out float distance,
        out bool stickyGround,
        int maxFloors)
    {
        distance = 0f;
        stickyGround = false;

        if (xform.MapUid == null ||
            !CMUVehicleSupportFootprint.TryGetFixtureLocalAabb(fixtures, out var localBounds))
        {
            return false;
        }

        CMUVehicleSupportFootprint.GenerateWorldSamples(
            localBounds,
            vehicleTraversal.SupportSampleSpacing,
            vehicleTraversal.SupportSampleInset,
            _transform.GetWorldPosition(xform),
            _transform.GetWorldRotation(xform),
            _vehicleSupportSamples);

        var falling = HasComp<CMUZFallingComponent>(target);
        var supported = 0;
        var supportedDistance = 0f;
        var highestSupportedSurfaceDistance = float.MaxValue;
        foreach (var sample in _vehicleSupportSamples)
        {
            var sampleDistance = DistanceToGroundAtWorldPositionCore(target, xform.MapUid, sample, out var sampleStickyGround, maxFloors);
            if (!CMUVehicleSupportFootprint.IsSampleSupported(
                    sampleDistance,
                    sampleStickyGround,
                    vehicleTraversal.MaxSupportStepHeight,
                    vehicleTraversal.SupportSnapDistance,
                    falling))
            {
                continue;
            }

            supported++;
            supportedDistance += sampleDistance;
            highestSupportedSurfaceDistance = MathF.Min(highestSupportedSurfaceDistance, sampleDistance);
            stickyGround |= sampleStickyGround;
        }

        if (CMUVehicleSupportFootprint.ShouldRejectSupport(
                supported,
                _vehicleSupportSamples.Count,
                vehicleTraversal.EdgeTipUnsupportedFraction,
                falling))
        {
            DebugLogFalling(
                target.Owner,
                "vehicle-support-reject",
                $"falling={falling} supported={supported}/{_vehicleSupportSamples.Count} edgeTip={vehicleTraversal.EdgeTipUnsupportedFraction:F3} sticky={stickyGround}");
            stickyGround = false;
            distance = maxFloors;
            return true;
        }

        distance = CMUVehicleSupportFootprint.GetSupportSnapDistance(
            supportedDistance,
            supported,
            highestSupportedSurfaceDistance,
            stickyGround);
        DebugLogFalling(
            target.Owner,
            "vehicle-support",
            $"falling={falling} supported={supported}/{_vehicleSupportSamples.Count} sticky={stickyGround} distance={distance:F3} highest={highestSupportedSurfaceDistance:F3}");
        return true;
    }

    /// <summary>
    /// Returns the distance to ground for an arbitrary world-space sample owned by a Z physics entity.
    /// </summary>
    [PublicAPI]
    public float DistanceToGroundAtWorldPosition(
        Entity<CMUZPhysicsComponent?> target,
        Vector2 worldPosition,
        out bool stickyGround,
        int maxFloors = 1)
    {
        if (!Prof.IsEnabled)
            return DistanceToGroundAtWorldPositionCore(target, Transform(target).MapUid, worldPosition, out stickyGround, maxFloors);

        using var profile = Prof.Group("CMU Z DistanceToGroundAtWorldPosition");
        return DistanceToGroundAtWorldPositionCore(target, Transform(target).MapUid, worldPosition, out stickyGround, maxFloors);
    }

    private float DistanceToGroundAtWorldPositionCore(
        Entity<CMUZPhysicsComponent?> target,
        EntityUid? mapUid,
        Vector2 worldPos,
        out bool stickyGround,
        int maxFloors)
    {
        stickyGround = false;
        if (!Resolve(target, ref target.Comp, false))
            return 0; //maybe in future: simpler distance calculation for entities without zPhysComp?

        if (mapUid is not { } resolvedMap ||
            !_zMapQuery.TryComp(resolvedMap, out var zMapComp))
        {
            DebugLogFalling(target.Owner, "distance-no-z-map", $"inputMap={mapUid} sampleWorld={worldPos}");
            return 0;
        }
        if (!_gridQuery.TryComp(resolvedMap, out var mapGrid))
        {
            DebugLogFalling(target.Owner, "distance-no-grid", $"inputMap={resolvedMap} sampleWorld={worldPos}");
            return 0;
        }

        //Select current map by default
        Entity<CMUZLevelMapComponent> checkingMap = (resolvedMap, zMapComp);
        MapGridComponent checkingGrid = mapGrid;
        var profiling = Prof.IsEnabled;

        for (var floor = 0; floor <= maxFloors; floor++)
        {
            if (profiling)
                _profileZDistanceFloors++;

            if (floor != 0) //Select map below
            {
                if (!TryMapDown((checkingMap.Owner, checkingMap.Comp), out var tempCheckingMap))
                {
                    DebugLogFalling(
                        target.Owner,
                        "distance-map-down-missing",
                        $"floor={floor} checkingMap={checkingMap.Owner} sampleWorld={worldPos}");
                    break;
                }

                checkingMap = tempCheckingMap.Value;
                if (!_gridQuery.TryComp(checkingMap.Owner, out var tempCheckingGrid))
                {
                    DebugLogFalling(
                        target.Owner,
                        "distance-map-down-no-grid",
                        $"floor={floor} checkingMap={checkingMap.Owner} sampleWorld={worldPos}");
                    continue;
                }

                checkingGrid = tempCheckingGrid;
            }

            var checkingTile = _map.WorldToTile(checkingMap, checkingGrid, worldPos);

            if (TryGetHighGroundDistance(target, checkingMap, checkingGrid, checkingTile, worldPos, floor, out var highGroundDistance, ref stickyGround))
            {
                if (profiling)
                    _profileZDistanceHighGroundHits++;

                DebugLogFalling(
                    target.Owner,
                    "distance-highground-hit",
                    $"floor={floor} checkingMap={checkingMap.Owner} tile={checkingTile} sampleWorld={worldPos} distance={highGroundDistance:F3} sticky={stickyGround}");
                return highGroundDistance;
            }

            //No ZEntities found, check floor tiles
            var tileFound = _map.TryGetTileRef(checkingMap, checkingGrid, checkingTile, out var tileRef);
            var tileEmpty = !tileFound || tileRef.Tile.IsEmpty;
            var tileType = tileFound ? tileRef.Tile.TypeId.ToString() : "none";
            if (tileFound &&
                !tileEmpty)
            {
                if (profiling)
                    _profileZDistanceTileHits++;

                DebugLogFalling(
                    target.Owner,
                    "distance-tile-hit",
                    $"floor={floor} checkingMap={checkingMap.Owner} tile={checkingTile} sampleWorld={worldPos} tileType={tileType} distance={target.Comp.LocalPosition + floor:F3}");
                return target.Comp.LocalPosition + floor;
            }

            DebugLogFalling(
                target.Owner,
                "distance-tile-miss",
                $"floor={floor} checkingMap={checkingMap.Owner} tile={checkingTile} sampleWorld={worldPos} tileFound={tileFound} tileEmpty={tileEmpty} tileType={tileType} local={target.Comp.LocalPosition:F3}");
        }

        if (profiling)
            _profileZDistanceMisses++;

        DebugLogFalling(target.Owner, "distance-miss", $"sampleWorld={worldPos} maxFloors={maxFloors}");
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
        if (!Prof.IsEnabled)
            return TryGetHighGroundDistanceCore(
                target,
                checkingMap,
                checkingGrid,
                checkingTile,
                worldPos,
                floor,
                out distance,
                ref stickyGround);

        using var profile = Prof.Group("CMU Z HighGroundDistance");
        return TryGetHighGroundDistanceCore(
            target,
            checkingMap,
            checkingGrid,
            checkingTile,
            worldPos,
            floor,
            out distance,
            ref stickyGround);
    }

    private bool TryGetHighGroundDistanceCore(
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
        var bestIsCurrentTile = false;
        var gridLocal = _map.WorldToLocal(checkingMap, checkingGrid, worldPos) / checkingGrid.TileSize;
        var profiling = Prof.IsEnabled;

        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                if (profiling)
                    _profileZHighGroundTiles++;

                var tile = checkingTile + new Vector2i(x, y);
                var isCurrentTile = x == 0 && y == 0;
                var query = _map.GetAnchoredEntitiesEnumerator(checkingMap, checkingGrid, tile);

                while (query.MoveNext(out var uid))
                {
                    if (profiling)
                        _profileZHighGroundAnchoredEntities++;

                    if (!_highgroundQuery.TryComp(uid, out var heightComp))
                        continue;

                    if (floor == 0 && heightComp.SupportOnlyFromAbove)
                        continue;

                    if (heightComp.HeightCurve.Count == 0)
                        continue;

                    var local = gridLocal - new Vector2(tile.X, tile.Y);
                    if (!TryGetHighGroundCurveT(uid.Value, heightComp, local, isCurrentTile, out var t))
                        continue;

                    if (profiling)
                        _profileZHighGroundCandidates++;

                    var candidateDistance = GetHighGroundDistance(target.Comp!, heightComp, t, floor);
                    var score = MathF.Abs(candidateDistance);

                    if (!ShouldReplaceHighGroundCandidate(isCurrentTile, score, found, bestIsCurrentTile, bestScore))
                        continue;

                    if (profiling)
                        _profileZHighGroundAccepted++;

                    found = true;
                    bestScore = score;
                    bestIsCurrentTile = isCurrentTile;
                    bestDistance = candidateDistance;
                    bestSticky = ShouldUseStickyGround(isCurrentTile, target.Comp!.Velocity, heightComp);
                }
            }
        }

        if (!found)
            return false;

        distance = bestDistance;
        stickyGround = bestSticky;
        return true;
    }

    private bool TryGetSweptStickyMoveGroundSnapDistance(
        Entity<CMUZPhysicsComponent> target,
        MoveEvent args,
        float currentDistanceToGround,
        bool currentStickyGround,
        out float distance)
    {
        if (!Prof.IsEnabled)
            return TryGetSweptStickyMoveGroundSnapDistanceCore(
                target,
                args,
                currentDistanceToGround,
                currentStickyGround,
                out distance);

        using var profile = Prof.Group("CMU Z Move Snap Sweep");
        return TryGetSweptStickyMoveGroundSnapDistanceCore(
            target,
            args,
            currentDistanceToGround,
            currentStickyGround,
            out distance);
    }

    private bool TryGetSweptStickyMoveGroundSnapDistanceCore(
        Entity<CMUZPhysicsComponent> target,
        MoveEvent args,
        float currentDistanceToGround,
        bool currentStickyGround,
        out float distance)
    {
        distance = 0f;

        var oldMapCoordinates = _transform.ToMapCoordinates(args.OldPosition, false);
        var newMapCoordinates = _transform.ToMapCoordinates(args.NewPosition, false);
        if (oldMapCoordinates.MapId == MapId.Nullspace ||
            newMapCoordinates.MapId == MapId.Nullspace ||
            oldMapCoordinates.MapId != newMapCoordinates.MapId)
        {
            return false;
        }

        var delta = newMapCoordinates.Position - oldMapCoordinates.Position;
        var moveDistance = delta.Length();
        if (moveDistance <= 0.001f)
            return false;

        var xform = Transform(target);
        if (xform.MapID != newMapCoordinates.MapId ||
            xform.MapUid is not { } mapUid ||
            !_zMapQuery.TryComp(mapUid, out var zMapComp) ||
            !_gridQuery.TryComp(mapUid, out var mapGrid))
        {
            return false;
        }

        var currentGroundSnapDistance = GetMoveGroundSnapDistance(currentDistanceToGround, currentStickyGround);
        var bestSnappedLocalPosition = target.Comp.LocalPosition - currentGroundSnapDistance;
        if (ShouldProcessImmediateMoveSnapZLevelTransition(_net.IsClient, bestSnappedLocalPosition, currentStickyGround))
            return false;

        var sampleCount = Math.Clamp(
            (int)MathF.Ceiling(moveDistance / MoveGroundSnapSweepStep),
            1,
            MaxMoveGroundSnapSweepSamples);
        if (Prof.IsEnabled)
            _profileZMoveSnapSweepSamples += Math.Max(0, sampleCount - 1);

        var found = false;
        var bestDistance = 0f;
        Entity<CMUZLevelMapComponent> checkingMap = (mapUid, zMapComp);

        for (var i = 1; i < sampleCount; i++)
        {
            var t = i / (float) sampleCount;
            var sampleWorldPosition = Vector2.Lerp(oldMapCoordinates.Position, newMapCoordinates.Position, t);

            if (!TryGetSweptStickyHighGroundDistance(
                    target,
                    checkingMap,
                    mapGrid,
                    oldMapCoordinates.Position,
                    newMapCoordinates.Position,
                    sampleWorldPosition,
                    bestSnappedLocalPosition,
                    out var candidateDistance,
                    out var candidateSnappedLocalPosition))
            {
                continue;
            }

            found = true;
            bestDistance = candidateDistance;
            bestSnappedLocalPosition = candidateSnappedLocalPosition;
        }

        if (!found)
            return false;

        distance = bestDistance;
        return true;
    }

    private bool TryGetSweptStickyHighGroundDistance(
        Entity<CMUZPhysicsComponent> target,
        Entity<CMUZLevelMapComponent> checkingMap,
        MapGridComponent checkingGrid,
        Vector2 oldWorldPosition,
        Vector2 newWorldPosition,
        Vector2 sampleWorldPosition,
        float bestSnappedLocalPosition,
        out float distance,
        out float snappedLocalPosition)
    {
        if (Prof.IsEnabled)
            _profileZMoveSnapSweepHighGroundChecks++;

        distance = 0f;
        snappedLocalPosition = 0f;

        var checkingTile = _map.WorldToTile(checkingMap, checkingGrid, sampleWorldPosition);
        var oldGridLocal = _map.WorldToLocal(checkingMap, checkingGrid, oldWorldPosition) / checkingGrid.TileSize;
        var newGridLocal = _map.WorldToLocal(checkingMap, checkingGrid, newWorldPosition) / checkingGrid.TileSize;
        var sampleGridLocal = _map.WorldToLocal(checkingMap, checkingGrid, sampleWorldPosition) / checkingGrid.TileSize;

        var found = false;
        var bestDistance = 0f;
        var bestLocalPosition = 0f;

        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                var tile = checkingTile + new Vector2i(x, y);
                var isCurrentTile = x == 0 && y == 0;
                var tileOrigin = new Vector2(tile.X, tile.Y);
                var query = _map.GetAnchoredEntitiesEnumerator(checkingMap, checkingGrid, tile);

                while (query.MoveNext(out var uid))
                {
                    if (!_highgroundQuery.TryComp(uid, out var heightComp))
                        continue;

                    if (heightComp.SupportOnlyFromAbove ||
                        heightComp.HeightCurve.Count == 0)
                    {
                        continue;
                    }

                    var sampleLocal = sampleGridLocal - tileOrigin;
                    if (!TryGetHighGroundCurveT(uid.Value, heightComp, sampleLocal, isCurrentTile, out var sampleT))
                        continue;

                    var oldT = GetHighGroundCurveT(uid.Value, heightComp, oldGridLocal - tileOrigin);
                    var newT = GetHighGroundCurveT(uid.Value, heightComp, newGridLocal - tileOrigin);
                    if (!ShouldUseSweptStickyHighGround(heightComp, oldT, newT, target.Comp.Velocity))
                        continue;

                    var candidateDistance = GetHighGroundDistance(target.Comp, heightComp, sampleT, 0);
                    var candidateSnappedLocalPosition = target.Comp.LocalPosition - candidateDistance;
                    if (!ShouldUseSweptStickyMoveSnap(candidateSnappedLocalPosition, bestSnappedLocalPosition) ||
                        (found && candidateSnappedLocalPosition <= bestLocalPosition + 0.001f))
                    {
                        continue;
                    }

                    found = true;
                    bestDistance = candidateDistance;
                    bestLocalPosition = candidateSnappedLocalPosition;
                }
            }
        }

        if (!found)
            return false;

        distance = bestDistance;
        snappedLocalPosition = bestLocalPosition;
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
        if (!Prof.IsEnabled)
            return TryMoveCore(ent, offset, map, worldPosition);

        using var profile = Prof.Group("CMU Z TryMove");
        return TryMoveCore(ent, offset, map, worldPosition);
    }

    private bool TryMoveCore(EntityUid ent, int offset, Entity<CMUZLevelMapComponent?>? map, Vector2? worldPosition)
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
                _pulling.TryDetachPullJointForTransfer(ent, pulled, puller, pullable);
                SetMapCoordinatesWithoutMoveSnap(ent, target);
                RaiseLocalEvent(ent, new CMUZLevelMoveEvent(offset));
                MoveFiremanCarriedTarget(ent, target, offset);
                QueuePullJointRefresh(ent, pulled);
                return;
            }

            if (TryComp(pulled, out PullerComponent? otherPullingPuller) &&
                TryComp(otherPullingPuller.Pulling, out PullableComponent? otherPullingPullable))
            {
                _pulling.TryStopPull(otherPullingPuller.Pulling.Value, otherPullingPullable, pulled);
            }

            _pulling.TryDetachPullJointForTransfer(ent, pulled, puller, pullable);
            SetMapCoordinatesWithoutMoveSnap(ent, target);
            SetMapCoordinatesWithoutMoveSnap(pulled, target);
            QueuePullJointRefresh(ent, pulled);

            RaiseLocalEvent(ent, new CMUZLevelMoveEvent(offset));
            RaiseLocalEvent(pulled, new CMUZLevelMoveEvent(offset));
            return;
        }

        SetMapCoordinatesWithoutMoveSnap(ent, target);
        RaiseLocalEvent(ent, new CMUZLevelMoveEvent(offset));
        MoveFiremanCarriedTarget(ent, target, offset);
    }

    private void SetMapCoordinatesWithoutMoveSnap(EntityUid uid, MapCoordinates target)
    {
        var added = _moveSnapSuppressed.Add(uid);
        try
        {
            _transform.SetMapCoordinates(uid, target);
        }
        finally
        {
            if (added)
                _moveSnapSuppressed.Remove(uid);
        }
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
            SetMapCoordinatesWithoutMoveSnap(carried, target);

        RaiseLocalEvent(carried, new CMUZLevelMoveEvent(offset));
    }

    private void QueuePullJointRefresh(EntityUid puller, EntityUid pulled)
    {
        if (TerminatingOrDeleted(puller) ||
            TerminatingOrDeleted(pulled))
        {
            return;
        }

        _deferredPullJointRefreshes.Add((puller, pulled));
    }

    private void FlushDeferredPullJointRefreshes()
    {
        if (_deferredPullJointRefreshes.Count == 0)
            return;

        _deferredPullJointRefreshBuffer.Clear();
        _deferredPullJointRefreshBuffer.AddRange(_deferredPullJointRefreshes);
        _deferredPullJointRefreshes.Clear();

        foreach (var (puller, pulled) in _deferredPullJointRefreshBuffer)
        {
            if (TerminatingOrDeleted(puller) ||
                TerminatingOrDeleted(pulled) ||
                !TryComp<PullerComponent>(puller, out var pullerComp) ||
                !TryComp<PullableComponent>(pulled, out var pullableComp))
            {
                continue;
            }

            if (pullerComp.Pulling == pulled &&
                pullableComp.Puller == puller)
            {
                _pulling.TryRefreshPullJointForTransfer(puller, pulled, pullerComp, pullableComp);
            }
        }

        _deferredPullJointRefreshBuffer.Clear();
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
        {
            DebugLogFalling(ent, "move-down-success", "result=z-map");
            return true;
        }

        //welp, that default Chasm behavior. Not really good, but ok for now.
        if (HasComp<ChasmFallingComponent>(ent))
        {
            DebugLogFalling(ent, "move-down-chasm-existing", "result=false");
            return false; //Already falling
        }

        var audio = new SoundPathSpecifier("/Audio/Effects/falling.ogg");
        _audio.PlayPredicted(audio, Transform(ent).Coordinates, ent);
        var falling = AddComp<ChasmFallingComponent>(ent);
        falling.NextDeletionTime = _timing.CurTime + falling.DeletionTime;
        _blocker.UpdateCanMove(ent);
        DebugLogFalling(ent, "move-down-chasm-start", $"deleteAt={falling.NextDeletionTime}");

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
