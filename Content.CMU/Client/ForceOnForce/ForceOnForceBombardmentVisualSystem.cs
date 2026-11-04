using System.Numerics;
using Content.Client.CMU14.Fighter;
using Content.Shared.CMU14.ForceOnForce;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.ForceOnForce;

public sealed partial class ForceOnForceBombardmentVisualSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    public override void Initialize() => _overlays.AddOverlay(new ForceOnForceBombardmentOverlay(EntityManager));
    public override void Shutdown() => _overlays.RemoveOverlay<ForceOnForceBombardmentOverlay>();
}

/// <summary>Bounded cosmetic particles: shells, fireballs and pressure waves have distinct silhouettes.</summary>
public sealed class ForceOnForceBombardmentOverlay(IEntityManager entities) : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;
    private readonly SharedTransformSystem _transform = entities.System<SharedTransformSystem>();
    private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();
    private readonly FighterParticleBatch _particles = new();

    protected override void Draw(in OverlayDrawArgs args)
    {
        _particles.Clear();
        var query = entities.EntityQueryEnumerator<ForceOnForceBombardmentVisualComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var visual, out var transform))
        {
            if (transform.MapID != args.MapId) continue;
            var origin = _transform.GetWorldPosition(uid);
            if (!args.WorldAABB.Enlarged(24).Contains(origin)) continue;
            if (!visual.Impacted)
            {
                // Never show a blast just because its predicted time elapsed: the server
                // may cancel an incoming effect when someone steps into its landing site.
                var duration = Math.Max(.01, (visual.ImpactAt - visual.StartedAt).TotalSeconds);
                var progress = Math.Clamp((float) ((_timing.CurTime - visual.StartedAt).TotalSeconds / duration), 0, 1);
                if (progress < 1) DrawIncoming(origin, visual, progress);
                continue;
            }
            var age = (float) (_timing.CurTime - visual.ImpactAt).TotalSeconds;
            if (age is >= 0 and < 6) DrawImpact(origin, visual, age);
        }
        args.WorldHandle.SetTransform(Matrix3x2.Identity);
        _particles.Draw(args.WorldHandle, (float) _timing.CurTime.TotalSeconds);
    }

    private void DrawIncoming(Vector2 origin, ForceOnForceBombardmentVisualComponent visual, float progress)
    {
        var direction = visual.Direction;
        var side = new Vector2(-direction.Y, direction.X);
        if (visual.Variant == 0)
        {
            // Multiple short incandescent shells, not an energy column.
            for (var i = 0; i < 3 + visual.Variation; i++)
            {
                var tip = origin - direction * ((1 - progress) * 18 + i * .65f) + side * MathF.Sin(i * 7) * .3f;
                _particles.Trail(tip, tip - direction * 2, .08f, Color.FromHex("#FFCB7D"));
                _particles.Mote(tip, new Vector2(.14f), Color.White);
            }
            return;
        }
        if (visual.Variant == 2)
        {
            var tip = origin - direction * (1 - progress) * 22;
            for (var i = 17; i >= 0; i--)
            {
                var tail = tip - direction * i * .55f + side * MathF.Sin(i * 2 + progress * 24) * i * .025f;
                var fade = 1 - i / 18f;
                _particles.Mote(tail, new Vector2((.5f + i * .06f) * visual.Scale),
                    Color.FromHex("#66534B").WithAlpha(fade * .55f), smoke: true, seed: i);
                if (i < 10) _particles.Mote(tail, new Vector2((.85f - i * .045f) * visual.Scale),
                    Color.FromHex(i % 2 == 0 ? "#FF6D23" : "#FFD573").WithAlpha(fade));
            }
            _particles.Mote(tip, new Vector2(.4f * visual.Scale), Color.FromHex("#FFF4CE"));
            return;
        }
        var color = Color.FromHex(visual.Variant == 1 ? "#70E9FF" : "#FFAD77");
        for (var i = 0; i < 8; i++)
        {
            var radial = new Angle(i * Math.PI / 4 + progress * 3).RotateVec(Vector2.UnitX);
            var spark = origin + radial * (1 - progress) * 2.5f;
            _particles.Trail(spark, spark + radial * .3f, .06f, color.WithAlpha(progress));
        }
    }

    private void DrawImpact(Vector2 origin, ForceOnForceBombardmentVisualComponent visual, float age)
    {
        if (visual.Variant == 3)
        {
            // Staggered pressure fronts give heavy blasts a wide, unmistakable silhouette.
            for (var wave = 0; wave < 2 + visual.Variation % 2; wave++)
            {
                var phase = age - wave * .22f;
                if (phase is < 0 or > 1.7f) continue;
                for (var segment = 0; segment < 40; segment++)
                {
                    var radial = new Angle(segment * Math.PI / 20).RotateVec(Vector2.UnitX);
                    var point = origin + radial * (.6f + phase * 7) * visual.Scale;
                    _particles.Mote(point, new Vector2(.18f + phase * .16f),
                        Color.FromHex(wave == 0 ? "#FFF0D7" : "#ADCAE8").WithAlpha((1 - phase / 1.7f) * .8f));
                }
            }
            _particles.Mote(origin, new Vector2(2.5f * visual.Scale),
                Color.FromHex("#FFF0C8").WithAlpha(Math.Max(0, 1 - age * 2.5f)));
        }
        else if (visual.Variant == 2)
        {
            for (var i = 0; i < 10 + visual.Variation * 2; i++)
            {
                var radial = new Angle(i * 2.4).RotateVec(Vector2.UnitX);
                if (age < 2.3f)
                {
                    var fragment = origin + radial * age * (3 + i % 3) * visual.Scale;
                    _particles.Trail(fragment, fragment - radial * .65f, .07f,
                        Color.FromHex("#FFBD55").WithAlpha(1 - age / 2.3f));
                }
                var flame = origin + radial * Math.Min(age, 1.2f) * visual.Scale + new Vector2(0, age * .4f);
                _particles.Mote(flame, new Vector2((.9f + age * .4f) * visual.Scale),
                    Color.FromHex(i % 2 == 0 ? "#FF6427" : "#FFC66C").WithAlpha(Math.Max(0, 1 - age / 2.5f) * .65f));
            }
        }
        else if (visual.Variant == 0 && age < 1)
        {
            for (var i = 0; i < 14; i++)
            {
                var radial = new Angle(i * 2.4).RotateVec(Vector2.UnitX);
                var spark = origin + radial * age * 4 * visual.Scale;
                _particles.Trail(spark, spark - radial * .4f, .05f, Color.FromHex("#FFC582").WithAlpha(1 - age));
            }
        }
    }

    protected override void DisposeBehavior()
    {
        _particles.Dispose();
        base.DisposeBehavior();
    }
}
