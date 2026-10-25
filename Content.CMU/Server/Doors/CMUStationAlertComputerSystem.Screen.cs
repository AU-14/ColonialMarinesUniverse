using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._RMC14.Areas;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.CMU14.Doors;
using Content.Shared.GameTicking;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Doors;

/// <summary>
///     The alert computer's screen: every area on its ship with a breach, a fire, bad air or a sealed firelock. Tiles
///     that were already airless when the computer started up, like the outer hull and its roof, are left out, so only
///     air the ship has lost shows up.
/// </summary>
public sealed partial class CMUStationAlertComputerSystem
{
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    /// <summary>Above this, in kelvin, a tile is burning.</summary>
    private const float FireTemperature = Atmospherics.FireMinimumTemperatureToExist;

    /// <summary>Below this, in kelvin, the air is dangerously cold.</summary>
    private const float FreezingTemperature = Atmospherics.T0C - 30f;

    /// <summary>Moles of a poisonous gas on one tile before it counts. A normal tile holds about 104.</summary>
    private const float ToxicMoles = 0.2f;

    /// <summary>Carbon dioxide builds up a little from breathing, so it gets more leeway.</summary>
    private const float CarbonDioxideMoles = 5f;

    /// <summary>Oxygen partial pressure, in kPa, below which the air can't be breathed for long.</summary>
    private const float LowOxygenPressure = 16f;

    private static readonly Gas[] ToxicGases =
        [Gas.Plasma, Gas.Tritium, Gas.Ammonia, Gas.NitrousOxide, Gas.Frezon, Gas.Phoron];

    /// <summary>Most serious first; an area is listed by its worst hazard.</summary>
    private static readonly CMUStationAlertHazard[] Severity =
    [
        CMUStationAlertHazard.Breach,
        CMUStationAlertHazard.Fire,
        CMUStationAlertHazard.Toxic,
        CMUStationAlertHazard.LowOxygen,
        CMUStationAlertHazard.LowPressure,
        CMUStationAlertHazard.HighPressure,
        CMUStationAlertHazard.Freezing,
        CMUStationAlertHazard.FirelockSealed,
    ];

    /// <summary>Per grid, the tiles that were airless when the ship was first checked.</summary>
    private readonly Dictionary<EntityUid, HashSet<Vector2i>> _airless = new();

    private readonly List<Vector2i> _tiles = new();

