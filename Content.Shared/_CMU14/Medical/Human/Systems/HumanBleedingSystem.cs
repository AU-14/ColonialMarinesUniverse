using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared.FixedPoint;

namespace Content.Shared._CMU14.Medical.Human.Systems;

public sealed class HumanBleedingSystem : EntitySystem
{
    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<HumanMedicalComponent, ActiveBleedingComponent>();
        while (query.MoveNext(out var uid, out var medical, out _))
        {
            var tick = CalculateBleedingTick(medical);
            if (!tick.HasActiveBleeding)
                continue;

            var ev = new HumanBleedingTickEvent(uid, tick.TotalRate, tick.ActiveSources, frameTime);
            RaiseLocalEvent(uid, ref ev);
        }
    }

    public static HumanBleedingTick CalculateBleedingTick(HumanMedicalComponent medical)
    {
        var total = FixedPoint2.Zero;
        var activeSources = 0;

        foreach (var source in medical.BleedSources)
        {
            if (!source.Active)
                continue;

            total += source.Rate;
            activeSources++;
        }

        return new HumanBleedingTick(total, activeSources);
    }
}

public readonly record struct HumanBleedingTick(
    FixedPoint2 TotalRate,
    int ActiveSources)
{
    public bool HasActiveBleeding => ActiveSources > 0 && TotalRate > FixedPoint2.Zero;
}

[ByRefEvent]
public readonly record struct HumanBleedingTickEvent(
    EntityUid Body,
    FixedPoint2 TotalRate,
    int ActiveSources,
    float FrameTime);
