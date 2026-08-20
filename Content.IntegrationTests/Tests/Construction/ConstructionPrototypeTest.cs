using System.Collections.Generic; // CMU14: error aggregation for single-pass validation
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Utility;
using Content.Server.Construction.Components;
using Content.Shared.Construction.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Construction
{
    [TestFixture]
    public sealed class ConstructionPrototypeTest : GameTest
    {
        // discount linter for construction graphs
        // TODO: Create serialization validators for these?
        // Top test definitely can be but writing a serializer takes ages.

        private static string[] _constructablePrototypes = GameDataScrounger.EntitiesWithComponent("Construction");
        private static string[] _constructions = GameDataScrounger.PrototypesOfKind<ConstructionPrototype>();

        [Test]
        [TestOf(typeof(ConstructionComponent))]
        [Description("Tests that every entity prototype with a construction component has a valid start node, and optionally a valid one for deconstruction.")]
        public async Task ConstructionComponentsValid() // CMU14 method: one pass instead of a test case per prototype (1455 cases here cost a pool cycle each)
        {
            var pair = Pair;
            var server = pair.Server;

            var protoMan = server.ResolveDependency<IPrototypeManager>();
            var errors = new List<string>();

            await server.WaitAssertion(() =>
            {
                foreach (var protoKey in _constructablePrototypes)
                {
                    var proto = protoMan.Index(protoKey);
                    var construction = (ConstructionComponent)proto.Components["Construction"].Component;

                    var graph = protoMan.Index<ConstructionGraphPrototype>(construction.Graph);

                    if (!graph.Nodes.ContainsKey(construction.Node))
                        errors.Add($"Found no node \"{construction.Node}\" on graph \"{graph.ID}\" for entity \"{proto.ID}\"!");

                    if (construction.DeconstructionNode is { } target && !graph.Nodes.ContainsKey(target))
                        errors.Add($"Invalid deconstruction node \"{target}\" on graph \"{graph.ID}\" for construction entity \"{proto.ID}\"!");
                }
            });

            Assert.That(errors, Is.Empty, string.Join("\n", errors));
        }

        [Test]
        [TestOf(typeof(ConstructionPrototype))]
        [Description("Tests that every construction prototype has a valid starting and target node, and a valid path between them.")]
        public async Task ConstructionFormsValidGraphs() // CMU14 method: one pass instead of a test case per prototype
        {
            var pair = Pair;
            var server = pair.Server;

            var protoMan = server.ResolveDependency<IPrototypeManager>();
            var entMan = server.ResolveDependency<IEntityManager>();
            var errors = new List<string>();

            await server.WaitAssertion(() =>
            {
                foreach (var protoKey in _constructions)
                {
                    var proto = protoMan.Index<ConstructionPrototype>(protoKey);
                    var start = proto.StartNode;
                    var target = proto.TargetNode;
                    var graph = protoMan.Index(proto.Graph);

                    if (!graph.Nodes.ContainsKey(start))
                        errors.Add($"Found no startNode \"{start}\" on graph \"{graph.ID}\"!");
                    if (!graph.Nodes.ContainsKey(target))
                        errors.Add($"Found no targetNode \"{target}\" on graph \"{graph.ID}\"!");
                    if (!graph.TryPath(start, target, out var path))
                    {
                        errors.Add($"Unable to find path from \"{start}\" to \"{target}\" on graph \"{graph.ID}\"");
                        continue;
                    }

                    if (path is not { Length: >= 1 })
                    {
                        errors.Add($"Unable to find path from \"{start}\" to \"{target}\" on graph \"{graph.ID}\".");
                        continue;
                    }

                    var next = path![0];
                    var nextId = next.Entity.GetId(null, null, new(entMan));
                    if (nextId is null)
                    {
                        errors.Add($"The next node ({next.Name}) in the path from the start node ({start}) to the target node ({target}) must specify an entity! Graph: {graph.ID}");
                        continue;
                    }

                    if (!protoMan.TryIndex(nextId, out EntityPrototype entity))
                    {
                        errors.Add($"The next node ({next.Name}) in the path from the start node ({start}) to the target node ({target}) specified an invalid entity prototype ({nextId} [{next.Entity}])");
                        continue;
                    }

                    if (!entity!.Components.ContainsKey("Construction"))
                        errors.Add($"The next node ({next.Name}) in the path from the start node ({start}) to the target node ({target}) specified an entity prototype ({next.Entity}) without a ConstructionComponent.");
                }
            });

            Assert.That(errors, Is.Empty, string.Join("\n", errors));
        }
    }
}
