using System.Numerics;
using Content.Shared.CMU14.ZLevels;
using Content.Shared.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Graphics;

namespace Content.Client.CMU14.ZLevels.Core;

public sealed partial class CMUClientZLevelsSystem
{
    // Index effects independently of SpriteComponent. Replicated height can arrive before the sprite,
    // and removing a sprite must not leave an effect untracked when the sprite is added again.
    private readonly Dictionary<EntityUid, PresentationReason> _presentationCandidates = new();
    private readonly List<SpritePresentation> _spritePresentation = new();
    private bool _renderingPresentation;

    /// <summary>
    /// Number of entities with active elevation or a projectile/follower presentation component.
    /// Grounded Z physics alone does not retain an entry or require per-frame discovery.
    /// </summary>
    public int PresentationCandidateCount => _presentationCandidates.Count;

    private void InitializePresentation()
    {
        SubscribeLocalEvent<CMUZPhysicsPresentationChangedEvent>(OnPhysicsPresentationChanged);
        SubscribeLocalEvent<CMUZLevelProjectileVisualOffsetComponent, ComponentStartup>(OnProjectilePresentationStartup);
        SubscribeLocalEvent<CMUZLevelProjectileVisualOffsetComponent, ComponentRemove>(OnProjectilePresentationRemoved);
        SubscribeLocalEvent<CMUZLevelPredictedProjectileVisualOffsetComponent, ComponentStartup>(OnPredictedPresentationStartup);
        SubscribeLocalEvent<CMUZLevelPredictedProjectileVisualOffsetComponent, ComponentRemove>(OnPredictedPresentationRemoved);
        SubscribeLocalEvent<CMUZVisualFollowerComponent, ComponentStartup>(OnFollowerPresentationStartup);
        SubscribeLocalEvent<CMUZVisualFollowerComponent, ComponentRemove>(OnFollowerPresentationRemoved);
    }

    private void OnPhysicsPresentationChanged(ref CMUZPhysicsPresentationChangedEvent args)
        => SetPresentationReason(args.Uid, PresentationReason.Elevation, args.Elevated);

    private void OnProjectilePresentationStartup(Entity<CMUZLevelProjectileVisualOffsetComponent> ent, ref ComponentStartup args)
        => SetPresentationReason(ent.Owner, PresentationReason.Projectile, true);

    private void OnProjectilePresentationRemoved(Entity<CMUZLevelProjectileVisualOffsetComponent> ent, ref ComponentRemove args)
        => SetPresentationReason(ent.Owner, PresentationReason.Projectile, false);

    private void OnPredictedPresentationStartup(Entity<CMUZLevelPredictedProjectileVisualOffsetComponent> ent, ref ComponentStartup args)
        => SetPresentationReason(ent.Owner, PresentationReason.PredictedProjectile, true);

    private void OnPredictedPresentationRemoved(Entity<CMUZLevelPredictedProjectileVisualOffsetComponent> ent, ref ComponentRemove args)
        => SetPresentationReason(ent.Owner, PresentationReason.PredictedProjectile, false);

    private void OnFollowerPresentationStartup(Entity<CMUZVisualFollowerComponent> ent, ref ComponentStartup args)
        => SetPresentationReason(ent.Owner, PresentationReason.Follower, true);

    private void OnFollowerPresentationRemoved(Entity<CMUZVisualFollowerComponent> ent, ref ComponentRemove args)
        => SetPresentationReason(ent.Owner, PresentationReason.Follower, false);

    private void SetPresentationReason(EntityUid uid, PresentationReason reason, bool active)
    {
        _presentationCandidates.TryGetValue(uid, out var reasons);
        var updated = active ? reasons | reason : reasons & ~reason;
        if (updated == reasons)
            return;

        if (updated == PresentationReason.None)
            _presentationCandidates.Remove(uid);
        else
            _presentationCandidates[uid] = updated;
    }

