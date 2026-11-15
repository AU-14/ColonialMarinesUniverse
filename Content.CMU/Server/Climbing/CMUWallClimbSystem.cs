using System.Numerics;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.ActionBlocker;
using Content.Shared.Climbing.Components;
using Content.Shared.CMU14.Climbing;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Wall;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Climbing;

/// <summary>
/// Allows climbing an underground wall onto the level immediately above it.
/// </summary>
public sealed partial class CMUWallClimbSystem : EntitySystem
{
    public static readonly TimeSpan ClimbDelay = TimeSpan.FromSeconds(15);
    private const float ClimbRange = 1.5f;

    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private CMUZLevelsSystem _zLevels = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<WallComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<WallComponent, CMUWallClimbDoAfterEvent>(OnDoAfter);
    }

    private void OnGetVerbs(Entity<WallComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !TryGetLanding(args.User, ent, out _, out _))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-wall-climb-up"),
            Act = () => TryStartClimb(user, ent),
        });
    }

    public bool TryStartClimb(EntityUid user, EntityUid wall)
    {
        if (!TryGetLanding(user, wall, out _, out _))
            return false;

        var args = new DoAfterArgs(EntityManager, user, ClimbDelay, new CMUWallClimbDoAfterEvent(), wall, wall)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };
        if (!_doAfter.TryStartDoAfter(args))
            return false;

        _popup.PopupEntity(Loc.GetString("cmu-wall-climb-start"), user, user);
        return true;
    }

    private void OnDoAfter(Entity<WallComponent> ent, ref CMUWallClimbDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        if (!TryGetLanding(args.User, ent, out _, out var position) ||
            !_zLevels.TryMove(args.User, 1, worldPosition: position))
        {
            _popup.PopupEntity(Loc.GetString("cmu-wall-climb-blocked"), args.User, args.User);
            return;
        }

        if (TryComp<CMUZPhysicsComponent>(args.User, out var physics))
        {
            _zLevels.SetZVelocity((args.User, physics), 0f);
            _zLevels.SetZLocalPosition((args.User, physics), 0f);
        }
    }

    private bool TryGetLanding(EntityUid user, EntityUid wall,
        out Entity<CMUZLevelMapComponent> destination, out Vector2 position)
    {
        destination = default;
        position = default;
        if (TerminatingOrDeleted(user) || TerminatingOrDeleted(wall) || !HasComp<WallComponent>(wall) ||
            !TryComp<ClimbingComponent>(user, out var climbing) || !climbing.CanClimb ||
            !_blocker.CanMove(user) || !_blocker.CanInteract(user, wall) || _mobState.IsIncapacitated(user) ||
            _containers.IsEntityInContainer(user))
            return false;

        var xform = Transform(user);
        var wallXform = Transform(wall);
        if (!wallXform.Anchored || xform.MapUid != wallXform.MapUid ||
            xform.MapUid is not { } map || !TryComp<CMUZLevelMapComponent>(map, out var level) ||
            level.Depth >= 0 || !_zLevels.TryMapOffset((map, level), 1, out var above))
            return false;

        var origin = _transform.GetMapCoordinates(user);
        var wallCoords = _transform.GetMapCoordinates(wall);
        if (!_interaction.InRangeUnobstructed(origin, wallCoords, ClimbRange,
                predicate: entity => entity == user || entity == wall))
            return false;

        destination = above.Value;
        position = wallCoords.Position;
        var upperOrigin = new MapCoordinates(origin.Position, Transform(destination).MapID);
        var upperTarget = new MapCoordinates(position, upperOrigin.MapId);

        // Climb through the opening beside the wall, then step onto its top. A solid
        // ceiling over the climber or an obstructed landing must never be bypassed.
        return _zLevels.IsZShotPathOpen(destination, origin.Position, origin.Position, map) &&
               _interaction.InRangeUnobstructed(upperOrigin, upperTarget, ClimbRange, CollisionGroup.MobMask) &&
               _zLevels.HasClimbLanding(user, destination, position, 0f);
    }
}
