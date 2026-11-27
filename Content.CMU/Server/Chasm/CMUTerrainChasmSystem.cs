using System.Numerics;
using Content.Shared.Chasm;
using Content.Shared.CMU14.Chasm;
using Content.Shared.Ghost.Components;
using Content.Shared.Maps;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Misc;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Chasm;

public sealed partial class CMUTerrainChasmSystem : EntitySystem
{
    [Dependency] private ChasmSystem _chasm = default!;
    [Dependency] private StepTriggerSystem _triggers = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private MobStateSystem _mob = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedGrapplingGunSystem _grapple = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;

    private static readonly CMUChasmLedgeKey[] Keys = [CMUChasmLedgeKey.X, CMUChasmLedgeKey.F, CMUChasmLedgeKey.C];
    private static readonly ProtoId<TagPrototype> CatwalkTag = "Catwalk";
    private static readonly TimeSpan PressInterval = TimeSpan.FromMilliseconds(70);
    private int _nextGrip;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUTerrainChasmComponent, StepTriggerAttemptEvent>(OnProbe);
        SubscribeLocalEvent<ActorComponent, MoveEvent>(OnActorMove);
        SubscribeLocalEvent<CMUTerrainChasmComponent, EntityStartFallingAttemptEvent>(OnFall);
        SubscribeLocalEvent<CMUChasmLedgeGripComponent, ComponentRemove>(OnRelease);
        SubscribeNetworkEvent<CMUChasmLedgePressEvent>(OnPress);
    }

    private void OnActorMove(Entity<ActorComponent> actor, ref MoveEvent args)
    {
        if (args.OnlyRotation || HasComp<GhostComponent>(actor))
            return;

        var gridUid = args.NewPosition.EntityId;
        if (args.ParentChanged || !TryComp<MapGridComponent>(gridUid, out var grid) ||
            Vector2.DistanceSquared(args.OldPosition.Position, args.NewPosition.Position) > grid.TileSize * grid.TileSize)
        {
            RemComp<CMUChasmApproachComponent>(actor);
            return;
        }

        var from = _map.LocalToTile(gridUid, grid, args.OldPosition);
        var to = _map.LocalToTile(gridUid, grid, args.NewPosition);
        if (from == to)
            return;

        if (!IsChasmTile(gridUid, grid, to))
            RemComp<CMUChasmApproachComponent>(actor);
        else if (!IsChasmTile(gridUid, grid, from))
            EnsureComp<CMUChasmApproachComponent>(actor).Ledge = _map.GridTileToLocal(gridUid, grid, from);
    }

    private void OnRelease(Entity<CMUChasmLedgeGripComponent> ent, ref ComponentRemove args)
    {
        if (!TerminatingOrDeleted(ent.Comp.Chasm) && !HasComp<ChasmFallingComponent>(ent))
            _triggers.RearmAfterLedgeGrab(ent.Comp.Chasm, ent);
    }

    private void OnProbe(Entity<CMUTerrainChasmComponent> ent, ref StepTriggerAttemptEvent args)
    {
        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var coords = _transform.WithEntityId(Transform(args.Tripper).Coordinates, gridUid);
        var tile = _map.LocalToTile(gridUid, grid, coords);
        var sourceTile = _map.LocalToTile(gridUid, grid, xform.Coordinates);
        if (tile != sourceTile)
        {
            // Keep tracking the contact so walking farther in gets checked on the next tick.
            if (!args.InitialContact)
                args.Cancelled = true;
            return;
        }

        if (args.InitialContact)
            return;

        // The square under the player's centre must be entirely over the hole. Adjacent
        // chasm tiles count as one opening, including when crossing a four-tile junction.
        var inset = new Vector2(0.25f * grid.TileSize);
        var min = _map.LocalToTile(gridUid, grid, coords.Offset(-inset));
        var max = _map.LocalToTile(gridUid, grid, coords.Offset(inset));
        for (var x = min.X; x <= max.X; x++)
        {
            for (var y = min.Y; y <= max.Y; y++)
            {
                var sample = new Vector2i(x, y);
                if (sample == tile || IsChasmTile(gridUid, grid, sample))
                    continue;

                // The rim still supports the player even if they follow it across several pit tiles.
                if (HasComp<ActorComponent>(args.Tripper) && !HasComp<CMUChasmLedgeGripComponent>(args.Tripper))
                {
                    var approach = EnsureComp<CMUChasmApproachComponent>(args.Tripper);
                    if (approach.Ledge.EntityId != gridUid ||
                        Vector2.DistanceSquared(coords.Position, approach.Ledge.Position) > 2f * grid.TileSize * grid.TileSize)
                        approach.Ledge = _map.GridTileToLocal(gridUid, grid, sample);
                }
                args.Cancelled = true;
                return;
            }
        }
    }

    private bool IsChasmTile(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        var anchored = _map.GetAnchoredEntities(gridUid, grid, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (HasComp<CMUTerrainChasmComponent>(uid))
                return true;
        }

        return false;
    }

    private bool CanHold(EntityUid uid)
    {
        return HasComp<ActorComponent>(uid) && !HasComp<GhostComponent>(uid) && _mob.IsAlive(uid) &&
            !HasComp<ThrownItemComponent>(uid) && !HasComp<KnockedDownComponent>(uid) &&
            !HasComp<StunnedComponent>(uid) && !HasComp<ChasmFallingComponent>(uid) &&
            !(TryComp<PullableComponent>(uid, out var pulled) && pulled.BeingPulled);
    }

    private void OnFall(Entity<CMUTerrainChasmComponent> ent, ref EntityStartFallingAttemptEvent args)
    {
        var uid = args.Faller;
        if (HasComp<CMUChasmLedgeGripComponent>(uid))
        {
            args.Cancelled = true;
            return;
        }

        if (args.Cancelled || !TryComp<CMUChasmApproachComponent>(uid, out var approach))
            return;

        var ledge = approach.Ledge;
        RemComp<CMUChasmApproachComponent>(uid);
        if (!CanHold(uid) || !TryComp<InputMoverComponent>(uid, out var mover) || !mover.CanMove ||
            !TryGetLanding(uid, ent, ledge, out var landing))
            return;

        var grip = AddComp<CMUChasmLedgeGripComponent>(uid);
        grip.Chasm = ent;
        grip.ReturnTo = landing;
        grip.Key = _random.Pick(Keys);
        grip.GripId = ++_nextGrip;
        grip.Expires = _timing.CurTime + CMUChasmLedgeGripComponent.Duration;
        _physics.SetLinearVelocity(uid, Vector2.Zero);
        Dirty(uid, grip);
        _popup.PopupEntity(Loc.GetString("cmu-chasm-ledge-grab", ("player", uid)), uid, Filter.PvsExcept(uid), true);
        args.Cancelled = true;
    }

    private bool TryGetLanding(EntityUid uid, EntityUid chasm, EntityCoordinates ledge, out EntityCoordinates landing)
    {
        landing = default;
        if (Transform(chasm).GridUid is not { } gridUid || ledge.EntityId != gridUid ||
            !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var position = _transform.WithEntityId(Transform(uid).Coordinates, gridUid);
        var reachSquared = 2f * grid.TileSize * grid.TileSize;
        if (Vector2.DistanceSquared(position.Position, ledge.Position) <= reachSquared && CanLand(ledge))
        {
            landing = ledge;
            return true;
        }

        // At a corner the original ledge can be blocked or farther away than the one beside it.
        var tile = _map.LocalToTile(gridUid, grid, position);
        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                var candidate = _map.GridTileToLocal(gridUid, grid, tile + new Vector2i(x, y));
                var distance = Vector2.DistanceSquared(position.Position, candidate.Position);
                if (distance > reachSquared || !CanLand(candidate))
                    continue;
                reachSquared = distance;
                landing = candidate;
            }
        }

        return landing.EntityId.IsValid();
    }

    private bool CanLand(EntityCoordinates coords)
    {
        if (!_turf.TryGetTileRef(coords, out var tile) || tile.Value.Tile.IsEmpty ||
            !TryComp<MapGridComponent>(tile.Value.GridUid, out var grid))
            return false;

        return !IsChasmTile(tile.Value.GridUid, grid, tile.Value.GridIndices) &&
            !_turf.IsTileBlocked(tile.Value, CollisionGroup.Impassable | CollisionGroup.BarricadeImpassable);
    }

    private void OnPress(CMUChasmLedgePressEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } uid || !TryComp<CMUChasmLedgeGripComponent>(uid, out var grip) ||
            grip.GripId != msg.GripId || grip.Presses != msg.Presses || !Enum.IsDefined(msg.Key) || !CheckGrip(uid, grip))
            return;

        if (msg.Key != grip.Key)
        {
            if (++grip.Mistakes >= CMUChasmLedgeGripComponent.MaxMistakes)
            {
                Fall(uid, grip);
                RemComp<CMUChasmLedgeGripComponent>(uid);
                return;
            }

            Dirty(uid, grip);
            return;
        }

        // Allow taps delayed by the network to arrive together, up to the number possible since the catch.
        var started = grip.Expires - CMUChasmLedgeGripComponent.Duration;
        if (_timing.CurTime < started + PressInterval * grip.Presses)
            return;

        if (++grip.Presses < CMUChasmLedgeGripComponent.RequiredPresses)
        {
            grip.Key = Keys[(Array.IndexOf(Keys, grip.Key) + 1) % Keys.Length];
            Dirty(uid, grip);
            return;
        }

        if (CanLand(grip.ReturnTo))
            _transform.SetCoordinates(uid, grip.ReturnTo);
        else
            Fall(uid, grip);

        RemComp<CMUChasmLedgeGripComponent>(uid);
    }

    public override void Update(float frameTime)
    {
        var grips = EntityQueryEnumerator<CMUChasmLedgeGripComponent>();
        while (grips.MoveNext(out var uid, out var grip))
            CheckGrip(uid, grip);
    }

    private bool CheckGrip(EntityUid uid, CMUChasmLedgeGripComponent grip)
    {
        // If rescued, teleported, or the opening is covered, release the player in place.
        var xform = Transform(uid);
        if (TerminatingOrDeleted(grip.Chasm) ||
            !_turf.TryGetTileRef(xform.Coordinates, out var tile) ||
            tile.Value.GridUid != grip.ReturnTo.EntityId ||
            !TryComp<MapGridComponent>(tile.Value.GridUid, out var grid) ||
            !IsChasmTile(tile.Value.GridUid, grid, tile.Value.GridIndices) || _grapple.IsEntityHooked(uid))
        {
            RemComp<CMUChasmLedgeGripComponent>(uid);
            return false;
        }

        var anchored = _map.GetAnchoredEntities(tile.Value.GridUid, grid, tile.Value.GridIndices);
        while (anchored.MoveNext(out var ent))
        {
            if (!_tags.HasTag(ent.Value, CatwalkTag))
                continue;
            RemComp<CMUChasmLedgeGripComponent>(uid);
            return false;
        }

        var position = _transform.WithEntityId(xform.Coordinates, tile.Value.GridUid);
        if (_timing.CurTime < grip.Expires && CanHold(uid) &&
            Vector2.DistanceSquared(position.Position, grip.ReturnTo.Position) <= 2f * grid.TileSize * grid.TileSize)
            return true;

        Fall(uid, grip);
        RemComp<CMUChasmLedgeGripComponent>(uid);
        return false;
    }

    private void Fall(EntityUid uid, CMUChasmLedgeGripComponent grip)
    {
        if (!HasComp<ChasmFallingComponent>(uid))
            _chasm.StartFalling((grip.Chasm, null), uid);
    }
}
