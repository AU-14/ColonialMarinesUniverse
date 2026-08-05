using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using NUnit.Framework;
using Robust.Shared.GameObjects;

namespace Content.Tests.Shared._CMU14.ZLevels;

[TestFixture]
public sealed class CMUZLevelViewerRefreshTest
{
    [Test]
    public void ViewerOnNetworkMapRefreshesWhenNetworkUpdates()
    {
        var viewerMap = new EntityUid(1);
        var otherMap = new EntityUid(2);
        var network = new CMUZLevelsNetworkComponent
        {
            ZLevelByEntity =
            {
                [viewerMap] = 0,
            },
        };

        Assert.That(CMUZLevelViewerRefresh.ShouldRefreshViewerForNetwork(viewerMap, network), Is.True);
        Assert.That(CMUZLevelViewerRefresh.ShouldRefreshViewerForNetwork(otherMap, network), Is.False);
        Assert.That(CMUZLevelViewerRefresh.ShouldRefreshViewerForNetwork(null, network), Is.False);
    }
}
