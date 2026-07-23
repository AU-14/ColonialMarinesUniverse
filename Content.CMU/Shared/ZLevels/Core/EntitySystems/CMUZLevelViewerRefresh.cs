using Content.Shared._CMU14.ZLevels.Core.Components;

namespace Content.Shared._CMU14.ZLevels.Core.EntitySystems;

public static class CMUZLevelViewerRefresh
{
    public static bool ShouldRefreshViewerForNetwork(EntityUid? viewerMap, CMUZLevelsNetworkComponent network)
    {
        return viewerMap is { } map && network.ZLevelByEntity.ContainsKey(map);
    }
}
