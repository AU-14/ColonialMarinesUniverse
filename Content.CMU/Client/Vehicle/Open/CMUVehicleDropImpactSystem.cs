using System.Numerics;
using Content.Client.CMU14.Industry;
using Content.Shared.CMU14.Vehicle.Open;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Vehicle.Open;

public sealed partial class CMUVehicleDropImpactSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;

    public override void Initialize() => _overlays.AddOverlay(new CMUVehicleDropImpactOverlay(EntityManager));
    public override void Shutdown() => _overlays.RemoveOverlay<CMUVehicleDropImpactOverlay>();
}

public sealed class CMUVehicleDropImpactOverlay(IEntityManager entities) : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;
    private readonly SharedTransformSystem _transform = entities.System<SharedTransformSystem>();
    private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();
    private readonly CavernParticleBatch _particles = new();

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        _particles.Clear();
        var query = entities.EntityQueryEnumerator<CMUVehicleDropImpactComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var impact, out var xform))
        {
            if (impact.ImpactAt is not { } at || xform.MapID != args.MapId)
                continue;

            var age = (float) (_timing.CurTime - at).TotalSeconds;
            if (age is < 0 or > 2.5f)
                continue;

            var center = _transform.GetWorldPosition(uid);
            if (!args.WorldAABB.Enlarged(4).Contains(center))
                continue;

            var fade = 1 - age / 2.5f;
            for (var i = 0; i < 24; i++)
            {
                var angle = i * MathF.Tau / 24;
                var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                var radius = .5f + age * (1.2f + i % 3 * .15f);
                var size = .2f + age * .28f;
                _particles.Mote(center + direction * radius, new Vector2(size, size * .6f), angle,
                    new Color(.65f, .61f, .54f, fade * .55f));
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
        _particles.Draw(handle, (float) _timing.CurTime.TotalSeconds);
        handle.UseShader(null);
    }

    protected override void DisposeBehavior()
    {
        _particles.Dispose();
        base.DisposeBehavior();
    }
}
