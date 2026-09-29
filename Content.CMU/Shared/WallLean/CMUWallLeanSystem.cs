using System.Numerics;
using Content.Shared._RMC14.Slow;
using Content.Shared.Buckle.Components;
using Content.Shared.DragDrop;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.CMU14.WallLean;

public sealed class CMUWallLeanSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly RMCSlowSystem _slow = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const float SideShift = 10f / 32f;
    private const float UnderWallShift = 16f / 32f;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUWallLeanableComponent, CanDropTargetEvent>(OnCanDropTarget);
        SubscribeLocalEvent<CMUWallLeanableComponent, DragDropTargetEvent>(OnDragDropTarget);
        SubscribeLocalEvent<CMUWallLeaningComponent, MoveInputEvent>(OnMoveInput);
        SubscribeLocalEvent<CMUWallLeaningComponent, PreventCollideEvent>(OnPreventCollide);
        SubscribeLocalEvent<CMUWallLeaningComponent, AttemptMobCollideEvent>(OnAttemptMobCollide);
        SubscribeLocalEvent<CMUWallLeaningComponent, AttemptMobTargetCollideEvent>(OnAttemptMobTargetCollide);
    }

    private void OnPreventCollide(Entity<CMUWallLeaningComponent> ent, ref PreventCollideEvent args)
    {
        if (ent.Comp.Wall != null && HasComp<MobStateComponent>(args.OtherEntity))
            args.Cancelled = true;
    }

    private void OnAttemptMobCollide(Entity<CMUWallLeaningComponent> ent, ref AttemptMobCollideEvent args)
    {
        if (ent.Comp.Wall != null)
            args.Cancelled = true;
    }

    private void OnAttemptMobTargetCollide(Entity<CMUWallLeaningComponent> ent, ref AttemptMobTargetCollideEvent args)
    {
        if (ent.Comp.Wall != null)
            args.Cancelled = true;
    }

    private void OnCanDropTarget(Entity<CMUWallLeanableComponent> ent, ref CanDropTargetEvent args)
    {
        if (args.User != args.Dragged || !HasComp<HumanoidProfileComponent>(args.User))
            return;

        args.CanDrop = true;
        args.Handled = true;
    }

    private void OnDragDropTarget(Entity<CMUWallLeanableComponent> ent, ref DragDropTargetEvent args)
    {
        if (args.User != args.Dragged || !HasComp<HumanoidProfileComponent>(args.User))
            return;

        args.Handled = true;
        if (_net.IsClient)
            return;

        TryLean(args.User, ent);
    }

    private void TryLean(EntityUid user, EntityUid wall)
    {
        if (HasComp<CMUWallLeaningComponent>(user))
            return;

        string? blocked = null;
        if (TryComp<PullerComponent>(user, out var puller) && puller.Pulling != null)
            blocked = "cmu-wall-lean-grabbing";
        else if (_mobState.IsIncapacitated(user))
            blocked = "cmu-wall-lean-incapacitated";
        else if (_standing.IsDown(user))
            blocked = "cmu-wall-lean-resting";
        else if (TryComp<BuckleComponent>(user, out var buckle) && buckle.Buckled)
            blocked = "cmu-wall-lean-buckled";

        if (blocked != null)
        {
            _popup.PopupEntity(Loc.GetString(blocked), user, user, PopupType.SmallCaution);
            return;
        }

        var wallPos = _transform.GetWorldPosition(wall);
        var userPos = _transform.GetWorldPosition(user);
        var diff = userPos - wallPos;
        Direction direction;
        if (MathF.Abs(diff.X) > MathF.Abs(diff.Y))
            direction = diff.X > 0 ? Direction.East : Direction.West;
        else
            direction = diff.Y > 0 ? Direction.North : Direction.South;

        var spot = wallPos + direction.ToVec();
        if ((spot - userPos).Length() > 0.75f)
            return;

        var offset = direction switch
        {
            Direction.North => new Vector2(0, -SideShift),
            Direction.South => new Vector2(0, UnderWallShift),
            Direction.East => new Vector2(-SideShift, 0),
            _ => new Vector2(SideShift, 0),
        };

        _transform.SetWorldPosition(user, spot);
        _transform.SetWorldRotation(user, direction.ToAngle());

        var leaning = EnsureComp<CMUWallLeaningComponent>(user);
        leaning.Wall = wall;
        leaning.Offset = offset;
        leaning.BehindWall = direction == Direction.North;
        leaning.Position = spot;
        Dirty(user, leaning);
        _physics.RegenerateContacts(user);
    }

    private void OnMoveInput(Entity<CMUWallLeaningComponent> ent, ref MoveInputEvent args)
    {
        if (_net.IsClient || !args.HasDirectionalMovement)
            return;

        StopLeaning(ent);
    }

    public void StopLeaning(Entity<CMUWallLeaningComponent> ent)
    {
        if (ent.Comp.Wall == null)
            return;

        ent.Comp.Wall = null;
        Dirty(ent);
        _physics.RegenerateContacts(ent.Owner);

        _slow.TrySuperSlowdown(ent, ent.Comp.SuperSlowAfter);
        _slow.TrySlowdown(ent, ent.Comp.SlowAfter);
        RemCompDeferred<CMUWallLeaningComponent>(ent);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<CMUWallLeaningComponent>();
        while (query.MoveNext(out var uid, out var leaning))
        {
            if (leaning.Wall is not { } wall)
                continue;

            if (TerminatingOrDeleted(wall) ||
                _mobState.IsIncapacitated(uid) ||
                _standing.IsDown(uid) ||
                TryComp<BuckleComponent>(uid, out var buckle) && buckle.Buckled ||
                (_transform.GetWorldPosition(uid) - leaning.Position).Length() > 0.1f)
            {
                StopLeaning((uid, leaning));
            }
        }
    }
}