    /// <summary>
    /// Renders one world pass with Z presentation composed over the current sprite state.
    /// Simulation and appearance systems retain ownership outside this synchronous render.
    /// </summary>
    public void RenderViewport(IClydeViewport viewport, CMUZVisibilityMask? mask = null)
    {
        if (_renderingPresentation)
            throw new InvalidOperationException("World viewport renders must not be nested.");

        _renderingPresentation = true;
        try
        {
            if (_config.GetCVar(CMUZLevelsCVars.Enabled) && viewport.Eye is { } eye)
            {
                foreach (var (uid, _) in _presentationCandidates)
                {
                    if (!TerminatingOrDeleted(uid) &&
                        TryComp(uid, out SpriteComponent? sprite) &&
                        TryComp(uid, out TransformComponent? xform) &&
                        xform.MapID == eye.Position.MapId)
                    {
                        ApplySpritePresentation(uid, sprite, xform, eye);
                    }
                }
            }

            // SetOffset does not queue broad-phase bounds updates. Synchronize changed presentation
            // for this pass; ordinary renders use the renderer's existing tree-query flush.
            if (_spritePresentation.Count != 0)
                _spriteTree.UpdateTreePositions();
            _culling.RenderViewport(viewport, mask);
        }
        finally
        {
            foreach (var state in _spritePresentation)
            {
                if (state.Sprite.Deleted)
                    continue;

                state.Sprite.NoRotation = state.NoRotation;
                _sprite.SetOffset((state.Uid, state.Sprite), state.Offset);
                _sprite.SetDrawDepth((state.Uid, state.Sprite), state.DrawDepth);
                _spriteTree.QueueTreeUpdate(state.Uid, state.Sprite);
            }

            // Later consumers must see the restored bounds even before another viewport renders.
            if (_spritePresentation.Count != 0)
                _spriteTree.UpdateTreePositions();
            _spritePresentation.Clear();
            _renderingPresentation = false;
        }
    }

    private void ApplySpritePresentation(EntityUid uid, SpriteComponent sprite, TransformComponent xform, IEye eye)
    {
        var height = TryComp(uid, out CMUZPhysicsComponent? zPhysics) ? zPhysics.LocalPosition : 0f;
        var worldOffset = Vector2.Zero;
        if (TryComp(uid, out CMUZLevelPredictedProjectileVisualOffsetComponent? predicted))
            worldOffset += predicted.Offset;
        else if (TryComp(uid, out CMUZLevelProjectileVisualOffsetComponent? projectile))
            worldOffset += projectile.Offset;

        if (TryComp(uid, out CMUZVisualFollowerComponent? follower) &&
            TryGetVisualFollowerTarget(follower, xform, out var target) &&
            TryComp(target, out CMUZPhysicsComponent? targetPhysics))
        {
            worldOffset += new Vector2(0f, targetPhysics.LocalPosition * ZLevelOffset);
        }

        if (height == 0f && worldOffset == Vector2.Zero)
            return;

        _spritePresentation.Add(new SpritePresentation(uid, sprite, sprite.NoRotation, sprite.Offset, sprite.DrawDepth));

        if (height != 0f)
            sprite.NoRotation = true;

        // World-space projectile/follower displacement must use this camera, not the main eye.
        var renderRotation = sprite.NoRotation ? -eye.Rotation : _transform.GetWorldRotation(xform);
        var offset = sprite.Offset + new Vector2(0f, height * ZLevelOffset) + (-renderRotation).RotateVec(worldOffset);
        _sprite.SetOffset((uid, sprite), offset);

        if (height > 0f)
            _sprite.SetDrawDepth((uid, sprite), Math.Max(sprite.DrawDepth, (int) Shared.DrawDepth.DrawDepth.OverMobs));

        _spriteTree.QueueTreeUpdate(uid, sprite, xform);
    }

    private bool TryGetVisualFollowerTarget(
        CMUZVisualFollowerComponent follower,
        TransformComponent xform,
        out EntityUid target)
    {
        if (follower.Target is { } configured && Exists(configured) && !TerminatingOrDeleted(configured))
        {
            target = configured;
            return true;
        }

        if (xform.ParentUid != EntityUid.Invalid && Exists(xform.ParentUid) && !TerminatingOrDeleted(xform.ParentUid))
        {
            target = xform.ParentUid;
            return true;
        }

        target = default;
        return false;
    }

    private readonly record struct SpritePresentation(
        EntityUid Uid,
        SpriteComponent Sprite,
        bool NoRotation,
        Vector2 Offset,
        int DrawDepth);

    [Flags]
    private enum PresentationReason : byte
    {
        None = 0,
        Elevation = 1 << 0,
        Projectile = 1 << 1,
        PredictedProjectile = 1 << 2,
        Follower = 1 << 3,
    }
}
