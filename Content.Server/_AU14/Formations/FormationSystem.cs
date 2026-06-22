using System.Linq;
using System.Numerics;
using Content.Server.Actions;
using Content.Shared._AU14.Formations;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Stunnable;
using Content.Shared.Maps;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._AU14.Formations;

public sealed partial class FormationSystem : EntitySystem
{
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    private static readonly float[] HuePalette =
    {
        0f, 30f, 60f, 90f, 120f, 150f, 180f, 210f, 240f, 270f, 300f, 330f
    };

    private const int BfsMaxSteps = 5;
    private const float DotDefaultLifetime = 120f;   // 2-minute slotting window
    private const float DotExtendedLifetime = 900f;  // 15-minute extended window (use sparingly)
    private const float DotReturnLifetime = 30f;     // 30-second re-slot window after unslot
    private const float FormationFollowSpeed = 5f;   // tiles/second for smooth movement

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AU14FormationLeaderComponent, ComponentInit>(OnLeaderInit);
        SubscribeLocalEvent<AU14FormationLeaderComponent, ComponentShutdown>(OnLeaderShutdown);

        SubscribeLocalEvent<AU14FormationLeaderComponent, AU14FormationMenuActionEvent>(OnFormationAction);

        SubscribeLocalEvent<FormationDotComponent, InteractHandEvent>(OnDotInteract);
        SubscribeLocalEvent<FormationDotComponent, ComponentShutdown>(OnDotShutdown);

        SubscribeLocalEvent<AU14FormationLeaderComponent, MoveEvent>(OnLeaderMoved);
        SubscribeLocalEvent<FormationSlottedComponent, MoveEvent>(OnFollowerMoved);

        SubscribeLocalEvent<FormationSlottedComponent, ComponentShutdown>(OnFollowerShutdown);
        SubscribeLocalEvent<FormationSlottedComponent, MobStateChangedEvent>(OnFollowerMobStateChanged);

