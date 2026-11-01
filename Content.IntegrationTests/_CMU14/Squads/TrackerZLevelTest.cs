using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared._RMC14.Tracker;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.Squads;

[TestFixture]
public sealed class TrackerZLevelTest : GameTest
{
    [Test]
    public async Task TrackerPointsAtTargetsOnOtherFloorsOfTheSameStack()
    {
        var maps = new List<EntityUid>();
        try
        {
            await Server.WaitAssertion(() =>
            {
                var mapSystem = Server.System<SharedMapSystem>();
                var zLevels = Server.System<CMUZLevelsSystem>();
                var lower = mapSystem.CreateMap();
                var upper = mapSystem.CreateMap();
                var unrelated = mapSystem.CreateMap();
                maps.AddRange(new[] { lower, upper, unrelated });

                var network = zLevels.CreateZNetwork();
                maps.Add(network);
                Assert.That(zLevels.TryAddMapsIntoZNetwork(network, new() { [lower] = 0, [upper] = 1 }), Is.True);

                MapId Id(EntityUid map) => SEntMan.GetComponent<MapComponent>(map).MapId;
                var tracker = SEntMan.SpawnEntity(null, new MapCoordinates(Vector2.Zero, Id(lower)));
                var trackers = Server.System<TrackerSystem>();

                Assert.Multiple(() =>
                {
                    Assert.That(trackers.GetAlertSeverity(tracker, new MapCoordinates(new Vector2(5, 0), Id(upper))),
                        Is.Not.EqualTo(TrackerSystem.CenterSeverity), "a target on the floor above must still be tracked");
                    Assert.That(trackers.GetAlertSeverity(tracker, new MapCoordinates(new Vector2(5, 0), Id(unrelated))),
                        Is.EqualTo(TrackerSystem.CenterSeverity), "a target on an unrelated map cannot be tracked");
                });
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                foreach (var uid in maps)
                {
                    if (SEntMan.EntityExists(uid))
                        SEntMan.DeleteEntity(uid);
                }
            });
        }
    }
}
