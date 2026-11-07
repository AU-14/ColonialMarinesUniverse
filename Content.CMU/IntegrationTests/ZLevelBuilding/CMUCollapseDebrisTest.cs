using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevelBuilding;
using Robust.Shared.Physics.Components;

namespace Content.IntegrationTests.CMU.ZLevelBuilding;

public sealed class CMUCollapseDebrisTest : GameTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task CollapsedWallsCannotCreateSolidContactPilesOnOpenFloors(bool structuralCollapse)
    {
        var source = await Pair.CreateTestMap();
        var landing = await Pair.CreateTestMap();
        EntityUid wall = default;
        await Server.WaitAssertion(() =>
        {
            wall = SSpawnAtPosition("CMWallMetal", source.GridCoords);
            if (structuralCollapse)
            {
                typeof(ZLevelSupportSystem).GetMethod("DropTileAndContents", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .Invoke(Server.System<ZLevelSupportSystem>(), new object[] { source.Grid, Vector2i.Zero, landing.MapUid });
            }
            else
            {
                typeof(ZCaveInSystem).GetMethod("DropBuiltEntitiesToLevelBelow", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .Invoke(Server.System<ZCaveInSystem>(), new object[]
                    {
                        landing.MapUid, source.Grid.Owner, source.Grid.Comp, Vector2i.Zero,
                        Server.System<SharedTransformSystem>().ToMapCoordinates(source.GridCoords).Position,
                    });
            }
            Assert.That(SComp<TransformComponent>(wall).MapUid, Is.EqualTo(landing.MapUid));
            SSpawnAtPosition("CMWallMetal", SComp<TransformComponent>(wall).Coordinates);
        });
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() => Assert.That(SComp<PhysicsComponent>(wall).ContactCount,
            Is.Zero, "A fallen wall must not grind against structures appearing at its landing position."));
    }
}
