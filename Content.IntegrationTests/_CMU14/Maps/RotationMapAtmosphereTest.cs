using System.Collections.Generic;
using Content.Server.Maps;
using Content.Shared._RMC14.Rules;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Maps;

[TestFixture]
public sealed class RotationMapAtmosphereTest
{
    private const string RMCDefaultMapPool = "RMCDefaultMapPool";

    [Test]
    public async Task InRotationMapsHaveMapAtmosphere()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var components = server.ResolveDependency<IComponentFactory>();
            var resources = server.ResolveDependency<IResourceManager>();
            var errors = new List<string>();
            var checkedPaths = new HashSet<ResPath>();

            if (prototypes.TryIndex<GameMapPoolPrototype>(RMCDefaultMapPool, out var pool))
            {
                foreach (var mapId in pool.Maps)
                {
                    CheckMapPaths(prototypes, resources, mapId, checkedPaths, errors);
                }
            }

            foreach (var planetPrototype in prototypes.EnumeratePrototypes<EntityPrototype>())
            {
                if (!planetPrototype.TryComp<RMCPlanetMapPrototypeComponent>(out var planet, components) ||
                    !planet!.InRotation)
                {
                    continue;
                }

                CheckMapPaths(prototypes, resources, planet.MapId, checkedPaths, errors);
            }

            Assert.That(
                errors,
                Is.Empty,
                "In-rotation map paths missing MapAtmosphere:\n" + string.Join('\n', errors));
        });

        await pair.CleanReturnAsync();
    }

    private static void CheckMapPaths(
        IPrototypeManager prototypes,
        IResourceManager resources,
        string mapId,
        HashSet<ResPath> checkedPaths,
        List<string> errors)
    {
        if (!prototypes.TryIndex<GameMapPrototype>(mapId, out var map))
        {
            errors.Add($"{mapId}: missing GameMapPrototype");
            return;
        }

        foreach (var path in EnumerateMapPaths(map))
        {
            if (!checkedPaths.Add(path))
                continue;

            using var reader = resources.ContentFileReadText(path);
            var text = reader.ReadToEnd();
            if (!text.Contains("- type: MapAtmosphere"))
                errors.Add($"{map.ID}: {path}");
        }
    }

    private static IEnumerable<ResPath> EnumerateMapPaths(GameMapPrototype map)
    {
        foreach (var path in map.MapsBelow)
        {
            yield return path;
        }

        yield return map.MapPath;

        foreach (var path in map.MapsAbove)
        {
            yield return path;
        }
    }
}
