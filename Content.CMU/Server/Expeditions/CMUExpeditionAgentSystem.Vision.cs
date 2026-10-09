using System.Numerics;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.NPC.Components;
using Content.Shared.Physics;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    [Dependency] private SharedRoofSystem _visionRoofs = default!;
    private readonly Dictionary<MapId, List<EntityUid>> _visionLights = new();
    private readonly Dictionary<EntityCoordinates, float> _illuminationCache = new();
    private TimeSpan _nextLightSnapshot;

    private bool SightLine(EntityUid observer, EntityCoordinates point, float range) =>
        _interaction.InRangeUnobstructed(observer, point, range,
            CollisionGroup.Impassable | CollisionGroup.InteractImpassable,
            predicate: entity => entity == observer || HasComp<NpcFactionMemberComponent>(entity) || TransparentWindow(entity)) &&
        !SmokeOccludes(Transform(observer).Coordinates, point);

    private bool CanSpot(EntityUid observer, EntityUid target)
    {
        if (!TryComp<CMUExpeditionAgentComponent>(observer, out var agent))
            return true;
        var point = Transform(target).Coordinates;
        return _transform.InRange(Transform(observer).Coordinates, point, agent.DarkSightRange) ||
            Illumination(point) >= agent.MinimumSightLight;
    }

    private float Illumination(EntityCoordinates point)
    {
        if (_timing.CurTime >= _nextLightSnapshot)
        {
            // Share one bounded-age snapshot across every observer. No world light scan per target.
            _nextLightSnapshot = _timing.CurTime + TimeSpan.FromSeconds(0.25);
            _illuminationCache.Clear();
            _visionLights.Clear();
            var query = EntityQueryEnumerator<PointLightComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var light, out var transform))
            {
                if (light.ContainerOccluded || light.Energy <= 0 ||
                    !light.Enabled && !(IsFlare(uid) && Comp<ExpendableLightComponent>(uid).Activated))
                    continue;
                if (!_visionLights.TryGetValue(transform.MapID, out var lights))
                    _visionLights[transform.MapID] = lights = new List<EntityUid>();
                lights.Add(uid);
            }
        }
        if (_illuminationCache.TryGetValue(point, out var cached))
            return cached;
        var map = _transform.ToMapCoordinates(point);
        var mapUid = Transform(point.EntityId).MapUid;
        var level = mapUid is { } mapEntity && TryComp<MapLightComponent>(mapEntity, out var ambient)
            ? Luminance(ambient.AmbientLightColor) : 0;
        if (_turf.TryGetTileRef(point, out var tile) && TryComp<MapGridComponent>(tile.Value.GridUid, out var grid))
        {
            Color? roof = null;
            if (TryComp<RoofComponent>(tile.Value.GridUid, out var explicitRoof))
                roof = _visionRoofs.GetColor((tile.Value.GridUid, grid, explicitRoof), tile.Value.GridIndices);
            else if (TryComp<ImplicitRoofComponent>(tile.Value.GridUid, out var implicitRoof))
                roof = implicitRoof.Color;
            if (roof is { } color)
                level = level * (1 - color.A) + Luminance(Color.FromSrgb(color)) * color.A;
        }
        if (_visionLights.TryGetValue(map.MapId, out var sources))
        foreach (var uid in sources)
        {
            if (!TryComp<PointLightComponent>(uid, out var light) || light.ContainerOccluded ||
                !TryComp(uid, out TransformComponent? transform) || transform.MapID != map.MapId)
                continue;
            var radius = light.Radius;
            if (IsFlare(uid) && TryComp<ExpendableLightComponent>(uid, out var flare))
            {
                if (!flare.Activated)
                    continue;
                // RMC's radius animation is client-only. Use its seven-metre lit radius,
                // tapering with the real finite fade timer, without altering the actual light.
                radius = flare.CurrentState == ExpendableLightState.Fading
                    ? 1 + 6 * Math.Clamp(flare.StateExpiryTime / Math.Max(0.01f, (float) flare.FadeOutDuration.TotalSeconds), 0, 1)
                    : 2.5f + 4.5f * Math.Clamp(((float) flare.GlowDuration.TotalSeconds - flare.StateExpiryTime) / 5, 0, 1);
            }
            else if (!light.Enabled)
                continue;
            var origin = _transform.GetWorldPosition(uid) + _transform.GetWorldRotation(uid).RotateVec(light.Offset);
            var delta = map.Position - origin;
            var distance = delta.Length();
            if (radius <= 0 || distance >= radius)
                continue;
            // Arbitrary client texture masks cannot be sampled here. Only their immediate
            // source is credited, avoiding illumination behind a directional flashlight.
            if (light.LightMask != null && distance > 1)
                continue;
            var fraction = distance / radius;
            var contribution = light.Energy * Luminance(Color.FromSrgb(light.Color)) *
                (1 - fraction) * (1 - fraction) / (1 + light.Falloff * fraction);
            if (contribution < 0.005f ||
                !_interaction.InRangeUnobstructed(new MapCoordinates(origin, map.MapId), map, radius,
                    CollisionGroup.Impassable | CollisionGroup.InteractImpassable,
                    predicate: entity => entity == uid || HasComp<NpcFactionMemberComponent>(entity) || TransparentWindow(entity)))
                continue;
            level += contribution;
        }
        _illuminationCache[point] = level;
        return level;
    }

    private static float Luminance(Color color) => color.R * 0.2126f + color.G * 0.7152f + color.B * 0.0722f;
}
