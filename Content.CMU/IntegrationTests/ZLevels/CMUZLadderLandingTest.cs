using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Interaction;
using Content.Shared.Movement.Pulling.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.ZLevels;

public sealed class CMUZLadderLandingTest : GameTest
{
    [Test]
    public async Task RemovedLandingKeepsClimberAndPulledBodyOnOriginalLevel()
    {
        var upper = await Pair.CreateTestMap();
        var lower = await Pair.CreateTestMap();
        EntityUid climber = default;
        EntityUid body = default;
        EntityUid ladder = default;
        EntityUid network = default;
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var levels = Server.System<CMUZLevelsSystem>();
            var createdNetwork = levels.CreateZNetwork();
            network = createdNetwork;
            Assert.That(levels.TryAddMapsIntoZNetwork(createdNetwork,
                new() { [upper.MapUid] = 0, [lower.MapUid] = -1 }), Is.True);
            maps.SetTile(upper.Grid, new Vector2i(1, 0), upper.Tile.Tile);
            maps.SetTile(upper.Grid, new Vector2i(1, 1), upper.Tile.Tile);
            maps.SetTile(lower.Grid, new Vector2i(1, 0), lower.Tile.Tile);
            climber = SSpawnAtPosition("CMMobHuman", new EntityCoordinates(upper.Grid.Owner, new Vector2(1.5f, 0.5f)));
            body = SSpawnAtPosition("CMMobHuman", new EntityCoordinates(upper.Grid.Owner, new Vector2(1.5f, 1.5f)));
            ladder = SSpawnAtPosition("RUMCPitLadderDown", new EntityCoordinates(upper.Grid.Owner, new Vector2(0.5f)));
            SComp<CMUZLevelLadderComponent>(ladder).Delay = TimeSpan.FromSeconds(1);
            Assert.That(Server.System<PullingSystem>().TryStartPull(climber, body), Is.True);
            SEntMan.EventBus.RaiseLocalEvent(ladder, new ActivateInWorldEvent(climber, ladder, true));
        });

        await RunSeconds(0.25f);
        await Server.WaitAssertion(() =>
            Server.System<SharedMapSystem>().SetTile(lower.Grid, Vector2i.Zero, Tile.Empty));
        await RunSeconds(1.25f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<TransformComponent>(climber).MapUid, Is.EqualTo(upper.MapUid),
                "A landing removed during the climb must not drop the climber into the opening.");
            Assert.That(SComp<TransformComponent>(body).MapUid, Is.EqualTo(upper.MapUid),
                "Rejecting a climb must also leave the pulled body on its original level.");
            Server.System<SharedMapSystem>().SetTile(lower.Grid, Vector2i.Zero, lower.Tile.Tile);
            SEntMan.EventBus.RaiseLocalEvent(ladder, new ActivateInWorldEvent(climber, ladder, true));
        });

        await RunSeconds(1.25f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<TransformComponent>(climber).MapUid, Is.EqualTo(lower.MapUid));
            Assert.That(SComp<TransformComponent>(body).MapUid, Is.EqualTo(lower.MapUid),
                "Restoring a safe landing must allow both entities to use the ladder normally.");
            SEntMan.DeleteEntity(network);
        });
    }

    [Test]
    public async Task LadderTopSupportsClimbingIntoAnUpperOpening()
    {
        var lower = await Pair.CreateTestMap();
        EntityUid upper = default;
        EntityUid network = default;
        EntityUid climber = default;
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var levels = Server.System<CMUZLevelsSystem>();
            upper = maps.CreateMap(runMapInit: true);
            var grid = SEntMan.EnsureComponent<MapGridComponent>(upper);
            maps.SetTile(upper, grid, new Vector2i(1, 0), lower.Tile.Tile);
            var createdNetwork = levels.CreateZNetwork();
            network = createdNetwork;
            Assert.That(levels.TryAddMapsIntoZNetwork(createdNetwork,
                new() { [lower.MapUid] = 0, [upper] = 1 }), Is.True);
            var position = new EntityCoordinates(lower.Grid.Owner, new Vector2(0.5f));
            climber = SSpawnAtPosition("CMMobHuman", position);
            var ladder = SSpawnAtPosition("RUMCPitLadderUp", position);
            SComp<CMUZLevelLadderComponent>(ladder).Delay = TimeSpan.Zero;
            SEntMan.EventBus.RaiseLocalEvent(ladder, new ActivateInWorldEvent(climber, ladder, true));
        });

        await RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<TransformComponent>(climber).MapUid, Is.EqualTo(upper),
                "An opening supported by the ladder top is a safe landing, even without an upper floor tile.");
            SEntMan.DeleteEntity(upper);
            SEntMan.DeleteEntity(network);
        });
    }
}
