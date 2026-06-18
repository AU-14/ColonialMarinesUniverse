using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared.FixedPoint;
using Robust.Shared.IoC;

namespace Content.Shared._CMU14.Medical.Human.Systems;

public sealed partial class HumanBoneKnittingSystem : EntitySystem
{
    [Dependency] private SharedHumanMedicalSystem _medical = default!;

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<HumanMedicalComponent, ActiveBoneKnittingComponent>();
        while (query.MoveNext(out var uid, out var medical, out _))
        {
            var tick = CalculateBoneKnittingTick(medical);
            if (!tick.HasActiveKnitting)
            {
                _medical.RefreshActiveMarkers(uid, medical);
                continue;
            }

            var result = HumanMedicalLedger.AdvanceBoneKnitting(medical, FixedPoint2.New(frameTime));
            if (!result.Applied)
            {
                _medical.RefreshActiveMarkers(uid, medical);
                continue;
            }

            _medical.RefreshActiveMarkers(uid, medical);

            var ev = new HumanBoneKnittingTickEvent(uid, result, tick.ActiveRegions, frameTime);
            RaiseLocalEvent(uid, ref ev);
        }
    }

    public static HumanBoneKnittingTick CalculateBoneKnittingTick(HumanMedicalComponent medical)
    {
        var activeRegions = 0;
        var shortestRemaining = FixedPoint2.Zero;

        foreach (var region in medical.Regions)
        {
            if (!region.Skeletal.Knitting)
                continue;

            activeRegions++;
            if (shortestRemaining == FixedPoint2.Zero ||
                region.Skeletal.KnittingSecondsRemaining < shortestRemaining)
            {
                shortestRemaining = region.Skeletal.KnittingSecondsRemaining;
            }
        }

        return new HumanBoneKnittingTick(activeRegions, shortestRemaining);
    }
}

public readonly record struct HumanBoneKnittingTick(
    int ActiveRegions,
    FixedPoint2 ShortestRemaining)
{
    public bool HasActiveKnitting => ActiveRegions > 0;
}

[ByRefEvent]
public readonly record struct HumanBoneKnittingTickEvent(
    EntityUid Body,
    MedicalTransactionResult Result,
    int ActiveRegionsBeforeTick,
    float FrameTime);
