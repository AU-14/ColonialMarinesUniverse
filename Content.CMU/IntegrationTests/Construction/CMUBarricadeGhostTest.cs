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
            var tile = CEntMan.GetCoordinates(TargetCoords);
            Assert.That(CConSys.TrySpawnGhost(folding, tile, Direction.South, out _), Is.False);
            Assert.That(CConSys.TrySpawnGhost(folding, tile, Direction.East, out var ghost), Is.True);
            east = ghost!.Value;
        });

        await InteractUsing("CMSheetMetal", 4);
        ClientAssertPrototype("AU14BarricadeMetal");

        Target = CEntMan.GetNetEntity(east);
        ConstructionGhostId = east.GetHashCode();
        await InteractUsing("CMSheetMetal", 5);
        ClientAssertPrototype("AU14BarricadeMetalDoor");
    }
}
