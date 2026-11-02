using Content.IntegrationTests.Fixtures;
using Content.Client.CMU14.TacticalMap.Reconstruction;
using Robust.Client.UserInterface;
using Content.Server.CMU14.CaveSurvey;
using Content.Server.CMU14.ZLevelBuilding;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.CaveSurvey;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Content.Shared.CMU14.ZLevelBuilding;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Wall;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU.ZLevelBuilding;

public sealed class CMUCaveToolsTest : GameTest
{
    [Test]
    public async Task PersonalSurveyStreamsOnlyObservedCellsAndTracksItsOwner()
    {
        EntityUid map = default;
        EntityUid network = default;
        EntityUid actor = default;
        var session = Pair.Player!;
        var original = session.AttachedEntity;
        try
        {
            await Server.WaitAssertion(() =>
            {
                var maps = Server.System<SharedMapSystem>();
                map = maps.CreateMap(runMapInit: true);
                var grid = SEntMan.EnsureComponent<MapGridComponent>(map);
                var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
                for (var x = -1; x <= 10; x++)
                for (var y = -1; y <= 2; y++) maps.SetTile(map, grid, new Vector2i(x, y), floor);
                var levels = Server.System<CMUZLevelsSystem>();
                var createdNetwork = levels.CreateZNetwork();
                network = createdNetwork.Owner;
                Assert.That(levels.TryAddMapsIntoZNetwork(createdNetwork, new() { [map] = -3 }), Is.True);
                actor = SEntMan.SpawnEntity("MobHuman", new EntityCoordinates(map, new Vector2(0.5f)));
                Server.PlayerMan.SetAttachedEntity(session, actor);
                Server.System<CMUCaveSurveySystem>().Observe(actor);
            });
            await Pair.RunUntilSynced();
            await Server.WaitPost(() => Server.System<CMUCaveSurveySystem>().Open(actor));
            await Pair.RunSeconds(1);
            await Client.WaitAssertion(() =>
            {
                var view = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<CMUReconstructionWindow>().Single().SurveyView;
                var scene = view.Scene!;
                Assert.That(scene.LoadedChunks, Is.EqualTo(scene.TotalChunks));
                var local = Vector2i.Zero - scene.Origin;
                Assert.That(scene.Cells[CMUReconGeometry.Index(local.X, local.Y, -3 - scene.MinDepth, scene.Width, scene.Height)],
                    Is.EqualTo((byte) CMUReconMaterial.Ground));
                Assert.That(scene.Cells[CMUReconGeometry.Index(local.X + 7, local.Y, -3 - scene.MinDepth, scene.Width, scene.Height)],
                    Is.EqualTo((byte) CMUReconMaterial.Empty), "Existing nearby floor outside the recording must stay unknown over the wire.");
            });
            await Server.WaitPost(() => Server.System<SharedTransformSystem>().SetCoordinates(actor, new EntityCoordinates(map, new Vector2(2.5f, 0.5f))));
            await Pair.RunSeconds(1);
            await Client.WaitAssertion(() =>
            {
                var scene = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<CMUReconstructionWindow>().Single().SurveyView.Scene!;
                Assert.That(scene.OperatorPosition!.Value.X, Is.EqualTo(2.5f).Within(0.1f), "Navigation must follow the owner without requesting a new terrain snapshot.");
            });
            await Client.CheckSandboxed(typeof(Content.Client.Entry.EntryPoint).Assembly);
            await Client.CheckSandboxed(typeof(CMUCaveSurveyData).Assembly);
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                if (SEntMan.EntityExists(actor)) Server.System<SharedUserInterfaceSystem>().CloseUi(actor, CMUCaveSurveyUiKey.Key);
                Server.PlayerMan.SetAttachedEntity(session, original);
                if (SEntMan.EntityExists(actor)) SEntMan.DeleteEntity(actor);
            });
            await Pair.RunUntilSynced();
            if (map.IsValid()) await Pair.DeleteEntityTreeLeafFirst(map);
            if (network.IsValid()) await Pair.DeleteEntityTreeLeafFirst(network);
        }
    }

    [Test]
    public async Task SurveyClipsWallsAndSharesFrozenKnowledgeWithoutGeneratingTerrain()
    {
        var map = await Pair.CreateTestMap();
        var other = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var levels = Server.System<CMUZLevelsSystem>();
            var network = levels.CreateZNetwork();
            Assert.That(levels.TryAddMapsIntoZNetwork(network, new Dictionary<EntityUid, int> { [map.MapUid] = -3 }), Is.True);
            var otherNetwork = levels.CreateZNetwork();
            Assert.That(levels.TryAddMapsIntoZNetwork(otherNetwork, new Dictionary<EntityUid, int> { [other.MapUid] = -3 }), Is.True);
            for (var x = -1; x <= 2; x++)
            for (var y = -1; y <= 2; y++) maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
            var stone = SEntMan.EnsureComponent<ZGeneratedStoneComponent>(map.MapUid);
            stone.StoneGrid = map.Grid.Owner;
            stone.GeneratedChunks.UnionWith([new(-1, -1), new(-1, 0), new(0, -1), new(0, 0)]);
            var actor = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var reader = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var paper = SEntMan.SpawnEntity("CMUCaveSurveyMap", map.GridCoords);
            var rockTile = new Vector2i(1, 0);
            var rock = SEntMan.SpawnEntity("AsteroidRock", maps.GridTileToLocal(map.Grid.Owner, map.Grid.Comp, rockTile));
            var survey = Server.System<CMUCaveSurveySystem>();
            survey.Observe(actor);
            var memory = SEntMan.GetComponent<CMUCaveSurveyMemoryComponent>(actor);
            var data = memory.Worlds[network];
            Assert.That(data.Get(rockTile, -3).Material, Is.EqualTo(CMUReconMaterial.Rock));
            Assert.That(data.Get(new Vector2i(2, 0), -3).Revision, Is.Zero, "A rock face must hide the tile behind it.");
            Assert.That(stone.GeneratedChunks.Count, Is.EqualTo(4), "Recording must never generate more terrain.");
            Assert.That(survey.WriteMap(actor, paper), Is.True);
            SEntMan.DeleteEntity(rock);
            memory.NextObservation = TimeSpan.Zero;
            survey.Observe(actor);
            Assert.That(data.Get(rockTile, -3).Material, Is.EqualTo(CMUReconMaterial.Ground));
            Assert.That(survey.ReadMap(reader, paper), Is.True);
            var read = SEntMan.GetComponent<CMUCaveSurveyMemoryComponent>(reader);
            Assert.That(read.Worlds[network].Get(rockTile, -3).Material, Is.EqualTo(CMUReconMaterial.Rock),
                "A physical map must not track later changes automatically.");
            Assert.That(survey.ReadMap(actor, paper), Is.True);
            Assert.That(data.Get(rockTile, -3).Material, Is.EqualTo(CMUReconMaterial.Ground), "Reading old knowledge must not erase a newer observation.");
            Server.System<SharedTransformSystem>().SetCoordinates(reader, other.GridCoords);
            survey.Observe(reader);
            Assert.That(read.Worlds[network].Get(rockTile, -3).Material, Is.EqualTo(CMUReconMaterial.Rock));
            Assert.That(read.Worlds[otherNetwork].Get(rockTile, -3).Material, Is.Not.EqualTo(CMUReconMaterial.Rock),
                "Identical coordinates on different worlds must remain separate.");
            Assert.That(survey.ReadMap(reader, paper), Is.False, "A map on another world is not accessible.");
            SEntMan.DeleteEntity(actor);
            SEntMan.DeleteEntity(reader);
            SEntMan.DeleteEntity(paper);
        });
    }

    [Test]
    public async Task UpStairClearsGeneratedLandingAcrossChunkEdgeAndLadderDeploysInItsShaft()
    {
        var map = await Pair.CreateTestMap();
        EntityUid upper = default;
        EntityUid lower = default;
        EntityUid ceiling = default;
        var tile = new Vector2i(7, 7);
        var position = new Vector2(7.5f, 7.5f);
        await Server.WaitAssertion(() =>
        {
            var building = Server.System<ZLevelBuildingSystem>();
            var settings = SEntMan.EnsureComponent<ZBuildableMapComponent>(map.MapUid);
            settings.Caverns = null;
            settings.DepthBiomes.Clear();
            settings.StoneCaves = null;
            settings.StoneOreVeins.Clear();
            settings.StoneDeposits.Clear();
            settings.StoneCaveDecor.Clear();
            settings.StoneWater = null;
            Assert.That(building.EnsureNeighborLevel(map.MapUid, -1, map.Grid.Owner, position, out ceiling, out _), Is.True);
            SEntMan.EnsureComponent<ZGeneratedStoneComponent>(ceiling).StoneGrid = ceiling;
            Assert.That(building.EnsureNeighborLevel(ceiling, -1, ceiling, position, out upper, out _), Is.True);
            SEntMan.EnsureComponent<ZGeneratedStoneComponent>(upper).StoneGrid = upper;
            Assert.That(building.EnsureNeighborLevel(upper, -1, upper, position, out lower, out _), Is.True);
            SEntMan.EnsureComponent<ZGeneratedStoneComponent>(lower).StoneGrid = lower;
            building.PrepareCavernConstruction(ceiling, position);
            building.PrepareCavernConstruction(lower, position, clearRock: true);
        });
        await RunSeconds(0.1f);
        EntityUid stair = default;
        await Server.WaitAssertion(() => stair = SEntMan.SpawnEntity("AU14ZStairUp", new EntityCoordinates(lower, position)));
        await RunSeconds(0.1f);
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(upper);
            var stone = SEntMan.GetComponent<ZGeneratedStoneComponent>(upper);
            Assert.That(stone.GeneratedChunks.Contains(new Vector2i(1, 1)), Is.True, "The far side of the landing crosses a chunk edge.");
            Assert.That(maps.GetTileRef(upper, grid, tile).Tile.IsEmpty, Is.True, "Stairs need an open shaft above their lower end.");
            Assert.That(maps.GetTileRef(upper, grid, tile + new Vector2i(1, 1)).Tile.IsEmpty, Is.False);
            foreach (var entity in maps.GetAnchoredEntities(upper, grid, tile + new Vector2i(1, 1)))
                Assert.That(SEntMan.HasComponent<WallComponent>(entity), Is.False, "The landing must not remain buried in generated rock.");
            SEntMan.DeleteEntity(stair);
            var actor = SEntMan.SpawnEntity("MobHuman", new EntityCoordinates(lower, position));
            var ladder = SEntMan.SpawnEntity("CMUDeployableZLevelLadder", new EntityCoordinates(lower, position));
            var use = new UseInHandEvent(actor);
            SEntMan.EventBus.RaiseLocalEvent(ladder, use);
            var bottom = false;
            var top = false;
            // The top end is in empty space and cannot anchor to a floor tile.
            var deployed = SEntMan.EntityQueryEnumerator<CMUDeployedZLevelLadderComponent, TransformComponent>();
            while (deployed.MoveNext(out _, out _, out var xform))
            {
                bottom |= xform.MapUid == lower;
                top |= xform.MapUid == upper;
            }
            Assert.That(bottom && top, Is.True, "A deployable ladder must connect both generated cave levels.");
            SEntMan.DeleteEntity(actor);
            SEntMan.DeleteEntity(upper);
            SEntMan.DeleteEntity(lower);
            SEntMan.DeleteEntity(ceiling);
        });
    }
}
