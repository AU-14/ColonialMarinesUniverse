using System.Numerics;
using Content.Client.Examine;
using Content.Client.Resources;
using Content.Client.Viewport;
using Content.Shared._CMU14.ZLevels;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Client._CMU14.ZLevels.Core;

public sealed partial class CMUZLevelVisibleEntityOverlay : Overlay
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IConfigurationManager _config = default!;

    private readonly SharedContainerSystem _container;
    private readonly EntityLookupSystem _lookup;
    private readonly ExamineSystem _examine;
    private readonly MobStateSystem _mobState;
    private readonly SpriteSystem _sprite;
    private readonly SharedTransformSystem _transform;
    private readonly EntityQuery<HumanoidAppearanceComponent> _humanoidQuery;
    private readonly EntityQuery<XenoComponent> _xenoQuery;
    private readonly EntityQuery<MobStateComponent> _mobStateQuery;
    private readonly EntityQuery<SpriteComponent> _spriteQuery;
    private readonly EntityQuery<TransformComponent> _xformQuery;
    private readonly HashSet<EntityUid> _candidates = new();
    private readonly List<Vector2> _screenLabels = new();
    private readonly Font _font;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV | OverlaySpace.ScreenSpace;

    public CMUZLevelVisibleEntityOverlay()
    {
        IoCManager.InjectDependencies(this);

        var cache = IoCManager.Resolve<IResourceCache>();
        _font = cache.GetFont("/Fonts/NotoSans/NotoSans-Bold.ttf", 18);

        _container = _entMan.System<SharedContainerSystem>();
        _lookup = _entMan.System<EntityLookupSystem>();
        _examine = _entMan.System<ExamineSystem>();
        _mobState = _entMan.System<MobStateSystem>();
        _sprite = _entMan.System<SpriteSystem>();
        _transform = _entMan.System<SharedTransformSystem>();
        _humanoidQuery = _entMan.GetEntityQuery<HumanoidAppearanceComponent>();
        _xenoQuery = _entMan.GetEntityQuery<XenoComponent>();
        _mobStateQuery = _entMan.GetEntityQuery<MobStateComponent>();
        _spriteQuery = _entMan.GetEntityQuery<SpriteComponent>();
        _xformQuery = _entMan.GetEntityQuery<TransformComponent>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!_config.GetCVar(CMUZLevelsCVars.Enabled) ||
            !_config.GetCVar(CMUZLevelsCVars.VisibleEntityIndicators))
        {
            return;
        }

        if (args.Viewport.Eye is not ScalingViewport.ZEye zEye ||
            !zEye.DrawVisibleEntityIndicators)
        {
            return;
        }

        if (args.Space == OverlaySpace.ScreenSpace)
        {
            DrawLabels(args);
            return;
        }

        _screenLabels.Clear();

        if (_player.LocalEntity is not { } player ||
            args.ViewportControl == null ||
            zEye.BaseMapId == MapId.Nullspace ||
            zEye.VisibleEntityIndicatorBounds.Count == 0)
        {
            return;
        }

        var origin = new MapCoordinates(zEye.Position.Position, zEye.BaseMapId);

        _candidates.Clear();
        _lookup.GetEntitiesIntersecting(args.MapId, args.WorldAABB, _candidates, LookupFlags.Uncontained);

        foreach (var uid in _candidates)
        {
            if (uid == player ||
                !_spriteQuery.TryComp(uid, out var sprite) ||
                !_xformQuery.TryComp(uid, out var xform) ||
                xform.MapID != args.MapId ||
                !sprite.Visible)
            {
                continue;
            }

            if (!_humanoidQuery.HasComp(uid) && !_xenoQuery.HasComp(uid))
                continue;

            if (_container.IsEntityOrParentInContainer(uid))
                continue;

            if (_mobStateQuery.TryComp(uid, out var mobState) && _mobState.IsDead(uid, mobState))
                continue;

            var worldPos = _transform.GetWorldPosition(xform, _xformQuery);
            var bounds = _sprite.GetLocalBounds((uid, sprite));
            var worldBounds = bounds.Translated(worldPos);

            if (!worldBounds.Intersects(args.WorldAABB) ||
                !IntersectsAnyOpening(worldBounds, zEye.VisibleEntityIndicatorBounds))
            {
                continue;
            }

            var targetOnBaseMap = new MapCoordinates(worldPos, zEye.BaseMapId);
            if (!_examine.InRangeUnOccluded(origin, targetOnBaseMap, 0f, null))
                continue;

            AddPlayerIndicator(player, zEye, args);
            return;
        }
    }

    private void DrawLabels(in OverlayDrawArgs args)
    {
        if (_screenLabels.Count == 0)
            return;

        foreach (var screenPosition in _screenLabels)
        {
            args.ScreenHandle.DrawString(_font, screenPosition + Vector2.One, "!", Color.Black);
            args.ScreenHandle.DrawString(_font, screenPosition, "!", Color.Yellow);
        }
    }

    private void AddPlayerIndicator(EntityUid player, ScalingViewport.ZEye zEye, in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null ||
            !_spriteQuery.TryComp(player, out var sprite) ||
            !_xformQuery.TryComp(player, out var xform))
        {
            return;
        }

        var worldPos = _transform.GetWorldPosition(xform, _xformQuery);
        var bounds = _sprite.GetLocalBounds((player, sprite));
        var topCenter = new Vector2(worldPos.X + bounds.Center.X, worldPos.Y + bounds.Top + 0.25f);
        Angle rotation = zEye.Rotation * -1;
        var zPassOffset = rotation.ToWorldVec() * CMUClientZLevelsSystem.ZLevelOffset * zEye.Depth;
        var screenPosition = args.ViewportControl.WorldToScreen(topCenter - zPassOffset) - new Vector2(5f, 24f);

        _screenLabels.Add(screenPosition);
    }

    private static bool IntersectsAnyOpening(Box2 bounds, IReadOnlyList<Box2> openings)
    {
        foreach (var opening in openings)
        {
            if (bounds.Intersects(opening))
                return true;
        }

        return false;
    }
}
