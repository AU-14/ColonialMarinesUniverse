using System.Numerics;
using Content.Client._CMU14.ZLevels.Core;
using Content.Shared._CMU14.ZLevels;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Maps;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.Client._CMU14.ZLevels.Culling;

/// <summary>
/// Performs content-side dynamic sprite culling for lower Z-level render passes.
/// </summary>
public sealed partial class CMUZLevelSpriteCullingSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private ITileDefinitionManager _tile = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private CMUClientZLevelsSystem _zLevels = default!;

    private readonly List<Box2> _openingBounds = new();
    private List<Entity<MapGridComponent>> _openingGrids = new();
    private readonly HashSet<EntityUid> _hiddenSprites = new();
    private readonly HashSet<EntityUid> _stillHidden = new();
    private readonly Dictionary<EntityUid, bool> _hiddenSpriteOriginalVisibility = new();
    private readonly List<EntityUid> _restoreScratch = new();

    private EntityQuery<MapComponent> _mapQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    /// <inheritdoc />
    public override void Initialize()
    {
        base.Initialize();

        _zLevels = EntityManager.System<CMUClientZLevelsSystem>();
        _mapQuery = GetEntityQuery<MapComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
    }

    /// <inheritdoc />
    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (!_config.GetCVar(CMUZLevelsCVars.Enabled) ||
            !_config.GetCVar(CMUZLevelsCVars.RenderEnabled) ||
            !_config.GetCVar(CMUZLevelsCVars.CullOccludedDynamicSprites))
        {
            RestoreAllHiddenSprites();
            return;
        }

        if (_player.LocalEntity is not { } playerUid ||
            !TryComp<CMUZLevelViewerComponent>(playerUid, out _) ||
            !_xformQuery.TryComp(playerUid, out var playerXform) ||
            playerXform.MapUid is not { } playerMapUid ||
            !_mapQuery.TryComp(playerMapUid, out var playerMapComp))
        {
            RestoreAllHiddenSprites();
            return;
        }

        var maxOpeningRects = Math.Max(0, _config.GetCVar(CMUZLevelsCVars.MaxOpeningRectsPerPass));
        var maxDepth = Math.Clamp(
            _config.GetCVar(CMUZLevelsCVars.MaxRenderDepth),
            0,
            CMUSharedZLevelsSystem.MaxZLevelsBelowRendering);

        var viewBounds = _eyeManager.GetWorldViewbounds().CalcBoundingBox();
        var openingLimit = maxOpeningRects == 0 ? int.MaxValue : maxOpeningRects + 1;
        if (!TryFindOpeningBounds(playerMapComp.MapId, viewBounds, openingLimit) ||
            _openingBounds.Count == 0 ||
            maxOpeningRects > 0 && _openingBounds.Count > maxOpeningRects)
        {
            RestoreAllHiddenSprites();
            return;
        }

        _stillHidden.Clear();

        for (var depthOffset = -1; depthOffset >= -maxDepth; depthOffset--)
        {
            if (!_zLevels.TryMapOffset(playerMapUid, depthOffset, out _, out var lowerMapComp) ||
                lowerMapComp.MapId == MapId.Nullspace)
            {
                continue;
            }

            CullDynamicSpritesOnMap(lowerMapComp.MapId);
        }

        RestoreNoLongerHiddenSprites();
    }

    private void CullDynamicSpritesOnMap(MapId mapId)
    {
        var query = EntityQueryEnumerator<SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var sprite, out var xform))
        {
            if (xform.MapID != mapId || xform.Anchored)
                continue;

            var hiddenByUs = _hiddenSprites.Contains(uid);
            if (!sprite.Visible && !hiddenByUs)
                continue;

            var worldBounds = GetApproximateSpriteBounds(uid, sprite, xform);
            if (IntersectsAnyOpening(worldBounds))
            {
                RestoreHiddenSprite(uid, sprite);
                continue;
            }

            HideSprite(uid, sprite);
        }
    }

    private Box2 GetApproximateSpriteBounds(EntityUid uid, SpriteComponent sprite, TransformComponent xform)
    {
        var worldPos = _transform.GetWorldPosition(xform, _xformQuery);
        var localBounds = _sprite.GetLocalBounds((uid, sprite));
        var radius = localBounds.Size.Length() * 0.5f + sprite.Offset.Length() + 0.25f;
        return new Box2(
            worldPos.X - radius,
            worldPos.Y - radius,
            worldPos.X + radius,
            worldPos.Y + radius);
    }

    private bool TryFindOpeningBounds(
        MapId mapId,
        Box2 worldAabb,
        int maxOpeningBounds)
    {
        _openingBounds.Clear();

        return _zLevels.OpeningCache.TryFindOpeningBounds(
            mapId,
            worldAabb,
            _openingBounds,
            out _,
            maxOpeningBounds,
            true,
            _openingGrids,
            _mapManager,
            _map,
            _transform,
            _tile);
    }

    private bool IntersectsAnyOpening(Box2 worldBounds)
    {
        foreach (var opening in _openingBounds)
        {
            if (worldBounds.Intersects(opening))
                return true;
        }

        return false;
    }

    private void HideSprite(EntityUid uid, SpriteComponent sprite)
    {
        _stillHidden.Add(uid);

        if (_hiddenSprites.Add(uid))
            _hiddenSpriteOriginalVisibility[uid] = sprite.Visible;

        if (sprite.Visible)
            _sprite.SetVisible((uid, sprite), false);
    }

    private void RestoreHiddenSprite(EntityUid uid, SpriteComponent sprite)
    {
        if (!_hiddenSprites.Remove(uid))
            return;

        var restoreVisible = _hiddenSpriteOriginalVisibility.Remove(uid, out var wasVisible) && wasVisible;
        if (restoreVisible && !sprite.Visible)
            _sprite.SetVisible((uid, sprite), true);
    }

    private void RestoreNoLongerHiddenSprites()
    {
        _restoreScratch.Clear();

        foreach (var uid in _hiddenSprites)
        {
            if (!_stillHidden.Contains(uid))
                _restoreScratch.Add(uid);
        }

        foreach (var uid in _restoreScratch)
        {
            if (TryComp<SpriteComponent>(uid, out var sprite))
                RestoreHiddenSprite(uid, sprite);
            else
            {
                _hiddenSprites.Remove(uid);
                _hiddenSpriteOriginalVisibility.Remove(uid);
            }
        }
    }

    private void RestoreAllHiddenSprites()
    {
        foreach (var uid in _hiddenSprites)
        {
            var restoreVisible = _hiddenSpriteOriginalVisibility.TryGetValue(uid, out var wasVisible) && wasVisible;
            if (restoreVisible && TryComp<SpriteComponent>(uid, out var sprite) && !sprite.Visible)
                _sprite.SetVisible((uid, sprite), true);
        }

        _hiddenSprites.Clear();
        _stillHidden.Clear();
        _hiddenSpriteOriginalVisibility.Clear();
    }

    /// <inheritdoc />
    public override void Shutdown()
    {
        base.Shutdown();
        RestoreAllHiddenSprites();
    }
}
