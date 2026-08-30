using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Server.CMU14.Threats;
using Content.Server.GameTicking.Presets;
using Content.Server.Maps;
using Content.Shared.CMU14.Threats;
using Content.Shared._RMC14.Rules;
using Content.Shared.CMU14;
using Content.Shared.CMU14.Scenario;
using Content.Shared.CMU14.util;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using KillAllColonistRuleComponent = Content.Shared.CMU14.Threats.Rules.KillAllColonistRuleComponent;

namespace Content.IntegrationTests.CMU14.Threats;

[TestFixture]
public sealed class DistressSignalThreatMarkerTest
{
    private static readonly ProtoId<ThreatPrototype> XenoThreat = "XenoThreat";
    private static readonly ProtoId<ThreatPrototype> TribalThreat = "TribalsThreat";
    private const string DistressSignalPreset = "DistressSignal";
    private const int MarkerValidationPlayerCount = 100;

    [Test]
    public async Task TribalThreatIsNotAvailableForDistressSignal()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var tribalThreat = prototypes.Index<ThreatPrototype>(TribalThreat);

            Assert.That(
                ThreatVoteSelection.IsThreatAllowed(tribalThreat, DistressSignalPreset, null, null, playerCount: 1),
                Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DistressSignalPlanetsDoNotOfferSelectableTribalThreat()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.ResolveDependency<IComponentFactory>();
            var preset = prototypes.Index<GamePresetPrototype>(DistressSignalPreset);
            var offenders = new List<string>();
            var tribalThreat = prototypes.Index<ThreatPrototype>(TribalThreat);

            foreach (var planetId in GamePlanetPoolPrototype.ExpandPlanetIds(prototypes, preset.PlanetPool, preset.SupportedPlanets))
            {
                var planetProto = prototypes.Index<EntityPrototype>(planetId);
                if (!planetProto.TryComp<RMCPlanetMapPrototypeComponent>(out var planet, factory))
                    continue;

                if (planet.AllowedThreats.Any(threat => threat.Id == TribalThreat) &&
                    ThreatVoteSelection.IsThreatAllowed(tribalThreat, DistressSignalPreset, null, null, MarkerValidationPlayerCount))
                {
                    offenders.Add($"{planetId} ({planet.MapId})");
                }
            }

            Assert.That(offenders, Is.Empty,
                $"Distress Signal planets offer selectable {TribalThreat}: {string.Join(", ", offenders)}");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DistressSignalThreatWinConditionsDoNotCountColonists()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.ResolveDependency<IComponentFactory>();
            var offenders = new List<string>();

            foreach (var threat in prototypes.EnumeratePrototypes<ThreatPrototype>())
            {
                if (!ThreatVoteSelection.IsThreatAllowed(threat, "DistressSignal", null, null, MarkerValidationPlayerCount))
                    continue;

                foreach (var ruleId in threat.WinConditions)
                {
                    var rulePrototype = prototypes.Index<EntityPrototype>(ruleId);
                    if (rulePrototype.TryComp<KillAllColonistRuleComponent>(out _, factory))
                        offenders.Add($"{threat.ID}:{ruleId}");
                }
            }

            Assert.That(offenders, Is.Empty,
                $"Distress Signal threats should not count dead colonists: {string.Join(", ", offenders)}");
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("DistressSignal")]
    [TestCase("ColonyFall")]
    public async Task SupportedPostRoundstartThreatVotePlanetsHaveMarkersForAllowedThreats(string presetId)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var resources = server.ResolveDependency<IResourceManager>();
            var factory = server.ResolveDependency<IComponentFactory>();
            var preset = prototypes.Index<GamePresetPrototype>(presetId);

