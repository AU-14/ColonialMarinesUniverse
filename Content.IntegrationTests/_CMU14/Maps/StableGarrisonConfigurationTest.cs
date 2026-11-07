using System.Collections.Generic;
using System.Linq;
using Content.Server.GameTicking.Presets;
using Content.Shared._RMC14.Rules;
using Content.Shared.Maps;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Maps;

[TestFixture]
public sealed class StableGarrisonConfigurationTest
{
    private const string PlanetId = "CMUPlanetStableGarrisonRedux";
    private const string MapId = "StableGarrisonRedux";
    private const string PresetId = "DistressSignal";

    [Test]
    public async Task RegularReduxIsTheOnlyMidnightRotationForTheOnlyVotablePreset()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var components = server.ResolveDependency<IComponentFactory>();
            var resources = server.ResolveDependency<IResourceManager>();

            var rotation = prototypes.EnumeratePrototypes<EntityPrototype>()
                .Where(proto => proto.TryComp<RMCPlanetMapPrototypeComponent>(out var planet, components) &&
                                planet!.InRotation)
                .Select(proto => proto.ID)
                .ToArray();
            Assert.That(rotation, Is.EqualTo(new[] { PlanetId }));

            var votablePresets = prototypes.EnumeratePrototypes<GamePresetPrototype>()
                .Where(preset => preset.ShowInVote)
                .Select(preset => preset.ID)
                .ToArray();
            Assert.That(votablePresets, Is.EqualTo(new[] { PresetId }));

            var preset = prototypes.Index<GamePresetPrototype>(PresetId);
            Assert.That(preset.SupportedPlanets, Is.EqualTo(new[] { PlanetId }));

            var planetPrototype = prototypes.Index<EntityPrototype>(PlanetId);
            Assert.That(
                planetPrototype.TryComp<RMCPlanetMapPrototypeComponent>(out var planet, components),
                Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(planet!.MapId, Is.EqualTo(MapId));
                Assert.That(planet.InRotation, Is.True);
                Assert.That(planet.DaycycleEnabled, Is.False);
                Assert.That(planet.GovforInShip, Is.False);
            });

            var map = prototypes.Index<GameMapPrototype>(MapId);
            Assert.Multiple(() =>
            {
                Assert.That(map.MapsBelow, Has.Count.EqualTo(1));
                Assert.That(map.MapsAbove, Has.Count.EqualTo(4));
                Assert.That(map.Stations, Has.Count.EqualTo(1));
                Assert.That(
                    map.Stations.Single().Value.StationPrototype.ToString(),
                    Is.EqualTo("CMUStandardGroundBase"));
            });

            foreach (var path in EnumerateMapPaths(map))
            {
                using var reader = resources.ContentFileReadText(path);
                var text = reader.ReadToEnd();
                Assert.Multiple(() =>
                {
                    Assert.That(text, Does.Not.Contain("- type: DayNightCycle"), path.ToString());
                    Assert.That(text, Does.Contain("ambientLightColor: '#000000'"), path.ToString());
                });
            }

            var mapText = string.Join("\n", EnumerateMapPaths(map).Select(path =>
            {
                using var reader = resources.ContentFileReadText(path);
                return reader.ReadToEnd();
            }));
            Assert.Multiple(() =>
            {
                Assert.That(mapText, Does.Contain("- proto: CMUSpawnPointGOVFOROfficer"));
                Assert.That(mapText, Does.Contain("- proto: CMUSpawnPointGOVFOREnlisted"));
            });
        });

        await pair.CleanReturnAsync();
    }

    private static IEnumerable<ResPath> EnumerateMapPaths(GameMapPrototype map)
    {
        yield return map.MapPath;
        foreach (var path in map.MapsAbove)
        {
            yield return path;
        }

        foreach (var path in map.MapsBelow)
        {
            yield return path;
        }
    }
}
