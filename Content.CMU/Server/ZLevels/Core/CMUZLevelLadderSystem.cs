using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._CMU14.ZLevels.Core;
using Content.Shared.DoAfter;
using Content.Shared.Ghost;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Map;

namespace Content.Server._CMU14.ZLevels.Core;

public sealed partial class CMUZLevelLadderSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private CMUZLevelsSystem _zLevels = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUZLevelLadderComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<CMUZLevelLadderComponent, DoAfterAttemptEvent<CMUZLevelLadderDoAfterEvent>>(OnDoAfterAttempt);
        SubscribeLocalEvent<CMUZLevelLadderComponent, CMUZLevelLadderDoAfterEvent>(OnDoAfter);
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

    private void Climb(Entity<CMUZLevelLadderComponent> ent, EntityUid user)
    {
        var ladderPosition = _transform.GetWorldPosition(ent);

        if (!_zLevels.TryMove(user, ent.Comp.Offset))
        {
            _popup.PopupClient(Loc.GetString("cmu-zlevel-ladder-no-level"), ent, user, PopupType.SmallCaution);
            return;
        }

        var userMap = Transform(user).MapID;
        _transform.SetMapCoordinates(user, new MapCoordinates(ladderPosition, userMap));

        if (TryComp<CMUZPhysicsComponent>(user, out var zPhysics))
        {
            _zLevels.SetZVelocity((user, zPhysics), 0f);
            _zLevels.SetZLocalPosition((user, zPhysics), ent.Comp.LandingLocalPosition);
        }

        var selfMessage = Loc.GetString("cmu-zlevel-ladder-finish-self");
        var othersMessage = Loc.GetString("cmu-zlevel-ladder-finish-others", ("user", user));
        _popup.PopupPredicted(selfMessage, othersMessage, user, user);
    }
}
