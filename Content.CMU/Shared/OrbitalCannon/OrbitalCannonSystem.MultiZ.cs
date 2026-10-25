using System.Collections.Generic;
using Content.Shared.CMU14.ZLevels.Ordnance;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.OrbitalCannon;

public sealed partial class OrbitalCannonSystem
{
    // Preserve the previous API for callers that select the ordnance permission gate.
    public void SpawnExplosion(EntProtoId prototype, EntityCoordinates coordinates, CMUTopDownOrdnanceKind kind)
    {
        if (_net.IsClient)
            return;

        var source = Spawn(prototype, coordinates);
        EnsureComp<CMUOrbitalExplosionKindComponent>(source).Kind = kind;
    }

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
        var kind = TryComp<CMUOrbitalExplosionKindComponent>(source, out var ordnance)
            ? ordnance.Kind
            : CMUTopDownOrdnanceKind.OrbitalBombardment;

        foreach (var layer in GetOrbitalExplosionLayers(impact, multiZ))
        {
            var coordinates = _transform.ToCoordinates(layer);
            if (step.CheckProtectionPer && kind != CMUTopDownOrdnanceKind.Scuttle &&
                !(kind == CMUTopDownOrdnanceKind.Mortar
                    ? _area.CanMortarFire(coordinates)
                    : _area.CanOrbitalBombard(coordinates, out _)))
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
