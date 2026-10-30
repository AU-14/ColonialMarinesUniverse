using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.Alert;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.ZLevels;

public sealed class CMURoofIndicatorTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task StationaryObserverSeesRoofRemovalWithoutChangingAreaPermissions()
    {
        var map = await Pair.CreateTestMap();
        EntityUid upper = default;
        EntityUid network = default;
        AreaInfoComponent info = default!;
        string coveredText = null!;
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var levels = Server.System<CMUZLevelsSystem>();
            upper = maps.CreateMap(runMapInit: true);
            var grid = SEntMan.EnsureComponent<MapGridComponent>(upper);
            maps.SetTile(upper, grid, Vector2i.Zero, map.Tile.Tile);
            var createdNetwork = levels.CreateZNetwork();
            network = createdNetwork;
            Assert.That(levels.TryAddMapsIntoZNetwork(createdNetwork, new() { [map.MapUid] = 0, [upper] = 1 }), Is.True);
            var areaGrid = SEntMan.EnsureComponent<AreaGridComponent>(map.Grid.Owner);
            Server.System<AreaSystem>().ReplaceArea(areaGrid, Vector2i.Zero, "RMCAreaBosenmoriBashoOob");
            var observer = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.AddComponent<AlertsComponent>(observer);
            info = SEntMan.AddComponent<AreaInfoComponent>(observer);
        });

        await RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(info.LastRestrictionState?.OpenToSky, Is.False);
            Assert.That(info.LastRestrictionState?.CloseAirSupport, Is.False);
            coveredText = info.LastRestrictionText!;
            var maps = Server.System<SharedMapSystem>();
            maps.SetTile(upper, SEntMan.GetComponent<MapGridComponent>(upper), Vector2i.Zero, Tile.Empty);
        });

        await RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(info.LastRestrictionState?.OpenToSky, Is.True);
            Assert.That(info.LastRestrictionState?.CloseAirSupport, Is.False);
            Assert.That(info.LastRestrictionText, Is.Not.EqualTo(coveredText), "Geometry changes must invalidate cached roof text without movement.");
            SEntMan.DeleteEntity(upper);
            SEntMan.DeleteEntity(network);
        });
    }
}
