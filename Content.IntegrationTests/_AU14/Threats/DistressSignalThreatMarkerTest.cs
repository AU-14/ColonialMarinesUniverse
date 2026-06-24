using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Server.AU14.Threats;
using Content.Server.GameTicking.Presets;
using Content.Server.Maps;
using Content.Shared._RMC14.Rules;
using Content.Shared.AU14;
using Content.Shared.AU14.Threats;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._AU14.Threats;

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

            foreach (var planetId in preset.SupportedPlanets)
            {
                var planetProto = prototypes.Index<EntityPrototype>(planetId);
                if (!planetProto.TryGetComponent<RMCPlanetMapPrototypeComponent>(out var planet, factory))
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
                    if (rulePrototype.TryGetComponent<KillAllColonistRuleComponent>(out _, factory))
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

            foreach (var planetId in preset.SupportedPlanets)
            {
                var planetProto = prototypes.Index<EntityPrototype>(planetId);
                if (!planetProto.TryGetComponent<RMCPlanetMapPrototypeComponent>(out var planet, factory))
                    continue;

                var gameMap = prototypes.Index<GameMapPrototype>(planet.MapId);
                var mapProtoCounts = CountMapPrototypes(resources, gameMap.MapPath);

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

                        var markerPrototype = GetThreatMarkerPrototype(partySpawn, markerType);
                        mapProtoCounts.TryGetValue(markerPrototype, out var count);
                        Assert.That(count, Is.GreaterThan(0),
                            $"{planetId} ({gameMap.ID}) allows {threat.ID} for {presetId}, but {gameMap.MapPath} has no {markerPrototype} entries.");
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
                if (!planetProto.TryGetComponent<RMCPlanetMapPrototypeComponent>(out var planet, factory))
                    continue;

                if (planet.AllowedThreats.All(threat => threat.Id != XenoThreat))
                    continue;

                var gameMap = prototypes.Index<GameMapPrototype>(planet.MapId);
                var mapProtoCounts = CountMapPrototypes(resources, gameMap.MapPath);

                foreach (var (markerType, requiredCount) in requiredMarkers)
                {
                    if (requiredCount <= 0)
                        continue;

                    var markerPrototype = GetThreatMarkerPrototype(partySpawn, markerType);
                    mapProtoCounts.TryGetValue(markerPrototype, out var count);
                    Assert.That(count, Is.GreaterThan(0),
                        $"{planetProto.ID} ({gameMap.ID}) allows {XenoThreat}, but {gameMap.MapPath} has no {markerPrototype} entries.");
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

    private static string GetThreatMarkerPrototype(PartySpawnPrototype partySpawn, ThreatMarkerType markerType)
    {
        var markerId = partySpawn.Markers.TryGetValue(markerType, out var id) ? id : string.Empty;
        return markerId switch
        {
            "" => markerType switch
            {
                ThreatMarkerType.Leader => "threatleaderspawnmarker",
                ThreatMarkerType.Member => "threatmemberspawnmarker",
                ThreatMarkerType.Entity => "threatentityspawnmarker",
                _ => throw new ArgumentOutOfRangeException(nameof(markerType), markerType, null),
            },
            "xenocf" => markerType switch
            {
                ThreatMarkerType.Leader => "xenocfthreatleaderspawnmarker",
                ThreatMarkerType.Member => "xenocfthreatmemberspawnmarker",
                ThreatMarkerType.Entity => "xenocfthreatentityspawnmarker",
                _ => throw new ArgumentOutOfRangeException(nameof(markerType), markerType, null),
            },
            "cultcfmarker" => markerType switch
            {
                ThreatMarkerType.Leader => "cultistcfthreatleaderspawnmarker",
                ThreatMarkerType.Member => "cultistcfthreatmemberspawnmarker",
                _ => throw new ArgumentOutOfRangeException(nameof(markerType), markerType, null),
            },
            _ => throw new InvalidOperationException($"Unknown threat marker id '{markerId}' for {markerType}."),
        };
    }
}
