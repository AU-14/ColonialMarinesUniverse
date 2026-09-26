using System.Numerics;
using Content.Shared.CMU14.Fighter;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Fighter;

public sealed partial class FighterStrikeVisualSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    public override void Initialize() => _overlays.AddOverlay(new FighterStrikeOverlay(EntityManager));
    public override void Shutdown() => _overlays.RemoveOverlay<FighterStrikeOverlay>();
}

/// <summary>Shared ground view: descending ordnance stays below clouds and normal map visibility.</summary>
public sealed partial class FighterStrikeOverlay(IEntityManager entities) : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;
    private readonly SharedTransformSystem _transform = entities.System<SharedTransformSystem>();
    private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();
    private readonly FighterParticleBatch _particles = new();

    protected override void Draw(in OverlayDrawArgs args)
    {
        _particles.Clear();
        var now = _timing.CurTime;
        var query = entities.EntityQueryEnumerator<FighterStrikeVisualComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var strike, out var xform))
        {
            if (xform.MapID != args.MapId || now >= strike.ExpiresAt) continue;
            var target = _transform.GetWorldPosition(uid);
            if (!args.WorldAABB.Enlarged(24).Contains(target)) continue;
            var gau = strike.Kind == FighterWeaponKind.Gau;
            foreach (var hit in strike.Impacts)
                DrawImpact(target + hit.Offset, (float) (now - strike.ImpactAt - hit.At).TotalSeconds,
                    strike.Kind, hit.Water, hit.Offset.X * 7 + hit.Offset.Y * 19 + (float) hit.At.TotalSeconds);
            if (!FighterEffects.TryDescent(strike, now, out var progress, out var volley)) continue;
            for (var previous = 0; previous < (gau ? 2 : 1) && volley - previous >= 0; previous++)
            {
                var phase = progress + previous * strike.VolleyInterval / FighterEffects.DescentSeconds(strike.Kind);
                if (phase > 1) continue;
                var origin = target + strike.Direction * (1 - phase) * (gau ? 12 : 20);
                var size = gau ? .1f : .12f + (1 - phase) * .24f;
                _particles.Trail(origin, origin + strike.Direction * (gau ? 2 : 3.5f), size * 2,
                    Color.FromHex("#FFAA58").WithAlpha(.8f));
                _particles.Trail(origin, origin + strike.Direction * .5f, size, Color.White);
                if (!gau)
                    for (var i = 1; i <= 7; i++)
                        _particles.Mote(origin + strike.Direction * i * .6f, new Vector2(.25f + i * .07f),
                            Color.FromHex("#B7B5AE").WithAlpha((1 - i / 8f) * .45f), smoke: true, seed: i);
            }
        }
        DrawManpadLaunches(in args, now);
        DrawBoilerPlasma(in args, now);
        DrawVtol(in args, now);
        DrawAirBursts(in args, now);
        DrawCrashingFighters(in args, now);
        args.WorldHandle.SetTransform(Matrix3x2.Identity);
        _particles.Draw(args.WorldHandle, (float) now.TotalSeconds);
    }

    protected override void DisposeBehavior()
    {
        _particles.Dispose();
        base.DisposeBehavior();
    }
}
