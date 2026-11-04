using System.Numerics;
using Content.Server.CMU14.Expeditions;
using Content.Shared.CMU14.Fighter;
using Content.Shared.CMU14.ZLevels.Core.Components;

namespace Content.Server.CMU14.Fighter;

public sealed partial class FighterSystem
{
    public bool TryLaunchExpedition(EntityUid? pilot, EntityUid map)
    {
        if (!TryComp<CMUExpeditionMapComponent>(map, out var expedition) || !expedition.Ready || expedition.LandingBeacon == null ||
            !TryGetSeat(pilot, out var seat, out var aircraft) || !seat.Comp.Pilot ||
            aircraft.Comp.GroundState != FighterGroundState.Grounded || !TryTakeoff(pilot))
            return false;
        var flight = aircraft.Comp;
        // The original physical launch site remains the return destination. Only the combat theater changes.
        flight.TerrainMap = map;
        flight.ViewMap = TryComp<CMUZLevelMapComponent>(map, out var level) && level.MapAbove is { } above ? above : map;
        var chart = EnsureComp<FighterChartComponent>(aircraft);
        BuildChart(map, flight, chart);
        flight.Position = FighterFlight.HoldingPoint(flight);
        flight.Entry = flight.Home + new Vector2(0, flight.Battlefield.Height / 2 + 12);
        flight.Exit = flight.Home - new Vector2(0, flight.Battlefield.Height / 2 + 12);
        flight.Mark = null;
        flight.TrainingImpact = null;
        foreach (var crewSeat in new[] { flight.FrontSeat, flight.RearSeat })
        {
            if (crewSeat is not { } uid || !TryComp<FighterSeatComponent>(uid, out var crew)) continue;
            CancelQueuedFire((uid, crew));
            CancelLaserLock((uid, crew));
            ClearLaser((uid, crew));
            crew.SensorFocus = crew.SensorLock = crew.TargetPosition = null;
            crew.Target = null;
            Dirty(uid, crew);
        }
        Dirty(aircraft.Owner, chart);
        Dirty(aircraft);
        return true;
    }
}
