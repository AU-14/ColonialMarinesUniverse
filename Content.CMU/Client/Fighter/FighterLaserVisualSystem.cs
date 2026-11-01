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
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;
    private readonly SharedTransformSystem _transform = entities.System<SharedTransformSystem>();
    private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();
    private readonly CavernParticleBatch _particles = new();
    internal TimeSpan LastIncomingDrawAt { get; private set; }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        // Cinematic bombardment also reuses this beam, without owning a seat
        // designation. Its authored lifetime remains independent of CAS cues.
        var beams = entities.EntityQueryEnumerator<FighterLaserComponent, TransformComponent>();
        while (beams.MoveNext(out var beamUid, out var beam, out var beamTransform))
        {
            if (beam.Aircraft != EntityUid.Invalid || _timing.CurTime < beam.StartedAt ||
                _timing.CurTime >= beam.ExpiresAt || beamTransform.MapID != args.MapId ||
                !args.WorldAABB.Enlarged(24).Contains(_transform.GetWorldPosition(beamUid))) continue;
            handle.SetTransform(_transform.GetWorldMatrix(beamUid));
            _particles.Clear();
            var age = (float) (_timing.CurTime - beam.StartedAt).TotalSeconds;
            CargoGuildBeam.Draw(_particles, Vector2.Zero, Math.Clamp(age * 3, 0, 1), age, color: beam.BeamColor);
            _particles.Draw(handle, (float) _timing.CurTime.TotalSeconds);
        }
        var query = entities.EntityQueryEnumerator<FighterStrikeVisualComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var strike, out var xform))
        {
            if (!FighterEffects.LaserVisible(strike, _timing.CurTime) || xform.MapID != args.MapId ||
                !args.WorldAABB.Enlarged(24).Contains(_transform.GetWorldPosition(uid))) continue;
            var age = (float) (_timing.CurTime - strike.ImpactAt + FighterEffects.LaserLead).TotalSeconds;
            var rotation = new Angle(Math.Atan2(-strike.Direction.X, strike.Direction.Y));
            handle.SetTransform(_transform.GetWorldPosition(uid), rotation);
            _particles.Clear();
            var incomingAge = (float) (_timing.CurTime - strike.ImpactAt + FighterEffects.WarningLead).TotalSeconds;
            var incoming = FighterEffects.WarningVisible(strike, _timing.CurTime);
            var beamColor = Color.FromHex("#FF3636");
            var power = Math.Clamp(age * 3, 0, 1);
            if (incoming) power *= .35f + .65f * (.5f + .5f * MathF.Sin(incomingAge * MathF.Tau * 3));
            CargoGuildBeam.Draw(_particles, Vector2.Zero, power, age, color: beamColor);
            if (incoming)
            {
                LastIncomingDrawAt = _timing.CurTime;
                for (var i = 0; i < 2; i++)
                {
                    var phase = incomingAge * 1.5f + i * .5f;
                    phase -= MathF.Floor(phase);
                    var radius = .4f + phase * 1.8f;
                    var tint = beamColor.WithAlpha((1 - phase) * .95f);
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
