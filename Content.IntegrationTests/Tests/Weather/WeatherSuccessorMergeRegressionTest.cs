#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Core;
using Content.Server.Weather;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Weather;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Weather;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Weather;

[TestFixture]
public sealed class WeatherSuccessorMergeRegressionTest : GameTest
{
    private static readonly string[] RmcWeatherPrototypes =
    [
        "RMCBigRedDust",
        "RMCBigRedSand",
        "RMCBigRedRocks",
        "RMCStrataClearsky",
        "RMCStrataSnowing",
        "RMCStrataBlizzard",
        "RMCStrataStorm",
        "RMCStrataStormLight",
        "RMCStrataStormVeryLight",
        "RMCHybrisaRain",
        "RMCHybrisaRainLight",
        "RMCHybrisaRainVeryLight",
        "RMCTrijentRainLight",
    ];

    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  id: WeatherMergeOpenArea
  components:
  - type: Area
    weatherEnabled: true

- type: entity
  id: WeatherMergeBlockedArea
  components:
  - type: Area
    weatherEnabled: false

- type: entity
  id: WeatherMergeBlocker
  components:
  - type: BlockWeather
""";

    [Test]
    public async Task RmcWeatherTypesResolveToStatusEffectsAndCycleAcrossZNetwork()
    {
        var lower = await Pair.CreateTestMap(initialized: true, "FloorBasalt");
        var upper = await Pair.CreateTestMap(initialized: true, "FloorBasalt");
        EntityUid networkUid = default;

        await Server.WaitAssertion(() =>
        {
            foreach (var id in RmcWeatherPrototypes)
            {
                var prototype = SProtoMan.Index<EntityPrototype>(id);
                Assert.That(prototype.TryComp<WeatherStatusEffectComponent>(out _, SEntMan.ComponentFactory),
                    Is.True, $"{id} must be an entity status effect rather than a deleted WeatherPrototype.");
            }

            var zLevels = Server.System<CMUZLevelsSystem>();
            var network = zLevels.CreateZNetwork();
            networkUid = network;
            Assert.That(zLevels.TryAddMapsIntoZNetwork(network, new Dictionary<EntityUid, int>
            {
                [lower.MapUid] = 0,
                [upper.MapUid] = 1,
            }), Is.True);

            var cycle = SEntMan.EnsureComponent<RMCWeatherCycleComponent>(lower.Grid.Owner);
            cycle.WeatherEvents =
            [
                new RMCWeatherEvent
                {
                    Name = "weather-merge-cycle",
                    Duration = TimeSpan.FromSeconds(30),
                    WeatherType = "RMCStrataStormLight",
                },
            ];
            cycle.LastEventCooldown = TimeSpan.Zero;
            cycle.MinTimeBetweenEvents = TimeSpan.FromHours(1);
            cycle.MinTimeVariance = TimeSpan.Zero;

            Server.System<RMCWeatherSystem>().Update(1f);
            Assert.Multiple(() =>
            {
                Assert.That(cycle.CurrentEvent, Is.Not.Null);
                Assert.That(cycle.CurrentEvent!.DurationRemaining, Is.EqualTo(TimeSpan.FromSeconds(29)));
            });

            var statuses = Server.System<StatusEffectsSystem>();
            var weather = Server.System<WeatherSystem>();
            Assert.That(statuses.TryGetStatusEffect(lower.MapUid, "RMCStrataStormLight", out var lowerEffect),
                Is.True);
            Assert.That(statuses.TryGetStatusEffect(upper.MapUid, "RMCStrataStormLight", out var upperEffect),
                Is.True, "the relative cycle duration must be applied to every map in the z-network");

            var lowerStatus = SEntMan.GetComponent<StatusEffectComponent>(lowerEffect!.Value);
            var upperStatus = SEntMan.GetComponent<StatusEffectComponent>(upperEffect!.Value);
            Assert.Multiple(() =>
            {
                Assert.That(lowerStatus.Duration, Is.EqualTo(TimeSpan.FromSeconds(30)));
                Assert.That(upperStatus.Duration, Is.EqualTo(TimeSpan.FromSeconds(30)));
            });

            var now = Server.Timing.CurTime;
            lowerStatus.StartEffectTime = now - TimeSpan.FromSeconds(7.5);
            lowerStatus.EndEffectTime = now + TimeSpan.FromSeconds(22.5);
            Assert.That(weather.GetWeatherPercent((lowerEffect.Value, lowerStatus)), Is.EqualTo(0.5f).Within(0.001f),
                "the first 15 seconds must ramp in from the relative start time");

            lowerStatus.StartEffectTime = now - TimeSpan.FromSeconds(22.5);
            lowerStatus.EndEffectTime = now + TimeSpan.FromSeconds(7.5);
            Assert.That(weather.GetWeatherPercent((lowerEffect.Value, lowerStatus)), Is.EqualTo(0.5f).Within(0.001f),
                "the final 15 seconds must ramp out from the remaining relative duration");
        });

        await Server.WaitPost(() =>
        {
            SEntMan.RemoveComponent<CMUZLevelMapComponent>(lower.MapUid);
            SEntMan.RemoveComponent<CMUZLevelMapComponent>(upper.MapUid);
            SEntMan.DeleteEntity(networkUid);
        });
        await Pair.RunUntilSynced();
    }

    [Test]
    public async Task WeatherEligibilityUnionsTileAreaRoofBlockerAndTileAboveRules()
    {
        var ordinary = await Pair.CreateTestMap(initialized: true, "FloorBasalt");
        var lower = await Pair.CreateTestMap(initialized: true, "FloorBasalt");
        var upper = await Pair.CreateTestMap(initialized: true, "FloorBasalt");

        await Server.WaitAssertion(() =>
        {
            var map = Server.System<SharedMapSystem>();
            var weather = Server.System<WeatherSystem>();
            var roofs = Server.System<SharedRoofSystem>();
            var areas = Server.System<AreaSystem>();
            var transform = Server.System<SharedTransformSystem>();
            var tiles = Server.ResolveDependency<ITileDefinitionManager>();
            var indices = ordinary.Tile.GridIndices;

            Assert.That(weather.CanWeatherAffect(
                (ordinary.Grid.Owner, ordinary.Grid.Comp, (RoofComponent?) null), ordinary.Tile), Is.True);

            map.SetTile(ordinary.Grid, indices, new Tile(tiles["Plating"].TileId));
            var tile = map.GetTileRef(ordinary.Grid, indices);
            Assert.That(weather.CanWeatherAffect(
                (ordinary.Grid.Owner, ordinary.Grid.Comp, (RoofComponent?) null), tile), Is.False,
                "ordinary grids must retain the upstream tile weather flag");

            map.SetTile(ordinary.Grid, indices, new Tile(tiles["FloorBasalt"].TileId));
            tile = map.GetTileRef(ordinary.Grid, indices);
            var roof = SEntMan.EnsureComponent<RoofComponent>(ordinary.Grid.Owner);
            roofs.SetRoof((ordinary.Grid.Owner, ordinary.Grid.Comp, roof), indices, true);
            Assert.That(weather.CanWeatherAffect((ordinary.Grid.Owner, ordinary.Grid.Comp, roof), tile), Is.False);
            roofs.SetRoof((ordinary.Grid.Owner, ordinary.Grid.Comp, roof), indices, false);

            var blocker = SEntMan.SpawnEntity("WeatherMergeBlocker", ordinary.GridCoords);
            transform.AnchorEntity(blocker);
            Assert.That(weather.CanWeatherAffect((ordinary.Grid.Owner, ordinary.Grid.Comp, roof), tile), Is.False,
                "an anchored BlockWeather entity must stop weather after roof and tile checks");
            SEntMan.DeleteEntity(blocker);

            var areaGrid = SEntMan.EnsureComponent<AreaGridComponent>(ordinary.Grid.Owner);
            map.SetTile(ordinary.Grid, indices, new Tile(tiles["Plating"].TileId));
            tile = map.GetTileRef(ordinary.Grid, indices);
            areas.ReplaceArea(areaGrid, indices, "WeatherMergeOpenArea");
            Assert.That(weather.CanWeatherAffect((ordinary.Grid.Owner, ordinary.Grid.Comp, roof), tile), Is.True,
                "Area grids must delegate to local weather settings instead of the upstream tile flag");
            areas.ReplaceArea(areaGrid, indices, "WeatherMergeBlockedArea");
            Assert.That(weather.CanWeatherAffect((ordinary.Grid.Owner, ordinary.Grid.Comp, roof), tile), Is.False);

            var lowerZ = SEntMan.EnsureComponent<CMUZLevelMapComponent>(lower.Grid.Owner);
            var upperZ = SEntMan.EnsureComponent<CMUZLevelMapComponent>(upper.Grid.Owner);
            lowerZ.MapAbove = upper.Grid.Owner;
            upperZ.MapBelow = lower.Grid.Owner;
            Assert.That(weather.CanWeatherAffect(
                (lower.Grid.Owner, lower.Grid.Comp, (RoofComponent?) null), lower.Tile), Is.False,
                "a nonempty tile at the same indices on the level above must block weather");

            map.SetTile(upper.Grid, upper.Tile.GridIndices, Tile.Empty);
            Assert.That(weather.CanWeatherAffect(
                (lower.Grid.Owner, lower.Grid.Comp, (RoofComponent?) null), lower.Tile), Is.True,
                "an empty tile above must not be treated as a roof");
        });

        await Server.WaitPost(() =>
        {
            SEntMan.RemoveComponent<CMUZLevelMapComponent>(lower.Grid.Owner);
            SEntMan.RemoveComponent<CMUZLevelMapComponent>(upper.Grid.Owner);
        });
        await Pair.RunUntilSynced();
    }
}

#pragma warning restore RA0002
