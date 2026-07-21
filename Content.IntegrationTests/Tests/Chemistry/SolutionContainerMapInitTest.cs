#nullable enable
using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Chemistry;

[TestFixture]
public sealed class SolutionContainerMapInitTest : GameTest
{
    [Test]
    public async Task EntityPrototypesDoNotDeclareOverlappingLegacySolutions()
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
                    !prototype.TryComp<SolutionComponent>(out var current, componentFactory))
                {
                    continue;
                }

                if (legacy.Solutions?.ContainsKey(current.Id) == true)
                    overlaps.Add($"{prototype.ID}: {current.Id}");
            }
#pragma warning restore CS0612

            overlaps.Sort(StringComparer.Ordinal);
            Assert.That(overlaps, Is.Empty,
                "Entity prototypes declare the same solution through Solution and SolutionContainerManager:" +
                Environment.NewLine + string.Join(Environment.NewLine, overlaps));
        });
    }
}
