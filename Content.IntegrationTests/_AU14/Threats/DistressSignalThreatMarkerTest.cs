using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Server.Maps;
using Content.Shared._RMC14.Rules;
using Content.Shared.AU14.Threats;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._AU14.Threats;

[TestFixture]
public sealed class DistressSignalThreatMarkerTest
{
    private const string XenoThreat = "XenoThreat";

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
                [ThreatMarkerType.Entity] = partySpawn.entitiestospawn.Values.Sum(),
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
            _ => throw new InvalidOperationException($"Unknown threat marker id '{markerId}' for {markerType}."),
        };
    }
}
