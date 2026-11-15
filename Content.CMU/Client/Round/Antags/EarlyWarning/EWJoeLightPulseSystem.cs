using Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Round.Antags.EarlyWarning;

/// <summary>
/// Animates THE LIGHT on every client, every frame: the orange glow swells and ebbs in overlapping waves, like
/// light off rippling water, with no flash. The colour runs a quarter wave behind the brightness so the light
/// seems to flow rather than blink. Purely visual and local; the server's light keeps its own values (and still
/// turns the light off when the Joe goes down).
/// </summary>
public sealed class EWJoeLightPulseSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private PointLightSystem _lights = default!;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var time = (float) _timing.RealTime.TotalSeconds;
        var query = EntityQueryEnumerator<EWJoeLightPulseComponent, PointLightComponent>();
        while (query.MoveNext(out var uid, out var pulse, out var light))
        {
            if (!light.Enabled)
                continue;

            var wave = Ripple(time, pulse.Period);
            _lights.SetEnergy(uid, pulse.MinEnergy + (pulse.MaxEnergy - pulse.MinEnergy) * wave, light);
            _lights.SetRadius(uid, pulse.MinRadius + (pulse.MaxRadius - pulse.MinRadius) * wave, light);
            var hue = Ripple(time - pulse.Period / 4f, pulse.Period);
            _lights.SetColor(uid, Color.InterpolateBetween(pulse.Deep, pulse.Bright, hue), light);
        }
    }

    /// <summary>
    /// 0 to 1, smooth: a long swell once per <paramref name="period"/>, a crossing wave at the golden ratio of it
    /// and a small fast shimmer. The three never line up, so the light never settles into a beat.
    /// </summary>
    public static float Ripple(float time, float period)
    {
        var t = time / period * MathF.Tau;
        var wave = 0.55f * MathF.Sin(t)
                   + 0.3f * MathF.Sin(t * 1.618f + 1.3f)
                   + 0.15f * MathF.Sin(t * 2.9f + 0.4f);
        return Math.Clamp(0.5f + 0.5f * wave, 0f, 1f);
    }
}
