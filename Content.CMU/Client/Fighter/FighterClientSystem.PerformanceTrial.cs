using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.Fighter;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Shared.Map.Components;

namespace Content.Client.CMU14.Fighter;

public sealed partial class FighterClientSystem
{
    private readonly List<double> _performanceFrames = [];
    private long _performancePrevious;
    private TimeSpan _performanceWindow;
    private FighterPhase _performancePhase;

    private void ResetPerformanceTrial()
    {
        _performanceFrames.Clear();
        _performancePrevious = 0;
        _performanceWindow = TimeSpan.Zero;
    }

    // Opt-in real renderer benchmark. No screenshots, releases or impact effects
    // occur during measurement. The same route, speed and window size are replayed.
    private void UpdatePerformanceTrial(FighterAircraftComponent aircraft, FighterSeatComponent seat)
    {
        if (!seat.Pilot || seat.Aircraft is not { } uid || !TryComp(uid, out FighterWeaponsComponent? weapons)) return;
        _trialStart ??= _timing.RealTime;
        var elapsed = (_timing.RealTime - _trialStart.Value).TotalSeconds;
        if (_trialStep == 0 && elapsed >= 10 && weapons.Targets.Count > 0)
        {
            RaiseNetworkEvent(new FighterSelectTargetEvent(weapons.Targets.Last().Id));
            var half = Math.Min(160, aircraft.Battlefield.Width * .4f);
            RaiseNetworkEvent(new FighterPlanEvent(aircraft.Home + new Vector2(-half, 30), aircraft.Home + new Vector2(half, -30)));
            RaiseNetworkEvent(new FighterSettingsEvent(350, 8));
            _display?.ShowOptics(true);
            _trialStep = 1;
        }
        if (_trialStep == 1 && elapsed >= 25)
        {
            RaiseNetworkEvent(new FighterCommandEvent(FighterCommand.Launch));
            _trialStep = 2;
        }
        var timestamp = Stopwatch.GetTimestamp();
        if (_performancePrevious != 0 && elapsed >= 15)
            _performanceFrames.Add((timestamp - _performancePrevious) * 1000d / Stopwatch.Frequency);
        _performancePrevious = timestamp;
        if (_performanceWindow == TimeSpan.Zero || _performancePhase != aircraft.Phase)
        {
            _performanceWindow = _timing.RealTime;
            _performancePhase = aircraft.Phase;
            _performanceFrames.Clear();
            Log.Info($"Fighter performance phase={aircraft.Phase}, position={aircraft.Position}.");
        }
        if ((_timing.RealTime - _performanceWindow).TotalSeconds >= 10)
        {
            if (_performanceFrames.Count > 0)
            {
                _performanceFrames.Sort();
                var mean = _performanceFrames.Average();
                var p95 = _performanceFrames[(int) ((_performanceFrames.Count - 1) * .95)];
                var slow = _performanceFrames.Count(frame => frame > 33.34) * 100d / _performanceFrames.Count;
                Log.Info($"Fighter performance phase={aircraft.Phase}, frames={_performanceFrames.Count}, mean_ms={mean:F2}, p95_ms={p95:F2}, fps={1000 / mean:F1}, over_33ms_pct={slow:F1}, position={aircraft.Position}.");
            }
            _performanceWindow = _timing.RealTime;
            _performanceFrames.Clear();
        }
        if (_trialStep == 2 && aircraft.Phase == FighterPhase.Pass)
        {
            MeasureRoofBatch(aircraft);
            _trialStep = 3;
        }
        if (_trialStep == 3 && aircraft.Phase == FighterPhase.Holding)
        {
            _trial = false;
            _configuration.SetCVar(FighterTrialCVars.Enabled, false);
            Log.Info("Fighter performance trial complete; manual controls restored.");
        }
    }

    private void MeasureRoofBatch(FighterAircraftComponent aircraft)
    {
        var maps = EntityManager.System<SharedMapSystem>();
        var roofs = EntityManager.System<SharedRoofSystem>();
        var transform = EntityManager.System<SharedTransformSystem>();
        var grids = new List<Entity<MapGridComponent>>();
        var roofTiles = new HashSet<Vector2i>();
        var world = new Box2(aircraft.Position - new Vector2(40, 30), aircraft.Position + new Vector2(40, 30));
        foreach (var map in new[] { aircraft.TerrainMap, aircraft.ViewMap }.Distinct())
        {
            grids.Clear();
            maps.FindGridsIntersecting(Comp<MapComponent>(map).MapId, world, ref grids, approx: true, includeMap: true);
            foreach (var grid in grids)
            {
                if (!TryComp(grid, out RoofComponent? roof)) continue;
                var tiles = new List<Vector2i>();
                var enumerator = maps.GetTilesIntersecting(grid, grid, world);
                while (enumerator.MoveNext(out var tile))
                    if (!tile.Tile.IsEmpty) tiles.Add(tile.GridIndices);
                var start = Stopwatch.GetTimestamp();
                foreach (var index in tiles) roofs.GetColor((grid, grid.Comp, roof), index);
                var legacyMs = (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
                start = Stopwatch.GetTimestamp();
                roofs.GetEntityRoofTiles(grid, transform.GetInvWorldMatrix(grid).TransformBox(world), roofTiles);
                foreach (var index in tiles) roofs.GetColor((grid, grid.Comp, roof), index, roofTiles.Contains(index));
                var batchMs = (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
                Log.Info($"Fighter roof benchmark: map={ToPrettyString(map)}, tiles={tiles.Count}, exact={tiles.Count(roofTiles.Contains)}, legacy_ms={legacyMs:F2}, batch_ms={batchMs:F2}.");
            }
        }
    }
}
