using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.Fighter;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Fighter;

public sealed partial class FighterSystem
{
    private readonly record struct ManpadChart(Box2 Bounds, byte[] Terrain);
    private readonly Dictionary<EntityUid, ManpadChart> _manpadCharts = [];
    private readonly Dictionary<ICommonSession, (EntityUid Launcher, EntityUid Map)> _manpadRadarMaps = [];
    private readonly HashSet<ICommonSession> _activeRadarUsers = [];
    private TimeSpan _nextManpadRadarUpdate;

    private void UpdateManpadRadar(Entity<FighterManpadComponent> launcher, TransformComponent xform)
    {
        if (!_containers.TryGetContainingContainer(launcher.Owner, out var container) ||
            !TryComp(container.Owner, out ActorComponent? actor) || !_manpads.CanAim(launcher, container.Owner) ||
            xform.MapUid is not { } terrain) return;
        while (TryComp(terrain, out CMUZLevelMapComponent? level) && level.Depth > 0 && level.MapBelow is { } below)
            terrain = below;

        SendAirspaceRadar(launcher, container.Owner, terrain, _transform.GetWorldPosition(container.Owner),
            _manpads.IsAiming(launcher), _manpads.HasAmmo(launcher), launcher.Comp.ReadyAt, launcher.Comp.LaunchAt,
            launcher.Comp.IgnoreIFF, launcher.Comp.Target);
    }

    private void SendAirspaceRadar(EntityUid source, EntityUid user, EntityUid terrain, Vector2 position,
        bool aiming, bool loaded, TimeSpan readyAt, TimeSpan launchAt, bool ignoreIFF, EntityUid? target, bool organic = false)
    {
        if (!TryComp(user, out ActorComponent? actor)) return;

        if (!_manpadCharts.TryGetValue(terrain, out var chart))
        {
            // The radar uses exactly the same battlefield and sectors as the jets.
            foreach (var aircraft in _combatAircraft)
            {
                if (aircraft.Comp1.TerrainMap != terrain || !TryComp(aircraft, out FighterChartComponent? existing)) continue;
                chart = new ManpadChart(aircraft.Comp1.Battlefield, existing.Terrain);
                break;
            }
            if (chart.Terrain == null || chart.Terrain.Length == 0)
            {
                var flight = new FighterAircraftComponent();
                var newChart = new FighterChartComponent();
                BuildChart(terrain, flight, newChart);
                if (newChart.Terrain.Length == 0) return;
                chart = new ManpadChart(flight.Battlefield, newChart.Terrain);
            }
            _manpadCharts[terrain] = chart;
        }

        var session = actor.PlayerSession;
        var location = (source, terrain);
        var sendTerrain = !_manpadRadarMaps.TryGetValue(session, out var previous) || previous != location;
        _manpadRadarMaps[session] = location;
        _activeRadarUsers.Add(session);
        var faction = _iff.GetOperatorFaction(user);
        var ev = new FighterAirspaceRadarEvent
        {
            Launcher = GetNetEntity(source),
            TerrainMap = GetNetEntity(terrain),
            Battlefield = chart.Bounds,
            Terrain = sendTerrain ? chart.Terrain : null,
            OperatorPosition = position,
            IgnoreIFF = ignoreIFF,
            Organic = organic,
            Aiming = aiming,
            Loaded = loaded,
            CooldownSeconds = Math.Max(0, (float) (readyAt - _timing.CurTime).TotalSeconds),
            LockSeconds = Math.Max(0, (float) (launchAt - _timing.CurTime).TotalSeconds),
        };
        foreach (var aircraft in _combatAircraft)
        {
            var flight = aircraft.Comp1;
            if (flight.TerrainMap != terrain || !FighterFlight.InAirspace(flight) || !flight.Flying ||
                !chart.Bounds.Contains(flight.Position) || !HasCombatPilot(flight)) continue;
            var disposition = ignoreIFF || _iff.Hostile(faction, aircraft.Comp3.Faction)
                ? FighterContactDisposition.Hostile
                : FighterIFFSystem.Same(faction, aircraft.Comp3.Faction)
                    ? FighterContactDisposition.Friendly : FighterContactDisposition.Unknown;
            ev.Contacts.Add(new FighterAirspaceContact(flight.Position, flight.Heading, flight.Height,
                disposition, target == aircraft.Owner));
        }
        RaiseNetworkEvent(ev, session);
    }

    private void ClearInactiveManpadRadars()
    {
        foreach (var session in _manpadRadarMaps.Keys.ToArray())
        {
            if (!_activeRadarUsers.Contains(session)) _manpadRadarMaps.Remove(session);
        }
    }
}
