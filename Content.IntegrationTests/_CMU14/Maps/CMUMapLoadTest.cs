using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Shared.CCVar;
using Content.Shared.Maps;
using Robust.Shared.Configuration;
using Robust.Shared.EntitySerialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Content.Shared.Station.Components;

namespace Content.IntegrationTests._CMU14.Maps;

[TestFixture]
public sealed class CMUMapLoadTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Connected = false,
        Dirty = true,
    };

    [Test]
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.GridFill), false)]
    public async Task AllGameMapsLoad()
    {
        var server = Pair.Server;
        var mapSystem = SEntMan.System<SharedMapSystem>();
        var stationSystem = SEntMan.System<StationSystem>();
        var ticker = SEntMan.System<GameTicker>();
        GameMapPrototype[] maps = [];

        await server.WaitPost(() =>
        {
            maps = SProtoMan.EnumeratePrototypes<GameMapPrototype>()
                .Where(map => map.MapPath.ToString().StartsWith("/Maps/_CMU14/", StringComparison.Ordinal))
                .OrderBy(map => map.ID, StringComparer.Ordinal)
                .ToArray();
        });

        Assert.That(maps, Is.Not.Empty);

        foreach (var map in maps)
        {
            await server.WaitAssertion(() =>
            {
                var previousMaps = mapSystem.GetAllMapIds().ToHashSet();
                var previousStations = GetStations();
                Exception failure = null!;

                try
                {
                    var options = DeserializationOptions.Default with { InitializeMaps = true };
                    ticker.LoadGameMap(map, out _, options);
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
                finally
                {
                    var createdStations = GetStations()
                        .Where(station => !previousStations.Contains(station))
                        .ToArray();
                    foreach (var station in createdStations)
                    {
                        stationSystem.DeleteStation(station);
                    }

                    var createdMaps = mapSystem.GetAllMapIds()
                        .Where(mapId => !previousMaps.Contains(mapId))
                        .Reverse()
                        .ToArray();
                    foreach (var mapId in createdMaps)
                    {
                        mapSystem.DeleteMap(mapId);
                    }
                }

                if (failure is not null)
                    throw new Exception($"Failed to load CMU game map {map.ID} from {map.MapPath}.", failure);
            });

            await server.WaitRunTicks(1);
        }
    }

    private HashSet<EntityUid> GetStations()
    {
        var stations = new HashSet<EntityUid>();
        var query = SEntMan.EntityQueryEnumerator<StationDataComponent>();
        while (query.MoveNext(out var station, out _))
        {
            stations.Add(station);
        }

        return stations;
    }
}
