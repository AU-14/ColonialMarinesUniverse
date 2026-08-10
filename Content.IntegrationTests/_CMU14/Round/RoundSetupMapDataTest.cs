#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Content.Shared.CMU.Round;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;
using Robust.UnitTesting.Pool;
using YamlDotNet.RepresentationModel;

namespace Content.IntegrationTests._CMU14.Round;

[TestFixture]
public sealed class RoundSetupMapDataTest
{
    private static readonly IReadOnlyDictionary<string, (int Consoles, int Lifts)> ExpectedEndpoints =
        new Dictionary<string, (int, int)>(StringComparer.Ordinal)
        {
            ["/Maps/_CMU14/BarkersMultiZ/barkers0.yml"] = (1, 1),
            ["/Maps/_CMU14/HopesRetreatMultiZ/HopesRetreatMultiZ0.yml"] = (1, 1),
            ["/Maps/_CMU14/LV624.yml"] = (2, 1),
            ["/Maps/_CMU14/StableGarrisonMultiZ/StableGarrisonMultiZ0.yml"] = (2, 1),
            ["/Maps/_CMU14/garrison.yml"] = (2, 1),
            ["/Maps/_CMU14/hybrisametro.yml"] = (2, 1),
            ["/Maps/_CMU14/lament.yml"] = (1, 1),
        };

    [Test]
    public async Task GroundMilitaryAsrsUsesSemanticEndpointsWithoutCatalogSnapshots()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var resources = server.ResolveDependency<IResourceManager>();
            var actual = new Dictionary<string, (int Consoles, int Lifts)>(StringComparer.Ordinal);
            var failures = new List<string>();

            foreach (var pathString in ExpectedEndpoints.Keys)
            {
                var path = new ResPath(pathString);

                using var stream = resources.ContentFileRead(path);
                using var reader = new StreamReader(stream);
                var yaml = new YamlStream();
                yaml.Load(reader);
                var root = (YamlMappingNode) yaml.Documents[0].RootNode;
                if (!root.Children.TryGetValue("entities", out var entityGroupsNode))
                    continue;

                foreach (var group in (YamlSequenceNode) entityGroupsNode)
                {
                    var groupMapping = (YamlMappingNode) group;
                    var prototype = Scalar(groupMapping, "proto");
                    if (prototype is "CMASRSConsoleGovfor" or "CMCargoElevatorGovfor")
                    {
                        failures.Add($"{path} still places force-specific ASRS prototype {prototype}.");
                        continue;
                    }

                    var isConsole = prototype == "CMURoundAsrsConsole";
                    var isLift = prototype == "CMURoundAsrsLift";
                    if (!isConsole && !isLift)
                        continue;

                    foreach (var entity in (YamlSequenceNode) groupMapping.Children["entities"])
                    {
                        var entityMapping = (YamlMappingNode) entity;
                        var uid = Scalar(entityMapping, "uid");
                        var components = (YamlSequenceNode) entityMapping.Children["components"];
                        var endpoint = FindComponent(components, "RoundSetupEndpoint");
                        if (endpoint == null || Scalar(endpoint, "side") != nameof(RoundSide.Govfor))
                        {
                            failures.Add($"{path} entity {uid} ({prototype}) has no explicit Govfor endpoint side.");
                            continue;
                        }

                        if (endpoint.Children.Count != 2)
                            failures.Add($"{path} entity {uid} stores data beyond endpoint type and side.");

                        if (isConsole &&
                            FindComponent(components, "RequisitionsComputer") is { } requisitions &&
                            requisitions.Children.ContainsKey("categories"))
                        {
                            failures.Add($"{path} entity {uid} retains a serialized ASRS catalog snapshot.");
                        }

                        var counts = actual.GetValueOrDefault(path.ToString());
                        actual[path.ToString()] = isConsole
                            ? (counts.Consoles + 1, counts.Lifts)
                            : (counts.Consoles, counts.Lifts + 1);
                    }
                }
            }

            Assert.Multiple(() =>
            {
                Assert.That(failures, Is.Empty);
                foreach (var (path, expected) in ExpectedEndpoints)
                    Assert.That(actual.GetValueOrDefault(path), Is.EqualTo(expected), path);
            });
        });

        await pair.CleanReturnAsync();
    }

    private static YamlMappingNode? FindComponent(YamlSequenceNode components, string type)
    {
        foreach (var component in components)
        {
            var mapping = (YamlMappingNode) component;
            if (Scalar(mapping, "type") == type)
                return mapping;
        }

        return null;
    }

    private static string Scalar(YamlMappingNode mapping, string key)
    {
        return ((YamlScalarNode) mapping.Children[key]).Value!;
    }
}
