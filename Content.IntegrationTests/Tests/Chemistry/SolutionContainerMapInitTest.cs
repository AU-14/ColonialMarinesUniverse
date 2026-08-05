#nullable enable
using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Nutrition.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Chemistry;

[TestFixture]
public sealed class SolutionContainerMapInitTest : GameTest
{
    [Test]
    public async Task EntityPrototypesDoNotOverlapLegacyAndMapInitSolutions()
    {
        var prototypeManager = Server.ResolveDependency<IPrototypeManager>();
        var componentFactory = Server.ResolveDependency<IComponentFactory>();

        await Server.WaitAssertion(() =>
        {
            var overlaps = new List<string>();

#pragma warning disable CS0612 // The compatibility component is what this regression test guards against.
            foreach (var prototype in prototypeManager.EnumeratePrototypes<EntityPrototype>())
            {
                if (!prototype.TryComp<SolutionContainerManagerComponent>(out var legacy, componentFactory) ||
                    legacy.Solutions is not { } legacySolutions)
                    continue;

                var mapInitSolutions = new HashSet<string>();
                if (prototype.TryComp<SolutionComponent>(out var current, componentFactory))
                    mapInitSolutions.Add(current.Id);

                // IngestionSystem creates the configured solution during MapInit even when the prototype
                // does not declare a Solution component, so it can also race the legacy compatibility port.
                if (prototype.TryComp<EdibleComponent>(out var edible, componentFactory))
                    mapInitSolutions.Add(edible.Solution);

                foreach (var solutionId in mapInitSolutions)
                {
                    if (legacySolutions.ContainsKey(solutionId))
                        overlaps.Add($"{prototype.ID}: {solutionId}");
                }
            }
#pragma warning restore CS0612

            overlaps.Sort(StringComparer.Ordinal);
            Assert.That(overlaps, Is.Empty,
                "Entity prototypes declare a legacy solution that is also supplied during MapInit:" +
                Environment.NewLine + string.Join(Environment.NewLine, overlaps));
        });
    }
}
