using Content.Client.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;

namespace Content.Client.CMU14.ZLevels.Culling;

/// <summary>
/// Applies a viewport's conservative mask only while that viewport renders. Semantic visibility
/// remains owned by appearance systems between renders and is never cached across frames.
/// </summary>
public sealed partial class CMUZLevelSpriteCullingSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SpriteTreeSystem _spriteTree = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly HashSet<Entity<SpriteComponent, TransformComponent>> _candidates = new();
    private readonly List<Entity<SpriteComponent>> _hidden = new();
    private bool _rendering;

    public int LastCandidates { get; private set; }
    public int LastHidden { get; private set; }

    /// <summary>
    /// Called after camera-specific sprite presentation and its tree updates. Both visibility and
    /// tree membership are restored even when the renderer throws or deletes a candidate.
    /// </summary>
    public void RenderViewport(IClydeViewport viewport, CMUZVisibilityMask? mask)
    {
        if (_rendering)
            throw new InvalidOperationException("World viewport renders must not be nested.");

        LastCandidates = 0;
        LastHidden = 0;
        if (mask is null ||
            viewport.Eye is not { } eye ||
            mask.MapId != eye.Position.MapId ||
            !_config.GetCVar(CMUZLevelsCVars.Enabled) ||
            !_config.GetCVar(CMUZLevelsCVars.RenderEnabled) ||
            mask.DynamicOnly && !_config.GetCVar(CMUZLevelsCVars.CullOccludedDynamicSprites))
        {
            viewport.Render();
            return;
        }

        var diagnostics = _config.GetCVar(CMUZLevelsCVars.ClientDiagnosticsEnabled);
        _rendering = true;
        try
        {
            _spriteTree.QueryAabb(_candidates, mask.MapId, mask.WorldBounds, approx: true);
            if (diagnostics)
                LastCandidates = _candidates.Count;
            foreach (var candidate in _candidates)
            {
                var (uid, sprite, xform) = candidate;
                if (!sprite.Visible || xform.MapID != mask.MapId || mask.DynamicOnly && xform.Anchored)
                    continue;

                var (position, rotation) = _transform.GetWorldPositionRotation(xform);
                var bounds = _sprite.CalculateBounds((uid, sprite), position, rotation, eye.Rotation).CalcBoundingBox();
                if (mask.ClassifyBounds(bounds) != CMUZVisibility.Hidden)
                    continue;

                _hidden.Add((uid, sprite));
                _sprite.SetVisible((uid, sprite), false);
            }

            if (diagnostics)
                LastHidden = _hidden.Count;
            if (_hidden.Count != 0)
                _spriteTree.UpdateTreePositions();
            viewport.Render();
        }
        finally
        {
            foreach (var entity in _hidden)
            {
                if (!entity.Comp.Deleted)
                    _sprite.SetVisible((entity.Owner, entity.Comp), true);
            }

            if (_hidden.Count != 0)
                _spriteTree.UpdateTreePositions();
            _hidden.Clear();
            _candidates.Clear();
            _rendering = false;
        }
    }
}
