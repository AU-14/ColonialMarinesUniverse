using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared.FixedPoint;

namespace Content.Shared._CMU14.Medical.Human.Systems;

public sealed partial class HumanTourniquetSystem : EntitySystem
{
    [Dependency] private SharedHumanMedicalSystem _medical = default!;

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<HumanMedicalComponent, ActiveTourniquetComponent>();
        while (query.MoveNext(out var uid, out var medical, out _))
        {
            var tick = CalculateTourniquetTick(medical);
            if (!tick.HasActiveTourniquets)
            {
                _medical.RefreshActiveMarkers(uid, medical);
                continue;
            }

            var result = HumanMedicalLedger.AdvanceTourniquets(medical, FixedPoint2.New(frameTime));
            if (!result.Applied)
            {
                _medical.RefreshActiveMarkers(uid, medical);
                continue;
            }

            _medical.RefreshActiveMarkers(uid, medical);
        }
    }

    public static HumanTourniquetTick CalculateTourniquetTick(HumanMedicalComponent medical)
    {
        var activeRegions = 0;
        var shortestRemaining = FixedPoint2.Zero;

        foreach (var region in medical.Regions)
        {
            if (!region.Tourniquet.Applied ||
                region.Tourniquet.Necrotic ||
                region.Tourniquet.NecrosisSecondsRemaining <= FixedPoint2.Zero)
            {
                continue;
            }

            activeRegions++;
            if (shortestRemaining == FixedPoint2.Zero ||
                region.Tourniquet.NecrosisSecondsRemaining < shortestRemaining)
            {
                shortestRemaining = region.Tourniquet.NecrosisSecondsRemaining;
            }
        }

        return new HumanTourniquetTick(activeRegions, shortestRemaining);
    }
}

public readonly record struct HumanTourniquetTick(
    int ActiveRegions,
    FixedPoint2 ShortestNecrosisSecondsRemaining)
{
    public bool HasActiveTourniquets => ActiveRegions > 0;
}