    private void InitializeScreen()
    {
        SubscribeLocalEvent<CMUStationAlertComputerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMUStationAlertComputerComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _airless.Clear());
    }

    private void OnMapInit(Entity<CMUStationAlertComputerComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.BaselineAt = _timing.CurTime + ent.Comp.BaselineDelay;
    }

    private void OnUiOpened(Entity<CMUStationAlertComputerComponent> ent, ref BoundUIOpenedEvent args)
    {
        ent.Comp.NextScan = _timing.CurTime + ent.Comp.ScanInterval;
        Scan(ent);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUStationAlertComputerComponent>();
        while (query.MoveNext(out var uid, out var computer))
        {
            if (!computer.Baselined && now >= computer.BaselineAt)
            {
                computer.Baselined = true;
                foreach (var (grid, areaGrid, _) in ShipGrids(uid))
                    GetAirless(grid, areaGrid);
            }

            if (now < computer.NextScan || !_ui.IsUiOpen(uid, CMUStationAlertUiKey.Key))
                continue;

            computer.NextScan = now + computer.ScanInterval;
            Scan((uid, computer));
        }
    }

    /// <summary>Every grid with areas on the computer's ship, with the deck it's on.</summary>
    private List<(EntityUid Grid, AreaGridComponent AreaGrid, int Deck)> ShipGrids(EntityUid computer)
    {
        var grids = new List<(EntityUid, AreaGridComponent, int)>();
        if (Transform(computer).MapUid is not { } map)
            return grids;

        var decks = new Dictionary<EntityUid, int>();
        if (_zLevels.TryGetZNetwork(map, out var network))
        {
            foreach (var (depth, deck) in _zLevels.GetOrderedNetworkMaps(network.Value))
                decks[deck] = depth;
        }
        else
        {
            decks[map] = 0;
        }

        var query = EntityQueryEnumerator<AreaGridComponent, MapGridComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var areaGrid, out _, out var xform))
        {
            if (xform.MapUid is { } gridMap && decks.TryGetValue(gridMap, out var deck))
                grids.Add((uid, areaGrid, deck));
        }

        return grids;
    }

    private HashSet<Vector2i> GetAirless(EntityUid grid, AreaGridComponent areaGrid)
    {
        if (_airless.TryGetValue(grid, out var airless))
            return airless;

        airless = new HashSet<Vector2i>();
        _tiles.Clear();
        _tiles.AddRange(areaGrid.Areas.Keys);
        var mixtures = _atmos.GetTileMixtures(grid, null, _tiles);
        for (var i = 0; i < _tiles.Count; i++)
        {
            if (mixtures?[i] is not { } air || air.Pressure < Atmospherics.HazardLowPressure)
                airless.Add(_tiles[i]);
        }

        _airless[grid] = airless;
        return airless;
    }

    private void Scan(Entity<CMUStationAlertComputerComponent> ent)
    {
        var areas = new Dictionary<(EntProtoId<AreaComponent> Area, int Deck), Tally>();
        var grids = ShipGrids(ent);

        foreach (var (grid, areaGrid, deck) in grids)
        {
            var airless = GetAirless(grid, areaGrid);
            _tiles.Clear();
            foreach (var tile in areaGrid.Areas.Keys)
            {
                if (!airless.Contains(tile))
                    _tiles.Add(tile);
            }

            var mixtures = _atmos.GetTileMixtures(grid, null, _tiles);
            if (mixtures == null)
                continue;

            for (var i = 0; i < _tiles.Count; i++)
            {
                if (mixtures[i] is not { } air)
                    continue;

                var hazards = Hazards(air);
                if (hazards == CMUStationAlertHazard.None)
                    continue;

                var key = (areaGrid.Areas[_tiles[i]], deck);
                if (!areas.TryGetValue(key, out var tally))
                    areas[key] = tally = new Tally();

                tally.Hazards |= hazards;
                tally.Tiles++;
                tally.LowestPressure = MathF.Min(tally.LowestPressure, air.Pressure);
                tally.HighestTemperature = MathF.Max(tally.HighestTemperature, air.Temperature);
            }
        }

        // Sealed firelocks, by the area they're in.
        var decks = grids.ToDictionary(g => g.Grid, g => g.Deck);
        var firelocks = EntityQueryEnumerator<CMUFirelockSealComponent, TransformComponent>();
        while (firelocks.MoveNext(out var uid, out var seal, out var xform))
        {
            if (!seal.Sealed ||
                xform.GridUid is not { } grid ||
                !decks.TryGetValue(grid, out var deck) ||
                !_area.TryGetArea(uid, out _, out var areaProto))
            {
                continue;
            }

            var key = (new EntProtoId<AreaComponent>(areaProto.ID), deck);
            if (!areas.TryGetValue(key, out var tally))
                areas[key] = tally = new Tally();

            tally.Hazards |= CMUStationAlertHazard.FirelockSealed;
            tally.SealedFirelocks++;
        }

        var alerts = areas
            .Select(pair => new CMUStationAlertEntry(
                AreaName(pair.Key.Area),
                pair.Key.Deck,
                pair.Value.Hazards,
                pair.Value.Tiles,
                pair.Value.Tiles > 0 ? pair.Value.LowestPressure : 0f,
                pair.Value.Tiles > 0 ? pair.Value.HighestTemperature : 0f,
                pair.Value.SealedFirelocks))
            .OrderBy(a => Rank(a.Hazards))
            .ThenByDescending(a => a.Tiles)
            .ThenBy(a => a.Area)
            .ToList();

        _ui.SetUiState(ent.Owner, CMUStationAlertUiKey.Key, new CMUStationAlertBuiState(alerts, _timing.CurTime));
    }

    private static CMUStationAlertHazard Hazards(GasMixture air)
    {
        var hazards = CMUStationAlertHazard.None;
        var pressure = air.Pressure;

        if (pressure < Atmospherics.HazardLowPressure)
            hazards |= CMUStationAlertHazard.Breach;
        else if (pressure < Atmospherics.WarningLowPressure)
            hazards |= CMUStationAlertHazard.LowPressure;
        else if (pressure > Atmospherics.WarningHighPressure)
            hazards |= CMUStationAlertHazard.HighPressure;

        if (air.Temperature > FireTemperature)
            hazards |= CMUStationAlertHazard.Fire;
        else if (air.Temperature < FreezingTemperature && pressure >= Atmospherics.HazardLowPressure)
            hazards |= CMUStationAlertHazard.Freezing;

        var toxic = 0f;
        foreach (var gas in ToxicGases)
            toxic += air.GetMoles(gas);

        if (toxic > ToxicMoles || air.GetMoles(Gas.CarbonDioxide) > CarbonDioxideMoles)
            hazards |= CMUStationAlertHazard.Toxic;

        var total = air.TotalMoles;
        if (pressure >= Atmospherics.WarningLowPressure && total > 0 &&
            pressure * air.GetMoles(Gas.Oxygen) / total < LowOxygenPressure)
        {
            hazards |= CMUStationAlertHazard.LowOxygen;
        }

        return hazards;
    }

    private static int Rank(CMUStationAlertHazard hazards)
    {
        for (var i = 0; i < Severity.Length; i++)
        {
            if ((hazards & Severity[i]) != 0)
                return i;
        }

        return Severity.Length;
    }

    private string AreaName(EntProtoId<AreaComponent> area)
    {
        return _prototypes.TryIndex(area, out var proto)
            ? proto.Name
            : Loc.GetString("cmu-station-alert-unknown-area");
    }

    private sealed class Tally
    {
        public CMUStationAlertHazard Hazards;
        public int Tiles;
        public float LowestPressure = float.MaxValue;
        public float HighestTemperature;
        public int SealedFirelocks;
    }
}
