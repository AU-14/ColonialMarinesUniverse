using System.Numerics;
using System.Threading;
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
    private readonly List<EntityUid> _zVisualRemovalQueue = new();
    private int _restoreVisualsRequested;
    private int _reconcileVisualsRequested;

    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new CMUZLevelBlurOverlay());
        _visibleEntityOverlay = new CMUZLevelVisibleEntityOverlay();
        _overlay.AddOverlay(_visibleEntityOverlay);

        SubscribeLocalEvent<CMUZPhysicsComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CMUZPhysicsComponent, ComponentShutdown>(OnZPhysicsShutdown);
        SubscribeLocalEvent<CMUZPhysicsComponent, AfterAutoHandleStateEvent>(OnZPhysicsState);
        SubscribeLocalEvent<CMUZPhysicsComponent, MoveEvent>(OnZPhysicsMoveGroundSnapClient);
        SubscribeLocalEvent<CMUZPhysicsComponent, GetEyeOffsetEvent>(OnEyeOffset);
        SubscribeLocalEvent<CMUZLevelProjectileVisualOffsetComponent, ComponentStartup>(OnProjectileVisualOffsetStartup);
        SubscribeLocalEvent<CMUZLevelProjectileVisualOffsetComponent, ComponentShutdown>(OnProjectileVisualOffsetShutdown);
        SubscribeLocalEvent<CMUZLevelPredictedProjectileVisualOffsetComponent, ComponentStartup>(OnPredictedProjectileVisualOffsetStartup);
        SubscribeLocalEvent<CMUZLevelPredictedProjectileVisualOffsetComponent, ComponentShutdown>(OnPredictedProjectileVisualOffsetShutdown);
        SubscribeLocalEvent<CMUZVisualFollowerComponent, ComponentShutdown>(OnVisualFollowerShutdown);
        SubscribeLocalEvent<GridRemovalEvent>(OnGridShutdown);
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);

        Subs.CVar(_config, CMUZLevelsCVars.Enabled, OnZLevelsEnabledChanged, true);
    }

    private void OnGridShutdown(GridRemovalEvent args)
    {
        InvalidateSharedOpeningCache(args.EntityUid);
    }

    private void OnTileChanged(ref TileChangedEvent args)
    {
        InvalidateSharedOpeningCache(ref args);
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

    private void OnProjectileVisualOffsetStartup(Entity<CMUZLevelProjectileVisualOffsetComponent> ent, ref ComponentStartup args)
    {
        if (TryComp<CMUZLevelPredictedProjectileVisualOffsetComponent>(ent, out var predicted))
        {
            CMUZProjectileSpriteVisuals.TransferOwnership(
                predicted.OriginalOffset,
                predicted.AppliedOffset,
                ref ent.Comp.OriginalOffset,
                ref ent.Comp.AppliedOffset);
            RemCompDeferred<CMUZLevelPredictedProjectileVisualOffsetComponent>(ent);
        }

        TryApplyProjectileVisualOffset(
            ent.Owner,
            ent.Comp.Offset,
            ref ent.Comp.OriginalOffset,
            ref ent.Comp.AppliedOffset);
    }

    private void OnProjectileVisualOffsetShutdown(Entity<CMUZLevelProjectileVisualOffsetComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<CMUZLevelPredictedProjectileVisualOffsetComponent>(ent, out var predicted))
        {
            CMUZProjectileSpriteVisuals.TransferOwnership(
                ent.Comp.OriginalOffset,
                ent.Comp.AppliedOffset,
                ref predicted.OriginalOffset,
                ref predicted.AppliedOffset);
            return;
        }

        RestoreProjectileVisualOffset(
            ent.Owner,
            ref ent.Comp.OriginalOffset,
            ref ent.Comp.AppliedOffset);
    }

    private void OnPredictedProjectileVisualOffsetStartup(Entity<CMUZLevelPredictedProjectileVisualOffsetComponent> ent, ref ComponentStartup args)
    {
        if (HasComp<CMUZLevelProjectileVisualOffsetComponent>(ent))
        {
            RemCompDeferred<CMUZLevelPredictedProjectileVisualOffsetComponent>(ent);
            return;
        }

        TryApplyProjectileVisualOffset(
            ent.Owner,
            ent.Comp.Offset,
            ref ent.Comp.OriginalOffset,
            ref ent.Comp.AppliedOffset);
    }

    private void OnPredictedProjectileVisualOffsetShutdown(Entity<CMUZLevelPredictedProjectileVisualOffsetComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<CMUZLevelProjectileVisualOffsetComponent>(ent, out var replicated))
        {
            TryApplyProjectileVisualOffset(
                ent.Owner,
                replicated.Offset,
                ref replicated.OriginalOffset,
                ref replicated.AppliedOffset);
            return;
        }

        RestoreProjectileVisualOffset(
            ent.Owner,
            ref ent.Comp.OriginalOffset,
            ref ent.Comp.AppliedOffset);
    }

    private void OnVisualFollowerShutdown(Entity<CMUZVisualFollowerComponent> ent, ref ComponentShutdown args)
    {
        RestoreProjectileVisualOffset(
            ent.Owner,
            ref ent.Comp.OriginalOffset,
            ref ent.Comp.AppliedOffset);
    }

    private void RestoreProjectileVisualOffset(
        EntityUid uid,
        ref Vector2? originalOffset,
        ref Vector2 appliedOffset)
    {
        if (TryComp<SpriteComponent>(uid, out var sprite))
        {
            var restored = CMUZProjectileSpriteVisuals.Restore(
                sprite.Offset,
                originalOffset,
                appliedOffset);
            if (restored != sprite.Offset)
                _sprite.SetOffset((uid, sprite), restored);
        }

        originalOffset = null;
        appliedOffset = Vector2.Zero;
    }

    private void OnStartup(Entity<CMUZPhysicsComponent> ent, ref ComponentStartup args)
    {
        if (!_config.GetCVar(CMUZLevelsCVars.Enabled) ||
            !TryComp<SpriteComponent>(ent, out var sprite))
        {
            return;
        }

        ApplyZPhysicsVisuals(ent.Owner, ent.Comp, sprite);
    }

    protected override void OnZLocalPositionChanged(Entity<CMUZPhysicsComponent> ent)
    {
        if (!_config.GetCVar(CMUZLevelsCVars.Enabled) ||
            !TryComp<SpriteComponent>(ent, out var sprite))
        {
            return;
        }

        ApplyZPhysicsVisuals(ent.Owner, ent.Comp, sprite);
    }

    private void OnZPhysicsState(Entity<CMUZPhysicsComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!_config.GetCVar(CMUZLevelsCVars.Enabled) ||
            !TryComp<SpriteComponent>(ent, out var sprite))
        {
            return;
        }

        ApplyZPhysicsVisuals(ent.Owner, ent.Comp, sprite);
    }

    private void OnZPhysicsShutdown(Entity<CMUZPhysicsComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<CMUZPhysicsVisualComponent>(ent, out var visual))
            return;

        if (TryComp<SpriteComponent>(ent, out var sprite))
            RestoreZPhysicsVisuals(ent.Owner, sprite, visual);

        RemCompDeferred<CMUZPhysicsVisualComponent>(ent);
    }

    private void OnZLevelsEnabledChanged(bool enabled)
    {
        if (enabled)
            Interlocked.Exchange(ref _reconcileVisualsRequested, 1);
        else
            Interlocked.Exchange(ref _restoreVisualsRequested, 1);
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

        if (!TryGetSpeechBubbleViewOrigin(out _, out var viewer, out var viewXform) ||
            viewXform.MapUid is not { } viewMap ||
            !TryComp<CMUZLevelMapComponent>(viewMap, out var viewZMap) ||
            !TryComp<CMUZLevelMapComponent>(speakerMap, out var speakerZMap) ||
            speakerZMap.NetworkUid != viewZMap.NetworkUid)
        {
            return false;
        }

        var depthOffset = speakerZMap.Depth - viewZMap.Depth;
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
                MaxZLevelTraversalDepth);

            if (-depthOffset > maxDepth)
                return false;
        }

        Angle rotation = _eye.CurrentEye.Rotation * -1;
        zPassOffset = rotation.ToWorldVec() * ZLevelOffset * depthOffset;
        return true;
    }

    private bool TryGetSpeechBubbleViewOrigin(
        out EntityUid viewEntity,
        out CMUZLevelViewerComponent viewer,
        out TransformComponent xform)
    {
        var currentEye = _eye.CurrentEye;
        var eyeQuery = EntityQueryEnumerator<EyeComponent>();
        while (eyeQuery.MoveNext(out var eyeUid, out var eye))
        {
            if (!ReferenceEquals(eye.Eye, currentEye))
                continue;

            if (eye.Target is { } target &&
                TryResolveSpeechBubbleViewOrigin(target, out viewEntity, out viewer, out xform))
            {
                return true;
            }

            return TryResolveSpeechBubbleViewOrigin(eyeUid, out viewEntity, out viewer, out xform);
        }

        if (_player.LocalEntity is { } player &&
            TryResolveSpeechBubbleViewOrigin(player, out viewEntity, out viewer, out xform) &&
            xform.MapID == currentEye.Position.MapId)
        {
            return true;
        }

        viewEntity = default;
        viewer = default!;
        xform = default!;
        return false;
    }

    private bool TryResolveSpeechBubbleViewOrigin(
        EntityUid candidate,
        out EntityUid viewEntity,
        out CMUZLevelViewerComponent viewer,
        out TransformComponent xform)
    {
        if (TryComp<CMUZLevelViewerComponent>(candidate, out var candidateViewer) &&
            XformQuery.TryComp(candidate, out var candidateXform) &&
            candidateXform.MapUid is not null)
        {
            viewEntity = candidate;
            viewer = candidateViewer;
            xform = candidateXform;
            return true;
        }

        viewEntity = default;
        viewer = default!;
        xform = default!;
        return false;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (Interlocked.Exchange(ref _restoreVisualsRequested, 0) != 0)
            RestoreAllZLevelVisuals();

        if (!_config.GetCVar(CMUZLevelsCVars.Enabled))
            return;

        if (Interlocked.Exchange(ref _reconcileVisualsRequested, 0) != 0)
            ReconcileZPhysicsVisuals();

        _zVisualRemovalQueue.Clear();
        var query = EntityQueryEnumerator<CMUZPhysicsVisualComponent, CMUZPhysicsComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out _, out var zPhys, out var sprite))
        {
            if (!ApplyZPhysicsVisuals(uid, zPhys, sprite, removeInactive: false))
                _zVisualRemovalQueue.Add(uid);
        }

        RemoveInactiveZPhysicsVisuals();

        var projectileQuery = EntityQueryEnumerator<CMUZLevelProjectileVisualOffsetComponent, SpriteComponent, TransformComponent>();
        while (projectileQuery.MoveNext(out var uid, out var visual, out var sprite, out var xform))
        {
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
            if (HasComp<CMUZLevelProjectileVisualOffsetComponent>(uid))
                continue;

            ApplyProjectileVisualOffset(
                uid,
                visual.Offset,
                ref visual.OriginalOffset,
                ref visual.AppliedOffset,
                sprite,
                xform);
        }

        var followerQuery = EntityQueryEnumerator<CMUZVisualFollowerComponent, SpriteComponent, TransformComponent>();
        while (followerQuery.MoveNext(out var uid, out var follower, out var sprite, out var xform))
        {
            ApplyVisualFollowerOffset(uid, follower, sprite, xform);
        }
    }

    private bool ApplyZPhysicsVisuals(
        EntityUid uid,
        CMUZPhysicsComponent zPhys,
        SpriteComponent sprite,
        bool removeInactive = true)
    {
        var current = GetSpriteState(sprite);
        if (!CMUZPhysicsSpriteVisuals.IsActive(zPhys.LocalPosition))
        {
            if (TryComp<CMUZPhysicsVisualComponent>(uid, out var inactiveVisual))
            {
                RestoreZPhysicsVisuals(uid, sprite, inactiveVisual);
                if (removeInactive)
                    RemComp<CMUZPhysicsVisualComponent>(uid);
            }

            return false;
        }

        var visual = EnsureComp<CMUZPhysicsVisualComponent>(uid);
        if (visual.Applied)
        {
            var baseline = CMUZPhysicsSpriteVisuals.RefreshBaseline(
                visual.Baseline,
                visual.AppliedState,
                current);
            visual.Baseline = baseline;
        }
        else
        {
            visual.Baseline = current;
        }

        var target = CMUZPhysicsSpriteVisuals.GetActiveState(
            visual.Baseline,
            zPhys.LocalPosition,
            ZLevelOffset,
            (int) Shared.DrawDepth.DrawDepth.OverMobs);
        SetSpriteState(uid, sprite, target);
        visual.AppliedState = target;
        visual.Applied = true;
        return true;
    }

    private void RestoreZPhysicsVisuals(
        EntityUid uid,
        SpriteComponent sprite,
        CMUZPhysicsVisualComponent visual)
    {
        var current = GetSpriteState(sprite);
        if (visual.Applied)
        {
            current = CMUZPhysicsSpriteVisuals.RestoreOwnedState(
                visual.Baseline,
                visual.AppliedState,
                current);
            SetSpriteState(uid, sprite, current);
        }

        visual.Applied = false;
    }

    private void ReconcileZPhysicsVisuals()
    {
        var query = EntityQueryEnumerator<CMUZPhysicsComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var zPhysics, out var sprite))
        {
            if (!CMUZPhysicsSpriteVisuals.IsActive(zPhysics.LocalPosition))
                continue;

            ApplyZPhysicsVisuals(uid, zPhysics, sprite);
        }
    }

    private void RestoreAllZLevelVisuals()
    {
        _zVisualRemovalQueue.Clear();
        var zPhysicsQuery = EntityQueryEnumerator<CMUZPhysicsVisualComponent>();
        while (zPhysicsQuery.MoveNext(out var uid, out var visual))
        {
            if (TryComp<SpriteComponent>(uid, out var sprite))
                RestoreZPhysicsVisuals(uid, sprite, visual);

            _zVisualRemovalQueue.Add(uid);
        }

        RemoveInactiveZPhysicsVisuals();

        var projectileQuery = EntityQueryEnumerator<CMUZLevelProjectileVisualOffsetComponent>();
        while (projectileQuery.MoveNext(out var uid, out var visual))
        {
            RestoreProjectileVisualOffset(
                uid,
                ref visual.OriginalOffset,
                ref visual.AppliedOffset);
        }

        var predictedProjectileQuery =
            EntityQueryEnumerator<CMUZLevelPredictedProjectileVisualOffsetComponent>();
        while (predictedProjectileQuery.MoveNext(out var uid, out var visual))
        {
            if (HasComp<CMUZLevelProjectileVisualOffsetComponent>(uid))
                continue;

            RestoreProjectileVisualOffset(
                uid,
                ref visual.OriginalOffset,
                ref visual.AppliedOffset);
        }

        var followerQuery = EntityQueryEnumerator<CMUZVisualFollowerComponent>();
        while (followerQuery.MoveNext(out var uid, out var follower))
        {
            RestoreProjectileVisualOffset(
                uid,
                ref follower.OriginalOffset,
                ref follower.AppliedOffset);
        }
    }

    private void RemoveInactiveZPhysicsVisuals()
    {
        foreach (var uid in _zVisualRemovalQueue)
        {
            RemComp<CMUZPhysicsVisualComponent>(uid);
        }

        _zVisualRemovalQueue.Clear();
    }

    private static CMUZPhysicsSpriteState GetSpriteState(SpriteComponent sprite)
    {
        return new CMUZPhysicsSpriteState(sprite.NoRotation, sprite.DrawDepth, sprite.Offset);
    }

    private void SetSpriteState(
        EntityUid uid,
        SpriteComponent sprite,
        CMUZPhysicsSpriteState target)
    {
        if (sprite.NoRotation != target.NoRotation)
            sprite.NoRotation = target.NoRotation;

        if (sprite.Offset != target.Offset)
            _sprite.SetOffset((uid, sprite), target.Offset);

        if (sprite.DrawDepth != target.DrawDepth)
            _sprite.SetDrawDepth((uid, sprite), target.DrawDepth);
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

    private void ApplyVisualFollowerOffset(
        EntityUid uid,
        CMUZVisualFollowerComponent follower,
        SpriteComponent sprite,
        TransformComponent xform)
    {
        if (!TryGetVisualFollowerTarget(follower, xform, out var target) ||
            !TryComp(target, out CMUZPhysicsComponent? zPhys))
        {
            RestoreProjectileVisualOffset(
                uid,
                ref follower.OriginalOffset,
                ref follower.AppliedOffset);
            return;
        }

        ApplyProjectileVisualOffset(
            uid,
            new Vector2(0f, zPhys.LocalPosition * ZLevelOffset),
            ref follower.OriginalOffset,
            ref follower.AppliedOffset,
            sprite,
            xform);
    }

    private bool TryGetVisualFollowerTarget(
        CMUZVisualFollowerComponent follower,
        TransformComponent xform,
        out EntityUid target)
    {
        if (follower.Target is { } configured &&
            Exists(configured) &&
            !TerminatingOrDeleted(configured))
        {
            target = configured;
            return true;
        }

        if (xform.ParentUid != EntityUid.Invalid &&
            Exists(xform.ParentUid) &&
            !TerminatingOrDeleted(xform.ParentUid))
        {
            target = xform.ParentUid;
            return true;
        }

        target = default;
        return false;
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

        var targetOffset = CMUZProjectileSpriteVisuals.Apply(
            sprite.Offset,
            localVisualOffset,
            ref originalOffset,
            ref appliedOffset);
        if (targetOffset != sprite.Offset)
            _sprite.SetOffset((uid, sprite), targetOffset);
    }

    public override void Shutdown()
    {
        RestoreAllZLevelVisuals();
        base.Shutdown();
        _overlay.RemoveOverlay<CMUZLevelBlurOverlay>();

        if (_visibleEntityOverlay is not null && _overlay.HasOverlay<CMUZLevelVisibleEntityOverlay>())
            _overlay.RemoveOverlay(_visibleEntityOverlay);
    }
}
