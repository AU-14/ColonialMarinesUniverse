using Content.Server.Administration.Logs;
using Content.Server.Power.Components;
using Content.Shared.CMU14.Cables; // RUMC: the layer of a cable (on the top of a tile, or under it)
using Content.Shared.Database;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Stacks;
using Content.Shared.Whitelist;
using Robust.Shared.Map.Components;

namespace Content.Server.Power.EntitySystems;

public sealed partial class CableSystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private EntityWhitelistSystem _whitelistSystem = default!;

    private void InitializeCablePlacer()
    {
        SubscribeLocalEvent<CablePlacerComponent, AfterInteractEvent>(OnCablePlacerAfterInteract);
    }

    private void OnCablePlacerAfterInteract(Entity<CablePlacerComponent> placer, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        var component = placer.Comp;
        if (!TryComp<MapGridComponent>(_transform.GetGrid(args.ClickLocation), out var grid))
            return;

        var gridUid = _transform.GetGrid(args.ClickLocation)!.Value;
        var snapPos = _map.TileIndicesFor((gridUid, grid), args.ClickLocation);
        var tileDef = (ContentTileDefinition)_tileManager[_map.GetTileRef(gridUid, grid, snapPos).Tile.TypeId];

        // RUMC: a cable is laid on the top layer of any sturdy tile; a screwdriver tucks it under the tile later.
        // RUMC: the original condition is kept here: if ((!component.OverTile && !tileDef.IsSubFloor) || !tileDef.Sturdy)
        if (!tileDef.Sturdy)
            return;

        foreach (var anchored in _map.GetAnchoredEntities((gridUid, grid), snapPos))
        {
            if (_whitelistSystem.IsWhitelistPass(component.Blacklist, anchored))
                return;

            if (TryComp<CableComponent>(anchored, out var wire) && wire.CableType == component.BlockingCableType)
                return;
        }

        if (TryComp<StackComponent>(placer, out var stack) && !_stack.TryUse((placer.Owner, stack), 1))
            return;

        // RUMC: the layer is chosen before the cable starts, so that the floor does not block anchoring it.
        // RUMC: the original line is kept here: var newCable = Spawn(component.CablePrototypeId, _map.GridTileToLocal(gridUid, grid, snapPos));
        var newCable = EntityManager.CreateEntityUninitialized(component.CablePrototypeId, _map.GridTileToLocal(gridUid, grid, snapPos));
        AddComp<CableLayerComponent>(newCable).Buried = false;
        EntityManager.InitializeAndStartEntity(newCable);
        _adminLogger.Add(LogType.Construction, LogImpact.Low,
            $"{ToPrettyString(args.User):player} placed {ToPrettyString(newCable):cable} at {Transform(newCable).Coordinates}");
        args.Handled = true;
    }
}
