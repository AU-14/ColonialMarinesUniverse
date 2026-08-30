using Content.IntegrationTests.Fixtures;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._RMC14.Chemistry;

[TestFixture]
[TestOf(typeof(SolutionComponent))]
public sealed class RMCBloodPuddleSolutionMigrationTest : GameTest
{
    private const string PrototypeId = "BloodDecalPuddle";
    private const string SolutionName = "puddle";

    [SidedDependency(Side.Server)]
    private SharedSolutionContainerSystem _solutions = default!;

    [Test]
    public async Task BloodPuddleUsesDirectSolution()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var factory = SEntMan.ComponentFactory;
            var prototype = SProtoMan.Index<EntityPrototype>(PrototypeId);

            Assert.Multiple(() =>
            {
                Assert.That(prototype.TryComp<SolutionComponent>(out _, factory), Is.True);
                Assert.That(prototype.TryComp<SolutionContainerManagerComponent>(out _, factory), Is.False);
                Assert.That(_solutions.TryGetSolution(prototype, SolutionName, out _), Is.True);
            });

            var puddle = SEntMan.SpawnEntity(PrototypeId, map.GridCoords);
            Assert.That(_solutions.TryGetSolution(puddle, SolutionName, out var solutionEntity, out _), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(solutionEntity!.Value.Owner, Is.EqualTo(puddle));
                Assert.That(SEntMan.HasComponent<SolutionContainerManagerComponent>(puddle), Is.False);
            });
        });
    }
}