        Subs.BuiEvents<AU14FormationLeaderComponent>(FormationMenuUiKey.Key, subs =>
        {
            subs.Event<FormationEnterPlacementMsg>(OnEnterPlacement);
            subs.Event<FormationPlaceDotMsg>(OnPlaceDot);
            subs.Event<FormationUndoLastDotMsg>(OnUndoLastDot);
            subs.Event<FormationConfirmMsg>(OnConfirm);
            subs.Event<FormationCancelPlanningMsg>(OnCancelPlanning);
            subs.Event<FormationClearMsg>(OnClear);
            subs.Event<FormationDisbandMsg>(OnDisband);
            subs.Event<FormationFreezeToggleMsg>(OnFreezeToggle);
            subs.Event<FormationDebugToggleMsg>(OnDebugToggle);
            subs.Event<FormationSetFollowModeMsg>(OnSetFollowMode);
            subs.Event<FormationCollisionToggleMsg>(OnCollisionToggle);
            subs.Event<FormationDotLifetimeToggleMsg>(OnDotLifetimeToggle);
        });
    }

    private void OnFormationAction(Entity<AU14FormationLeaderComponent> ent, ref AU14FormationMenuActionEvent args)
    {
        SendBuiState(ent);
        _ui.OpenUi(ent.Owner, FormationMenuUiKey.Key, args.Performer);
        args.Handled = true;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnLeaderInit(Entity<AU14FormationLeaderComponent> ent, ref ComponentInit args)
    {
        var hue = HuePalette[_random.Next(HuePalette.Length)];
        ent.Comp.FormationColor = Color.FromHsv(new Vector4(hue / 360f, 0.85f, 1f, 1f));

        _actions.AddAction(ent.Owner, ref ent.Comp.ActionUid, ent.Comp.ActionPrototype);

        ent.Comp.LastFacing = Transform(ent.Owner).LocalRotation.GetDir();
    }

    private void OnLeaderShutdown(Entity<AU14FormationLeaderComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionUid);
        ClearFormation(ent, disbandFollowers: true);
    }

    // ── Dot interaction ────────────────────────────────────────────────────────

    private void OnDotInteract(Entity<FormationDotComponent> dot, ref InteractHandEvent args)
    {
        if (args.Handled) return;
        var user = args.User;

        if (dot.Comp.IsLeaderDot && !HasComp<AU14FormationLeaderComponent>(user))
        {
            _popup.PopupEntity("This position is reserved for formation leaders.", dot, user, PopupType.Small);
            args.Handled = true;
            return;
        }

        // SlottedEntity acts as an immediate claim flag — prevents two players slotting before QueueDel resolves.
        if (dot.Comp.SlottedEntity.HasValue)
        {
            _popup.PopupEntity("This slot is already occupied.", dot, user, PopupType.Small);
            args.Handled = true;
            return;
        }

        if (HasComp<FormationSlottedComponent>(user))
        {
            _popup.PopupEntity("You are already slotted into a formation.", dot, user, PopupType.Small);
            args.Handled = true;
            return;
        }

        if (!TryComp<AU14FormationLeaderComponent>(dot.Comp.OwnerLeader, out _))
        {
            args.Handled = true;
            return;
        }

        SlotIntoFormation(user, dot);
        args.Handled = true;
    }

    private void OnDotShutdown(Entity<FormationDotComponent> dot, ref ComponentShutdown args)
    {
        // Dots are deleted immediately when someone slots in, so shutdown just cleans the leader list.
        if (TryComp<AU14FormationLeaderComponent>(dot.Comp.OwnerLeader, out var leader))
            leader.PlacedDots.Remove(dot);
    }

    // ── Slotting ──────────────────────────────────────────────────────────────

    private void SlotIntoFormation(EntityUid user, Entity<FormationDotComponent> dot)
    {
        var leaderXform = Transform(dot.Comp.OwnerLeader);

        // Teleport user to dot centre so their offset is exact.
        _transform.SetCoordinates(user, Transform(dot).Coordinates);

        var userXform = Transform(user);
        var userMapCoords = _transform.GetMapCoordinates(user, xform: userXform);
        var leaderMapCoords = _transform.GetMapCoordinates(dot.Comp.OwnerLeader, xform: leaderXform);
        var worldOffset = userMapCoords.Position - leaderMapCoords.Position;
        var localOffset = WorldToLeaderLocal(worldOffset, leaderXform.LocalRotation);

        _transform.SetLocalRotation(user, dot.Comp.FacingDirection.ToAngle());

        // Claim the dot immediately so a second click in the same frame sees it occupied.
        dot.Comp.SlottedEntity = user;
        Dirty(dot);

        var slotted = EnsureComp<FormationSlottedComponent>(user);
        slotted.LeaderUid = dot.Comp.OwnerLeader;
        slotted.LocalOffset = localOffset;
        slotted.IsLeaderDot = dot.Comp.IsLeaderDot;
        slotted.IsBeingForceMoved = false;
        slotted.JoinStunActive = true;
        slotted.PathQueue.Clear();
        slotted.SmoothTargetTile = null;

        if (TryComp<AU14FormationLeaderComponent>(dot.Comp.OwnerLeader, out var leader))
        {
            if (!leader.ActiveFollowers.Contains(user))
            {
                leader.ActiveFollowers.Add(user);
                // Only suppress collisions when the leader has that setting active.
                if (leader.CollisionsDisabled)
                {
                    EnsureComp<FormationMemberComponent>(user);
                    EnsureComp<FormationMemberComponent>(dot.Comp.OwnerLeader);
                }
            }
            SendBuiState((dot.Comp.OwnerLeader, leader));
        }

        _stun.TryStun(user, TimeSpan.FromSeconds(1), true);
        _stunFreezeExpiry[user] = _timing.CurTime + TimeSpan.FromSeconds(1.1);

        // Delete the slotting dot — formation state now lives in FormationSlottedComponent.
        if (!TerminatingOrDeleted(dot))
            QueueDel(dot);
    }

    private readonly Dictionary<EntityUid, TimeSpan> _stunFreezeExpiry = new();

    // ── Unslotting ────────────────────────────────────────────────────────────

    private void ForceUnslot(EntityUid follower, bool returnDot)
    {
        if (!TryComp<FormationSlottedComponent>(follower, out var slotted)) return;

        var leaderUid = slotted.LeaderUid;
        var isLeaderDot = slotted.IsLeaderDot;
        // Capture before RemComp so we can compute the original slot position for the return dot.
        var localOffset = slotted.LocalOffset;

        RemComp<FormationSlottedComponent>(follower);
        _stunFreezeExpiry.Remove(follower);

        TryComp<AU14FormationLeaderComponent>(leaderUid, out var leader);
        if (leader != null)
        {
            leader.ActiveFollowers.Remove(follower);
            // Remove no-collide from leader when last follower leaves.
            if (leader.ActiveFollowers.Count == 0)
                RemComp<FormationMemberComponent>(leaderUid);
            // Remove this follower's debug indicator if it exists.
            if (leader.DebugDots.TryGetValue(follower, out var debugDot))
            {
                if (!TerminatingOrDeleted(debugDot)) QueueDel(debugDot);
                leader.DebugDots.Remove(follower);
            }
            SendBuiState((leaderUid, leader));
        }

        if (!returnDot || leader == null) return;

        // Spawn the return dot at the follower's ORIGINAL formation slot position (relative to the
        // leader's current tile), not at where they physically are now. This preserves the correct
        // offset when they re-slot.
        var leaderXform = Transform(leaderUid);
        if (leaderXform.GridUid is not { } gridUid) return;
        if (!TryComp<MapGridComponent>(gridUid, out var gridComp)) return;

        var leaderTile = _map.TileIndicesFor(gridUid, gridComp, leaderXform.Coordinates);
        var returnFacing = leaderXform.LocalRotation.GetDir();
        var tile = LeaderLocalToWorld(leaderTile, returnFacing, localOffset);

        var returnDotUid = SpawnDotEntity(tile, returnFacing, isLeaderDot, leader.FormationColor,
            leaderUid, TimeSpan.FromSeconds(DotReturnLifetime));

        // Make the return dot dynamic so it follows the formation slot each tick.
        if (TryComp<FormationDotComponent>(returnDotUid, out var returnDotComp))
        {
            returnDotComp.IsDynamicSlot = true;
            returnDotComp.SlotLocalOffset = localOffset;
        }

        leader.PlacedDots.Add(returnDotUid);
    }

    // ── Movement ──────────────────────────────────────────────────────────────

    private void OnLeaderMoved(Entity<AU14FormationLeaderComponent> ent, ref MoveEvent args)
    {
        if (ent.Comp.FormationFrozen) return;
        var xform = Transform(ent.Owner);
        if (xform.GridUid is not { } gridUid) return;
        if (!TryComp<MapGridComponent>(gridUid, out var gridComp)) return;

        var newTile = _map.TileIndicesFor(gridUid, gridComp, xform.Coordinates);
        var newFacing = xform.LocalRotation.GetDir();

        // Echo on tile change OR facing change so the formation reshapes on in-place turns.
        if (newTile == ent.Comp.LastTilePos && newFacing == ent.Comp.LastFacing) return;

        ent.Comp.LastTilePos = newTile;
        ent.Comp.LastFacing = newFacing;

        EchoMovementToFollowers(ent, gridUid, gridComp, newTile, newFacing);
    }

    private void EchoMovementToFollowers(
        Entity<AU14FormationLeaderComponent> leader,
        EntityUid gridUid,
        MapGridComponent gridComp,
        Vector2i leaderTile,
        Direction leaderFacing)
    {
        // Snapshot the list — MoveFollowerStep may call ForceUnslot which removes from ActiveFollowers.
        foreach (var follower in leader.Comp.ActiveFollowers.ToList())
        {
            if (!TryComp<FormationSlottedComponent>(follower, out var slotted)) continue;
            if (slotted.IsLeaderDot) continue; // leader's own position is where they are

            MoveFollowerStep(follower, slotted, leader, gridUid, gridComp, leaderTile, leaderFacing);
        }
    }

    private void MoveFollowerStep(
        EntityUid follower,
        FormationSlottedComponent slotted,
        Entity<AU14FormationLeaderComponent> leader,
        EntityUid gridUid,
        MapGridComponent gridComp,
        Vector2i leaderTile,
        Direction leaderFacing)
    {
        var xform = Transform(follower);
        var targetTile = LeaderLocalToWorld(leaderTile, leaderFacing, slotted.LocalOffset);
        var currentTile = _map.TileIndicesFor(gridUid, gridComp, xform.Coordinates);

        if (currentTile == targetTile)
        {
            // Already in position — just sync facing.
            slotted.IsBeingForceMoved = true;
            _transform.SetLocalRotation(follower, leaderFacing.ToAngle());
            slotted.IsBeingForceMoved = false;
            slotted.SmoothTargetTile = null;
            return;
        }

        Vector2i nextStep;
        if (slotted.PathQueue.Count > 0)
        {
            nextStep = slotted.PathQueue.Dequeue();
            if (!IsStepTowardTarget(nextStep, currentTile, targetTile))
            {
                slotted.PathQueue.Clear();
                nextStep = PlanNextStep(follower, currentTile, targetTile, gridUid, gridComp, slotted);
            }
        }
        else
        {
            nextStep = PlanNextStep(follower, currentTile, targetTile, gridUid, gridComp, slotted);
        }

        if (nextStep == currentTile)
        {
            ForceUnslot(follower, returnDot: true);
            _popup.PopupEntity("Formation path blocked — stepping out.", follower, follower, PopupType.SmallCaution);
            return;
        }

        // Queue smooth movement target; Update() handles the incremental movement.
        slotted.SmoothTargetTile = nextStep;
        slotted.SmoothTargetFacing = leaderFacing;
    }

    private Vector2i PlanNextStep(
        EntityUid follower,
        Vector2i from,
        Vector2i to,
        EntityUid gridUid,
        MapGridComponent gridComp,
        FormationSlottedComponent slotted)
    {
        var directDir = GetStepDirection(from, to);
        var directNext = from + directDir;
        if (IsTilePassable(directNext, gridUid, gridComp, follower))
            return directNext;

        var path = BfsPath(from, to, gridUid, gridComp, follower, BfsMaxSteps);
        if (path == null || path.Count == 0) return from;

        for (var i = 2; i < path.Count; i++)
            slotted.PathQueue.Enqueue(path[i]);

        return path[1];
    }

    private static Vector2i GetStepDirection(Vector2i from, Vector2i to)
    {
        var delta = to - from;
        if (Math.Abs(delta.X) >= Math.Abs(delta.Y))
            return new Vector2i(Math.Sign(delta.X), 0);
        return new Vector2i(0, Math.Sign(delta.Y));
    }

    private static bool IsStepTowardTarget(Vector2i step, Vector2i current, Vector2i target)
    {
        var db = current - target;
        var da = step - target;
        var distBefore = Math.Abs(db.X) + Math.Abs(db.Y);
        var distAfter = Math.Abs(da.X) + Math.Abs(da.Y);
        return distAfter < distBefore;
    }

    private List<Vector2i>? BfsPath(
        Vector2i start,
        Vector2i goal,
        EntityUid gridUid,
        MapGridComponent gridComp,
        EntityUid mover,
        int maxSteps)
    {
        if (start == goal) return null;

        var queue = new Queue<Vector2i>();
        var cameFrom = new Dictionary<Vector2i, Vector2i>();
        queue.Enqueue(start);
        cameFrom[start] = start;

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            var pathLen = 0;
            var trace = current;
            while (cameFrom[trace] != trace) { trace = cameFrom[trace]; pathLen++; }
            if (pathLen >= maxSteps) continue;

            foreach (var neighbor in GetCardinalNeighbors(current))
            {
                if (cameFrom.ContainsKey(neighbor)) continue;
                if (!IsTilePassable(neighbor, gridUid, gridComp, mover)) continue;

                cameFrom[neighbor] = current;

                if (neighbor == goal)
                {
                    var path = new List<Vector2i>();
                    var node = goal;
                    while (node != start) { path.Add(node); node = cameFrom[node]; }
                    path.Add(start);
                    path.Reverse();
                    return path;
                }

                queue.Enqueue(neighbor);
            }
        }

        return null;
    }

    private static IEnumerable<Vector2i> GetCardinalNeighbors(Vector2i pos)
    {
        yield return pos + new Vector2i(1, 0);
        yield return pos + new Vector2i(-1, 0);
        yield return pos + new Vector2i(0, 1);
        yield return pos + new Vector2i(0, -1);
    }

    private bool IsTilePassable(Vector2i tile, EntityUid gridUid, MapGridComponent gridComp, EntityUid mover)
    {
        var tileRef = _map.GetTileRef(gridUid, gridComp, tile);
        if (tileRef.Tile.IsEmpty) return false;

        foreach (var entity in _lookup.GetLocalEntitiesIntersecting(gridUid, tile, gridComp: gridComp))
        {
            if (entity == mover || entity == gridUid) continue;
            if (!TryComp<PhysicsComponent>(entity, out var physics)) continue;
            if (physics.BodyType != BodyType.Static) continue;
            if (!TryComp<FixturesComponent>(entity, out var fixturesComp)) continue;
            foreach (var (_, fixture) in fixturesComp.Fixtures)
            {
                if (!fixture.Hard) continue;
                if ((fixture.CollisionLayer & (int)CollisionGroup.MidImpassable) != 0) return false;
                if ((fixture.CollisionLayer & (int)CollisionGroup.Impassable) != 0) return false;
            }
        }

        return true;
    }

    // ── Follower voluntary movement ───────────────────────────────────────────

    private void OnFollowerMoved(Entity<FormationSlottedComponent> ent, ref MoveEvent args)
    {
        if (ent.Comp.IsBeingForceMoved) return;
        if (ent.Comp.JoinStunActive) return;
        if (ent.Comp.IsLeaderDot) return;
        if (ent.Comp.SmoothTargetTile.HasValue) return;

        if (!Exists(ent.Comp.LeaderUid)) return;

        var leaderXform = Transform(ent.Comp.LeaderUid);
        if (leaderXform.GridUid is not { } gridUid) return;
        if (!TryComp<MapGridComponent>(gridUid, out var gridComp)) return;

        var followerXform = Transform(ent.Owner);

        // Detach immediately if the follower has been carried/transported off the grid.
        if (followerXform.GridUid != gridUid)
        {
            ForceUnslot(ent, returnDot: false);
            return;
        }

        var leaderTile = _map.TileIndicesFor(gridUid, gridComp, leaderXform.Coordinates);
        var leaderFacing = leaderXform.LocalRotation.GetDir();
        var expectedTile = LeaderLocalToWorld(leaderTile, leaderFacing, ent.Comp.LocalOffset);
        var followerTile = _map.TileIndicesFor(gridUid, gridComp, followerXform.Coordinates);

        if (followerTile == expectedTile) return;

        ForceUnslot(ent, returnDot: true);
        _popup.PopupEntity("You step out of formation. Your slot remains briefly.", ent, ent, PopupType.Small);
    }

    private void OnFollowerShutdown(Entity<FormationSlottedComponent> ent, ref ComponentShutdown args)
    {
        _stunFreezeExpiry.Remove(ent);

        if (TryComp<AU14FormationLeaderComponent>(ent.Comp.LeaderUid, out var leader))
            leader.ActiveFollowers.Remove(ent.Owner);

        // Remove the shared no-collide marker from the follower.
        RemComp<FormationMemberComponent>(ent);
    }

    // ── BUI Handlers ──────────────────────────────────────────────────────────

    private void OnEnterPlacement(Entity<AU14FormationLeaderComponent> ent, ref FormationEnterPlacementMsg msg)
    {
        ent.Comp.IsInPlanningMode = true;
        ent.Comp.IsPlacingLeaderDot = msg.IsLeaderDot;
        SendBuiState(ent);
    }

    private void OnPlaceDot(Entity<AU14FormationLeaderComponent> ent, ref FormationPlaceDotMsg msg)
    {
        ent.Comp.PendingDots.Add(new FormationPendingDot
        {
            TilePos = new Vector2i(msg.TileX, msg.TileY),
            Facing = msg.Facing,
            IsLeaderDot = msg.IsLeaderDot,
        });
        SendBuiState(ent);
    }

    private void OnUndoLastDot(Entity<AU14FormationLeaderComponent> ent, ref FormationUndoLastDotMsg msg)
    {
        if (ent.Comp.PendingDots.Count > 0)
            ent.Comp.PendingDots.RemoveAt(ent.Comp.PendingDots.Count - 1);
        SendBuiState(ent);
    }

    private void OnConfirm(Entity<AU14FormationLeaderComponent> ent, ref FormationConfirmMsg msg)
    {
        SpawnPendingDots(ent);
        ent.Comp.IsInPlanningMode = false;
        ent.Comp.PendingDots.Clear();
        SendBuiState(ent);
    }

    private void OnCancelPlanning(Entity<AU14FormationLeaderComponent> ent, ref FormationCancelPlanningMsg msg)
    {
        ent.Comp.IsInPlanningMode = false;
        ent.Comp.PendingDots.Clear();
        SendBuiState(ent);
    }

    private void OnClear(Entity<AU14FormationLeaderComponent> ent, ref FormationClearMsg msg)
    {
        // Delete all open (unoccupied) slotting dots.
        foreach (var dotUid in ent.Comp.PlacedDots.ToList())
        {
            if (!TerminatingOrDeleted(dotUid))
                QueueDel(dotUid);
        }
        ent.Comp.PlacedDots.Clear();
        SendBuiState(ent);
    }

    private void OnDisband(Entity<AU14FormationLeaderComponent> ent, ref FormationDisbandMsg msg)
    {
        ClearFormation(ent, disbandFollowers: true);
        SendBuiState(ent);
    }

    private void OnFreezeToggle(Entity<AU14FormationLeaderComponent> ent, ref FormationFreezeToggleMsg msg)
    {
        ent.Comp.FormationFrozen = !ent.Comp.FormationFrozen;
        var notice = ent.Comp.FormationFrozen ? "Formation halted." : "Formation marching.";
        _popup.PopupEntity(notice, ent, ent, PopupType.Small);
        SendBuiState(ent);
    }

    private void OnDebugToggle(Entity<AU14FormationLeaderComponent> ent, ref FormationDebugToggleMsg msg)
    {
        ent.Comp.DebugShowSlots = !ent.Comp.DebugShowSlots;
        if (!ent.Comp.DebugShowSlots)
            ClearDebugDots(ent.Comp);
        SendBuiState(ent);
    }

    private void OnSetFollowMode(Entity<AU14FormationLeaderComponent> ent, ref FormationSetFollowModeMsg msg)
    {
        ent.Comp.FollowMode = msg.Mode;
        var notice = msg.Mode == FormationFollowMode.Chase
            ? "Follow mode: Chase - followers close gaps aggressively."
            : "Follow mode: Hold - followers step with you one tile at a time.";
        _popup.PopupEntity(notice, ent, ent, PopupType.Small);
        SendBuiState(ent);
    }

    private void OnCollisionToggle(Entity<AU14FormationLeaderComponent> ent, ref FormationCollisionToggleMsg msg)
    {
        ent.Comp.CollisionsDisabled = !ent.Comp.CollisionsDisabled;

        if (ent.Comp.CollisionsDisabled)
        {
            // Suppress collisions for everyone currently in formation.
            foreach (var follower in ent.Comp.ActiveFollowers)
                EnsureComp<FormationMemberComponent>(follower);
            if (ent.Comp.ActiveFollowers.Count > 0)
                EnsureComp<FormationMemberComponent>(ent.Owner);
            _popup.PopupEntity("Collisions disabled - members can pass through each other.", ent, ent, PopupType.Small);
        }
        else
        {
            // Restore normal collisions for everyone.
            foreach (var follower in ent.Comp.ActiveFollowers)
                RemComp<FormationMemberComponent>(follower);
            RemComp<FormationMemberComponent>(ent.Owner);
            _popup.PopupEntity("Collisions enabled - members block each other normally.", ent, ent, PopupType.Small);
        }

        SendBuiState(ent);
    }

    private void OnFollowerMobStateChanged(Entity<FormationSlottedComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState is MobState.Dead or MobState.Critical)
            ForceUnslot(ent, returnDot: false);
    }

    private void OnDotLifetimeToggle(Entity<AU14FormationLeaderComponent> ent, ref FormationDotLifetimeToggleMsg msg)
    {
        ent.Comp.ExtendedDotLifetime = !ent.Comp.ExtendedDotLifetime;
        var notice = ent.Comp.ExtendedDotLifetime
            ? "Dot lifetime: 15 minutes. Use this sparingly - abuse has consequences."
            : "Dot lifetime: standard 2 minutes.";
        _popup.PopupEntity(notice, ent, ent, PopupType.Small);
        SendBuiState(ent);
    }

    // ── Dot spawning ──────────────────────────────────────────────────────────

    private void SpawnPendingDots(Entity<AU14FormationLeaderComponent> leader)
    {
        var xform = Transform(leader.Owner);
        if (xform.GridUid is not { } gridUid) return;
        if (!TryComp<MapGridComponent>(gridUid, out var gridComp)) return;

        var lifetime = leader.Comp.ExtendedDotLifetime ? DotExtendedLifetime : DotDefaultLifetime;
        foreach (var pending in leader.Comp.PendingDots)
        {
            var dotUid = SpawnDotEntity(pending.TilePos, pending.Facing, pending.IsLeaderDot,
                leader.Comp.FormationColor, leader, TimeSpan.FromSeconds(lifetime));
            leader.Comp.PlacedDots.Add(dotUid);
        }

        leader.Comp.LastTilePos = _map.TileIndicesFor(gridUid, gridComp, xform.Coordinates);
        leader.Comp.LastFacing = xform.LocalRotation.GetDir();
    }

    private EntityUid SpawnDotEntity(
        Vector2i tile,
        Direction facing,
        bool isLeaderDot,
        Color color,
        EntityUid ownerLeader,
        TimeSpan lifetime)
    {
        if (!Exists(ownerLeader)) return EntityUid.Invalid;
        var leaderXform = Transform(ownerLeader);
        if (leaderXform.GridUid is not { } gridUid) return EntityUid.Invalid;
        if (!TryComp<MapGridComponent>(gridUid, out var gridComp)) return EntityUid.Invalid;

        var coords = _map.GridTileToLocal(gridUid, gridComp, tile);
        var protoId = isLeaderDot ? "AU14FormationDotLeader" : "AU14FormationDotFollower";
        var dotUid = Spawn(protoId, coords);

        _transform.SetLocalRotation(dotUid, facing.ToAngle());

        var dotComp = EnsureComp<FormationDotComponent>(dotUid);
        dotComp.DotColor = color;
        dotComp.FacingDirection = facing;
        dotComp.IsLeaderDot = isLeaderDot;
        dotComp.OwnerLeader = ownerLeader;
        dotComp.MaxLifetime = lifetime;
        dotComp.DeathTime = _timing.CurTime + lifetime;
        Dirty(dotUid, dotComp);

        return dotUid;
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    private void ClearFormation(Entity<AU14FormationLeaderComponent> leader, bool disbandFollowers)
    {
        // Delete all open slotting dots.
        foreach (var dotUid in leader.Comp.PlacedDots.ToList())
        {
            if (!TerminatingOrDeleted(dotUid))
                QueueDel(dotUid);
        }
        leader.Comp.PlacedDots.Clear();
        leader.Comp.PendingDots.Clear();

        if (!disbandFollowers) return;

        // Unslot all active followers without spawning return dots.
        // RemComp<FormationSlottedComponent> triggers OnFollowerShutdown which removes FormationMemberComponent.
        foreach (var follower in leader.Comp.ActiveFollowers.ToList())
        {
            if (TryComp<FormationSlottedComponent>(follower, out _))
                RemComp<FormationSlottedComponent>(follower);
            _stunFreezeExpiry.Remove(follower);
        }
        leader.Comp.ActiveFollowers.Clear();

        // Remove no-collide from the leader too.
        RemComp<FormationMemberComponent>(leader.Owner);

        // Kill debug indicators.
        ClearDebugDots(leader.Comp);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        // Detach followers who have been knocked down (stunned to the floor) outside of
        // the 1-second join window, or who have been carried off the grid.
        var knockedQuery = EntityQueryEnumerator<FormationSlottedComponent, KnockedDownComponent>();
        while (knockedQuery.MoveNext(out var knockedUid, out var knockedSlotted, out _))
        {
            if (knockedSlotted.JoinStunActive) continue; // ignore the slot-in stun
            ForceUnslot(knockedUid, returnDot: false);
            _popup.PopupEntity("Formation broken - you were knocked down.", knockedUid, knockedUid, PopupType.SmallCaution);
        }

        // Detach followers whose grid no longer matches their leader (carried, buckled into a
        // vehicle, teleported, etc.) and who aren't already mid-move.
        var gridCheckQuery = EntityQueryEnumerator<FormationSlottedComponent>();
        while (gridCheckQuery.MoveNext(out var gcUid, out var gcSlotted))
        {
            if (gcSlotted.IsLeaderDot || gcSlotted.SmoothTargetTile.HasValue) continue;
            if (!Exists(gcSlotted.LeaderUid)) continue;
            var leaderGrid = Transform(gcSlotted.LeaderUid).GridUid;
            if (leaderGrid == null) continue;
            if (Transform(gcUid).GridUid != leaderGrid)
                ForceUnslot(gcUid, returnDot: false);
        }

        // Clear join stuns.
        var expiredStuns = new List<EntityUid>();
        foreach (var (uid, expiry) in _stunFreezeExpiry)
        {
            if (now >= expiry)
            {
                expiredStuns.Add(uid);
                if (TryComp<FormationSlottedComponent>(uid, out var s))
                    s.JoinStunActive = false;
            }
        }
        foreach (var uid in expiredStuns) _stunFreezeExpiry.Remove(uid);

        // Expire open slotting dots.
        var dotQuery = EntityQueryEnumerator<FormationDotComponent>();
        while (dotQuery.MoveNext(out var uid, out var dot))
        {
            if (now < dot.DeathTime) continue;
            if (!TerminatingOrDeleted(uid))
                QueueDel(uid);
        }

        // Dynamic return dots: reposition each tick so the re-slot arrow tracks the formation
        // even as the leader keeps moving ("moving car" — the door stays with the car).
        var dynQuery = EntityQueryEnumerator<FormationDotComponent>();
        while (dynQuery.MoveNext(out var dynUid, out var dynDot))
        {
            if (!dynDot.IsDynamicSlot) continue;
            if (now >= dynDot.DeathTime) continue; // expiry loop already queued this for deletion

            if (!Exists(dynDot.OwnerLeader) || TerminatingOrDeleted(dynDot.OwnerLeader))
            {
                QueueDel(dynUid);
                continue;
            }

            var dynLeaderXform = Transform(dynDot.OwnerLeader);
            if (dynLeaderXform.GridUid is not { } dynGrid) continue;
            if (!TryComp<MapGridComponent>(dynGrid, out var dynGridComp)) continue;

            var dynLeaderTile = _map.TileIndicesFor(dynGrid, dynGridComp, dynLeaderXform.Coordinates);
            var dynFacing = dynLeaderXform.LocalRotation.GetDir();
            var dynTargetTile = LeaderLocalToWorld(dynLeaderTile, dynFacing, dynDot.SlotLocalOffset);
            var dynTargetCoords = _map.GridTileToLocal(dynGrid, dynGridComp, dynTargetTile);

            _transform.SetCoordinates(dynUid, dynTargetCoords);
            _transform.SetLocalRotation(dynUid, dynFacing.ToAngle());

            if (dynDot.FacingDirection != dynFacing)
            {
                dynDot.FacingDirection = dynFacing;
                Dirty(dynUid, dynDot);
            }
        }

        // Debug slot indicators: keep one persistent dot per follower at their target position.
        var debugLeaderQuery = EntityQueryEnumerator<AU14FormationLeaderComponent>();
        while (debugLeaderQuery.MoveNext(out var leaderUid, out var leaderComp))
        {
            if (!leaderComp.DebugShowSlots) continue;

            var leaderXform = Transform(leaderUid);
            if (leaderXform.GridUid is not { } debugGrid) continue;
            if (!TryComp<MapGridComponent>(debugGrid, out var debugGridComp)) continue;

            var leaderTile   = _map.TileIndicesFor(debugGrid, debugGridComp, leaderXform.Coordinates);
            var leaderFacing = leaderXform.LocalRotation.GetDir();

            // Remove stale entries for followers who have since unslotted.
            foreach (var (exFollower, exDot) in leaderComp.DebugDots.ToList())
            {
                if (HasComp<FormationSlottedComponent>(exFollower)) continue;
                if (!TerminatingOrDeleted(exDot)) QueueDel(exDot);
                leaderComp.DebugDots.Remove(exFollower);
            }

            foreach (var follower in leaderComp.ActiveFollowers)
            {
                if (!TryComp<FormationSlottedComponent>(follower, out var slottedComp)) continue;
                if (slottedComp.IsLeaderDot) continue;

                var targetTile   = LeaderLocalToWorld(leaderTile, leaderFacing, slottedComp.LocalOffset);
                var targetCoords = _map.GridTileToLocal(debugGrid, debugGridComp, targetTile);

                if (!leaderComp.DebugDots.TryGetValue(follower, out var dbgDot) || TerminatingOrDeleted(dbgDot))
                {
                    // Spawn a new indicator dot — white so it is visually distinct from live dots.
                    dbgDot = Spawn("AU14FormationDotFollower", targetCoords);
                    _transform.SetLocalRotation(dbgDot, leaderFacing.ToAngle());
                    var dbgComp = EnsureComp<FormationDotComponent>(dbgDot);
                    dbgComp.DotColor       = Color.White;
                    dbgComp.FacingDirection = leaderFacing;
                    dbgComp.IsLeaderDot    = false;
                    dbgComp.OwnerLeader    = leaderUid;
                    dbgComp.SlottedEntity  = leaderUid; // Prevent slotting
                    dbgComp.DeathTime      = now + TimeSpan.FromHours(24);
                    dbgComp.MaxLifetime    = TimeSpan.FromHours(24);
                    Dirty(dbgDot, dbgComp);
                    leaderComp.DebugDots[follower] = dbgDot;
                }
                else
                {
                    // Move existing dot to updated target.
                    _transform.SetCoordinates(dbgDot, targetCoords);
                    _transform.SetLocalRotation(dbgDot, leaderFacing.ToAngle());
                    if (TryComp<FormationDotComponent>(dbgDot, out var dbgComp))
                    {
                        dbgComp.FacingDirection = leaderFacing;
                        Dirty(dbgDot, dbgComp);
                    }
                }
            }
        }

        // Chase mode: continuously reposition any follower that is off-target and not already moving.
        var chaseLeaderQuery = EntityQueryEnumerator<AU14FormationLeaderComponent>();
        while (chaseLeaderQuery.MoveNext(out var chaseLeaderUid, out var chaseLeaderComp))
        {
            if (chaseLeaderComp.FollowMode != FormationFollowMode.Chase) continue;
            if (chaseLeaderComp.FormationFrozen) continue;
            if (chaseLeaderComp.ActiveFollowers.Count == 0) continue;

            var chaseLeaderXform = Transform(chaseLeaderUid);
            if (chaseLeaderXform.GridUid is not { } chaseGrid) continue;
            if (!TryComp<MapGridComponent>(chaseGrid, out var chaseGridComp)) continue;

            var chaseTile = _map.TileIndicesFor(chaseGrid, chaseGridComp, chaseLeaderXform.Coordinates);
            var chaseFacing = chaseLeaderXform.LocalRotation.GetDir();

            foreach (var chaseFollower in chaseLeaderComp.ActiveFollowers.ToList())
            {
                if (!TryComp<FormationSlottedComponent>(chaseFollower, out var chaseSlotted)) continue;
                if (chaseSlotted.IsLeaderDot) continue;
                if (chaseSlotted.SmoothTargetTile.HasValue) continue; // already moving
                if (chaseSlotted.JoinStunActive) continue;

                var followerXform = Transform(chaseFollower);
                if (followerXform.GridUid != chaseGrid) continue;

                var followerTile = _map.TileIndicesFor(chaseGrid, chaseGridComp, followerXform.Coordinates);
                var targetTile = LeaderLocalToWorld(chaseTile, chaseFacing, chaseSlotted.LocalOffset);
                if (followerTile == targetTile) continue;

                MoveFollowerStep(chaseFollower, chaseSlotted, (chaseLeaderUid, chaseLeaderComp),
                    chaseGrid, chaseGridComp, chaseTile, chaseFacing);
            }
        }

        // Smooth movement for slotted followers.
        var slottedQuery = EntityQueryEnumerator<FormationSlottedComponent>();
        while (slottedQuery.MoveNext(out var uid, out var slotted))
        {
            if (slotted.SmoothTargetTile is not { } targetTile) continue;

            var xform = Transform(uid);
            if (xform.GridUid is not { } gridUid)
            {
                slotted.SmoothTargetTile = null;
                continue;
            }
            if (!TryComp<MapGridComponent>(gridUid, out var gridComp))
            {
                slotted.SmoothTargetTile = null;
                continue;
            }

            var targetLocal = _map.GridTileToLocal(gridUid, gridComp, targetTile);
            var targetPos = targetLocal.Position;
            var currentPos = xform.LocalPosition;
            var delta = targetPos - currentPos;
            var dist = delta.Length();

            slotted.IsBeingForceMoved = true;

            if (dist < 0.1f)
            {
                // Snap to tile centre and apply final facing.
                _transform.SetCoordinates(uid, targetLocal);
                _transform.SetLocalRotation(uid, slotted.SmoothTargetFacing.ToAngle());
                slotted.SmoothTargetTile = null;
            }
            else
            {
                var step = delta.Normalized() * FormationFollowSpeed * frameTime;
                if (step.Length() > dist) step = delta;
                _transform.SetCoordinates(uid, new EntityCoordinates(xform.ParentUid, currentPos + step));
            }

            slotted.IsBeingForceMoved = false;
        }
    }

    // ── BUI State ─────────────────────────────────────────────────────────────

    private void SendBuiState(Entity<AU14FormationLeaderComponent> leader)
    {
        var pending = leader.Comp.PendingDots.Select(p => new FormationPendingDotNet
        {
            TileX = p.TilePos.X,
            TileY = p.TilePos.Y,
            Facing = p.Facing,
            IsLeaderDot = p.IsLeaderDot,
        }).ToList();

        var state = new FormationMenuBuiState(
            pending,
            leader.Comp.IsInPlanningMode,
            leader.Comp.IsPlacingLeaderDot,
            leader.Comp.FormationFrozen,
            leader.Comp.PlacedDots.Count,
            leader.Comp.ActiveFollowers.Count,
            leader.Comp.DebugShowSlots,
            leader.Comp.FollowMode,
            leader.Comp.CollisionsDisabled,
            leader.Comp.ExtendedDotLifetime);

        _ui.SetUiState(leader.Owner, FormationMenuUiKey.Key, state);
    }

    // ── Math helpers ──────────────────────────────────────────────────────────
    // SS14 angle convention: 0 = South (0,-1), π/2 = East (1,0), π = North (0,1).
    // Forward vector for angle θ: (sin θ, -cos θ)
    // Right vector (90° CW from forward): (-cos θ, -sin θ)
    // Leader-local axes: +Y = forward (direction leader faces), +X = leader's right hand.

    // Slot-based formation targeting (industry standard — works for any formation shape):
    //   world_target = leader_world_pos + rotate(slot_local_offset, leader_facing)
    // The localOffset is captured once on slot-in via WorldToLeaderLocal and never changes.
    // On every leader move/turn, LeaderLocalToWorld recomputes the world target instantly.

    private static Vector2 WorldToLeaderLocal(Vector2 worldOffset, Angle leaderAngle)
    {
        var cos = (float)Math.Cos(leaderAngle.Theta);
        var sin = (float)Math.Sin(leaderAngle.Theta);
        return new Vector2(
            -worldOffset.X * cos - worldOffset.Y * sin,
             worldOffset.X * sin - worldOffset.Y * cos
        );
    }

    private static Vector2 LeaderLocalToWorldVec(Vector2 localOffset, Angle leaderAngle)
    {
        var cos = (float)Math.Cos(leaderAngle.Theta);
        var sin = (float)Math.Sin(leaderAngle.Theta);
        return new Vector2(
            -localOffset.X * cos + localOffset.Y * sin,
            -localOffset.X * sin - localOffset.Y * cos
        );
    }

    private static Vector2i LeaderLocalToWorld(Vector2i leaderTile, Direction leaderFacing, Vector2 localOffset)
    {
        var worldVec = LeaderLocalToWorldVec(localOffset, leaderFacing.ToAngle());
        return leaderTile + new Vector2i((int)MathF.Round(worldVec.X), (int)MathF.Round(worldVec.Y));
    }

    // ── Debug helpers ─────────────────────────────────────────────────────────

    private void ClearDebugDots(AU14FormationLeaderComponent leader)
    {
        foreach (var (_, dot) in leader.DebugDots)
        {
            if (!TerminatingOrDeleted(dot))
                QueueDel(dot);
        }
        leader.DebugDots.Clear();
    }
}
