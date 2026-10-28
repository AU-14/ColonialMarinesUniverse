using System.Numerics;
using Content.Shared.CMU14.Dropship.TacticalLand;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Dropship.TacticalLand;

public sealed class MohawkLandingBorderOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> UnshadedShader = "unshaded";
    private readonly IEntityManager _entities;
    private readonly IGameTiming _timing;
    private readonly SharedTransformSystem _transform;
    private readonly MohawkLandingBorderRenderer _renderer = new();
    private readonly ShaderInstance _unshaded = IoCManager.Resolve<IPrototypeManager>()
        .Index(UnshadedShader).Instance();

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    public MohawkLandingBorderOverlay(IEntityManager entities, IGameTiming timing)
    {
        _entities = entities;
        _timing = timing;
        _transform = entities.System<SharedTransformSystem>();
        // Project onto the floor without covering people or walls inside the landing zone.
        ZIndex = (int) Content.Shared.DrawDepth.DrawDepth.HighFloorObjects + 2;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        handle.UseShader(_unshaded);
        var query = _entities.EntityQueryEnumerator<MohawkLandingBorderComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var border, out var xform))
        {
            if (xform.MapID != args.MapId || border.Bounds.IsEmpty())
                continue;

            var matrix = _transform.GetWorldMatrix(uid);
            if (!args.WorldAABB.Enlarged(border.Bounds.Size.Length()).Contains(matrix.Translation))
                continue;

            handle.SetTransform(matrix);
            _renderer.Draw(handle, border.Bounds, (float) _timing.CurTime.TotalSeconds);
        }

        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(null);
    }
}
