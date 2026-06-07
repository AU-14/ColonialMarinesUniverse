using System.Numerics;
using Content.Shared._CMU14.ZLevels;
using Content.Shared._CMU14.ZLevels.Core;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Camera;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client._CMU14.ZLevels.Core;

/// <summary>
/// Only process Eye offset and drawdepth on clientside
/// </summary>
public sealed partial class CMUClientZLevelsSystem : CMUSharedZLevelsSystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private SharedTransformSystem _transformSystem = default!;

    public static float ZLevelOffset = CMUSharedZLevelsSystem.ZLevelVisualOffset;

    private CMUZLevelVisibleEntityOverlay? _visibleEntityOverlay;

    public CMUZLevelOpeningCache OpeningCache { get; } = new();

    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new CMUZLevelBlurOverlay());
        _visibleEntityOverlay = new CMUZLevelVisibleEntityOverlay();
        _overlay.AddOverlay(_visibleEntityOverlay);

        SubscribeLocalEvent<CMUZPhysicsComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CMUZPhysicsComponent, MoveEvent>(OnZPhysicsMoveGroundSnapClient);
        SubscribeLocalEvent<CMUZPhysicsComponent, GetEyeOffsetEvent>(OnEyeOffset);
        SubscribeLocalEvent<CMUZFallingComponent, ComponentShutdown>(OnFallingShutdown);
        SubscribeLocalEvent<CMUZLevelProjectileVisualOffsetComponent, ComponentStartup>(OnProjectileVisualOffsetStartup);
        SubscribeLocalEvent<CMUZLevelProjectileVisualOffsetComponent, ComponentShutdown>(OnProjectileVisualOffsetShutdown);
        SubscribeLocalEvent<CMUZLevelPredictedProjectileVisualOffsetComponent, ComponentStartup>(OnPredictedProjectileVisualOffsetStartup);
        SubscribeLocalEvent<CMUZLevelPredictedProjectileVisualOffsetComponent, ComponentShutdown>(OnPredictedProjectileVisualOffsetShutdown);
        SubscribeLocalEvent<GridRemovalEvent>(OnGridShutdown);
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
    }

    private void OnGridShutdown(GridRemovalEvent args)
    {
        InvalidateSharedOpeningCache(args.EntityUid);
        OpeningCache.RemoveGrid(args.EntityUid);
    }

    private void OnTileChanged(ref TileChangedEvent args)
    {
        InvalidateSharedOpeningCache(ref args);
        OpeningCache.InvalidateTiles(args.Entity, args.Changes);
    }

    private void OnEyeOffset(Entity<CMUZPhysicsComponent> ent, ref GetEyeOffsetEvent args)
    {
        if (!_config.GetCVar(CMUZLevelsCVars.Enabled))
            return;

        Angle rotation = _eye.CurrentEye.Rotation * -1;
        var offset = rotation.RotateVec(new Vector2(0, ent.Comp.LocalPosition * ZLevelOffset));
        args.Offset += offset;
    }

    private void OnZPhysicsMoveGroundSnapClient(Entity<CMUZPhysicsComponent> ent, ref MoveEvent args)
    {
        OnZPhysicsMoveGroundSnap(ent, ref args);

        if (!_config.GetCVar(CMUZLevelsCVars.Enabled) ||
            !TryComp<SpriteComponent>(ent, out var sprite))
        {
            return;
        }

        ApplyZPhysicsVisuals(ent.Owner, ent.Comp, sprite);
    }

    private void OnFallingShutdown(Entity<CMUZFallingComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<CMUZPhysicsComponent>(ent, out var zPhys) ||
            !TryComp<SpriteComponent>(ent, out var sprite))
        {
            return;
        }

        if (MathF.Abs(zPhys.LocalPosition) > 0.001f)
            return;

        sprite.NoRotation = zPhys.NoRotDefault;
        _sprite.SetOffset((ent.Owner, sprite), zPhys.SpriteOffsetDefault);
        _sprite.SetDrawDepth((ent.Owner, sprite), zPhys.DrawDepthDefault);
    }

    private void OnProjectileVisualOffsetStartup(Entity<CMUZLevelProjectileVisualOffsetComponent> ent, ref ComponentStartup args)
    {
        TryApplyProjectileVisualOffset(
            ent.Owner,
            ent.Comp.Offset,
            ref ent.Comp.OriginalOffset,
            ref ent.Comp.AppliedOffset);
    }

    private void OnProjectileVisualOffsetShutdown(Entity<CMUZLevelProjectileVisualOffsetComponent> ent, ref ComponentShutdown args)
    {
        RestoreProjectileVisualOffset(ent.Owner, ent.Comp.OriginalOffset);
    }

    private void OnPredictedProjectileVisualOffsetStartup(Entity<CMUZLevelPredictedProjectileVisualOffsetComponent> ent, ref ComponentStartup args)
    {
        TryApplyProjectileVisualOffset(
            ent.Owner,
            ent.Comp.Offset,
            ref ent.Comp.OriginalOffset,
            ref ent.Comp.AppliedOffset);
    }

    private void OnPredictedProjectileVisualOffsetShutdown(Entity<CMUZLevelPredictedProjectileVisualOffsetComponent> ent, ref ComponentShutdown args)
    {
        RestoreProjectileVisualOffset(ent.Owner, ent.Comp.OriginalOffset);
    }

    private void RestoreProjectileVisualOffset(EntityUid uid, Vector2? originalOffset)
    {
        if (originalOffset is { } original &&
            TryComp<SpriteComponent>(uid, out var sprite))
        {
            _sprite.SetOffset((uid, sprite), original);
        }
    }

    private void OnStartup(Entity<CMUZPhysicsComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (sprite.SnapCardinals)
            return;

        ent.Comp.NoRotDefault = sprite.NoRotation;
        ent.Comp.DrawDepthDefault = sprite.DrawDepth;
        ent.Comp.SpriteOffsetDefault = sprite.Offset;
    }

    public bool TryGetSpeechBubbleZOffset(
        EntityUid speaker,
        out Vector2 zPassOffset,
        TransformComponent? speakerXform = null)
    {
        zPassOffset = default;

        if (!_config.GetCVar(CMUZLevelsCVars.Enabled) ||
            !_config.GetCVar(CMUZLevelsCVars.RenderEnabled))
        {
            return false;
        }

        if (speakerXform == null &&
            !TryComp(speaker, out speakerXform))
        {
            return false;
        }

        if (speakerXform.MapUid is not { } speakerMap)
            return false;

        if (speakerXform.MapID == _eye.CurrentEye.Position.MapId)
            return true;

        if (_player.LocalEntity is not { } player ||
            !TryComp<CMUZLevelViewerComponent>(player, out var viewer) ||
            !TryComp(player, out TransformComponent? playerXform) ||
            playerXform.MapUid is not { } playerMap ||
            !TryComp<CMUZLevelMapComponent>(playerMap, out var playerZMap) ||
            !TryComp<CMUZLevelMapComponent>(speakerMap, out var speakerZMap) ||
            speakerZMap.NetworkUid != playerZMap.NetworkUid)
        {
            return false;
        }

        var depthOffset = speakerZMap.Depth - playerZMap.Depth;
        if (depthOffset == 0)
            return true;

        if (depthOffset > 0)
        {
            if (depthOffset != 1 ||
                !viewer.LookUp && !viewer.StairPreviewUp)
            {
                return false;
            }
        }
        else
        {
            var maxDepth = Math.Clamp(
                _config.GetCVar(CMUZLevelsCVars.MaxRenderDepth),
                0,
                MaxZLevelsBelowRendering);

            if (-depthOffset > maxDepth)
                return false;
        }

        Angle rotation = _eye.CurrentEye.Rotation * -1;
        zPassOffset = rotation.ToWorldVec() * ZLevelOffset * depthOffset;
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_config.GetCVar(CMUZLevelsCVars.Enabled))
            return;

        var query = EntityQueryEnumerator<CMUZPhysicsComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var zPhys, out var sprite))
        {
            ApplyZPhysicsVisuals(uid, zPhys, sprite);
        }

        var projectileQuery = EntityQueryEnumerator<CMUZLevelProjectileVisualOffsetComponent, SpriteComponent, TransformComponent>();
        while (projectileQuery.MoveNext(out var uid, out var visual, out var sprite, out var xform))
        {
            if (HasComp<CMUZLevelPredictedProjectileVisualOffsetComponent>(uid))
                continue;

            ApplyProjectileVisualOffset(
                uid,
                visual.Offset,
                ref visual.OriginalOffset,
                ref visual.AppliedOffset,
                sprite,
                xform);
        }

        var predictedProjectileQuery = EntityQueryEnumerator<CMUZLevelPredictedProjectileVisualOffsetComponent, SpriteComponent, TransformComponent>();
        while (predictedProjectileQuery.MoveNext(out var uid, out var visual, out var sprite, out var xform))
        {
            ApplyProjectileVisualOffset(
                uid,
                visual.Offset,
                ref visual.OriginalOffset,
                ref visual.AppliedOffset,
                sprite,
                xform);
        }
    }

    private void ApplyZPhysicsVisuals(EntityUid uid, CMUZPhysicsComponent zPhys, SpriteComponent sprite)
    {
        var targetNoRotation = zPhys.LocalPosition != 0 || zPhys.NoRotDefault;
        if (sprite.NoRotation != targetNoRotation)
            sprite.NoRotation = targetNoRotation;

        var targetOffset = zPhys.SpriteOffsetDefault + new Vector2(0, zPhys.LocalPosition * ZLevelOffset);
        if (sprite.Offset != targetOffset)
            _sprite.SetOffset((uid, sprite), targetOffset);

        var targetDrawDepth = zPhys.LocalPosition > 0 ? (int)Shared.DrawDepth.DrawDepth.OverMobs : zPhys.DrawDepthDefault;
        if (sprite.DrawDepth != targetDrawDepth)
            _sprite.SetDrawDepth((uid, sprite), targetDrawDepth);
    }

    private bool TryApplyProjectileVisualOffset(
        EntityUid uid,
        Vector2 visualOffset,
        ref Vector2? originalOffset,
        ref Vector2 appliedOffset)
    {
        if (!_config.GetCVar(CMUZLevelsCVars.Enabled) ||
            !TryComp<SpriteComponent>(uid, out var sprite) ||
            !TryComp(uid, out TransformComponent? xform))
        {
            return false;
        }

        ApplyProjectileVisualOffset(
            uid,
            visualOffset,
            ref originalOffset,
            ref appliedOffset,
            sprite,
            xform);
        return true;
    }

    private void ApplyProjectileVisualOffset(
        EntityUid uid,
        Vector2 visualOffset,
        ref Vector2? originalOffset,
        ref Vector2 appliedOffset,
        SpriteComponent sprite,
        TransformComponent xform)
    {
        Angle renderRotation;
        if (sprite.NoRotation)
            renderRotation = _eye.CurrentEye.Rotation * -1;
        else
            renderRotation = _transformSystem.GetWorldRotation(xform);

        var localVisualOffset = (-renderRotation).RotateVec(visualOffset);

        originalOffset ??= sprite.Offset - appliedOffset;
        if (appliedOffset == localVisualOffset)
            return;

        _sprite.SetOffset((uid, sprite), originalOffset.Value + localVisualOffset);
        appliedOffset = localVisualOffset;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<CMUZLevelBlurOverlay>();

        if (_visibleEntityOverlay is not null && _overlay.HasOverlay<CMUZLevelVisibleEntityOverlay>())
            _overlay.RemoveOverlay(_visibleEntityOverlay);
    }
}
