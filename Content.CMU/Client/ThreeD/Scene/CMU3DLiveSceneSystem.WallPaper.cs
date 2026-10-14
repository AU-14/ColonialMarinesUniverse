using System.Numerics;
using Content.Shared.CMU14.ThreeD;
using Robust.Client.GameObjects;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map.Components;

namespace Content.Client.CMU14.ThreeD.Scene;

public sealed partial class CMU3DLiveSceneSystem
{
    private readonly Dictionary<EntityUid, Vector2?> _paperOffsets = [];
    private readonly HashSet<Entity<SpriteComponent>> _paperCandidates = [];

    private bool TryPaperPose(EntityUid uid, SpriteComponent sprite, CMU3DModelPrototype model,
        out CMU3DWallPaperPose pose)
    {
        pose = default;
        if (!CMU3DWallPaper.ValidModel(model) || !sprite.Visible || sprite.Color.A <= 0 || sprite.ContainerOccluded ||
            !sprite.AddToTree || sprite.NoRotation || !sprite.SnapCardinals || sprite.Offset != Vector2.Zero || sprite.Scale != Vector2.One || sprite.Rotation != Angle.Zero ||
            sprite.GranularLayersRendering || _sprites.GetPostShaders(sprite).Count > 0 ||
            !TryComp(uid, out TransformComponent? xform) || !xform.Anchored ||
            !TryComp(xform.GridUid, out MapGridComponent? grid))
            return false;
        SpriteComponent.Layer? basis = null;
        foreach (var layer in sprite.AllLayers)
        {
            if (!layer.Visible || layer.Color.A <= 0 || !layer.RsiState.IsValid && layer.Texture == null)
                continue;
            if (basis != null || layer is not SpriteComponent.Layer actual)
                return false;
            basis = actual;
        }
        if (basis?.ActualState is not { RsiDirections: RsiDirectionType.Dir1, DelayCount: 1 } ||
            ((ISpriteLayer) basis).ActualRsi?.Size != new Vector2i(32, 32) || basis.CopyToShaderParameters != null ||
            basis.DirOffset != SpriteComponent.DirectionOffset.None)
            return false;
        var source = Snapshot(basis);
        if (!source.IdentityTransform || source.Color != Color.White || source.Frame != 0 ||
            !CMU3DSourceReference.Matches(source.Rsi, model.ReferenceRsi) || source.State != model.ReferenceState)
            return false;
        var position = _transform.GetWorldPosition(xform);
        var yaw = CMU3DSceneLayout.RenderYaw(model, (float) _transform.GetWorldRotation(xform).Theta,
            sprite.NoRotation, sprite.SnapCardinals);
        var gridYaw = (float) _transform.GetWorldRotation(xform.GridUid!.Value).Theta;
        if (MathF.Abs(MathF.Sin(2 * (yaw - gridYaw))) > .00001f ||
            !TryWallMount(uid, model, ref yaw, out var inside, out var offset))
            return false;
        offset += BackWallMountOffset(uid, model, yaw, offset, inside);
        return CMU3DWallPaper.TryPose(model, uid, position, sprite.DrawDepth, sprite.RenderOrder,
            position + offset, yaw, inside, out pose);
    }

    private bool TryPaperOffset(EntityUid uid, SpriteComponent sprite, CMU3DModelPrototype model, out Vector2 offset)
    {
        offset = Vector2.Zero;
        if (_paperOffsets.TryGetValue(uid, out var cached))
        {
            offset = cached ?? Vector2.Zero;
            return cached.HasValue;
        }
        if (!TryPaperPose(uid, sprite, model, out var first) || !TryComp(uid, out TransformComponent? xform))
        {
            _paperOffsets[uid] = null;
            return false;
        }
        var papers = new List<CMU3DWallPaperPose> { first };
        var seen = new HashSet<EntityUid> { uid };
        var valid = true;
        for (var index = 0; index < papers.Count && valid; index++)
        {
            var paper = papers[index];
            _paperCandidates.Clear();
            // Each paper is at most one tile wide. Discover the entire overlap chain,
            // including off-camera sources, instead of ranking only captured entities.
            _lookup.GetEntitiesIntersecting(xform.MapID,
                new Box2(paper.SourcePosition - new Vector2(2), paper.SourcePosition + new Vector2(2)),
                _paperCandidates, LookupFlags.Uncontained);
            foreach (var (other, otherSprite) in _paperCandidates)
            {
                if (seen.Contains(other) || TerminatingOrDeleted(other) || !otherSprite.Visible || otherSprite.Color.A <= 0 ||
                    !TryComp(other, out MetaDataComponent? meta) || meta.EntityPrototype is not { } prototype ||
                    _catalog?.Resolve(prototype.ID) is not { Exact: true } match || !match.Model.WallPaper ||
                    !TryComp(other, out TransformComponent? otherXform) || otherXform.GridUid != xform.GridUid ||
                    (meta.Flags & (MetaDataFlags.Detached | MetaDataFlags.InContainer)) != 0 ||
                    _containers.IsEntityOrParentInContainer(other, meta, otherXform))
                    continue;
                if (!TryPaperPose(other, otherSprite, match.Model, out var neighbor))
                {
                    // Unknown adjacent paper cannot safely participate in an inferred stack.
                    valid = false;
                    break;
                }
                if (!CMU3DWallPaper.Overlaps(paper, neighbor))
                    continue;
                seen.Add(other);
                papers.Add(neighbor);
                if (papers.Count > CMU3DWallPaper.Limit)
                {
                    valid = false;
                    break;
                }
            }
        }
        if (!valid || !CMU3DWallPaper.TryOffsets(papers, out var offsets))
        {
            foreach (var paper in papers)
                _paperOffsets[paper.Uid] = null;
            return false;
        }
        foreach (var (source, value) in offsets)
            _paperOffsets[source] = value;
        offset = offsets[uid];
        return true;
    }
}
