using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared.FixedPoint;
using Robust.Shared.IoC;

namespace Content.Shared._CMU14.Medical.Human.Systems;

public sealed partial class HumanTreatedWoundHealingSystem : EntitySystem
{
    [Dependency] private SharedHumanMedicalSystem _medical = default!;

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<HumanMedicalComponent, ActiveTreatedWoundHealingComponent>();
        while (query.MoveNext(out var uid, out var medical, out _))
        {
            var tick = CalculateTreatedWoundHealingTick(medical);
            if (!tick.HasActiveHealing)
            {
                _medical.RefreshActiveMarkers(uid, medical);
                continue;
            }

            var result = HumanMedicalLedger.AdvanceTreatedWoundHealing(medical, FixedPoint2.New(frameTime));
            if (!result.Applied)
            {
                _medical.RefreshActiveMarkers(uid, medical);
                continue;
            }

            _medical.RefreshActiveMarkers(uid, medical);

            var ev = new HumanTreatedWoundHealingTickEvent(
                uid,
                result,
                tick.ActiveInjuries,
                result.BruteHealed,
                result.BurnHealed,
                frameTime);
            RaiseLocalEvent(uid, ref ev);
        }
    }

    public static HumanTreatedWoundHealingTick CalculateTreatedWoundHealingTick(HumanMedicalComponent medical)
    {
        var activeInjuries = 0;
        var bruteRecoveryRate = FixedPoint2.Zero;
        var burnRecoveryRate = FixedPoint2.Zero;
        for (var i = 1; i < medical.Regions.Length; i++)
        {
            var region = medical.Regions[i].Region;
            if (region == BodyRegion.None)
                continue;

            HumanMedicalLedger.CalculateTreatedRecoveryRates(
                medical,
                region,
                out var regionBruteRecoveryRate,
                out var regionBurnRecoveryRate,
                out var regionActiveInjuries);

            activeInjuries += regionActiveInjuries;
            bruteRecoveryRate += regionBruteRecoveryRate;
            burnRecoveryRate += regionBurnRecoveryRate;
        }

        return new HumanTreatedWoundHealingTick(activeInjuries, bruteRecoveryRate, burnRecoveryRate);
    }
}

public readonly record struct HumanTreatedWoundHealingTick(
    int ActiveInjuries,
    FixedPoint2 BruteRecoveryRate,
    FixedPoint2 BurnRecoveryRate)
{
    public bool HasActiveHealing => ActiveInjuries > 0 &&
        (BruteRecoveryRate > FixedPoint2.Zero || BurnRecoveryRate > FixedPoint2.Zero);
}

[ByRefEvent]
public readonly record struct HumanTreatedWoundHealingTickEvent(
    EntityUid Body,
    MedicalTransactionResult Result,
    int ActiveInjuriesBeforeTick,
    FixedPoint2 BruteHealed,
    FixedPoint2 BurnHealed,
    float FrameTime);
