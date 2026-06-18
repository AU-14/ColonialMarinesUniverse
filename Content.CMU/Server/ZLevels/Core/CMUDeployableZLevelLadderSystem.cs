using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;

namespace Content.Server._CMU14.ZLevels.Core;

public sealed partial class CMUDeployableZLevelLadderSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private CMUZLevelsSystem _zLevels = default!;

    private const int UpperOffset = 1;

    private readonly HashSet<Entity<CMUZLevelLadderComponent>> _ladders = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUDeployableZLevelLadderComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<CMUDeployableZLevelLadderComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnAfterInteract(Entity<CMUDeployableZLevelLadderComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        args.Handled = true;
        TryDeploy(ent, args.User, args.ClickLocation);
    }

    private void OnUseInHand(Entity<CMUDeployableZLevelLadderComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        TryDeploy(ent, args.User, _transform.GetMoverCoordinates(args.User));
    }

    private void TryDeploy(
        Entity<CMUDeployableZLevelLadderComponent> ent,
        EntityUid user,
        EntityCoordinates location)
    {
        var snapped = _transform.GetMoverCoordinates(location).SnapToGrid();
        var lowerMapCoordinates = _transform.ToMapCoordinates(snapped);

        if (!TryGetTileCenter(lowerMapCoordinates, out var lowerCoordinates, out var lowerMap) ||
            HasLadderAt(_transform.ToMapCoordinates(lowerCoordinates), ent.Comp.ExistingLadderRadius))
        {
            Popup(user, "cmu-deployable-z-ladder-blocked");
            return;
        }

        if (!_zLevels.TryProjectToZMap(
                (lowerMap, null),
                UpperOffset,
                _transform.ToMapCoordinates(lowerCoordinates).Position,
                out var upperMapCoordinates,
                out _))
        {
            Popup(user, "cmu-deployable-z-ladder-no-level");
            return;
        }

        if (!TryGetTileCenter(upperMapCoordinates, out var upperCoordinates, out _) ||
            HasLadderAt(_transform.ToMapCoordinates(upperCoordinates), ent.Comp.ExistingLadderRadius))
        {
            Popup(user, "cmu-deployable-z-ladder-blocked");
            return;
        }

        Spawn(ent.Comp.LowerPrototype, lowerCoordinates);
        Spawn(ent.Comp.UpperPrototype, upperCoordinates);

        _hands.TryDrop(user, ent.Owner);
        QueueDel(ent);
        Popup(user, "cmu-deployable-z-ladder-success", PopupType.Small);
    }

    private bool TryGetTileCenter(
        MapCoordinates coordinates,
        out EntityCoordinates tileCenter,
        out EntityUid mapUid)
    {
        tileCenter = default;
        mapUid = default;

        if (!_map.TryGetMap(coordinates.MapId, out var map) ||
            map is not { } resolvedMap ||
            !TryComp<MapGridComponent>(resolvedMap, out var grid))
        {
            return false;
        }

        var tile = _map.WorldToTile(resolvedMap, grid, coordinates.Position);
        if (!_map.TryGetTileRef(resolvedMap, grid, tile, out var tileRef) ||
            tileRef.Tile.IsEmpty ||
            _turf.IsSpace(tileRef))
        {
            return false;
        }

        tileCenter = _map.GridTileToLocal(resolvedMap, grid, tile);
        mapUid = resolvedMap;
        return true;
    }

    private bool HasLadderAt(MapCoordinates coordinates, float radius)
    {
        _ladders.Clear();
        _lookup.GetEntitiesInRange(
            coordinates.MapId,
            coordinates.Position,
            radius,
            _ladders,
            LookupFlags.Static | LookupFlags.StaticSundries);

        return _ladders.Count > 0;
    }

    private void Popup(EntityUid user, string message, PopupType type = PopupType.SmallCaution)
    {
        _popup.PopupClient(Loc.GetString(message), user, user, type);
    }
}
