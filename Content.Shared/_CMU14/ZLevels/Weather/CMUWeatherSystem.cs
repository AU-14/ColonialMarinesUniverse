using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Weather;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.ZLevels.Weather;

/// <summary>
/// A subsystem that connects WeatherSystem with ZLevelSystem. Allows you to control the weather for the entire z-network at once.
/// </summary>
public sealed partial class CMUWeatherSystem : EntitySystem
{
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;
    [Dependency] private SharedWeatherSystem _weather = default!;

    public bool TrySetWeather(Entity<CMUZLevelsNetworkComponent?> network, EntProtoId? proto, TimeSpan? duration)
    {
        if (!Resolve(network, ref network.Comp))
            return false;

        var resolvedNetwork = (network.Owner, network.Comp);

        if (!_zLevels.TryGetDepthBounds(resolvedNetwork, out var minDepth, out var maxDepth))
            return false;

        var success = false;

        for (var depth = minDepth; depth <= maxDepth; depth++)
        {
            if (!_zLevels.TryGetMapAtDepth(resolvedNetwork, depth, out var map))
                continue;

            if (!TryComp<MapComponent>(map, out var mapComp))
                continue;

            success |= _weather.TrySetWeather(mapComp.MapId, proto, out _, duration);
        }

        return success;
    }
}
