using Content.Server._RMC14.Rules.DistressSignal;
using Robust.Shared.Serialization.Markdown.Mapping;

namespace Content.Server.GameTicking;

public sealed partial class GameTicker
{
    [Dependency] private CMDistressSignalRuleSystem _rmcDistressSignal = default!;

    private void AddRMCReplayMapMetadata(MappingDataNode metadata)
    {
        var maps = new List<string>();

        if (_gameMapManager.GetSelectedMap() is { } selectedMap)
            maps.Add(selectedMap.MapName);

        if (_rmcDistressSignal.SelectedPlanetMapName is { } planetMap)
            maps.Add(planetMap);

        metadata["maps"] = _serialman.WriteValue(maps, notNullableOverride: true);
    }
}
