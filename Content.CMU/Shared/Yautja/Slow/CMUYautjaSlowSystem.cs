using Content.Shared._RMC14.Slow;
using Content.Shared.Random.Helpers;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Yautja.Slow;

// xeno abilities hand their pred slow here so preds get cmss13's timings (with its tick jitter)
// instead of RMC's flat ones. marines and everyone else keep the RMC numbers
public sealed partial class CMUYautjaSlowSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private RMCSlowSystem _slow = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    // cmss13 SShuman and SSoldeffects waits
    private const float LifeTick = 2f;
    private const float EffectTick = 1f;
    private const float EffectStep = 1.1f;

    // true when the target was a pred and the slow got handled here, even if the roll came out at nothing
    public bool TryApply(EntityUid target, YautjaCmssSlow? slow)
    {
        if (slow == null || !HasComp<YautjaComponent>(target))
            return false;

        var rand = SharedRandomExtensions.PredictedRandom(_timing, GetNetEntity(target));
        var seconds = Roll(slow, rand.NextFloat(0f, LifeTick), rand.NextFloat(0f, EffectTick));
        if (seconds <= 0f)
            return true;

        var duration = TimeSpan.FromSeconds(seconds);
        if (slow.Super)
            _slow.TrySuperSlowdown(target, duration, ignoreDurationModifier: true);
        else
            _slow.TrySlowdown(target, duration, ignoreDurationModifier: true);

        return true;
    }

    // cmss13's get_turf(xeno) check, for stomps that only hit what's underneath
    public bool SameTile(EntityUid a, EntityUid b)
    {
        var aXform = Transform(a);
        var bXform = Transform(b);
        if (aXform.GridUid is not { } grid || bXform.GridUid != grid)
            return false;

        return _transform.GetGridTilePositionOrDefault((a, aXform)) == _transform.GetGridTilePositionOrDefault((b, bXform));
    }

    // replays cmss13: life ticks every 2s take 1 off the counter, the xeno_slow effect adds 1.1 every 1s
    // until its ttl runs out. returns when the counter hit zero for good. effect ticks go first on a tie
    public static float Roll(YautjaCmssSlow slow, float lifePhase, float effectPhase)
    {
        var counter = slow.Ticks;
        var ttl = (float) slow.Effect.TotalSeconds;
        var life = lifePhase;
        var effect = effectPhase;
        var end = 0f;

        // 64s cap is way past anything cmss13 does, just a guard against bad yaml
        while (life < 64f)
        {
            if (effect < ttl && effect <= life)
            {
                counter += EffectStep;
                effect += EffectTick;
                continue;
            }

            if (counter > 0f)
            {
                counter = MathF.Max(counter - 1f, 0f);
                if (counter <= 0f)
                    end = life;
            }

            life += LifeTick;
            if (counter <= 0f && effect >= ttl)
                break;
        }

        return end;
    }
}
