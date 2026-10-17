using System.Numerics;
using Content.Client.UserInterface.Systems.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.CMU14.Yautja;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client.CMU14.Yautja;

public sealed partial class YautjaAbilityPreviewSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        _overlay.AddOverlay(new YautjaAbilityPreviewOverlay(EntityManager));
    }

    public override void Shutdown()
    {
        _overlay.RemoveOverlay<YautjaAbilityPreviewOverlay>();
    }
}

public sealed class YautjaAbilityPreviewOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    private static readonly Color RangeColor = new(0.95f, 0.15f, 0.1f, 0.8f);
    private const float OutlineThickness = 0.1f;

    private readonly IPlayerManager _player;
    private readonly IUserInterfaceManager _ui;
    private readonly SharedMapSystem _map;
    private readonly SharedTransformSystem _transform;
    private readonly EntityQuery<EntityTargetActionComponent> _entityTargetQuery;
    private readonly EntityQuery<TargetActionComponent> _targetActionQuery;
    private readonly EntityQuery<TransformComponent> _transformQuery;

    public YautjaAbilityPreviewOverlay(IEntityManager entities)
    {
        _player = IoCManager.Resolve<IPlayerManager>();
        _ui = IoCManager.Resolve<IUserInterfaceManager>();
        _map = entities.System<SharedMapSystem>();
        _transform = entities.System<SharedTransformSystem>();
        _entityTargetQuery = entities.GetEntityQuery<EntityTargetActionComponent>();
        _targetActionQuery = entities.GetEntityQuery<TargetActionComponent>();
        _transformQuery = entities.GetEntityQuery<TransformComponent>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } player ||
            !_transformQuery.TryComp(player, out var xform) ||
            _ui.GetUIController<ActionUIController>().SelectingTargetFor is not { } action ||
            !_entityTargetQuery.TryComp(action, out var entityTarget) ||
            entityTarget.Event is not YautjaHuntingLeapActionEvent ||
            !_targetActionQuery.TryComp(action, out var targetAction))
        {
            return;
        }

        var origin = _transform.GetMapCoordinates(player, xform: xform);
        if (origin.MapId != args.MapId || !_map.TryFindGridAt(origin, out var gridUid, out var grid))
            return;

        var center = _map.CoordinatesToTile(gridUid, grid, origin);
        var tileSize = grid.TileSize;
        var halfTile = tileSize / 2f;
        var maxTiles = (int) MathF.Ceiling((targetAction.Range + halfTile) / tileSize);
        var tiles = new HashSet<Vector2i>();
        for (var x = -maxTiles; x <= maxTiles; x++)
        {
            for (var y = -maxTiles; y <= maxTiles; y++)
            {
                var indices = center + new Vector2i(x, y);
                var tileCenter = _map.GridTileToWorld(gridUid, grid, indices).Position;
                var delta = Vector2.Abs(tileCenter - origin.Position);
                var closest = Vector2.Max(delta - new Vector2(halfTile), Vector2.Zero);
                if (closest.Length() <= targetAction.Range)
                    tiles.Add(indices);
            }
        }

        DrawTileBorder(args.WorldHandle, gridUid, grid, tiles);
    }

    private void DrawTileBorder(DrawingHandleWorld handle,
        EntityUid gridUid,
        MapGridComponent grid,
        HashSet<Vector2i> tiles)
    {
        var tileSize = grid.TileSize;
        var tileSizeVector = new Vector2(tileSize);
        foreach (var indices in tiles)
        {
            var local = new Vector2(indices.X * tileSize, indices.Y * tileSize);
            var bottomLeft = _transform.ToMapCoordinates(new EntityCoordinates(gridUid, local)).Position;
            var bottomRight = _transform.ToMapCoordinates(new EntityCoordinates(gridUid, local + new Vector2(tileSize, 0))).Position;
            var topRight = _transform.ToMapCoordinates(new EntityCoordinates(gridUid, local + tileSizeVector)).Position;
            var topLeft = _transform.ToMapCoordinates(new EntityCoordinates(gridUid, local + new Vector2(0, tileSize))).Position;

            if (!tiles.Contains(indices + Vector2i.Up))
                DrawEdge(handle, topLeft, topRight);
            if (!tiles.Contains(indices + Vector2i.Down))
                DrawEdge(handle, bottomLeft, bottomRight);
            if (!tiles.Contains(indices + Vector2i.Right))
                DrawEdge(handle, bottomRight, topRight);
            if (!tiles.Contains(indices + Vector2i.Left))
                DrawEdge(handle, bottomLeft, topLeft);
        }
    }

    private static void DrawEdge(DrawingHandleWorld handle, Vector2 from, Vector2 to)
    {
        var delta = to - from;
        var half = OutlineThickness / 2f;
        if (Math.Abs(delta.X) < 0.001f)
        {
            handle.DrawRect(new Box2(from.X - half, Math.Min(from.Y, to.Y), from.X + half, Math.Max(from.Y, to.Y)), RangeColor);
            return;
        }

        handle.DrawRect(new Box2(Math.Min(from.X, to.X), from.Y - half, Math.Max(from.X, to.X), from.Y + half), RangeColor);
    }
}
