using System.Collections.Generic;
using Robust.Shared.Map;

namespace Content.Shared._RMC14.OrbitalCannon;

public sealed partial class OrbitalCannonSystem
{
    public List<MapCoordinates> GetOrbitalExplosionLayers(MapCoordinates impact, bool multiZ)
    {
        var layers = new List<MapCoordinates>();
        if (!multiZ)
        {
            layers.Add(impact);
            return layers;
        }

        foreach (var mapId in _zLevels.GetAllNetworkMapIds(impact.MapId))
            layers.Add(new MapCoordinates(impact.Position, mapId));

        return layers;
    }

    private void CMUSpawnOrbitalTargetMarkers(EntityUid source, bool multiZ)
    {
        var impact = _transform.GetMapCoordinates(source);
        foreach (var layer in GetOrbitalExplosionLayers(impact, multiZ))
            Spawn(OrbitalTargetMarker, layer);
    }

    private void CMUFireOrbitalExplosionStep(EntityUid source, bool multiZ, OrbitalCannonExplosion step, MapCoordinates impact)
    {
        foreach (var layer in GetOrbitalExplosionLayers(impact, multiZ))
        {
            var coordinates = _transform.ToCoordinates(layer);
            if (step.CheckProtectionPer && !_area.CanOrbitalBombard(coordinates, out _))
                continue;

            if (step.ExplosionEffect is { } effectPrototype)
            {
                var effect = Spawn(effectPrototype, layer);
                _rmcExplosion.TryDoEffect(effect);
            }

            if (step.Type is { } type)
                _rmcExplosion.QueueExplosion(layer, type, step.Total, step.Slope, step.Max, source, canCreateVacuum: false);

            if (step.Fire is { } fire && step.FireRange > 0)
                _rmcFlammable.SpawnFireDiamond(fire, coordinates, step.FireRange);
        }
    }
}
