using System.Numerics;
using Content.Client.CMU14.Industry;
using Content.Shared.CMU14.Fighter;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Fighter;

/// <summary>World rendering for every nearby player, independent of cockpit controls.</summary>
public sealed partial class FighterLaserVisualSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;

    public override void Initialize() => _overlays.AddOverlay(new FighterLaserOverlay(EntityManager));
    public override void Shutdown() => _overlays.RemoveOverlay<FighterLaserOverlay>();
}

public sealed class FighterLaserOverlay(IEntityManager entities) : Overlay
{
    private static readonly Color BeamColor = Color.FromHex("#FF3636");
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;
    private readonly SharedTransformSystem _transform = entities.System<SharedTransformSystem>();
    private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();
    private readonly CavernParticleBatch _particles = new();
    internal TimeSpan LastIncomingDrawAt { get; private set; }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var query = entities.EntityQueryEnumerator<FighterLaserComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var laser, out var xform))
        {
            if (_timing.CurTime >= laser.ExpiresAt || xform.MapID != args.MapId ||
                !args.WorldAABB.Enlarged(24).Contains(_transform.GetWorldPosition(uid))) continue;
            var age = MathF.Max(0, (float) (_timing.CurTime - laser.StartedAt).TotalSeconds);
            // Server rotation points local +Y toward the jet over the ground map.
            handle.SetTransform(_transform.GetWorldMatrix(uid));
            _particles.Clear();
            var incomingAge = (float) (_timing.CurTime - laser.IncomingAt).TotalSeconds;
            var power = Math.Clamp(age * 3, 0, 1);
            if (laser.Incoming) power *= .35f + .65f * (.5f + .5f * MathF.Sin(incomingAge * MathF.Tau * 3));
            CargoGuildBeam.Draw(_particles, Vector2.Zero, power, age, color: BeamColor);
            if (laser.Incoming)
            {
                LastIncomingDrawAt = _timing.CurTime;
                for (var i = 0; i < 2; i++)
                {
                    var phase = incomingAge * 1.5f + i * .5f;
                    phase -= MathF.Floor(phase);
                    var radius = .4f + phase * 1.8f;
                    var tint = BeamColor.WithAlpha((1 - phase) * .95f);
                    const int segments = 48;
                    for (var segment = 0; segment < segments; segment++)
                    {
                        var angle = segment * MathF.Tau / segments;
                        var nextAngle = (segment + 1) * MathF.Tau / segments;
                        var start = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                        var end = new Vector2(MathF.Cos(nextAngle), MathF.Sin(nextAngle)) * radius;
                        // World-width triangles stay legible in a scaled camera feed.
                        _particles.Filament(start, end, .035f, tint);
                    }
                }
            }
            _particles.Draw(handle, (float) _timing.CurTime.TotalSeconds);
        }
        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }

    protected override void DisposeBehavior()
    {
        _particles.Dispose();
        base.DisposeBehavior();
    }
}
