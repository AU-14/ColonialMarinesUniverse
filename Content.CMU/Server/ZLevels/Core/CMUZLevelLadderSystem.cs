using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._CMU14.ZLevels.Core;
using Content.Shared.DoAfter;
using Content.Shared.Ghost;
using Content.Shared.Interaction;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Server._CMU14.ZLevels.Core;

public sealed partial class CMUZLevelLadderSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private CMUZLevelsSystem _zLevels = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUZLevelLadderComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<CMUZLevelLadderComponent, DoAfterAttemptEvent<CMUZLevelLadderDoAfterEvent>>(OnDoAfterAttempt);
        SubscribeLocalEvent<CMUZLevelLadderComponent, CMUZLevelLadderDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<CMUZLevelLadderComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);
        SubscribeLocalEvent<CMUZLevelLadderComponent, ComponentRemove>(OnLadderRemove);
        SubscribeLocalEvent<CMUZLevelLadderComponent, EntityTerminatingEvent>(OnLadderRemove);

        SubscribeLocalEvent<CMUZLevelLadderWatchingComponent, MoveInputEvent>(OnWatchingMoveInput);
        SubscribeLocalEvent<CMUZLevelLadderWatchingComponent, ComponentRemove>(OnWatchingRemove);
        SubscribeLocalEvent<CMUZLevelLadderWatchingComponent, EntityTerminatingEvent>(OnWatchingRemove);
    }

    private void OnActivate(Entity<CMUZLevelLadderComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var user = args.User;
        var delay = HasComp<GhostComponent>(user) ? TimeSpan.Zero : ent.Comp.Delay;
        var doAfter = new DoAfterArgs(EntityManager, user, delay, new CMUZLevelLadderDoAfterEvent(), ent, ent, ent)
        {
            AttemptFrequency = delay == TimeSpan.Zero ? AttemptFrequency.Never : AttemptFrequency.EveryTick,
            BlockDuplicate = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        if (delay > TimeSpan.Zero)
        {
            var selfMessage = Loc.GetString("cmu-zlevel-ladder-start-self");
            var othersMessage = Loc.GetString("cmu-zlevel-ladder-start-others", ("user", user));
            _popup.PopupPredicted(selfMessage, othersMessage, user, user);
        }
    }

    private void OnDoAfterAttempt(Entity<CMUZLevelLadderComponent> ent, ref DoAfterAttemptEvent<CMUZLevelLadderDoAfterEvent> args)
    {
        if (args.Cancelled)
            return;

        var user = args.DoAfter.Args.User;
        var userCoords = _transform.GetMapCoordinates(user);
        var ladderCoords = _transform.GetMapCoordinates(ent);
        if (userCoords.MapId != ladderCoords.MapId ||
            (userCoords.Position - ladderCoords.Position).Length() > ent.Comp.Range)
        {
            args.Cancel();
            return;
        }

        if (Transform(user).Anchored)
            args.Cancel();
    }

    private void OnDoAfter(Entity<CMUZLevelLadderComponent> ent, ref CMUZLevelLadderDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        Climb(ent, args.User);
    }

    private void OnGetAltVerbs(Entity<CMUZLevelLadderComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        var user = args.User;
        if (!HasComp<EyeComponent>(user) ||
            !CanWatchPopup(ent, user) ||
            !TryGetLookCoordinates(ent, out _))
        {
            return;
        }

        args.Verbs.Add(new AlternativeVerb
        {
            Priority = 100,
            Act = () =>
            {
                if (!CanWatchPopup(ent, user))
                    return;

                ToggleLook(user, ent);
            },
            Text = Loc.GetString(ent.Comp.Offset > 0
                ? "cmu-zlevel-ladder-look-up"
                : "cmu-zlevel-ladder-look-down"),
        });
    }

    private void OnLadderRemove<T>(Entity<CMUZLevelLadderComponent> ent, ref T args)
    {
        var query = EntityQueryEnumerator<CMUZLevelLadderWatchingComponent>();
        while (query.MoveNext(out var uid, out var watching))
        {
            if (watching.Ladder == ent.Owner)
                CloseLook(uid, watching);
        }
    }

    private void OnWatchingMoveInput(Entity<CMUZLevelLadderWatchingComponent> ent, ref MoveInputEvent args)
    {
        if (!args.HasDirectionalMovement)
            return;

        CloseLook(ent.Owner, ent.Comp);
    }

    private void OnWatchingRemove<T>(Entity<CMUZLevelLadderWatchingComponent> ent, ref T args)
    {
        CleanupLook(ent.Owner, ent.Comp);
    }

    private void Climb(Entity<CMUZLevelLadderComponent> ent, EntityUid user)
    {
        CloseLook(user);

        var ladderPosition = _transform.GetWorldPosition(ent);

        if (!_zLevels.TryMove(user, ent.Comp.Offset, worldPosition: ladderPosition))
        {
            _popup.PopupClient(Loc.GetString("cmu-zlevel-ladder-no-level"), ent, user, PopupType.SmallCaution);
            return;
        }

        if (TryComp<CMUZPhysicsComponent>(user, out var zPhysics))
        {
            _zLevels.SetZVelocity((user, zPhysics), 0f);
            _zLevels.SetZLocalPosition((user, zPhysics), ent.Comp.LandingLocalPosition);
        }

        var selfMessage = Loc.GetString("cmu-zlevel-ladder-finish-self");
        var othersMessage = Loc.GetString("cmu-zlevel-ladder-finish-others", ("user", user));
        _popup.PopupPredicted(selfMessage, othersMessage, user, user);
    }

    private void ToggleLook(EntityUid user, Entity<CMUZLevelLadderComponent> ladder)
    {
        if (TryComp(user, out CMUZLevelLadderWatchingComponent? existing))
        {
            var sameLadder = existing.Ladder == ladder.Owner;
            CloseLook(user, existing);

            if (sameLadder)
                return;
        }

        if (!TryComp(user, out EyeComponent? eye))
            return;

        if (!TryGetLookCoordinates(ladder, out var coordinates))
        {
            _popup.PopupClient(Loc.GetString("cmu-zlevel-ladder-no-level"), ladder, user, PopupType.SmallCaution);
            return;
        }

        var peekTarget = Spawn(null, coordinates);
        var watching = EnsureComp<CMUZLevelLadderWatchingComponent>(user);
        watching.Ladder = ladder;
        watching.PeekTarget = peekTarget;
        watching.PreviousTarget = eye.Target;

        _eye.SetTarget(user, peekTarget, eye);
    }

    private void CloseLook(EntityUid user, CMUZLevelLadderWatchingComponent? watching = null)
    {
        if (!Resolve(user, ref watching, false))
            return;

        CleanupLook(user, watching);
        RemCompDeferred<CMUZLevelLadderWatchingComponent>(user);
    }

    private void CleanupLook(EntityUid user, CMUZLevelLadderWatchingComponent watching)
    {
        if (watching.Ladder == null &&
            watching.PeekTarget == null &&
            watching.PreviousTarget == null)
        {
            return;
        }

        if (TryComp(user, out EyeComponent? eye))
            _eye.SetTarget(user, watching.PreviousTarget, eye);

        if (watching.PeekTarget is { } peekTarget &&
            Exists(peekTarget))
        {
            QueueDel(peekTarget);
        }

        watching.Ladder = null;
        watching.PeekTarget = null;
        watching.PreviousTarget = null;
    }

    private bool CanWatchPopup(Entity<CMUZLevelLadderComponent> ladder, EntityUid user)
    {
        if (!_interaction.InRangeUnobstructed(user, ladder.Owner, ladder.Comp.Range, popup: true))
            return false;

        return true;
    }

    private bool TryGetLookCoordinates(Entity<CMUZLevelLadderComponent> ladder, out MapCoordinates coordinates)
    {
        coordinates = default;

        if (Transform(ladder).MapUid is not { } map)
            return false;

        return _zLevels.TryProjectToZMap(
            (map, null),
            ladder.Comp.Offset,
            _transform.GetWorldPosition(ladder),
            out coordinates,
            out _);
    }
}
