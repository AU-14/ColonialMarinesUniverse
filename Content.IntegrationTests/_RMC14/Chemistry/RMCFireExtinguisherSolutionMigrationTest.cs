using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Fluids.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.Item.ItemToggle;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._RMC14.Chemistry;

[TestFixture]
[TestOf(typeof(SolutionComponent))]
public sealed class RMCFireExtinguisherSolutionMigrationTest : GameTest
{
    private const string SolutionName = "spray";

    private static readonly IReadOnlyDictionary<string, int> Extinguishers =
        new Dictionary<string, int>
        {
            ["CMFireExtinguisher"] = 50,
            ["CMFireExtinguisherPortable"] = 30,
        };

    [SidedDependency(Side.Server)]
    private SharedSolutionContainerSystem _solutions = default!;

    [Test]
    public async Task ExtinguishersUseOneDirectSpraySolutionAndPreserveCapabilities()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var factory = SEntMan.ComponentFactory;

            foreach (var (prototypeId, capacity) in Extinguishers)
            {
                var prototype = SProtoMan.Index<EntityPrototype>(prototypeId);
                Assert.Multiple(() =>
                {
                    Assert.That(prototype.TryComp<SolutionComponent>(out _, factory), Is.True, prototypeId);
                    Assert.That(prototype.TryComp<SolutionContainerManagerComponent>(out _, factory), Is.False,
                        prototypeId);
                    Assert.That(prototype.TryComp<SolutionManagerComponent>(out _, factory), Is.False, prototypeId);
                    Assert.That(prototype.TryComp<RefillableSolutionComponent>(out var refillable, factory), Is.True,
                        prototypeId);
                    Assert.That(refillable!.Solution, Is.EqualTo(SolutionName), prototypeId);
                    Assert.That(prototype.TryComp<DrainableSolutionComponent>(out var drainable, factory), Is.True,
                        prototypeId);
                    Assert.That(drainable!.Solution, Is.EqualTo(SolutionName), prototypeId);
                    Assert.That(prototype.TryComp<SprayComponent>(out var spray, factory), Is.True, prototypeId);
                    Assert.That(spray!.Solution, Is.EqualTo(SolutionName), prototypeId);
                });

                var extinguisher = SEntMan.SpawnEntity(prototypeId, map.GridCoords);
                Assert.That(_solutions.TryGetSolution(
                    extinguisher,
                    SolutionName,
                    out var solutionEntity,
                    out var solution), Is.True, prototypeId);
                Assert.Multiple(() =>
                {
                    Assert.That(solutionEntity!.Value.Owner, Is.EqualTo(extinguisher), prototypeId);
                    Assert.That(SEntMan.HasComponent<SolutionContainerManagerComponent>(extinguisher), Is.False,
                        prototypeId);
                    Assert.That(SEntMan.HasComponent<SolutionManagerComponent>(extinguisher), Is.False, prototypeId);
                    Assert.That(_solutions.TryGetRefillableSolution(extinguisher, out var refillableEntity, out _),
                        Is.True, prototypeId);
                    Assert.That(refillableEntity!.Value.Owner, Is.EqualTo(extinguisher), prototypeId);
                    Assert.That(_solutions.TryGetDrainableSolution(extinguisher, out var drainableEntity, out _),
                        Is.True, prototypeId);
                    Assert.That(drainableEntity!.Value.Owner, Is.EqualTo(extinguisher), prototypeId);
                    AssertSolution(solution!, capacity, prototypeId);
                });
            }

        });

        // Let the inherited UseDelay's initial zero-time entry expire before exercising a real spray.
        await Server.WaitRunTicks(1);
        await Server.WaitAssertion(() =>
        {
            var fullSize = SEntMan.SpawnEntity("CMFireExtinguisher", map.GridCoords);
            Assert.That(_solutions.TryGetSolution(fullSize, SolutionName, out _, out var before), Is.True);
            Assert.That(before!.Volume, Is.EqualTo(FixedPoint2.New(50)));

            Assert.That(Server.System<ItemToggleSystem>().TryActivate(
                fullSize,
                predicted: false,
                showPopup: false), Is.True,
                "The inherited extinguisher safety must be disengaged through its gameplay toggle.");
            var spray = SEntMan.GetComponent<SprayComponent>(fullSize);
            Server.System<SpraySystem>().Spray(
                (fullSize, spray),
                map.MapCoords.Offset(new Vector2(1, 0)));

            Assert.That(_solutions.TryGetSolution(fullSize, SolutionName, out _, out var after), Is.True);
            Assert.That(after!.Volume, Is.EqualTo(FixedPoint2.New(45.02)),
                "The legacy 5u/3-cloud spray quantizes to three 1.66u fixed-point transfers.");
        });
    }

    private static void AssertSolution(Solution solution, int capacity, string prototypeId)
    {
        Assert.Multiple(() =>
        {
            Assert.That(solution.MaxVolume, Is.EqualTo(FixedPoint2.New(capacity)), prototypeId);
            Assert.That(solution.Volume, Is.EqualTo(FixedPoint2.New(capacity)), prototypeId);
            Assert.That(solution.Contents, Has.Count.EqualTo(1), prototypeId);
            Assert.That(solution.Contents[0].Reagent.Prototype, Is.EqualTo("Water"), prototypeId);
            Assert.That(solution.Contents[0].Quantity, Is.EqualTo(FixedPoint2.New(capacity)), prototypeId);
        });
    }
}
