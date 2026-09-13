using Content.IntegrationTests.Fixtures;
using Content.Server.Decals;
using Content.Server.Fluids.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.IntegrationTests.Tests.Fluids;

[TestFixture]
[TestOf(typeof(AbsorbentSystem))]
public sealed class AbsorbentMergeRegressionTest : GameTest
{
    private static readonly EntProtoId WetMop = "AbsorbentMergeWetMop";
    private static readonly EntProtoId DirectMop = "AbsorbentMergeDirectMop";
    private static readonly EntProtoId User = "AbsorbentMergeUser";
    private static readonly ProtoId<ReagentPrototype> Cleaner = "AbsorbentMergeCleaner";

    [TestPrototypes]
    private const string Prototypes = """
- type: reagent
  parent: Water
  id: AbsorbentMergeCleaner
  absorbent: true
  tileReactions:
  - !type:CleanDecalsReaction
    cleanCost: 1

- type: entity
  id: AbsorbentMergeUser
  components:
  - type: Sprite

- type: entity
  id: AbsorbentMergeWetMop
  components:
  - type: Absorbent
    useAbsorberSolution: true
  - type: Solution
    id: absorbed
    solution:
      maxVol: 10

- type: entity
  id: AbsorbentMergeDirectMop
  components:
  - type: Absorbent
    useAbsorberSolution: false
  - type: Solution
    id: absorbed
    solution:
      maxVol: 20
""";

    [Test]
    public async Task TargetlessTileMopNeedsAbsorberAndRunsTileReactionBeforeCleaning()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var entities = Server.EntMan;
            var decals = Server.System<DecalSystem>();
            var solutions = Server.System<SharedSolutionContainerSystem>();
            var user = entities.SpawnEntity(User, map.GridCoords);
            var mop = entities.SpawnEntity(WetMop, map.GridCoords);

            Assert.That(decals.TryAddDecal(
                "burnt1",
                map.GridCoords,
                out var decal,
                cleanable: true), Is.True);

            var dryInteract = new AfterInteractEvent(user, mop, null, map.GridCoords, true);
            entities.EventBus.RaiseLocalEvent(mop, dryInteract);
            Assert.Multiple(() =>
            {
                Assert.That(dryInteract.Handled, Is.True);
                Assert.That(decals.ContainsDecal(map.Grid.Owner, decal), Is.True,
                    "A dry solution-backed mop must reject the tile without cleaning it.");
            });

            Assert.That(solutions.TryGetSolution(mop, "absorbed", out var solutionEnt, out var solution), Is.True);
            solutions.AddSolution(solutionEnt!.Value, new Solution(Cleaner, FixedPoint2.New(5)));

            var wetInteract = new AfterInteractEvent(user, mop, null, map.GridCoords, true);
            entities.EventBus.RaiseLocalEvent(mop, wetInteract);
            Assert.Multiple(() =>
            {
                Assert.That(wetInteract.Handled, Is.True);
                Assert.That(decals.ContainsDecal(map.Grid.Owner, decal), Is.False);
                Assert.That(solution!.GetTotalPrototypeQuantity(Cleaner), Is.EqualTo(FixedPoint2.New(4)),
                    "The tile reaction must consume its configured reagent cost before decal cleanup completes.");
            });
        });
    }

    [Test]
    public async Task PuddleMopCleansCoLocatedDecalsOnBothRemovalPaths()
    {
        var map = await Pair.CreateTestMap();

        EntityUid directPuddle = default;
        await Server.WaitAssertion(() =>
        {
            var entities = Server.EntMan;
            var absorbents = Server.System<AbsorbentSystem>();
            var decals = Server.System<DecalSystem>();
            var puddles = Server.System<PuddleSystem>();
            var solutions = Server.System<SharedSolutionContainerSystem>();
            var user = entities.SpawnEntity(User, map.GridCoords);

            var wetMop = entities.SpawnEntity(WetMop, map.GridCoords);
            var wetMopComp = entities.GetComponent<AbsorbentComponent>(wetMop);
            Assert.That(solutions.TryGetSolution(wetMop, wetMopComp.SolutionName, out var wetSolution), Is.True);
            solutions.AddSolution(wetSolution!.Value, new Solution("Water", FixedPoint2.New(5)));

            Assert.That(decals.TryAddDecal(
                "burnt1",
                map.GridCoords,
                out var absorberDecal,
                cleanable: true), Is.True);
            Assert.That(puddles.TrySpillAt(
                map.GridCoords,
                new Solution("Blood", FixedPoint2.New(5)),
                out var absorberPuddle,
                sound: false), Is.True);

            absorbents.Mop((wetMop, wetMopComp), user, absorberPuddle);
            Assert.That(decals.ContainsDecal(map.Grid.Owner, absorberDecal), Is.False,
                "The absorber-solution puddle path must clean co-located decals.");
            entities.DeleteEntity(absorberPuddle);

            var directMop = entities.SpawnEntity(DirectMop, map.GridCoords);
            var directMopComp = entities.GetComponent<AbsorbentComponent>(directMop);
            Assert.That(decals.TryAddDecal(
                "burnt1",
                map.GridCoords,
                out var directDecal,
                cleanable: true), Is.True);
            Assert.That(puddles.TrySpillAt(
                map.GridCoords,
                new Solution("Blood", FixedPoint2.New(5)),
                out directPuddle,
                sound: false), Is.True);

            absorbents.Mop((directMop, directMopComp), user, directPuddle);
            Assert.That(decals.ContainsDecal(map.Grid.Owner, directDecal), Is.False,
                "The direct-absorption removal path must clean co-located decals.");
        });

        await Server.WaitRunTicks(2);
        await Server.WaitAssertion(() =>
            Assert.That(Server.EntMan.EntityExists(directPuddle), Is.False,
                "An emptied puddle must be deleted on the direct-absorption path."));
    }

    [Test]
    public async Task SolutionProgressIncludesTransparentAvailableSegment()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var entities = Server.EntMan;
            var solutions = Server.System<SharedSolutionContainerSystem>();
            var mop = entities.SpawnEntity(WetMop, map.GridCoords);
            var absorbent = entities.GetComponent<AbsorbentComponent>(mop);

            Assert.That(solutions.TryGetSolution(mop, absorbent.SolutionName, out var solutionEnt), Is.True);
            solutions.AddSolution(solutionEnt!.Value, new Solution("Water", FixedPoint2.New(5)));
            solutions.AddSolution(solutionEnt.Value, new Solution("Cola", FixedPoint2.New(2)));
            solutions.UpdateChemicals(solutionEnt.Value);

            Assert.Multiple(() =>
            {
                Assert.That(absorbent.Progress, Has.Count.EqualTo(3),
                    "Absorbent, contaminant, and available-volume segments must remain distinct.");
                Assert.That(absorbent.Progress[Color.Transparent], Is.EqualTo(3f),
                    "Available capacity must use the upstream transparent progress segment.");
                Assert.That(absorbent.Progress.Values.Sum(), Is.EqualTo(10f));
            });
        });
    }
}