            foreach (var planetId in GamePlanetPoolPrototype.ExpandPlanetIds(prototypes, preset.PlanetPool, preset.SupportedPlanets))
            {
                var planetProto = prototypes.Index<EntityPrototype>(planetId);
                if (!planetProto.TryComp<RMCPlanetMapPrototypeComponent>(out var planet, factory))
                    continue;

                var gameMap = prototypes.Index<GameMapPrototype>(planet.MapId);
                foreach (var threatId in planet.AllowedThreats)
                {
                    var threat = prototypes.Index<ThreatPrototype>(threatId);
                    if (!ThreatVoteSelection.IsThreatAllowed(threat, presetId, null, null, MarkerValidationPlayerCount))
                        continue;

                    var partySpawn = prototypes.Index<PartySpawnPrototype>(threat.RoundStartSpawn);
                    var bodyCount = ThreatVoteSelection.CalculateBodyCount(partySpawn, MarkerValidationPlayerCount);
                    var requiredMarkers = new Dictionary<ThreatMarkerType, int>
                    {
                        [ThreatMarkerType.Leader] = bodyCount.Leaders,
                        [ThreatMarkerType.Member] = bodyCount.Members,
                        [ThreatMarkerType.Entity] = partySpawn.EntitiesToSpawn.Values.Sum(),
                    };

                    foreach (var (markerType, requiredCount) in requiredMarkers)
                    {
                        if (requiredCount <= 0)
                            continue;

                        var count = CountCompatibleMapMarkers(
                            resources,
                            prototypes,
                            factory,
                            gameMap,
                            partySpawn,
                            markerType);
                        Assert.That(count, Is.GreaterThan(0),
                            $"{planetId} ({gameMap.ID}) allows {threat.ID} for {presetId}, but its authored maps have no compatible {markerType} marker entries.");
                    }
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlanetMapsAllowingXenoThreatHaveSpawnMarkers()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var resources = server.ResolveDependency<IResourceManager>();
            var factory = server.ResolveDependency<IComponentFactory>();
            var xenoThreat = prototypes.Index<ThreatPrototype>(XenoThreat);
            var partySpawn = prototypes.Index<PartySpawnPrototype>(xenoThreat.RoundStartSpawn);

            var requiredMarkers = new Dictionary<ThreatMarkerType, int>
            {
                [ThreatMarkerType.Leader] = partySpawn.LeadersToSpawn.Values.Sum(),
                [ThreatMarkerType.Member] = partySpawn.GruntsToSpawn.Values.Sum(),
                [ThreatMarkerType.Entity] = partySpawn.EntitiesToSpawn.Values.Sum(),
            };

            foreach (var planetProto in prototypes.EnumeratePrototypes<EntityPrototype>())
            {
                if (!planetProto.TryComp<RMCPlanetMapPrototypeComponent>(out var planet, factory))
                    continue;

                if (planet.AllowedThreats.All(threat => threat.Id != XenoThreat))
                    continue;

                var gameMap = prototypes.Index<GameMapPrototype>(planet.MapId);
                foreach (var (markerType, requiredCount) in requiredMarkers)
                {
                    if (requiredCount <= 0)
                        continue;

                    var count = CountCompatibleMapMarkers(
                        resources,
                        prototypes,
                        factory,
                        gameMap,
                        partySpawn,
                        markerType);
                    Assert.That(count, Is.GreaterThan(0),
                        $"{planetProto.ID} ({gameMap.ID}) allows {XenoThreat}, but its authored maps have no compatible {markerType} marker entries.");
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    private static Dictionary<string, int> CountMapPrototypes(IResourceManager resources, ResPath mapPath)
    {
        using var file = resources.ContentFileRead(mapPath);
        using var reader = new StreamReader(file);
        var counts = new Dictionary<string, int>();

        while (reader.ReadLine() is { } line)
        {
            line = line.Trim();
            const string prefix = "- proto: ";
            if (!line.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            var proto = line[prefix.Length..];
            counts.TryGetValue(proto, out var existing);
            counts[proto] = existing + 1;
        }

        return counts;
    }

    private static int CountCompatibleMapMarkers(
        IResourceManager resources,
        IPrototypeManager prototypes,
        IComponentFactory factory,
        GameMapPrototype gameMap,
        PartySpawnPrototype partySpawn,
        ThreatMarkerType markerType)
    {
        var markerId = partySpawn.Markers.TryGetValue(markerType, out var id) ? id : string.Empty;
        var requiredTags = new[]
        {
            ScenarioMarkerTags.ForceHostile,
            ScenarioMarkerTags.Bucket(markerType.ToString()),
            ScenarioMarkerTags.MarkerId(markerId),
        };
        var count = 0;

        foreach (var mapPath in EnumerateMapPaths(gameMap))
        {
            foreach (var (prototypeId, occurrences) in CountMapPrototypes(resources, mapPath))
            {
                if (!prototypes.TryIndex<EntityPrototype>(prototypeId, out var prototype) ||
                    !prototype.TryComp<ScenarioSpawnMarkerComponent>(out var marker, factory) ||
                    marker.Kind != SpawnMarkerKind.ThreatMarker ||
                    requiredTags.Any(required =>
                        !marker.Tags.Contains(required, StringComparer.OrdinalIgnoreCase)))
                {
                    continue;
                }

                count += occurrences * Math.Max(1, marker.Count);
            }
        }

        return count;
    }

    private static IEnumerable<ResPath> EnumerateMapPaths(GameMapPrototype gameMap)
    {
        yield return gameMap.MapPath;

        foreach (var path in gameMap.MapsBelow)
        {
            yield return path;
        }

        foreach (var path in gameMap.MapsAbove)
        {
            yield return path;
        }
    }
}
