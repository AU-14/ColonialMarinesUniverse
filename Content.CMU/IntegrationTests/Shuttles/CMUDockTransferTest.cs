using Content.IntegrationTests.Fixtures;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;

namespace Content.IntegrationTests.CMU14.Shuttles;

[TestFixture]
public sealed class CMUDockTransferTest : GameTest
{
    [Test]
    public async Task GridTransferReconnectsDockButOrdinaryReanchoringDoesNot()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var transform = Server.System<SharedTransformSystem>();
            var docking = Server.System<DockingSystem>();
            var otherGrid = maps.CreateGridEntity(map.MapId);
            var replacement = maps.CreateGridEntity(map.MapId);
            maps.SetTile(otherGrid, Vector2i.Zero, map.Tile.Tile);
            maps.SetTile(replacement, Vector2i.Zero, map.Tile.Tile);
            transform.SetLocalPosition(otherGrid, new Vector2(0, -1));
            var first = SEntMan.SpawnAttachedTo("AirlockShuttle", map.GridCoords);
            var second = SEntMan.SpawnAttachedTo("AirlockShuttle",
                new EntityCoordinates(otherGrid, new Vector2(.5f, .5f)), rotation: Angle.FromDegrees(180));
            var firstDock = SEntMan.GetComponent<DockingComponent>(first);
            var secondDock = SEntMan.GetComponent<DockingComponent>(second);
            Assert.That(docking.CanDock((first, firstDock), (second, secondDock)), Is.True);
            docking.Dock((first, firstDock), (second, secondDock));

            transform.Unanchor(first);
            transform.SetCoordinates(first, new EntityCoordinates(replacement, new Vector2(.5f, .5f)));
            transform.AnchorEntity(first);
            Assert.That(firstDock.DockedWith, Is.EqualTo(second));
            Assert.That(secondDock.DockedWith, Is.EqualTo(first));
            Assert.That(firstDock.DockJoint!.BodyAUid == replacement.Owner ||
                        firstDock.DockJoint.BodyBUid == replacement.Owner, Is.True,
                "The weld must connect the replacement grid, not retain the previous grid.");

            transform.Unanchor(first);
            transform.AnchorEntity(first);
            Assert.That(firstDock.DockedWith, Is.Null, "Reanchoring on the same grid must not implicitly dock again.");
            Assert.That(secondDock.DockedWith, Is.Null);
        });
    }
}
