using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.CMU14.Ship;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.CMU14.ZLevels.Ghost;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Ship;

/// <summary>
///     While someone floats in zero-g aboard a <see cref="CMUZeroGSpaceComponent"/> ship, they get actions to push off
///     to the deck above or below, as long as there's open space to float through and room on the other side.
/// </summary>
public sealed partial class CMUZeroGMoverSystem : EntitySystem
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(0.5);

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private CMUZeroGSpaceSystem _zeroG = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUZeroGMoverComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CMUZeroGMoverComponent, CMUZLevelActionUp>(OnUp);
        SubscribeLocalEvent<CMUZeroGMoverComponent, CMUZLevelActionDown>(OnDown);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;

        _nextCheck = now + CheckInterval;

        var query = EntityQueryEnumerator<MobStateComponent, ActionsComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            var floating = _zeroG.IsFloating(uid);
            var hasMover = HasComp<CMUZeroGMoverComponent>(uid);
            if (floating && !hasMover)
            {
                var mover = AddComp<CMUZeroGMoverComponent>(uid);
                _actions.AddAction(uid, ref mover.UpAction, mover.UpActionId);
                _actions.AddAction(uid, ref mover.DownAction, mover.DownActionId);
            }
            else if (!floating && hasMover)
            {
                RemCompDeferred<CMUZeroGMoverComponent>(uid);
            }
        }
    }

    private void OnShutdown(Entity<CMUZeroGMoverComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.UpAction);
        _actions.RemoveAction(ent.Owner, ent.Comp.DownAction);
    }

    private void OnUp(Entity<CMUZeroGMoverComponent> ent, ref CMUZLevelActionUp args)
    {
        if (args.Handled)
            return;

        // Only a successful push-off counts, so a blocked attempt doesn't start the cooldown.
        args.Handled = TryFloat(ent, 1);
    }

    private void OnDown(Entity<CMUZeroGMoverComponent> ent, ref CMUZLevelActionDown args)
    {
        if (args.Handled)
            return;

        args.Handled = TryFloat(ent, -1);
    }

    private bool TryFloat(EntityUid uid, int offset)
    {
        if (!_zeroG.IsFloating(uid))
            return false;

        var xform = Transform(uid);
        var coordinates = _transform.GetMapCoordinates(uid, xform);
        if (xform.MapUid is not { } map ||
            !_zLevels.TryMapOffset(map, offset, out _, out var targetMap))
        {
            _popup.PopupEntity(Loc.GetString("cmu-zero-g-no-deck"), uid, uid);
            return false;
        }

        var targetCoordinates = new MapCoordinates(coordinates.Position, targetMap.MapId);

        // Going up passes through the floor of the deck above; going down, through the space under us.
        var gap = offset > 0 ? targetCoordinates : coordinates;
        if (!_zeroG.IsOpenAt(gap) || IsBlocked(targetCoordinates))
        {
            _popup.PopupEntity(Loc.GetString(offset > 0 ? "cmu-zero-g-blocked-up" : "cmu-zero-g-blocked-down"), uid, uid);
            return false;
        }

        return _zLevels.TryMove(uid, offset);
    }

    private bool IsBlocked(MapCoordinates coordinates)
    {
        if (!_map.TryFindGridAt(coordinates, out var gridUid, out var grid))
            return false;

        var tile = _map.WorldToTile(gridUid, grid, coordinates.Position);
        return _turf.IsTileBlocked(gridUid, tile, CollisionGroup.Impassable, grid);
    }
}

/// <summary>
///     Holds the up and down actions of someone floating in zero-g aboard a ship.
/// </summary>
[RegisterComponent]
public sealed partial class CMUZeroGMoverComponent : Component
{
    [DataField]
    public EntProtoId UpActionId = "CMUActionZeroGUp";

    [DataField]
    public EntityUid? UpAction;

    [DataField]
    public EntProtoId DownActionId = "CMUActionZeroGDown";

    [DataField]
    public EntityUid? DownAction;
}
