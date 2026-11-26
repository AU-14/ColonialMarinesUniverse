using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Construction.Prototypes;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.Construction;

public sealed class CMUBarricadeGhostTest : InteractionTest
{
    [Test]
    public async Task BuildBothPlannedEdges()
    {
        await StartConstruction("AU14BarricadeMetalRecipe");
        EntityUid east = default;
        await Client.WaitAssertion(() =>
        {
            var folding = CProtoMan.Index<ConstructionPrototype>("AU14BarricadeMetalDoorRecipe");
            Assert.That(CConSys.TrySpawnGhost(folding, CEntMan.GetCoordinates(TargetCoords),
                Direction.East, out var ghost), Is.True);
            east = ghost!.Value;
        });

        await InteractUsing("CMSheetMetal", 4);
        ClientAssertPrototype("AU14BarricadeMetal");

        Target = CEntMan.GetNetEntity(east);
        ConstructionGhostId = east.GetHashCode();
        await InteractUsing("CMSheetMetal", 5);
        ClientAssertPrototype("AU14BarricadeMetalDoor");
    }

    [Test]
    public async Task PlanBothEdgesOfACorner()
    {
        await Client.WaitAssertion(() =>
        {
            var metal = CProtoMan.Index<ConstructionPrototype>("AU14BarricadeMetalRecipe");
            var folding = CProtoMan.Index<ConstructionPrototype>("AU14BarricadeMetalDoorRecipe");
            var grille = CProtoMan.Index<ConstructionPrototype>("Grille");
            var tile = CEntMan.GetCoordinates(TargetCoords);

            Assert.That(CConSys.TrySpawnGhost(metal, tile, Direction.South, out var south), Is.True);
            Assert.That(CConSys.TrySpawnGhost(folding, tile, Direction.East, out _), Is.True);
            Assert.That(CConSys.TrySpawnGhost(folding, tile, Direction.South, out _), Is.False);
            Assert.That(CConSys.TrySpawnGhost(grille, tile, Direction.North, out _), Is.False);

            CConSys.ClearGhost(south!.Value.GetHashCode());
            Assert.That(CConSys.TrySpawnGhost(folding, tile, Direction.South, out _), Is.True);
            Assert.That(CConSys.TrySpawnGhost(metal, tile, Direction.East, out _), Is.False);
            CConSys.ClearAllGhosts();
            Assert.That(CConSys.TrySpawnGhost(grille, tile, Direction.North, out _), Is.True);
            Assert.That(CConSys.TrySpawnGhost(metal, tile, Direction.East, out _), Is.False);
            CConSys.ClearAllGhosts();
        });
    }
}
